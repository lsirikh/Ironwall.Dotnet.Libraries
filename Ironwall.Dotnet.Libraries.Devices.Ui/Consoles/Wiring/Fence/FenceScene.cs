using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 그리기 역할 — 목업 CSS 클래스 하나에 하나. 색은 여기 없다: 렌더러가 그릴 때마다 테마 토큰으로 다시 풀어 칠한다(Frozen · 정적 브러시 금지).
/// </summary>
public enum FenceInk
{
    Ground, Section, Strata, Grid, Base, PostNumber, Axis, Caption,
    Mesh, Rail, PostFront, PostSide, PostTop, CapFront, CapSide, CapTop,
    Chain, ReturnOuter, ReturnInner, EndCap, Chevron, LabelChain, LabelReturn,
    OliveFront, OliveSide, OliveTop, GlandFront, GlandSide, GlandTop,
    Pir, Plate, Number, NumberSmall, FenceLabel, Ball, Rod, RodRib, UgCap,
    VbusFront, VbusSide, VbusTop, VbusRib, VbusEar, VbusText, VbusLabel,
    EnclosureFront, EnclosureSide, EnclosureTop, Dock, CardSmart, CardVbus, CardSmartText, CardVbusText, Port, PortText,
    ControllerFront, ControllerSide, ControllerTop, ControllerText, ControllerSub,
    Range, GapMark, GapText,
    Hit, Select, Draft, Proposal,
    GroupBack, GroupBody, GroupText, GroupSub,
    Pill, PillDuplicate, PillInsert, PillPort, Insert,
    FacingTag, FacingTagText, SideLabel,
    // 센서 방향(2026-10-01) — 렌즈 쪽 모서리(주 색 굵은 선 · 화살 아님) · 뒷판 이음매 · 철망 뒤(내부) 센서 위에 겹치는 철망 선
    LensEdge, BackSeam, MeshOver,
    // 9점 격자 끌기 안내(2026-10-01) — 빨강(위급 토큰 · 잠깐 뜨는 끌기 안내) 채운 원 · 흐린 원 · 고리 · 빈 마름모
    SnapDot, SnapDotDim, SnapRing, SnapBlocked,
    // 펜스 편집기(fence-wiring-editor) — 망 선택 · 러버밴드 · 모양 5종
    PanelSelectFill, PanelSelectEdge, RubberBand,
    Razor, RazorArm, BrickFront, WallSide, WallTopFace, WallCap, ConcreteFront, ConcreteSeam, DesignFace, DesignRail, DesignPost,
    // 개념도(FR-12)
    ConceptWire, ConceptReturn, ConceptArrow, ConceptNode, ConceptNodeIp, ConceptNodeText, ConceptNodeSub, ConceptController, ConceptControllerText,
    ConceptPort, ConceptPortText, ConceptTitle, ConceptInfo, ConceptInsert, ConceptInternalNet,
    BrickSide, BrickTop,
    // 작은 배율의 윤형 철조망 — 코일 대신 톱니 띠 하나(fence-wiring-editor 검토 V2)
    RazorBand,
    // 펜스 모양 5종(fence-style-art) — 윤형 코일 · 가시 · 철선, 디자인펜스 철선 · V 접힘 · 클램프, 벽돌 줄눈 · 벽돌 색 셋, 미장 반점
    RazorCoil, RazorBarb, RazorStrand, DesignWire, DesignFold, DesignClamp,
    BrickMortarFace, BrickTone0, BrickTone1, BrickTone2, ConcreteSpeckleDark, ConcreteSpeckleLight,
    // 두 줄 개념도(fence-wiring-editor v0.3 FR-20) — Ch1 실선 · Ch2 점선 · 펜스 격자 · 줄 칩 · 눈금 · VBus
    ConceptCh1, ConceptCh2, ConceptCh2Dash, ConceptFence, ConceptMesh, ConceptPost, ConceptGround, ConceptTick, ConceptTickText,
    ConceptLabelLower, ConceptLabelUpper, ConceptChipLower, ConceptChipUpper, ConceptChipTextLower, ConceptChipTextUpper,
    ConceptVbus, ConceptVbusText, ConceptTarget,
}

/// <summary>
/// 그림 갈래. <see cref="Strokes"/> · <see cref="Patches"/> 는 도형 여럿을 그림 하나에 묶는다(<see cref="FenceShape.Figures"/> = 도형마다 점 수) —
/// 철망 · 코일 · 벽돌처럼 조각이 많은 무늬를 한 기하로 그려 망 200칸에서도 가볍게(fence-style-art).
/// </summary>
public enum FenceShapeKind { Polygon, Polyline, Line, Ellipse, Rect, Text, Pill, Strokes, Patches }

public enum FenceTextAnchor { Start, Middle, End }

/// <summary>
/// 그림 하나 — 순수 값. <see cref="FenceShapeKind.Rect"/> 은 [왼쪽 위, 오른쪽 아래] · <see cref="FenceShapeKind.Ellipse"/> 는 [가운데] + 반지름 ·
/// <see cref="FenceShapeKind.Text"/> 는 [기준선 점] · <see cref="FenceShapeKind.Pill"/> 은 [가운데](폭은 렌더러가 글자로 잰다).
/// </summary>
public sealed record FenceShape(
    FenceShapeKind Kind,
    FenceInk Ink,
    Point[] Points,
    double RadiusX = 0,
    double RadiusY = 0,
    string? Text = null,
    double FontSize = 0,
    FenceTextAnchor Anchor = FenceTextAnchor.Middle,
    double Opacity = 1,
    string? Color = null,
    int[]? Figures = null,
    bool Closed = false);

/// <summary>칩 하나의 그림 — 모양 · 적중 사각형(칩 좌표).</summary>
public sealed record FenceChipPicture(IReadOnlyList<FenceShape> Shapes, Rect Hit);

/// <summary>격자 점 안내의 모습 — 빈 점 · 센서가 있는 점(흐림) · 포인터 아래(크게 + 고리) · 함께 끈 센서가 갈 점(고리) · 갈 수 없음(빈 마름모).</summary>
public enum FenceSnapState { Free, Occupied, Hot, Member, Blocked }

/// <summary>끄는 동안 그리는 격자 점 하나(세계 좌표 · 투영 뒤).</summary>
public sealed record FenceSnapDot(Point At, FenceSnapState State);

/// <summary>
/// 펜스 뷰의 장면 — 목업(<c>wiring-fence-view-mockup.html</c> · <c>draw()</c> · <c>sensorShape()</c> · <c>drawCtrl()</c>)을 옮긴 <b>순수</b> 그림 목록.
/// 좌표는 투영 뒤 그림 좌표. 칩(센서 · 묶음 · 제어기)은 <b>자기 x 를 원점</b>으로 그린다 — 투영이 선형이라 <c>P(x+dx, y, z) = P(dx, y, z) + (x, 0)</c>.
/// </summary>
public static partial class FenceScene
{
    private const double H = FenceProjector.H;
    private const double PW = FenceProjector.PW;
    private const double UZ = FenceProjector.UZ;
    private const double SEC = FenceProjector.SEC;

    #region - Static layer -
    /// <summary>
    /// 정적 층 — 땅 · 단면 · 격자 · 축 · 탐지 범위 · 망 · 기둥 · 선(체인 · 리턴케이블 · 가지) · VBUS 표지 · 빈틈 표지.
    /// </summary>
    /// <param name="enclosureX">함체 x — 끄는 중이면 끄는 자리(리턴케이블 · VBUS 가 따라간다).</param>
    /// <param name="enclosureGap">함체 틈 — VBUS 표지를 고른다.</param>
    public static IReadOnlyList<FenceShape> Static(FenceWorld world, FenceProjector p, bool showRange, double enclosureX, int enclosureGap, double zoom = 1)
    {
        var o = new List<FenceShape>(512);
        var u = world.Upm;
        var shape = world.Shape;
        var xs = world.X.Values.Append(enclosureX).ToList();
        var x0 = xs.Min() - 5 * u;
        var x1 = xs.Max() + 5 * u;
        var gz0 = -36 * p.K;
        var gz1 = world.GroundDepth;

        var ground = new[] { p.P(x0 - 40, 0, gz0), p.P(x1 + 40, 0, gz0), p.P(x1 + 40, 0, gz1), p.P(x0 - 40, 0, gz1) };
        o.Add(Poly(FenceInk.Ground, ground));

        if (shape == WiringShape.Line)
        {
            o.Add(Poly(FenceInk.Section, p.P(x0 - 40, 0, gz1), p.P(x1 + 40, 0, gz1), p.P(x1 + 40, -SEC, gz1), p.P(x0 - 40, -SEC, gz1)));
            foreach (var y in new[] { -20.0, -42.0 }) o.Add(Seg(FenceInk.Strata, p.P(x0 - 40, y, gz1), p.P(x1 + 40, y, gz1)));
            var lp = p.P(x0 - 30, -SEC + 10, gz1);
            o.Add(Text(FenceInk.Axis, lp, "지면 단면 · 펜스 안쪽", 10, FenceTextAnchor.Start));
        }

        // 기둥 LOD — 화면 간격이 좁아 빗살이 되면 기둥 센서의 기둥 · 양 끝 · 간격을 지키는 기둥만(철망은 이어진다). 확대하면 다 돌아온다.
        var posts = ThinPosts(Posts(world, x0, x1), PostSensorXs(world), zoom);
        if (p.K > 0.02)
            foreach (var x in posts)
                o.Add(Seg(FenceInk.Grid, p.P(x, 0, 8), p.P(x, 0, Math.Min(gz1, 120)), 0.5 * p.K));
        o.Add(Seg(FenceInk.Base, p.P(x0 - 40, 0, 0), p.P(x1 + 40, 0, 0)));

        // 땅 표기(FR-20 · 카탈로그 설치 사례) — 펜스 너머 = 외부, 보는 쪽 = 내부. 센서가 앞(외부) · 뒤(내부) 어느 쪽을 보는지 읽는 기준.
        var outside = p.P(x1 + SIDE_LABEL_LEAD, 0, OutsideLabelDepth(p));
        var inside = p.P(x1 + SIDE_LABEL_LEAD, 0, InsideLabelDepth(shape));
        o.Add(Text(FenceInk.SideLabel, new Point(outside.X, outside.Y - 2), "펜스 외부", 11, FenceTextAnchor.Start));
        o.Add(Text(FenceInk.SideLabel, new Point(inside.X, inside.Y + 4), "펜스 내부", 11, FenceTextAnchor.Start));

        // 축 — 링 = 체인 위치 · 그 밖 = 제어기에서 거리
        if (shape == WiringShape.Ring)
        {
            // 위치 번호는 센서마다(기둥마다가 아니다 — 펜스센서는 기둥 사이에 달린다 · FR-20).
            for (var i = 0; i < world.Seq.Count; i++)
            {
                var q = p.P(world.X[world.Seq[i]], 0, 20);
                o.Add(Text(FenceInk.PostNumber, new Point(q.X, q.Y + 4), $"{i + 1}", 10.5));
            }
            var a = p.P(x0 - 16, 0, 20);
            o.Add(Text(FenceInk.Axis, new Point(a.X, a.Y + 4), "위치 · 약 6m", 10, FenceTextAnchor.End));
        }
        else
        {
            var step = shape == WiringShape.Line ? 25 : 10;
            var reach = Math.Max(-x0, x1);
            for (var m = step; m * u <= reach; m += step)
                foreach (var sign in shape == WiringShape.Line ? new[] { 1 } : new[] { -1, 1 })
                {
                    var x = sign * m * u;
                    if (x < x0 || x > x1) continue;
                    var q = p.P(x, 0, 20);
                    o.Add(Text(FenceInk.PostNumber, new Point(q.X, q.Y + 4), $"{m}m", 10.5));
                }
        }

        // 탐지 범위(FR-19) — 땅 위에만(렌더러가 땅 다각형으로 자른다)
        if (showRange)
            foreach (var (key, x) in world.X)
            {
                var r = FenceWorld.RangeOf(world.Sensors[key].Kind);
                if (r <= 0) continue;
                var c = p.P(x, 0, world.Sensors[key].Kind == FenceKind.Underground ? UZ : 0);
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Range, new[] { c }, r * u, Math.Max(6, r * u * p.Cz * 0.8)));
            }

        // 펜스(지중은 배경으로 옅게)
        var fenceStart = o.Count;
        for (var i = 0; i < posts.Count - 1; i++)
        {
            var a = posts[i] + PW / 2;
            var b = posts[i + 1] - PW / 2;
            if (b > a) o.Add(Poly(FenceInk.Mesh, p.P(a, 8, 0), p.P(b, 8, 0), p.P(b, H - 8, 0), p.P(a, H - 8, 0)));
        }
        if (posts.Count > 1)
        {
            var pa = posts[0];
            var pb = posts[^1];
            o.Add(Seg(FenceInk.Rail, p.P(pa, H - 8, 0), p.P(pb, H - 8, 0)));
            o.Add(Seg(FenceInk.Rail, p.P(pa, 10, 0), p.P(pb, 10, 0)));
            // 가운데 레일은 두지 않는다 — 펜스센서는 레일이 아니라 철망 가운데에 달린다(FR-20).
        }
        foreach (var x in posts)
        {
            Box(o, p, x, 0, H, -p.De / 2, p.De / 2, PW, FenceInk.PostFront, FenceInk.PostSide, FenceInk.PostTop);
            Box(o, p, x, H, H + 5, -p.De / 2 - 1.5 * p.K, p.De / 2 + 1.5 * p.K, PW + 4, FenceInk.CapFront, FenceInk.CapSide, FenceInk.CapTop);
        }
        if (shape == WiringShape.Line)
            for (var i = fenceStart; i < o.Count; i++) o[i] = o[i] with { Opacity = 0.5 };

        // 보는 쪽(FR-20)은 칩의 "뒤" 표지 · 속성 칸 · 저장값으로 말한다 — 땅의 작은 부채꼴 · 화살은 없앴다(사용자 요청 · 화면만 어지럽혔다).

        switch (shape)
        {
            case WiringShape.Ring: RingCables(o, world, p, enclosureX, enclosureGap); break;
            case WiringShape.TwoBranch: BranchCables(o, world, p); break;
            default: LineCables(o, world, p); break;
        }

        if (showRange)
            foreach (var gap in world.RangeGaps())
            {
                var c = p.P(gap.X, 0, shape == WiringShape.Line ? UZ : 30);
                o.Add(Poly(FenceInk.GapMark, new Point(c.X, c.Y - 7), new Point(c.X + 6, c.Y), new Point(c.X, c.Y + 7), new Point(c.X - 6, c.Y)));
                o.Add(Text(FenceInk.GapText, new Point(c.X, c.Y - 12), $"빈틈 {gap.Metres:0}m", 10));
            }

        return Legible(o, zoom);
    }

    /// <summary>땅 다각형(탐지 범위를 자를 테두리).</summary>
    public static Point[] GroundPolygon(FenceWorld world, FenceProjector p, double enclosureX)
    {
        var u = world.Upm;
        var xs = world.X.Values.Append(enclosureX).ToList();
        var x0 = xs.Min() - 5 * u;
        var x1 = xs.Max() + 5 * u;
        return new[] { p.P(x0 - 40, 0, -36 * p.K), p.P(x1 + 40, 0, -36 * p.K), p.P(x1 + 40, 0, world.GroundDepth), p.P(x0 - 40, 0, world.GroundDepth) };
    }

    /// <summary>
    /// 기둥 x(FR-20 설치 위치 · <see cref="FenceSlotLayout.MountPosts"/>) — 기둥 센서(스마트 · 복합)는 자기 x, 펜스센서는 자기 칸 양쪽(철망 가운데에 오게).
    /// 링은 그것뿐, 가지 · 한 줄은 빈 구간을 <see cref="FenceWorld.PostM"/> 간격으로 채운다(센서가 없으면 옛 격자).
    /// </summary>
    public static IReadOnlyList<double> Posts(FenceWorld world, double x0, double x1)
    {
        var u = world.Upm;
        var sensors = world.Seq.Select(k => (world.X[k], world.Sensors.TryGetValue(k, out var s) ? s.Type : Ironwall.Dotnet.Libraries.Enums.EnumDeviceType.NONE));
        var half = world.Spacing.FenceMetres / 2 * u;
        return world.Shape == WiringShape.Ring
            ? FenceSlotLayout.MountPosts(sensors, half)
            : FenceSlotLayout.MountPosts(sensors, half, world.PostM * u, x0 + 2 * u, x1 - 2 * u);
    }

    /// <summary>
    /// 기둥이 화면에서 이보다 촘촘하면 솎는다(px) — 2.5m 펜스센서 칸이 맞춤 배율에서 빗살이 되던 문제.
    /// 14px 로는 64% 맞춤(칸 18px)이 그대로 빗살이었다(재촬영 실측) — 28px 이면 64% 에서 한 칸 걸러, 100% 에서는 전부 보인다.
    /// </summary>
    public const double MIN_POST_SPACING_PX = 28;

    /// <summary>기둥에 다는 센서(스마트 · 복합 · 모름)의 x — 솎아도 남길 기둥.</summary>
    public static IEnumerable<double> PostSensorXs(FenceWorld world)
        => world.Seq.Where(k => world.Sensors.TryGetValue(k, out var s) ? WiringTopology.IsPostMounted(s.Type) : true).Select(k => world.X[k]);

    /// <summary>
    /// 기둥 LOD(순수) — 이웃 기둥의 화면 간격(세계 간격 × <paramref name="zoom"/>)이 모두 <paramref name="minPx"/> 이상이면 그대로.
    /// 아니면 <b>기둥 센서의 기둥</b>(<paramref name="keep"/>) · 양 끝은 늘 남기고, 나머지는 왼쪽부터 앞 기둥과 다음 필수 기둥 둘 다에서
    /// <paramref name="minPx"/> 이상 떨어진 것만 남긴다(몇 개 걸러 하나).
    /// </summary>
    public static IReadOnlyList<double> ThinPosts(IReadOnlyList<double> posts, IEnumerable<double> keep, double zoom, double minPx = MIN_POST_SPACING_PX)
    {
        var sorted = (posts ?? Array.Empty<double>()).OrderBy(x => x).ToList();
        if (sorted.Count < 3) return sorted;
        var min = minPx / SafeZoom(zoom);
        var dense = false;
        for (var i = 1; i < sorted.Count && !dense; i++) dense = sorted[i] - sorted[i - 1] < min - 1e-9;
        if (!dense) return sorted;

        var keepXs = (keep ?? Enumerable.Empty<double>()).ToList();
        bool Must(double x) => keepXs.Any(k => Math.Abs(k - x) < 0.5);
        var must = sorted.Select((x, i) => i == 0 || i == sorted.Count - 1 || Must(x)).ToList();

        var result = new List<double>();
        for (var i = 0; i < sorted.Count; i++)
        {
            var x = sorted[i];
            if (must[i]) { result.Add(x); continue; }
            if (result.Count > 0 && x - result[^1] < min) continue;
            var next = sorted.Skip(i + 1).Where((_, j) => must[i + 1 + j]).DefaultIfEmpty(double.PositiveInfinity).First();
            if (next - x < min) continue;
            result.Add(x);
        }
        return result;
    }

    /// <summary>땅 표기 "펜스 외부 · 펜스 내부"(FR-20)를 오른쪽 끝에서 얼마나 바깥에 두나(세계 단위).</summary>
    public const double SIDE_LABEL_LEAD = 10;

    /// <summary>땅 표기 글자가 오른쪽으로 차지하는 폭(세계 단위 · [전체 보기]가 담는다) — 작은 배율에서 글자를 키워도 잘리지 않게 넉넉히.</summary>
    public const double SIDE_LABEL_WIDTH = 100;

    /// <summary>"펜스 외부" 표기의 깊이 — 펜스 너머(보는 쪽 반대). 평면에서도 지면선 바로 위.</summary>
    public static double OutsideLabelDepth(FenceProjector p) => -26 * p.K - 6;

    /// <summary>"펜스 내부" 표기의 깊이 — 보는 쪽(함체 · 지중 선이 있는 쪽).</summary>
    public static double InsideLabelDepth(WiringShape shape) => shape == WiringShape.Line ? 40 : 52;

    /// <summary>
    /// 뒤를 보는 기둥 센서(FR-20)를 기둥 반대쪽으로 미는 깊이 — 입체는 기둥 두께 + 몸체 깊이만큼 너머로(오른쪽 위),
    /// 평면에서도 보이게 고정값을 더한다(평면은 깊이가 위로만 밀린다).
    /// </summary>
    public static double BackFacingOffset(FenceProjector p) => -(p.De + 20 * p.K + 14);

    private static void RingCables(List<FenceShape> o, FenceWorld world, FenceProjector p, double xe, int enclosureGap)
    {
        var keys = world.Seq;
        if (keys.Count == 0) return;
        const double CY = 41, RZ = 118, EW = 150;
        var cz = p.De / 2 + 6 * p.K;
        var xa = keys.Min(k => world.X[k]);
        var xb = keys.Max(k => world.X[k]);

        var left = new[] { p.P(xe - EW / 2 + 4, 12, RZ), p.P(xe - EW / 2 - 12, 2, RZ), p.P(xa - 22, 2, RZ), p.P(xa - 22, 2, cz), p.P(xa - 22, CY, cz), p.P(xa - 9, CY, cz) };
        var right = new[] { p.P(xe + EW / 2 - 4, 12, RZ), p.P(xe + EW / 2 + 12, 2, RZ), p.P(xb + 22, 2, RZ), p.P(xb + 22, 2, cz), p.P(xb + 22, CY, cz), p.P(xb + 9, CY, cz) };
        foreach (var path in new[] { left, right })
        {
            o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ReturnOuter, path));
            o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ReturnInner, path));
        }
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(xa - 9, CY, cz), p.P(xb + 9, CY, cz) }));
        foreach (var e in new[] { left[5], right[5] }) o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.EndCap, new[] { e }, 3.6, 3.6));

        var la = p.P(xa - 22, 0, RZ + 16);
        var lb = p.P(xb + 22, 0, RZ + 16);
        o.Add(Text(FenceInk.LabelReturn, new Point(la.X, la.Y + 4), $"◀ 리턴케이블 · {WiringValidation.PORT_1} → #1", 11, FenceTextAnchor.Start));
        o.Add(Text(FenceInk.LabelReturn, new Point(lb.X, lb.Y + 4), $"#{keys.Count} ← {WiringValidation.PORT_2} · 리턴케이블 ▶", 11, FenceTextAnchor.End));

        // A/B 번호 알약(체인 선 높이)
        foreach (var key in keys)
        {
            if (!world.Sensors.TryGetValue(key, out var s) || s.PortText.Length == 0) continue;
            o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.PillPort, new[] { p.P(world.X[key], 17, cz) }, Text: s.PortText, FontSize: 10));
        }

        // VBUS 보상 표지 — 표시 전용(FR-05)
        foreach (var vg in world.VbusGaps(enclosureGap))
        {
            // 두 이웃 센서 사이 틈의 가운데, 체인 선(CY) 위에 걸린 작은 유닛 — 센서(폭 26~32)보다 작게(14), 깊이도 얕게.
            var x = world.GapMid(vg);
            var z0 = cz - 1;
            var z1 = cz + 4 * p.K;
            Box(o, p, x, CY - 8, CY + 8, z0, z1, 14, FenceInk.VbusFront, FenceInk.VbusSide, FenceInk.VbusTop);
            foreach (var y in new[] { CY - 4, CY + 4 }) o.Add(Seg(FenceInk.VbusRib, p.P(x - 6, y, z1), p.P(x + 6, y, z1)));
            var tp = p.P(x, CY, z1);
            o.Add(Text(FenceInk.VbusText, new Point(tp.X, tp.Y + 3.5), "V", 10));
            var lp = p.P(x, 0, 52);                                  // 기둥 번호(깊이 20) 아래 · 리턴케이블(118) 위
            o.Add(Text(FenceInk.VbusLabel, new Point(lp.X, lp.Y + 4), "V · VBUS 보상", 10));
        }
    }

    private static void BranchCables(List<FenceShape> o, FenceWorld world, FenceProjector p)
    {
        const double CY = 50, Zc = 76;
        var cz = p.De / 2 + 3 * p.K;
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(0, 58, Zc - 9 * p.K), p.P(0, 58, cz + 1), p.P(0, CY, cz) }));

        void Side(IReadOnlyList<int> branch, int sign)
        {
            if (branch.Count == 0) return;
            var xs = new List<double> { 0 };
            xs.AddRange(branch.Select(k => world.X[k]));
            var end = sign < 0 ? xs.Min() : xs.Max();
            o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(0, CY, cz), p.P(end, CY, cz) }));
            for (var i = 0; i < xs.Count - 1; i++)
            {
                if (Math.Abs(xs[i + 1] - xs[i]) < 20) continue;
                var m = p.P((xs[i] + xs[i + 1]) / 2, CY, cz);
                o.Add(Poly(FenceInk.Chevron, new Point(m.X - 4 * sign, m.Y - 4.5), new Point(m.X + 4.5 * sign, m.Y), new Point(m.X - 4 * sign, m.Y + 4.5)));
            }
            var lp = p.P(end, 0, 46);
            o.Add(Text(FenceInk.LabelChain, new Point(lp.X, lp.Y + 4),
                sign < 0 ? "◀ 왼쪽 가지 · 제어기에서 L1, L2 …" : "오른쪽 가지 · 제어기에서 R1, R2 … ▶", 11,
                sign < 0 ? FenceTextAnchor.Start : FenceTextAnchor.End));
        }

        // 가지 목록은 제어기 쪽부터 — 그대로 x 를 쓴다(왼쪽은 음수).
        Side(world.Chain.Branch(WiringSpec.LINE_PRIMARY), -1);
        Side(world.Chain.Branch(WiringSpec.LINE_SECONDARY), 1);
    }

    private static void LineCables(List<FenceShape> o, FenceWorld world, FenceProjector p)
    {
        if (world.Seq.Count == 0) return;
        var xs = world.Seq.Select(k => world.X[k]).ToList();
        var end = xs.Max();
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(0, 0, UZ), p.P(0, -9, UZ), p.P(end, -9, UZ) }));
        foreach (var x in xs) o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(x, -9, UZ), p.P(x, -2, UZ) }));
        var lp = p.P(end, -SEC + 12, UZ);
        o.Add(Text(FenceInk.LabelChain, lp, "한 줄 · 제어기 쪽이 1 ▶", 11, FenceTextAnchor.End));
    }
    #endregion

    #region - Chips -
    /// <summary>센서 칩 — 제품 모양(FR-10 · FR-17) + 번호판 + 모서리 표지(제안 = 왼쪽 위 · 미저장 = 오른쪽 위) + 선택 윤곽.</summary>
    /// <param name="lift">설치 자리 높이로 칩 전체를 올리는 값(세계 단위 · fence-wiring-editor FR-07). 0 이면 목업 높이 그대로.</param>
    /// <param name="coil">
    /// 윤형 코일 자리면 그 코일 반지름(세계 단위 · <see cref="FenceWorld.CoilOf"/>) — 몸은 코일 <b>안</b>(가운데 높이)에, 번호판은 코일 <b>위</b>에 둔다
    /// (번호 글자가 코일 선을 지나지 않게 · 펜스센서는 윤형과 같이 배치). 0 이면 보통 칩.
    /// </param>
    public static FenceChipPicture Sensor(FenceSensor s, WiringShape shape, FenceProjector p, bool selected, double zoom = 1, double lift = 0, double coil = 0)
    {
        if (lift != 0) p = p with { YLift = lift };
        // 코일 위 번호판 자리 — 코일 꼭대기(그림의 코일과 같은 식: 가운데 − 세로 반지름 × 1.02)에서 틈 + 반 판만큼 위
        Point? coilPlate = null;
        if (coil > 0 && s.Kind != FenceKind.Underground)
        {
            var anchor = s.Kind switch { FenceKind.Fence => 66.0, FenceKind.Multi => H + 36, _ => 71.0 };
            var centre = p.P(0, anchor, 0);
            var f = Math.Max(1, MIN_TEXT / (COIL_PLATE_TEXT * SafeZoom(zoom)));
            coilPlate = new Point(centre.X, centre.Y - coil * p.Cy * 1.02 - COIL_PLATE_GAP - COIL_PLATE_H * f / 2);
        }
        var o = new List<FenceShape>(24);
        var k = p.K;
        var de = p.De;
        var big = s.Big(shape);
        Point[] bb;

        // 보는 쪽(FR-20) — 뒤를 보면 몸체를 기둥 반대쪽으로 민다(칩의 "뒤" 표지와 함께 — 땅 부채꼴은 없앴다).
        var q = s.IsBackFacing ? p with { ZOffset = BackFacingOffset(p) } : p;
        Rect? plate = null;

        switch (s.Kind)
        {
            case FenceKind.Multi:
            {
                var zf = de / 2 + 14 * k;
                Box(o, p, 0, 0, H + 16, -de / 2 - 1, de / 2 + 1, 7, FenceInk.PostFront, FenceInk.PostSide, FenceInk.PostTop);
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Ball, new[] { p.P(0, H + 20, 0) }, 4.6, 4.6));     // 볼 마운트는 기둥 끝에 남는다
                var mw = SensorBodyWidth(38, s.Yaw);
                Box(o, q, 0, H + 24, H + 48, de / 2, zf, mw, FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop);
                Lens(o, q, s.Yaw, mw, H + 24, H + 48, zf, new Point(-11, H + 36), 4);
                if (s.IsBackFacing) MeshOver(o, q, mw, H + 24, H + 48, zf);
                var pl = coilPlate ?? q.P(5, H + 36, zf);
                plate = Plate(o, pl, 24, 16, big, FenceInk.NumberSmall, 11, 4, s, zoom);
                bb = new[] { q.P(-19, H + 48, de / 2), q.P(19, H + 48, de / 2), q.P(-19, H + 24, zf), q.P(19, H + 24, zf), q.P(19, H + 48, 0) };
                break;
            }
            case FenceKind.Fence:
            {
                var zf = de / 2 + 6 * k;
                // 코일 안 — 몸의 깊이 가운데가 코일(깊이 0)과 같은 화면 높이에 오게 조금 올린다(입체에서 몸이 코일 아래로 처지지 않게)
                var b = coilPlate is null ? p : p with { YLift = p.YLift + (de / 2 + 3 * k) * p.Cz / p.Cy };
                Box(o, b, 0, 56, 76, de / 2, zf, 13, FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop);
                var c = b.P(0, 66, zf);
                o.Add(RectShape(FenceInk.Pir, new Rect(c.X - 3, c.Y - 5, 6, 3), 1));
                if (coilPlate is { } cp)
                {
                    plate = Plate(o, cp, 22, COIL_PLATE_H, big, FenceInk.NumberSmall, COIL_PLATE_TEXT, 4, s, zoom);
                    bb = new[] { b.P(-7, 76, de / 2), b.P(7, 76, de / 2), b.P(-7, 56, zf), b.P(7, 56, zf), b.P(7, 76, 0) };
                }
                else
                {
                    var l = b.P(0, 46, zf);
                    o.Add(Text(FenceInk.FenceLabel, new Point(l.X, l.Y + 3), big, 10));
                    Corners(o, c.X, b.P(0, 71, zf).Y, 13, 10, s);
                    bb = new[] { b.P(-7, 76, de / 2), b.P(7, 76, de / 2), b.P(-7, 40, zf), b.P(7, 40, zf), b.P(7, 76, 0) };
                }
                break;
            }
            case FenceKind.Underground:
            {
                var rod = new[] { p.P(-2.6, 0, UZ), p.P(2.6, 0, UZ), p.P(2.6, -40, UZ), p.P(0, -48, UZ), p.P(-2.6, -40, UZ) };
                o.Add(Poly(FenceInk.Rod, rod));
                foreach (var y in new[] { -12.0, -24.0, -34.0 }) o.Add(Seg(FenceInk.RodRib, p.P(-3.4, y, UZ), p.P(3.4, y, UZ)));
                o.Add(Seg(FenceInk.RodRib, p.P(0, 2, UZ), p.P(0, 13, UZ)));
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.UgCap, new[] { p.P(0, 0, UZ) }, 8, 3 + 2 * k));
                var pl = p.P(0, 22, UZ);
                plate = Plate(o, pl, 22, 18, big, FenceInk.Number, 12.5, 4.5, s, zoom);
                bb = new[] { p.P(-12, 32, UZ), p.P(12, 32, UZ), p.P(-9, -48, UZ), p.P(9, -48, UZ) };
                break;
            }
            default:
            {
                double zb0 = de / 2, zb1 = de / 2 + 12 * k, zh = de / 2 + 19 * k, zg0 = de / 2 + 3 * k, zg1 = de / 2 + 8 * k;
                Box(o, q, -6, 43, 50, zg0, zg1, 5, FenceInk.GlandFront, FenceInk.GlandSide, FenceInk.GlandTop);
                Box(o, q, 6, 43, 50, zg0, zg1, 5, FenceInk.GlandFront, FenceInk.GlandSide, FenceInk.GlandTop);
                var bw = SensorBodyWidth(26, s.Yaw);
                Box(o, q, 0, 50, 92, zb0, zb1, bw, FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop);
                Box(o, q, 0, 92, 99, zb0, zh, SensorBodyWidth(32, s.Yaw), FenceInk.OliveFront, FenceInk.OliveSide, FenceInk.OliveTop);
                Lens(o, q, s.Yaw, bw, 50, 92, zb1, new Point(0, 58), 3.2);
                if (s.IsBackFacing) MeshOver(o, q, bw, 50, 99, zb1);
                var pl = coilPlate ?? q.P(0, 78, zb1);
                plate = Plate(o, pl, 20, coilPlate is null ? 18 : COIL_PLATE_H, big, FenceInk.Number, coilPlate is null ? 12.5 : COIL_PLATE_TEXT, 4.5, s, zoom);
                bb = new[] { q.P(-16, 99, zb0), q.P(16, 99, zb0), q.P(-16, 92, zh), q.P(16, 92, zh), q.P(-9, 43, zg1), q.P(9, 43, zg1), q.P(16, 99, zb0 - 1) };
                break;
            }
        }

        // 번호판은 작은 배율에서 커진다(Plate) — 적중 사각형(= 칩 배치 사각형)이 그것을 담아야 잘리지 않는다.
        if (plate is { } pr) bb = bb.Append(pr.TopLeft).Append(pr.BottomRight).ToArray();

        // "뒤" 표지(FR-20) — 번호판 <b>왼쪽</b>에 붙인 작은 판(번호판과 겹치지 않는다). 색이 아니라 글자로 말한다(주 글자라 줌에 맞서 키운다).
        if (s.IsBackFacing && plate is { } pl2)
        {
            var tag = BackTagRect(pl2, zoom);
            var size = tag.Height - 5;
            o.Add(RectShape(FenceInk.FacingTag, tag, 3));
            o.Add(Text(FenceInk.FacingTagText, new Point(tag.X + tag.Width / 2, tag.Y + tag.Height / 2 + size * 0.36), "내", size));
            bb = bb.Append(tag.TopLeft).Append(tag.BottomRight).ToArray();
        }

        var box = Bounds(bb);
        var hit = new Rect(box.X - 5, box.Y - 5, box.Width + 10, box.Height + 10);
        o.Insert(0, RectShape(FenceInk.Hit, hit, 0));
        if (selected) o.Add(RectShape(FenceInk.Select, hit, 8));
        return new FenceChipPicture(Legible(o, zoom), hit);
    }

    /// <summary>코일 위 번호판 — 판 높이 · 글자 크기 · 코일 꼭대기와의 틈(세계 단위 · 가시 끝보다 멀리).</summary>
    public const double COIL_PLATE_H = 16;
    public const double COIL_PLATE_TEXT = 11;
    public const double COIL_PLATE_GAP = 4;

    /// <summary>격자 점 반지름(화면 px).</summary>
    public const double SNAP_DOT_PX = 4;

    /// <summary>펜스센서 묶음 칩(FR-18) — "펜스센서 ×N" 겹 카드 + 첫–끝 번호.</summary>
    /// <param name="maxWidth">
    /// 이웃 칩까지 남는 폭(세계 단위) — 작은 배율에서 글자가 줌에 맞서 커지면 카드가 이웃 번호를 덮는다(검토 V2 · 실측 34%).
    /// 넘치면 짧은 카드("×5" · 번호 줄 없음)로 줄인다(기둥 솎기와 같은 생각 — 덮느니 줄인다). 없으면 제한 없음.
    /// </param>
    public static FenceChipPicture Group(IReadOnlyList<FenceSensor> members, WiringShape shape, FenceProjector p, bool selected, double zoom = 1,
                                         double maxWidth = double.PositiveInfinity)
    {
        var o = new List<FenceShape>(10);
        var n = members.Count;
        var title = $"펜스센서 ×{n}";
        var range = $"{members[0].Big(shape)}–{members[^1].Big(shape)}";
        var titleSize = Math.Max(15, MIN_TEXT / SafeZoom(zoom));
        var subSize = Math.Max(11, MIN_TEXT / SafeZoom(zoom));
        var w = Math.Max(Math.Max(EstimateWidth(title, titleSize), EstimateWidth(range, subSize)) + 26, 70);
        var compact = w > maxWidth;
        if (compact)
        {
            title = $"×{n}";
            range = string.Empty;
            subSize = 0;
            w = Math.Max(EstimateWidth(title, titleSize) + 16, 30);
        }
        var h = compact ? Math.Max(26, titleSize + 10) : Math.Max(40, titleSize + subSize + 16);
        var c = p.P(0, 66, p.De / 2 + 4);
        var x = c.X - w / 2;
        var y = c.Y - h / 2;

        var hit = new Rect(x - 8, y - 14, w + 16, h + 40);
        o.Add(RectShape(FenceInk.Hit, hit, 0));
        o.Add(RectShape(FenceInk.GroupBack, new Rect(x + 8, y - 8, w, h), 15));
        o.Add(RectShape(FenceInk.GroupBack, new Rect(x + 4, y - 4, w, h), 15));
        o.Add(RectShape(FenceInk.GroupBody, new Rect(x, y, w, h), 15));
        // 첫–끝 번호는 카드 <b>안</b> 둘째 줄 — 카드 밖에 두면 기둥 · 철망 무늬 위라 읽히지 않았다(실측 · 2026-09-29).
        var top = compact ? y + (h - titleSize) / 2 : y + (h - titleSize - subSize - 4) / 2;
        o.Add(Text(FenceInk.GroupText, new Point(c.X, top + titleSize * 0.86), title, titleSize));
        if (!compact) o.Add(Text(FenceInk.GroupSub, new Point(c.X, top + titleSize + 4 + subSize * 0.82), range, subSize));
        if (members.Any(m => m.IsChanged))
            o.Add(Poly(FenceInk.Draft, new Point(x + w - 14, y), new Point(x + w - 4, y), new Point(x + w, y + 4), new Point(x + w, y + 14)));
        if (selected) o.Add(RectShape(FenceInk.Select, new Rect(x - 5, y - 9, w + 18, h + 30), 16));
        return new FenceChipPicture(Legible(o, zoom), hit);
    }

    /// <summary>제어기 · 함체 칩 — 링은 1U 도킹 함체(스마트 제어기 + VBUS 제어기 · 포트 A/B), 그 밖은 기둥 위 제어기 상자.</summary>
    public static FenceChipPicture Controller(WiringShape shape, FenceProjector p, bool selected, double zoom = 1)
    {
        var o = new List<FenceShape>(24);
        var k = p.K;
        Point fl, fr;
        double dx, dy;

        if (shape == WiringShape.Ring)
        {
            double z0 = 106 + 24 * (1 - k), z1 = 130, ew = 150, eh = 44;
            Box(o, p, 0, 0, eh, z0, z1, ew, FenceInk.EnclosureFront, FenceInk.EnclosureSide, FenceInk.EnclosureTop);
            o.Add(RectShape(FenceInk.Dock, RectOf(p.P(-62, 30, z1), p.P(62, 12, z1)), 1.5));
            o.Add(RectShape(FenceInk.CardSmart, RectOf(p.P(-60, 28, z1), p.P(18, 14, z1)), 0));
            o.Add(RectShape(FenceInk.CardVbus, RectOf(p.P(20, 28, z1), p.P(60, 14, z1)), 0));
            // 포트 A · B 는 동그라미만 — 글자(6px)는 읽히지 않는다. 이름은 함체 밖 "Ch1(A) · Ch2(B)" 가 말한다(§1-0).
            foreach (var px in new[] { -50.0, -38.0 })
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Port, new[] { p.P(px, 21, z1) }, 3.4, 3.4));
            var t1 = p.P(-10, 21, z1);
            var t2 = p.P(40, 21, z1);
            o.Add(Text(FenceInk.CardSmartText, new Point(t1.X, t1.Y + 3.6), "스마트", 10));
            o.Add(Text(FenceInk.CardVbusText, new Point(t2.X, t2.Y + 3.6), "VBUS", 10));
            var sa = p.P(-ew / 2 - 6, 30, z1);
            var sb = p.P(ew / 2 + 6, 30, z1);
            o.Add(Text(FenceInk.LabelReturn, sa, WiringValidation.PORT_1, 11, FenceTextAnchor.End));
            o.Add(Text(FenceInk.LabelReturn, new Point(sb.X + (z1 - z0) * p.Sh, sb.Y), WiringValidation.PORT_2, 11, FenceTextAnchor.Start));
            var cap = p.P(0, 0, z1);
            o.Add(Text(FenceInk.Caption, new Point(cap.X, cap.Y + 15), "함체 · 1U 도킹(스마트 + VBUS 제어기) — ‹ 옆으로 끌기 ›", 10.5));
            fl = p.P(-ew / 2, eh, z1);
            fr = p.P(ew / 2, 0, z1);
            dx = (z1 - z0) * p.Sh;
            dy = (z1 - z0) * p.Cz;
        }
        else
        {
            var zc = shape == WiringShape.TwoBranch ? 76 : UZ - 8;
            var z0 = zc - 9 * k;
            var z1 = k > 0 ? zc + 9 * k : zc;
            Box(o, p, 0, 0, 28, zc - 2 * k, zc + 2 * k, 6, FenceInk.PostFront, FenceInk.PostSide, FenceInk.PostTop);
            Box(o, p, 0, 28, 60, z0, z1, 50, FenceInk.ControllerFront, FenceInk.ControllerSide, FenceInk.ControllerTop);
            // "제어기" 는 늘 화면에서 11px 이상(Legible) — 전원 · 통신은 아래 설명 글에 있다(작은 7px 부제는 뺐다).
            var t1 = p.P(0, 44, z1);
            o.Add(Text(FenceInk.ControllerText, new Point(t1.X, t1.Y + 4), "제어기", 11));
            if (shape == WiringShape.TwoBranch)
            {
                var cap = p.P(0, 0, z1);
                o.Add(Text(FenceInk.Caption, new Point(cap.X, cap.Y + 15), "PIDS 제어기 · 24VDC · Ethernet", 10.5));
            }
            else
            {
                // 한 줄은 제어기 아래로 체인 선이 지나간다 — 설명은 상자 위에.
                var cap = p.P(-25, 76, z1);
                o.Add(Text(FenceInk.Caption, cap, "지중 제어기 · 24VDC · Ethernet", 10.5, FenceTextAnchor.Start));
            }
            fl = p.P(-25, 60, z1);
            fr = p.P(25, 0, z1);
            dx = (z1 - z0) * p.Sh;
            dy = (z1 - z0) * p.Cz;
        }

        var hit = new Rect(fl.X - 6, fl.Y - dy - 6, fr.X - fl.X + dx + 12, fr.Y - fl.Y + dy + 12);
        o.Insert(0, RectShape(FenceInk.Hit, hit, 0));
        if (selected) o.Add(RectShape(FenceInk.Select, hit, 6));
        return new FenceChipPicture(Legible(o, zoom), hit);
    }
    #endregion

    #region - Overlay -
    /// <summary>
    /// 덧그림(세계 좌표) — "번호 같음 · id순" 알약 · 고른/가리킨 센서 이름 알약 · 끄는 중 삽입 막대(세로 막대 + 위아래 화살 + 자리 알약).
    /// </summary>
    public static IReadOnlyList<FenceShape> Overlay(FenceWorld world, FenceProjector p, IReadOnlyCollection<int> hidden,
                                                    int? named, double? insertionX, string? insertionLabel, double zoom = 1,
                                                    IReadOnlyList<FenceSnapDot>? snapDots = null, string? snapLabel = null)
    {
        var o = new List<FenceShape>();
        var lt = world.LabelTop;
        var lz = world.OverlayDepth;

        foreach (var (key, x) in world.X)
        {
            if (hidden.Contains(key) || !world.Sensors[key].IsDuplicateNumber) continue;
            o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.PillDuplicate, new[] { p.P(x, lt - 28, lz) }, Text: "번호 같음 · id순", FontSize: 10));
        }

        if (insertionX is null && named is { } key2 && world.X.TryGetValue(key2, out var nx) && !hidden.Contains(key2))
        {
            var s = world.Sensors[key2];
            o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.Pill, new[] { p.P(nx, lt - 4, lz) }, Text: $"{s.Name} · {s.Number}", FontSize: 11.5));
        }

        if (insertionX is { } ix)
        {
            var z = world.Shape == WiringShape.Line ? FenceProjector.UZ : p.De / 2;
            var (ya, yb) = world.InsertionSpan;
            var top = p.P(ix, yb, z);
            var bot = p.P(ix, ya, z);
            o.Add(Seg(FenceInk.Insert, top, bot));
            o.Add(Poly(FenceInk.Insert, new Point(top.X - 6, top.Y - 9), new Point(top.X + 6, top.Y - 9), new Point(top.X, top.Y - 1)));
            o.Add(Poly(FenceInk.Insert, new Point(bot.X - 6, bot.Y + 9), new Point(bot.X + 6, bot.Y + 9), new Point(bot.X, bot.Y + 1)));
            if (!string.IsNullOrEmpty(insertionLabel))
                o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.PillInsert, new[] { p.P(ix, yb + 26, z) }, Text: insertionLabel, FontSize: 11.5));
        }

        // 9점 격자 안내(끄는 동안) — 빨강 채운 원(선택 윤곽 · 삽입 막대와 형태가 다르다) · 센서가 있는 점은 흐리게 · 포인터 아래는 크게 + 고리 ·
        // 함께 끈 센서가 갈 점은 고리 · 갈 수 없으면 빈 마름모(경고). 크기는 화면에서 같게(줌에 맞서).
        if (snapDots is { Count: > 0 })
        {
            var r = SNAP_DOT_PX / SafeZoom(zoom);
            foreach (var dot in snapDots)
            {
                switch (dot.State)
                {
                    case FenceSnapState.Blocked:
                        o.Add(Poly(FenceInk.SnapBlocked, new Point(dot.At.X, dot.At.Y - r * 1.8), new Point(dot.At.X + r * 1.8, dot.At.Y), new Point(dot.At.X, dot.At.Y + r * 1.8), new Point(dot.At.X - r * 1.8, dot.At.Y)));
                        break;
                    case FenceSnapState.Hot:
                        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.SnapRing, new[] { dot.At }, r * 2.4, r * 2.4));
                        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.SnapDot, new[] { dot.At }, r * 1.5, r * 1.5));
                        break;
                    case FenceSnapState.Member:
                        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.SnapRing, new[] { dot.At }, r * 1.8, r * 1.8));
                        break;
                    case FenceSnapState.Occupied:
                        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.SnapDotDim, new[] { dot.At }, r, r));
                        break;
                    default:
                        o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.SnapDot, new[] { dot.At }, r, r));
                        break;
                }
            }
            if (!string.IsNullOrEmpty(snapLabel) && snapDots.FirstOrDefault(d => d.State is FenceSnapState.Hot or FenceSnapState.Blocked) is { } hot)
                o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.PillInsert, new[] { new Point(hot.At.X, hot.At.Y - 26) }, Text: snapLabel, FontSize: 11.5));
        }
        return Legible(o, zoom);
    }
    #endregion

    #region - Legibility (글자 최소 크기) -
    /// <summary>화면에서 글자가 이보다 작아지지 않게 한다(DIU) — 주 글자(번호 · 묶음 제목 · 알약 · 빈틈)는 줌에 맞서 키운다.</summary>
    public const double MIN_TEXT = 10;

    /// <summary>제어기 이름("제어기")은 늘 이보다 크게.</summary>
    public const double MIN_CONTROLLER_TEXT = 11;

    /// <summary>보조 글자(축 · 기둥 번호 · A/B 알약 · 설명)는 화면에서 이보다 작아지면 그리지 않는다.</summary>
    public const double HIDE_SECONDARY_BELOW = 9;

    /// <summary>이 줌까지는 보조 글자를 화면 <see cref="HIDE_SECONDARY_BELOW"/> 로 받쳐 보이고, 그보다 작으면 뺀다(겹침 방지).</summary>
    public const double SECONDARY_MIN_ZOOM = 0.6;

    /// <summary>주 글자 — 읽을 수 있어야 하는 것. 그 밖은 보조.</summary>
    public static bool IsPrimaryText(FenceInk ink) => ink is FenceInk.Number or FenceInk.NumberSmall or FenceInk.FenceLabel
        or FenceInk.GroupText or FenceInk.ControllerText or FenceInk.GapText or FenceInk.Pill or FenceInk.PillInsert or FenceInk.FacingTagText
        or FenceInk.GroupSub       // 묶음 카드 안 첫–끝 번호
        or FenceInk.SideLabel;     // "펜스 외부 · 내부" — 보는 쪽(앞 · 뒤)을 읽는 기준이라 작은 배율에서도 남긴다(FR-20)

    private static double SafeZoom(double zoom) => zoom > 0.05 ? zoom : 0.05;

    /// <summary>
    /// 줌 <paramref name="zoom"/> 에서 글자를 읽히게 — 주 글자는 화면 <see cref="MIN_TEXT"/>(제어기 <see cref="MIN_CONTROLLER_TEXT"/>) 이상으로 키우고,
    /// 보조 글자는 줌이 <see cref="SECONDARY_MIN_ZOOM"/> 이상이면 화면 <see cref="HIDE_SECONDARY_BELOW"/> 로 받쳐 보이고, 그보다 작으면 뺀다. 글자가 아닌 그림은 그대로.
    /// </summary>
    public static IReadOnlyList<FenceShape> Legible(IReadOnlyList<FenceShape> shapes, double zoom)
    {
        var z = SafeZoom(zoom);
        var result = new List<FenceShape>(shapes.Count);
        foreach (var shape in shapes)
        {
            if (shape.Kind is not (FenceShapeKind.Text or FenceShapeKind.Pill)) { result.Add(shape); continue; }
            if (IsPrimaryText(shape.Ink))
            {
                var min = shape.Ink == FenceInk.ControllerText ? MIN_CONTROLLER_TEXT : MIN_TEXT;
                result.Add(shape.FontSize * z >= min ? shape : shape with { FontSize = min / z });
            }
            else if (shape.FontSize * z >= HIDE_SECONDARY_BELOW) result.Add(shape);
            else if (z >= SECONDARY_MIN_ZOOM) result.Add(shape with { FontSize = HIDE_SECONDARY_BELOW / z });   // 조금 줄였으면 9px 로 받쳐 준다
        }
        return result;
    }
    #endregion

    #region - Helpers -
    /// <summary>글자 폭 어림(렌더러 없이) — 한글 · 전각은 글자 크기, 그 밖은 0.58 배.</summary>
    public static double EstimateWidth(string text, double px)
        => (text ?? string.Empty).Sum(ch => ch >= 0x1100 ? px : ch == ' ' ? px * 0.3 : px * 0.58);

    /// <summary>"뒤" 표지 사각형 — 번호판 왼쪽에 2 만큼 띄워 붙이고 세로 가운데를 맞춘다(겹침 없음 · 시험 대상).</summary>
    public static Rect BackTagRect(Rect plate, double zoom)
    {
        var size = Math.Max(10, MIN_TEXT / SafeZoom(zoom));
        var w = size + 7;
        var h = size + 5;
        return new Rect(plate.Left - 2 - w, plate.Top + plate.Height / 2 - h / 2, w, h);
    }

    /// <summary>번호판 — 줌이 작아 번호를 키워야 하면(<see cref="MIN_TEXT"/>) 판도 같은 비율로 키운다.</summary>
    private static Rect Plate(List<FenceShape> o, Point at, double w, double h, string big, FenceInk numberInk, double size, double baseline, FenceSensor s,
                              double zoom)
    {
        var f = Math.Max(1, MIN_TEXT / (size * SafeZoom(zoom)));
        w *= f; h *= f; size *= f; baseline *= f;
        w = Math.Max(w, EstimateWidth(big, size) + 6 * f);          // 세 자리 센서 번호(101 …)도 판 안에
        o.Add(RectShape(FenceInk.Plate, new Rect(at.X - w / 2, at.Y - h / 2, w, h), 2.5 * f));
        o.Add(Text(numberInk, new Point(at.X, at.Y + baseline), big, size));
        Corners(o, at.X, at.Y, w, h, s);
        return new Rect(at.X - w / 2, at.Y - h / 2, w, h);
    }

    /// <summary>모서리 표지 — 제안 = 왼쪽 위 삼각(정보) · 미저장 = 오른쪽 위 삼각(경고) · 색이 아니라 자리로 가른다.</summary>
    private static void Corners(List<FenceShape> o, double cx, double cy, double w, double h, FenceSensor s)
    {
        var x0 = cx - w / 2;
        var y0 = cy - h / 2;
        var x1 = cx + w / 2;
        var size = Math.Min(8, w / 2.4);
        if (s.IsSuggested) o.Add(Poly(FenceInk.Proposal, new Point(x0, y0), new Point(x0 + size, y0), new Point(x0, y0 + size)));
        if (s.IsChanged) o.Add(Poly(FenceInk.Draft, new Point(x1, y0), new Point(x1 - size, y0), new Point(x1, y0 + size)));
    }

    /// <summary>목업 <c>box()</c> — 앞면 · (깊이가 있으면) 옆면 · 윗면.</summary>
    #region - Sensor orientation (센서 방향 2026-10-01) -
    /// <summary>옆모습(90° · 270°)에서 몸의 앞면 폭 = 정면 폭 × 이 비(좁은 옆판).</summary>
    public const double SIDE_PROFILE_RATIO = 0.45;

    /// <summary>보는 방향에 따른 몸 앞면 폭 — 정면 · 뒷면(0° · 180°)은 그대로, 옆모습(90° · 270°)은 좁게.</summary>
    public static double SensorBodyWidth(double frontWidth, WiringYaw yaw)
        => yaw is WiringYaw.Along or WiringYaw.Against ? frontWidth * SIDE_PROFILE_RATIO : frontWidth;

    /// <summary>렌즈가 그림의 어느 쪽 끝인가 — 정방향(90°) = 오른쪽(+1) · 역방향(270°) = 왼쪽(−1) · 정면 · 뒷면 = 가운데(0).</summary>
    public static int LensSide(WiringYaw yaw) => yaw switch { WiringYaw.Along => 1, WiringYaw.Against => -1, _ => 0 };

    /// <summary>
    /// 렌즈 — 정면(0°): 앞면에 렌즈(지금까지의 모습) · 뒷면(180°): 렌즈 없이 뒷판 이음매 · 옆모습(90° · 270°): 보는 쪽 끝에 렌즈 + 그 모서리를 주 색 굵은 선으로
    /// (화살 · 부채꼴이 아닌 몸의 모양으로 말한다 — 사용자가 지운 화살을 되살리지 않는다).
    /// </summary>
    private static void Lens(List<FenceShape> o, FenceProjector q, WiringYaw yaw, double width, double y0, double y1, double z, Point front, double radius)
    {
        switch (yaw)
        {
            case WiringYaw.Toward:
                o.Add(Seg(FenceInk.BackSeam, q.P(0, y0 + 3, z), q.P(0, y1 - 3, z)));
                break;
            case WiringYaw.Along:
            case WiringYaw.Against:
            {
                var side = LensSide(yaw);
                var x = side * width / 2;
                o.Add(Seg(FenceInk.LensEdge, q.P(x, y0 + 2, z), q.P(x, y1 - 2, z)));
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Pir, new[] { q.P(x - side * radius * 0.6, front.Y, z) }, radius * 0.8, radius));
                break;
            }
            default:
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Pir, new[] { q.P(front.X, front.Y, z) }, radius, radius));
                break;
        }
    }

    /// <summary>철망 뒤(내부) 센서 — 몸 위로 철망 선 몇 가닥을 겹친다(철망 면 뒤에 있음을 형태로 · 고를 수는 그대로).</summary>
    private static void MeshOver(List<FenceShape> o, FenceProjector q, double width, double y0, double y1, double z)
    {
        var half = width / 2 + 3;
        for (var t = -half; t < half; t += 7)
            o.Add(Seg(FenceInk.MeshOver, q.P(t, y0, z + 0.5), q.P(t + (y1 - y0) * 0.35, y1, z + 0.5), 0.85));
    }
    #endregion

    private static void Box(List<FenceShape> o, FenceProjector p, double x, double y0, double y1, double z0, double z1, double w,
                            FenceInk front, FenceInk side, FenceInk top)
    {
        var xa = x - w / 2;
        var xb = x + w / 2;
        if (z1 - z0 > 0.4)
        {
            o.Add(Poly(side, p.P(xb, y0, z1), p.P(xb, y0, z0), p.P(xb, y1, z0), p.P(xb, y1, z1)));
            o.Add(Poly(top, p.P(xa, y1, z1), p.P(xb, y1, z1), p.P(xb, y1, z0), p.P(xa, y1, z0)));
        }
        o.Add(Poly(front, p.P(xa, y0, z1), p.P(xb, y0, z1), p.P(xb, y1, z1), p.P(xa, y1, z1)));
    }

    private static Rect Bounds(IEnumerable<Point> points)
    {
        var list = points.ToList();
        return new Rect(new Point(list.Min(q => q.X), list.Min(q => q.Y)), new Point(list.Max(q => q.X), list.Max(q => q.Y)));
    }

    private static Rect RectOf(Point a, Point b) => new(a, b);

    private static FenceShape Poly(FenceInk ink, params Point[] points) => new(FenceShapeKind.Polygon, ink, points);

    private static FenceShape Seg(FenceInk ink, Point a, Point b, double opacity = 1) => new(FenceShapeKind.Line, ink, new[] { a, b }, Opacity: opacity);

    private static FenceShape RectShape(FenceInk ink, Rect r, double radius)
        => new(FenceShapeKind.Rect, ink, new[] { r.TopLeft, r.BottomRight }, radius, radius);

    private static FenceShape Text(FenceInk ink, Point at, string text, double size, FenceTextAnchor anchor = FenceTextAnchor.Middle)
        => new(FenceShapeKind.Text, ink, new[] { at }, Text: text, FontSize: size, Anchor: anchor);
    #endregion
}
