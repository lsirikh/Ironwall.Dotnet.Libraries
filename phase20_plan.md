# Phase 20: PidsSymbol FOV BaseBearing 초기 각도 설정

**PRD**: `Docs/prd/PRD_PidsSymbol_FOV_BaseBearing.md`
**목표**: PidsSymbol FOV가 BaseBearing(카메라 물리적 설치 방향)에서 시작하도록 개선
**공식**: `DetectionBearing = BaseBearing + 사용자회전각도`

---

## Phase 20.1: 데이터 모델 업데이트 (STRUCTURAL - TDD)

**파일**: `Ironwall.Dotnet.Monitoring.Models/Symbols/PidsSymbolModel.cs`

### Test 20.1.1: BaseBearing 기본값 검증 [ ]

```csharp
[Fact]
public void BaseBearing_ShouldHaveDefaultValue()
{
    // Arrange & Act
    var symbol = new PidsSymbolModel();

    // Assert
    Assert.Equal(0.0, symbol.BaseBearing);
}
```

### Test 20.1.2: BaseBearing JSON 직렬화 검증 [ ]

```csharp
[Fact]
public void BaseBearing_ShouldSerializeToJson()
{
    // Arrange
    var symbol = new PidsSymbolModel
    {
        BaseBearing = 90.0
    };

    // Act
    var json = JsonConvert.SerializeObject(symbol);

    // Assert
    Assert.Contains("\"base_bearing\":90.0", json);
}
```

### ActionItem 20.1.1: BaseBearing 속성 추가 [ ]

**구현**:
```csharp
/// <summary>
/// 기준 방향 각도 (카메라 물리적 설치 방향)
/// <para>0.0 ~ 360.0 (정북 기준 시계방향 각도)</para>
/// </summary>
[JsonProperty("base_bearing", Order = 29)]
public double BaseBearing { get; set; } = 0.0;
```

---

## Phase 20.2: Database Schema 업데이트 (STRUCTURAL)

**파일**: `Ironwall.Dotnet.Libraries.GMaps.Db/Services/GMapDbSymbolService.cs`

### ActionItem 20.2.1: createPidsSymbolsSql에 BaseBearing 컬럼 추가 [ ]

**라인 303 수정**:
- `BaseBearing DECIMAL(5,2) DEFAULT 0.0` 추가
- ❌ DetectionRange, DetectionAngle, DetectionBearing는 추가 안 함 (런타임 전용)

### ActionItem 20.2.2: Insert 쿼리에 BaseBearing 추가 [ ]

```sql
INSERT INTO `PidsSymbols` (
    `SymbolId`, `LinkedDeviceId`, `DeviceType`, `ShowFOV`,
    `FOVColor`, `FOVOpacity`, `EventStatus`,
    `BaseBearing`
) VALUES (
    @SymbolId, @LinkedDeviceId, @DeviceType, @ShowFOV,
    @FOVColor, @FOVOpacity, @EventStatus,
    @BaseBearing
);
```

### ActionItem 20.2.3: Update 쿼리에 BaseBearing 추가 [ ]

```sql
UPDATE `PidsSymbols` SET
    `LinkedDeviceId` = @LinkedDeviceId,
    `DeviceType` = @DeviceType,
    `ShowFOV` = @ShowFOV,
    `FOVColor` = @FOVColor,
    `FOVOpacity` = @FOVOpacity,
    `EventStatus` = @EventStatus,
    `BaseBearing` = @BaseBearing,
    `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `SymbolId` = @SymbolId;
```

### ActionItem 20.2.4: Select 쿼리에 BaseBearing 추가 [ ]

```sql
SELECT
    `SymbolId`, `LinkedDeviceId`, `DeviceType`, `ShowFOV`,
    `FOVColor`, `FOVOpacity`, `EventStatus`,
    `BaseBearing`,
    `CreatedAt`, `UpdatedAt`
FROM `PidsSymbols`
WHERE `SymbolId` = @SymbolId;
```

---

## Phase 20.3: FOV 생성 로직 수정 (BEHAVIORAL - TDD)

### Test 20.3.1: FOV 생성 시 BaseBearing 반영 검증 [ ]

```csharp
[Fact]
public void CreateFov_WithBaseBearing_ShouldSetInitialBearing()
{
    // Arrange
    var symbol = new PidsSymbolModel
    {
        BaseBearing = 135.0
    };

    // Act
    symbol.DetectionBearing = symbol.BaseBearing;

    // Assert
    Assert.Equal(135.0, symbol.DetectionBearing);
}
```

### Test 20.3.2: Symbol 로드 후 DetectionBearing 초기화 검증 [ ]

```csharp
[Fact]
public void LoadSymbol_ShouldInitializeDetectionBearing()
{
    // Arrange
    var symbol = new PidsSymbolModel
    {
        BaseBearing = 90.0
    };

    // Act
    symbol.DetectionBearing = symbol.BaseBearing;

    // Assert
    Assert.Equal(90.0, symbol.DetectionBearing);
}
```

### ActionItem 20.3.1: FOV 생성 코드 탐색 [ ]

- GMapCameraMarker, FovRenderer 또는 관련 ViewModel 탐색
- 현재 DetectionBearing 초기화 방식 분석

### ActionItem 20.3.2: FOV 초기 Bearing에 BaseBearing 적용 [ ]

```csharp
// Before
symbol.DetectionBearing = 0.0;

// After
symbol.DetectionBearing = symbol.BaseBearing;
```

---

## Phase 20.4: UI 구현 (BEHAVIORAL)

### ActionItem 20.4.1: PidsPropertyStyle.xaml에 BaseBearing Slider 추가 [ ]

**파일**: `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml`
**위치**: DetectionBearing Slider 뒤 (라인 209 이후)

```xml
<!--  기준 방향 (BaseBearing)  -->
<Grid Margin="0,5">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="60" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="35" />
    </Grid.ColumnDefinitions>
    <TextBlock
        Grid.Column="0"
        Style="{StaticResource PropertyLabel}"
        Text="기준방향" />
    <Slider
        x:Name="BaseBearingSlider"
        Grid.Column="1"
        Margin="5,0"
        VerticalAlignment="Center"
        Maximum="360"
        Minimum="0"
        TickFrequency="5"
        Value="{Binding BaseBearing}" />
    <TextBlock
        Grid.Column="2"
        VerticalAlignment="Center"
        Foreground="White"
        Text="{Binding BaseBearing, StringFormat='{}{0:F0}°'}" />
</Grid>
```

### ActionItem 20.4.2: GMapPropertyPidsControl.cs에 BaseBearing 속성 추가 [ ]

**파일**: `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapProperties/GMapPropertyPidsControl.cs`

```csharp
public double BaseBearing
{
    get => (SelectedMarker as GMapPidsMarker)?.Model.BaseBearing ?? 0.0;
    set
    {
        if (SelectedMarker is GMapPidsMarker pidsMarker)
        {
            pidsMarker.Model.BaseBearing = value;
            OnPropertyChanged(nameof(BaseBearing));
        }
    }
}
```

---

## Phase 20.5: 통합 테스트 및 검증 (BEHAVIORAL)

### ActionItem 20.5.1: 수동 통합 테스트 시나리오 [ ]

1. **신규 Symbol 생성**:
   - BaseBearing = 90도 설정
   - FOV가 정동 방향(90도)으로 생성됨
   - DetectionBearing = 90도

2. **사용자 회전**:
   - FOV를 45도 회전
   - DetectionBearing = 135도 (BaseBearing + 45)
   - BaseBearing = 90도 유지

3. **DB 저장/로드**:
   - Symbol 저장
   - 애플리케이션 재시작
   - Symbol 로드 시 BaseBearing = 90도
   - DetectionBearing = 90도로 초기화

4. **하위 호환성**:
   - 기존 Symbol (BaseBearing = 0)
   - DetectionBearing = 0도 (정북 방향)

### ActionItem 20.5.2: 회귀 테스트 [ ]

- 기존 Symbol FOV 기능 정상 동작 확인
- FOV 색상, 투명도, 거리, 각도 설정 정상 확인

---

## Phase 20 진행 상태

| Phase | 내용 | 상태 |
|-------|------|------|
| 20.1 | BaseBearing 속성 추가 (2 Tests + 1 ActionItem) | [ ] |
| 20.2 | Database Schema (4 ActionItems) | [ ] |
| 20.3 | FOV 생성 로직 (2 Tests + 2 ActionItems) | [ ] |
| 20.4 | UI 구현 (2 ActionItems) | [ ] |
| 20.5 | 통합 테스트 (2 ActionItems) | [ ] |

**총 테스트**: 4개 (Unit Tests)
**총 ActionItem**: 11개

**핵심 변경사항**:
- PidsSymbolModel에 `BaseBearing` 속성 추가 (DB 저장 O)
- DetectionRange, DetectionAngle, DetectionBearing은 런타임 전용 (DB 저장 X)
- FOV 생성 시 `DetectionBearing = BaseBearing`로 초기화
- UI에 BaseBearing Slider 추가 (0~360도)
