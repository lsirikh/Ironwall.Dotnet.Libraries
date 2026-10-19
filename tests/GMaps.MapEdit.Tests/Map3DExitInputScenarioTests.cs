using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 를 나간 뒤 2D 지도 입력(2026-10-19 사용자 보고 "3D 들어갔다 나왔는데 왼쪽 · 오른쪽 마우스 버튼 다 안 먹히고 휠도 안 된다").
/// 3D 중 무엇을 했든(회전 · 줌 · 패널 · 판 · 카메라 상자 · 빈 곳 클릭 · 편집 모드) 어느 길로 나가든(「나가기」 · 편집 모드 · 로딩 실패 · 설정 끔 ·
/// 이벤트 이동 · 네이티브 창 전환) 나간 직후 ① 지도가 누름을 받을 수 있고(IsHitTestVisible · IsEnabled · 조상 · 3D 화면 접힘 · 일찍 끝내는 상태 없음)
/// ② 휠 · 드래그 · 클릭 처리기가 실제로 지도를 움직이며(이벤트 주입 → 줌 · 위치 변화 · 클릭 통지) ③ 앱 로그에 2D 입력 상태 진단 줄이 남는지 본다.
/// 창 · WebView2 는 만들지 않는다(창 없는 WPF 는 누름 판정 자체가 돌지 않아 ①은 조건으로, ②는 지도에 직접 올린 이벤트로 잰다).
/// </summary>
public class Map3DExitInputScenarioTests
{
    private const string Area = "area-a";

    // ─────────────── 장치 ───────────────

    private static List<string> CaptureInfo(Rig rig)
    {
        var lines = new List<string>();
        rig.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        return lines;
    }

    /// <summary>「3D 로 보기」 → 펼침(재사용 장면). 3D 동안 2D 지도는 누름을 받지 않는다.</summary>
    internal static void Enter(Rig rig)
    {
        if (!rig.Vm.Map3DAreas.Any())
        {
            rig.W.Map.InitializeForBackgroundRendering(1200, 800);   // 벤더 코어 시작(드래그 팬이 돌게 — MapNav 장치와 같음)
            Scenario.Pump();
            rig.Areas(rig.AreaAround(Area));
        }
        rig.W.Map.Position = rig.Center;   // 앞 회차 드래그로 옮긴 가운데를 영역 안으로
        rig.HostReady(Area);
        rig.Gate();
        rig.PressEnter();
        Map3DEntryScenarioTests.SettleEntry(rig);
        Assert.True(rig.Shown);
        Assert.False(rig.W.Map.IsHitTestVisible, "3D 가 보이는 동안 2D 입력은 막혀 있다");
    }

    /// <summary>3D 가 보낸 카메라(사용자 조작) — 앱 로그 17:40 처럼 2D 가 3D 카메라(줌 · 방위)를 따라간다.</summary>
    internal static void Camera3D(Rig rig, double bearing, double zoom)
        => Scenario.Call(rig.Vm, "OnMap3DMessage", Map3DMath.ParseMessage(
               new JObject { ["type"] = "camera", ["reason"] = "user", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["zoom"] = zoom, ["bearing"] = bearing, ["pitch"] = 55 }.ToString()));

    internal static void From3D(Rig rig, JObject msg) => Scenario.Call(rig.Vm, "OnMap3DMessage", Map3DMath.ParseMessage(msg.ToString()));

    /// <summary>
    /// 나간 직후 2D 입력이 살아 있다 — 조건(누름 판정 · 사용 가능 · 조상 · 3D 화면 접힘 · 일찍 끝내는 상태 없음) +
    /// 휠(줌 바뀜) · 드래그(위치 바뀜) · 클릭(OnMapClicked) 처리기 실제 동작.
    /// </summary>
    internal static void AssertMap2DInputAlive(Rig rig, string when)
    {
        var map = rig.W.Map;
        Assert.False(rig.Shown, $"{when}: 2D 로 돌아왔다");
        Assert.True(map.IsHitTestVisible, $"{when}: 지도 누름 판정(IsHitTestVisible)");
        Assert.True(map.IsEnabled, $"{when}: 지도 사용 가능(IsEnabled)");
        for (DependencyObject? d = VisualTreeHelper.GetParent(map); d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u) Assert.True(u.IsHitTestVisible && u.IsEnabled, $"{when}: 조상 {u.GetType().Name} 이 지도 입력을 막는다");
        Assert.False(rig.Vm.Map3DHost.IsRevealed, $"{when}: 3D 화면은 접혔다(지도를 덮지 않는다)");
        Assert.Equal("없음", Scenario.Call(map, "InputGateText"));
        var local = map.ReadLocalValue(UIElement.IsHitTestVisibleProperty);
        Assert.True(local == DependencyProperty.UnsetValue || local is true, $"{when}: 지도에 남은 로컬 값 {local}");

        // 휠 한 칸 위 → 줌이 반 단계 오르고, 아래 → 돌아온다(정수 줌 단계는 벤더가 화면 좌표를 읽어 창 없이 못 잰다 — 같은 OnMouseWheel 처리기의 반 단계 경로)
        double zoom = map.EffectiveZoom;
        Wheel(map, +120);
        Assert.Equal(zoom + 0.5, map.EffectiveZoom);
        Wheel(map, -120);
        Assert.Equal(zoom, map.EffectiveZoom);

        // 빈 곳 좌드래그 → 위치가 바뀐다
        var before = map.Position;
        Pan(map, new GPoint(600, 400), new GPoint(700, 450));
        Assert.NotEqual(before, map.Position);

        // 빈 곳 좌클릭 → 지도 클릭 통지
        int clicks = 0;
        Action<PointLatLng, Point> onClick = (_, _) => clicks++;
        map.OnMapClicked += onClick;
        try
        {
            map.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
            map.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
        }
        finally { map.OnMapClicked -= onClick; }
        Assert.Equal(1, clicks);
    }

    private static void Wheel(GMapCustomControl map, int delta)
    {
        var e = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.MouseWheelEvent };
        map.RaiseEvent(e);
        Assert.True(e.Handled, "휠이 지도에서 소비되지 않았다");
        Scenario.Pump();
    }

    private static readonly FieldInfo CoreField = typeof(GMapControl).GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>빈 곳 좌드래그(MapNav 시험과 같은 방식 — 누름 · 뗌은 실제 라우팅 이벤트, 그 사이 이동은 벤더 OnMouseMove 가 부르는 코어 BeginDrag/Drag).</summary>
    private static void Pan(GMapCustomControl map, GPoint from, GPoint to)
    {
        var core = CoreField.GetValue(map)!;
        void Call(string m, params object[] a) => core.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(core, a);
        map.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Call("BeginDrag", from);
        Call("Drag", new GPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        Call("Drag", to);
        map.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
        Scenario.Pump();
    }

    /// <summary>「건물 높이」 판을 여는 3D building 메시지(판 입력칸 초기값은 생성자 대신 채운다).</summary>
    private static void PickBuilding(Rig rig, int id = 7)
    {
        Scenario.SetField(rig.Vm, "_bhFloorHeightText", "3.0");
        Scenario.SetField(rig.Vm, "_bhFloorsText", "");
        Scenario.SetField(rig.Vm, "_bhHeightText", "");
        From3D(rig, new JObject { ["type"] = "building", ["id"] = id, ["origin"] = "photo", ["height"] = 12.0, ["height_source"] = "overlay",
                                  ["height_from"] = "shadow", ["levels"] = null, ["area_m2"] = 320.4, ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["manual"] = false });
        Assert.NotNull(rig.Vm.BuildingHeightBuilding);
    }

    // ─────────────── 1. 사용자 보고 흐름 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryManual)]
    [InlineData(Map3DModel.EntryAuto)]
    public void should_accept_wheel_drag_click_when_exited_after_rotating_and_zooming_in_3d(string mode)
        => Map3DEntryScenarioTests.Run(mode, rig =>
        {
            Enter(rig);
            // 앱 로그 17:40:14~23 — 3D 에서 돌리고(66°) 줌이 바뀌며 2D 가 따라간다
            foreach (var (b, z) in new[] { (30d, 17.4), (56d, 17.1), (66.4, 17.0) }) Camera3D(rig, b, z);
            rig.Vm.IsLayerPanelVisible = true;   // 왼쪽 레이어 판 열림(관찰 화면과 같음)
            rig.PressExit();
            AssertMap2DInputAlive(rig, "나가기 직후");
            Assert.Equal(17d, rig.W.Map.EffectiveZoom);   // 들어가기 직전 2D 자리
        });

    // ─────────────── 2. 3D 중 연 판 · 창 ───────────────

    public enum During { Nothing, LayerPanel, CameraPopup, EmptyClick, BuildingPanel, BuildingPanelThenCancel }

    [Theory]
    [InlineData(During.Nothing)]
    [InlineData(During.LayerPanel)]
    [InlineData(During.CameraPopup)]
    [InlineData(During.EmptyClick)]
    [InlineData(During.BuildingPanel)]
    [InlineData(During.BuildingPanelThenCancel)]
    public void should_restore_2d_input_when_exited_with_panel_or_window_opened_in_3d(During during)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Scenario.SetField(rig.Vm, "_map3DBuildingPost", (Action<string>)(_ => { }));
            Enter(rig);
            switch (during)
            {
                case During.LayerPanel: rig.Vm.IsLayerPanelVisible = true; break;
                case During.CameraPopup: rig.Vm.CameraPopups.Add(new CameraStreamPopupViewModel(11, "정문 PTZ", rig.Center)); break;
                case During.EmptyClick:   // 3D 빈 곳 클릭 = "지도에서 보기" 강조 해제 · 조립 카드 닫기 경로(생성자 없는 VM — 강조 상태 필드 초기값을 채운다)
                    Scenario.SetField(rig.Vm, "_locateHighlight", Activator.CreateInstance(typeof(MapViewModel).GetField("_locateHighlight", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType));
                    From3D(rig,new JObject { ["type"] = "pick", ["action"] = "click", ["x"] = 300, ["y"] = 200 }); break;
                case During.BuildingPanel: PickBuilding(rig); break;
                case During.BuildingPanelThenCancel: PickBuilding(rig); rig.Vm.CancelBuildingHeightCommand.Execute(null); break;
            }
            rig.PressExit();
            AssertMap2DInputAlive(rig, during.ToString());
            Assert.Null(rig.Vm.BuildingHeightBuilding);   // 판은 3D 와 함께 닫힌다
        });

    /// <summary>
    /// ★ 수정 전 실패 증명 — 「건물 높이」 판이 열린 채 나갈 때 3D 로 보내기(WebView2 PostWebMessageAsJson)가 예외를 던지면
    /// 옛 Leave3D 는 그 자리에서 끊겨 2D("2D" · 「나가기」 없음)인데 지도 IsHitTestVisible=false · 3D 화면 펼친 채로 남았다.
    /// </summary>
    [Fact]
    public void should_restore_2d_input_and_close_panel_when_3d_send_throws_while_exiting()
        => Scenario.Sta(() =>
        {
            var rig = new Rig(Map3DModel.EntryManual);
            Scenario.SetField(rig.Vm, "_map3DBuildingPost", (Action<string>)(_ => { }));
            Enter(rig);
            PickBuilding(rig);
            Scenario.SetField(rig.Vm, "_map3DBuildingPost", (Action<string>)(_ => throw new InvalidOperationException("CoreWebView2 members cannot be accessed after the WebView2 control is disposed.")));

            Exception? thrown = null;
            try { rig.PressExit(); }
            catch (Exception ex) { thrown = ex; }
            Assert.False(rig.Shown, "상태는 2D(「나가기」 없음)");
            Assert.True(rig.W.Map.IsHitTestVisible, $"2D 인데 지도 누름 판정이 꺼진 채 — 3D 화면 펼침={rig.Vm.Map3DHost.IsRevealed}, 예외={thrown?.InnerException?.Message ?? thrown?.Message}");
            Assert.Null(thrown);
            AssertMap2DInputAlive(rig, "3D 로 보내기 실패");
            Assert.Null(rig.Vm.BuildingHeightBuilding);
            Assert.False(rig.Vm.IsMap3DShown);
            Assert.Contains(rig.W.AllErrors, e => e.Contains("3D 로 보내기 실패(격리)"));
        });

    // ─────────────── 3. 나가는 길 전부 ───────────────

    public enum Exit { Button, EditMode, LoadFailed, SettingsOff, HostFallback, EventFlyTo }

    [Theory]
    [InlineData(Exit.Button)]
    [InlineData(Exit.EditMode)]
    [InlineData(Exit.LoadFailed)]
    [InlineData(Exit.SettingsOff)]
    [InlineData(Exit.HostFallback)]
    [InlineData(Exit.EventFlyTo)]
    public void should_restore_2d_input_whichever_way_3d_is_left(Exit exit)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Camera3D(rig, 40, 17.2);
            switch (exit)
            {
                case Exit.Button: rig.PressExit(); break;
                case Exit.EditMode:   // 편집 · 묶음 회전 판은 편집 모드 안 — 켜면 3D 를 접는다(SyncMap3DWithInputModes)
                    Scenario.SetField(rig.Vm, "_isEditModeEnabled", true);
                    rig.W.Map.SetEditMode(true);
                    Scenario.Call(rig.Vm, "SyncMap3DWithInputModes");
                    break;
                case Exit.LoadFailed: Scenario.Call(rig.Vm, "OnMap3DLoadFailed", "3D 화면이 멈췄습니다(RenderProcessExited · Crashed)"); break;
                case Exit.SettingsOff: Scenario.Call(rig.Vm, "Leave3D", null, "settings-off"); break;
                case Exit.HostFallback: Scenario.Call(rig.Vm, "Leave3D", null, "host-fallback"); break;
                case Exit.EventFlyTo: Scenario.Call(rig.Vm, "Leave3D", null, "event-flyto"); break;
            }
            AssertMap2DInputAlive(rig, exit.ToString());
            if (exit == Exit.EditMode)
            {
                Scenario.SetField(rig.Vm, "_isEditModeEnabled", false);   // 편집 모드 끄기 — 다시 운영 모드 입력
                rig.W.Map.SetEditMode(false);
                Scenario.Call(rig.Vm, "SyncMap3DWithInputModes");
                AssertMap2DInputAlive(rig, "편집 모드 끈 뒤");
            }
        });

    [Fact]
    public void should_keep_2d_blocked_by_esc_and_restore_only_by_exit_button_when_3d_shown_in_wpf()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Assert.False(rig.PressEsc(), "WPF 안 3D 는 ESC 로 나가지 않는다(「나가기」 단추로만)");
            Assert.True(rig.Shown);
            Assert.False(rig.W.Map.IsHitTestVisible, "아직 3D — 2D 입력은 막혀 있다");
            rig.PressExit();
            AssertMap2DInputAlive(rig, "단추로 나간 뒤");
            Assert.False(rig.PressEsc(), "2D 에서 ESC 는 3D 경로가 받지 않는다");
            AssertMap2DInputAlive(rig, "2D 에서 ESC 뒤");
        });

    // ─────────────── 4. 막기 · 되돌리기 규칙 ───────────────

    [Fact]
    public void should_restore_2d_input_when_other_code_changed_it_or_3d_revealed_twice_during_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            rig.W.Map.IsHitTestVisible = true;    // 3D 중 다른 코드가 바꿈
            rig.W.Map.IsHitTestVisible = false;
            Scenario.Call(rig.Vm, "Reveal3D");    // 펼치기가 두 번(두 번째는 막힌 값을 "원래 값"으로 적으면 안 된다)
            Assert.False(rig.W.Map.IsHitTestVisible);
            rig.PressExit();
            AssertMap2DInputAlive(rig, "다른 코드 · 두 번 펼침 뒤");
        });

    [Fact]
    public void should_restore_2d_input_every_time_when_entered_and_exited_many_times()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            for (int i = 0; i < 5; i++)
            {
                Enter(rig);
                Camera3D(rig, 10 * i, 17 + 0.1 * i);
                rig.PressExit();
                AssertMap2DInputAlive(rig, $"{i + 1}번째 나가기");
                rig.Gate(); rig.Tick();
            }
        });

    /// <summary>
    /// ★ 사용자 증언(17:55) "줌 단추를 눌러서 풀었다" — 줌 변경은 남은 이미지 끌기(휠 차단 게이트)를 비운다(ResetDragState 백스톱).
    /// 3D 가 보이는 동안 뗌을 못 받아 일시 제스처 상태가 남은 채 나가면 옛 코드는 휠이 계속 막혔다(줌 단추 전까지). 나가기가 같은 정리를 한다.
    /// </summary>
    public enum Stuck { ImageDrag, PanSuppress, RubberBand, LinePress }

    [Theory]
    [InlineData(Stuck.ImageDrag)]
    [InlineData(Stuck.PanSuppress)]
    [InlineData(Stuck.RubberBand)]
    [InlineData(Stuck.LinePress)]
    public void should_clear_stuck_gesture_and_accept_input_when_exited_after_release_was_missed_in_3d(Stuck stuck)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var warnings = new List<string>();
            rig.W.Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => warnings.Add(m));
            Enter(rig);
            var map = rig.W.Map;
            switch (stuck)
            {
                case Stuck.ImageDrag: Scenario.SetField(map, "_isImageDrag", true); Scenario.SetField(map, "_isDragging", true); break;
                case Stuck.PanSuppress: Scenario.GetField<Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.PanSuppressGate>(map, "_panSuppress").OnStrokeCancelled(true); break;
                case Stuck.RubberBand: Scenario.SetField(map, "_isRubberBanding", true); break;
                case Stuck.LinePress: Scenario.SetField(map, "_linePress", (Point?)new Point(10, 10)); break;
            }
            Assert.NotEqual("없음", Scenario.Call(map, "InputGateText"));
            rig.PressExit();
            AssertMap2DInputAlive(rig, $"남은 {stuck}");
            Assert.Contains(warnings, w => w.StartsWith("[Map3D] 나가며 남은 2D 입력 상태 정리:", StringComparison.Ordinal));
        });

    // ─────────────── 5. 진단 줄 ───────────────

    [Fact]
    public void should_log_2d_input_state_now_and_when_idle_after_exit()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = CaptureInfo(rig);
            Enter(rig);
            rig.PressExit();
            var now = Assert.Single(infos, l => l.StartsWith("[Map3D] 2D 입력 상태(exit-button · now)", StringComparison.Ordinal));
            Assert.Contains("hit=True enabled=True", now);
            Assert.Contains("막는조상=없음", now);
            Assert.Contains("가운데=창 밖", now);   // 창 없는 시험 — 실제 앱은 "지도" 또는 덮은 요소 이름
            Assert.Contains("캡처=없음", now);
            Assert.Contains("3D펼침=False", now);
            Assert.EndsWith("지도상태=없음", now);
            Scenario.Pump();
            Assert.Single(infos, l => l.StartsWith("[Map3D] 2D 입력 상태(exit-button · idle) hit=True", StringComparison.Ordinal));
        });

    [Fact]
    public void should_name_blocking_state_in_log_when_2d_input_is_still_blocked_after_exit()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = CaptureInfo(rig);
            Enter(rig);
            rig.W.Deco.IsEnabled = false;             // 조상이 막는 경우(가정) — 진단 줄이 이름으로 짚는다
            rig.PressExit();
            var now = Assert.Single(infos, l => l.Contains("2D 입력 상태(exit-button · now)"));
            Assert.Contains("enabled=False", now);
            Assert.Contains("막는조상=AdornerDecorator", now);
            rig.W.Deco.IsEnabled = true;
            AssertMap2DInputAlive(rig, "조상 풀린 뒤");
        });
}
