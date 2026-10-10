using System.Windows;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>카탈로그 §1 위치 · 홈 · 사이트 고정(MAP-002~005 · 015 · 030~033) — 뷰모델 + 코어 시작한 지도(창 없음).</summary>
public class HomeAnchorScenarioTests
{
    // 사이트: 약 11 km × 9 km — 줌 16(1200×800 ≈ 2.3 km × 1.2 km) 화면이 넉넉히 들어간다
    private const double N = 37.55, S = 37.45, W = 126.95, E = 127.05;

    private static void AssertViewInsideSite(GMapCustomControl map)
    {
        var c = map.Position;
        Assert.InRange(c.Lat, S, N);
        Assert.InRange(c.Lng, W, E);
        var v = map.ViewArea;   // Lat=북, Lng=서
        const double tol = 1e-4;
        Assert.True(v.Lat <= N + tol && v.Lat - v.HeightLat >= S - tol && v.Lng >= W - tol && v.Lng + v.WidthLng <= E + tol,
            $"보기 {v} 가 사이트 N{N} S{S} W{W} E{E} 밖");
    }

    private static MapViewModel AnchoredVm(GMapCustomControl map, bool allowRotation = false)
    {
        var vm = NavKit.NewVm(map, new GMapSetupModel { MapAnchor = NavKit.Anchor(N, W, S, E, allowRotation) });
        Scenario.Call(vm, "ApplyMapAnchor");
        Assert.True(map.IsAnchorActive);
        return vm;
    }

    [Fact]
    [Trait("Scenario", "MAP-002")]
    public void should_return_center_and_zoom_to_home_when_home_button_pressed_after_pan() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 14);
        var vm = NavKit.NewVm(map);
        vm.HomePosition = new HomePositionModel { Position = new CoordinateModel(37.40, 126.90, 0), Zoom = 17.5, IsAvailable = false };
        Assert.False(vm.MoveHomeLocationCommand!.CanExecute(null));   // 홈 미설정이면 단추 꺼짐

        vm.HomePosition.IsAvailable = true;
        map.Position = new PointLatLng(37.60, 127.10);                  // 팬
        NavKit.Pump();
        Assert.True(vm.MoveHomeLocationCommand.CanExecute(null));
        vm.MoveHomeLocationCommand.Execute(null);
        NavKit.Pump();

        Assert.Equal(37.40, map.Position.Lat, 6);
        Assert.Equal(126.90, map.Position.Lng, 6);
        Assert.Equal(17.5, map.EffectiveZoom, 6);                       // 하프스텝 홈 줌도 그대로(17, dzl1)
    });

    [Fact]
    [Trait("Scenario", "MAP-003")]
    public void should_clip_home_move_into_site_when_anchor_locks_and_home_is_outside() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 16, lat: 37.5, lng: 127.0);
        var vm = AnchoredVm(map);
        vm.HomePosition = new HomePositionModel { Position = new CoordinateModel(37.80, 127.30, 0), Zoom = 16, IsAvailable = true };   // 사이트 밖 홈

        vm.MoveHomeLocationCommand!.Execute(null);
        NavKit.Pump();

        AssertViewInsideSite(map);
        Assert.True(map.Position.Lat > 37.5 && map.Position.Lng > 127.0, "홈 쪽(북동) 경계로 잘려야 한다");
    });

    [Fact]
    [Trait("Scenario", "MAP-015")]
    public void should_keep_view_inside_site_when_panned_outside_while_anchor_locked() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 16);
        AnchoredVm(map);
        foreach (var target in new[] { new PointLatLng(37.5, 127.4), new PointLatLng(37.2, 127.0), new PointLatLng(37.9, 126.5) })
        {
            map.Position = target;   // 사이트 밖으로 팬(비드래그 위치 변경 — 드래그 종료 클램프와 같은 ClampCenterToBounds)
            NavKit.Pump();
            AssertViewInsideSite(map);
        }
        map.Zoom = 15;               // 줌아웃해도 inset 재계산으로 보기 유지(화면 0.05° 폭 < 사이트 0.1°)
        NavKit.Pump();
        AssertViewInsideSite(map);
    });

    [Fact]
    [Trait("Scenario", "MAP-004")]
    public void should_show_cancel_banner_and_keep_home_when_escape_pressed_in_home_placement() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap();
        var vm = NavKit.NewVm(map);
        vm.HomePosition = new HomePositionModel { Position = new CoordinateModel(37.41, 126.91, 0), Zoom = 15, IsAvailable = true };

        vm.SetHomeLocationCommand!.Execute(null);                        // 편집 띠 「홈 설정」
        Assert.True(map.IsHomePlacementMode);
        Assert.Contains("ESC", vm.AimStatusMessage);
        Assert.Equal(Cursors.Cross, Mouse.OverrideCursor);

        var esc = NavKit.KeyArgs(Key.Escape);
        Scenario.Call(vm, "OnMapPreviewKeyDownForAim", map, esc);        // 창 PreviewKeyDown 후킹과 같은 처리기

        Assert.True(esc.Handled);
        Assert.False(map.IsHomePlacementMode);
        Assert.Equal("홈 설정을 취소했습니다.", vm.AimStatusMessage);
        Assert.True(vm.IsAimStatusVisible);
        Assert.Null(Mouse.OverrideCursor);
        Assert.Equal(37.41, vm.HomePosition.Position!.Latitude, 9);   // 변화 없음
        Assert.Equal(126.91, vm.HomePosition.Position.Longitude, 9);
        Assert.Equal(15, vm.HomePosition.Zoom);
    });

    [Fact]
    [Trait("Scenario", "MAP-005")]
    public void should_persist_clicked_point_and_effective_zoom_when_home_placement_clicked() => Scenario.StaAsync(async () =>
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: 17);
        map.DigitalZoomLevel = 1;                                         // 실효 17.5
        var vm = NavKit.NewVm(map);
        vm.HomePosition = new HomePositionModel { Position = new CoordinateModel(37.41, 126.91, 0), Zoom = 15, IsAvailable = false };

        vm.SetHomeLocationCommand!.Execute(null);
        Scenario.Call(vm, "OnHomePlacementClicked", new PointLatLng(37.512345, 127.012345), new Point(600, 400));

        Assert.False(map.IsHomePlacementMode);
        Assert.True(vm.HomePosition.IsAvailable);
        Assert.True(vm.MoveHomeLocationCommand!.CanExecute(null));
        var saved = await NavKit.WaitSetting("HomePosition", t => Math.Abs(((double?)t.SelectToken("Position.Latitude") ?? 0) - 37.512345) < 1e-6);
        Assert.NotNull(saved);
        Assert.Equal(37.512345, (double)saved!.SelectToken("Position.Latitude")!, 6);
        Assert.Equal(127.012345, (double)saved.SelectToken("Position.Longitude")!, 6);
        Assert.Equal(17.5, (double)saved["Zoom"]!, 6);
        Assert.True((bool)saved["IsAvailable"]!);
    });

    [Fact]
    [Trait("Scenario", "MAP-030")]
    public void should_open_and_close_anchor_panel_when_edit_strip_anchor_and_header_close_used() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap();
        var vm = NavKit.NewVm(map);
        Assert.False(vm.IsMapAnchorPanelVisible);

        vm.ShowMapAnchorPanelCommand.Execute(null);                     // 편집 띠 「사이트 고정」
        Assert.True(vm.IsMapAnchorPanelVisible);
        Assert.Same(vm.ShowMapAnchorPanelCommand, vm.MapAnchorPanel.CloseCommand);   // 머리 ✕ = 같은 토글
        Assert.Equal("사이트 고정 (Map Anchor)", vm.MapAnchorPanel.PanelTitle);

        vm.MapAnchorPanel.CloseCommand!.Execute(null);
        Assert.False(vm.IsMapAnchorPanelVisible);
    });

    [Fact]
    [Trait("Scenario", "MAP-031")]
    public void should_fill_nw_se_from_view_and_lock_view_and_persist_when_anchor_saved() => Scenario.StaAsync(async () =>
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: 16);
        var setup = new GMapSetupModel();
        var vm = NavKit.NewVm(map, setup);

        vm.ShowMapAnchorPanelCommand.Execute(null);
        vm.AnchorNwLat = vm.AnchorNwLng = vm.AnchorSeLat = vm.AnchorSeLng = 0;
        vm.SetAnchorFromViewCommand.Execute(null);                        // 「현재 화면 기준」
        var v = map.ViewArea;
        Assert.Equal(v.Lat, vm.AnchorNwLat, 6);
        Assert.Equal(v.Lng, vm.AnchorNwLng, 6);
        Assert.Equal(v.Lat - v.HeightLat, vm.AnchorSeLat, 6);
        Assert.Equal(v.Lng + v.WidthLng, vm.AnchorSeLng, 6);
        Assert.Equal(16, vm.AnchorMinZoom);

        vm.AnchorEnabled = true;
        vm.SaveMapAnchorCommand.Execute(null);                            // 「저장 · 적용」
        await NavKit.WaitUntil(() => map.IsAnchorActive);

        Assert.True(map.IsAnchorActive);
        Assert.False(vm.IsMapAnchorPanelVisible);
        Assert.True(setup.MapAnchor!.IsAvailable);
        var saved = await NavKit.WaitSetting("MapAnchor", t => (bool?)t["IsEnabled"] == true);
        Assert.Equal(vm.AnchorNwLat, (double)saved!.SelectToken("NorthWest.Latitude")!, 6);
        Assert.Equal(vm.AnchorSeLng, (double)saved.SelectToken("SouthEast.Longitude")!, 6);

        var before = map.Position;
        map.Position = new PointLatLng(before.Lat + 0.2, before.Lng + 0.2);   // 보기 잠김 — 밖으로 못 나간다
        NavKit.Pump();
        Assert.Equal(before.Lat, map.Position.Lat, 4);
        Assert.Equal(before.Lng, map.Position.Lng, 4);
    });

    [Fact]
    [Trait("Scenario", "MAP-032")]
    public void should_show_cancel_banner_and_keep_fields_when_escape_pressed_in_anchor_draw() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap();
        var vm = NavKit.NewVm(map);
        vm.AnchorNwLat = 37.6; vm.AnchorNwLng = 126.9; vm.AnchorSeLat = 37.4; vm.AnchorSeLng = 127.1;

        vm.DrawAnchorAreaCommand.Execute(null);                          // 「지도에서 영역 그리기」
        Assert.True(map.IsAnchorDrawMode);
        Assert.Contains("ESC", vm.AimStatusMessage);

        var esc = NavKit.KeyArgs(Key.Escape);
        Scenario.Call(vm, "OnMapPreviewKeyDownForAim", map, esc);

        Assert.True(esc.Handled);
        Assert.False(map.IsAnchorDrawMode);
        Assert.Equal("영역 그리기를 취소했습니다.", vm.AimStatusMessage);
        Assert.Null(Mouse.OverrideCursor);
        Assert.Equal((37.6, 126.9, 37.4, 127.1), (vm.AnchorNwLat, vm.AnchorNwLng, vm.AnchorSeLat, vm.AnchorSeLng));
    });

    [Fact]
    [Trait("Scenario", "MAP-033")]
    public void should_force_and_lock_allow_rotation_when_panel_opened_in_rotation_mode_with_strict() => Scenario.Sta(() =>
    {
        try
        {
            var map = NavKit.NewMap();
            var setup = new GMapSetupModel { MapAnchor = NavKit.Anchor(N, W, S, E, allowRotation: false, strict: true) };
            setup.MapAnchor.IsEnabled = false;   // 저장돼 있지만 아직 끈 상태
            var vm = NavKit.NewVm(map, setup);

            vm.ShowMapAnchorPanelCommand.Execute(null);   // 회전 모드 OFF — 자유 편집
            Assert.True(vm.AnchorStrict);
            Assert.False(vm.AnchorAllowRotation);
            Assert.True(vm.IsAnchorAllowRotationEditable);
            vm.ShowMapAnchorPanelCommand.Execute(null);   // 닫기

            RotationFeature.IsEnabled = true;             // 회전 모드 ON
            vm.ShowMapAnchorPanelCommand.Execute(null);
            Assert.True(vm.AnchorStrict);
            Assert.True(vm.AnchorAllowRotation);          // 자동 체크
            Assert.False(vm.IsAnchorAllowRotationEditable);   // 잠김
        }
        finally { NavKit.ResetRotationFeature(); }
    });
}
