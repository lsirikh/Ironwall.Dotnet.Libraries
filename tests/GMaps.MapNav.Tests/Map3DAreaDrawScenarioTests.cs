using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 3D 영역 그리기 경계 시나리오(영역 B 전수, 2026-10-19) — 창 없는 실제 지도(코어만 시작) + 생성자 없는 MapViewModel 의 그리기 경로
/// (EnterMap3DDrawMode → 러버밴드 놓기 OnAnchorAreaDrawn · Enter TryHandleMap3DKey · ESC OnMapPreviewKeyDownForAim).
/// Map3DRotatedAreaScenarioTests 와 같은 장치(그 시험은 고치지 않고 여기 더한다).
/// </summary>
public class Map3DAreaDrawScenarioTests
{
    private static (GMapCustomControl map, MapViewModel vm) Rig(double bearing, int zoom = 17)
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: zoom);
        var vm = NavKit.NewVm(map);
        Scenario.Call(vm, "InitializeRotationCommands");
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
    private static bool DrawMode(MapViewModel vm) => Scenario.GetField<bool>(vm, "_isMap3DDrawMode");
    private static double Mpp(GMapCustomControl map) => Map3DMath.MetersPerPixel(map.Zoom, map.Position.Lat);

    // ── B-06 회전 + 디지털 줌 + Enter ──

    [Theory]
    [InlineData(45d, 17.5d)]
    [InlineData(-30d, 16.5d)]
    public void should_take_inner_visible_screen_scaled_by_digital_zoom_when_enter_pressed_on_rotated_map(double bearing, double effectiveZoom) => Run(() =>
    {
        var (map, vm) = Rig(bearing, zoom: 16);
        map.SetEffectiveZoom(effectiveZoom);
        NavKit.Pump();
        double s = map.DigitalZoomScale;
        Assert.True(Math.Abs(s - 1d) > 0.01, $"디지털 줌 배율이 1 — 이 시험의 전제(배율 ≠ 1)가 아님 s={s}");
        Scenario.Call(vm, "EnterMap3DDrawMode");

        Assert.True((bool)Scenario.Call(vm, "TryHandleMap3DKey", NavKit.KeyArgs(Key.Enter))!);

        var r = BakeArea(vm)!.Rect!;
        Assert.Equal(Map3DMath.ToMap3DBearing(bearing), r.RotationDeg, 6);
        double mpp = Mpp(map);   // 코어(정수) 줌의 m/px — 보이는 화면은 그 화면 크기를 배율만큼 줄인 것
        Assert.Equal(1200 / s * mpp, r.WidthM, 1200 / s * mpp * 0.01);
        Assert.Equal(800 / s * mpp, r.HeightM, 800 / s * mpp * 0.01);
        // 배율은 가운데 기준 — 모서리를 정수 px 로 바꾸므로(배율이 1 이 아니면 반폭이 정수가 아니다) 2 px 안
        var (cx, cy) = r.ToLocal(map.Position.Lng, map.Position.Lat);
        Assert.True(Math.Sqrt(cx * cx + cy * cy) < 2 * mpp, $"가운데 어긋남 {cx:F2}, {cy:F2} m (mpp={mpp:F2})");
        Assert.False(map.IsAnchorDrawMode);
        Assert.True(vm.IsMap3DBakeOptionsOpen);
    });

    // ── B-05 회전 없음 + 디지털 줌 + Enter ──

    [Fact]
    public void should_take_bbox_shrunk_by_digital_zoom_when_enter_pressed_on_unrotated_map() => Run(() =>
    {
        var (map, vm) = Rig(0, zoom: 16);
        map.SetEffectiveZoom(17.5);
        NavKit.Pump();
        double s = map.DigitalZoomScale;
        Assert.True(Math.Abs(s - 1d) > 0.01, $"s={s}");
        Scenario.Call(vm, "EnterMap3DDrawMode");

        Assert.True((bool)Scenario.Call(vm, "TryHandleMap3DKey", NavKit.KeyArgs(Key.Enter))!);

        var area = BakeArea(vm)!;
        Assert.Null(area.Rect);
        var v = map.ViewArea;   // 코어 정수 줌의 보이는 상자
        Assert.Equal(v.WidthLng / s, area.Bbox[2] - area.Bbox[0], 9);
        Assert.Equal(v.HeightLat / s, area.Bbox[3] - area.Bbox[1], 9);
        Assert.Equal(v.Lng + v.WidthLng / 2, (area.Bbox[0] + area.Bbox[2]) / 2, 9);
    });

    // ── B-10 회전 변 정확히 2000 m → 만들기 판(경고 없음) ──

    [Fact]
    public void should_open_bake_panel_when_rotated_side_is_exactly_2000m() => Run(() =>
    {
        var (map, vm) = Rig(30, zoom: 14);
        var rect = new Map3DRotRect(map.Position.Lng, map.Position.Lat, 2000d, 2000d, 30d);
        Scenario.Call(vm, "TakeMap3DArea", new Map3DArea(rect.Bbox(), rect));
        Assert.True(vm.IsMap3DBakeOptionsOpen);
        Assert.False(map.IsAnchorDrawMode);
        Assert.Same(rect, BakeArea(vm)!.Rect);

        vm.CancelMap3DBakeOptionsCommand.Execute(null);
        var over = new Map3DRotRect(map.Position.Lng, map.Position.Lat, 2000.5d, 2000d, 30d);
        Scenario.Call(vm, "TakeMap3DArea", new Map3DArea(over.Bbox(), over));
        Assert.False(vm.IsMap3DBakeOptionsOpen);
        Assert.True(map.IsAnchorDrawMode);   // 넘으면 다시 그리기
        Assert.Contains("영역이 너무 큽니다", vm.AimStatusMessage);
    });

    // ── B-08 모서리 없음 → bbox 폴백 ──

    [Fact]
    public void should_fall_back_to_bbox_without_rect_when_last_anchor_corners_missing() => Run(() =>
    {
        var (map, vm) = Rig(30);
        Scenario.Call(vm, "EnterMap3DDrawMode");
        Scenario.SetField(map, "<LastAnchorCorners>k__BackingField", null);
        var nw = map.FromLocalToLatLng(300, 200);
        var se = map.FromLocalToLatLng(700, 400);

        Scenario.Call(vm, "OnAnchorAreaDrawn", nw, se);

        var area = BakeArea(vm)!;
        Assert.Null(area.Rect);
        Assert.Equal(Map3DMath.BuildBbox(nw.Lat, nw.Lng, se.Lat, se.Lng), area.Bbox);
        Assert.True(vm.IsMap3DBakeOptionsOpen);
    });

    // ── B-12 ESC 취소 · 늦은 놓기 ──

    [Fact]
    public void should_end_draw_mode_and_ignore_late_release_when_esc_pressed_while_drawing() => Run(() =>
    {
        var (map, vm) = Rig(30);
        Scenario.Call(vm, "EnterMap3DDrawMode");
        Assert.True(map.IsAnchorDrawMode);
        Assert.True(DrawMode(vm));

        var esc = NavKit.KeyArgs(Key.Escape);
        Scenario.Call(vm, "OnMapPreviewKeyDownForAim", map, esc);   // 창 PreviewKeyDown 처리기 그대로

        Assert.True(esc.Handled);
        Assert.False(map.IsAnchorDrawMode);
        Assert.False(DrawMode(vm));
        Assert.Null(map.AnchorDrawCornersLabel);   // 크기 표시도 내림
        Assert.False(vm.IsMap3DBakeOptionsOpen);

        // 취소 뒤 늦게 도착한 놓기(러버밴드 처리기) — 3D 만들기 판을 열지 않는다
        Scenario.SetField(map, "<LastAnchorCorners>k__BackingField",
            new[] { map.FromLocalToLatLng(300, 200), map.FromLocalToLatLng(700, 200), map.FromLocalToLatLng(700, 400), map.FromLocalToLatLng(300, 400) });
        Scenario.Call(vm, "OnAnchorAreaDrawn", map.FromLocalToLatLng(300, 200), map.FromLocalToLatLng(700, 400));
        Assert.False(vm.IsMap3DBakeOptionsOpen);
        Assert.Null(BakeArea(vm));
    });

    // ── B-13 만들기 판이 열린 채 다시 「영역 그리기」 ──

    [Fact]
    public void should_close_bake_panel_when_draw_area_pressed_again() => Run(() =>
    {
        var (map, vm) = Rig(0);
        Scenario.Call(vm, "EnterMap3DDrawMode");
        Assert.True((bool)Scenario.Call(vm, "TryHandleMap3DKey", NavKit.KeyArgs(Key.Enter))!);
        Assert.True(vm.IsMap3DBakeOptionsOpen);

        Assert.True(vm.DrawMap3DAreaCommand.CanExecute(null));
        vm.DrawMap3DAreaCommand.Execute(null);

        Assert.False(vm.IsMap3DBakeOptionsOpen);
        Assert.Null(BakeArea(vm));
        Assert.True(map.IsAnchorDrawMode);
        Assert.True(DrawMode(vm));
    });
}
