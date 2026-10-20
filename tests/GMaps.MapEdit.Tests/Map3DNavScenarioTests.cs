using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 방향 링 · 이동 단추(2026-10-19) 시나리오 — 실제 MapViewModel(MapViewModel.Map3DNav) · 실제 컨트롤(Map3DNavControl) 키보드 경로.
/// 창 · WebView2 는 만들지 않는다(3D 호스트는 ready 상태만 꽂는다 — 보내기는 WebView2 가 없어 조용히 버려진다).
/// </summary>
public class Map3DNavScenarioTests
{
    private const string Area = "area-nav";
    private static readonly DateTime T0 = new(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc);

    private static void Enter(Rig rig)
    {
        if (!rig.Vm.Map3DAreas.Any()) rig.Areas(rig.AreaAround(Area));
        rig.W.Map.Position = rig.Center;
        rig.HostReady(Area);
        rig.Gate();
        rig.PressEnter();
        Map3DEntryScenarioTests.SettleEntry(rig);
        Assert.True(rig.Shown);
    }

    private static void Camera(Rig rig, double bearing)
        => Scenario.Call(rig.Vm, "OnMap3DMessage", Map3DMath.ParseMessage(
               new JObject { ["type"] = "camera", ["reason"] = "user", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["zoom"] = 18, ["bearing"] = bearing, ["pitch"] = 60 }.ToString()));

    private static DateTime _now;
    private static void UseClock(Rig rig, DateTime start)
    {
        _now = start;
        Scenario.SetField(rig.Vm, "_map3DNavClock", (Func<DateTime>)(() => _now));
    }

    private static bool Moving(Rig rig) => Scenario.GetField<Map3DNavMover?>(rig.Vm, "_map3DNavMover")?.IsActive == true;

    [Fact]
    public void should_show_ring_instead_of_compass_only_while_3d_is_shown()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Scenario.SetField(rig.Vm, "_suppressInstrumentSave", true);   // 보기 메뉴 저장(설정 파일 쓰기)은 하지 않는다
            rig.Vm.IsCompassVisible = true;
            Assert.True(rig.Vm.IsCompassShown);
            Assert.False(rig.Vm.IsMap3DShown);

            rig.Vm.IsNotifying = true;   // 생성자 없이 만들면 Caliburn 통지 스위치가 꺼져 있다
            var changed = new List<string?>();
            rig.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            Enter(rig);
            Assert.True(rig.Vm.IsMap3DShown);
            Assert.False(rig.Vm.IsCompassShown, "3D 에서는 나침반 대신 링");
            Assert.Contains(nameof(rig.Vm.IsCompassShown), changed);

            changed.Clear();
            rig.PressExit();
            Assert.True(rig.Vm.IsCompassShown, "2D 로 돌아오면 기존 나침반");
            Assert.Contains(nameof(rig.Vm.IsCompassShown), changed);

            rig.Vm.IsCompassVisible = false;   // 보기 메뉴에서 끄면 2D 에서도 숨김
            Assert.False(rig.Vm.IsCompassShown);
            rig.Vm.IsCompassVisible = true;
        });

    [Fact]
    public void should_turn_ring_with_3d_camera_bearing_when_camera_messages_arrive()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Camera(rig, 90);
            Assert.Equal(-90, rig.Vm.Map3DNavRingAngle, 9);
            Assert.Equal("방위 90°", rig.Vm.Map3DNavBearingText);
            Camera(rig, 359.6);
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);
            Camera(rig, 0);
            Assert.Equal(0, rig.Vm.Map3DNavRingAngle, 9);
        });

    [Fact]
    public void should_turn_ring_ahead_of_3d_when_nudged_and_snap_north_when_n_pressed()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Camera(rig, 40);
            rig.Vm.NudgeMap3D(new Map3DNudge(-50, 0, 0, 0, 0));   // 링 끌기 — 보낸 만큼 먼저 돈다(40 − 50 = 350)
            Assert.Equal("방위 350°", rig.Vm.Map3DNavBearingText);
            Assert.Equal(-350, rig.Vm.Map3DNavRingAngle, 9);
            rig.Vm.Map3DNorthCommand.Execute(null);
            Assert.Equal(0, rig.Vm.Map3DNavRingAngle, 9);
            rig.Vm.Map3DTopViewCommand.Execute(null);          // 위에서 · 비스듬히는 방위를 바꾸지 않는다
            rig.Vm.Map3DDefaultViewCommand.Execute(null);
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);
            Camera(rig, 12);                                    // 3D 가 알려 주면 그 값
            Assert.Equal("방위 12°", rig.Vm.Map3DNavBearingText);
        });

    [Fact]
    public void should_ignore_nav_in_2d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            UseClock(rig, T0);
            rig.Vm.NudgeMap3D(new Map3DNudge(30, 0, 0, 0, 0));
            rig.Vm.Map3DNorthCommand.Execute(null);
            rig.Vm.StartMap3DMove(Map3DNavMove.Forward);
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);
            Assert.False(Moving(rig), "2D 에서는 이동을 시작하지 않는다");
        });

    [Fact]
    public void should_keep_moving_while_held_and_stop_on_release_or_exit()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            rig.Vm.StartMap3DMove(Map3DNavMove.Forward);
            Assert.True(Moving(rig));
            _now = T0.AddMilliseconds(40);
            Scenario.Call(rig.Vm, "TickMap3DMove");
            Assert.True(Moving(rig));
            rig.Vm.StopMap3DMove(Map3DNavMove.Forward);
            Assert.False(Moving(rig), "떼면 멈춘다");

            rig.Vm.StartMap3DMove(Map3DNavMove.ZoomIn);
            rig.Vm.StartMap3DMove(Map3DNavMove.Left);
            rig.Vm.StopMap3DMove(Map3DNavMove.ZoomIn);
            Assert.True(Moving(rig), "다른 것을 아직 누르고 있다");
            rig.PressExit();
            Assert.False(Moving(rig), "3D 를 나가면 누르고 있던 이동을 멈춘다");
            _now = T0.AddSeconds(1);
            Scenario.Call(rig.Vm, "TickMap3DMove");
            Assert.False(Moving(rig));
        });

    [Fact]
    public void should_turn_tilt_north_and_move_by_keyboard_when_nav_panel_has_focus()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            Camera(rig, 0);
            var ctrl = new Map3DNavControl { DataContext = rig.Vm };
            var source = new HeadlessSource { RootVisual = ctrl };

            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.Right, down: true));
            Assert.Equal("방위 15°", rig.Vm.Map3DNavBearingText);
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.Left, down: true));
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.Left, down: true));
            Assert.Equal("방위 345°", rig.Vm.Map3DNavBearingText);
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.Up, down: true));   // 기울기(방위 그대로)
            Assert.Equal("방위 345°", rig.Vm.Map3DNavBearingText);
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.N, down: true));
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);

            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.W, down: true));
            Assert.True(Moving(rig));
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.W, down: false));
            Assert.False(Moving(rig));
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.OemPlus, down: true));
            Assert.True(Moving(rig));
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.OemPlus, down: false));

            Assert.False(PressKey(ctrl, source, System.Windows.Input.Key.Escape, down: true), "Esc 는 이 판이 먹지 않는다");
            Assert.False(PressKey(ctrl, source, System.Windows.Input.Key.Q, down: true));
        });

    [Fact]
    public void should_move_panel_not_camera_when_arrow_pressed_on_move_grip()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Camera(rig, 0);
            var ctrl = new Map3DNavControl { DataContext = rig.Vm };
            var source = new HeadlessSource { RootVisual = ctrl };
            var grip = FindThumb(ctrl, "GMaps.Map3D.Nav.Move");
            var offset = Assert.IsType<TranslateTransform>(ctrl.RenderTransform);

            Assert.True(PressKey(grip, source, System.Windows.Input.Key.Right, down: true));
            Assert.True(PressKey(grip, source, System.Windows.Input.Key.Up, down: true));
            Assert.Equal((16d, -16d), (offset.X, offset.Y));
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);   // 손잡이 화살표는 카메라를 돌리지 않는다
            Assert.True(PressKey(grip, source, System.Windows.Input.Key.Home, down: true));
            Assert.Equal((0d, 0d), (offset.X, offset.Y));
            Assert.False(PressKey(grip, source, System.Windows.Input.Key.Escape, down: true), "끄는 중이 아니면 Esc 를 먹지 않는다");
            Assert.True(PressKey(ctrl, source, System.Windows.Input.Key.Right, down: true));   // 손잡이 밖 화살표는 예전처럼 방향 돌리기
            Assert.Equal("방위 15°", rig.Vm.Map3DNavBearingText);
            Assert.Equal((0d, 0d), (offset.X, offset.Y));
        });

    [Fact]
    public void should_draw_shadow_on_separate_layer_when_nav_xaml_read()
    {
        var xaml = Read("Views", "Maps", "Map3DNavControl.xaml");
        Assert.Contains("UseLayoutRounding=\"True\"", xaml);
        var content = Regex.Match(xaml, @"<Border\s+Padding=""6""\s+AutomationProperties.AutomationId=""GMaps.Map3D.Nav""[^>]*>", RegexOptions.Singleline).Value;
        Assert.Contains("Effect=\"{x:Null}\"", content);   // 내용 판은 효과 없이(그림자는 뒤 빈 판만) — 모서리 · 글자 흐림 방지
        Assert.Contains("AutomationProperties.AutomationId=\"GMaps.Map3D.Nav.Move\"", xaml);
    }

    private static System.Windows.Controls.Primitives.Thumb FindThumb(DependencyObject root, string id)
    {
        System.Windows.Controls.Primitives.Thumb? Find(object node)
        {
            if (node is System.Windows.Controls.Primitives.Thumb t && System.Windows.Automation.AutomationProperties.GetAutomationId(t) == id) return t;
            if (node is not DependencyObject d) return null;
            foreach (var c in LogicalTreeHelper.GetChildren(d)) if (Find(c) is { } hit) return hit;
            return null;
        }
        return Find(root) ?? throw new Xunit.Sdk.XunitException($"{id} 없음");
    }

    private static bool PressKey(UIElement target, PresentationSource source, System.Windows.Input.Key key, bool down)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
        target.RaiseEvent(e);
        return e.Handled;
    }

    // ─────────────── XAML 배선 · 디자인 토큰 · 자동화 id ───────────────

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui" }.Concat(parts).ToArray()));

    [Fact]
    public void should_bind_ring_to_3d_and_compass_to_2d_when_map_view_read()
    {
        var xaml = Read("Views", "Maps", "MapView.xaml");
        var nav = Regex.Match(xaml, @"<local:Map3DNavControl[^>]*/>", RegexOptions.Singleline).Value;
        Assert.Contains("Visibility=\"{Binding IsMap3DShown, Converter={StaticResource BooleanToVisibilityConverter}}\"", nav);
        Assert.Contains("HorizontalAlignment=\"Left\"", nav);     // 왼쪽 아래(2026-10-20 사용자 지시) — 손잡이로 옮길 수 있다
        Assert.Contains("VerticalAlignment=\"Bottom\"", nav);
        var compass = Regex.Match(xaml, @"<ctrl:GMapCompassControl[^>]*/>", RegexOptions.Singleline).Value;
        Assert.Contains("Visibility=\"{Binding IsCompassShown, Converter={StaticResource BooleanToVisibilityConverter}}\"", compass);
        Assert.Single(Regex.Matches(xaml, "<local:Map3DNavControl"));
    }

    [Fact]
    public void should_use_only_theme_tokens_and_spec_automation_ids_in_nav_control()
    {
        var xaml = Read("Views", "Maps", "Map3DNavControl.xaml");
        Assert.DoesNotMatch(@"=""#[0-9A-Fa-f]{3,8}""", xaml);   // 하드코딩 색 없음
        foreach (Match m in Regex.Matches(xaml, @"\b(Fill|Stroke|Foreground|Background|BorderBrush)=""([^""]*)"""))
            Assert.True(m.Groups[2].Value.StartsWith("{DynamicResource ", StringComparison.Ordinal) || m.Groups[2].Value == "Transparent",
                        $"{m.Groups[1].Value}={m.Groups[2].Value} — 토큰(DynamicResource)만");
        Assert.Contains("Style=\"{StaticResource MapFloatingPanelStyle}\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource PanelSecondaryButtonStyle}\"", xaml);
        Assert.Contains("HelpKey=\"Map.Map3D.Nav\"", xaml);
        foreach (var id in new[] { "Ring", "North", "Tilt", "TopView", "DefaultView", "Forward", "Back", "Left", "Right", "ZoomIn", "ZoomOut" })
            Assert.Single(Regex.Matches(xaml, $"AutomationProperties.AutomationId=\"GMaps.Map3D.Nav.{id}\""));
        // 링 · 지구본은 peer 가 있는 Thumb, 나머지는 Button
        Assert.Matches(@"<Thumb\s+AutomationProperties.AutomationId=""GMaps.Map3D.Nav.Ring""", xaml);
        Assert.Matches(@"<Thumb\s+Width=""40""\s+Height=""40""\s+AutomationProperties.AutomationId=""GMaps.Map3D.Nav.Tilt""", xaml);
        // 템플릿 밖 x:Name 없음(CM 바인딩 지시자) — 템플릿 부품 이름(Rim · Letter · Globe · Grip)만
        foreach (Match m in Regex.Matches(xaml, @"x:Name=""([^""]+)"""))
            Assert.Contains(m.Groups[1].Value, new[] { "Rim", "Letter", "Globe", "Grip" });
    }

    [Fact]
    public void should_load_nav_control_and_resolve_shared_panel_styles()
        => Scenario.Sta(() =>
        {
            var ctrl = new Map3DNavControl();
            Assert.NotNull(ctrl.TryFindResource("Map3DNavButtonStyle"));
            Assert.NotNull(ctrl.TryFindResource("MapFloatingPanelStyle"));
        });

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string? path = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
