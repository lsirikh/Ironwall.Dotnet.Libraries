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

### 동기
- 맵 전환 시 이벤트 중복으로 성능 저하 및 예상치 못한 동작 발생 가능
- 연속 전환 시 Race condition으로 잘못된 맵이 로드될 수 있음
- 장시간 운용 시 SQLite 연결 누수로 "database locked" 발생 가능

## 2. Goals (목표)

### 핵심 목표
- [ ] 맵 전환 시 이벤트 핸들러 누적 방지 (always 1개 구독)
- [ ] 연속 빠른 전환 시 Race condition 방지
- [ ] MBTiles 전환 시 이전 SQLite 연결 명시적 해제

### 비목표 (Out of Scope)
- 맵 전환 애니메이션/트랜지션 효과
- 온라인↔오프라인 전환 (현재 MBTiles 오프라인만 사용)
- 맵 전환 시 심볼 재배치 로직 (현재 마커는 유지됨)

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 | 비고 |
|----|---------|---------|------|
| MS-01 | ConfigureCommonMapSettings에서 += 전에 -= 선행 | Must | 4개 이벤트 핸들러 |
| MS-02 | ChangeMapAsync에 SemaphoreSlim(1,1) 적용 | Must | 동시 실행 방지 |
| MS-03 | MBTilesMapProvider.Open() 전 이전 source 명시적 Close | Must | GMap.NET 코드 수정 |
| MS-04 | MBTiles 클래스에 Close()/Dispose() 메서드 추가 | Must | 리소스 정리 |
| MS-05 | 맵 전환 후 기존 마커 유지 검증 | Should | 회귀 테스트 |

### 비기능 요구사항
- 성능: 맵 전환 시간 기존과 동일 (추가 오버헤드 없음)
- 안정성: 100회 연속 전환에도 이벤트 1회만 실행
- 호환성: GMap.NET 내부 수정은 최소화

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
