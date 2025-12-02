# MySQL Concurrency Error 트러블슈팅

## 에러 메시지
```
MySql.Data.MySqlClient.MySqlException
Message=Record has changed since last read in table 'pidssymbols'
```

## 발생 위치
- `GMapDbSymbolService.cs:line 1500` (UpdatePidsSymbolAsync)
- `MapViewModel.cs:line 3318` (DbUpdateProcess)
- `MapViewModel.cs:line 3839` (OnMarkerPropertyChanged)

---

## 원인 1: UpdatedAt 자동 갱신 문제 (✅ 해결됨)

### 문제 설명
MySQL 테이블 스키마에서 `UpdatedAt` 컬럼이 다음과 같이 정의됨:
```sql
`UpdatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
```

UPDATE 쿼리에서 `UpdatedAt`를 명시하지 않으면 MySQL이 자동으로 타임스탬프를 갱신하여 optimistic locking 충돌 발생.

### 해결 방법
모든 UPDATE 쿼리에 `UpdatedAt = CURRENT_TIMESTAMP` 명시 추가:
```sql
UPDATE PidsSymbols SET
    LinkedDeviceId = @LinkedDeviceId,
    DeviceType = @DeviceType,
    ShowFOV = @ShowFOV,
    FOVColor = @FOVColor,
    FOVOpacity = @FOVOpacity,
    EventStatus = @EventStatus,
    BaseBearing = @BaseBearing,
    UpdatedAt = CURRENT_TIMESTAMP  -- ✅ 명시적 설정
WHERE SymbolId = @SymbolId;
```

**커밋**: [fecebaf] fix: MySQL concurrency error - 모든 UPDATE 쿼리에 UpdatedAt 명시

---

## 원인 2: 런타임 전용 속성을 DB에 저장하려고 시도 (✅ 해결됨)

### 문제 설명
**DetectionRange, DetectionAngle, DetectionBearing**은 **런타임 전용 속성**으로 DB에 저장하면 안 됨:
- BaseBearing: DB 저장 ✅ (카메라 물리적 설치 방향, 고정값)
- DetectionBearing: DB 저장 ❌ (현재 FOV 방향, 런타임 전용)
- DetectionRange: DB 저장 ❌ (탐지 범위, 런타임 전용)
- DetectionAngle: DB 저장 ❌ (탐지 각도, 런타임 전용)

그러나 `GMapPropertyPidsControl.cs`에서 **런타임 전용 속성 변경 시에도 `OnMarkerPropertyChanged`를 호출**하여 DB UPDATE를 트리거:

**재현 시나리오**:
1. DetectionBearing Slider를 0으로 변경 → ❌ DB UPDATE 시작 (잘못됨!)
2. 즉시 BaseBearing Slider를 105로 변경 → ✅ DB UPDATE 시작 (정상)
3. 두 UPDATE 쿼리가 거의 동시에 실행됨
4. 첫 번째 UPDATE가 완료되면 `UpdatedAt` 타임스탬프 변경
5. 두 번째 UPDATE 실행 시 "Record has changed" 에러 발생

### 호출 흐름 (수정 전)
```
GMapPropertyPidsControl (UI)
  → OnDetectionBearingChanged() → OnMarkerPropertyChanged() ❌ (잘못됨!)
  → OnBaseBearingChanged() → OnMarkerPropertyChanged() ✅
    → MapViewModel.OnMarkerPropertyChanged()
      → DbUpdateProcess()
        → GMapDbSymbolService.UpdatePidsSymbolAsync()
          ❌ MySqlException: Record has changed
```

### 근본 원인
- **런타임 전용 속성을 DB에 저장**: DetectionRange, DetectionAngle, DetectionBearing 변경 시 불필요한 DB UPDATE
- **비동기 UPDATE 작업의 동시 실행**: 런타임 속성 + DB 속성 동시 변경 시 UPDATE 충돌
- **낙관적 잠금(Optimistic Locking) 실패**: UpdatedAt 타임스탬프 불일치

### 해결 방법
런타임 전용 속성 변경 콜백에서 `OnMarkerPropertyChanged` 호출 제거:

**수정 위치**: `GMapPropertyPidsControl.cs`
```csharp
// OnDetectionRangeChanged - 수정 후
private static void OnDetectionRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is GMapPropertyPidsControl control &&
        control.SelectedMarker is IPidsEditableMarker pidsMarker &&
        !control._isInitializing && !control._isClearingBindings)
    {
        // DetectionRange는 런타임 전용 (DB 저장 안 함)
        pidsMarker.DetectionRange = (double)e.NewValue;
        // OnMarkerPropertyChanged 호출 안 함 (DB UPDATE 트리거 방지)
    }
}

// OnDetectionAngleChanged, OnDetectionBearingChanged도 동일하게 수정
```

**커밋**: [<commit-hash>] fix: 런타임 전용 FOV 속성 DB 저장 방지

---

## 해결 방안

### 방안 1: UI 속성 변경 Debouncing (추천)
**구현 위치**: `GMapPropertyPidsControl.cs` 또는 `MapViewModel.cs`

**개념**: 마지막 속성 변경 후 일정 시간(예: 500ms) 대기 후 DB 저장
```csharp
// GMapPropertyPidsControl.cs
private CancellationTokenSource _debounceCts;
private const int DebounceDelayMs = 500;

private async void OnPropertyChangedDebounced(string propertyName, object oldValue, object newValue)
{
    // 이전 debounce 취소
    _debounceCts?.Cancel();
    _debounceCts = new CancellationTokenSource();

    try
    {
        // 500ms 대기 (사용자가 추가 변경할 시간)
        await Task.Delay(DebounceDelayMs, _debounceCts.Token);

        // 대기 완료 후 DB 저장
        OnMarkerPropertyChanged(propertyName, oldValue, newValue);
    }
    catch (TaskCanceledException)
    {
        // 새로운 변경으로 인해 취소됨 (정상)
    }
}
```

**장점**:
- 사용자가 여러 속성을 빠르게 변경해도 마지막 변경만 DB에 저장
- DB 부하 감소
- 동시성 충돌 방지

**단점**:
- 저장 지연 발생 (사용자 경험 고려 필요)

---

### 방안 2: DB 업데이트 직렬화 (Serialization)
**구현 위치**: `MapViewModel.DbUpdateProcess()`

**개념**: 동일한 Symbol에 대한 UPDATE를 순차적으로 처리
```csharp
// MapViewModel.cs
private readonly SemaphoreSlim _updateSemaphore = new(1, 1);

private async Task DbUpdateProcess(ISymbolModel model)
{
    await _updateSemaphore.WaitAsync();
    try
    {
        // 기존 UPDATE 로직
        await _symbolDbService.UpdatePidsSymbolAsync(model);
    }
    finally
    {
        _updateSemaphore.Release();
    }
}
```

**장점**:
- UPDATE 순서 보장
- 동시성 충돌 완전 방지

**단점**:
- 첫 번째 UPDATE가 완료될 때까지 두 번째 UPDATE 대기 (성능 저하 가능)

---

### 방안 3: 재시도 로직 (Retry Pattern)
**구현 위치**: `GMapDbSymbolService.UpdatePidsSymbolAsync()`

**개념**: 동시성 충돌 발생 시 자동 재시도
```csharp
private async Task<IPidsSymbolModel?> UpdatePidsSymbolAsync(IPidsSymbolModel model, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            // 기존 UPDATE 로직
            return await ExecuteUpdateAsync(model);
        }
        catch (MySqlException ex) when (ex.Message.Contains("Record has changed"))
        {
            if (attempt == maxRetries)
                throw; // 최대 재시도 초과 시 예외 전파

            // 짧은 대기 후 재시도
            await Task.Delay(100 * attempt);
            _log?.Warn($"Update retry {attempt}/{maxRetries} - Id={model.Id}");
        }
    }
}
```

**장점**:
- 일시적 충돌 자동 해결
- 기존 코드 최소 변경

**단점**:
- 재시도 횟수 초과 시 여전히 실패 가능
- 근본 원인 해결 아님

---

## 권장 해결 조합

**최적 해결책**: **방안 1 (Debouncing) + 방안 3 (Retry)**

1. **UI 레이어**: 속성 변경 Debouncing으로 불필요한 UPDATE 감소
2. **DB 레이어**: 재시도 로직으로 일시적 충돌 자동 복구

```csharp
// GMapPropertyPidsControl.cs - Debouncing
private void OnBaseBearingChanged(...)
{
    OnPropertyChangedDebounced("BaseBearing", oldValue, newValue);
}

// GMapDbSymbolService.cs - Retry
private async Task<IPidsSymbolModel?> UpdatePidsSymbolAsync(..., int maxRetries = 3)
{
    // 재시도 로직 구현
}
```

---

## 테스트 시나리오

### 정상 동작 테스트
1. DetectionBearing Slider를 0으로 변경
2. 500ms 대기
3. DB UPDATE 성공 확인

### 동시성 충돌 테스트
1. DetectionBearing Slider를 0으로 변경
2. **즉시** BaseBearing Slider를 105로 변경 (100ms 이내)
3. Debouncing에 의해 마지막 변경(BaseBearing=105)만 DB에 저장
4. 에러 발생하지 않음

### 재시도 테스트
1. Debouncing 비활성화
2. 두 속성 빠르게 연속 변경
3. 첫 번째 UPDATE 실패 시 자동 재시도
4. 최종 성공 확인

---

## 관련 파일

- `GMapDbSymbolService.cs:1500` - UPDATE 실행 지점
- `MapViewModel.cs:3318` - DbUpdateProcess 호출
- `MapViewModel.cs:3839` - OnMarkerPropertyChanged 이벤트 처리
- `GMapPropertyPidsControl.cs` - UI 속성 변경 이벤트

---

## 히스토리

- **2025-12-02**: 원인 1 해결 ([fecebaf]) - UpdatedAt 명시
- **2025-12-02**: 원인 2 해결 - 런타임 전용 속성 DB 저장 방지
- **2025-12-02**: 문서 작성 및 업데이트
