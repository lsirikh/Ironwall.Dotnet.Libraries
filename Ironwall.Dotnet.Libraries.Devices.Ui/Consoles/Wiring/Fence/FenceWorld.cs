using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>펜스 뷰가 그리는 센서 모양 갈래(FR-17) — 제품군별.</summary>
public enum FenceKind
{
    /// <summary>스마트 복합센서 II(후드형) — 모르는 종류도 이 모양.</summary>
    Smart = 0,
    /// <summary>복합센서(기둥 위 볼 마운트).</summary>
    Multi = 1,
    /// <summary>펜스센서(철망 레일 위 작은 박스).</summary>
    Fence = 2,
    /// <summary>지진동센서(땅속 막대).</summary>
    Underground = 3,
}

/// <summary>
/// 목업(<c>docs/design/wiring-fence-view-mockup.html</c> · <c>proj()</c> · <c>P()</c>)의 비스듬한 투영.
/// <c>k</c> = 1 이면 입체(35° 느낌), 0 이면 평면. x 는 깊이만큼만 밀리고 같은 깊이의 센서끼리 순서가 바뀌지 않는다.
/// </summary>
/// <remarks>
/// 2.5D 몸체는 <c>Viewport3D</c> 가 아니라 이 투영으로 <b>2D 에 그린다</b> — 원격 데스크톱(Tier 0)에서 3D 층의 비용을 피하고,
/// 목업과 같은 좌표를 그대로 쓰기 위해서다(F-3 결정). 지도 3D 몸체(<c>HousingModels</c>) 재사용은 범위 밖으로 둔다.
/// </remarks>
/// <param name="K">1 = 입체 · 0 = 평면.</param>
/// <param name="ZOffset">모든 깊이에 더하는 값 — 뒤를 보는 기둥 센서(FR-20)를 기둥 반대쪽에 그릴 때만 쓴다(0 이면 목업 그대로).</param>
/// <param name="YLift">모든 높이에 더하는 값 — 설치 자리(기둥 위 · 망 가운데 · 담 위 …) 높이로 센서 칩을 올리고 내릴 때(fence-wiring-editor FR-07).</param>
public readonly record struct FenceProjector(double K, double ZOffset = 0, double YLift = 0)
{
    /// <summary>펜스 높이(세계 단위).</summary>
    public const double H = 130;
    /// <summary>기둥 폭 · 깊이.</summary>
    public const double PW = 10;
    public const double PD = 10;
    /// <summary>지진동센서 · 지중 선의 깊이(펜스 안쪽).</summary>
    public const double UZ = 70;
    /// <summary>지면 단면 깊이(한 줄 모양).</summary>
    public const double SEC = 62;

    public double De => PD * K;
    public double Sh => 0.46 * K;
    public double Cy => 1 - 0.17 * K;
    public double Cz => 0.62 - 0.12 * K;

    /// <summary>세계 (x 가로 · y 높이 · z 깊이) → 그림 좌표(y 아래로 +).</summary>
    public Point P(double x, double y, double z) => new(x - (z + ZOffset) * Sh, -(y + YLift) * Cy + (z + ZOffset) * Cz);

    public static FenceProjector Tilt => new(1);
    public static FenceProjector Flat => new(0);
}

/// <summary>한 장면의 센서 한 대 — 그리기 · 속성 칸에 필요한 사실.</summary>
public sealed record FenceSensor(
    int Key,
    int Id,
    int Number,
    string Name,
    EnumDeviceType Type,
    int? Channel,
    int Line,
    int Order,
    int? OppositeOrder,
    bool IsSuggested,
    bool IsChanged,
    bool IsDuplicateNumber,
    WiringFacing Facing = WiringFacing.Front,
    WiringYaw Yaw = WiringYaw.Away)
{
    public FenceKind Kind => FenceWorld.KindOf(Type);

    /// <summary>센서 모델의 입체 모양(sensor-mount-preset FR-18) — 모델을 모르면 <c>null</c>(장비 종류로 정한다).</summary>
    public SensorShape? ModelShape { get; init; }

    /// <summary>그릴 모양 — 모델 모양, 없으면 장비 종류(PIR · 광케이블 · 스마트2 는 스마트 상자가 아닌 제 모양).</summary>
    public SensorShape DrawShape => ModelShape ?? FenceWorld.ShapeOf(Type);

    /// <summary>
    /// 속성 프리셋의 탐지 거리(m · sensor-mount-preset FR-14 "그림" 쓰임) — 없으면 <c>null</c>(종류별 기본 <see cref="FenceWorld.RangeOf(FenceKind)"/>).
    /// </summary>
    public double? RangeM { get; init; }

    /// <summary>보는 쪽이 있는 센서인가(FR-20 — 기둥에 다는 스마트 복합 · 복합).</summary>
    public bool HasFacing => WiringTopology.SupportsFacing(Type);

    /// <summary>뒤(펜스 내부)를 보는 기둥 센서 — 기둥 반대쪽에 그리고 칩에 "뒤" 표지.</summary>
    public bool IsBackFacing => HasFacing && Facing == WiringFacing.Back;

    /// <summary>
    /// 칩의 큰 글자 — <b>센서 번호</b>(장비번호 · 개념도 · 표와 같은 수 · 재검토 렌더). 사슬 위치 · Ch1/Ch2 에서 센 수는 툴팁 · 속성 칸에 남는다.
    /// 옛 가지 모양만 "L2"/"R1".
    /// </summary>
    public string Big(WiringShape shape) => shape == WiringShape.TwoBranch
        ? $"{(Line == WiringSpec.LINE_PRIMARY ? "L" : "R")}{Order}"
        : $"{Number}";

    /// <summary>링의 양 포트 번호 "A3 · B32". 링이 아니면 빈 글자.</summary>
    public string PortText => OppositeOrder is { } b ? $"A{Order} · B{b}" : string.Empty;
}

/// <summary>줌이 낮을 때 한 덩어리로 그리는 단위 — 센서 하나 · 펜스센서 묶음 · 제어기(가지 · 한 줄).</summary>
/// <param name="Key">센서 키 · 묶음은 첫 센서 키 · 제어기는 <see cref="FenceWorld.CONTROLLER_KEY"/>.</param>
public sealed record FenceUnit(int Key, IReadOnlyList<int> Keys, bool IsGroup, bool IsController)
{
    public int Count => Keys.Count;
}

/// <summary>칩이 붙는 물체 자리에 닿는 방식(sensor-mount-preset FR-22).</summary>
public enum FenceSeatMode
{
    /// <summary>몸 가운데를 붙는 높이에 두되 몸이 물체 범위 안에 들게(기둥 옆 · 메쉬 · 담 앞면 · 코일 속 · 바닥).</summary>
    Within = 0,
    /// <summary>몸 밑이 붙는 높이에 얹힌다(기둥 꼭대기 · 담 위 · 코일 위).</summary>
    Sit = 1,
    /// <summary>몸 위가 붙는 높이에 매달린다(상단 레일).</summary>
    Hang = 2,
}

/// <summary>
/// 위치 프리셋 센서가 붙은 물체 자리(세계 단위 · sensor-mount-preset FR-22) — 칩이 이 점에 닿게 선다(입체 · 평면 같은 값).
/// </summary>
/// <param name="X">붙는 점 가로(세계 단위) — 칩 x 와 같다(옆으로 벌리지 않는다).</param>
/// <param name="Height">그림의 물체 위 붙는 높이(세계 단위) — 기둥 꼭대기면 갓 위, 레일이면 그려진 레일 선.</param>
/// <param name="Low">물체 범위 아래(세계 단위).</param>
/// <param name="High">물체 범위 위(세계 단위).</param>
/// <param name="Anchor">붙는 곳.</param>
/// <param name="Mode">닿는 방식.</param>
public sealed record FenceMountMark(double X, double Height, double Low, double High, SensorMountAnchor Anchor, FenceSeatMode Mode);

/// <summary>
/// "권장 간격 넘음" 한 쌍(sensor-mount-preset FR-14) — 이웃한 같은 모델 센서 둘 · 둘 사이(m) · 속성 프리셋 권장 설치 간격(m).
/// </summary>
public sealed record FenceSpacingAlert(int Left, int Right, double Metres, double Limit)
{
    /// <summary>표지 글자 — "간격 22.5m · 권장 20m".</summary>
    public string Text => $"간격 {Metres:0.#}m · 권장 {Limit:0.#}m";
}

/// <summary>
/// 펜스 뷰의 세계 배치 — <b>순수</b>(NFR-01). 센서 x 는 <see cref="FenceSlotLayout"/> 의 누적 거리(FR-17: 이웃 기본 간격 중 작은 값 · 실측 우선)에
/// 모양별 배율과 원점을 입힌 값이다. 드롭 자리 · 함체 틈 · 묶음 · 전체 보기 경계를 한곳에서 낸다.
/// </summary>
public sealed class FenceWorld
{
    /// <summary>제어기(함체) 단위 키 — 센서 키와 겹치지 않는다.</summary>
    public const int CONTROLLER_KEY = int.MinValue + 1;

    /// <summary>
    /// 펜스 높이 <see cref="FenceProjector.H"/>(세계 단위)가 몇 m 인가 — 망 높이(m)를 세계 높이로 옮기는 기준(철조망 기본 2.4m).
    /// 가로(<see cref="Upm"/>)와 세로 배율이 다르다 — 목업처럼 높이를 과장해 읽히게 한다(Viewport3D 가 아닌 2.5D · FR-02).
    /// </summary>
    public const double REFERENCE_HEIGHT_M = 2.4;

    /// <summary>같은 자리에 센서가 여럿이면 옆으로 벌리는 간격(세계 단위).</summary>
    public const double STACK_DX = 30;

    private readonly Dictionary<int, double> _lift = new();
    private readonly Dictionary<int, double> _coil = new();

    /// <summary>이 줌보다 작으면 펜스센서 묶음으로 접는다(FR-18) — <see cref="FenceSlotLayout.GROUP_ZOOM_THRESHOLD"/> 와 같다.</summary>
    public const double GROUP_ZOOM = FenceSlotLayout.GROUP_ZOOM_THRESHOLD;

    private readonly Dictionary<int, double> _x = new();
    private readonly Dictionary<int, FenceSensor> _sensors;

    private FenceWorld(WiringShape shape, WiringChain chain, IReadOnlyDictionary<int, FenceSensor> sensors, double upm, double postM, double ctrlGapM)
    {
        Shape = shape;
        Chain = chain;
        _sensors = sensors.ToDictionary(p => p.Key, p => p.Value);
        Upm = upm;
        PostM = postM;
        CtrlGapM = ctrlGapM;
    }

    #region - Facts -
    public WiringShape Shape { get; }
    public WiringChain Chain { get; }

    /// <summary>1m 가 몇 세계 단위인가(목업 <c>upm</c>).</summary>
    public double Upm { get; }

    /// <summary>기둥 간격(m) — 링 밖 모양에서 격자처럼 세운다.</summary>
    public double PostM { get; }

    /// <summary>제어기에서 첫 센서까지(m) — 가지 · 한 줄.</summary>
    public double CtrlGapM { get; }

    /// <summary>이 세계를 세운 간격 표 — 펜스센서 칸 폭(기둥)이 현장 간격을 따른다.</summary>
    public WiringSpacingTable Spacing { get; private set; } = WiringSpacingTable.Default;

    /// <summary>제어기(함체) x.</summary>
    public double ControllerX { get; private set; }

    /// <summary>센서 x(세계 단위).</summary>
    public IReadOnlyDictionary<int, double> X => _x;

    public IReadOnlyDictionary<int, FenceSensor> Sensors => _sensors;

    /// <summary>화면 왼쪽 → 오른쪽 센서 키(체인 순서 그대로).</summary>
    public IReadOnlyList<int> Seq => Chain.Keys;

    /// <summary>지면 깊이 끝(목업 <c>gz</c>).</summary>
    public double GroundDepth => Shape switch { WiringShape.Ring => 162, WiringShape.TwoBranch => 150, _ => 70 };

    /// <summary>이름표 높이(목업 <c>labelTop</c>).</summary>
    public double LabelTop => Shape switch { WiringShape.Ring => FenceProjector.H + 44, WiringShape.TwoBranch => FenceProjector.H + 88, _ => 92 };

    /// <summary>삽입 막대의 아래 · 위 높이(목업 <c>insY</c>).</summary>
    public (double Low, double High) InsertionSpan => Shape switch
    {
        WiringShape.Ring => (-4, FenceProjector.H + 10),
        WiringShape.TwoBranch => (-4, FenceProjector.H + 52),
        _ => (-54, 40),
    };

    /// <summary>덧그림 깊이 — 한 줄(지중)은 땅속.</summary>
    public double OverlayDepth => Shape == WiringShape.Line ? FenceProjector.UZ : 0;

    public double MinX { get; private set; }
    public double MaxX { get; private set; }

    /// <summary>펜스 구성(망 목록 · 자리)으로 세운 세계인가 — 아니면 옛 간격 표 배치.</summary>
    public WiringFenceLayout? Layout { get; private set; }

    /// <summary>망 · 기둥 자리(m) — 펜스 구성으로 세웠을 때만.</summary>
    public FenceGeometry? Geometry { get; private set; }

    public bool IsLayout => Geometry is { Panels.Count: > 0 };

    /// <summary>1m 높이가 몇 세계 단위인가(<see cref="REFERENCE_HEIGHT_M"/> 가 <see cref="FenceProjector.H"/>).</summary>
    public double Vpm { get; private set; } = FenceProjector.H / REFERENCE_HEIGHT_M;

    /// <summary>센서 칩을 설치 자리 높이로 올리는 값(세계 단위 · 없으면 0).</summary>
    public double LiftOf(int key) => _lift.TryGetValue(key, out var lift) ? lift : 0;

    /// <summary>
    /// 윤형 코일 자리의 센서면 그 코일 반지름(세계 단위), 아니면 0 — 칩이 몸을 코일 가운데에 두고 번호판을 코일 위로 올린다(글자가 코일 선을 지나지 않게).
    /// </summary>
    public double CoilOf(int key) => _coil.TryGetValue(key, out var r) ? r : 0;

    private readonly HashSet<int> _plateAbove = new();

    /// <summary>
    /// 번호판을 몸 위에 둘 센서 — 윤형 망 위 줄의 코일 밖 센서(몸 아래에 두면 번호판이 코일 선에 걸린다). 그 밖은 몸 아래.
    /// </summary>
    public bool PlateAboveOf(int key) => _plateAbove.Contains(key);

    /// <summary>
    /// 자리 <paramref name="mount"/> 의 설치 높이(세계 단위 · 칩을 올리기 전 목표 높이) — 윤형 코일은 코일 가운데, 그 밖은 줄을 따른 높이(<see cref="FenceLayoutMath.LaneHeightM"/>).
    /// 높이 단계 끌기의 안내선이 쓴다. 펜스 구성이 아니면 0.
    /// </summary>
    public double MountHeight(SensorMountSpec mount)
    {
        if (Geometry is not { Panels.Count: > 0 } geometry || Layout is not { } layout || mount is null) return 0;
        var m = FenceLayoutMath.Normalize(mount, layout.Panels);
        if (m.Spot == FenceMountSpot.RazorCoil)
            return FenceStyleArt.CoilCenterHeight(geometry.Panels[m.Panel].Spec.HeightM * Vpm) + m.HeightOffsetM * Vpm;
        return FenceLayoutMath.LaneHeightM(m, geometry, layout.HasUpperSensors || m.Lane == FenceLane.Upper) * Vpm;
    }

    /// <summary>그림 맨 위 높이(세계 단위) — 가장 높은 망 · 기둥 · 코일 · 센서.</summary>
    public double TopHeight { get; private set; } = FenceProjector.H + 40;

    /// <summary>탐지 반경(m) — 복합 20 · 지진동 15(FR-19).</summary>
    public static double RangeOf(FenceKind kind) => kind switch { FenceKind.Multi => 20, FenceKind.Underground => 15, _ => 0 };

    /// <summary>
    /// 센서 한 대의 탐지 반경(m) — 속성 프리셋 탐지 거리(<see cref="FenceSensor.RangeM"/>)가 있으면 그것, 없으면 종류별 기본(sensor-mount-preset FR-14).
    /// </summary>
    public static double RangeOf(FenceSensor sensor)
        => sensor is null ? 0 : sensor.RangeM is { } r ? (double.IsFinite(r) && r > 0 ? r : 0) : RangeOf(sensor.Kind);

    /// <summary>키로 찾은 센서의 탐지 반경(m) — 모르는 키면 0.</summary>
    public double RangeOfKey(int key) => _sensors.TryGetValue(key, out var s) ? RangeOf(s) : 0;

    private IReadOnlyList<FenceSpacingAlert> _spacingAlerts = Array.Empty<FenceSpacingAlert>();

    /// <summary>"권장 간격 넘음" 쌍(sensor-mount-preset FR-14 · W3 "알림" 쓰임) — 둘 다 이 장면에 있는 쌍만.</summary>
    public IReadOnlyList<FenceSpacingAlert> SpacingAlerts => _spacingAlerts;

    /// <summary>
    /// 권장 간격 넘는 쌍을 싣는다(뷰모델 <c>PresetSpacingAlerts</c> — 망 자리 m 로 잰 값). 장면에 없는 센서가 낀 쌍은 버린다.
    /// </summary>
    public void UseSpacingAlerts(IEnumerable<FenceSpacingAlert>? alerts)
        => _spacingAlerts = (alerts ?? Enumerable.Empty<FenceSpacingAlert>()).Where(a => _x.ContainsKey(a.Left) && _x.ContainsKey(a.Right)).ToList();

    /// <summary>알림 표지 x(세계 단위) — 두 센서의 가운데.</summary>
    public double SpacingAlertX(FenceSpacingAlert alert) => (_x[alert.Left] + _x[alert.Right]) / 2;

    /// <summary>장비 종류의 기본 모양(모델을 모를 때 · sensor-mount-preset FR-18).</summary>
    public static SensorShape ShapeOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.Multi => SensorShape.Multi,
        EnumDeviceType.Fence or EnumDeviceType.Fence_Group => SensorShape.Fence,
        EnumDeviceType.Underground => SensorShape.Underground,
        EnumDeviceType.PIR => SensorShape.Pir,
        EnumDeviceType.OpticalCable or EnumDeviceType.Cable => SensorShape.Optical,
        EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartMultisensor2 => SensorShape.Smart2,
        _ => SensorShape.Smart,
    };

    public static FenceKind KindOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.Multi => FenceKind.Multi,
        EnumDeviceType.Fence => FenceKind.Fence,
        EnumDeviceType.Underground => FenceKind.Underground,
        _ => FenceKind.Smart,
    };
    #endregion

    #region - Build -
    /// <summary>체인과 센서 사실로 세계를 세운다.</summary>
    /// <param name="measuredGapMetres">실측 거리(왼쪽 이웃 → 이 센서, m). 없으면 종류별 기본 간격.</param>
    public static FenceWorld Build(WiringChain chain, IReadOnlyDictionary<int, FenceSensor> sensors,
                                   IReadOnlyDictionary<int, double>? measuredGapMetres = null, WiringSpacingTable? spacing = null)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(sensors);
        var shape = chain.Shape;
        var (upm, postM, ctrlGapM) = shape switch
        {
            WiringShape.Ring => (68.0 / 6, 6.0, 0.0),
            WiringShape.TwoBranch => (68.0 / 6, 5.0, 5.0),
            _ => (4.4, 12.5, 12.5),
        };
        var world = new FenceWorld(shape, chain, sensors, upm, postM, ctrlGapM);

        EnumDeviceType TypeOf(int key) => sensors.TryGetValue(key, out var s) ? s.Type : EnumDeviceType.NONE;
        var layout = FenceSlotLayout.Build(chain, TypeOf, new FenceLayoutOptions { PixelsPerMetre = 1, MeasuredGapMetres = measuredGapMetres, Spacing = spacing });
        world.Spacing = layout.Spacing;
        var m = layout.Slots.Select(s => s.Metres).ToList();
        var keys = chain.Keys;

        switch (shape)
        {
            case WiringShape.Ring:
                for (var i = 0; i < keys.Count; i++) world._x[keys[i]] = m[i] * upm;
                world.ControllerX = world.GapMid(chain.ControllerGap);
                break;

            case WiringShape.TwoBranch:
                var g = chain.ControllerGap;
                for (var i = 0; i < g; i++) world._x[keys[i]] = -(ctrlGapM + (m[g - 1] - m[i])) * upm;
                for (var i = g; i < keys.Count; i++) world._x[keys[i]] = (ctrlGapM + (m[i] - m[g])) * upm;
                world.ControllerX = 0;
                break;

            default:
                for (var i = 0; i < keys.Count; i++) world._x[keys[i]] = (ctrlGapM + m[i]) * upm;
                world.ControllerX = 0;
                break;
        }

        var xs = world._x.Values.Append(world.ControllerX).ToList();
        world.MinX = xs.Min();
        world.MaxX = xs.Max();
        return world;
    }
    #endregion

    /// <summary>
    /// 펜스 구성(망 목록 · 센서 자리)으로 세운다(fence-wiring-editor FR-01 · FR-02 · FR-07) — 센서 x = 자리의 가로(m) × <see cref="Upm"/>
    /// (같은 자리 여럿은 옆으로 벌린다), 센서 높이 = 자리 높이(m) × <see cref="Vpm"/>. 구성이 꺼졌거나 망이 없으면 옛 배치.
    /// </summary>
    public static FenceWorld FromLayout(WiringChain chain, IReadOnlyDictionary<int, FenceSensor> sensors, WiringFenceLayout? layout, WiringSpacingTable? spacing = null)
        => FromLayout(chain, sensors, layout, spacing, null);

    /// <summary>
    /// <paramref name="poseOf"/>(sensor-mount-preset FR-17) — 위치 프리셋으로 서는 센서의 자세(프리셋 + 따로 고친 값 · 편집기와 같은 계산). 그 센서는
    /// 가로 = 칸 시작 + 칸 안 몫 × 칸 폭, 높이 = 자세 높이(기둥 위로 뜨거나 바닥을 뚫지 않게 이미 범위 안)로 선다. <c>null</c> 이면 9자리 계산 그대로.
    /// </summary>
    public static FenceWorld FromLayout(WiringChain chain, IReadOnlyDictionary<int, FenceSensor> sensors, WiringFenceLayout? layout, WiringSpacingTable? spacing,
                                        Func<int, SensorMountPose?>? poseOf)
        => FromLayout(chain, sensors, layout, spacing, poseOf, null);

    /// <summary>
    /// <paramref name="bayOf"/> — 프리셋 센서가 서는 칸(뷰모델 <c>FencePresetBayOf</c> · 자세를 계산한 칸과 같다). 없으면 자세의 가로 몫으로 고른다
    /// (기둥 자리 + 몫 0.5 미만이면 옆 칸으로 잘못 그리던 길 — FR-22 조사 5).
    /// </summary>
    public static FenceWorld FromLayout(WiringChain chain, IReadOnlyDictionary<int, FenceSensor> sensors, WiringFenceLayout? layout, WiringSpacingTable? spacing,
                                        Func<int, SensorMountPose?>? poseOf, Func<int, int?>? bayOf)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(sensors);
        if (layout is null || !layout.IsActive || layout.Panels.Count == 0) return Build(chain, sensors, (IReadOnlyDictionary<int, double>?)null, spacing);

        var upm = 68.0 / 6;
        var geometry = layout.Geometry;
        var world = new FenceWorld(WiringShape.Ring, chain, sensors, upm, FencePanelSpec.DEFAULT_SPAN_M, 0)
        {
            Layout = layout,
            Geometry = geometry,
            Spacing = spacing ?? WiringSpacingTable.Default,
        };
        var vpm = world.Vpm;

        var seats = chain.Keys.Select(k => (Key: k, Mount: FenceLayoutMath.Normalize(layout.MountOf(k) ?? new SensorMountSpec(0, FenceMountSpot.PostTop), layout.Panels)))
                              .ToList();
        var stacks = seats.GroupBy(t => (t.Mount.Lane, FenceLayoutMath.SeatOf(t.Mount))).ToDictionary(g => g.Key, g => g.Select(t => t.Key).ToList());   // 줄마다 따로(위 · 아래 줄은 높이가 다르다)
        // 같은 망의 망 자리 센서는 망 길이를 고르게 나눠 선다(담은 기둥이 없어 가운데 한 점에 겹쳤다 — 헤디드 r21) · 기둥 자리만 옆으로 벌린다
        var spread = FenceLayoutMath.SpreadXs(seats, geometry, layout.ControllerEnd);
        var top = FenceProjector.H + 40;
        var twoLanes = layout.HasUpperSensors;
        foreach (var (key, mount) in seats)
        {
            if (poseOf?.Invoke(key) is { } rawPose)
            {
                // 위치 프리셋 — 편집기와 같은 자세(칸 안 몫 · 높이)를 뷰모델과 같은 칸에. 그림도 같은 불변식으로 한 번 더 붙인다(FR-22).
                var bayIndex = Math.Clamp(bayOf?.Invoke(key) ?? SensorMountMigration.BayIndexOf(mount, geometry.Panels.Count, rawPose.AlongRatio), 0, geometry.Panels.Count - 1);
                var bay = geometry.Panels[bayIndex];
                var pose = SensorMountResolver.Clamp(rawPose, FenceSampleBay.FromPanel(bay.Spec));
                var sensor = sensors.TryGetValue(key, out var ps) ? ps : null;
                var presetKind = sensor?.Kind ?? FenceKind.Smart;
                var x = (bay.StartM + bay.SpanM * pose.AlongRatio) * upm;
                world._x[key] = x;
                var post = geometry.Posts[Math.Clamp(pose.AlongRatio >= 0.5 ? bay.Index + 1 : bay.Index, 0, geometry.Posts.Count - 1)];
                var seat = SeatOf(pose, bay.Spec, post.HeightM * vpm + POST_CAP_H, vpm);
                var span = sensor is null ? (Bottom: 43.0, Top: 99.0) : FenceScene.BodySpan(sensor, mounted: true);
                var presetLift = presetKind == FenceKind.Underground ? 0 : MountLift(seat.Mode, seat.Height, seat.Low, seat.High, span.Bottom, span.Top);
                if (pose.Anchor is SensorMountAnchor.Razor or SensorMountAnchor.RazorLoop && bay.Spec.Style == EnumFenceStyle.ChainLinkRazor)
                    world._coil[key] = FenceStyleArt.CoilRadius(bay.Spec.HeightM * vpm);
                world._lift[key] = presetLift;
                world._marks[key] = new FenceMountMark(x, seat.Height, seat.Low, seat.High, pose.Anchor, seat.Mode);
                // 화면 벌리기가 칩을 옮겨도 되는 가로 범위 — 기둥은 그 한 점, 그 밖(레일 · 메쉬 · 코일 · 담 · 바닥)은 그 칸 안
                world._mountRange[key] = SensorMountResolver.KindOf(pose.Anchor) == SensorAnchorKind.Post
                    ? (x, x)
                    : (Math.Min(x, bay.StartM * upm), Math.Max(x, (bay.StartM + bay.SpanM) * upm));
                top = Math.Max(top, presetLift + (presetKind == FenceKind.Multi ? FenceProjector.H + 56 : 110));
                continue;
            }
            var point = FenceLayoutMath.PointOf(mount, geometry);
            var heightM = FenceLayoutMath.LaneHeightM(mount, geometry, twoLanes);                 // 위 줄 = 꼭대기 위 · 아래 줄 = 망 위(FR-18)
            var stack = stacks[(mount.Lane, FenceLayoutMath.SeatOf(mount))];
            var dx = mount.IsPostSpot ? (stack.IndexOf(key) - (stack.Count - 1) / 2.0) * STACK_DX : 0;
            world._x[key] = (spread.TryGetValue(key, out var xm) ? xm : point.XM) * upm + dx;
            var kind = sensors.TryGetValue(key, out var s) ? s.Kind : FenceKind.Smart;
            double lift;
            if (mount.Spot == FenceMountSpot.RazorCoil)
            {
                // 윤형 코일(펜스센서는 윤형과 같이) — 몸 가운데를 2.5D 코일 가운데에(그림과 같은 식 · 망 높이 → 세계 높이)
                var fenceH = geometry.Panels[mount.Panel].Spec.HeightM * vpm;
                lift = LiftFor(kind, FenceMountSpot.PanelCenter, FenceStyleArt.CoilCenterHeight(fenceH) + mount.HeightOffsetM * vpm);
                world._coil[key] = FenceStyleArt.CoilRadius(fenceH);
            }
            // 위 줄 칩은 꼭대기 위에 올라앉는다 — 몸 가운데를 목표 높이 + 반 칩(약 28)에 맞춘다
            else
            {
                lift = mount.Lane == FenceLane.Upper ? LiftFor(kind, FenceMountSpot.PanelCenter, heightM * vpm + UPPER_CHIP_HALF) : LiftFor(kind, mount.Spot, heightM * vpm);
                var overRazor = mount.IsPostSpot ? geometry.Posts[mount.Panel].HasRazor : geometry.Panels[mount.Panel].Spec.Style == EnumFenceStyle.ChainLinkRazor;
                if (mount.Lane == FenceLane.Upper && overRazor) world._plateAbove.Add(key);
            }
            world._lift[key] = lift;
            top = Math.Max(top, lift + (kind == FenceKind.Multi ? FenceProjector.H + 56 : 110));
        }
        // 모양이 바뀌는 곳(담 ↔ 철망 · 기둥 자리 ↔ 망 가운데)에서 이웃 칩이 거의 같은 x 에 서면 번호판이 겹친다(검토 V3 · 재검토) —
        // 줄마다(위 · 아래 줄은 높이가 달라 서로 겹치지 않는다) 가로 순서를 지킨 채 겹친 것만 칩 폭 + 틈 이상으로 벌린다(무리 가운데는 제자리 평균).
        var chainIndex = chain.Keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i);
        var gap = ChipGap(sensors.Values.Where(v => chainIndex.ContainsKey(v.Key)));
        // 위치 프리셋 센서는 벌리지 않는다 — 옆으로 밀면 기둥 · 망에서 떨어져 허공에 뜬다(헤디드 83169 · FR-22 조사 6).
        // 9자리 센서는 펜스 시작 ~ 끝 안에서만 벌린다 — 제어기 쪽 끝에 몰린 칩이 펜스 밖 허공으로 밀렸다(실창 r29 5회차 031).
        foreach (var lane in seats.GroupBy(t => t.Mount.Lane))
        {
            var ordered = lane.Select(t => t.Key).Where(k => world._x.ContainsKey(k) && !world._marks.ContainsKey(k)).OrderBy(k => world._x[k]).ThenBy(k => chainIndex[k]).ToList();
            var apart = Separate(ordered.Select(k => world._x[k]).ToList(), gap, 0, geometry.LengthM * upm, layout.ControllerEnd == FenceControllerEnd.Left);
            for (var i = 0; i < ordered.Count; i++) world._x[ordered[i]] = apart[i];
        }
        // 윤형 — 받침(5) + 코일 지름(펜스 높이의 40%) 위까지
        foreach (var post in geometry.Posts) top = Math.Max(top, post.HeightM * vpm + (post.HasRazor ? 12 + 2 * FenceStyleArt.CoilRadius(post.HeightM * vpm) : 12));
        foreach (var panel in geometry.Panels)
            top = Math.Max(top, panel.Spec.HeightM * vpm + (panel.Spec.Style == EnumFenceStyle.ChainLinkRazor ? 12 + 2 * FenceStyleArt.CoilRadius(panel.Spec.HeightM * vpm) : 14));
        world.TopHeight = top;

        // 두 줄 형상(v0.3 §1-0b) — 제어기는 펜스 왼쪽 끝 또는 오른쪽 끝 바깥(케이블 보기의 함체 · 끌면 반대쪽으로)
        var placedXs = world._x.Values.Append(0).Append(geometry.LengthM * upm).ToList();
        world.ControllerX = layout.ControllerEnd == FenceControllerEnd.Left
            ? placedXs.Min() - CONTROLLER_LEAD * upm
            : placedXs.Max() + CONTROLLER_LEAD * upm;
        var xs = world._x.Values.Append(0).Append(geometry.LengthM * upm).Append(world.ControllerX).ToList();
        world.MinX = xs.Min();
        world.MaxX = xs.Max();
        return world;
    }

    /// <summary>두 줄 형상에서 제어기(함체)를 펜스 끝 바깥에 두는 거리(m).</summary>
    public const double CONTROLLER_LEAD = 5;

    #region - 붙임(sensor-mount-preset FR-22) -
    private readonly Dictionary<int, FenceMountMark> _marks = new();

    /// <summary>기둥 갓 높이(세계 단위 · <see cref="FenceScene.StaticLayout"/> 의 기둥 갓 h ~ h + 5).</summary>
    public const double POST_CAP_H = 5;

    /// <summary>철망 상단 레일 선이 펜스 높이에서 내려온 거리(세계 단위 · 그림의 레일 선 h − 8).</summary>
    public const double RAIL_INSET = 8;

    /// <summary>담 갓 높이(세계 단위 · 담 위 h ~ h + 7).</summary>
    public const double WALL_CAP_H = 7;

    /// <summary>위치 프리셋 센서가 붙은 물체 자리 — 프리셋으로 서지 않는 센서면 <c>null</c>.</summary>
    public FenceMountMark? MountMarkOf(int key) => _marks.TryGetValue(key, out var m) ? m : null;

    /// <summary>위치 프리셋으로 서서 물체에 붙은 센서인가 — 옆으로 벌리지 않는다.</summary>
    public bool IsMounted(int key) => _marks.ContainsKey(key);

    private readonly Dictionary<int, (double Lo, double Hi)> _mountRange = new();

    /// <summary>
    /// 붙은 센서 칩을 화면에서 옆으로 옮겨도 그 물체 위에 남는 가로 범위(세계 단위) — 기둥이면 그 기둥 한 점, 레일 · 메쉬 · 코일 · 담 · 바닥이면 그 칸.
    /// 붙지 않은 센서면 <c>null</c>(실창 4회차: 벌리기가 기둥 1 의 칩을 기둥 없는 허공 · 제어기 상자 위로 밀었다).
    /// </summary>
    public (double Lo, double Hi)? MountRangeOf(int key) => _mountRange.TryGetValue(key, out var r) ? r : null;

    /// <summary>기둥에 붙은 프리셋 센서의 x(세계 단위) — 기둥 솎기가 그 기둥을 남긴다(기둥이 사라져 센서가 뜨지 않게).</summary>
    public IEnumerable<double> PostMountXs => _marks.Values.Where(m => SensorMountResolver.KindOf(m.Anchor) == SensorAnchorKind.Post).Select(m => m.X);

    /// <summary>
    /// 자세 → 그림의 물체 위 붙는 자리(세계 단위) — <b>순수</b>. 모델 높이(m × <paramref name="vpm"/>)를 그림에 그려진 물체(기둥 갓 · 레일 선 · 메쉬 · 코일 · 담 갓)에 맞춘다.
    /// </summary>
    /// <param name="postTop">그 기둥의 그림 꼭대기(갓 위 · 세계 단위).</param>
    public static (double Height, double Low, double High, FenceSeatMode Mode) SeatOf(SensorMountPose pose, FencePanelSpec spec, double postTop, double vpm)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(spec);
        var s = spec.Normalized();
        var h = s.HeightM * vpm;
        var t = pose.HeightM * vpm;
        switch (pose.Anchor)
        {
            case SensorMountAnchor.Ground:
                return (t, SensorMountResolver.MIN_BURIED_M * vpm, 0, FenceSeatMode.Within);
            case SensorMountAnchor.PostTop:
                return (postTop, postTop, postTop, FenceSeatMode.Sit);
            case SensorMountAnchor.PostRail or SensorMountAnchor.PostMid or SensorMountAnchor.PostLow:
                return (Math.Clamp(t, 0, postTop), 0, postTop, FenceSeatMode.Within);
            case SensorMountAnchor.Rail:
                return (h - RAIL_INSET, h - RAIL_INSET, h - RAIL_INSET, FenceSeatMode.Hang);
            case SensorMountAnchor.Razor or SensorMountAnchor.RazorLoop:
            {
                // 모델 코일(가운데 · 반지름 m)을 그림 코일(가운데 · 반지름 세계 단위)로 옮긴다 — 코일 꼭대기 점이면 코일 위에 얹힌다
                var sample = FenceSampleBay.FromPanel(s);
                var cc = FenceStyleArt.CoilCenterHeight(h);
                var r = FenceStyleArt.CoilRadius(h);
                if (pose.HeightM >= sample.CoilTopM - SensorMountSurfaces.EPS_M) return (cc + r, cc + r, cc + r, FenceSeatMode.Sit);
                var k = sample.CoilRadiusM > 0 ? (pose.HeightM - sample.CoilCenterM) / sample.CoilRadiusM : 0;
                return (cc + Math.Clamp(k, -1, 1) * r, cc - r, cc + r, FenceSeatMode.Within);
            }
            case SensorMountAnchor.WallTop:
                return (h + WALL_CAP_H, h + WALL_CAP_H, h + WALL_CAP_H, FenceSeatMode.Sit);
            case SensorMountAnchor.WallFace:
                return (Math.Clamp(t, 0, h), 0, h, FenceSeatMode.Within);
            default:
                return (Math.Clamp(t, RAIL_INSET, h - RAIL_INSET), RAIL_INSET, h - RAIL_INSET, FenceSeatMode.Within);
        }
    }

    /// <summary>
    /// 칩을 올리는 값 — <b>순수</b>. 몸(칩 좌표 <paramref name="bottom"/> ~ <paramref name="top"/>)이 붙는 높이 <paramref name="seat"/> 에 반드시 닿는다:
    /// 얹힘 = 몸 밑이 그 높이 · 매달림 = 몸 위가 그 높이 · 그 밖 = 몸 가운데를 맞추되 몸이 물체 범위(<paramref name="low"/> ~ <paramref name="high"/>) 안에 들게.
    /// 지상 물체면 몸이 땅을 뚫지 않는다(닿음이 먼저).
    /// </summary>
    public static double MountLift(FenceSeatMode mode, double seat, double low, double high, double bottom, double top)
    {
        double lift;
        switch (mode)
        {
            case FenceSeatMode.Sit: lift = seat - bottom; break;
            case FenceSeatMode.Hang: lift = seat - top; break;
            default:
                lift = seat - (bottom + top) / 2;
                if (top - bottom <= high - low) lift = Math.Clamp(lift, low - bottom, high - top);
                else lift = (low + high) / 2 - (bottom + top) / 2;
                break;
        }
        lift = Math.Clamp(lift, seat - top, seat - bottom);                // 닿음 — 붙는 높이가 몸 범위 안
        if (low >= 0 && bottom + lift < 0) lift = Math.Min(-bottom, seat - bottom);   // 땅을 뚫지 않게(닿음은 지킨다)
        return lift;
    }
    #endregion

    /// <summary>그 센서 칩 몸 가운데 높이(세계 단위) — 케이블 보기의 줄 선 높이.</summary>
    public double BodyCenterOf(int key)
        => LiftOf(key) + (_sensors.TryGetValue(key, out var s) ? s.Kind switch { FenceKind.Fence => 66, FenceKind.Multi => FenceProjector.H + 36, _ => 71 } : 71);

    /// <summary>위 줄 칩을 꼭대기 위로 올려 앉히는 반 칩 높이(세계 단위).</summary>
    public const double UPPER_CHIP_HALF = 28;

    /// <summary>
    /// 이웃 칩의 최소 간격(세계 단위) — 칩 폭(스마트 몸 32 · 번호판 20) + 틈 8. 30(같은 자리 벌림)으로는 담/철망 경계의 두 칩 번호판이 여전히 겹쳤다(재검토 렌더).
    /// </summary>
    public const double MIN_CHIP_DX = 40;

    /// <summary>
    /// 이웃 칩의 간격(세계 단위) — <see cref="MIN_CHIP_DX"/> 와 가장 넓은 번호판(배율 1 · 다섯 자리 번호 "70066" 은 판이 42) + 틈 8 중 큰 값.
    /// 고정 40 으로는 다섯 자리 번호판끼리 겹쳤다(헤디드 r21 담 위 세 대).
    /// </summary>
    public static double ChipGap(IEnumerable<FenceSensor> sensors)
    {
        var widest = (sensors ?? Enumerable.Empty<FenceSensor>()).Select(s => FenceScene.EstimateWidth(s.Big(WiringShape.Ring), 12.5) + 6).DefaultIfEmpty(0).Max();
        return Math.Max(MIN_CHIP_DX, widest + 8);
    }

    /// <summary>
    /// 순서를 지킨 채 이웃 간격을 <paramref name="minGap"/> 이상으로 — 겹친 무리만 움직이고 무리의 가운데는 원래 자리의 평균에 둔다(순수 · 시험 대상).
    /// 이미 충분히 떨어진 값은 그대로다.
    /// </summary>
    public static IReadOnlyList<double> Separate(IReadOnlyList<double> xs, double minGap)
    {
        var n = xs?.Count ?? 0;
        var result = new double[n];
        if (n == 0) return result;
        // 무리: (첫 칸, 개수, Σ(x − 무리 안 자리 × 간격)) — 시작 = 합 / 개수
        var blocks = new List<(int First, int Count, double Sum)>();
        for (var i = 0; i < n; i++)
        {
            blocks.Add((i, 1, xs![i]));
            while (blocks.Count > 1)
            {
                var prev = blocks[^2];
                var cur = blocks[^1];
                if (prev.Sum / prev.Count + prev.Count * minGap <= cur.Sum / cur.Count + 1e-9) break;
                blocks.RemoveAt(blocks.Count - 1);
                blocks[^1] = (prev.First, prev.Count + cur.Count, prev.Sum + cur.Sum - cur.Count * prev.Count * minGap);
            }
        }
        foreach (var (first, count, sum) in blocks)
            for (var j = 0; j < count; j++) result[first + j] = sum / count + j * minGap;
        return result;
    }

    /// <summary>
    /// <see cref="Separate(IReadOnlyList{double}, double)"/> 를 [<paramref name="lo"/>, <paramref name="hi"/>] 안으로 — 범위를 넘은 무리만 순서 · 간격을 지킨 채 안으로 민다(순수 · 시험 대상).
    /// 범위가 모자라 다 못 들어가면 간격(겹치지 않음)을 지키고 <paramref name="keepLo"/> 쪽(제어기 쪽) 끝만 지킨다 — 넘치는 칩은 제어기 반대쪽으로 나간다(실창 r29 5회차).
    /// </summary>
    public static IReadOnlyList<double> Separate(IReadOnlyList<double> xs, double minGap, double lo, double hi, bool keepLo)
    {
        var apart = Separate(xs, minGap);
        // y = x − 자리 × 간격 은 순서대로 커진다 — y 를 범위로 자르면 순서 · 간격이 그대로 남는다
        var top = hi - (apart.Count - 1) * minGap;
        return apart.Select((x, i) =>
        {
            var y = x - i * minGap;
            y = top >= lo ? Math.Clamp(y, lo, top) : keepLo ? Math.Max(y, lo) : Math.Min(y, top);
            return y + i * minGap;
        }).ToList();
    }

    /// <summary>
    /// 자리 높이(세계 단위)로 칩을 올리는 값 — 위 자리(기둥 위 · 담 위)는 칩의 <b>윗선</b>을, 그 밖은 몸 <b>가운데</b>를 맞춘다.
    /// 지진동은 늘 땅속이라 0.
    /// </summary>
    public static double LiftFor(FenceKind kind, FenceMountSpot spot, double targetHeight)
    {
        if (kind == FenceKind.Underground) return 0;
        var top = spot is FenceMountSpot.PostTop or FenceMountSpot.WallTop;
        var anchor = kind switch
        {
            FenceKind.Multi => top ? FenceProjector.H : FenceProjector.H + 36,
            FenceKind.Fence => top ? 76 : 66,
            _ => top ? 99 : 71,
        };
        return targetHeight - anchor;
    }

    #region - Gaps · drop -
    /// <summary>체인 틈 g 의 가운데 x(목업 <c>gapMid</c>) — 양 끝은 끝 센서에서 3m 바깥.</summary>
    public double GapMid(int gap)
    {
        var keys = Chain.Keys;
        var n = keys.Count;
        if (n == 0) return 0;
        if (gap <= 0) return _x[keys[0]] - 3 * Upm;
        if (gap >= n) return _x[keys[n - 1]] + 3 * Upm;
        return (_x[keys[gap - 1]] + _x[keys[gap]]) / 2;
    }

    /// <summary>x 에 가장 가까운 체인 틈(함체 끌기 · 목업 <c>nearestGap</c>).</summary>
    public int NearestGap(double x, int low = 0, int? high = null)
    {
        var hi = high ?? Chain.Count;
        var best = low;
        var bestDistance = double.PositiveInfinity;
        for (var g = low; g <= hi; g++)
        {
            var d = Math.Abs(GapMid(g) - x);
            if (d < bestDistance) { bestDistance = d; best = g; }
        }
        return best;
    }

    /// <summary>
    /// 세계 x 에 놓으면 어느 목록의 몇 번째 자리(옮기기 <b>전</b> 기준 · <see cref="WiringChain.PlaceInBranch"/> 규칙)인가.
    /// 링 · 한 줄 = 선 1 · 그 x 보다 왼쪽 센서 수. 양쪽 가지 = 제어기 왼쪽이면 선 1 · 그 자리와 제어기 사이 센서 수, 오른쪽이면 선 2.
    /// </summary>
    public (int Line, int Index) DropAt(double worldX)
    {
        if (Shape == WiringShape.TwoBranch)
        {
            if (worldX < ControllerX)
                return (WiringSpec.LINE_PRIMARY, Chain.Branch(WiringSpec.LINE_PRIMARY).Count(k => _x[k] > worldX));
            return (WiringSpec.LINE_SECONDARY, Chain.Branch(WiringSpec.LINE_SECONDARY).Count(k => _x[k] < worldX));
        }
        return (WiringSpec.LINE_PRIMARY, Chain.Keys.Count(k => _x[k] < worldX));
    }

    /// <summary>
    /// 삽입 막대 x — 끄는 것을 뺀 이웃 둘의 가운데(목업 <c>moveDrag</c>). 한 줄은 제어기 앞에 놓지 못한다.
    /// </summary>
    public double InsertionX(double worldX, IReadOnlyCollection<int> dragged)
    {
        var positions = Chain.Keys.Where(k => !dragged.Contains(k)).Select(k => _x[k]).ToList();
        if (Shape != WiringShape.Ring) positions.Add(ControllerX);
        positions.Sort();
        if (positions.Count == 0) return worldX;

        var i = positions.Count(x => x < worldX);
        if (Shape == WiringShape.Line && i == 0) i = 1;
        var left = i > 0 ? positions[i - 1] : positions[0] - 3 * Upm;
        var right = i < positions.Count ? positions[i] : positions[^1] + 3 * Upm;
        return (left + right) / 2;
    }

    /// <summary>VBUS 표지 틈(링만 · 3대 이상) — 함체 틈에서 ±5칸(<see cref="WiringTopology.VbusGaps"/>)을 1…N−1 로 좁히고 함체 틈은 뺀다.</summary>
    public IReadOnlyList<int> VbusGaps(int enclosureGap)
    {
        var n = Chain.Count;
        if (Shape != WiringShape.Ring || n < 3) return Array.Empty<int>();
        return WiringTopology.VbusGaps(enclosureGap, n)
            .Select(g => Math.Clamp(g, 1, n - 1))
            .Where(g => g != enclosureGap)
            .Distinct()
            .ToList();
    }

    /// <summary>탐지 빈틈(FR-19) — 이웃한 반경 원 둘이 닿지 않는 구간. (가운데 x, 빈 거리 m, 왼쪽 · 오른쪽 센서).</summary>
    public IReadOnlyList<(double X, double Metres, int Left, int Right)> RangeGaps()
    {
        var points = _x.Where(p => _sensors.TryGetValue(p.Key, out var s) && RangeOf(s) > 0)
                       .Select(p => (Key: p.Key, X: p.Value, R: RangeOf(_sensors[p.Key])))
                       .OrderBy(p => p.X).ToList();
        var result = new List<(double, double, int, int)>();
        for (var i = 1; i < points.Count; i++)
        {
            var d = (points[i].X - points[i - 1].X) / Upm;
            var lack = d - points[i].R - points[i - 1].R;
            if (lack > 0.05) result.Add(((points[i].X + points[i - 1].X) / 2, lack, points[i - 1].Key, points[i].Key));
        }
        return result;
    }

    /// <summary>탐지 반경이 있는 센서가 있는가 — [탐지 범위] 단추를 켤 수 있는가.</summary>
    public bool HasRangeSensors => _sensors.Values.Any(s => Chain.Contains(s.Key) && RangeOf(s) > 0)
                                   || _sensors.Values.Any(s => RangeOf(s) > 0);
    #endregion

    #region - Units (FR-18) -
    /// <summary>
    /// 그릴 단위를 화면 순서로 — <paramref name="grouped"/> 이면 한 목록 안에서 이어진 펜스센서 2대 이상을 한 묶음으로.
    /// 가지 · 한 줄은 제어기 단위가 제자리에 들어간다.
    /// </summary>
    public IReadOnlyList<FenceUnit> Units(bool grouped)
    {
        var units = new List<FenceUnit>();
        var keys = Chain.Keys;
        var controllerAt = Shape switch { WiringShape.TwoBranch => Chain.ControllerGap, WiringShape.Line => 0, _ => -1 };

        var run = new List<int>();
        void Flush()
        {
            if (run.Count >= 2 && grouped) units.Add(new FenceUnit(run[0], run.ToList(), true, false));
            else foreach (var k in run) units.Add(new FenceUnit(k, new[] { k }, false, false));
            run.Clear();
        }

        for (var i = 0; i <= keys.Count; i++)
        {
            if (i == controllerAt)
            {
                Flush();
                units.Add(new FenceUnit(CONTROLLER_KEY, Array.Empty<int>(), false, true));
            }
            if (i == keys.Count) break;

            var key = keys[i];
            if (_sensors.TryGetValue(key, out var s) && s.Kind == FenceKind.Fence) run.Add(key);
            else { Flush(); units.Add(new FenceUnit(key, new[] { key }, false, false)); }
        }
        Flush();
        return units;
    }

    /// <summary>단위의 x — 묶음은 평균, 제어기는 <see cref="ControllerX"/>.</summary>
    public double UnitX(FenceUnit unit) => unit.IsController ? ControllerX : unit.Keys.Average(k => _x[k]);

    /// <summary>묶음을 쓸 것인가 — 줌이 문턱보다 작고 펜스센서가 있을 때.</summary>
    public bool ShouldGroup(double zoom) => zoom < GROUP_ZOOM && _sensors.Values.Any(s => s.Kind == FenceKind.Fence && Chain.Contains(s.Key));
    #endregion

    #region - Fit -
    /// <summary>
    /// 전체 보기 경계(투영 뒤 그림 좌표 · 목업 <c>fit()</c>). 줌은 호출부가 <c>GraphViewport.TryFit</c> 로 고른다.
    /// </summary>
    public Rect FitBounds(FenceProjector p)
    {
        var u = Upm;
        var pad = Shape == WiringShape.Ring ? 90 : 60;
        var xs = new List<double>
        {
            p.P(MinX - pad, 0, GroundDepth).X, p.P(MaxX + pad, FenceProjector.H + 40, -36 * p.K).X,
            p.P(MinX - 5 * u, 0, 0).X, p.P(MaxX + 5 * u, 0, 0).X,
            p.P(MaxX + 5 * u + FenceScene.SIDE_LABEL_LEAD, 0, FenceScene.OutsideLabelDepth(p)).X + FenceScene.SIDE_LABEL_WIDTH,   // 땅 표기 "펜스 외부"(FR-20)
        };
        if (Shape == WiringShape.Ring)
        {
            xs.Add(p.P(ControllerX - 110, 0, 130).X);
            xs.Add(p.P(ControllerX + 120, 0, 106).X);
            xs.Add(p.P(MinX - 5 * u - 16 - 80, 0, 20).X);      // 축 글자 "위치 · 약 6m"(기둥 범위 왼쪽 바깥 · 평면에서도 잘리지 않게)
        }

        var minY = Math.Min(Math.Min(p.P(0, LabelTop + 10, OverlayDepth).Y, p.P(0, FenceProjector.H + 12, 0).Y), p.P(0, TopHeight, 0).Y);
        var maxY = Shape == WiringShape.Line
            ? p.P(0, -FenceProjector.SEC, FenceProjector.UZ).Y + 6
            : Math.Max(p.P(0, 0, GroundDepth).Y, p.P(0, 0, Shape == WiringShape.Ring ? 130 : 85).Y + 22) + 4;
        return new Rect(new Point(xs.Min(), minY), new Point(xs.Max(), maxY));
    }
    #endregion
}
