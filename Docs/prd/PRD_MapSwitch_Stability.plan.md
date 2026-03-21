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

## Phase 4: 최종 검증

- [ ] **4.1**: 전체 빌드 확인 — 오류 0개
- [ ] **4.2**: UI 수동 검증
  - 맵 전환 10회 반복 → 이벤트 중복 없음 (로그 확인)
  - 빠른 연속 클릭 → 정상 동작 (Race condition 없음)
  - 위성↔일반 전환 → SQLite 에러 없음
  - 전환 전후 심볼 마커 유지 확인

---

## 실행 순서

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
