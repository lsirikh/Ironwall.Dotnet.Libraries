using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 펜스 편집기 장면(fence-wiring-editor FR-02 · FR-04 · FR-12) — 망 목록으로 그린 펜스 · 망 칩 · 모양 견본 · 러버밴드.
/// 모양 5종을 2.5D 투영에 그린다(Viewport3D 아님). 색은 잉크 · 사람이 고른 색(<see cref="FenceShape.Color"/>)으로만 — 렌더러가 매번 토큰을 푼다.
/// </summary>
public static partial class FenceScene
{
    /// <summary>담의 두께(세계 깊이).</summary>
    public const double WALL_DEPTH = 10;

    /// <summary>철망 · 윤형 기둥 폭(세계 단위) — 가는 관.</summary>
    public const double CHAIN_POST_W = 6;

    /// <summary>윤형 코일 반지름(세계 단위).</summary>
    public const double RAZOR_R = 9;

    /// <summary>
    /// 망 목록으로 세운 세계의 정적 층 — 땅 · 번호 · 탐지 범위 · 망(모양 5종) · 기둥. 케이블(리턴케이블 · 함체 쪽 · A/B 번호)은
    /// <paramref name="showCables"/> 일 때만(기본 꺼짐 — 연결은 개념도가 맡는다 · FR-12).
    /// </summary>
    public static IReadOnlyList<FenceShape> StaticLayout(FenceWorld world, FenceProjector p, bool showRange, bool showCables,
                                                         double enclosureX, int enclosureGap, double zoom = 1)
    {
        var geometry = world.Geometry ?? throw new InvalidOperationException("펜스 구성으로 세운 세계가 아닙니다.");
        var o = new List<FenceShape>(512);
        var u = world.Upm;
        var v = world.Vpm;
        var xs = world.X.Values.Append(0).Append(geometry.LengthM * u).Append(showCables ? enclosureX : 0).ToList();
        var x0 = xs.Min() - 5 * u;
        var x1 = xs.Max() + 5 * u;
        var gz0 = -36 * p.K;
        var gz1 = world.GroundDepth;

        o.Add(Poly(FenceInk.Ground, p.P(x0 - 40, 0, gz0), p.P(x1 + 40, 0, gz0), p.P(x1 + 40, 0, gz1), p.P(x0 - 40, 0, gz1)));
        if (p.K > 0.02)
            foreach (var post in geometry.Posts.Where(q => q.Exists))
                o.Add(Seg(FenceInk.Grid, p.P(post.XM * u, 0, 8), p.P(post.XM * u, 0, Math.Min(gz1, 120)), 0.35 * p.K));
        o.Add(Seg(FenceInk.Base, p.P(x0 - 40, 0, 0), p.P(x1 + 40, 0, 0)));

        var outside = p.P(x1 + SIDE_LABEL_LEAD, 0, OutsideLabelDepth(p));
        var inside = p.P(x1 + SIDE_LABEL_LEAD, 0, InsideLabelDepth(world.Shape));
        o.Add(Text(FenceInk.SideLabel, new Point(outside.X, outside.Y - 2), "펜스 외부", 11, FenceTextAnchor.Start));
        o.Add(Text(FenceInk.SideLabel, new Point(inside.X, inside.Y + 4), "펜스 내부", 11, FenceTextAnchor.Start));

        // 센서 번호(number_device) — 센서 아래 땅에. 위치를 옮기면 대역 번호가 다시 매겨져 여기서 바로 보인다(FR-09).
        foreach (var key in world.Seq)
        {
            if (!world.Sensors.TryGetValue(key, out var s)) continue;
            var q = p.P(world.X[key], 0, 20);
            o.Add(Text(FenceInk.PostNumber, new Point(q.X, q.Y + 4), s.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), 10.5));
        }
        // "번호" 머리 — 첫 번호 글자의 왼쪽 끝에서 틈(10)만큼 더 왼쪽에 끝나게(재검토: "번호80096" 이 붙어 보였다)
        var axis = p.P(x0 - 16 + 5 * u, 0, 20);
        var first = world.Seq.Where(world.X.ContainsKey).Select(k => (X: p.P(world.X[k], 0, 20).X, Text: world.Sensors.TryGetValue(k, out var fs) ? fs.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty))
                             .OrderBy(t => t.X).FirstOrDefault();
        var axisRight = first.Text is { Length: > 0 } ? Math.Min(axis.X, first.X - EstimateWidth(first.Text, 10.5) / 2 - NUMBER_CAPTION_GAP) : axis.X;
        o.Add(Text(FenceInk.Axis, new Point(axisRight, axis.Y + 4), "번호", 10, FenceTextAnchor.End));

        if (showRange)
            foreach (var (key, x) in world.X)
            {
                var r = FenceWorld.RangeOf(world.Sensors[key].Kind);
                if (r <= 0) continue;
                var c = p.P(x, 0, world.Sensors[key].Kind == FenceKind.Underground ? UZ : 0);
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Range, new[] { c }, r * u, Math.Max(6, r * u * p.Cz * 0.8)));
            }

        // 망 — 모양마다(FR-02). 기둥이 서 있는 쪽은 기둥 폭의 절반만큼 비운다.
        foreach (var panel in geometry.Panels)
        {
            var leftPost = geometry.Posts[panel.Index].Exists;
            var rightPost = geometry.Posts[panel.Index + 1].Exists;
            DrawPanel(o, p, panel, u, v, leftPost, rightPost, zoom);
        }

        // 기둥 — 윤형 옆 기둥은 코일 받침만큼 솟는다. 디자인펜스 옆 기둥은 초록.
        var postXs = geometry.Posts.Where(q => q.Exists).Select(q => q.XM * u).ToList();
        var keep = PostSensorXs(world);
        var shown = new HashSet<double>(ThinPosts(postXs, keep, zoom));
        foreach (var post in geometry.Posts.Where(q => q.Exists))
        {
            var x = post.XM * u;
            if (!shown.Contains(x)) continue;
            var h = post.HeightM * v;
            var design = new[] { post.Index - 1, post.Index }.Where(i => i >= 0 && i < geometry.Panels.Count)
                                                             .Any(i => geometry.Panels[i].Spec.Style == EnumFenceStyle.DesignFence);
            var color = new[] { post.Index - 1, post.Index }.Where(i => i >= 0 && i < geometry.Panels.Count)
                                                            .Select(i => geometry.Panels[i].Spec).FirstOrDefault(s => !s.IsWall && s.Style == EnumFenceStyle.DesignFence)?.Color;
            // 철망 기둥은 가는 관(사진), 디자인펜스 기둥은 각기둥 + 클램프
            var width = design ? PW : CHAIN_POST_W;
            BoxC(o, p, x, 0, h, -p.De / 2, p.De / 2, width, design ? FenceInk.DesignPost : FenceInk.PostFront, FenceInk.PostSide, FenceInk.PostTop, design ? color : null);
            BoxC(o, p, x, h, h + 5, -p.De / 2 - 1.5 * p.K, p.De / 2 + 1.5 * p.K, width + 3, FenceInk.CapFront, FenceInk.CapSide, FenceInk.CapTop, null);
            if (design) o.AddRange(FenceStyleArt.DesignClamps(p, x, h, width, color));
            if (post.HasRazor) o.AddRange(FenceStyleArt.YArms(p, x, h + FenceStyleArt.COIL_SEAT_GAP, h).Shapes);           // Y 받침(두 팔이 30° 바깥으로)
        }

        if (showCables) LaneCables(o, world, p);

        if (showRange)
            foreach (var gap in world.RangeGaps())
            {
                var c = p.P(gap.X, 0, 30);
                o.Add(Poly(FenceInk.GapMark, new Point(c.X, c.Y - 7), new Point(c.X + 6, c.Y), new Point(c.X, c.Y + 7), new Point(c.X - 6, c.Y)));
                o.Add(Text(FenceInk.GapText, new Point(c.X, c.Y - 12), $"빈틈 {gap.Metres:0}m", 10));
            }

        return Legible(o, zoom);
    }

    /// <summary>
    /// 망 한 칸 — 모양 5종(FR-02). 철조망: 마름모 망 + 위아래 레일 · 윤형: 그 위에 원형 코일 · 벽돌 · 시멘트: 두께 있는 담 + 갓돌(시멘트는 이음매) ·
    /// 디자인: 초록 세로살 + 가로대 두 줄.
    /// </summary>
    private static void DrawPanel(List<FenceShape> o, FenceProjector p, FencePanelGeometry panel, double u, double v, bool leftPost, bool rightPost, double zoom)
    {
        var spec = panel.Spec;
        var xa = panel.StartM * u;
        var xb = panel.EndM * u;
        var h = spec.HeightM * v;
        var color = spec.Color;
        var a = xa + (leftPost ? PW / 2 : 0);
        var b = xb - (rightPost ? PW / 2 : 0);
        if (b <= a) { a = xa; b = xb; }

        switch (spec.Style)
        {
            case EnumFenceStyle.Brick:
            case EnumFenceStyle.Concrete:
            {
                var wd = WALL_DEPTH * Math.Max(0.25, p.K);
                var brick = spec.Style == EnumFenceStyle.Brick;
                BoxC(o, p, (xa + xb) / 2, 0, h, -wd / 2, wd / 2, xb - xa,
                     brick ? FenceInk.BrickFront : FenceInk.ConcreteFront, brick ? FenceInk.BrickSide : FenceInk.WallSide, brick ? FenceInk.BrickTop : FenceInk.WallTopFace, color);
                BoxC(o, p, (xa + xb) / 2, h, h + 7, -wd / 2 - 2, wd / 2 + 2, xb - xa + 4, FenceInk.WallCap, FenceInk.WallCap, FenceInk.WallCap, null);
                if (spec.Style == EnumFenceStyle.Concrete)
                {
                    // 미장 결(결정적 반점 · 망 번호 씨) + 옅은 이음매
                    o.AddRange(FenceStyleArt.ConcreteStucco(p, xa, xb, h, wd / 2, zoom, panel.Index, color));
                    var cx = (xa + xb) / 2;
                    o.Add(Seg(FenceInk.ConcreteSeam, p.P(cx, 2, wd / 2), p.P(cx, h - 2, wd / 2), 0.55));
                    o.Add(Seg(FenceInk.ConcreteSeam, p.P(xa + 1, 2, wd / 2), p.P(xa + 1, h - 2, wd / 2), 0.55));
                }
                else
                {
                    // 벽돌 쌓기(엇갈림 · 줄눈 · 색 흔들림) — 작은 배율에서는 바탕 무늬만
                    o.AddRange(FenceStyleArt.BrickCourses(p, xa, xb, h, wd / 2, zoom, panel.Index, color));
                }
                break;
            }
            case EnumFenceStyle.DesignFence:
            {
                // 3D 용접망 — 세로 철선 + 가로로 앞으로 꺾인 V 접힘(사진: 초록 용접망 · 각기둥 · 클램프)
                o.AddRange(FenceStyleArt.DesignMesh(p, a, b, h, color));
                break;
            }
            default:
            {
                o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.Mesh, new[] { p.P(a, 8, 0), p.P(b, 8, 0), p.P(b, h - 8, 0), p.P(a, h - 8, 0) }, Color: color));
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.Rail, new[] { p.P(xa, h - 8, 0), p.P(xb, h - 8, 0) }, Color: color));
                o.Add(new FenceShape(FenceShapeKind.Line, FenceInk.Rail, new[] { p.P(xa, 10, 0), p.P(xb, 10, 0) }, Color: color));
                if (spec.Style == EnumFenceStyle.ChainLinkRazor && zoom < RAZOR_BAND_ZOOM)
                {
                    // 작은 배율 — 코일(가는 선 · 드문드문)은 거의 보이지 않는다(검토 V2). 톱니 띠 하나로 줄여 모양을 읽히게 한다.
                    o.Add(new FenceShape(FenceShapeKind.Polygon, FenceInk.RazorBand, RazorBand(p, xa, xb, h, zoom), Opacity: 0.75));
                }
                else if (spec.Style == EnumFenceStyle.ChainLinkRazor)
                {
                    // 콘서티나 코일 — Y 받침 안에 얹힌 겹친 고리 + 가시 + 팔 끝 철선(사진: 윤형철조망). 고리는 칸마다 그림 하나로 묶는다(NFR-02).
                    o.AddRange(FenceStyleArt.RazorCoil(p, xa, xb, h + FenceStyleArt.COIL_SEAT_GAP, h));
                }
                break;
            }
        }
    }

    /// <summary>
    /// 케이블 보기(두 줄 형상 · v0.3 §1-0b) — 제어기(펜스 끝 바깥)에서 Ch1 이 아래 줄을 따라 먼 끝까지, 먼 끝에서 위로 꺾여 위 줄(센서가 있으면 그 칩 높이)을
    /// 제어기 쪽으로 돌아와 Ch2 로 들어간다. 위 줄에 센서가 없거나 센서가 끝난 뒤의 구간은 리턴케이블(2겹 선). 칩마다 Ch1 · Ch2 에서 센 번호 알약.
    /// </summary>
    private static void LaneCables(List<FenceShape> o, FenceWorld world, FenceProjector p)
    {
        var layout = world.Layout;
        var geometry = world.Geometry;
        if (layout is null || geometry is null) return;
        var keys = world.Seq;
        var u = world.Upm;
        var left = layout.ControllerEnd == FenceControllerEnd.Left;
        var cz = p.De / 2 + 6 * p.K;
        const double LOWER_Y = 41;
        var xs = keys.Select(k => world.X[k]).Append(0).Append(geometry.LengthM * u).ToList();
        var nearX = world.ControllerX;
        var farX = left ? xs.Max() + 18 : xs.Min() - 18;
        var upper = keys.Where(k => layout.LaneOf(k) == FenceLane.Upper).ToList();
        var lower = keys.Where(k => layout.LaneOf(k) == FenceLane.Lower).ToList();
        var fenceTop = geometry.Panels.Count == 0 ? FenceProjector.H : geometry.Panels.Max(q => q.Spec.HeightM) * world.Vpm;
        var upperY = upper.Count > 0 ? upper.Average(world.BodyCenterOf) : fenceTop + 14;
        var lowerY = lower.Count > 0 ? Math.Min(LOWER_Y, lower.Min(world.BodyCenterOf)) : LOWER_Y;

        // Ch1 — 제어기 → 아래 줄 → 먼 끝
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(nearX, 12, cz), p.P(nearX, lowerY, cz), p.P(farX, lowerY, cz) }));
        // 먼 끝 꺾임 + 위 줄 센서 구간(사슬) · 그 뒤 제어기까지 리턴(2겹)
        var nearUpper = upper.Count > 0 ? (left ? upper.Min(k => world.X[k]) : upper.Max(k => world.X[k])) : farX;
        if (upper.Count > 0)
            o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.Chain, new[] { p.P(farX, lowerY, cz), p.P(farX, upperY, cz), p.P(nearUpper, upperY, cz) }));
        var back = upper.Count > 0
            ? new[] { p.P(nearUpper, upperY, cz), p.P(nearX, upperY, cz), p.P(nearX, 22, cz) }
            : new[] { p.P(farX, lowerY, cz), p.P(farX, upperY, cz), p.P(nearX, upperY, cz), p.P(nearX, 22, cz) };
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ReturnOuter, back));
        o.Add(new FenceShape(FenceShapeKind.Polyline, FenceInk.ReturnInner, back));

        var anchor = left ? FenceTextAnchor.Start : FenceTextAnchor.End;
        var dx = left ? 10 : -10;
        var a = p.P(nearX, lowerY, cz);
        var b = p.P(nearX, upperY, cz);
        o.Add(Text(FenceInk.LabelChain, new Point(a.X + dx, a.Y + 14), $"{Model.WiringValidation.PORT_1} ▶ 아래 줄", 11, anchor));
        o.Add(Text(FenceInk.LabelReturn, new Point(b.X + dx, b.Y - 6), upper.Count > 0 ? $"{Model.WiringValidation.PORT_2} ◀ 위 줄" : $"{Model.WiringValidation.PORT_2} ◀ 리턴선", 11, anchor));

        // Ch1 · Ch2 에서 센 번호 알약 — 칩 아래(그 줄 높이)
        foreach (var key in keys)
        {
            if (!world.Sensors.TryGetValue(key, out var s) || s.PortText.Length == 0) continue;
            var y = layout.LaneOf(key) == FenceLane.Upper ? upperY - 30 : lowerY - 24;
            o.Add(new FenceShape(FenceShapeKind.Pill, FenceInk.PillPort, new[] { p.P(world.X[key], y, cz) }, Text: s.PortText, FontSize: 10));
        }
    }

    /// <summary>이 줌보다 작으면 윤형 코일 대신 톱니 띠(<see cref="RazorBand"/>).</summary>
    public const double RAZOR_BAND_ZOOM = 0.6;

    /// <summary>
    /// 작은 배율의 윤형 철조망 — 망 위 코일 자리를 덮는 톱니 띠(아래는 곧은 선 · 위는 톱니). 톱니 간격은 화면 약 12px(줌에 맞서 넓힌다) · 높이는 코일 지름.
    /// </summary>
    internal static Point[] RazorBand(FenceProjector p, double xa, double xb, double h, double zoom)
    {
        var low = h + 2;
        var high = h + 2 * RAZOR_R + 2;
        var step = Math.Max(RAZOR_R, 12 / Math.Max(0.05, zoom));
        var top = new List<Point>();
        var up = true;
        for (var x = xa; x < xb; x += step / 2, up = !up) top.Add(p.P(x, up ? high : low + RAZOR_R * 0.6, 0));
        top.Add(p.P(xb, high, 0));
        return top.Append(p.P(xb, low, 0)).Append(p.P(xa, low, 0)).ToArray();
    }

    /// <summary>
    /// 망 칩 그림(FR-04) — 칩 원점 = 망의 A 쪽 끝. 적중 = 망 앞면을 담는 사각형. 고르면 테두리 + 옅은 칠(색이 아니라 형태).
    /// </summary>
    public static FenceChipPicture PanelChip(FencePanelGeometry panel, FenceWorld world, FenceProjector p, bool selected)
    {
        var w = panel.SpanM * world.Upm;
        var h = panel.Spec.HeightM * world.Vpm + (panel.Spec.IsWall ? 7 : 0);
        var z = panel.Spec.IsWall ? WALL_DEPTH * Math.Max(0.25, p.K) / 2 : 0;
        var face = new[] { p.P(0, 0, z), p.P(w, 0, z), p.P(w, h, z), p.P(0, h, z) };
        var hit = new Rect(new Point(face.Min(q => q.X), face.Min(q => q.Y)), new Point(face.Max(q => q.X), face.Max(q => q.Y)));
        var o = new List<FenceShape>(3) { RectShape(FenceInk.Hit, hit, 0) };
        if (selected)
        {
            o.Add(Poly(FenceInk.PanelSelectFill, face));
            o.Add(Poly(FenceInk.PanelSelectEdge, face));
        }
        return new FenceChipPicture(o, hit);
    }

    /// <summary>
    /// 모양 견본(속성 칸의 판 종류 단추 · SB B) — (0,0)~(w,h) 안에 평면으로 한 칸.
    /// </summary>
    public static IReadOnlyList<FenceShape> Swatch(EnumFenceStyle style, double w, double h, string? color = null)
    {
        var o = new List<FenceShape>(12);
        var flat = FenceProjector.Flat;
        var u = 1.0;
        var spec = new FencePanelSpec(style, color, 1, w);
        // 견본은 세계 단위 = 화면 px. 높이 h 는 0.78 이 망, 나머지는 코일 · 갓돌 자리.
        var bodyH = style == EnumFenceStyle.ChainLinkRazor ? h * 0.68 : h * 0.82;
        var geometry = new FencePanelGeometry(0, 0, w, spec with { HeightM = bodyH });
        var v = 1.0;
        var list = new List<FenceShape>();
        DrawPanel(list, flat, geometry, u, v, !spec.IsWall, !spec.IsWall, 1);
        // 투영 y 는 위가 − — 견본 칸 안으로 옮긴다(아래 끝 = h − 2).
        var dy = h - 2;
        // 큰 코일 · 가시 · 철선은 견본(수십 px)에 넘친다 — 견본은 아래 작은 고리로 말한다
        foreach (var shape in list.Where(s => s.Kind != FenceShapeKind.Ellipse && s.Ink is not (FenceInk.RazorCoil or FenceInk.RazorBarb or FenceInk.RazorStrand)))
            o.Add(shape with { Points = shape.Points.Select(q => new Point(q.X, q.Y + dy)).ToArray() });
        if (style == EnumFenceStyle.ChainLinkRazor)
            for (var x = 4.0; x <= w - 3.5; x += 7)
                o.Add(new FenceShape(FenceShapeKind.Ellipse, FenceInk.Razor, new[] { new Point(x, dy - bodyH - 4) }, 3.5, 3.5));
        if (!spec.IsWall)
            foreach (var x in new[] { 1.5, w - 1.5 })
                o.Add(new FenceShape(FenceShapeKind.Rect, style == EnumFenceStyle.DesignFence ? FenceInk.DesignPost : FenceInk.PostFront,
                                     new[] { new Point(x - 1.5, dy - bodyH - 2), new Point(x + 1.5, dy) }, Color: style == EnumFenceStyle.DesignFence ? color : null));
        return o;
    }

    /// <summary>러버밴드(선택 사각형) — 화면 좌표 사각형 하나, 점선 {5,3}(이 앱의 영역 선택 어휘).</summary>
    public static FenceShape RubberBand(Rect screen)
        => new(FenceShapeKind.Rect, FenceInk.RubberBand, new[] { screen.TopLeft, screen.BottomRight });

    /// <summary><see cref="Box"/> 와 같되 사람이 고른 색을 싣는다(앞 · 옆 · 윗면 모두 — 렌더러가 옆은 어둡게, 윗면은 밝게 섞는다).</summary>
    private static void BoxC(List<FenceShape> o, FenceProjector p, double x, double y0, double y1, double z0, double z1, double w,
                             FenceInk front, FenceInk side, FenceInk top, string? color)
    {
        var xa = x - w / 2;
        var xb = x + w / 2;
        if (z1 - z0 > 0.4)
        {
            o.Add(new FenceShape(FenceShapeKind.Polygon, side, new[] { p.P(xb, y0, z1), p.P(xb, y0, z0), p.P(xb, y1, z0), p.P(xb, y1, z1) }, Color: color));
            o.Add(new FenceShape(FenceShapeKind.Polygon, top, new[] { p.P(xa, y1, z1), p.P(xb, y1, z1), p.P(xb, y1, z0), p.P(xa, y1, z0) }, Color: color));
        }
        o.Add(new FenceShape(FenceShapeKind.Polygon, front, new[] { p.P(xa, y0, z1), p.P(xb, y0, z1), p.P(xb, y1, z1), p.P(xa, y1, z1) }, Color: color));
    }
}
