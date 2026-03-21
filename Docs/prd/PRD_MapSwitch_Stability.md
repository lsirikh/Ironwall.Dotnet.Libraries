# PRD: 맵 전환 안정성 개선

- **Version**: 1.0
- **Date**: 2026-03-21
- **Status**: Draft
- **Language/Framework**: C# / WPF (.NET 8) + GMap.NET

## 1. Background (배경)

### 현재 상황

맵 전환(콤보박스에서 위성지도↔일반지도 선택) 시 3가지 버그가 존재한다.

**Bug #1: 이벤트 핸들러 누적 (HIGH)**
```csharp
// ConfigureCommonMapSettings() — 맵 전환마다 호출됨
MainMap.OnPositionChanged += MainMap_OnCurrentPositionChanged;
MainMap.MouseMove += MainMap_MouseMove;
MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
MainMap.OnMapZoomChanged += MainMap_OnMapZoomChanged;
// -= 가 없음 → 전환 N번 후 이벤트 N번 중복 실행
```

**Bug #2: Fire-and-forget Race Condition (MEDIUM)**
```csharp
// SelectedMapItem setter
_ = ChangeMapAsync(value);  // await 없이 fire-and-forget
// → 빠른 연속 클릭 시 2개 MapConfigureAsync 동시 실행 가능
```

**Bug #3: MBTiles SQLite 리소스 누수 (MEDIUM)**
```csharp
// MBTilesMapProvider.Open() — 싱글턴
source = new MBTiles(MBTilesFilePath);  // 이전 source Close 없이 교체
// → 이전 SQLite 연결이 GC 의존, 명시적 해제 안 됨
```

**Bug #4: 싱글턴 Provider 참조 미변경 → 타일 리로드 안 됨 (CRITICAL)**
```
MBTilesMapProvider.Instance.Open("map_base.mbtiles")  // 내부 source 교체
MainMap.MapProvider = MBTilesMapProvider.Instance       // 같은 참조!
→ GMap.NET: "MapProvider 안 바뀌었으니 리로드 안 함"
→ KiberTileCache: 이전 타일 유지
→ 결과: 새 타일 + 옛 캐시 = 겹침
```

**Bug #5: 콤보박스 이름 "고양시일부" 잔존**
```
이전 MBTiles 메타데이터 name="고양시일부"로 DB 생성됨
→ DB 삭제 후 재시작해도 이전 세션 코드가 provider.DataName 사용
→ 현재 코드는 파일명 기준이지만, DB에 남은 옛 데이터
```

**Bug #6: 맵 전환 시 위치/줌 리셋 (MEDIUM)**
```
위성지도 zoom=18 서울 보고 있음
→ 일반지도 전환
→ ConfigureMBTilesMap: Position = MBTiles center, Zoom = centerZoom
→ zoom=13 + 전혀 다른 위치로 이동 ❌

원인: ConfigureMBTilesMap에서 매번 provider.CenterLocation/CenterZoom으로 설정
초기 로드 시에만 center를 사용하고, 전환 시에는 현재 위치/줌을 유지해야 함
```

### 동기
- 맵 전환 시 이벤트 중복으로 성능 저하 및 예상치 못한 동작 발생 가능
- 연속 전환 시 Race condition으로 잘못된 맵이 로드될 수 있음
- 장시간 운용 시 SQLite 연결 누수로 "database locked" 발생 가능
- **싱글턴 Provider에서 MBTiles 전환 시 타일이 겹침 (가장 심각)**
- **맵 전환 시 현재 보고 있는 위치/줌이 리셋됨**

## 2. Goals (목표)

### 핵심 목표
- [x] 맵 전환 시 이벤트 핸들러 누적 방지 (always 1개 구독) — Phase 1 완료
- [x] 연속 빠른 전환 시 Race condition 방지 — Phase 2 완료
- [x] MBTiles 전환 시 이전 SQLite 연결 명시적 해제 — Phase 3 완료
- [x] **MBTiles 전환 시 타일 캐시 초기화 + 강제 리로드** — Phase 5 완료
- [x] **디버깅 로그 추가 (전환 과정 추적)** — Phase 6 완료
- [ ] **맵 전환 시 현재 위치/줌 유지 (초기 로드만 center 사용)** — Phase 8 (신규)

### 비목표 (Out of Scope)
- 맵 전환 애니메이션/트랜지션 효과
- 맵 전환 시 심볼 재배치 로직 (현재 마커는 유지됨)

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 | 상태 |
|----|---------|---------|------|
| MS-01 | ConfigureCommonMapSettings에서 += 전에 -= 선행 | Must | ✅ 완료 |
| MS-02 | ChangeMapAsync에 SemaphoreSlim(1,1) 적용 | Must | ✅ 완료 |
| MS-03 | MBTilesMapProvider.Open() 전 이전 source 명시적 Close | Must | ✅ 완료 |
| MS-04 | MBTiles 클래스에 Close()/Dispose() 메서드 추가 | Must | ✅ 완료 |
| MS-05 | 맵 전환 후 기존 마커 유지 검증 | Should | 미검증 |
| **MS-06** | **ConfigureMBTilesMap에서 캐시 초기화 + 강제 리로드** | **Must** | ✅ 완료 |
| **MS-07** | **맵 전환 디버깅 로그 추가** | **Must** | ✅ 완료 |
| **MS-08** | **맵 전환 시 현재 위치/줌 유지 (초기 로드만 center 사용)** | **Must** | 신규 |

### MS-06 근본 원인 및 해결

```
[근본 원인]
MBTilesMapProvider는 싱글턴 → 참조(주소)가 항상 동일
MainMap.MapProvider = 같은 참조 → GMap.NET이 변경 감지 못함
KiberTileCache(22MB)에 이전 맵 타일이 남아 → 겹침

[해결 방안]
ConfigureMBTilesMap() 내부에서:
1. MainMap.MapProvider = EmptyProvider.Instance  ← 임시 빈 Provider
2. GMaps.Instance.MemoryCache.Clear()            ← 타일 캐시 초기화
3. MBTilesMapProvider.Instance.Open(newPath)      ← 새 MBTiles 열기
4. MainMap.MapProvider = MBTilesMapProvider.Instance  ← Provider 재설정
5. MainMap.ReloadMap()                            ← 강제 리로드
```

### MS-06 전환 흐름 (수정 후)

```
[사용자: "위성지도" → "일반지도" 선택]
    │
    ▼
ChangeMapAsync("일반지도")
    │ _mapSwitchLock.WaitAsync(0) = true ✅
    │
    ▼
MapConfigureAsync()
    │
    ▼
ConfigureMBTilesMap(definedMap)
    │
    ├── [Step 1] MainMap.MapProvider = EmptyProvider.Instance
    │   └── GMap.NET: "Provider 바뀜! 기존 렌더링 중단"
    │
    ├── [Step 2] GMaps.Instance.MemoryCache.Clear()
    │   └── KiberTileCache: 위성 타일 22MB 전부 삭제
    │
    ├── [Step 3] source?.Close()
    │   └── map_satellite.mbtiles SQLite 연결 해제
    │
    ├── [Step 4] MBTilesMapProvider.Instance.Open("map_base.mbtiles")
    │   └── 새 SQLite 연결 + 메타데이터 읽기
    │
    ├── [Step 5] MainMap.MapProvider = MBTilesMapProvider.Instance
    │   └── GMap.NET: "Provider 바뀜! 새 타일 요청 시작"
    │
    ├── [Step 6] MainMap.ReloadMap()
    │   └── 화면 전체 타일 재요청 → map_base.mbtiles에서 로드
    │
    └── Position / Zoom 설정
```

### MS-07 디버깅 로그

```
ConfigureMBTilesMap 진입 시:
  [MapSwitch] 전환 시작: {현재Provider} → {새파일명}
  [MapSwitch] Step 1: EmptyProvider 전환
  [MapSwitch] Step 2: MemoryCache 클리어 ({N}개 타일 삭제)
  [MapSwitch] Step 3: 이전 source Close
  [MapSwitch] Step 4: Open({파일명}) = {결과}
  [MapSwitch] Step 5: MapProvider 설정
  [MapSwitch] Step 6: ReloadMap 호출
  [MapSwitch] 전환 완료: {새Provider}, Zoom={min}~{max}, Center=({lat},{lng})
```

### 비기능 요구사항
- 성능: 맵 전환 시간 기존과 동일 (캐시 클리어 ~10ms 추가)
- 안정성: 100회 연속 전환에도 타일 겹침 없음
- 호환성: GMap.NET 내부 수정은 최소화

### MS-08 구현 상세

```
[현재 동작 — Bug #6]
ConfigureMBTilesMap:
  Position = provider.CenterLocation  ← 매번 MBTiles center로 이동
  Zoom = provider.CenterZoom           ← 매번 MBTiles centerZoom으로 변경

[수정 후]
ConfigureMBTilesMap(definedMap, isInitialLoad):
  if (isInitialLoad)                   ← 최초 로드 시만
    Position = provider.CenterLocation
    Zoom = provider.CenterZoom
  else                                 ← 전환 시
    // Position/Zoom 유지 (변경 안 함)
    // MinZoom/MaxZoom만 새 MBTiles에 맞게 업데이트
```

```
[전환 흐름]

앱 시작 (isInitialLoad = true):
  → SeedMBTilesMapsAsync
  → ConfigureMBTilesMap(satellite, isInitialLoad=true)
  → Position = MBTiles center ✅ (처음이니까)

콤보박스 전환 (isInitialLoad = false):
  → ChangeMapAsync → MapConfigureAsync
  → ConfigureMBTilesMap(base, isInitialLoad=false)
  → 현재 Position 저장 → 캐시 클리어 → Open → Provider 설정
  → Position/Zoom 복원 ✅ (보고 있던 위치 유지)
```

### isInitialLoad 판별 방법

```csharp
// 방법 A: MapConfigureAsync 호출원 구분
// MapConfigureAsync(isInitialLoad: true)  ← OnActivateAsync에서 호출
// MapConfigureAsync(isInitialLoad: false) ← ChangeMapAsync에서 호출

// 방법 B: 현재 Provider가 Empty/null이면 초기 로드
var isInitialLoad = MainMap.MapProvider == null
                 || MainMap.MapProvider == GMapProviders.EmptyProvider;
```

## 4. Technical Approach (기술 접근)

### 현재 맵 전환 흐름 (버그 포함)

```
[사용자: 콤보박스에서 "일반지도" 선택]
    │
    ▼
SelectedMapItem setter
    │ _ = ChangeMapAsync(value)  ← ⚠ fire-and-forget (await 없음)
    │                               빠른 클릭 시 2개 동시 실행 가능
    ▼
ChangeMapAsync(targetMap)
    │ SelectedMap = targetMap
    │ _setupModel.MapName = targetMap.Name
    │
    ▼
MapConfigureAsync()
    │
    ├── SeedMBTilesMapsAsync()      ← Datas/ 폴더↔DB 동기화
    │
    ├── SelectedMap 결정              ← 콤보박스 선택값 기반
    │
    ├── ConfigureDefinedMapAsync()
    │   └── case MBTiles:
    │       └── ConfigureMBTilesMap(definedMap)
    │           │
    │           ├── MBTilesMapProvider.Instance.Open(path)
    │           │   └── ⚠ 이전 source Close 없이 교체 (SQLite 누수)
    │           │
    │           ├── MainMap.MapProvider = provider
    │           ├── MainMap.MinZoom / MaxZoom 설정
    │           └── MainMap.Position / Zoom 설정
    │
    └── ConfigureCommonMapSettings()
        │
        ├── ⚠ MainMap.OnPositionChanged += handler  (N번 누적!)
        ├── ⚠ MainMap.MouseMove += handler           (N번 누적!)
        ├── ⚠ MainMap.MouseLeftButtonDown += handler  (N번 누적!)
        └── ⚠ MainMap.OnMapZoomChanged += handler    (N번 누적!)
```

### 수정 후 맵 전환 흐름

```
[사용자: 콤보박스에서 "일반지도" 선택]
    │
    ▼
SelectedMapItem setter
    │ _ = ChangeMapAsync(value)
    ▼
ChangeMapAsync(targetMap)
    │
    ├── ✅ _mapSwitchLock.WaitAsync(0)
    │   └── false → return (이미 전환 중이면 스킵)
    │
    │ try {
    │   SelectedMap = targetMap
    │   await MapConfigureAsync()
    │   await SaveCurrentMapSettingsAsync()
    │ } finally {
    │   _mapSwitchLock.Release()
    │ }
    │
    ▼
MapConfigureAsync()
    │
    ├── SeedMBTilesMapsAsync()
    │
    ├── ConfigureDefinedMapAsync()
    │   └── case MBTiles:
    │       └── ConfigureMBTilesMap(definedMap)
    │           │
    │           ├── MBTilesMapProvider.Instance.Open(path)
    │           │   ├── ✅ source?.Close()  ← 이전 SQLite 명시적 해제
    │           │   └── source = new MBTiles(path)
    │           │
    │           ├── MainMap.MapProvider = provider
    │           └── Position / Zoom 설정
    │
    └── ConfigureCommonMapSettings()
        │
        ├── ✅ MainMap.OnPositionChanged -= handler  ← 먼저 해제
        ├── ✅ MainMap.MouseMove -= handler
        ├── ✅ MainMap.MouseLeftButtonDown -= handler
        ├── ✅ MainMap.OnMapZoomChanged -= handler
        │
        ├── MainMap.OnPositionChanged += handler     ← 다시 구독 (항상 1개)
        ├── MainMap.MouseMove += handler
        ├── MainMap.MouseLeftButtonDown += handler
        └── MainMap.OnMapZoomChanged += handler
```

### MBTilesMapProvider 싱글턴 파일 전환 상세

```
[위성지도 → 일반지도 전환]

MBTilesMapProvider.Instance (싱글턴)
    │
    ▼ Open("map_base.mbtiles")
    │
    ├── ✅ source?.Close()
    │   └── map_satellite.mbtiles SQLite 연결 해제
    │       db.Close() → db.Dispose() → db = null
    │
    ├── source = new MBTiles("map_base.mbtiles")
    │   └── 새 SQLite 연결 생성
    │
    ├── metadata 읽기
    │   ├── bounds → Bounds, CenterLocation
    │   ├── minzoom → MinZoom
    │   ├── maxzoom → MaxZoom
    │   └── name → DataName
    │
    └── return true
```

### Race Condition 방지 상세

```
[빠른 연속 클릭 시나리오]

t=0ms: 사용자 "위성지도" 클릭
    → ChangeMapAsync("위성지도")
    → _mapSwitchLock.WaitAsync(0) = true ✅ 진입
    → MapConfigureAsync 시작...

t=100ms: 사용자 "일반지도" 클릭 (위성지도 전환 진행 중)
    → ChangeMapAsync("일반지도")
    → _mapSwitchLock.WaitAsync(0) = false ❌ 스킵 (return)
    → 아무 일도 안 함

t=500ms: 위성지도 전환 완료
    → _mapSwitchLock.Release()
    → 위성지도 정상 로드

[결과: 마지막 클릭이 무시되지만 안전]
```

### 영향받는 컴포넌트

| 컴포넌트 | 변경 유형 | 설명 |
|----------|----------|------|
| `MapViewModel.cs` | 수정 | ConfigureCommonMapSettings, ChangeMapAsync |
| `MBTilesMapProvider.cs` | 수정 | Open() 메서드에 이전 source Close 추가 |
| `MBTilesMapProvider.MBTiles` | 수정 | Close() 메서드 추가 |

### MS-01 구현 상세

```csharp
private void ConfigureCommonMapSettings()
{
    // 기존 핸들러 해제 (누적 방지)
    MainMap.OnPositionChanged -= MainMap_OnCurrentPositionChanged;
    MainMap.MouseMove -= MainMap_MouseMove;
    MainMap.MouseLeftButtonDown -= MainMap_MouseLeftButtonDown;
    MainMap.OnMapZoomChanged -= MainMap_OnMapZoomChanged;

    // 새로 구독
    MainMap.OnPositionChanged += MainMap_OnCurrentPositionChanged;
    MainMap.MouseMove += MainMap_MouseMove;
    MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
    MainMap.OnMapZoomChanged += MainMap_OnMapZoomChanged;
}
```

### MS-02 구현 상세

```csharp
private readonly SemaphoreSlim _mapSwitchLock = new(1, 1);

private async Task ChangeMapAsync(IMapModel targetMap)
{
    if (!await _mapSwitchLock.WaitAsync(0)) return; // 이미 전환 중이면 스킵
    try
    {
        SelectedMap = targetMap;
        _setupModel.MapName = targetMap.Name;
        await MapConfigureAsync();
        NotifyOfPropertyChange(nameof(SelectedMapItem));
        await SaveCurrentMapSettingsAsync();
    }
    finally
    {
        _mapSwitchLock.Release();
    }
}
```

### MS-03/04 구현 상세

```csharp
// MBTilesMapProvider.cs
public bool Open(string MBTilesFilePath)
{
    // 이전 source 명시적 해제
    if (source != null)
    {
        source.Close();
        source = null;
    }

    source = new MBTiles(MBTilesFilePath);
    // ... metadata 읽기 ...
}

// MBTiles 내부 클래스
public void Close()
{
    try
    {
        if (db != null)
        {
            db.Close();
            db.Dispose();
            db = null;
        }
    }
    catch { /* 이미 dispose된 경우 무시 */ }
}
```

### 의존성
- GMap.NET.Core 내부 코드 수정 필요 (MBTilesMapProvider.cs)
- 외부 라이브러리 추가 없음

## 5. Test Strategy (테스트 전략)

### 검증 항목

| 항목 | 검증 방법 |
|------|----------|
| 이벤트 핸들러 1회만 등록 | 맵 5회 전환 후 줌 변경 → 로그에 이벤트 1회만 출력 |
| Race condition 방지 | 빠른 연속 클릭 → SemaphoreSlim으로 1회만 실행 |
| SQLite 리소스 해제 | 위성↔일반 10회 전환 → ObjectDisposedException 없음 |
| 마커 유지 | 전환 전후 MainMap.Markers.Count 동일 |

### 검증 기준
- [ ] 빌드 오류 0개
- [ ] 맵 전환 10회 반복 시 이벤트 중복 없음
- [ ] ObjectDisposedException 없음
- [ ] 기존 테스트 회귀 없음

## 6. Risks & Mitigations (리스크)

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| GMap.NET 내부 수정 | Medium | MBTiles 클래스만 최소 수정, Close() 메서드 추가만 |
| SemaphoreSlim 데드락 | Low | WaitAsync(0) 사용으로 블로킹 없이 스킵 |
| -= 호출 시 미등록 핸들러 | Low | C#에서 미등록 핸들러 -= 는 안전 (예외 없음) |

## 7. References (참고)
- 관련 분석: Agent 분석 결과 (2026-03-21)
- 관련 PRD: PRD_MBTiles_DefinedMap_Integration.md
- 파일: MapViewModel.cs (5000+ lines), MBTilesMapProvider.cs (~300 lines)
