using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 직접 그린 건물(「건물 그리기」) 순수 함수 · 그리기 상태 기계 — 점 추가 · 닫기(첫 점 · 더블클릭 · Close) · 꼭짓점 끌기 · 선분 가운데 점 넣기 ·
/// 오른쪽 클릭/Delete/Backspace · 자기 교차 · 3점 미만 · 영역 밖 · Esc 두 단계 · 되돌리기 · 회전된 지도(화면 ↔ 위경도) · 요청 본문 ·
/// 미리보기 메시지 · 응답 해석 · map-3d 창구(가짜 처리기) · 출처 글.
/// </summary>
public class Map3DUserBuildingTests
{
    // 지도 흉내 — 위경도 → 화면(1° = 100000 px, 위가 북), 회전(시계 rotDeg)도 넣을 수 있다
    private const double Lon0 = 127.0, Lat0 = 37.5, K = 100000.0;

    private static (Func<UbPoint, UbPoint> ToScreen, Func<UbPoint, UbPoint> ToGeo) Projection(double rotDeg = 0)
    {
        double r = rotDeg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        UbPoint ToScreen(UbPoint g)
        {
            double x = (g.X - Lon0) * K, y = -(g.Y - Lat0) * K;
            return new UbPoint(500 + x * c - y * s, 400 + x * s + y * c);
        }
        UbPoint ToGeo(UbPoint p)
        {
            double x = p.X - 500, y = p.Y - 400;
            double ux = x * c + y * s, uy = -x * s + y * c;
            return new UbPoint(Lon0 + ux / K, Lat0 - uy / K);
        }
        return (ToScreen, ToGeo);
    }

    private static Map3DUserBuildingDraft Draft(double rot = 0, Func<UbPoint, bool>? inside = null)
    {
        var (ts, tg) = Projection(rot);
        return new Map3DUserBuildingDraft(ts, tg, inside);
    }

    /// <summary>화면 클릭 한 번(누르기 → 그 자리 놓기).</summary>
    private static void Click(Map3DUserBuildingDraft d, double x, double y)
    {
        d.PointerDown(new UbPoint(x, y));
        d.PointerUp(new UbPoint(x, y));
    }

    private static void Drag(Map3DUserBuildingDraft d, UbPoint from, UbPoint to)
    {
        d.PointerDown(from);
        d.PointerMove(new UbPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        d.PointerMove(to);
        d.PointerUp(to);
    }

    /// <summary>화면 100 px 정사각형(약 9 × 11 m) — 시계 방향으로 찍는다.</summary>
    private static Map3DUserBuildingDraft Square(double rot = 0, Func<UbPoint, bool>? inside = null)
    {
        var d = Draft(rot, inside);
        Click(d, 400, 300); Click(d, 500, 300); Click(d, 500, 400); Click(d, 400, 400);
        return d;
    }

    // ── 점 추가 · 닫기 ──

    [Fact]
    public void should_add_points_and_close_by_clicking_first_point()
    {
        var d = Square();
        Assert.Equal(4, d.Points.Count);
        Assert.False(d.IsClosed);
        Assert.Equal("꼭짓점 4개 — 첫 점을 누르거나 Enter 로 닫기", Map3DUserBuilding.PointCountText(4, false));
        Click(d, 402, 298);                                         // 첫 점(손잡이 반지름 안) → 닫기, 점은 늘지 않는다
        Assert.True(d.IsClosed);
        Assert.Equal(4, d.Points.Count);
        Assert.Null(d.Validate());
        Assert.Equal(new UbPoint(127.0 - 0.001, 37.5 + 0.001), Map3DUserBuilding.Round(d.Points[0]));
        Click(d, 450, 350);                                         // 닫힌 뒤 빈 곳 클릭 = 아무것도 아님
        Assert.Equal(4, d.Points.Count);
    }

    [Fact]
    public void should_refuse_close_with_fewer_than_three_points_and_close_by_double_click()
    {
        var d = Draft();
        Click(d, 400, 300); Click(d, 500, 300);
        Assert.False(d.Close());
        Assert.Equal(Map3DUserBuilding.TooFewText, d.LastError);
        Assert.False(d.DoubleClick());
        Click(d, 450, 400);
        Assert.Null(d.LastError);                                   // 다음 성공한 편집에서 이유를 지운다
        Assert.True(d.DoubleClick());
        Assert.True(d.IsClosed);
        Assert.Equal("꼭짓점 3개 · 닫힘", Map3DUserBuilding.PointCountText(3, true));
        Assert.Equal("꼭짓점 2개 — 3개 이상 찍으세요", Map3DUserBuilding.PointCountText(2, false));
        Assert.Equal("지도를 눌러 첫 꼭짓점을 찍으세요", Map3DUserBuilding.PointCountText(0, false));
    }

    [Fact]
    public void should_not_stack_point_when_clicking_existing_vertex_but_select_it()
    {
        var d = Square();
        Click(d, 501, 301);                                         // 둘째 꼭짓점 위 클릭 = 고르기만
        Assert.Equal(4, d.Points.Count);
        Assert.Equal(1, d.SelectedVertex);
    }

    // ── 자기 교차 · 영역 밖 ──

    [Fact]
    public void should_refuse_point_that_crosses_earlier_edge_and_close_that_crosses()
    {
        var d = Draft();
        Click(d, 400, 300); Click(d, 500, 300); Click(d, 400, 400);
        Click(d, 500, 400);                                         // 3→4 는 괜찮다(닫으면 나비넥타이)
        Assert.Equal(4, d.Points.Count);
        Assert.False(d.Close());                                    // 4→1 닫는 선이 2→3 과 엇갈림
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Click(d, 450, 250);                                         // 4→새 점이 1→2 를 가로지름
        Assert.Equal(4, d.Points.Count);
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Assert.True(Map3DUserBuilding.IsSelfIntersecting(d.Points, closed: true));
        Assert.False(Map3DUserBuilding.IsSelfIntersecting(d.Points, closed: false));
    }

    [Fact]
    public void should_refuse_points_outside_area()
    {
        var d = Draft(inside: g => g.X < 127.0005);                // 화면 x 550 너머는 영역 밖
        Click(d, 400, 300); Click(d, 600, 300);
        Assert.Single(d.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, d.LastError);
    }

    [Fact]
    public void should_restore_vertex_when_drag_makes_cross_or_leaves_area()
    {
        var d = Square(inside: g => g.X < 127.0005);
        Assert.True(d.Close());
        var before = d.Points.ToArray();
        Drag(d, new UbPoint(400, 300), new UbPoint(450, 450));      // 첫 점을 반대편 아래로 → 엇갈림
        Assert.Equal(before, d.Points);
        Assert.Equal(Map3DUserBuilding.CrossText, d.LastError);
        Drag(d, new UbPoint(500, 300), new UbPoint(580, 300));      // 영역 밖
        Assert.Equal(before, d.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, d.LastError);
    }

    [Fact]
    public void should_validate_too_small_outline_and_too_many_points()
    {
        var tiny = new[] { new UbPoint(127, 37.5), new UbPoint(127.00001, 37.5), new UbPoint(127.00001, 37.50001) };
        Assert.Equal(Map3DUserBuilding.TooSmallText, Map3DUserBuilding.Validate(tiny));
        var many = Enumerable.Range(0, 201).Select(i => new UbPoint(127 + Math.Cos(i * 0.031) * 0.001, 37.5 + Math.Sin(i * 0.031) * 0.001)).ToList();
        Assert.Equal(Map3DUserBuilding.TooManyText, Map3DUserBuilding.Validate(many));
        var square = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002), new UbPoint(127, 37.5002) };
        Assert.Null(Map3DUserBuilding.Validate(square));
        Assert.Equal(Map3DUserBuilding.OutsideText, Map3DUserBuilding.Validate(square, g => g.X < 127.0002));
        Assert.InRange(Map3DUserBuilding.AreaSquareMeters(square), 26.5 * 22.1 * 0.98, 26.5 * 22.1 * 1.02);
        Assert.True(Map3DUserBuilding.Contains(square, new UbPoint(127.0001, 37.5001)));
        Assert.False(Map3DUserBuilding.Contains(square, new UbPoint(127.0004, 37.5001)));
        var dup = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127, 37.5) };
        Assert.True(Map3DUserBuilding.IsSelfIntersecting(dup, closed: true));   // 같은 자리 점
    }

    // ── 끌기 · 점 넣기 · 지우기 ──

    [Fact]
    public void should_move_vertex_by_drag_and_insert_vertex_by_midpoint_drag()
    {
        var d = Square();
        d.Close();
        Assert.Equal(4, d.MidpointsScreen().Count());
        Drag(d, new UbPoint(500, 400), new UbPoint(520, 420));      // 셋째 꼭짓점 끌기
        Assert.Equal(4, d.Points.Count);
        var (ts, _) = Projection();
        var moved = ts(d.Points[2]);
        Assert.Equal(520, moved.X, 0);
        Assert.Equal(420, moved.Y, 0);
        Drag(d, new UbPoint(450, 300), new UbPoint(450, 260));      // 첫 선분 가운데 손잡이 → 꼭짓점 1 자리에 새 점
        Assert.Equal(5, d.Points.Count);
        Assert.Equal(260, ts(d.Points[1]).Y, 0);
        Assert.Null(d.Validate());
        // 데드존 안(4 px) 움직임은 끌기가 아니다
        var snap = d.Points.ToArray();
        d.PointerDown(new UbPoint(400, 300)); d.PointerMove(new UbPoint(402, 302)); d.PointerUp(new UbPoint(402, 302));
        Assert.Equal(snap, d.Points);
        // 윤곽 밖 빈 곳 끌기는 아무것도 안 한다(윤곽 안 끌기 = 통째로 옮기기, 아래 시험)
        Drag(d, new UbPoint(700, 700), new UbPoint(730, 720));
        Assert.Equal(snap, d.Points);
    }

    [Fact]
    public void should_delete_by_right_click_delete_and_backspace_and_reopen_below_three()
    {
        var d = Square();
        d.Close();
        Assert.True(d.RightClick(new UbPoint(500, 300)));          // 오른쪽 클릭 = 지우기
        Assert.Equal(3, d.Points.Count);
        Assert.True(d.IsClosed);
        Assert.False(d.RightClick(new UbPoint(450, 450)));         // 꼭짓점 아님
        d.PointerDown(new UbPoint(400, 400)); d.PointerUp(new UbPoint(400, 400));   // 고르기
        Assert.True(d.RemoveSelected());                           // Delete
        Assert.Equal(2, d.Points.Count);
        Assert.False(d.IsClosed);                                  // 3점 밑 → 다시 열림
        Assert.True(d.RemoveLast());                               // Backspace = 마지막 점
        Assert.Single(d.Points);
        Assert.False(d.RemoveSelected());                          // 고른 점 없음
    }

    [Fact]
    public void should_cancel_drag_then_clear_points_then_exit_on_escape()
    {
        var d = Square();
        d.Close();
        var before = d.Points.ToArray();
        d.PointerDown(new UbPoint(400, 300));
        d.PointerMove(new UbPoint(430, 330));
        Assert.True(d.IsDragging);
        Assert.Equal(UbEscapeResult.CanceledDrag, d.Escape());    // 1) 끄는 중 → 끌기 전으로
        Assert.Equal(before, d.Points);
        var open = Draft();
        Click(open, 400, 300); Click(open, 500, 300);
        Assert.Equal(UbEscapeResult.ClearedPoints, open.Escape()); // 2) 그리던 점 지움
        Assert.Empty(open.Points);
        Assert.Equal(UbEscapeResult.Exit, open.Escape());           // 3) 빈 윤곽 → 끝내기
        Assert.True(open.Undo());                                   // 지운 점도 되돌린다
        Assert.Equal(2, open.Points.Count);
    }

    [Fact]
    public void should_undo_each_step_in_reverse_order()
    {
        var d = Square();
        d.Close();
        Drag(d, new UbPoint(500, 400), new UbPoint(520, 420));
        Assert.True(d.Undo());                                      // 끌기 되돌림
        Assert.Equal(4, d.Points.Count);
        Assert.True(d.IsClosed);
        Assert.True(d.Undo());                                      // 닫기 되돌림
        Assert.False(d.IsClosed);
        for (int i = 0; i < 4; i++) Assert.True(d.Undo());
        Assert.Empty(d.Points);
        Assert.False(d.Undo());
        Assert.False(d.CanUndo);
    }

    [Fact]
    public void should_load_saved_outline_closed_without_undo()
    {
        var d = Draft();
        d.Load(new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) });
        Assert.True(d.IsClosed);
        Assert.False(d.CanUndo);
        Assert.Null(d.Validate());
    }

    // ── 회전된 지도 ──

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(-135)]
    public void should_draw_same_geo_outline_on_rotated_map(double rot)
    {
        var (ts, _) = Projection(rot);
        var geo = new[] { new UbPoint(126.9995, 37.5005), new UbPoint(127.0005, 37.5005), new UbPoint(127.0005, 37.4995), new UbPoint(126.9995, 37.4995) };
        var d = Draft(rot);
        foreach (var g in geo) { var s = ts(g); Click(d, s.X, s.Y); }
        var first = ts(geo[0]);
        Click(d, first.X + 1, first.Y - 1);                         // 회전된 화면에서도 첫 점 = 닫기
        Assert.True(d.IsClosed);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(geo[i].X, d.Points[i].X, 7);
            Assert.Equal(geo[i].Y, d.Points[i].Y, 7);
        }
        // 회전된 화면에서 끌기 — 화면 끌기 거리만큼 위경도가 회전된 방향으로 움직인다
        var p1 = ts(geo[1]);
        Drag(d, p1, new UbPoint(p1.X + 30, p1.Y));
        Assert.Equal(p1.X + 30, ts(d.Points[1]).X, 6);
        Assert.Equal(p1.Y, ts(d.Points[1]).Y, 6);
    }

    // ── 형상 도구 ──

    private static Map3DUserBuildingDraft ToolDraft(UbTool tool, double rot = 0)
    {
        var d = Draft(rot);
        Assert.True(d.SetTool(tool));
        return d;
    }

    private static List<UbPoint> ScreenOf(Map3DUserBuildingDraft d, double rot = 0)
    {
        var (ts, _) = Projection(rot);
        return d.Points.Select(ts).ToList();
    }

    [Fact]
    public void should_make_rectangle_by_drag_and_square_with_shift_and_by_two_clicks()
    {
        var d = ToolDraft(UbTool.Rect);
        d.PointerDown(new UbPoint(400, 300));
        d.PointerMove(new UbPoint(450, 330));
        Assert.True(d.IsDragging);
        Assert.Equal(4, d.Preview.Count);                                 // 끄는 동안 미리보기만
        Assert.Empty(d.Points);
        d.PointerUp(new UbPoint(500, 340));
        Assert.True(d.IsClosed);
        Assert.Equal(UbTool.Rect, d.CompletedBy);
        var s = ScreenOf(d);
        Assert.Equal(new[] { (400.0, 300.0), (500.0, 300.0), (500.0, 340.0), (400.0, 340.0) },
                     s.Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))));
        Assert.Empty(d.Preview);

        var sq = ToolDraft(UbTool.Rect);
        sq.PointerDown(new UbPoint(400, 300));
        sq.PointerMove(new UbPoint(420, 320), shift: true);
        sq.PointerUp(new UbPoint(480, 320), shift: true);                  // Shift = 긴 변(80)에 맞춘 정사각형
        var q = ScreenOf(sq);
        Assert.Equal(80, q[1].X - q[0].X, 6);
        Assert.Equal(80, q[2].Y - q[1].Y, 6);

        var clicks = ToolDraft(UbTool.Rect);
        Click(clicks, 400, 300);
        Assert.Single(clicks.Anchors);
        Assert.False(clicks.IsClosed);
        clicks.PointerMove(new UbPoint(450, 380));
        Assert.Equal(4, clicks.Preview.Count);                            // 두 번째 클릭 전 미리보기
        Click(clicks, 460, 390);
        Assert.True(clicks.IsClosed);
        Assert.Empty(clicks.Anchors);
    }

    [Fact]
    public void should_make_screen_aligned_rectangle_on_rotated_map()
    {
        var d = ToolDraft(UbTool.Rect, rot: 40);
        Drag(d, new UbPoint(400, 300), new UbPoint(520, 380));
        var s = ScreenOf(d, rot: 40);
        Assert.Equal(s[0].Y, s[1].Y, 6);                                  // 화면에서 반듯(위경도로는 40° 돌아간 사각형)
        Assert.Equal(s[1].X, s[2].X, 6);
        var g = d.Points;
        Assert.NotEqual(Math.Round(g[0].Y, 9), Math.Round(g[1].Y, 9));
        Assert.Null(d.Validate());
    }

    [Theory]
    [InlineData(32)]
    [InlineData(8)]
    [InlineData(128)]
    public void should_approximate_circle_with_configured_vertex_count_and_radius(int sides)
    {
        var d = ToolDraft(UbTool.Circle);
        d.CircleSides = sides;
        Drag(d, new UbPoint(500, 400), new UbPoint(560, 400));
        Assert.True(d.IsClosed);
        Assert.Equal(sides, d.Points.Count);
        Assert.Equal(UbTool.Circle, d.CompletedBy);
        foreach (var p in ScreenOf(d)) Assert.Equal(60, Math.Sqrt((p.X - 500) * (p.X - 500) + (p.Y - 400) * (p.Y - 400)), 6);
        var c = Draft();
        c.CircleSides = 3;
        Assert.Equal(Map3DUserBuilding.MinCircleSides, c.CircleSides);   // 범위로
        c.CircleSides = 1000;
        Assert.Equal(Map3DUserBuilding.MaxCircleSides, c.CircleSides);
        // 두 번 클릭 = 가운데 · 가장자리
        var two = ToolDraft(UbTool.Circle);
        Click(two, 500, 400);
        Click(two, 500, 450);
        Assert.Equal(Map3DUserBuilding.DefaultCircleSides, two.Points.Count);
        Assert.Equal(50, Math.Abs(ScreenOf(two)[0].X - 500), 6);
    }

    [Fact]
    public void should_make_ellipse_by_drag_and_by_three_clicks_with_rotation()
    {
        var d = ToolDraft(UbTool.Ellipse);
        Drag(d, new UbPoint(500, 400), new UbPoint(580, 430));            // 가로 80 · 세로 30
        var s = ScreenOf(d);
        Assert.Equal(80, s.Max(p => p.X) - 500, 6);
        Assert.Equal(30, s.Max(p => p.Y) - 400, 6);

        var three = ToolDraft(UbTool.Ellipse);
        Click(three, 500, 400);                                           // 가운데
        Click(three, 560, 460);                                           // 긴 축 끝(45°, 반지름 84.85)
        Assert.False(three.IsClosed);
        three.PointerMove(new UbPoint(480, 420));
        Assert.NotEmpty(three.Preview);
        Click(three, 480, 420);                                           // 짧은 축 쪽 — 긴 축 선에서 28.28 떨어짐
        Assert.True(three.IsClosed);
        var e = ScreenOf(three);
        Assert.Equal(60, e[0].X - 500, 6);                                // 첫 꼭짓점 = 긴 축 끝
        Assert.Equal(60, e[0].Y - 400, 6);
        double minR = e.Min(p => Math.Sqrt((p.X - 500) * (p.X - 500) + (p.Y - 400) * (p.Y - 400)));
        Assert.Equal(Math.Sqrt(800), minR, 1);
        // Shift 끌기 = 원
        var circle = ToolDraft(UbTool.Ellipse);
        circle.PointerDown(new UbPoint(500, 400));
        circle.PointerMove(new UbPoint(530, 400), shift: true);
        circle.PointerUp(new UbPoint(540, 410), shift: true);
        foreach (var p in ScreenOf(circle)) Assert.Equal(Math.Sqrt(1700), Math.Sqrt((p.X - 500) * (p.X - 500) + (p.Y - 400) * (p.Y - 400)), 6);
    }

    [Fact]
    public void should_refuse_too_small_or_outside_shapes_and_cancel_shape_with_escape()
    {
        var tiny = ToolDraft(UbTool.Circle);
        Click(tiny, 500, 400); Click(tiny, 501, 400);                     // 반지름 1 px(약 1 m) — 넓이 4 m² 미만
        Assert.False(tiny.IsClosed);
        Assert.Equal(Map3DUserBuilding.TooSmallText, tiny.LastError);
        var (ts, tg) = Projection();
        var outside = new Map3DUserBuildingDraft(ts, tg, g => g.X < 127.0005);
        outside.SetTool(UbTool.Rect);
        Drag(outside, new UbPoint(500, 300), new UbPoint(600, 400));
        Assert.False(outside.IsClosed);
        Assert.Equal(Map3DUserBuilding.OutsideText, outside.LastError);
        var esc = ToolDraft(UbTool.Rect);
        esc.PointerDown(new UbPoint(400, 300));
        esc.PointerMove(new UbPoint(450, 350));
        Assert.Equal(UbEscapeResult.CanceledDrag, esc.Escape());          // 끄는 중 → 미리보기 버림
        Assert.Empty(esc.Preview);
        Click(esc, 400, 300);
        Assert.True(esc.RemoveLast());                                    // Backspace = 마지막 클릭
        Assert.Empty(esc.Anchors);
        Click(esc, 400, 300);
        Assert.Equal(UbEscapeResult.ClearedPoints, esc.Escape());
        Assert.Equal(UbEscapeResult.Exit, esc.Escape());
    }

    [Fact]
    public void should_change_tool_only_on_empty_outline()
    {
        var d = Draft();
        Click(d, 400, 300);
        Assert.False(d.SetTool(UbTool.Circle));
        Assert.Equal(UbTool.Polygon, d.Tool);
        var c = ToolDraft(UbTool.Circle);
        Assert.False(c.DoubleClick());                                    // 형상 도구는 두 번 누르기로 닫지 않는다
    }

    [Fact]
    public void should_close_polygon_within_snap_distance_of_first_point()
    {
        var d = Square();
        Click(d, 409, 300);                                               // 손잡이 반지름(7) 밖 · 붙는 거리(10) 안
        Assert.True(d.IsClosed);
        Assert.Equal(4, d.Points.Count);
        var far = Square();
        Click(far, 390, 290);                                             // 14 px — 닫지 않고 점을 더한다(엇갈림 없음)
        Assert.False(far.IsClosed);
        Assert.Equal(5, far.Points.Count);
    }

    [Fact]
    public void should_snap_next_point_to_right_angle_with_shift()
    {
        var snapped = Map3DUserBuilding.SnapAngle(new UbPoint(400, 300), new UbPoint(500, 300), new UbPoint(506, 390));
        Assert.Equal(500, snapped.X, 6);                                  // 앞 변(가로) 기준 90°
        Assert.Equal(300 + Math.Sqrt(36 + 8100), snapped.Y, 6);           // 길이는 그대로
        var diag = Map3DUserBuilding.SnapAngle(null, new UbPoint(0, 0), new UbPoint(50, 45));
        Assert.Equal(diag.X, diag.Y, 6);                                  // 점 하나면 화면 가로 기준 45°
        var tilted = Map3DUserBuilding.SnapAngle(new UbPoint(0, 0), new UbPoint(30, 40), new UbPoint(30 - 40 + 3, 40 + 30 + 1));
        double dot = (30 - 0) * (tilted.X - 30) + (40 - 0) * (tilted.Y - 40);
        Assert.Equal(0, dot, 6);                                          // 기울어진 앞 변에 직각
        var d = Draft();
        Click(d, 400, 300); Click(d, 500, 300);
        d.PointerDown(new UbPoint(506, 390));
        d.PointerUp(new UbPoint(506, 390), shift: true);
        var (ts, _) = Projection();
        Assert.Equal(500, ts(d.Points[2]).X, 6);
    }

    [Fact]
    public void should_move_whole_outline_by_body_drag_and_rotate_by_handle()
    {
        var d = Square();
        d.Close();
        Assert.Equal(UbHandleKind.Body, d.HitTest(new UbPoint(450, 350)).Kind);
        Drag(d, new UbPoint(450, 350), new UbPoint(470, 330));            // 통째로 (+20, −20)
        var s = ScreenOf(d);
        Assert.Equal(420, s[0].X, 6);
        Assert.Equal(280, s[0].Y, 6);
        var rot = d.RotateHandleScreen()!.Value;
        Assert.Equal(470, rot.X, 6);                                      // 가운데 위
        Assert.Equal(280 - Map3DUserBuilding.RotateHandleOffsetPx, rot.Y, 6);
        Assert.Equal(UbHandleKind.Rotate, d.HitTest(rot).Kind);
        // 가운데(470, 330) 기준 위(−90°) → 오른쪽(0°) = 시계 90°
        Drag(d, rot, new UbPoint(470 + 80, 330));
        var r = ScreenOf(d);
        Assert.Equal(520, r[0].X, 6);                                     // (420,280) → (520,280)
        Assert.Equal(280, r[0].Y, 6);
        Assert.True(d.Undo());                                            // 회전 되돌림
        Assert.Equal(420, ScreenOf(d)[0].X, 6);
        Assert.Null(Draft().RotateHandleScreen());
    }

    [Fact]
    public void should_restore_when_body_drag_leaves_area()
    {
        var d = Square(inside: g => g.X < 127.0005);
        d.Close();
        var before = d.Points.ToArray();
        Drag(d, new UbPoint(450, 350), new UbPoint(560, 350));
        Assert.Equal(before, d.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, d.LastError);
    }

    // ── 서버 · 3D 메시지 ──

    [Fact]
    public void should_build_put_body_and_preview_messages()
    {
        var outline = new[] { new UbPoint(127.123456789, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) };
        var body = Map3DUserBuilding.PutBody(outline, 18.04, 6, "  경비동 ");
        Assert.Equal(127.1234568, body["outline"]![0]![0]!.Value<double>());
        Assert.Equal(3, ((JArray)body["outline"]!).Count);
        Assert.Equal(18.0, body.Value<double>("height"));
        Assert.Equal(6, body.Value<int>("floors"));
        Assert.Equal("경비동", body.Value<string>("name"));
        var noName = Map3DUserBuilding.PutBody(outline, 900, null, "  ");
        Assert.Equal(600.0, noName.Value<double>("height"));         // 1~600 으로
        Assert.Equal(JTokenType.Null, noName["floors"]!.Type);
        Assert.Equal(JTokenType.Null, noName["name"]!.Type);
        Assert.Equal(60, Map3DUserBuilding.PutBody(outline, 9, null, new string('가', 80)).Value<string>("name")!.Length);
        Assert.Equal(("flat", "auto"), (noName.Value<string>("roof"), noName.Value<string>("use")));
        var dome = Map3DUserBuilding.PutBody(outline, 9, null, null, "dome", "industrial");
        Assert.Equal(("dome", "industrial"), (dome.Value<string>("roof"), dome.Value<string>("use")));
        var odd = Map3DUserBuilding.PutBody(outline, 9, null, null, "pyramid", "factory");
        Assert.Equal(("flat", "auto"), (odd.Value<string>("roof"), odd.Value<string>("use")));   // 모르는 값은 기본

        var prev = JObject.Parse(Map3DUserBuilding.PreviewMessageJson("a1b2c3d4e5f6", outline, 18));
        Assert.Equal("userbuilding.preview", prev.Value<string>("type"));
        Assert.Equal("a1b2c3d4e5f6", prev.Value<string>("uid"));
        Assert.Equal(18, prev.Value<double>("height"));
        Assert.Equal(3, ((JArray)prev["outline"]!).Count);
        var gone = JObject.Parse(Map3DUserBuilding.PreviewMessageJson("a1b2c3d4e5f6", null, null));
        Assert.Equal(JTokenType.Null, gone["height"]!.Type);
        Assert.Null(gone["outline"]);
        Assert.Equal("{\"type\":\"userbuilding.clear\"}", Map3DUserBuilding.ClearMessageJson());
        var uid = Map3DUserBuilding.NewUid();
        Assert.Matches("^[0-9a-f]{12}$", uid);
    }

    [Fact]
    public void should_parse_items_and_skip_broken_ones()
    {
        var list = Map3DUserBuilding.ParseList(@"{""items"":[
            {""uid"":""a1b2c3d4e5f6"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":18,""floors"":6,""name"":""경비동"",""baked_id"":4,""roof"":""dome"",""use"":""industrial""},
            {""uid"":""0123456789ab"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":9.5,""floors"":null,""name"":null,""baked_id"":null},
            {""uid"":""BAD"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,37.5]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,""x""],[1,2]],""height"":9},
            {""uid"":""abcdefabcdef"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":0}
        ],""count"":6}");
        Assert.Equal(2, list.Count);
        Assert.Equal(("a1b2c3d4e5f6", 18.0, 6, "경비동", (int?)4), (list[0].Uid, list[0].Height, list[0].Floors!.Value, list[0].Name!, list[0].BakedId));
        Assert.Equal(("dome", "industrial"), (list[0].Roof, list[0].Use));
        Assert.Equal(("flat", "auto"), (list[1].Roof, list[1].Use));        // 옛 서버 · 없음 = 기본
        Assert.Null(list[1].Floors);
        Assert.Null(list[1].BakedId);
        Assert.Null(list[1].Name);
        Assert.Equal("직접 그린 건물 3개", Map3DUserBuilding.CountText(3));
        Assert.Equal("", Map3DUserBuilding.CountText(0));
    }

    [Fact]
    public void should_say_user_drawn_as_height_source_for_user_origin()
    {
        Assert.Equal("직접 그린 건물", Map3DBuilding.SourceText("user", "user"));
        Assert.Equal("지도 자료", Map3DBuilding.SourceText("user", "map"));      // 사용자 GeoJSON(지도 자료)는 그대로
    }

    // ── map-3d 창구(가짜 처리기) ──

    private sealed class Fake : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, (HttpStatusCode, string)> Reply { get; set; } = _ => (HttpStatusCode.OK, "{}");
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
            var (code, json) = Reply(request);
            return new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private const string Item = @"{""uid"":""a1b2c3d4e5f6"",""outline"":[[127,37.5],[127.0003,37.5],[127.0003,37.5002]],""height"":18,""floors"":6,""name"":null,""baked_id"":null}";

    [Fact]
    public async Task should_call_user_building_endpoints_with_expected_method_path_and_body()
    {
        var fake = new Fake();
        var c = new Map3DClient("http://127.0.0.1:8765", fake);
        fake.Reply = r => r.Method == HttpMethod.Get ? (HttpStatusCode.OK, "{\"items\":[" + Item + "],\"count\":1}")
                       : r.Method == HttpMethod.Put ? (HttpStatusCode.OK, "{\"item\":" + Item + ",\"count\":1}")
                       : r.RequestUri!.AbsolutePath.EndsWith("user-buildings") ? (HttpStatusCode.OK, "{\"deleted\":3,\"count\":0}")
                       : (HttpStatusCode.OK, "{\"deleted\":1,\"count\":0}");
        var list = await c.ListUserBuildingsAsync("j1");
        Assert.Single(list);
        var outline = new[] { new UbPoint(127, 37.5), new UbPoint(127.0003, 37.5), new UbPoint(127.0003, 37.5002) };
        var saved = await c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 18, 6, null);
        Assert.Equal(18, saved.Height);
        Assert.True(await c.DeleteUserBuildingAsync("j1", "a1b2c3d4e5f6"));
        Assert.Equal(3, await c.DeleteAllUserBuildingsAsync("j1"));
        Assert.Equal(new[] { "GET /api/jobs/j1/user-buildings", "PUT /api/jobs/j1/user-buildings/a1b2c3d4e5f6",
                             "DELETE /api/jobs/j1/user-buildings/a1b2c3d4e5f6", "DELETE /api/jobs/j1/user-buildings" },
                     fake.Requests.Select(r => $"{r.Method} {r.Path}"));
        var body = JObject.Parse(fake.Requests[1].Body!);
        Assert.Equal(new[] { "outline", "height", "floors", "name", "roof", "use" }, body.Properties().Select(p => p.Name));

        // 옛 서버(404) — 목록은 비고, 지우기는 false. 거절(400)은 서버 이유 그대로
        fake.Reply = _ => (HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
        Assert.Empty(await c.ListUserBuildingsAsync("j1"));
        Assert.False(await c.DeleteUserBuildingAsync("j1", "a1b2c3d4e5f6"));
        fake.Reply = _ => (HttpStatusCode.BadRequest, "{\"detail\":\"윤곽이 이 3D 영역 밖에 있습니다\"}");
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 9, null, null));
        Assert.Equal("윤곽이 이 3D 영역 밖에 있습니다", ex.Message);
        fake.Reply = _ => (HttpStatusCode.OK, "{\"item\":{\"uid\":\"x\"}}");
        await Assert.ThrowsAsync<Map3DRequestException>(() => c.PutUserBuildingAsync("j1", "a1b2c3d4e5f6", outline, 9, null, null));
    }

    [Fact]
    public void should_read_user_building_count_from_scene_list()
    {
        var scenes = Map3DClient.ParseScenes(@"{""scenes"":[{""id"":""a"",""bbox"":[127,37,127.01,37.01],""user_buildings"":3},
                                                          {""id"":""b"",""bbox"":[127,37,127.01,37.01]},
                                                          {""id"":""c"",""bbox"":[127,37,127.01,37.01],""user_buildings"":""x""}]}");
        Assert.Equal(new[] { 3, 0, 0 }, scenes.Select(s => s.UserBuildings));
    }

    // ── 박공 지붕이 되는 모양(map-3d buildings.build_building 과 같은 판정) ──

    /// <summary>미터 꼭짓점(동 · 북, 원점 기준 · rotDeg 만큼 돌림) → 위경도.</summary>
    private static IReadOnlyList<UbPoint> Meters(double rotDeg, params (double E, double N)[] pts)
    {
        double r = rotDeg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        return pts.Select(p => new UbPoint(Lon0 + (p.E * c - p.N * s) / (111320.0 * Math.Cos(Lat0 * Math.PI / 180)), Lat0 + (p.E * s + p.N * c) / 110540.0)).ToList();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(73)]
    public void should_allow_gable_for_rectangle_in_any_direction(double rot)
    {
        Assert.True(Map3DUserBuilding.CanGable(Meters(rot, (0, 0), (20, 0), (20, 10), (0, 10))));
        Assert.True(Map3DUserBuilding.CanGable(Meters(rot, (0, 0), (20, 0), (20, 8), (18, 10), (0, 10))));   // 모서리 조금 자른 네모(98 %)
    }

    [Fact]
    public void should_refuse_gable_for_l_shape_circle_and_tiny_outline()
    {
        Assert.False(Map3DUserBuilding.CanGable(Meters(0, (0, 0), (30, 0), (30, 10), (10, 10), (10, 30), (0, 30))));   // ㄱ자 56 %
        Assert.False(Map3DUserBuilding.CanGable(Map3DUserBuilding.CirclePoints(new UbPoint(127, 37.5), 0.0001, 32)));    // 원(약 78 %)
        Assert.False(Map3DUserBuilding.CanGable(Meters(0, (0, 0), (2, 0), (2, 1.9), (0, 1.9))));                          // 감싸는 네모 4 m² 이하
        Assert.False(Map3DUserBuilding.CanGable(Meters(0, (0, 0), (20, 0))));                                              // 윤곽 아님
    }

    [Theory]
    [InlineData(7, true)]    // 20 × 10 네모에서 7 × 4 를 뺌 = 86 %
    [InlineData(8, false)]   // 8 × 4 를 뺌 = 84 %
    public void should_use_85_percent_fill_of_smallest_rotated_rectangle(double cut, bool gable)
        => Assert.Equal(gable, Map3DUserBuilding.CanGable(Meters(20, (0, 0), (20, 0), (20, 6), (20 - cut, 6), (20 - cut, 10), (0, 10))));

    [Fact]
    public void should_allow_cone_and_dome_only_for_round_outline_with_16_or_more_points()
    {
        var circle = Meters(0, Enumerable.Range(0, 32).Select(i => (10 * Math.Cos(2 * Math.PI * i / 32), 10 * Math.Sin(2 * Math.PI * i / 32))).ToArray());
        var octagon = Meters(0, Enumerable.Range(0, 8).Select(i => (10 * Math.Cos(2 * Math.PI * i / 8), 10 * Math.Sin(2 * Math.PI * i / 8))).ToArray());
        var square = Meters(0, (0, 0), (20, 0), (20, 20), (0, 20));
        Assert.True(Map3DUserBuilding.CanRoundRoof(circle));
        Assert.False(Map3DUserBuilding.CanRoundRoof(octagon));                 // 꼭짓점 16개 미만
        Assert.False(Map3DUserBuilding.CanRoundRoof(square));
        Assert.True(Map3DUserBuilding.CanBuildRoof(Map3DUserBuilding.RoofDome, circle));
        Assert.False(Map3DUserBuilding.CanBuildRoof(Map3DUserBuilding.RoofCone, square));
        Assert.False(Map3DUserBuilding.CanBuildRoof(Map3DUserBuilding.RoofGable, circle));
        Assert.True(Map3DUserBuilding.CanBuildRoof(Map3DUserBuilding.RoofGable, square));
        Assert.True(Map3DUserBuilding.CanBuildRoof(Map3DUserBuilding.RoofFlat, octagon));   // 평지붕은 늘 된다
    }

    // ── 삼각형 도구 ──

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]     // 아래 오른쪽 → 위 왼쪽으로 끌어도 같은 삼각형
    [InlineData(40, false)]   // 돌린 지도에서도 화면에 보이는 대로
    public void should_make_isosceles_triangle_with_apex_at_box_top_middle_when_dragged(double rot, bool reverse)
    {
        var d = ToolDraft(UbTool.Triangle, rot);
        var a = new UbPoint(400, 300); var b = new UbPoint(500, 380);
        if (reverse) Drag(d, b, a); else Drag(d, a, b);
        Assert.True(d.IsClosed);
        Assert.Equal(UbTool.Triangle, d.CompletedBy);
        var s = ScreenOf(d, rot);
        Assert.Equal(new[] { (450.0, 300.0), (500.0, 380.0), (400.0, 380.0) },   // 꼭대기 → 오른쪽 아래 → 왼쪽 아래
                     s.Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))));
        Assert.Null(d.Validate());
        if (rot != 0) Assert.NotEqual(Math.Round(d.Points[1].Y, 9), Math.Round(d.Points[2].Y, 9));   // 위경도로는 돌아간 밑변
    }

    [Fact]
    public void should_make_triangle_by_two_clicks_and_equilateral_with_shift()
    {
        var clicks = ToolDraft(UbTool.Triangle);
        Click(clicks, 400, 300);
        clicks.PointerMove(new UbPoint(460, 360));
        Assert.Equal(3, clicks.Preview.Count);                             // 두 번째 클릭 전 미리보기
        Click(clicks, 460, 360);
        Assert.True(clicks.IsClosed);
        Assert.Equal(new[] { (430.0, 300.0), (460.0, 360.0), (400.0, 360.0) }, ScreenOf(clicks).Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))));

        var eq = ToolDraft(UbTool.Triangle);
        eq.PointerDown(new UbPoint(400, 300));
        eq.PointerMove(new UbPoint(450, 310), shift: true);
        eq.PointerUp(new UbPoint(500, 310), shift: true);                  // Shift = 정삼각형(높이 = 100 × √3/2)
        var q = ScreenOf(eq);
        Assert.Equal(100 * Math.Sqrt(3) / 2, q[1].Y - q[0].Y, 6);
        Assert.Equal(Map3DUserBuilding.Dist(q[0], q[1]), Map3DUserBuilding.Dist(q[1], q[2]), 6);
    }

    // ── 합치기 ──

    [Fact]
    public void should_build_merge_body_with_null_defaults_and_parse_merge_result()
    {
        var body = Map3DUserBuilding.MergeBody(new[] { "a1b2c3d4e5f6", "0123456789ab" });
        Assert.Equal(@"{""uids"":[""a1b2c3d4e5f6"",""0123456789ab""],""height"":null,""floors"":null,""name"":null,""use"":null}",
                     body.ToString(Newtonsoft.Json.Formatting.None));
        var filled = Map3DUserBuilding.MergeBody(new[] { "a1b2c3d4e5f6" }, 12, 4, " 창고 ", "industrial");
        Assert.Equal((12.0, 4, "창고", "industrial"), (filled.Value<double>("height"), filled.Value<int>("floors"), filled.Value<string>("name"), filled.Value<string>("use")));
        Assert.Equal(JTokenType.Null, Map3DUserBuilding.MergeBody(new[] { "x" }, use: "palace")["use"]!.Type);   // 모르는 외벽은 서버가 정함
        Assert.Null(body["uid"]);                                                                               // 처음 합치기 — uid 를 보내지 않는다(옛 엔진 호환)
        Assert.Equal("0123456789ab", Map3DUserBuilding.MergeBody(new[] { "x", "y" }, uid: "0123456789ab").Value<string>("uid"));   // 다시 하기 — 처음 uid 그대로

        var r = Map3DUserBuilding.ParseMergeResult("{\"item\":" + Item + ",\"merged\":[\"0123456789ab\",\"a1b2c3d4e5f6\"],\"holes_filled\":2,\"roof_forced_flat\":true}")!;
        Assert.Equal("a1b2c3d4e5f6", r.Item.Uid);
        Assert.Equal(new[] { "0123456789ab", "a1b2c3d4e5f6" }, r.Merged);
        Assert.Equal(2, r.HolesFilled);
        Assert.True(r.RoofForcedFlat);
        Assert.Null(Map3DUserBuilding.ParseMergeResult("{\"item\":{\"uid\":\"x\"}}"));
        Assert.Equal("3개를 하나로 합쳤습니다 · 지붕은 평지붕", Map3DUserBuilding.MergedText(3, 0));
        Assert.Equal("2개를 하나로 합쳤습니다 · 지붕은 평지붕 · 안쪽 빈 곳 1개를 채움", Map3DUserBuilding.MergedText(2, 1));
        Assert.Equal("도형 2개 선택", Map3DUserBuilding.SelectionText(2));
    }

    [Fact]
    public async Task should_post_merge_to_job_user_buildings_merge_url_and_raise_server_detail()
    {
        var fake = new Fake();
        var c = new Map3DClient("http://127.0.0.1:8765/", fake);
        fake.Reply = _ => (HttpStatusCode.OK, "{\"item\":" + Item + ",\"merged\":[\"0123456789ab\",\"fedcba987654\"],\"holes_filled\":0,\"roof_forced_flat\":true}");
        var r = await c.MergeUserBuildingsAsync("j1", new[] { "0123456789ab", "fedcba987654" });
        Assert.Equal("a1b2c3d4e5f6", r.Item.Uid);
        var req = Assert.Single(fake.Requests);
        Assert.Equal("POST /api/jobs/j1/user-buildings/merge", $"{req.Method} {req.Path}");
        Assert.Equal(@"{""uids"":[""0123456789ab"",""fedcba987654""],""height"":null,""floors"":null,""name"":null,""use"":null}", req.Body);

        fake.Reply = _ => (HttpStatusCode.BadRequest, "{\"detail\":\"서로 떨어진 도형은 합칠 수 없습니다\"}");
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.MergeUserBuildingsAsync("j1", new[] { "0123456789ab", "fedcba987654" }));
        Assert.Equal((HttpStatusCode.BadRequest, "서로 떨어진 도형은 합칠 수 없습니다"), (ex.StatusCode, ex.Message));
        fake.Reply = _ => (HttpStatusCode.NotFound, "{\"detail\":\"없는 건물: fedcba987654\"}");
        ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.MergeUserBuildingsAsync("j1", new[] { "0123456789ab", "fedcba987654" }));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }
}
