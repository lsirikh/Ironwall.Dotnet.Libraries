# PRD: MBTiles 기반 DefinedMap 통합

- **Version**: 1.0
- **Date**: 2026-03-19
- **Status**: Draft
- **Language/Framework**: C# / WPF (.NET 8) + GMap.NET + Dapper + MariaDB

## 1. Background (배경)

### 현재 상황

```
AvailableMaps ComboBox
├── Google 지도 (Normal)     ← 온라인 필요
├── Google 위성 (Satellite)  ← 온라인 필요
├── Google 혼합 (Hybrid)     ← 온라인 필요
├── Bing 지도 (Normal)       ← 온라인 필요
├── OpenStreetMap             ← 온라인 필요
└── [커스텀 맵들]             ← TIF 기반 FileBasedCustomMapProvider
```

- 모든 DefinedMap이 **온라인 전용** (Google/Bing/OSM API 의존)
- 오프라인 환경(GOP 군 작전)에서 사용 불가
- CustomMap은 TIF 눈대중 맞춤 → 좌표 정밀도 낮음
- MBTiles 로드 테스트 성공 (삼송테크노벨리.mbtiles, 110타일, 즉시 렌더링)

### 동기

- GOP 운용 환경: **오프라인 전용** (인터넷 없음)
- V-World 타일 다운로더로 MBTiles 생성 파이프라인 구축 완료
- MBTiles를 기존 DefinedMap 시스템에 통합하여 콤보박스에서 선택 가능하게
- CustomMap은 기본 지도가 아닌 **오버레이 레이어 전용**으로 용도 재정의

## 2. Goals (목표)

### 핵심 목표

- [ ] MBTiles 파일을 DefinedMap으로 등록하여 콤보박스에서 선택 가능
- [ ] 위성지도 + 일반지도 2개 MBTiles 등록 지원
- [ ] 콤보박스 선택 시 MBTilesMapProvider로 자동 전환
- [ ] 기존 Google/Bing/OSM DefinedMap 데이터 정리 (운용 시 불필요)
- [ ] CustomMap을 오버레이 레이어 전용으로 역할 분리

### 비목표 (Out of Scope)

- MBTiles 파일 생성 (V-World 다운로더에서 처리)
- 레이어 시스템 연동 (별도 PRD_Layer_Panel_Tree_Redesign에서 처리)
- CustomMap 오버레이 UI (별도 PRD)

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 | 비고 |
|----|---------|---------|------|
| MB-01 | EnumMapVendor에 `MBTiles = 20` 추가 | Must | |
| MB-02 | ~~삭제~~ — 기존 `ServiceUrl` 컬럼을 MBTiles 파일명으로 재활용 (DDL 변경 없음) | - | |
| MB-03 | ~~삭제~~ — `ServiceUrl` 프로퍼티 이미 존재 (모델 변경 없음) | - | |
| MB-10 | 빌드 시 `Datas/*.mbtiles` 자동 복사 (CopyToOutputDirectory) | Must | .csproj |
| MB-04 | ConfigureDefinedMapAsync에서 Vendor==MBTiles 분기 추가 | Must | MBTilesMapProvider.Open() |
| MB-05 | InsertDefinedMapAsync/FetchDefinedMapsAsync에서 ServiceUrl 처리 | Must | |
| MB-06 | 콤보박스에서 MBTiles맵 선택 시 아이콘 구분 (PackIcon: Database) | Should | |
| MB-07 | 기존 온라인 DefinedMap 데이터 삭제 기능 (또는 SeedDefault에서 MBTiles만 등록) | Should | |
| MB-08 | MBTiles 파일 경로 유효성 검사 (File.Exists) | Must | |
| MB-09 | 앱 시작 시 MBTiles맵이 기본 선택되도록 설정 | Should | appsettings |
| MB-11 | Datas/ 폴더↔DB 동기화 (고아 정리 + 신규 등록 + 변경 감지) | Must | SeedMBTilesMapsAsync |
| MB-12 | 파일 없는 DB 엔트리 자동 삭제 (고아 정리) | Must | ServiceUrl 기준 비교 |
| MB-13 | 파일 수정일 > DB UpdatedAt 시 메타데이터 자동 갱신 | Must | bounds/zoom UPDATE |
| MB-14 | UpdateDefinedMapMetadataAsync DB 메서드 | Must | IGMapDbService 인터페이스 |

### SeedMBTilesMapsAsync 상세 흐름

```
앱 시작 → SeedMBTilesMapsAsync()
│
├── [1] Datas/ 폴더 스캔 → *.mbtiles 파일 목록
│
├── [2] DB에서 기존 MBTiles DefinedMap 조회 (Vendor==MBTiles)
│
├── [3] 고아 정리 (MB-12)
│      DB에 있지만 폴더에 파일 없음 → DELETE + Provider 제거
│
├── [4] 변경 감지 (MB-13)
│      DB에 있고 폴더에도 있음 + 파일수정일 > DB.UpdatedAt
│      → MBTiles Open → bounds/zoom 재읽기 → UpdateDefinedMapMetadataAsync
│
└── [5] 신규 등록
       폴더에 있지만 DB에 없음 → MBTiles Open → InsertDefinedMapAsync
       파일명으로 Style 결정: *satellite* → 위성지도, 나머지 → 일반지도
```

### 비기능 요구사항

- 성능: MBTiles 로드 100ms 이내 (SQLite Open + metadata 읽기)
- 호환성: 기존 MapLayers DB와 하위 호환 (MapId FK)

## 4. Technical Approach (기술 접근)

### 전체 구조 변경

```
변경 전:
AvailableMaps ComboBox
├── Google 지도 (Online, DefinedMap, Vendor=Google)
├── Bing 위성 (Online, DefinedMap, Vendor=Microsoft)
└── 커스텀 맵 (Offline, CustomMap, FileBasedProvider)

변경 후:
AvailableMaps ComboBox
├── 위성지도 (Offline, DefinedMap, Vendor=MBTiles, Style=Satellite)
├── 일반지도 (Offline, DefinedMap, Vendor=MBTiles, Style=Normal)
└── (Google/Bing은 개발 모드에서만 표시하거나 삭제)

오버레이 (레이어 패널에서 관리):
├── 군사지도.tif (CustomMap → 오버레이 전용)
└── 드론사진.png (ImageOverlay)
```

### 영향받는 컴포넌트

| 컴포넌트 | 변경 유형 | 설명 |
|----------|----------|------|
| `EnumMapVendor.cs` | 수정 | `MBTiles = 20` 추가 |
| `IDefinedMapModel.cs` | 수정 | `ServiceUrl` 프로퍼티 추가 |
| `DefinedMapModel.cs` | 수정 | `ServiceUrl` 구현 |
| `GMapDbService.cs` | 수정 | DDL ALTER + CRUD에 ServiceUrl 포함 |
| `MapViewModel.cs` | 수정 | ConfigureDefinedMapAsync에 MBTiles 분기 |
| `MapView.xaml` | 수정 | 콤보박스 아이콘 Vendor별 구분 (선택) |

### DB 스키마 변경

```sql
-- DefinedMaps 테이블에 MBTiles 파일명 컬럼 추가 (경로 아님, 파일명만)
ALTER TABLE DefinedMaps
ADD COLUMN ServiceUrl VARCHAR(200) DEFAULT NULL
AFTER ServiceUrl;
```

### MBTiles 파일 경로 규칙

```
실행 경로:  bin/Debug/net8.0-windows7.0/
기본 경로:  {실행 경로}/Datas/
위성지도:   Datas/map_satellite.mbtiles
일반지도:   Datas/map_base.mbtiles

경로 조합: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas", fileName)
```

- `ServiceUrl`에는 파일명만 저장 (예: `map_satellite.mbtiles`)
- 기본 경로 `Datas/`는 코드에서 고정
- 빌드 시 `.csproj` CopyToOutputDirectory로 자동 복사

### 빌드 시 자동 복사 (.csproj)

```xml
<!-- Dotnet.Monitoring.Solution.csproj -->
<ItemGroup>
  <None Include="Datas\*.mbtiles">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 데이터 등록 예시

```sql
-- 위성지도 등록
INSERT INTO Maps (Name, ProviderType, Category, CoordinateSystem, MinZoomLevel, MaxZoomLevel)
VALUES ('위성지도', 'Defined', 'Satellite', 'WGS84', 15, 18);

INSERT INTO DefinedMaps (MapId, GMapProviderName, Vendor, Style, ServiceUrl)
VALUES (LAST_INSERT_ID(), 'MBTilesMapProvider', 'MBTiles', 'Satellite',
        'map_satellite.mbtiles');

-- 일반지도 등록
INSERT INTO Maps (Name, ProviderType, Category, CoordinateSystem, MinZoomLevel, MaxZoomLevel)
VALUES ('일반지도', 'Defined', 'Standard', 'WGS84', 15, 18);

INSERT INTO DefinedMaps (MapId, GMapProviderName, Vendor, Style, ServiceUrl)
VALUES (LAST_INSERT_ID(), 'MBTilesMapProvider', 'MBTiles', 'Normal',
        'map_base.mbtiles');
```

### MBTiles 메타데이터 → DB 등록 흐름

```
[앱 시작 — MapConfigureAsync]

1. GMapDbService.StartService()
   → FetchInstanceAsync() → _mapProvider 로드 (최초엔 비어있음)

2. MapConfigureAsync() → SeedMBTilesMapsAsync()
   ↓
   DB에 Vendor=MBTiles DefinedMap 있는지 확인
   ↓ 있으면 → 건너뜀
   ↓ 없으면
   Datas/ 폴더 스캔 → .mbtiles 파일 목록
   ↓ 각 파일마다
   MBTilesMapProvider.Instance.Open(path) → 메타데이터 읽기
   ↓
   DefinedMapModel 생성 (메타데이터 기반):
     Maps:
       Name        = provider.DataName 또는 파일명에서 추출
       Description = "MBTiles 오프라인 지도 (파일명)"
       Category    = 파일명에 "satellite" 포함 → Satellite, 아니면 Standard
       DataType    = Raster
       EpsgCode    = "EPSG:3857"
       MinLatitude = provider.Bounds[1].Lat  (남쪽)
       MaxLatitude = provider.Bounds[0].Lat  (북쪽)
       MinLongitude= provider.Bounds[0].Lng  (서쪽)
       MaxLongitude= provider.Bounds[1].Lng  (동쪽)
       MinZoomLevel= provider.MinZoom
       MaxZoomLevel= provider.MaxZoom
       TileSize    = 256
       CreatedBy   = "System"
     DefinedMaps:
       GMapProviderName = "MBTilesMapProvider"
       ProviderGuid     = "CD2A114E-188C-423F-BBCC-FB7849333AE4"
       Vendor           = MBTiles
       Style            = Satellite 또는 Normal
       ServiceUrl       = 파일명 ("map_satellite.mbtiles")
   ↓
   InsertDefinedMapAsync(model) → DB 저장
   _mapProvider.Add(model) → Provider 목록에 즉시 추가 (재로드 불필요)

3. SelectedMap 검색
   → appsettings.MapName으로 검색
   → 없으면 MBTiles 맵 중 첫 번째 선택 (폴백)
   → 그래도 없으면 아무 맵 선택

4. ConfigureDefinedMapAsync(Vendor=MBTiles)
   → ConfigureMBTilesMap()
   → Datas/{ServiceUrl} 경로로 MBTilesMapProvider.Open()
   → MainMap.MapProvider 설정 → 맵 표시
```

### ConfigureDefinedMapAsync 변경

```csharp
private async Task ConfigureDefinedMapAsync(DefinedMapModel definedMap)
{
    switch (definedMap.Vendor)
    {
        case EnumMapVendor.MBTiles:
            var mbtilesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Datas",
                definedMap.ServiceUrl ?? "");
            if (!File.Exists(mbtilesPath))
            {
                _log?.Error($"MBTiles 파일 없음: {mbtilesPath}");
                break;
            }
            var provider = MBTilesMapProvider.Instance;
            if (provider.Open(mbtilesPath))
            {
                MainMap.MapProvider = provider;
                MainMap.Manager.Mode = AccessMode.ServerOnly;
                if (provider.CenterLocation != PointLatLng.Empty)
                    MainMap.Position = provider.CenterLocation;
            }
            break;

        case EnumMapVendor.Google:
            // 기존 로직 유지 (개발 모드용)
            ...
    }
}
```

### 콤보박스 아이콘 구분

```xml
<md:PackIcon Kind="{Binding Vendor, Converter={StaticResource VendorToIconConverter}}" />
<!-- MBTiles → Database, Google → GoogleMaps, Bing → MicrosoftBing -->
```

### 의존성

- `GMap.NET.Core`: MBTilesMapProvider (이미 내장, `#if SQLite` 조건부)
- `System.Data.SQLite`: MBTiles SQLite 읽기 (이미 참조됨)

## 5. Test Strategy (테스트 전략)

### 단위 테스트

| 테스트 | 검증 내용 |
|--------|----------|
| `InsertDefinedMap_MBTiles_WithFilePath` | ServiceUrl 포함 Insert + Fetch |
| `FetchDefinedMaps_MBTiles_HasFilePath` | Vendor=MBTiles 레코드의 ServiceUrl != null |
| `ConfigureMBTiles_FileNotFound_LogsError` | 파일 없을 때 에러 로그 |
| `ConfigureMBTiles_ValidFile_ProviderSet` | 유효한 MBTiles → MapProvider 전환 확인 |

### 검증 기준

- [ ] 모든 단위 테스트 통과
- [ ] MBTiles 맵이 콤보박스에 표시
- [ ] 콤보박스 선택 시 즉시 맵 전환
- [ ] 앱 재시작 후 MBTiles 맵 유지
- [ ] 빌드 경고 0개

## 6. Risks & Mitigations (리스크)

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| MBTiles 파일 경로 변경 시 로드 실패 | High | File.Exists 검사 + 사용자 알림 |
| MBTilesMapProvider 싱글턴 → 동시 2개 맵 불가 | Medium | 위성/일반 전환 시 .Open() 재호출 |
| System.Data.SQLite DLL 누락 | Medium | 빌드 시 Copy Local 확인 |
| 기존 DefinedMaps 데이터와 충돌 | Low | DB 마이그레이션 시 기존 데이터 유지, ServiceUrl는 NULL 허용 |

## 7. References (참고)

- [GMap_MapProviding_DeepAnalysis.md](../GMap_MapProviding_DeepAnalysis.md) — 맵 프로바이더 분석
- [GMap_Projects_Analysis.md](../GMap_Projects_Analysis.md) — 전체 GMap 프로젝트 분석
- [project_offline_map_strategy.md](../../.claude/.../memory/project_offline_map_strategy.md) — 오프라인 맵 전략
- MBTilesMapProvider: `GMap.NET.Core/MapProviders/Etc/MBTilesMapProvider.cs`
- V-World 다운로더: `C:\workspace_python\vworld-tile-downloader\`
