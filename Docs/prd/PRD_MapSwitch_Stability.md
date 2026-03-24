# PRD: 맵 전환 안정성 개선

- **Version**: 2.0
- **Date**: 2026-03-21
- **Status**: In Progress
- **Language/Framework**: C# / WPF (.NET 8) + GMap.NET

---

## 1. Background (배경)

### 버그 목록

| # | 버그 | 심각도 | 상태 |
|---|------|--------|------|
| 1 | 이벤트 핸들러 누적 (전환마다 += 반복) | HIGH | ✅ 해결 (dcaffac) |
| 2 | Fire-and-forget Race Condition | MEDIUM | ✅ 해결 (dcaffac) |
| 3 | MBTiles SQLite 리소스 누수 | MEDIUM | ✅ 해결 (dcaffac) |
| 4 | 싱글턴 Provider 참조 미변경 → 타일 겹침 | CRITICAL | ✅ 해결 (9d35e65) |
| 5 | 콤보박스 초기 선택 빈칸 | LOW | ✅ 해결 (5d43950) |
| 6 | 맵 전환 시 위치/줌 리셋 | MEDIUM | ✅ 해결 (abfd24e) |
| 7 | 초기 로드 시 HomePosition으로 시작 안 됨 | MEDIUM | 미해결 |

### Bug #1~#6 상세 (해결 완료)

**Bug #1: 이벤트 핸들러 누적**
```csharp
// ConfigureCommonMapSettings() — 전환마다 호출
MainMap.OnPositionChanged += handler;  // -= 없이 += 만 반복
// → 전환 5번 → 핸들러 5개 중복 실행
// 수정: -= 선행 추가
```

**Bug #2: Race Condition**
```csharp
_ = ChangeMapAsync(value);  // fire-and-forget → 빠른 클릭 시 2개 동시 실행
// 수정: SemaphoreSlim(1,1) + WaitAsync(0) → 이미 전환 중이면 스킵
```

**Bug #3: SQLite 누수**
```csharp
source = new MBTiles(path);  // 이전 source Close 없이 교체
// 수정: source?.Close() 선행 호출
```

**Bug #4: 타일 겹침 (가장 심각)**
```
MBTilesMapProvider.Instance = 싱글턴 (항상 같은 참조)
→ GMap.NET: "Provider 안 바뀌었으니 리로드 안 함"
→ KiberTileCache에 이전 타일 유지 → 겹침

수정: EmptyProvider 임시 전환 + MemoryCache.Clear() + ReloadMap()
```

**Bug #5: 콤보박스 빈칸**
```
MapConfigureAsync에서 SelectedMap 설정 후 NotifyOfPropertyChange 누락
수정: NotifyOfPropertyChange(nameof(SelectedMapItem)) 추가
```

**Bug #6: 전환 시 위치/줌 리셋**
```
전환마다 Position = MBTiles center로 이동
수정: isInitialLoad 파라미터 — 전환 시에는 현재 위치/줌 유지
```

---

### Bug #7: 초기 로드 시 HomePosition + 콤보박스 빈칸 (미해결)

#### 증상 2가지
1. 앱 시작 시 콤보박스 빈칸 (선택 표시 안 됨)
2. HomePosition이 아닌 MBTiles center에서 시작

#### 디버깅 — 실제 로그 추적 (2026-03-23)

```
16:37:50,061 FetchDefinedMapsAsync 완료 - 2건           ← DB에서 맵 2개 로드 ✅
16:37:50,067 [MapSwitch] 전환 시작: None → map_base.mbtiles (isInitialLoad=True)
16:37:50,081 [MapSwitch] Step 1: EmptyProvider 전환
16:37:50,085 [MapSwitch] Step 2: MemoryCache 클리어
16:37:50,121 [MapSwitch] Step 3: Open(map_base.mbtiles) 성공
16:37:50,126 [MapSwitch] Step 4: MapProvider 설정 완료
16:37:50,132 [MapSwitch] Step 5: 초기 로드 → MBTiles center (37.48°N, zoom=13)
16:37:50,161 ⚠ 기존 제공자 지도 설정 실패:
             "Please, do not call ReloadMap before form is loaded, it's useless"
16:37:50,189 ⚠ 지도 설정 실패 (같은 예외)
```

#### 코드 흐름 (예외 발생 경로)

```
OnActivateAsync
  → MapConfigureAsync(isInitialLoad: true)
    → SeedMBTilesMapsAsync() ✅
    → SelectedMap = "일반지도" ✅
    → ConfigureDefinedMapAsync(definedMap, isInitialLoad: true)
      → ConfigureMBTilesMap(definedMap, isInitialLoad: true)
        → Step 1~4: MBTiles Open ✅
        → Step 5: Position = MBTiles center ✅
        → Step 6: ReloadMap() → 💥 예외! (폼 미로드)
      → catch → WARN 로그 → return ← 여기서 빠져나감

    → ConfigureCommonMapSettings(isInitialLoad) ← ❌ 실행 안 됨 (예외로 스킵)
    → NotifyOfPropertyChange(SelectedMapItem)   ← ❌ 실행 안 됨 (예외로 스킵)
```

#### 근본 원인

1. **ReloadMap 예외:** `OnActivateAsync` 시점에 WPF 폼이 아직 렌더링 안 됨
   → GMap.NET이 "form is loaded 전에 ReloadMap 호출하지 마라" 예외
2. **예외 전파:** ConfigureMBTilesMap → ConfigureDefinedMapAsync → MapConfigureAsync
   → catch에서 WARN만 찍고 return
3. **후속 로직 전부 스킵:** ConfigureCommonMapSettings(HomePosition 이동) + NotifyOfPropertyChange(콤보박스 선택)

#### 수정 방향

```
[수정 1] ReloadMap을 try-catch로 감싸서 예외가 후속 로직을 차단하지 않도록
  ConfigureMBTilesMap 내부의 ReloadMap() 호출을:
    try { MainMap.ReloadMap(); }
    catch { _log?.Warn("ReloadMap 실패 — 폼 로드 후 자동 리로드"); }
  → ConfigureCommonMapSettings + NotifyOfPropertyChange 정상 실행

[수정 2] isInitialLoad=true일 때 ReloadMap 자체를 호출하지 않음
  → 폼 로드 후 GMap.NET이 자동으로 타일을 로드하므로 불필요
  → Step 6 스킵

[수정 3] ConfigureCommonMapSettings(isInitialLoad) — 이미 구현 완료
  → isInitialLoad=true: HomePosition으로 이동
  → isInitialLoad=false: Position 유지

[수정 2 채택] — isInitialLoad=true일 때 ReloadMap 스킵이 가장 안전
```
    Position = HomePosition (초기 로드 + 비-MBTiles 공통)
```

---

## 2. Goals (목표)

### 핵심 목표
- [x] Bug #1: 이벤트 핸들러 -= 선행 — ✅ Phase 1
- [x] Bug #2: SemaphoreSlim Race condition 방지 — ✅ Phase 2
- [x] Bug #3: MBTiles SQLite Close 선행 — ✅ Phase 3
- [x] Bug #4: EmptyProvider + 캐시 클리어 + ReloadMap — ✅ Phase 5
- [x] Bug #5: NotifyOfPropertyChange 추가 — ✅ Phase 5.5
- [x] Bug #6: isInitialLoad로 위치/줌 유지 — ✅ Phase 8
- [ ] **Bug #7: 초기 로드 시 HomePosition 시작** — Phase 8.5 (신규)

### 비목표
- 맵 전환 애니메이션/트랜지션 효과
- 맵 전환 시 심볼 재배치 로직

---

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 | 상태 |
|----|---------|---------|------|
| MS-01 | ConfigureCommonMapSettings -= 선행 | Must | ✅ |
| MS-02 | ChangeMapAsync SemaphoreSlim | Must | ✅ |
| MS-03 | MBTiles Open() 전 Close() | Must | ✅ |
| MS-04 | MBTiles Close()/Dispose() 추가 | Must | ✅ |
| MS-05 | 전환 후 마커 유지 검증 | Should | 미검증 |
| MS-06 | EmptyProvider + 캐시 클리어 + ReloadMap | Must | ✅ |
| MS-07 | [MapSwitch] 디버깅 로그 | Must | ✅ |
| MS-08 | isInitialLoad 위치/줌 유지 | Must | ✅ |
| **MS-09** | **초기 로드 시 HomePosition 시작** | **Must** | **신규** |

---

## 4. Technical Approach (기술 접근)

### 전체 맵 전환 흐름 (현재 수정 완료 상태)

```
[콤보박스 전환]
SelectedMapItem setter
    → ChangeMapAsync(targetMap)
    → _mapSwitchLock.WaitAsync(0) — Race condition 방지
    → MapConfigureAsync(isInitialLoad: false)
        → ConfigureMBTilesMap(definedMap, isInitialLoad: false)
            → Step 1: EmptyProvider 임시 전환
            → Step 2: MemoryCache.Clear()
            → Step 3: source?.Close()
            → Step 4: MBTilesMapProvider.Open(newPath)
            → Step 5: MapProvider = MBTilesMapProvider.Instance
            → Step 6: ReloadMap()
            → 위치/줌 유지 (isInitialLoad=false)
        → ConfigureCommonMapSettings()
            → -= 해제 + += 구독 (핸들러 1개 보장)
            → MBTiles 분기: MinZoom/MaxZoom만 설정
    → SaveCurrentMapSettingsAsync()
    → _mapSwitchLock.Release()
```

```
[앱 시작]
OnActivateAsync
    → MapConfigureAsync(isInitialLoad: true)
        → SeedMBTilesMapsAsync()
        → ConfigureMBTilesMap(definedMap, isInitialLoad: true)
            → Step 1~6 동일
            → Position = MBTiles center ← ★ Bug #7: 여기가 문제
        → ConfigureCommonMapSettings()
            → MBTiles 분기: Position 안 건드림 ← HomePosition 스킵
```

### MS-09 수정 상세

```csharp
// 변경 전
private void ConfigureCommonMapSettings()
{
    if (SelectedMap is DefinedMapModel dm && dm.Vendor == EnumMapVendor.MBTiles)
    {
        MainMap.MinZoom = SelectedMap.MinZoomLevel;
        MainMap.MaxZoom = SelectedMap.MaxZoomLevel;
    }
    else
    {
        MainMap.Position = _setupModel.HomePosition?.PointLatLng ?? ...;
        MainMap.Zoom = _setupModel.HomePosition?.Zoom ?? DEFAULT_ZOOM;
    }
}

// 변경 후
private void ConfigureCommonMapSettings(bool isInitialLoad = false)
{
    if (SelectedMap is DefinedMapModel dm && dm.Vendor == EnumMapVendor.MBTiles && !isInitialLoad)
    {
        // 전환 시: Position/Zoom 유지, MinZoom/MaxZoom만 설정
        MainMap.MinZoom = SelectedMap.MinZoomLevel;
        MainMap.MaxZoom = SelectedMap.MaxZoomLevel;
    }
    else
    {
        // 초기 로드 + 비-MBTiles: HomePosition으로 이동
        MainMap.Position = _setupModel.HomePosition?.PointLatLng ?? ...;
        MainMap.MinZoom = SelectedMap.MinZoomLevel;
        MainMap.MaxZoom = SelectedMap.MaxZoomLevel;
        MainMap.Zoom = _setupModel.HomePosition?.Zoom ?? DEFAULT_ZOOM;
    }
}
```

```
호출 체인:
MapConfigureAsync(isInitialLoad)
    → ConfigureDefinedMapAsync(definedMap, isInitialLoad)
    → ConfigureCommonMapSettings(isInitialLoad)  ← 파라미터 전달
```

### 영향받는 컴포넌트

| 컴포넌트 | 변경 유형 | 설명 |
|----------|----------|------|
| `MapViewModel.cs` | 수정 | ConfigureCommonMapSettings에 isInitialLoad 파라미터 |
| `MBTilesMapProvider.cs` | 수정 완료 | Open() Close 선행 (이미 완료) |

---

## 5. Test Strategy (테스트 전략)

| 항목 | 검증 방법 |
|------|----------|
| 이벤트 핸들러 1회 | 맵 5회 전환 후 줌 변경 → 로그 1회 |
| Race condition | 빠른 연속 클릭 → 1회만 실행 |
| SQLite 해제 | 10회 전환 → ObjectDisposedException 없음 |
| 타일 겹침 없음 | 10회 전환 → 깨끗한 전환 |
| 전환 시 위치 유지 | 위성→일반 → 같은 위치/줌 |
| **초기 HomePosition** | **앱 시작 → HomePosition(37.648°N) 표시** |

---

## 6. Risks & Mitigations (리스크)

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| GMap.NET 내부 수정 | Medium | MBTiles Close() 추가만 (최소 수정) |
| SemaphoreSlim 데드락 | Low | WaitAsync(0) 비블로킹 |
| isInitialLoad 전파 누락 | Medium | 호출 체인 3곳만 전달 |

---

## 7. References (참고)
- PRD_MBTiles_DefinedMap_Integration.md
- MapViewModel.cs (5000+ lines)
- MBTilesMapProvider.cs (~300 lines)
