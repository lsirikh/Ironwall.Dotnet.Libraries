using System.Windows.Input;
using System.Windows.Shapes;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using ScenarioShared;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 회전된 3D 영역(2026-10-19) VM 시나리오 — 창 없는 실제 지도(코어만 시작)를 돌려 놓고 3D 영역을 그린다.
/// ① 러버밴드(화면 사각형 네 모서리) · Enter(보이는 화면) → 만들기 패널에 넘어간 영역이 회전 사각형 그대로 ② 크기 라벨 · 최대 크기
/// ③ 서버 목록의 회전 영역 → 2D 테두리가 기울어진 네모 ④ 게이트 장면 찾기 ⑤ 「이 영역 3D 로 보기」 = 가운데 + 회전각.
/// </summary>
public class Map3DRotatedAreaScenarioTests
{
    private static (GMapCustomControl map, MapViewModel vm) Rig(double bearing, int zoom = 17)
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: zoom);
        var vm = NavKit.NewVm(map);
        Scenario.Call(vm, "InitializeRotationCommands");
        // 생성자를 거치지 않아 필드 초기화가 없다 — 3D 경로가 쓰는 것만 채운다
        Scenario.SetField(vm, "_map3DSettings", new Map3DModel());
        Scenario.SetField(vm, "_map3DScenes", new List<Map3DScene>());
        Scenario.SetField(vm, "_map3DAreaMarkers", new Dictionary<string, Map3DAreaMarker>());
        Scenario.SetField(vm, "_map3DBakeOptionsOverlays", new List<Map3DBakeOverlay>());
        Scenario.SetField(vm, "<Map3DAreas>k__BackingField", new System.Collections.ObjectModel.ObservableCollection<Map3DAreaItem>());
        RotationFeature.IsEnabled = true;
        map.SetMapRotation(bearing);
        NavKit.Pump();
        Assert.Equal((float)bearing, map.Bearing, 3);
        return (map, vm);
    }

    private static void Run(Action body) => Scenario.Sta(() =>
    {
        try { body(); }
        finally { NavKit.ResetRotationFeature(); Mouse.OverrideCursor = null; }
    });

    private static Map3DArea? BakeArea(MapViewModel vm) => Scenario.GetField<Map3DArea?>(vm, "_map3DBakeOptionsArea");

    /// <summary>화면 사각형(지도 로컬 px) 네 모서리 위경도 — 좌상 · 우상 · 우하 · 좌하(GMapCustomControl.RubberRectCorners 와 같은 순서).</summary>
    private static PointLatLng[] Corners(GMapCustomControl map, double l, double t, double r, double b)
        => new[] { map.FromLocalToLatLng((int)l, (int)t), map.FromLocalToLatLng((int)r, (int)t), map.FromLocalToLatLng((int)r, (int)b), map.FromLocalToLatLng((int)l, (int)b) };

    private static double Mpp(GMapCustomControl map) => Map3DMath.MetersPerPixel(map.Zoom, map.Position.Lat);

    /// <summary>러버밴드를 놓았을 때와 같이 — 네 모서리를 남기고 AnchorAreaDrawn(북서, 남동) 처리기를 부른다.</summary>
    private static void DrawRubber(GMapCustomControl map, MapViewModel vm, double l, double t, double r, double b)
    {
        Scenario.Call(vm, "EnterMap3DDrawMode");
        var c = Corners(map, l, t, r, b);
        Scenario.SetField(map, "<LastAnchorCorners>k__BackingField", c);
        var nw = new PointLatLng(c.Max(p => p.Lat), c.Min(p => p.Lng));
        var se = new PointLatLng(c.Min(p => p.Lat), c.Max(p => p.Lng));
        Scenario.Call(vm, "OnAnchorAreaDrawn", nw, se);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(-60)]
    public void should_hand_rotated_rect_to_bake_panel_when_drawn_on_rotated_map(double bearing) => Run(() =>
    {
        var (map, vm) = Rig(bearing);
        DrawRubber(map, vm, 300, 200, 700, 400);   // 화면 400 × 200 px

        var area = BakeArea(vm);
        Assert.NotNull(area);
        Assert.True(vm.IsMap3DBakeOptionsOpen);
        var r = area!.Rect!;
        Assert.Equal(Map3DMath.ToMap3DBearing(bearing), r.RotationDeg, 6);
        double mpp = Mpp(map);
        Assert.Equal(400 * mpp, r.WidthM, 400 * mpp * 0.01);    // 화면 그대로 가로 · 세로(1 % — 정수 px · 메르카토르 축척)
        Assert.Equal(200 * mpp, r.HeightM, 200 * mpp * 0.01);
        // 사각형 가운데 = 화면 사각형 가운데(모서리를 정수 px 로 바꾸므로 2 px 안)
        var mid = map.FromLocalToLatLng(500, 300);
        var (mx, my) = r.ToLocal(mid.Lng, mid.Lat);
        Assert.True(Math.Sqrt(mx * mx + my * my) < 2 * mpp, $"{mx}, {my}");
        // 굽기 요청 본문에 rect 가 실린다
        Assert.Equal(r.RotationDeg, Map3DBake.BuildBody(area.Bbox, Map3DBake.ModeTiles, null, 17, area.Rect)["rect"]!.Value<double>("rotation_deg"), 9);
    });

    [Fact]
    public void should_keep_north_aligned_bbox_when_drawn_on_unrotated_map() => Run(() =>
    {
        var (map, vm) = Rig(0);
        DrawRubber(map, vm, 300, 200, 700, 400);

        var area = BakeArea(vm)!;
        Assert.Null(area.Rect);
        var nw = map.FromLocalToLatLng(300, 200); var se = map.FromLocalToLatLng(700, 400);
        Assert.Equal(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng), area.Bbox);   // 예전 그대로
        Assert.Null(Map3DBake.BuildBody(area.Bbox, Map3DBake.ModeTiles, null, 17, area.Rect)["rect"]);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    public void should_take_visible_screen_when_enter_pressed(double bearing) => Run(() =>
    {
        var (map, vm) = Rig(bearing);
        Scenario.Call(vm, "EnterMap3DDrawMode");
        Assert.True(map.IsAnchorDrawMode);

        var handled = (bool)Scenario.Call(vm, "TryHandleMap3DKey", NavKit.KeyArgs(Key.Enter))!;

        Assert.True(handled);
        var area = BakeArea(vm)!;
        if (bearing == 0)
        {
            Assert.Null(area.Rect);
            return;
        }
        // 돌아간 화면 그대로 — 1200 × 800 px 화면(감싸는 상자가 아니라)
        var r = area.Rect!;
        Assert.Equal(45, r.RotationDeg, 6);
        double mpp = Mpp(map);
        Assert.Equal(1200 * mpp, r.WidthM, 1200 * mpp * 0.01);
        Assert.Equal(800 * mpp, r.HeightM, 800 * mpp * 0.01);
        Assert.Equal(map.Position.Lat, r.Lat, 5);
        Assert.Equal(map.Position.Lng, r.Lon, 5);
    });

    [Fact]
    public void should_label_and_limit_by_rotated_sides_when_drawing() => Run(() =>
    {
        var (map, vm) = Rig(45, zoom: 15);
        Scenario.Call(vm, "EnterMap3DDrawMode");
        double mpp = Mpp(map);
        // 화면 1000 × 1000 px ≈ 3.8 km — 실제 변이 2 km 를 넘는다 → 경고
        var (text, warn) = map.AnchorDrawCornersLabel!(Corners(map, 100, 0, 1100, 800));
        Assert.True(warn, text);
        // 45° 화면에서 화면 450 × 450 px(≈ 1.7 km 네모) — 감싸는 bbox 는 한 변 ≈ 2.4 km 지만 실제 변은 1.7 km → 경고 없음 · 그대로 만들기 패널
        double side = 450;
        var label = map.AnchorDrawCornersLabel!(Corners(map, 375, 175, 375 + side, 175 + side));
        Assert.False(label.IsWarning, label.Text);
        Assert.StartsWith(Map3DGuide.FormatLength(side * mpp)[..3], label.Text);
        DrawRubber(map, vm, 375, 175, 375 + side, 175 + side);
        Assert.True(vm.IsMap3DBakeOptionsOpen);
        var (bw, bh) = Map3DGuide.AreaSizeMeters(BakeArea(vm)!.Bbox);
        Assert.True(bw > 2000 && bh > 2000, $"{bw} × {bh}");    // 예전(감싸는 bbox 로 셈)이면 거절됐다
    });

    [Fact]
    public void should_ask_to_redraw_when_rotated_side_too_large() => Run(() =>
    {
        var (map, vm) = Rig(30, zoom: 14);
        DrawRubber(map, vm, 100, 100, 1100, 300);   // 가로 ≈ 7.6 km
        Assert.False(vm.IsMap3DBakeOptionsOpen);
        Assert.True(map.IsAnchorDrawMode);            // 다시 그리기 모드
        Assert.Contains("영역이 너무 큽니다", vm.AimStatusMessage);
    });

    [Fact]
    public void should_draw_tilted_outline_and_find_scene_inside_rotated_rect() => Run(() =>
    {
        var (map, vm) = Rig(0);
        var r = new Map3DRotRect(map.Position.Lng, map.Position.Lat, 300, 160, 45);
        var other = new[] { r.Bbox()[2] - 0.0003, r.Bbox()[3] - 0.0003, r.Bbox()[2] + 0.002, r.Bbox()[3] + 0.002 };
        var scenes = new List<Map3DSceneInfo>
        {
            new("rot", null, r.Bbox(), null, 10, "job") { Rect = r },
            new("old", null, other, null, 3, "job"),
        };
        Scenario.Call(vm, "ApplyMap3DAreas", scenes);
        NavKit.Pump();

        // ③ 테두리 — 지도 방위 0 에서 45° 영역은 마름모(위 변이 화면에서 기울어 있다)
        var markers = Scenario.GetField<Dictionary<string, Map3DAreaMarker>>(vm, "_map3DAreaMarkers");
        var marker = markers["rot"];
        marker.Refresh(map, true);
        var pts = Scenario.GetField<Polygon>(marker, "_outline").Points;
        Assert.Equal(4, pts.Count);
        double dx = pts[1].X - pts[0].X, dy = pts[1].Y - pts[0].Y;
        Assert.Equal(45, Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI), 1.5);
        double topPx = Math.Sqrt(dx * dx + dy * dy);
        Assert.Equal(300 / Mpp(map), topPx, 300 / Mpp(map) * 0.02);
        // 지도를 영역 방위로 돌리면 반듯한 네모
        map.SetMapRotation(45); NavKit.Pump();
        marker.Refresh(map, true);
        pts = Scenario.GetField<Polygon>(marker, "_outline").Points;
        Assert.True(Math.Abs(pts[1].Y - pts[0].Y) < 2 && Math.Abs(pts[3].X - pts[0].X) < 2, string.Join(" ", pts));
        // 옛 영역도 네 모서리 다각형(지도가 돌아가 있어도 맞다)
        var oldPts = Scenario.GetField<Polygon>(markers["old"], "_outline");
        markers["old"].Refresh(map, true);
        Assert.Equal(4, oldPts.Points.Count);

        // ④ 게이트 장면 찾기 — 회전 사각형 안만
        var find = (PointLatLng p) => (Map3DScene?)Scenario.Call(vm, "FindMap3DSceneAt", p);
        Assert.Equal("rot", find(new PointLatLng(r.Lat, r.Lon))!.Id);
        var b = r.Bbox();
        Assert.Equal("old", find(new PointLatLng(b[3] - 0.0001, b[2] - 0.0001))!.Id);   // 감싸는 bbox 모서리 = 회전 영역 밖
        var (fLon, fLat) = r.ToLonLat(0, 120);
        Assert.Null(find(new PointLatLng(fLat, fLon)));
        var (iLon, iLat) = r.ToLonLat(145, 75);   // 모서리 바로 안
        Assert.Equal("rot", find(new PointLatLng(iLat, iLon))!.Id);
    });

    [Fact]
    public void should_center_and_rotate_map_when_area_view_opens_rotated_area() => Run(() =>
    {
        var (map, vm) = Rig(0);
        var r = new Map3DRotRect(map.Position.Lng + 0.003, map.Position.Lat - 0.002, 300, 160, 300);
        var item = new Map3DAreaItem(new Map3DSceneInfo("rot", null, r.Bbox(), null, 10, "job") { Rect = r }, _ => { }, _ => { }, _ => { });

        Scenario.Call(vm, "ViewMap3DArea", item);

        Assert.Equal(r.Lat, map.Position.Lat, 6);
        Assert.Equal(r.Lon, map.Position.Lng, 6);
        Assert.Equal(-60f, map.Bearing, 3);                                            // 2D 도 그 회전각(300° = −60°)
        Assert.Equal(300, Map3DMath.EnterBearing(map.Bearing, r), 6);                  // 3D 처음 시점 방위
        var back = Scenario.GetField<Map3DCamera?>(vm, "_map3DReturn");
        Assert.Equal(0, back!.Value.Bearing, 6);                                       // 나가면 들어오기 전 2D(방위 0)로
        Assert.Equal(NavKit.Lat, back.Value.Lat, 6);
    });

    [Fact]
    public void should_center_without_rotating_when_area_view_opens_old_area() => Run(() =>
    {
        var (map, vm) = Rig(20);
        var bbox = new[] { 127.001, 37.501, 127.003, 37.502 };
        var item = new Map3DAreaItem(new Map3DSceneInfo("old", null, bbox, null, 3, "job"), _ => { }, _ => { }, _ => { });

        Scenario.Call(vm, "ViewMap3DArea", item);

        Assert.Equal(37.5015, map.Position.Lat, 6);
        Assert.Equal(127.002, map.Position.Lng, 6);
        Assert.Equal(20f, map.Bearing, 3);                                             // 옛 영역 — 지도 방위 그대로
    });
}
