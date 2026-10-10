using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>카탈로그 §1 줌 · 축척 · 좌표 · 보기 토글 · WINDY(MAP-008/009/034~038/193 · AUX-008/027 · 추가 줄).</summary>
public class ZoomViewScenarioTests
{
    private static T Styled<T>(T control, string themeFile, double w = 400, double h = 80) where T : Control
    {
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{themeFile}", UriKind.Relative) });
        if (!control.Resources.Contains("BooleanToVisibilityConverter")) control.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        control.ApplyTemplate();
        Scenario.Layout(control, w, h);
        return control;
    }

    [Fact]
    [Trait("Scenario", "MAP-008")]
    public void should_show_plus_labels_in_orange_when_zoom_in_passes_max() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 18, maxZoom: 19);
        var vm = NavKit.NewVm(map);
        var zoom = new MapZoomControl { MinZoom = 5, MaxZoom = 19 };
        Styled(zoom, "MapZoomStyle.xaml");
        var label = (TextBlock)zoom.Template.FindName("ZoomLabelText", zoom);
        string Step()
        {
            vm.ZoomInCommand!.Execute(null);
            zoom.Zoom = map.Zoom; zoom.DigitalZoomLevel = map.DigitalZoomLevel;
            return zoom.ZoomLabel;
        }
        var labels = Enumerable.Range(0, 6).Select(_ => (Text: Step(), Orange: zoom.IsSoftZoom, Brush: ((SolidColorBrush)label.Foreground).Color)).ToList();

        Assert.Equal(new[] { "18.5", "19", "19.5", "19.5+", "19.5++", "19.5++" }, labels.Select(l => l.Text));   // 최대 넘어 디지털 사다리 · 끝에서 멈춤
        var orange = (Color)ColorConverter.ConvertFromString("#E67E22");
        Assert.All(labels.Take(3), l => { Assert.False(l.Orange); Assert.NotEqual(orange, l.Brush); });       // 정식 줌(하프 포함)은 주황 아님
        Assert.All(labels.Skip(3), l => { Assert.True(l.Orange); Assert.Equal(orange, l.Brush); });           // "+"/"++" 소프트 밴드 = 주황
        Assert.Equal(20.5, map.EffectiveZoom, 6);
    });

    [Fact]
    [Trait("Scenario", "MAP-009")]
    public void should_stop_at_min_zoom_without_error_when_zoom_out_repeated() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 13, minZoom: 10);
        var vm = NavKit.NewVm(map);
        for (int i = 0; i < 30; i++) vm.ZoomOutCommand!.Execute(null);
        Assert.Equal(10.0, map.EffectiveZoom, 6);
        Assert.Equal(0, map.DigitalZoomLevel);

        // 사이트 고정의 최소줌 하한이 있으면 그 줌에서 멈춘다
        var anchored = NavKit.NewMap(zoom: 17, minZoom: 10);
        var vm2 = NavKit.NewVm(anchored, new GMapSetupModel { MapAnchor = NavKit.Anchor(38.5, 126.0, 36.5, 128.0, minZoomFloor: 14) });
        Scenario.Call(vm2, "ApplyMapAnchor");
        for (int i = 0; i < 30; i++) vm2.ZoomOutCommand!.Execute(null);
        Assert.Equal(14.0, anchored.EffectiveZoom, 6);
    });

    [Fact]
    [Trait("Scenario", "MAP-034")]
    public void should_report_korean_wgs84_and_mgrs_that_change_when_cursor_point_moves() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 16, lat: 37.5, lng: 127.0);
        var vm = NavKit.NewVm(map);
        var move = new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent };
        Scenario.Call(vm, "MainMap_MouseMove", map, move);
        var first = (vm.CurrentCoordinatePosition.Latitude, vm.CurrentCoordinatePosition.Longitude, vm.CurrentMGRS, vm.CurrentUTM);

        map.Position = new GMap.NET.PointLatLng(36.0, 128.5);   // 같은 화면 점 아래 지리가 바뀐다
        NavKit.Pump();
        Scenario.Call(vm, "MainMap_MouseMove", map, move);
        var second = (vm.CurrentCoordinatePosition.Latitude, vm.CurrentCoordinatePosition.Longitude, vm.CurrentMGRS, vm.CurrentUTM);

        foreach (var c in new[] { first, second })
        {
            Assert.InRange(c.Latitude, 33.0, 39.0);                // 한반도 범위
            Assert.InRange(c.Longitude, 124.0, 132.0);
            Assert.StartsWith("52S", c.CurrentMGRS);
            Assert.False(string.IsNullOrWhiteSpace(c.CurrentUTM));
        }
        Assert.NotEqual(first.Latitude, second.Latitude);
        Assert.NotEqual(first.CurrentMGRS, second.CurrentMGRS);
    });

    [Fact]
    [Trait("Scenario", "MAP-035")]
    public void should_show_and_hide_mgrs_and_utm_rows_when_ctrl_2_and_3_toggled() => Scenario.Sta(() =>
    {
        var keys = NavKit.MapViewKeyBindings();
        Assert.Equal("{Binding ToggleMGRSCommand}", keys["Ctrl+D2"]);
        Assert.Equal("{Binding ToggleUTMCommand}", keys["Ctrl+D3"]);
        var vm = NavKit.NewVm(NavKit.NewMap());
        var coord = new MapCoordinateControl { MGRS = "52S CG 1 2", UTM = "52S 1 2" };
        Styled(coord, "MapCoordinateStyle.xaml", 400, 120);
        Grid Row(string label) => (Grid)((TextBlock)FindText(coord, label)!).Parent;
        void Sync() { coord.IsShowMGRS = vm.IsShowMGRS; coord.IsShowUTM = vm.IsShowUTM; Scenario.Layout(coord, 400, 120); }

        Sync();
        Assert.Equal(Visibility.Collapsed, Row("MGRS:").Visibility);
        vm.ToggleMGRSCommand!.Execute(null);                       // Ctrl+2
        vm.ToggleUTMCommand!.Execute(null);                        // Ctrl+3
        Sync();
        Assert.True(vm.IsShowMGRSGrid);                            // MGRS 격자도 함께
        Assert.Equal(Visibility.Visible, Row("MGRS:").Visibility);
        Assert.Equal(Visibility.Visible, Row("UTM:").Visibility);
        vm.ToggleMGRSCommand.Execute(null);                        // 되돌림
        vm.ToggleUTMCommand.Execute(null);
        Sync();
        Assert.False(vm.IsShowMGRSGrid);
        Assert.Equal(Visibility.Collapsed, Row("MGRS:").Visibility);
        Assert.Equal(Visibility.Collapsed, Row("UTM:").Visibility);
    });

    private static DependencyObject? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text) return tb;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindText(VisualTreeHelper.GetChild(root, i), text) is { } hit) return hit;
        return null;
    }

    [Fact]
    [Trait("Scenario", "MAP-036")]
    public void should_change_scale_text_when_zoom_changes() => Scenario.Sta(() =>
    {
        var map = NavKit.NewMap(zoom: 15);
        var vm = NavKit.NewVm(map);
        Scenario.Call(vm, "CreateScaleBar");
        var at15 = vm.Scale;
        map.Zoom = 18;
        Scenario.Call(vm, "CreateScaleBar");
        var at18 = vm.Scale;
        map.DigitalZoomLevel = 1;                                  // 18.5 — 라벨 숫자만 ÷1.25
        Scenario.Call(vm, "CreateScaleBar");
        var at185 = vm.Scale;

        Assert.False(string.IsNullOrWhiteSpace(at15));
        Assert.NotEqual(at15, at18);
        Assert.NotEqual(at18, at185);
        Assert.Equal(4, vm.ScalePoints!.Count);
    });

    /// <summary>보기 토글 4종(Ctrl+Shift+C/W/F/X): 키 → 명령 · 토글 · 영속 · 되돌림.</summary>
    [Fact]
    [Trait("Scenario", "MAP-037,MAP-038,AUX-008,AUX-027")]
    public void should_toggle_persist_and_restore_view_instruments_when_ctrl_shift_shortcuts_pressed() => Scenario.StaAsync(async () =>
    {
        NavKit.ResetSettings();
        var keys = NavKit.MapViewKeyBindings();
        Assert.Equal("{Binding ToggleCompassVisibleCommand}", keys["Ctrl+Shift+C"]);
        Assert.Equal("{Binding ToggleWindyIndicatorVisibleCommand}", keys["Ctrl+Shift+W"]);
        Assert.Equal("{Binding ToggleDetFaultVisibleCommand}", keys["Ctrl+Shift+F"]);
        Assert.Equal("{Binding ToggleCenterCrosshairCommand}", keys["Ctrl+Shift+X"]);
        Assert.Equal("{Binding ToggleWGS84Command}", keys["Ctrl+D1"]);
        var menu = NavKit.MapViewXaml().Descendants().Where(e => e.Name.LocalName == "MenuItem").Select(e => (string?)e.Attribute("IsChecked")).ToList();
        foreach (var p in new[] { "IsCompassVisible", "IsWindyIndicatorVisible", "IsDetFaultVisible", "IsCenterCrosshairVisible" })
            Assert.Contains($"{{Binding {p}, Mode=TwoWay}}", menu);   // 메뉴 IsChecked 가 같은 속성

        var vm = NavKit.NewVm(NavKit.NewMap(), new GMapSetupModel { MapInstrumentVisibility = new MapInstrumentVisibilityModel() });
        Scenario.Call(vm, "LoadMapInstrumentsFromSettings");      // 생성자 없이 만든 VM — 필드 초기값 대신 부팅 복원(기본값)으로 시작
        Assert.True(vm.IsCompassVisible); Assert.True(vm.IsWindyIndicatorVisible); Assert.True(vm.IsDetFaultVisible); Assert.False(vm.IsCenterCrosshairVisible);

        vm.ToggleCompassVisibleCommand.Execute(null);             // Ctrl+Shift+C → 숨김
        vm.ToggleWindyIndicatorVisibleCommand.Execute(null);      // Ctrl+Shift+W → 숨김
        vm.ToggleDetFaultVisibleCommand.Execute(null);            // Ctrl+Shift+F → 숨김
        vm.ToggleCenterCrosshairCommand.Execute(null);            // Ctrl+Shift+X → 보임
        Assert.False(vm.IsCompassVisible); Assert.False(vm.IsWindyIndicatorVisible); Assert.False(vm.IsDetFaultVisible); Assert.True(vm.IsCenterCrosshairVisible);
        var saved = await NavKit.WaitSetting("MapInstrumentVisibility", t => (bool?)t["Crosshair"] == true && (bool?)t["Compass"] == false);
        Assert.Equal((false, false, false, true), ((bool)saved!["Compass"]!, (bool)saved["Windy"]!, (bool)saved["DetectionFault"]!, (bool)saved["Crosshair"]!));

        vm.ToggleCompassVisibleCommand.Execute(null);             // ×2 → 원래대로
        vm.ToggleWindyIndicatorVisibleCommand.Execute(null);
        vm.ToggleDetFaultVisibleCommand.Execute(null);
        vm.ToggleCenterCrosshairCommand.Execute(null);
        saved = await NavKit.WaitSetting("MapInstrumentVisibility", t => (bool?)t["Crosshair"] == false && (bool?)t["Compass"] == true);
        Assert.Equal((true, true, true, false), ((bool)saved!["Compass"]!, (bool)saved["Windy"]!, (bool)saved["DetectionFault"]!, (bool)saved["Crosshair"]!));

        // 재시작 복원: 저장된 값이 뷰모델에 그대로 돌아오고, 복원 중에는 저장하지 않는다
        var setup = new GMapSetupModel { MapInstrumentVisibility = new MapInstrumentVisibilityModel { Compass = false, Windy = false, DetectionFault = true, Crosshair = true } };
        var restored = NavKit.NewVm(NavKit.NewMap(), setup);
        Scenario.Call(restored, "LoadMapInstrumentsFromSettings");
        Assert.Equal((false, false, true, true), (restored.IsCompassVisible, restored.IsWindyIndicatorVisible, restored.IsDetFaultVisible, restored.IsCenterCrosshairVisible));
        await Task.Delay(150);
        Assert.Equal(true, (bool?)NavKit.Setting("MapInstrumentVisibility")!["Compass"]);   // 복원이 파일을 덮지 않았다
    });

    [Fact]
    [Trait("Scenario", "MAP-193")]
    public void should_open_windy_panel_and_follow_mode_when_indicator_clicked_and_mode_message_arrives() => Scenario.StaAsync(async () =>
    {
        var (ea, published) = NavKit.Recorder();
        NavKit.ResetSettings();
        var vm = NavKit.NewVm(NavKit.NewMap(), new GMapSetupModel { MapInstrumentVisibility = new MapInstrumentVisibilityModel() }, ea);
        Scenario.Call(vm, "LoadMapInstrumentsFromSettings");
        Assert.True(vm.IsWindyIndicatorVisible);
        Assert.Equal(Ironwall.Dotnet.Libraries.Enums.EnumWindyMode.wind0, vm.WindyMode);
        foreach (var (value, mode) in new[] { (2, Ironwall.Dotnet.Libraries.Enums.EnumWindyMode.wind2), (3, Ironwall.Dotnet.Libraries.Enums.EnumWindyMode.wind3), (9, Ironwall.Dotnet.Libraries.Enums.EnumWindyMode.wind0) })
        {
            await vm.HandleAsync(new ChangeModeWindyMessageModel { Value = value }, CancellationToken.None);
            Assert.Equal(mode, vm.WindyMode);                      // 표시가 모드를 따른다(범위 밖은 평시)
        }
        vm.OpenWindyPanelCommand.Execute(null);                    // 배지 클릭
        Assert.IsType<OpenWindyPanelMessageModel>(Assert.Single(published));
        vm.ToggleWindyIndicatorVisibleCommand.Execute(null);       // Ctrl+Shift+W — 표시 토글
        Assert.False(vm.IsWindyIndicatorVisible);
        vm.ToggleWindyIndicatorVisibleCommand.Execute(null);
        Assert.True(vm.IsWindyIndicatorVisible);
        await NavKit.WaitSetting("MapInstrumentVisibility", t => (bool?)t["Windy"] == true);   // 저장 끝까지(다음 시험과 파일 경합 방지)
    });

    [Fact]
    public void should_route_ctrl_e_and_map_menu_exit_to_confirm_popup() => Scenario.Sta(() =>
    {
        // 카탈로그 §1 「추가」 줄 — 지도 메뉴 종료 Ctrl+E 경로(키 → 명령 → 확인 팝업 발행)
        Assert.Equal("{Binding ExitApplicationCommand}", NavKit.MapViewKeyBindings()["Ctrl+E"]);
        var exitItem = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "종료");
        Assert.Equal("{Binding ExitApplicationCommand}", (string?)exitItem.Attribute("Command"));
        var (ea, published) = NavKit.Recorder();
        var vm = NavKit.NewVm(null, ea: ea);
        vm.ExitApplicationCommand!.Execute(null);
        var popup = Assert.IsType<OpenConfirmPopupMessageModel>(Assert.Single(published));
        Assert.IsType<ExitProgramMessageModel>(popup.MessageModel);
    });

    [Fact]
    public void should_publish_setup_panel_open_when_zoom_range_menu_chosen() => Scenario.Sta(() =>
    {
        // MAP-011 의 앞 절반 — 메뉴가 설정 패널 열기를 요청한다(지도 카드 선택은 호스트 몫 · 헤디드).
        var item = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "MenuItem" && (string?)e.Attribute("Header") == "지도 줌 범위 설정…");
        Assert.Equal("{Binding OpenMapSetupCommand}", (string?)item.Attribute("Command"));
        var (ea, published) = NavKit.Recorder();
        var vm = NavKit.NewVm(null, ea: ea);
        vm.OpenMapSetupCommand.Execute(null);
        Assert.IsType<OpenSetupPanelMessageModel>(Assert.Single(published));
    });
}
