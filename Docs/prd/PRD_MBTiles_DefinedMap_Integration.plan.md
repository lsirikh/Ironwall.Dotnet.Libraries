# TDD Plan: MBTiles 기반 DefinedMap 통합

- **PRD**: Docs/prd/PRD_MBTiles_DefinedMap_Integration.md
- **Date**: 2026-03-19
- **Status**: Not Started

---

## Phase 1: Enum + 모델 (Structural)

> MB-01, MB-03

- [ ] **1.1**: EnumMapVendor에 MBTiles 추가
  - Target: `Ironwall.Dotnet.Libraries.Enums/EnumMapVendor.cs`
  - 구현: `MBTiles = 20` 항목 추가 (Display="MBTiles", Description="오프라인 MBTiles 타일 DB")
  - 빌드 확인

- [ ] **1.2**: IDefinedMapModel + DefinedMapModel에 ServiceUrl(파일명) 추가
  - Target: `Ironwall.Dotnet.Monitoring.Models/Maps/IDefinedMapModel.cs`
  - Target: `Ironwall.Dotnet.Monitoring.Models/Maps/DefinedMapModel.cs`
  - 구현: `string? ServiceUrl(파일명) { get; set; }` 프로퍼티 추가
  - 빌드 확인

---

## Phase 2: DB 스키마 + CRUD (Structural + Behavioral)

> MB-02, MB-05

- [ ] **2.1**: GMapDbService — BuildSchemeAsync에 ServiceUrl(파일명) 컬럼 추가
  - Target: `GMaps.Db/Services/GMapDbService.cs`
  - 구현: DefinedMaps CREATE TABLE에 `ServiceUrl(파일명) VARCHAR(200) DEFAULT NULL` 추가
  - 빌드 확인

- [ ] **2.2**: GMapDbService — Insert/Fetch에 ServiceUrl(파일명) 포함
  - Target: `GMaps.Db/Services/GMapDbService.cs`
  - 구현:
    - InsertDefinedMapAsync: INSERT에 ServiceUrl(파일명) 컬럼 추가
    - FetchDefinedMapsAsync: SELECT에 ServiceUrl(파일명) 포함
    - DefinedMapSQL DTO에 ServiceUrl(파일명) 필드 추가
  - 빌드 확인

- [ ] **Test 2.3**: InsertDefinedMap_MBTiles — MBTiles DefinedMap Insert + Fetch 검증
  - File: `GMaps.Db/Tests/UnitTestMap.cs`
  - Red: Vendor=MBTiles, ServiceUrl(파일명)="map_satellite.mbtiles" Insert → Fetch → 값 확인
  - Green: Phase 2.1~2.2 구현으로 통과
  - Assert: ServiceUrl(파일명) != null, Vendor == "MBTiles"

---

## Phase 3: MapViewModel — MBTiles Provider 전환 (Behavioral)

> MB-04, MB-08

- [ ] **3.1**: ConfigureDefinedMapAsync에 MBTiles 분기 추가
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - `case EnumMapVendor.MBTiles:` 분기
    - `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", fileName)`
    - `File.Exists` 검사
    - `MBTilesMapProvider.Instance.Open(path)`
    - `MainMap.MapProvider = provider`
    - `MainMap.Manager.Mode = AccessMode.ServerOnly`
    - 메타데이터 기반 Position/Zoom 설정
  - 기존 LoadMBTilesMap() 메서드와 통합 (중복 제거)
  - 빌드 확인

- [ ] **3.2**: LoadMBTilesMap 메서드 정리 — ConfigureDefinedMapAsync로 통합
  - Target: `MapViewModel.cs`
  - 구현: LoadMBTilesMap() 삭제 또는 ConfigureDefinedMapAsync 내부 호출로 변경
  - RestoreOnlineMap()은 유지 (개발 모드 복귀용)
  - 빌드 확인

---

## Phase 4: DB 데이터 등록 + 콤보박스 연동 (Behavioral)

> MB-07, MB-09

- [ ] **4.1**: SeedMBTilesMapsFromFolder — Datas/ 폴더 스캔 → 메타데이터 읽기 → DB 등록
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - `SeedMBTilesMapsAsync()` 메서드 (MapViewModel에 위치)
    - Datas/ 폴더에서 *.mbtiles 파일 스캔
    - 각 파일 MBTilesMapProvider.Open() → metadata 추출
    - DefinedMapModel에 bounds/zoom/name 정확히 채움
    - InsertDefinedMapAsync() 호출
    - 기존 온라인 DefinedMaps 삭제
    - 일반지도: Name="일반지도", Vendor=MBTiles, Style=Normal, ServiceUrl(파일명)="map_base.mbtiles"
    - 이미 존재하면 건너뜀 (INSERT IGNORE 또는 SELECT 확인)
  - IGMapDbService에 메서드 시그니처 추가
  - 빌드 확인

- [ ] **4.2**: MapViewModel — 앱 시작 시 MBTiles맵 기본 선택
  - Target: `MapViewModel.cs`
  - 구현:
    - SeedDefaultMBTilesMapsAsync() 호출 (GMapDbService StartService 또는 MapViewModel 초기화)
    - AvailableMaps에 MBTiles 맵이 포함되도록 확인
    - 기본 SelectedMap을 MBTiles 위성지도로 설정
  - 빌드 확인

---

## Phase 5: 빌드 복사 + 최종 검증

> MB-10

- [ ] **5.1**: Datas 폴더 생성 + MBTiles 복사 설정
  - Target: `Dotnet.Monitoring.Solution/Dotnet.Monitoring.Solution.csproj` (또는 라이브러리 csproj)
  - 구현:
    - Datas/ 폴더 생성
    - 삼송테크노벨리.mbtiles → Datas/map_satellite.mbtiles 복사
    - .csproj에 CopyToOutputDirectory 설정
  - 빌드 확인: bin/Debug/.../Datas/map_satellite.mbtiles 존재

- [ ] **5.2**: 전체 빌드 확인 — 오류 0개
- [ ] **5.3**: UI 수동 검증
  - 앱 시작 → 콤보박스에 "위성지도" 표시
  - 콤보박스에서 "위성지도" 선택 → MBTiles 맵 로드
  - 줌 15~18 범위에서 타일 표시 확인
  - 앱 재시작 → 이전 선택 유지

---

## 실행 순서

```
Phase 1 (Enum + 모델)
    ↓
Phase 2 (DB 스키마 + CRUD + 테스트)
    ↓
Phase 3 (MapViewModel MBTiles 분기)
    ↓
Phase 4 (SeedDefault + 콤보박스 연동)
    ↓
Phase 5 (빌드 복사 + 검증)
```

---

## Phase 6: Datas↔DB 동기화 (고아 정리 + 변경 감지)

> MB-11, MB-12, MB-13, MB-14

- [ ] **6.1**: IGMapDbService — UpdateDefinedMapMetadataAsync 인터페이스 추가
  - Target: `GMaps.Db/Services/IGMapDbService.cs`
  - 구현: `Task UpdateDefinedMapMetadataAsync(int mapId, double minLat, double maxLat, double minLng, double maxLng, int minZoom, int maxZoom)`
  - 빌드 확인

- [ ] **6.2**: GMapDbService — UpdateDefinedMapMetadataAsync 구현
  - Target: `GMaps.Db/Services/GMapDbService.cs`
  - 구현: Maps 테이블 UPDATE (MinLatitude, MaxLatitude, MinLongitude, MaxLongitude, MinZoomLevel, MaxZoomLevel, UpdatedAt)
  - 빌드 확인

- [ ] **6.3**: SeedMBTilesMapsAsync 리팩토링 — 3단계 동기화 로직
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    1. 고아 정리: DB에 있지만 폴더에 파일 없는 엔트리 → DELETE
    2. 변경 감지: 파일 수정일 > DB UpdatedAt → 메타데이터 UPDATE
    3. 신규 등록: 폴더에 있지만 DB에 없는 파일 → INSERT
  - ~~기존 `if (existing MBTiles) return;` 전체 스킵 제거~~
  - 빌드 확인

- [ ] **6.4**: 빌드 + UI 검증
  - 앱 시작 → "고양시일부" 엔트리 자동 삭제 (파일 없음)
  - map_satellite.mbtiles, map_base.mbtiles 자동 등록
  - 콤보박스에 "위성지도", "일반지도" 표시
  - 파일 교체 후 재시작 → 메타데이터 갱신 확인

---

## 실행 순서

```
Phase 1~5 (기존 — 완료됨)
    ↓
Phase 6 (Datas↔DB 동기화 — 진행 중)
```

**총 14개 체크박스 | Phase 6: 신규 4개**
