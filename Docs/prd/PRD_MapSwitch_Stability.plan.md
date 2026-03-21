# TDD Plan: 맵 전환 안정성 개선

- **PRD**: Docs/prd/PRD_MapSwitch_Stability.md
- **Date**: 2026-03-21
- **Status**: Not Started

---

## Phase 1: 이벤트 핸들러 누적 버그 수정 (MS-01)

- [ ] **1.1**: ConfigureCommonMapSettings — += 전에 -= 선행
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - `MainMap.OnPositionChanged -= MainMap_OnCurrentPositionChanged;`
    - `MainMap.MouseMove -= MainMap_MouseMove;`
    - `MainMap.MouseLeftButtonDown -= MainMap_MouseLeftButtonDown;`
    - `MainMap.OnMapZoomChanged -= MainMap_OnMapZoomChanged;`
    - 이후 기존 += 유지
  - 검증: 맵 5회 전환 후 줌 변경 → 로그 1회만 출력
  - 빌드 확인

---

## Phase 2: Fire-and-forget Race Condition 방지 (MS-02)

- [ ] **2.1**: ChangeMapAsync에 SemaphoreSlim 적용
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - 필드: `private readonly SemaphoreSlim _mapSwitchLock = new(1, 1);`
    - `ChangeMapAsync` 진입 시 `WaitAsync(0)` → false면 return (이미 전환 중)
    - try/finally로 Release 보장
  - 검증: 빠른 연속 클릭 시 MapConfigureAsync 1회만 실행
  - 빌드 확인

---

## Phase 3: MBTiles SQLite 리소스 누수 수정 (MS-03, MS-04)

- [ ] **3.1**: MBTiles 클래스에 Close() 메서드 추가
  - Target: `GMap.NET/GMap.NET/GMap.NET.Core/MapProviders/Etc/MBTilesMapProvider.cs`
  - 구현:
    - `MBTiles.Close()` 메서드: db.Close() + db.Dispose() + db=null
    - try-catch로 ObjectDisposedException 방지
  - 빌드 확인

- [ ] **3.2**: MBTilesMapProvider.Open()에서 이전 source Close 추가
  - Target: `GMap.NET/GMap.NET/GMap.NET.Core/MapProviders/Etc/MBTilesMapProvider.cs`
  - 구현:
    - `source = new MBTiles(...)` 전에 `source?.Close()` 호출
  - 검증: 위성↔일반 10회 전환 → ObjectDisposedException 없음
  - 빌드 확인

---

## Phase 4: ~~최종 검증~~ → Phase 5로 이동

*(Phase 1~3 완료됨, Bug #4 발견으로 Phase 5~7 추가)*

---

## Phase 5: 싱글턴 캐시 겹침 수정 (MS-06) — 핵심 버그

- [ ] **5.1**: ConfigureMBTilesMap 리팩토링 — 캐시 초기화 + 강제 리로드
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    1. `MainMap.MapProvider = EmptyProvider.Instance` (임시 전환)
    2. `GMaps.Instance.MemoryCache.Clear()` (타일 캐시 초기화)
    3. `source?.Close()` (이전 SQLite 해제)
    4. `MBTilesMapProvider.Instance.Open(path)` (새 MBTiles 열기)
    5. `MainMap.MapProvider = MBTilesMapProvider.Instance` (Provider 재설정)
    6. `MainMap.ReloadMap()` (강제 리로드)
  - 빌드 확인

---

## Phase 6: 디버깅 로그 추가 (MS-07)

- [ ] **6.1**: ConfigureMBTilesMap 내부에 Step별 로그 추가
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - 전환 시작/완료, 각 Step 로그
    - MemoryCache 삭제 타일 수
    - Provider 참조 변경 확인
    - 파일명, Zoom, Center 기록
  - 빌드 확인

---

## Phase 7: 최종 검증

- [ ] **7.1**: 전체 빌드 확인 — 오류 0개
- [ ] **7.2**: UI 수동 검증
  - 위성↔일반 10회 전환 → 타일 겹침 없음
  - 전환 시 이전 맵 완전히 사라짐
  - 이벤트 중복 없음 (로그 확인)
  - 빠른 연속 클릭 → 정상 동작
  - 전환 전후 심볼 마커 유지 확인
  - 디버깅 로그 정상 출력

---

## 실행 순서

```
Phase 1~3 (완료 — 이벤트/Race/SQLite)
    ↓
Phase 5 (캐시 겹침 수정 — 핵심)
    ↓
Phase 6 (디버깅 로그)
    ↓
Phase 7 (최종 검증)
```

**총 Phase 1~3 완료, Phase 5~7 신규 (5개 체크박스)**

---

*(아래는 이전 Phase 4 기록)*

```
Phase 1 (이벤트 핸들러 -= 추가)
    ↓
Phase 2 (SemaphoreSlim Race condition 방지)
    ↓
Phase 3 (MBTiles SQLite Close)
    ↓
Phase 4 (빌드 + UI 검증)
```

**총 6개 체크박스 | 수정 2개 파일 (MapViewModel.cs, MBTilesMapProvider.cs)**
