using System;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>3D 화면 위 GIS 창 자리 — 지켜볼 목록 · 화면 좌표 해석 · CSS px → 지도 칸 좌표 · 카메라 상자 오프셋(2D ↔ 3D) · 창 열 때 3D 유지 판정.</summary>
public class Map3DScreenTests
{
    private const double Eps = 1e-9;

    // ── 지켜볼 목록(GIS → 3D) ──

    [Fact]
    public void should_send_marker_kind_id_and_geo_when_watch_built()
    {
        var o = JObject.Parse(Map3DScreen.WatchJson(new[]
        {
            new Map3DWatchItem(Map3DScreen.CameraPopupKey(5), "pids", 12, 37.3, 127.1, 0d),
            new Map3DWatchItem("geo", null, null, 37.4, 127.2, 4d),
        }));
        Assert.Equal("overlay.watch", o.Value<string>("type"));
        var items = (JArray)o["items"]!;
        Assert.Equal("camera-popup:5", items[0].Value<string>("key"));
        Assert.Equal("pids", items[0].Value<string>("kind"));
        Assert.Equal(12, items[0].Value<int>("id"));
        Assert.Equal(127.1, items[0].Value<double>("lon"));
        Assert.Null(items[1]["kind"]);
        Assert.Null(items[1]["id"]);
        Assert.Equal(4d, items[1].Value<double>("altM"));
    }

    [Fact]
    public void should_send_empty_list_when_no_window_open()
        => Assert.Empty((JArray)JObject.Parse(Map3DScreen.WatchJson(Array.Empty<Map3DWatchItem>()))["items"]!);

    // ── 화면 좌표(3D → GIS) ──

    [Fact]
    public void should_parse_screen_points_when_overlay_screen_arrives()
    {
        var msg = Map3DMath.ParseMessage("""{"type":"overlay.screen","items":[{"key":"camera-popup:5","x":120.5,"y":80,"visible":true},{"key":"component-card:3","x":null,"y":null,"visible":false},{"x":1,"y":2},{"key":"half","x":3,"visible":true}]}""");
        Assert.NotNull(msg);
        Assert.Equal("overlay.screen", msg!.Type);
        var s = msg.Screens!;
        Assert.Equal(3, s.Count);   // key 없는 항목은 버린다
        Assert.Equal(new Map3DScreenPoint("camera-popup:5", 120.5, 80, true), s[0]);
        Assert.Equal(new Map3DScreenPoint("component-card:3", null, null, false), s[1]);   // 카메라 뒤
        Assert.Null(s[2].X);   // y 가 없으면 x 도 버린다
        Assert.Null(s[2].Y);
    }

    [Fact]
    public void should_return_empty_screens_when_items_missing()
        => Assert.Empty(Map3DMath.ParseMessage("""{"type":"overlay.screen"}""")!.Screens!);

    // ── CSS px → 지도 칸(outer) DIP ──

    [Theory]
    [InlineData(100, 50, 1.0, 0, 0, 100, 50)]
    [InlineData(100, 50, 1.25, 0, 0, 125, 62.5)]    // WebView2 확대 1.25 — CSS px 1 = DIP 1.25
    [InlineData(100, 50, 1.0, 10, 20, 110, 70)]     // 3D 호스트가 지도 칸 (10, 20) 에서 시작
    [InlineData(100, 50, 0, 0, 0, 100, 50)]         // 배율이 0 이하 · NaN 이면 1
    [InlineData(100, 50, double.NaN, 0, 0, 100, 50)]
    public void should_add_host_origin_and_zoom_factor_when_converting_to_outer(double x, double y, double zoom, double ox, double oy, double ex, double ey)
    {
        var (rx, ry) = Map3DScreen.ToOuter(x, y, zoom, ox, oy);
        Assert.Equal(ex, rx, 9);
        Assert.Equal(ey, ry, 9);
    }

    // ── 카메라 상자 오프셋(2D 규칙 → 3D 화면) ──

    [Fact]
    public void should_keep_offset_when_2d_and_3d_view_match()
    {
        var (x, y) = Map3DScreen.Offset2DTo3D(100, -340, 18, 18, 30, 30);
        Assert.Equal(100, x, 9);
        Assert.Equal(-340, y, 9);
    }

    [Fact]
    public void should_double_offset_when_3d_zoomed_in_one_level()
    {
        var (x, y) = Map3DScreen.Offset2DTo3D(100, -340, 18, 19, 0, 0);
        Assert.Equal(200, x, 9);
        Assert.Equal(-680, y, 9);
    }

    [Fact]
    public void should_rotate_offset_counterclockwise_when_3d_bearing_turned_right()
    {
        // 2D 정북(위)을 가리키던 오프셋 — 3D 에서 화면 위쪽이 동쪽(방위 90)이면 북은 화면 왼쪽
        var (x, y) = Map3DScreen.Offset2DTo3D(0, -100, 18, 18, 0, 90);
        Assert.Equal(-100, x, 9);
        Assert.Equal(0, y, 9);
        // 동쪽(오른쪽) 오프셋 → 화면 위
        (x, y) = Map3DScreen.Offset2DTo3D(100, 0, 18, 18, 0, 90);
        Assert.Equal(0, x, 9);
        Assert.Equal(-100, y, 9);
    }

    [Fact]
    public void should_treat_wrapped_bearings_alike_when_crossing_north()
    {
        var a = Map3DScreen.Offset2DTo3D(50, -80, 18, 18, 350, 10);
        var b = Map3DScreen.Offset2DTo3D(50, -80, 18, 18, -10, 10);
        Assert.Equal(a.X, b.X, 9);
        Assert.Equal(a.Y, b.Y, 9);
    }

    [Theory]
    [InlineData(100, -340, 17.5, 18.75, 12, 200)]
    [InlineData(-30, 45, 19, 16.6, 359, 1)]
    [InlineData(0, 0, 18, 20, 0, 180)]
    public void should_restore_2d_offset_when_3d_drag_converted_back(double dx, double dy, double z2, double z3, double b2, double b3)
    {
        var (x3, y3) = Map3DScreen.Offset2DTo3D(dx, dy, z2, z3, b2, b3);
        var (x2, y2) = Map3DScreen.Offset3DTo2D(x3, y3, z2, z3, b2, b3);
        Assert.Equal(dx, x2, 6);
        Assert.Equal(dy, y2, 6);
    }

    [Fact]
    public void should_put_popup_at_3d_marker_plus_2d_offset_when_view_matches()
    {
        // 2D: 마커 (400, 300), 상자 왼쪽 위(앵커) (500, -40) — 2D 첫 자리 규칙(오른쪽 100 · 위 100 + 높이 240)
        // 3D: 같은 마커가 기울어진 화면 (380, 420) 에 보인다 → 상자는 (480, 80)
        var (x, y) = Map3DScreen.PopupTopLeft3D((380, 420), (400, 300), (500, -40), 18, 18, 0, 0);
        Assert.Equal(480, x, 9);
        Assert.Equal(80, y, 9);
    }

    [Fact]
    public void should_follow_marker_when_3d_camera_moves_marker_on_screen()
    {
        var a = Map3DScreen.PopupTopLeft3D((380, 420), (400, 300), (500, -40), 18, 18.5, 0, 20);
        var b = Map3DScreen.PopupTopLeft3D((330, 470), (400, 300), (500, -40), 18, 18.5, 0, 20);
        Assert.Equal(a.X - 50, b.X, 9);   // 마커가 움직인 만큼 그대로
        Assert.Equal(a.Y + 50, b.Y, 9);
    }

    [Fact]
    public void should_restore_2d_anchor_when_popup_dragged_in_3d()
    {
        var (z2, z3, b2, b3) = (17.5, 18.25, 10d, 75d);
        var top3D = Map3DScreen.PopupTopLeft3D((380, 420), (400, 300), (520, 10), z2, z3, b2, b3);
        var (ax, ay) = Map3DScreen.DraggedAnchor2D(top3D, (380, 420), (400, 300), z2, z3, b2, b3);
        Assert.Equal(520, ax, 6);
        Assert.Equal(10, ay, 6);
    }

    [Fact]
    public void should_ignore_non_finite_view_values_when_computing_offset()
    {
        var (x, y) = Map3DScreen.Offset2DTo3D(10, 20, double.NaN, 19, 0, double.PositiveInfinity);
        Assert.Equal(10, x, 9);
        Assert.Equal(20, y, 9);
    }

    // ── 창 열 때 3D 유지 ──

    [Theory]
    [InlineData("composition", false)]   // WPF 안 3D — 창이 3D 위에 보이고 마커 화면 자리를 따라간다
    [InlineData("hwnd", true)]           // 네이티브 창 3D 는 WPF 창을 덮는다 — 2D 로
    [InlineData("none", true)]
    public void should_stay_in_3d_when_window_opens_over_composition_host(string hostMode, bool expected)
        => Assert.Equal(expected, Map3DGuide.ShouldLeave3DForWindow(hostMode));

    // ── Esc 로 3D 나가기 ──

    [Theory]
    [InlineData("composition", false)]   // WPF 안 3D — 「나가기」 단추로만(2026-10-19)
    [InlineData("hwnd", true)]           // 네이티브 창 3D 는 그 창이 단추를 가린다 — Esc 를 남긴다
    [InlineData("none", false)]
    public void should_exit_3d_by_esc_only_when_native_window_hides_exit_button(string hostMode, bool expected)
        => Assert.Equal(expected, Map3DGuide.EscExitsMap3D(hostMode));
}
