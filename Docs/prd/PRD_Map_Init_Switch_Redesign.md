# PRD: 맵 초기화/전환 프로세스 재설계

- **Version**: 1.0
- **Date**: 2026-03-23
- **Status**: Draft
- **Language/Framework**: C# / WPF (.NET 8) + GMap.NET

---

## 1. Background (배경)

### GMap.NET 내부 생명주기

```
[GMapControl 생명주기]

GMapControl 생성 (XAML 파싱)
  → _core.IsStarted = false
  → MapProvider 설정 가능 (단, 타일 로딩 안 됨)
  → Position/Zoom 설정 가능 (값만 저장)
    ↓
GMapControl_Loaded (WPF Loaded 이벤트)
  → _core.OnMapOpen()
  → _core.IsStarted = true ← ★ 이 시점부터 타일 로딩 시작
  → ForceUpdateOverlays()
  → 현재 설정된 Provider + Position + Zoom으로 타일 자동 로드
    ↓
이후:
  → ReloadMap() 호출 가능 (IsStarted=true이므로)
  → MapProvider 변경 → 자동 타일 리로드
```

```
[ReloadMap 내부 — Core.cs:588]

public void ReloadMap()
{
    if (IsStarted)
    {
        CancelAsyncTasks();
        Matrix.ClearAllLevels();    // 타일 매트릭스 초기화
        FailedLoads.Clear();
        Refresh.Set();              // 타일 리프레시 트리거
        UpdateBounds();
    }
    else
    {
        throw new Exception("Please, do not call ReloadMap before form is loaded");
    }
}
```

### 현재 타이밍 문제

```
[Caliburn.Micro ViewModel 생명주기]

OnViewAttached()       ← MainMap 참조 획득 (GMapControl 인스턴스)
  ↓
OnActivateAsync()      ← MapConfigureAsync(isInitialLoad: true) 실행
  ↓                       ★ 이 시점: IsStarted = false (GMapControl_Loaded 미실행)
  ↓                       ★ ReloadMap() → 예외 발생
  ↓
GMapControl_Loaded()   ← IsStarted = true (이제 ReloadMap 가능)
  ↓                       ★ 타일 자동 로드 시작 (현재 Provider/Position/Zoom 기준)
```

**OnActivateAsync는 GMapControl_Loaded보다 먼저 실행됩니다.** 이것이 모든 문제의 근본 원인입니다.

### 현재 버그 3개

**Bug A: 초기 로드 시 빈 타일**
```
OnActivateAsync → ConfigureMBTilesMap → ReloadMap() → 예외 (IsStarted=false)
→ 예외로 ConfigureCommonMapSettings 스킵
→ GMapControl_Loaded → 타일 로드 시작하지만 Provider 설정이 불완전
→ 결과: 타일 없음
```

**Bug B: 콤보박스 빈칸**
```
OnActivateAsync → ConfigureMBTilesMap → ReloadMap() → 예외
→ 예외로 NotifyOfPropertyChange(SelectedMapItem) 스킵
→ 콤보박스 선택 표시 안 됨
```

**Bug C: HomePosition 미이동**
```
OnActivateAsync → ConfigureMBTilesMap → Step 5에서 MBTiles center 설정
→ ConfigureCommonMapSettings 스킵됨 (예외)
→ HomePosition 이동 로직 실행 안 됨
→ MBTiles center에 머무름 (또는 ReloadMap 스킵 시 타일 안 뜸)
```

### 현재 코드의 구조적 문제

```
ConfigureMBTilesMap(definedMap, isInitialLoad)
  ├── Step 1: EmptyProvider 전환          ← 초기 로드 시 불필요
  ├── Step 2: MemoryCache.Clear()          ← 초기 로드 시 불필요 (비어있음)
  ├── Step 3: MBTiles Open                 ← 항상 필요
  ├── Step 4: MapProvider = MBTiles        ← 항상 필요
  ├── Step 5: Position/Zoom 설정           ← 케이스별로 다름
  └── Step 6: ReloadMap()                  ← 초기 로드 시 호출 불가!

→ 하나의 메서드에서 if/else로 분기하다 보니 점점 복잡해지고 버그 발생
```

---

## 2. Goals (목표)

### 핵심 목표
- [ ] 초기 로드와 맵 전환 프로세스를 **완전 분리**
- [ ] 초기 로드: Provider + Position(HomePosition) + Zoom 설정만 → GMapControl_Loaded에서 타일 자동 로드
- [ ] 맵 전환: EmptyProvider → CacheClear → Open → Provider → ReloadMap → Position 복원
- [ ] 콤보박스 선택 + HomePosition 이동이 예외로 스킵되지 않음

### 비목표
- GMap.NET Core 내부 수정 (ReloadMap 로직 변경 등)
- 온라인 맵 Provider (Google/Bing) 지원 — 현재 MBTiles만

---

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 |
|----|---------|---------|
| MI-01 | `InitializeMBTilesMap(definedMap)` — 초기 로드 전용 메서드 | Must |
| MI-02 | `SwitchMBTilesMap(definedMap)` — 맵 전환 전용 메서드 | Must |
| MI-03 | 초기 로드 시 ReloadMap 호출하지 않음 | Must |
| MI-04 | 초기 로드 시 Position = HomePosition (appsettings) | Must |
| MI-05 | 맵 전환 시 Position/Zoom 유지 (현재 위치 보존) | Must |
| MI-06 | 맵 전환 시 EmptyProvider + CacheClear + ReloadMap 수행 | Must |
| MI-07 | NotifyOfPropertyChange가 어떤 경우에도 실행됨 | Must |

---

## 4. Technical Approach (기술 접근)

### 케이스별 프로세스 정의

#### Case 1: 초기 로드 (`OnActivateAsync`)

```
OnActivateAsync
  → MapConfigureAsync(isInitialLoad: true)
    → SeedMBTilesMapsAsync()
    → SelectedMap 결정 (appsettings.MapName → _mapProvider 매칭)
    → InitializeMBTilesMap(definedMap)    ← 신규 메서드
    → ConfigureCommonMapSettings()        ← HomePosition 이동
    → NotifyOfPropertyChange             ← 콤보박스 선택
  → SymbolConfigureAsync()
  → ImageConfigureAsync()

```

**InitializeMBTilesMap(definedMap):**
```csharp
private void InitializeMBTilesMap(DefinedMapModel definedMap)
{
    // 1. MBTiles 열기
    var provider = MBTilesMapProvider.Instance;
    provider.Open(mbtilesPath);

    // 2. Provider 설정 (EmptyProvider 불필요 — 최초이므로)
    MainMap.MapProvider = provider;
    MainMap.Manager.Mode = AccessMode.ServerOnly;

    // 3. Zoom 범위 설정
    MainMap.MinZoom = provider.MinZoom;
    MainMap.MaxZoom = provider.MaxZoom;

    // 4. Position/Zoom는 설정하지 않음
    //    → ConfigureCommonMapSettings에서 HomePosition으로 설정됨

    // 5. ReloadMap 호출하지 않음
    //    → GMapControl_Loaded → OnMapOpen → 타일 자동 로드
}
```

**왜 ReloadMap이 필요 없는가:**
```
GMapControl_Loaded (IsStarted=false → true)
  → _core.OnMapOpen()
  → 현재 설정된 MapProvider로 타일 로드 시작
  → 이 시점에 MBTilesMapProvider가 이미 설정되어 있으므로
  → 정상적으로 MBTiles 타일 로드됨
```

**왜 EmptyProvider가 필요 없는가:**
```
초기 상태: MapProvider = null (또는 기본값)
→ MapProvider = MBTilesMapProvider.Instance 설정
→ GMap.NET 입장: "새 Provider!" → OnMapOpen에서 이 Provider로 타일 로드
→ 참조 변경 자연 발생 (null → MBTiles)
```

#### Case 2: 맵 전환 (`ChangeMapAsync`)

```
ChangeMapAsync(targetMap)
  → _mapSwitchLock.WaitAsync(0)
  → MapConfigureAsync(isInitialLoad: false)
    → SelectedMap = targetMap
    → SwitchMBTilesMap(definedMap)     ← 신규 메서드
    → ConfigureCommonMapSettings()     ← Position 유지 (MBTiles 분기)
    → NotifyOfPropertyChange
  → SaveCurrentMapSettingsAsync()
```

**SwitchMBTilesMap(definedMap):**
```csharp
private void SwitchMBTilesMap(DefinedMapModel definedMap)
{
    // 0. 현재 위치/줌 저장
    var savedPosition = MainMap.Position;
    var savedZoom = MainMap.Zoom;

    // 1. EmptyProvider 전환 (GMap.NET에 참조 변경 알림)
    MainMap.MapProvider = GMapProviders.EmptyProvider;

    // 2. 메모리 타일 캐시 초기화
    GMaps.Instance.MemoryCache.Clear();

    // 3. 새 MBTiles 열기 (Open 내부에서 이전 source Close)
    var provider = MBTilesMapProvider.Instance;
    provider.Open(mbtilesPath);

    // 4. MBTiles Provider 설정
    MainMap.MapProvider = provider;
    MainMap.Manager.Mode = AccessMode.ServerOnly;

    // 5. Zoom 범위 설정
    MainMap.MinZoom = provider.MinZoom;
    MainMap.MaxZoom = provider.MaxZoom;

    // 6. 위치/줌 복원
    MainMap.Position = savedPosition;
    MainMap.Zoom = savedZoom;

    // 7. 강제 리로드 (IsStarted=true 보장)
    MainMap.ReloadMap();
}
```

**왜 EmptyProvider가 필요한가:**
```
MBTilesMapProvider는 싱글턴
→ MapProvider = MBTilesMapProvider.Instance (같은 참조)
→ GMap.NET: "바뀐 거 없음" → 리로드 안 함
→ Empty 거쳐야 참조 변경 감지됨
```

**왜 ReloadMap이 필요한가:**
```
Provider 교체 + Empty 경유로 GMap.NET이 인식하지만
MemoryCache에 이전 타일이 남아있을 수 있음
ReloadMap → Matrix.ClearAllLevels + Refresh → 깨끗한 타일 로드
```

#### Case 3: HomePosition 버튼 클릭

```
GoToHomePosition()
  → MainMap.Position = HomePosition 좌표
  → MainMap.Zoom = HomePosition.Zoom
  // ReloadMap 불필요 — 같은 Provider, 위치만 변경
```

### ConfigureCommonMapSettings 역할 정리

```csharp
private void ConfigureCommonMapSettings(bool isInitialLoad = false)
{
    // Position/Zoom 설정
    if (isInitialLoad)
    {
        // 초기 로드: HomePosition으로 이동
        MainMap.Position = _setupModel.HomePosition?.PointLatLng ?? DEFAULT_POSITION;
        MainMap.Zoom = _setupModel.HomePosition?.Zoom ?? DEFAULT_ZOOM;
    }
    // 맵 전환: Position/Zoom은 SwitchMBTilesMap에서 이미 복원됨 → 건드리지 않음

    // MinZoom/MaxZoom 보정 (DB 값 우선)
    MainMap.MinZoom = SelectedMap.MinZoomLevel;
    MainMap.MaxZoom = SelectedMap.MaxZoomLevel;

    // 이벤트 핸들러 (-= 선행 + += 구독)
    MainMap.OnPositionChanged -= handler;
    MainMap.OnPositionChanged += handler;
    // ... 기타 이벤트

    // HomePosition 객체 초기화
    SetInitialHomePosition();
}
```

### 메서드 분리 요약

| 기존 | 신규 | 호출 시점 |
|------|------|----------|
| `ConfigureMBTilesMap(dm, isInitialLoad=true)` | `InitializeMBTilesMap(dm)` | OnActivateAsync |
| `ConfigureMBTilesMap(dm, isInitialLoad=false)` | `SwitchMBTilesMap(dm)` | ChangeMapAsync |
| `ConfigureCommonMapSettings()` | `ConfigureCommonMapSettings(isInitialLoad)` | 둘 다 |

### 각 단계별 필요 여부

| 단계 | 초기 로드 | 맵 전환 | 이유 |
|------|----------|---------|------|
| EmptyProvider 전환 | ❌ | ✅ | 초기엔 Provider가 null이므로 참조 변경 자연 발생 |
| MemoryCache.Clear | ❌ | ✅ | 초기엔 캐시 비어있음 |
| MBTiles Open | ✅ | ✅ | 항상 필요 |
| MapProvider 설정 | ✅ | ✅ | 항상 필요 |
| MinZoom/MaxZoom | ✅ | ✅ | 항상 필요 |
| Position 설정 | HomePosition | 복원 | 케이스별 다름 |
| Zoom 설정 | HomePosition.Zoom | 복원 | 케이스별 다름 |
| ReloadMap | ❌ | ✅ | 초기엔 OnMapOpen이 처리, 전환 시만 필요 |

### 영향받는 컴포넌트

| 컴포넌트 | 변경 유형 | 설명 |
|----------|----------|------|
| `MapViewModel.cs` | 수정 | ConfigureMBTilesMap → InitializeMBTilesMap + SwitchMBTilesMap 분리 |
| `MapViewModel.cs` | 수정 | ConfigureCommonMapSettings — MBTiles 분기 단순화 |
| `MapViewModel.cs` | 수정 | ConfigureDefinedMapAsync — isInitialLoad에 따라 다른 메서드 호출 |

---

## 5. Test Strategy (테스트 전략)

### 검증 항목

| 항목 | 검증 방법 |
|------|----------|
| 초기 로드: 타일 정상 표시 | 앱 시작 → 지도 타일 보임 |
| 초기 로드: HomePosition | 앱 시작 → 37.648°N, zoom 18 |
| 초기 로드: 콤보박스 선택 | 앱 시작 → "위성지도" 표시 |
| 초기 로드: 심볼 표시 | 앱 시작 → PIDS 마커 보임 |
| 맵 전환: 타일 깨끗한 전환 | 위성↔일반 10회 → 겹침 없음 |
| 맵 전환: 위치/줌 유지 | 전환 전후 동일 좌표/줌 |
| 맵 전환: ReloadMap 정상 | 전환 시 예외 없음 |

---

## 6. Risks & Mitigations (리스크)

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| OnMapOpen 타이밍이 플랫폼마다 다를 수 있음 | Medium | GMapControl_Loaded에서 확인 — 표준 WPF 이벤트 |
| InitializeMBTilesMap에서 Provider 설정 후 OnMapOpen 전에 Position 변경 | Low | Position 설정은 값만 저장, OnMapOpen에서 반영 |

---

## 7. References (참고)
- GMap.NET Core.cs:588 — ReloadMap() → IsStarted 체크
- GMap.NET GMapControl.cs:902 — GMapControl_Loaded → OnMapOpen
- PRD_MapSwitch_Stability.md — 이전 맵 전환 안정성 PRD
- PRD_MBTiles_DefinedMap_Integration.md — MBTiles 통합 PRD
