using System.ComponentModel;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>카탈로그 §1 회전 · 기울이기(MAP-016~020 · 024~028). 실제 수식키(Keyboard.Modifiers)는 하드웨어 상태라 흉내 낼 수 없어,
/// 키가 부르는 같은 처리(RotateMap/ResetRotation/StepTiltAngle · 명령)와 그 스텝 상수 · 키 바인딩 표를 단언한다.</summary>
public class RotationTiltScenarioTests
{
    private const double N = 37.55, S = 37.45, W = 126.95, E = 127.05;

    private static (GMapCustomControl map, MapViewModel vm) Rig(int zoom = 18, MapAnchorModel? anchor = null)
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: zoom);
        map.TiltSettings = new MapTiltModel { MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 35 };
        var vm = NavKit.NewVm(map, new GMapSetupModel { MapAnchor = anchor });
        Scenario.Call(vm, "InitializeRotationCommands");
        if (anchor != null) Scenario.Call(vm, "ApplyMapAnchor");
        NavKit.Pump();
        return (map, vm);
    }

    private static void Rotation(Action body) => Scenario.Sta(() => { try { body(); } finally { NavKit.ResetRotationFeature(); } });

    [Fact]
    [Trait("Scenario", "MAP-016")]
    public void should_rotate_by_coarse_step_only_after_rotation_mode_toggled_on() => Rotation(() =>
    {
        var (map, vm) = Rig();
        Assert.Equal(5.0, NavKit.PrivateConst<double>(typeof(GMapCustomControl), "ROTATION_STEP_COARSE"));   // Shift+휠 한 칸
        Assert.False(vm.IsRotationFeatureEnabled);
        map.RotateMap(5);                                       // OFF — 입력 차단
        Assert.Equal(0f, map.Bearing);

        vm.ToggleRotationFeatureCommand!.Execute(null);         // 「지도 회전 모드(Ctrl+Shift+R)」
        Assert.True(vm.IsRotationFeatureEnabled);
        map.RotateMap(5); map.RotateMap(5);
        Assert.Equal(10.0, map.MapRotation, 3);
        Assert.Equal(10f, map.Bearing, 3);                      // 나침반 · 표시각이 읽는 canonical 방위
        map.RotateMap(-5);
        Assert.Equal(5f, map.Bearing, 3);
    });

    [Fact]
    [Trait("Scenario", "MAP-017")]
    public void should_rotate_fine_and_coarse_steps_when_ctrl_arrow_rotation_applied() => Rotation(() =>
    {
        var (map, _) = Rig();
        Assert.Equal(1.0, NavKit.PrivateConst<double>(typeof(GMapCustomControl), "ROTATION_STEP_FINE"));     // Ctrl+Shift+←/→
        RotationFeature.IsEnabled = true;
        map.RotateMap(-NavKit.PrivateConst<double>(typeof(GMapCustomControl), "ROTATION_STEP_COARSE"));      // Ctrl+←
        Assert.Equal(-5f, map.Bearing, 3);
        map.RotateMap(NavKit.PrivateConst<double>(typeof(GMapCustomControl), "ROTATION_STEP_FINE"));         // Ctrl+Shift+→
        Assert.Equal(-4f, map.Bearing, 3);
        map.RotateMap(-178);                                                                                // 경계 넘어도 canonical [-180,180)
        Assert.Equal(178f, map.Bearing, 3);
    });

    [Fact]
    [Trait("Scenario", "MAP-018")]
    public void should_keep_angle_and_block_input_when_rotation_mode_turned_off_at_30() => Rotation(() =>
    {
        var (map, vm) = Rig();
        vm.ToggleRotationFeatureCommand!.Execute(null);
        map.SetMapRotation(30);
        Assert.Equal(30f, map.Bearing, 3);

        vm.ToggleRotationFeatureCommand.Execute(null);          // 끔
        Assert.False(vm.IsRotationFeatureEnabled);
        Assert.Equal(30f, map.Bearing, 3);                      // 각도 유지
        map.RotateMap(5);                                       // Shift+휠 — 막힘
        map.SetMapRotation(60);
        Assert.Equal(30f, map.Bearing, 3);
        Assert.Equal(30.0, map.MapRotation, 3);                 // DP 도 직전 canonical 로 되돌림
    });

    [Fact]
    [Trait("Scenario", "MAP-019")]
    public void should_return_to_north_when_reset_rotation_even_with_rotation_mode_off() => Rotation(() =>
    {
        var (map, vm) = Rig();
        RotationFeature.IsEnabled = true;
        map.SetMapRotation(40);
        RotationFeature.IsEnabled = false;                      // 끈 뒤에도 정북 복귀는 항상 허용
        Assert.True(vm.ResetRotationCommand!.CanExecute(null));
        vm.ResetRotationCommand.Execute(null);                  // Ctrl+R · 나침반 N(SetMapRotation(0))
        Assert.Equal(0f, map.Bearing);
        Assert.Equal(0.0, map.MapRotation);

        RotationFeature.IsEnabled = true;
        map.SetMapRotation(-25);
        map.SetMapRotation(0);                                  // 나침반 N 클릭 경로
        Assert.Equal(0f, map.Bearing);
    });

    [Fact]
    [Trait("Scenario", "MAP-020")]
    public void should_disable_rotation_toggle_with_reason_tooltip_when_site_anchor_locks_rotation() => Rotation(() =>
    {
        RotationFeature.IsEnabled = true;
        var (map, vm) = Rig(zoom: 16, anchor: NavKit.Anchor(N, W, S, E, allowRotation: false));
        Assert.Equal(0f, map.Bearing);                          // A모드 = 정북 강제
        Assert.True(map.IsRotationLockedByAnchor);
        Assert.False(vm.IsRotationToggleEnabled);               // 툴바 토글 IsEnabled
        Assert.False(vm.ResetRotationCommand!.CanExecute(null));

        vm.ToggleRotationFeatureCommand!.Execute(null);         // 잠김 — 바뀌지 않는다
        Assert.True(RotationFeature.IsEnabled);
        map.RotateMap(5);
        Assert.Equal(0f, map.Bearing);

        // 사유 툴팁 — 꺼졌을 때만 바꿔 다는 스타일 트리거(MapView.xaml)
        var toggle = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "ToggleButton"
            && (string?)e.Attribute("Command") == "{Binding ToggleRotationFeatureCommand}");
        Assert.Equal("{Binding IsRotationToggleEnabled}", (string?)toggle.Attribute("IsEnabled"));
        var disabledTip = toggle.Descendants().Where(e => e.Name.LocalName == "Trigger" && (string?)e.Attribute("Property") == "IsEnabled" && (string?)e.Attribute("Value") == "False")
            .SelectMany(t => t.Elements()).Single(s => (string?)s.Attribute("Property") == "ToolTip");
        Assert.Equal("지도 회전 모드 — 사이트 고정 중에는 바꿀 수 없습니다(고정을 먼저 해제하세요)", (string?)disabledTip.Attribute("Value"));
    });

    [Fact]
    [Trait("Scenario", "MAP-024")]
    public void should_step_tilt_by_five_and_disable_step_buttons_at_zero_and_35() => Scenario.Sta(() =>
    {
        var (map, vm) = Rig();
        Assert.False(vm.StepTiltAngleCommand.CanExecute("5"));   // OFF 면 ± 단추 꺼짐
        vm.ToggleTiltFeatureCommand.Execute(null);
        Assert.Equal(20.0, vm.TiltAngleDeg, 6);
        for (int i = 0; i < 3; i++) vm.StepTiltAngleCommand.Execute("5");
        Assert.Equal(35.0, vm.TiltAngleDeg, 6);
        Assert.False(vm.StepTiltAngleCommand.CanExecute("5"));   // 상한 — StepUp 꺼짐
        Assert.True(vm.StepTiltAngleCommand.CanExecute("-5"));
        for (int i = 0; i < 7; i++) vm.StepTiltAngleCommand.Execute("-5");
        Assert.Equal(0.0, vm.TiltAngleDeg, 6);
        Assert.False(vm.StepTiltAngleCommand.CanExecute("-5"));  // 하한 — StepDown 꺼짐
        vm.StepTiltAngleCommand.Execute("-5");                    // 더 눌러도 0
        Assert.Equal(0.0, map.RequestedTiltDeg, 6);
    });

    [Fact]
    [Trait("Scenario", "MAP-025")]
    public void should_reopen_angle_popup_when_angle_button_used_and_refuse_when_tilt_off() => Scenario.Sta(() =>
    {
        var (_, vm) = Rig();
        vm.ToggleTiltFeatureCommand.Execute(null);
        Assert.True(vm.IsTiltAnglePopupOpen);                     // 켜면 자동 열림
        vm.IsTiltAnglePopupOpen = false;                          // 팝업 닫힘(StaysOpen=False)
        vm.ToggleTiltAnglePopupCommand.Execute(null);             // 「각도」 단추
        Assert.True(vm.IsTiltAnglePopupOpen);

        vm.ToggleTiltFeatureCommand.Execute(null);                // 끔
        Assert.False(vm.IsTiltAnglePopupOpen);
        vm.IsTiltAnglePopupOpen = true;                           // 끈 상태 — 열리지 않는다
        Assert.False(vm.IsTiltAnglePopupOpen);
        var angleButton = NavKit.MapViewXaml().Descendants().Single(e => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value == "GMaps.Tilt.AngleButton");
        Assert.Equal("{Binding IsTiltFeatureEnabled}", (string?)angleButton.Attribute("IsEnabled"));   // 끄면 단추 꺼짐
    });

    [Fact]
    [Trait("Scenario", "MAP-026")]
    public void should_toggle_and_step_tilt_badge_when_tilt_shortcuts_applied() => Scenario.Sta(() =>
    {
        var (map, vm) = Rig();
        Assert.Equal("{Binding ToggleTiltFeatureCommand}", NavKit.MapViewKeyBindings()["Ctrl+Shift+T"]);
        Assert.Equal(5.0, GMapCustomControl.TILT_STEP_COARSE);    // Ctrl+↑/↓
        Assert.Equal(1.0, GMapCustomControl.TILT_STEP_FINE);      // Ctrl+Shift+↑/↓

        vm.ToggleTiltFeatureCommand.Execute(null);                // Ctrl+Shift+T
        NavKit.Pump();
        Assert.Equal("기울임 20°", vm.TiltBadgeText);
        map.StepTiltAngle(GMapCustomControl.TILT_STEP_COARSE);    // Ctrl+↑
        NavKit.Pump();
        Assert.Equal("기울임 25°", vm.TiltBadgeText);
        map.StepTiltAngle(-GMapCustomControl.TILT_STEP_FINE);     // Ctrl+Shift+↓
        NavKit.Pump();
        Assert.Equal("기울임 24°", vm.TiltBadgeText);
        vm.ToggleTiltFeatureCommand.Execute(null);                // Ctrl+Shift+T 다시 — 끔
        NavKit.Pump();
        Assert.False(vm.IsTiltBadgeVisible);
        Assert.Equal(string.Empty, vm.TiltBadgeText);
    });

    [Fact]
    [Trait("Scenario", "MAP-027")]
    public void should_keep_menu_and_toolbar_tilt_state_in_sync_both_ways() => Scenario.Sta(() =>
    {
        var (map, vm) = Rig();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsTiltFeatureEnabled = true;                           // 보기 → 「지도 기울이기」(TwoWay)
        Assert.True(map.IsTiltFeatureEnabled);                    // 툴바 토글(OneWay 원천)
        Assert.Contains(nameof(MapViewModel.IsTiltFeatureEnabled), changed);

        changed.Clear();
        vm.ToggleTiltFeatureCommand.Execute(null);                // 툴바 토글
        Assert.False(map.IsTiltFeatureEnabled);
        Assert.False(vm.IsTiltFeatureEnabled);                    // 메뉴 체크도 꺼짐
        Assert.Contains(nameof(MapViewModel.IsTiltFeatureEnabled), changed);

        var menu = NavKit.MapViewXaml().Descendants().Single(e => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value == "GMaps.Tilt.MenuToggle");
        Assert.Equal("{Binding IsTiltFeatureEnabled, Mode=TwoWay}", (string?)menu.Attribute("IsChecked"));
    });

    [Fact]
    [Trait("Scenario", "MAP-028")]
    public void should_lock_tilt_toggle_and_menu_with_reason_when_anchor_disallows_rotation() => Scenario.Sta(() =>
    {
        var (map, vm) = Rig(anchor: NavKit.Anchor(N, W, S, E, allowRotation: false));
        Assert.False(vm.IsTiltToggleEnabled);
        Assert.False(vm.ToggleTiltFeatureCommand.CanExecute(null));
        Assert.Contains("앵커(정북 고정) 중 사용 불가", vm.TiltToggleToolTip);
        vm.IsTiltFeatureEnabled = true;                           // 메뉴 경로도 막힘
        Assert.False(map.IsTiltFeatureEnabled);

        var (_, vmB) = Rig(anchor: NavKit.Anchor(N, W, S, E, allowRotation: true));   // B모드는 허용
        Assert.True(vmB.IsTiltToggleEnabled);
        Assert.True(vmB.ToggleTiltFeatureCommand.CanExecute(null));
    });
}
