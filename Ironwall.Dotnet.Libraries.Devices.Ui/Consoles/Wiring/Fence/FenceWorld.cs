using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
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
    WiringFacing Facing = WiringFacing.Front)
{
    public FenceKind Kind => FenceWorld.KindOf(Type);

    /// <summary>보는 쪽이 있는 센서인가(FR-20 — 기둥에 다는 스마트 복합 · 복합).</summary>
    public bool HasFacing => WiringTopology.SupportsFacing(Type);

    /// <summary>뒤(펜스 내부)를 보는 기둥 센서 — 기둥 반대쪽에 그리고 칩에 "뒤" 표지.</summary>
    public bool IsBackFacing => HasFacing && Facing == WiringFacing.Back;

    /// <summary>칩의 큰 글자 — 링 "3" · 가지 "L2"/"R1" · 한 줄 "4".</summary>
    public string Big(WiringShape shape) => shape == WiringShape.TwoBranch
        ? $"{(Line == WiringSpec.LINE_PRIMARY ? "L" : "R")}{Order}"
        : $"{Order}";

    /// <summary>링의 양 포트 번호 "A3 · B32". 링이 아니면 빈 글자.</summary>
    public string PortText => OppositeOrder is { } b ? $"A{Order} · B{b}" : string.Empty;
}

/// <summary>줌이 낮을 때 한 덩어리로 그리는 단위 — 센서 하나 · 펜스센서 묶음 · 제어기(가지 · 한 줄).</summary>
/// <param name="Key">센서 키 · 묶음은 첫 센서 키 · 제어기는 <see cref="FenceWorld.CONTROLLER_KEY"/>.</param>
public sealed record FenceUnit(int Key, IReadOnlyList<int> Keys, bool IsGroup, bool IsController)
{
    public int Count => Keys.Count;
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

    /// <summary>그림 맨 위 높이(세계 단위) — 가장 높은 망 · 기둥 · 코일 · 센서.</summary>
    public double TopHeight { get; private set; } = FenceProjector.H + 40;

    /// <summary>탐지 반경(m) — 복합 20 · 지진동 15(FR-19).</summary>
    public static double RangeOf(FenceKind kind) => kind switch { FenceKind.Multi => 20, FenceKind.Underground => 15, _ => 0 };

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
        var top = FenceProjector.H + 40;
        var twoLanes = layout.HasUpperSensors;
        foreach (var (key, mount) in seats)
        {
            var point = FenceLayoutMath.PointOf(mount, geometry);
            var heightM = FenceLayoutMath.LaneHeightM(mount, geometry, twoLanes);                 // 위 줄 = 꼭대기 위 · 아래 줄 = 망 위(FR-18)
            var stack = stacks[(mount.Lane, FenceLayoutMath.SeatOf(mount))];
            var dx = (stack.IndexOf(key) - (stack.Count - 1) / 2.0) * STACK_DX;
            world._x[key] = point.XM * upm + dx;
            var kind = sensors.TryGetValue(key, out var s) ? s.Kind : FenceKind.Smart;
            // 위 줄 칩은 꼭대기 위에 올라앉는다 — 몸 가운데를 목표 높이 + 반 칩(약 28)에 맞춘다
            var lift = mount.Lane == FenceLane.Upper ? LiftFor(kind, FenceMountSpot.PanelCenter, heightM * vpm + UPPER_CHIP_HALF) : LiftFor(kind, mount.Spot, heightM * vpm);
            world._lift[key] = lift;
            top = Math.Max(top, lift + (kind == FenceKind.Multi ? FenceProjector.H + 56 : 110));
        }
        // 모양이 바뀌는 곳(담 ↔ 철망 · 기둥 자리 ↔ 망 가운데)에서 이웃 칩이 거의 같은 x 에 서면 번호판이 겹친다(검토 V3 · 재검토) —
        // 줄마다(위 · 아래 줄은 높이가 달라 서로 겹치지 않는다) 가로 순서를 지킨 채 겹친 것만 칩 폭 + 틈 이상으로 벌린다(무리 가운데는 제자리 평균).
        var chainIndex = chain.Keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i);
        foreach (var lane in seats.GroupBy(t => t.Mount.Lane))
        {
            var ordered = lane.Select(t => t.Key).Where(world._x.ContainsKey).OrderBy(k => world._x[k]).ThenBy(k => chainIndex[k]).ToList();
            var spread = Separate(ordered.Select(k => world._x[k]).ToList(), MIN_CHIP_DX);
            for (var i = 0; i < ordered.Count; i++) world._x[ordered[i]] = spread[i];
        }
        foreach (var post in geometry.Posts) top = Math.Max(top, post.HeightM * vpm + (post.HasRazor ? 44 : 12));
        foreach (var panel in geometry.Panels) top = Math.Max(top, panel.Spec.HeightM * vpm + (panel.Spec.Style == EnumFenceStyle.ChainLinkRazor ? 44 : 14));
        world.TopHeight = top;

        world.ControllerX = world.GapMid(chain.ControllerGap);
        var xs = world._x.Values.Append(0).Append(geometry.LengthM * upm).Append(world.ControllerX).ToList();
        world.MinX = xs.Min();
        world.MaxX = xs.Max();
        return world;
    }

    /// <summary>위 줄 칩을 꼭대기 위로 올려 앉히는 반 칩 높이(세계 단위).</summary>
    public const double UPPER_CHIP_HALF = 28;

    /// <summary>
    /// 이웃 칩의 최소 간격(세계 단위) — 칩 폭(스마트 몸 32 · 번호판 20) + 틈 8. 30(같은 자리 벌림)으로는 담/철망 경계의 두 칩 번호판이 여전히 겹쳤다(재검토 렌더).
    /// </summary>
    public const double MIN_CHIP_DX = 40;

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
        var points = _x.Where(p => _sensors.TryGetValue(p.Key, out var s) && RangeOf(s.Kind) > 0)
                       .Select(p => (Key: p.Key, X: p.Value, R: RangeOf(_sensors[p.Key].Kind)))
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
    public bool HasRangeSensors => _sensors.Values.Any(s => Chain.Contains(s.Key) && RangeOf(s.Kind) > 0)
                                   || _sensors.Values.Any(s => RangeOf(s.Kind) > 0);
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
