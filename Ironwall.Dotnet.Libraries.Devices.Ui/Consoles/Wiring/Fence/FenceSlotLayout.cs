using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>펜스 뷰 투영 — 입체(35° 비스듬히) · 평면(옆에서 본 입면). 둘 다 <b>x 는 같다</b>(순서 · 삽입 판정이 투영에 기대지 않는다).</summary>
public enum FenceProjection
{
    /// <summary>기본 입체 — 지도 3D 몸체와 같은 35° 기울기(PRD FR-10).</summary>
    Tilt35 = 0,
    /// <summary>[평면 보기] · 원격 데스크톱(Tier 0) 자동 평면 — 깊이를 버린다.</summary>
    Flat = 1,
}

/// <summary>펜스 배치 입력 선택값.</summary>
public sealed record FenceLayoutOptions
{
    /// <summary>1m 가 몇 DIU 인가(줌 1 기준). 줌 · 팬은 뷰포트 변환이 맡는다.</summary>
    public double PixelsPerMetre { get; init; } = FenceSlotLayout.DEFAULT_PIXELS_PER_METRE;

    public FenceProjection Projection { get; init; } = FenceProjection.Tilt35;

    /// <summary>
    /// 실측 거리(m) — 센서 키 → <b>화면 왼쪽 이웃에서 이 센서까지</b>의 거리. 있으면 종류별 기본 간격 대신 쓴다.
    /// 0 이하 · NaN · 무한대는 무시한다. 체인 맨 왼쪽 센서의 값은 쓸 곳이 없어 무시된다.
    /// </summary>
    public IReadOnlyDictionary<int, double>? MeasuredGapMetres { get; init; }

    /// <summary>종류별 간격 표 — 펜스센서 현장 간격(2~4m)을 바꾼 표를 줄 수 있다(v0.4 §1-C). 없으면 기준 표.</summary>
    public WiringSpacingTable? Spacing { get; init; }
}

/// <summary>센서 한 대의 자리.</summary>
/// <param name="Key">센서 키.</param>
/// <param name="Index">체인 인덱스(0부터 · 화면 왼쪽→오른쪽).</param>
/// <param name="Type">센서 종류.</param>
/// <param name="Metres">첫 센서에서 잰 거리(m).</param>
/// <param name="X">화면 x(DIU) — 투영과 무관.</param>
/// <param name="Anchor">센서가 붙는 점(투영 뒤) — 체인 선이 여기를 지난다.</param>
/// <param name="Rect">센서 칩 사각형(투영 뒤) — 클릭 · 드래그 적중 영역.</param>
public sealed record FenceSensorSlot(int Key, int Index, EnumDeviceType Type, double Metres, double X, Point Anchor, Rect Rect);

/// <summary>VBUS 보상 유닛 표지(표시 전용 · FR-05).</summary>
public sealed record FenceVbusMarker(int Gap, Point Position);

/// <summary>줌에 따라 그릴 한 덩어리 — 센서 하나, 또는 접힌 펜스센서 묶음(FR-18).</summary>
public sealed record FenceLayoutItem(IReadOnlyList<FenceSensorSlot> Slots, Rect Rect)
{
    public bool IsGroup => Slots.Count > 1;
    public int FirstIndex => Slots[0].Index;
    public int LastIndex => Slots[^1].Index;
    public IEnumerable<int> Keys => Slots.Select(s => s.Key);

    /// <summary>"펜스센서 ×40" — 묶음 칩 글씨.</summary>
    public string Label => IsGroup ? $"펜스센서 ×{Slots.Count}" : string.Empty;
}

/// <summary>
/// 펜스 형상 뷰의 좌표 — <b>순수 함수</b>(PRD FR-04 · FR-05 · FR-09 · FR-17 · FR-18 · NFR-01). 3D 층과 2D 층이 같은 값을 쓴다.
/// </summary>
/// <remarks>
/// <para><b>간격(FR-17)</b> — 이웃한 두 센서 사이 = <b>두 종류 기본 간격 중 작은 값</b>(펜스센서가 복합센서 사이를 채운다:
/// 복합 20m 사이에 펜스 2.5m × 8). 실측 거리가 주어진 센서는 그 값을 쓴다.</para>
/// <para><b>투영</b> — 지도 3D 몸체(<c>GMaps.Ui/Symbols3D/HousingMath.Pitch</c> = 35°)와 같은 비스듬한 투영:
/// 화면 y = 지면 − (높이·cos35° + 깊이·sin35°)·배율, x 는 그대로. Devices.Ui 는 GMaps.Ui 를 참조하지 않아 상수를 옮겨 둔다.</para>
/// </remarks>
public sealed class FenceSlotLayout
{
    /// <summary>입체 기울기(°) — <c>Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D.HousingMath.Pitch</c> 와 같아야 한다(참조가 없어 옮겨 둠).</summary>
    public const double PITCH_DEGREES = 35;

    public const double DEFAULT_PIXELS_PER_METRE = 12;

    /// <summary>이 줌보다 작으면 펜스센서 연속 구간을 한 묶음으로 접는다(FR-18).</summary>
    public const double GROUP_ZOOM_THRESHOLD = 0.8;

    /// <summary>종류별 기준 간격(m) — 정본은 <see cref="WiringSpacingTable"/>(v0.4 §1-C: 펜스 3m · 현장 2~4m).</summary>
    public const double SPACING_FENCE_M = WiringSpacingTable.FENCE_DEFAULT_M;
    public const double SPACING_SMART_M = WiringSpacingTable.SMART_M;
    public const double SPACING_MULTI_M = WiringSpacingTable.MULTI_M;
    public const double SPACING_UNDERGROUND_M = WiringSpacingTable.UNDERGROUND_M;
    public const double SPACING_DEFAULT_M = WiringSpacingTable.OTHER_M;

    /// <summary>체인 양 끝에서 제어기 · 여백까지(m).</summary>
    public const double END_LEAD_M = 6;

    /// <summary>기둥 높이(m).</summary>
    public const double POST_HEIGHT_M = 3.0;

    /// <summary>리턴케이블이 지나는 땅속 깊이(높이 음수 · m).</summary>
    public const double CABLE_HEIGHT_M = -2.5;

    /// <summary>함체가 놓이는 깊이(펜스 안쪽 · 보는 쪽이 음수 · m).</summary>
    public const double ENCLOSURE_DEPTH_M = -1.0;

    public const double SIDE_MARGIN = 24;
    public const double TOP_PAD = 24;
    public const double ENCLOSURE_WIDTH = 44;
    public const double ENCLOSURE_HEIGHT = 26;

    private static readonly double SinPitch = Math.Sin(PITCH_DEGREES * Math.PI / 180);
    private static readonly double CosPitch = Math.Cos(PITCH_DEGREES * Math.PI / 180);

    private readonly double _ppm;
    private readonly List<double> _xs;

    private FenceSlotLayout(WiringChain chain, IReadOnlyList<FenceSensorSlot> slots, List<double> xs, double ppm, FenceProjection projection,
                            double groundY)
    {
        Chain = chain;
        Slots = slots;
        _xs = xs;
        _ppm = ppm;
        Projection = projection;
        GroundY = groundY;
    }

    #region - Result -
    public WiringChain Chain { get; }
    public FenceProjection Projection { get; }

    /// <summary>센서 자리 — 체인 순서.</summary>
    public IReadOnlyList<FenceSensorSlot> Slots { get; }

    /// <summary>센서 x(DIU) — 체인 순서. 삽입 판정(<see cref="FenceDropMath"/>)의 입력.</summary>
    public IReadOnlyList<double> SensorXs => _xs;

    /// <summary>지면 선 y(투영 뒤 · 높이 0 · 깊이 0).</summary>
    public double GroundY { get; }

    /// <summary>기둥 윗끝 y.</summary>
    public double PostTopY { get; private set; }

    /// <summary>기둥 x — 지중이 아닌 센서마다 하나(겹치면 한 번).</summary>
    public IReadOnlyList<double> PostXs { get; private set; } = Array.Empty<double>();

    /// <summary>체인 선(링 · 한 줄). 한 줄은 제어기 포트에서 시작한다. 양쪽 가지는 빈 목록 — <see cref="BranchPaths"/>.</summary>
    public IReadOnlyList<Point> ChainPolyline { get; private set; } = Array.Empty<Point>();

    /// <summary>리턴케이블 2가닥(링만) — [0] Sensor A → 체인 첫 센서 · [1] Sensor B → 체인 끝 센서. 땅속으로 지난다.</summary>
    public IReadOnlyList<IReadOnlyList<Point>> ReturnCables { get; private set; } = Array.Empty<IReadOnlyList<Point>>();

    /// <summary>가지 선 2가닥(양쪽 가지만) — [0] 왼쪽(제어기 → 바깥) · [1] 오른쪽.</summary>
    public IReadOnlyList<IReadOnlyList<Point>> BranchPaths { get; private set; } = Array.Empty<IReadOnlyList<Point>>();

    /// <summary>VBUS 보상 유닛 표지(링만).</summary>
    public IReadOnlyList<FenceVbusMarker> VbusMarkers { get; private set; } = Array.Empty<FenceVbusMarker>();

    /// <summary>함체(링) · 제어기(그 밖) 사각형 — 제어기 틈 아래.</summary>
    public Rect EnclosureRect { get; private set; }

    /// <summary>함체 Sensor A 포트(왼쪽 변 가운데) · Sensor B 포트(오른쪽 변 가운데).</summary>
    public Point PortA => new(EnclosureRect.Left, EnclosureRect.Top + EnclosureRect.Height / 2);
    public Point PortB => new(EnclosureRect.Right, EnclosureRect.Top + EnclosureRect.Height / 2);

    /// <summary>그린 것 전부를 담는 세계 경계(여백 포함) — [전체 보기]의 기준.</summary>
    public Rect Bounds { get; private set; }
    #endregion

    #region - Pure helpers -
    /// <summary>종류별 기준 간격(m · FR-17) — 기준 표(<see cref="WiringSpacingTable.Default"/>).</summary>
    public static double SpacingMetres(EnumDeviceType type) => WiringSpacingTable.Default.SpacingOf(type);

    /// <summary>이웃 두 센서 사이 기준 간격 = 두 종류 간격 중 작은 값.</summary>
    public static double GapMetres(EnumDeviceType left, EnumDeviceType right) => WiringSpacingTable.Default.GapBetween(left, right);

    /// <summary>
    /// 세계 점(x DIU · 높이 m · 깊이 m) → 화면 y. 입체: 지면 − (높이·cos35° + 깊이·sin35°)·배율 · 평면: 지면 − 높이·배율.
    /// </summary>
    public static double ProjectY(double groundY, double heightM, double depthM, double ppm, FenceProjection projection)
        => projection == FenceProjection.Flat
            ? groundY - heightM * ppm
            : groundY - (heightM * CosPitch + depthM * SinPitch) * ppm;

    /// <summary>
    /// 기둥 x(FR-20 설치 위치) — <b>기둥 센서</b>(스마트 · 복합 · 모름)는 자기 x 에 기둥 하나, <b>펜스센서</b>는 기둥 사이 철망 가운데라
    /// 자기 칸의 양쪽(x ± <paramref name="halfPanel"/>)에 기둥을 세운다(이어진 펜스센서는 기둥을 나눠 쓴다), 지진동은 기둥이 없다.
    /// 센서 x 는 건드리지 않는다(순서 · 간격 규칙 FR-17 그대로).
    /// </summary>
    /// <param name="fillSpacing">0 보다 크면 빈 구간을 이 간격 이하로 채운다(가지 · 한 줄의 배경 펜스). 펜스센서 칸 안에는 넣지 않는다.</param>
    /// <param name="from">채움의 왼쪽 끝(없으면 채우지 않음).</param>
    /// <param name="to">채움의 오른쪽 끝.</param>
    public static IReadOnlyList<double> MountPosts(IEnumerable<(double X, EnumDeviceType Type)> sensors, double halfPanel,
                                                   double fillSpacing = 0, double? from = null, double? to = null)
    {
        var list = (sensors ?? Enumerable.Empty<(double X, EnumDeviceType Type)>()).OrderBy(s => s.X).ToList();
        var eps = Math.Max(0.5, halfPanel * 0.05);
        var raw = new List<double>();
        foreach (var (x, type) in list)
        {
            if (type == EnumDeviceType.Fence) { raw.Add(x - halfPanel); raw.Add(x + halfPanel); }
            else if (WiringTopology.IsPostMounted(type)) raw.Add(x);
        }
        raw.Sort();
        var posts = new List<double>();
        foreach (var x in raw)
            if (posts.Count == 0 || x - posts[^1] > eps) posts.Add(x);

        if (!(fillSpacing > 0) || from is not { } lo || to is not { } hi || hi < lo) return posts;

        // 센서 기둥이 없으면(지진동만 · 빈 체인) 옛 격자 그대로 — 간격의 배수에서 시작한다.
        if (posts.Count == 0)
        {
            for (var x = Math.Floor(lo / fillSpacing) * fillSpacing; x <= hi + 0.1; x += fillSpacing) posts.Add(x);
            return posts;
        }

        var fences = list.Where(s => s.Type == EnumDeviceType.Fence).Select(s => s.X).ToList();
        var filled = new List<double>();
        for (var x = posts[0] - fillSpacing; x >= lo - 0.1; x -= fillSpacing) filled.Add(x);
        for (var i = 0; i < posts.Count; i++)
        {
            filled.Add(posts[i]);
            if (i == posts.Count - 1) break;
            var a = posts[i];
            var b = posts[i + 1];
            if (b - a <= fillSpacing * 1.2 || fences.Any(f => f > a + eps && f < b - eps)) continue;     // 펜스센서 칸은 나누지 않는다
            var n = (int)Math.Ceiling((b - a) / fillSpacing);
            for (var j = 1; j < n; j++) filled.Add(a + (b - a) * j / n);
        }
        for (var x = posts[^1] + fillSpacing; x <= hi + 0.1; x += fillSpacing) filled.Add(x);
        filled.Sort();
        return filled;
    }

    /// <summary>펜스센서 한 칸의 절반(m, 기준 표) — 이웃 펜스센서 간격의 절반이라 이어진 칸이 기둥을 나눠 쓴다. 현장 간격은 표의 <c>FenceMetres / 2</c>.</summary>
    public const double FENCE_HALF_PANEL_M = SPACING_FENCE_M / 2;

    /// <summary>이 배치에 쓴 간격 표(펜스센서 현장 간격 포함).</summary>
    public WiringSpacingTable Spacing { get; private init; } = WiringSpacingTable.Default;

    /// <summary>종류별 모양 — (붙는 높이 m, 깊이 m, 칩 폭, 칩 높이, 아래로 매달리는가).</summary>
    private static (double Height, double Depth, double Width, double ChipHeight, bool HangsDown) ShapeOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.Fence => (POST_HEIGHT_M / 2, 0, 14, 10, false), // 기둥 사이 철망 가운데(높이 중간 · FR-20)
        EnumDeviceType.Multi => (3.0, 0, 22, 22, false),                // 기둥 위 볼 마운트
        EnumDeviceType.Underground => (-0.8, -2.0, 10, 26, true),       // 땅속 막대(펜스 안쪽)
        _ => (2.6, 0, 18, 24, false),                                   // 스마트 후드형 · 모름
    };
    #endregion

    #region - Build -
    /// <summary>체인과 센서 종류로 좌표를 세운다. 모르는 키의 종류는 <see cref="EnumDeviceType.NONE"/>(기본 간격 · 스마트 모양).</summary>
    public static FenceSlotLayout Build(WiringChain chain, Func<int, EnumDeviceType>? typeOf, FenceLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chain);
        options ??= new FenceLayoutOptions();
        typeOf ??= _ => EnumDeviceType.NONE;
        var ppm = options.PixelsPerMetre > 0 && double.IsFinite(options.PixelsPerMetre) ? options.PixelsPerMetre : DEFAULT_PIXELS_PER_METRE;
        var projection = options.Projection;

        // 지면 y — 가장 높은 것(기둥 · 볼 마운트 칩)이 위 여백 안에 들어오게.
        var groundY = TOP_PAD + 26 + POST_HEIGHT_M * ppm;

        var spacing = options.Spacing ?? WiringSpacingTable.Default;
        var types = chain.Keys.Select(k => typeOf(k)).ToList();
        var metres = new List<double>(types.Count);
        for (var i = 0; i < types.Count; i++)
        {
            if (i == 0) { metres.Add(0); continue; }
            var gap = options.MeasuredGapMetres is { } measured && measured.TryGetValue(chain.Keys[i], out var m) && m > 0 && double.IsFinite(m)
                ? m
                : spacing.GapBetween(types[i - 1], types[i]);
            metres.Add(metres[i - 1] + gap);
        }

        var x0 = SIDE_MARGIN + END_LEAD_M * ppm;
        var xs = metres.Select(m => x0 + m * ppm).ToList();

        var slots = new List<FenceSensorSlot>(types.Count);
        for (var i = 0; i < types.Count; i++)
        {
            var shape = ShapeOf(types[i]);
            var anchor = new Point(xs[i], ProjectY(groundY, shape.Height, shape.Depth, ppm, projection));
            var rect = shape.HangsDown
                ? new Rect(anchor.X - shape.Width / 2, anchor.Y, shape.Width, shape.ChipHeight)
                : new Rect(anchor.X - shape.Width / 2, anchor.Y - shape.ChipHeight, shape.Width, shape.ChipHeight);
            slots.Add(new FenceSensorSlot(chain.Keys[i], i, types[i], metres[i], xs[i], anchor, rect));
        }

        var layout = new FenceSlotLayout(chain, slots, xs, ppm, projection, groundY) { Spacing = spacing };
        layout.Complete();
        return layout;
    }

    /// <summary>
    /// 틈 <paramref name="gap"/>(0…N)의 x — 가운데 틈은 두 센서의 가운데, 양 끝은 끝 센서에서 <see cref="END_LEAD_M"/> 의 절반 바깥.
    /// 센서가 없으면 첫 센서가 올 자리.
    /// </summary>
    public double GapX(int gap) => GapX(_xs, gap, END_LEAD_M * _ppm / 2, SIDE_MARGIN + END_LEAD_M * _ppm);

    internal static double GapX(IReadOnlyList<double> xs, int gap, double halfLead, double emptyX)
    {
        if (xs.Count == 0) return emptyX;
        var g = Math.Clamp(gap, 0, xs.Count);
        if (g == 0) return xs[0] - halfLead;
        if (g == xs.Count) return xs[^1] + halfLead;
        return (xs[g - 1] + xs[g]) / 2;
    }

    private void Complete()
    {
        var shape = Chain.Shape;
        var anchors = Slots.Select(s => s.Anchor).ToList();

        PostTopY = ProjectY(GroundY, POST_HEIGHT_M, 0, _ppm, Projection);
        PostXs = MountPosts(Slots.Select(s => (s.X, s.Type)), Spacing.FenceMetres / 2 * _ppm);

        // 함체(링) · 제어기 — 제어기 틈 아래, 펜스 안쪽 지면에.
        var gx = GapX(Chain.ControllerGap);
        var ey = ProjectY(GroundY, 0, ENCLOSURE_DEPTH_M, _ppm, Projection);
        EnclosureRect = new Rect(gx - ENCLOSURE_WIDTH / 2, ey - ENCLOSURE_HEIGHT / 2, ENCLOSURE_WIDTH, ENCLOSURE_HEIGHT);

        var cableY = ProjectY(GroundY, CABLE_HEIGHT_M, 0, _ppm, Projection);
        switch (shape)
        {
            case WiringShape.Ring:
                ChainPolyline = anchors;
                if (anchors.Count > 0)
                {
                    ReturnCables = new IReadOnlyList<Point>[]
                    {
                        new[] { PortA, new Point(PortA.X, cableY), new Point(anchors[0].X, cableY), anchors[0] },
                        new[] { PortB, new Point(PortB.X, cableY), new Point(anchors[^1].X, cableY), anchors[^1] },
                    };
                    VbusMarkers = WiringTopology.VbusGaps(Chain.ControllerGap, Chain.Count)
                        .Select(g => new FenceVbusMarker(g, new Point(GapX(g), ChainYAtGap(anchors, g)))).ToList();
                }
                break;

            case WiringShape.TwoBranch:
                var left = new List<Point> { PortA };
                for (var i = Chain.ControllerGap - 1; i >= 0; i--) left.Add(anchors[i]);     // 제어기 → 바깥
                var right = new List<Point> { PortB };
                for (var i = Chain.ControllerGap; i < anchors.Count; i++) right.Add(anchors[i]);
                BranchPaths = new IReadOnlyList<Point>[] { left, right };
                break;

            default:
                ChainPolyline = new[] { PortB }.Concat(anchors).ToList();
                break;
        }

        var bounds = EnclosureRect;
        foreach (var s in Slots) bounds.Union(s.Rect);
        foreach (var x in PostXs) { bounds.Union(new Point(x, PostTopY)); bounds.Union(new Point(x, GroundY)); }
        foreach (var path in ReturnCables.Concat(BranchPaths).Append(ChainPolyline))
            foreach (var p in path) bounds.Union(p);
        bounds.Union(new Point(GapX(0), GroundY));
        bounds.Union(new Point(GapX(Chain.Count), GroundY));
        bounds.Inflate(SIDE_MARGIN, SIDE_MARGIN);
        Bounds = bounds;
    }

    /// <summary>틈 위의 체인 선 높이 — 두 이웃 고정점 y 의 가운데(끝이면 끝 센서 y).</summary>
    private static double ChainYAtGap(IReadOnlyList<Point> anchors, int gap)
    {
        if (gap <= 0) return anchors[0].Y;
        if (gap >= anchors.Count) return anchors[^1].Y;
        return (anchors[gap - 1].Y + anchors[gap].Y) / 2;
    }
    #endregion

    #region - Grouping (FR-18) -
    /// <summary>
    /// 줌 <paramref name="zoom"/> 에서 그릴 덩어리. <see cref="GROUP_ZOOM_THRESHOLD"/> 미만이면 <b>펜스센서가 2대 이상 이어진 구간</b>을
    /// 한 묶음으로 접는다 — 다른 종류 센서와 제어기 틈에서 끊는다(가지 · 함체 경계를 넘는 묶음은 드롭 자리를 흐린다).
    /// </summary>
    public IReadOnlyList<FenceLayoutItem> Group(double zoom)
    {
        var items = new List<FenceLayoutItem>(Slots.Count);
        if (!(zoom < GROUP_ZOOM_THRESHOLD))
        {
            foreach (var s in Slots) items.Add(new FenceLayoutItem(new[] { s }, s.Rect));
            return items;
        }

        var run = new List<FenceSensorSlot>();
        void Flush()
        {
            if (run.Count == 0) return;
            if (run.Count == 1) items.Add(new FenceLayoutItem(new[] { run[0] }, run[0].Rect));
            else
            {
                var rect = run[0].Rect;
                foreach (var s in run) rect.Union(s.Rect);
                items.Add(new FenceLayoutItem(run.ToList(), rect));
            }
            run.Clear();
        }

        foreach (var s in Slots)
        {
            if (s.Index == Chain.ControllerGap && Chain.Shape != WiringShape.Line) Flush();
            if (s.Type == EnumDeviceType.Fence) { run.Add(s); continue; }
            Flush();
            items.Add(new FenceLayoutItem(new[] { s }, s.Rect));
        }
        Flush();
        return items;
    }
    #endregion
}
