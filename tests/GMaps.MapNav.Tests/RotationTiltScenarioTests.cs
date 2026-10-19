using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>카탈로그 §1 회전 · 기울이기(MAP-016~020 · 024~028). 실제 수식키(Keyboard.Modifiers)는 하드웨어 상태라 흉내 낼 수 없어,
/// 키가 부르는 같은 처리(RotateMap/ResetRotation · 명령)와 그 스텝 상수 · 키 바인딩 표를 단언한다.
/// 기울이기(MAP-024~028)는 2026-10-19 제거 — 그 길이 없어졌음을 단언한다.</summary>
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

    // ── 지도 기울이기 제거(2026-10-19, 사용자 요청 "이 기능은 제거해도될 것 같다. 현재 동작도 안되고") ──
    // MAP-024~028 은 예전 기울이기 조작(각도 ± · 각도 판 · 단축키 · 메뉴/툴바 동기 · 앵커 잠금)이었다. 지금은 그 길이 모두 없어졌음을 단언한다.

    private static IEnumerable<string> TiltAutomationIds()
        => NavKit.MapViewXaml().Descendants()
            .Select(e => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value)
            .Where(id => id != null && id.StartsWith("GMaps.Tilt.", StringComparison.Ordinal))!;

    [Fact]
    [Trait("Scenario", "MAP-024")]
    public void should_have_no_tilt_toggle_angle_popup_menu_item_or_badge_when_map_view_loaded()
    {
        Assert.Empty(TiltAutomationIds());                                   // 툴바 토글 · 각도 판(StepDown/AngleSlider/StepUp/AngleText) · 각도 단추 · 보기 메뉴 · 배지
        var xaml = System.IO.File.ReadAllText(NavKit.RepoFile(System.IO.Path.Combine("Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml")));
        Assert.DoesNotContain("Tilt", xaml.Replace("Map.Toolbar.RotateTilt", ""), StringComparison.Ordinal);   // 기울이기 바인딩(명령 · 팝업 · 배지 · 오버스캔) 없음 — 회전 "?" 의 도움말 키 이름만 예외
        Assert.DoesNotContain("PerspectiveMore", xaml, StringComparison.Ordinal);   // 캡처의 아이콘
        Assert.DoesNotContain(NavKit.MapViewXaml().Descendants(), e => (string?)e.Attribute("Header") == "지도 기울이기");   // 보기 메뉴 항목
    }

    [Fact]
    [Trait("Scenario", "MAP-025")]
    public void should_expose_no_tilt_members_on_map_view_model_when_tilt_removed()
    {
        var names = typeof(MapViewModel).GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Where(n => n.Contains("Tilt", StringComparison.Ordinal)).ToList();
        Assert.Empty(names);                                                 // 명령 · 팝업 상태 · 배지 · 각도 · 설정 복원/저장 모두 없음
    }

    [Fact]
    [Trait("Scenario", "MAP-026")]
    public void should_not_bind_ctrl_shift_t_or_tilt_arrow_keys_when_tilt_removed() => Scenario.Sta(() =>
    {
        var keys = NavKit.MapViewKeyBindings();
        Assert.False(keys.ContainsKey("Ctrl+Shift+T"), "Ctrl+Shift+T 가 아직 묶여 있다");
        Assert.DoesNotContain(keys.Values, c => c.Contains("Tilt", StringComparison.Ordinal));
        var control = typeof(GMapCustomControl);
        Assert.NotNull(control.GetMethod("ToggleRotationFeature"));          // 회전 단축키(Ctrl+Shift+R — 컨트롤 키 처리)는 그대로
        Assert.Null(control.GetMethod("ToggleTiltFeature"));                 // 단축키 · 툴바 공용 진입점 없음
        Assert.Null(control.GetMethod("StepTiltAngle"));                     // Ctrl(+Shift)+↑/↓ 각도 처리 없음
        Assert.Null(control.GetField("TILT_STEP_COARSE"));
        Assert.Null(control.GetField("TILT_STEP_FINE"));

        var (map, _) = Rig();
        Assert.False(map.IsTiltFeatureEnabled);                              // 기본 꺼짐 · 0°
        Assert.Equal(0.0, map.TiltDeg);
    });

    [Fact]
    [Trait("Scenario", "MAP-027")]
    public void should_stay_flat_when_saved_tilt_setting_was_on_and_vm_restored() => Scenario.Sta(() =>
    {
        NavKit.ResetSettings();
        var map = NavKit.NewMap(zoom: 18);
        var vm = NavKit.NewVm(map, new GMapSetupModel { MapTilt = new MapTiltModel { IsEnabled = true, AngleDeg = 30, MinZoom = 5, HysteresisSteps = 1, MaxAngleDeg = 35 } });
        Scenario.Call(vm, "InitializeRotationCommands");
        NavKit.Pump();
        Assert.False(map.IsTiltFeatureEnabled, "저장된 IsEnabled=true 를 따라 기울이기가 켜졌다");
        Assert.Equal(0.0, map.TiltDeg);
        Assert.True(map.RenderTransform == null || map.RenderTransform.Value.IsIdentity);
        Assert.Null(NavKit.Setting("MapTilt"));                              // 쓰지도 않는다
    });

    [Fact]
    [Trait("Scenario", "MAP-028")]
    public void should_keep_rotation_toggle_working_when_tilt_removed() => Rotation(() =>
    {
        var (map, vm) = Rig();
        Assert.True(vm.IsRotationToggleEnabled);
        vm.ToggleRotationFeatureCommand!.Execute(null);
        Assert.True(vm.IsRotationFeatureEnabled);
        map.RotateMap(5);
        Assert.Equal(5f, map.Bearing, 3);
        Assert.Equal(0.0, map.TiltDeg);                                      // 회전해도 기울기는 0

        // 툴바 ④ 묶음 = 회전 토글 + "?" 만(기울이기 토글 · 각도 단추 없음)
        var rotate = NavKit.MapViewXaml().Descendants().Single(e => e.Name.LocalName == "ToggleButton"
            && (string?)e.Attribute("Command") == "{Binding ToggleRotationFeatureCommand}");
        var group = rotate.Parent!;
        Assert.Equal(new[] { "ToggleButton", "HelpTip" }, group.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("Map.Toolbar.RotateTilt", (string?)group.Elements().Last().Attribute("HelpKey"));
    });
}
