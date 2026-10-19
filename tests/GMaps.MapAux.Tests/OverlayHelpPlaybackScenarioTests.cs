using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-085(추적 설정 창) · MAP-173(측정 "?" × Esc) · MAP-191(겹창 끌기 클램프) · §3.5 AUX-012(재생 패널 → ⚙) · AUX-017(재생 운행) ·
/// AUX-018(추적/재생 "?") · AUX-021(음원 전송 성공/실패).
/// 겹창은 실제 템플릿을 부모 없이 세워 AutomationId 단추를 누르고, "?" 는 창 없는 템플릿 + HWND 없는 입력 원천으로 연다.
/// 재생은 가짜 추적 기록(ITrackPointReader) · 가짜 시계로 실제 엔진 · 실제 재생 겹층(PlaybackOverlayManager)을 돌린다. 스피커 전송은 가짜 방송 서비스.
/// </summary>
public class OverlayHelpPlaybackScenarioTests
{
    // ───────────────────────── 공용 ─────────────────────────

    private sealed class Reader : ITrackPointReader
    {
        public List<ITrackPointModel> Points = new();
        public Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(Points);
    }

    private static T Templated<T>(T control, string theme, double w, double h) where T : Control
    {
        NoPopupHelp.Install(control);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{theme}", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(T)];
        Scenario.Layout(control, w, h);
        return control;
    }

    private static (MapVmRig rig, PlaybackViewModel playback, TrackingSetupViewModel tracking) TrackingRig()
    {
        var rig = MapVmRig.Create();
        var engine = new PlaybackEngine(new FakeClock());
        var playback = new PlaybackViewModel(engine, new Reader(), new PlaybackOverlayManager(engine), new TrackingSetupModel());
        var tracking = new TrackingSetupViewModel(new TrackingSetupModel());
        Scenario.SetField(rig.Vm, "_playbackVm", playback);
        Scenario.SetField(rig.Vm, "_trackingSetupVm", tracking);
        Scenario.Call(rig.Vm, "InitializeNavigationCommands");
        return (rig, playback, tracking);
    }

    // ───────────────────────── MAP-085 · AUX-012 ─────────────────────────

    /// <remarks>재생 버튼(툴바) → 재생 패널 → 머리 ⚙(GMaps.Playback.SettingsButton) → 추적 설정 패널 → ✕. 툴바 단추 자체(HelpText)는 MAP-194 몫.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-085,AUX-012")]
    public void should_open_tracking_settings_from_playback_gear_and_close_it_with_close_button_when_playback_panel_used() => OneSta.Sta(() =>
    {
        var (rig, playback, tracking) = TrackingRig();
        rig.Vm.TogglePlaybackPanelCommand!.Execute(null);                       // 툴바 「추적 재생」
        Assert.True(rig.Vm.IsPlaybackPanelVisible);
        var pb = Templated(rig.Vm.PlaybackPanel!, "PlaybackConsoleStyle.xaml", 470, 550);
        Assert.Same(playback, pb.DataContext);
        Assert.Same(rig.Vm.ToggleTrackingSettingsPanelCommand, pb.SettingsCommand);
        Assert.Contains(Parts.Descendants(pb).OfType<TextBlock>(), t => t.Text == "추적 재생 (Playback)");

        var gear = Parts.ById<Button>(pb, "GMaps.Playback.SettingsButton");
        Parts.Press(gear);
        Assert.True(rig.Vm.IsTrackingSettingsPanelVisible);
        var ts = Templated(rig.Vm.TrackingSettingsPanel!, "TrackingSettingsStyle.xaml", 360, 640);
        Assert.Same(tracking, ts.DataContext);
        Assert.Contains(Parts.Descendants(ts).OfType<TextBlock>(), t => t.Text == "추적 설정");   // 「추적 설정」 글

        Parts.Press(Parts.Descendants(ts).OfType<Button>().First(b => b.Name == "PART_CloseButton"));   // ✕
        Assert.False(rig.Vm.IsTrackingSettingsPanelVisible);
        Assert.True(rig.Vm.IsPlaybackPanelVisible);                             // 재생 패널은 그대로

        Parts.Press(gear);                                                      // 다시 ⚙ = 같은 패널 다시 열림
        Assert.True(rig.Vm.IsTrackingSettingsPanelVisible);
        Assert.Same(ts, rig.Vm.TrackingSettingsPanel);
        Parts.Press(gear);                                                      // ⚙ 한 번 더 = 닫힘(토글)
        Assert.False(rig.Vm.IsTrackingSettingsPanelVisible);

        Parts.Press(Parts.Descendants(pb).OfType<Button>().First(b => b.Name == "PART_CloseButton"));   // 재생 ✕
        Assert.False(rig.Vm.IsPlaybackPanelVisible);
        Assert.False(playback.IsPlaying);
    });

    /// <summary>
    /// 결함 AUX-D6: 카탈로그 MAP-085 「추적 설정 → ✕/Esc → 닫힘」 중 Esc 가 추적 설정 겹창을 닫지 않는다.
    /// TrackingSettingsControl · TrackingSetupViewModel 에 Esc 처리기가 없고(닫기 = CloseCommand 단추뿐) 지도 Esc 경로도 겹창을 보지 않는다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-085")]
    [Trait("Defect", "AUX-D6")]
    public void should_close_tracking_settings_overlay_when_escape_pressed_inside_it() => OneSta.Sta(() =>
    {
        var (rig, _, _) = TrackingRig();
        rig.Vm.ToggleTrackingSettingsPanelCommand!.Execute(null);
        var ts = Templated(rig.Vm.TrackingSettingsPanel!, "TrackingSettingsStyle.xaml", 360, 640);
        var source = new AuxHeadlessSource { RootVisual = ts };
        try
        {
            var inner = Parts.Descendants(ts).OfType<CheckBox>().First();          // 「추적 표시」 체크 — 포커스가 겹창 안
            source.Press(inner, Key.Escape, preview: true);
            source.Press(inner, Key.Escape, preview: false);
            Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Vm, new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.False(rig.Vm.IsTrackingSettingsPanelVisible,
                "추적 설정 겹창에서 Esc(PreviewKeyDown · KeyDown · 지도 Esc 경로)를 눌러도 닫히지 않는다 — 닫기는 ✕ 단추뿐");
        }
        finally { source.RootVisual = null!; }
    });

    // ───────────────────────── AUX-018 ─────────────────────────

    [Fact]
    [Trait("Scenario", "AUX-018")]
    public void should_open_one_tracking_or_playback_callout_at_a_time_and_close_it_with_escape_keeping_panels_open() => OneSta.Sta(() =>
    {
        var (rig, _, _) = TrackingRig();
        rig.Vm.TogglePlaybackPanelCommand!.Execute(null);
        rig.Vm.ToggleTrackingSettingsPanelCommand!.Execute(null);
        var pb = Templated(rig.Vm.PlaybackPanel!, "PlaybackConsoleStyle.xaml", 470, 550);
        var ts = Templated(rig.Vm.TrackingSettingsPanel!, "TrackingSettingsStyle.xaml", 360, 640);
        var root = new Grid();
        var source = new AuxHeadlessSource { RootVisual = root };
        try
        {
            root.Children.Add(pb);   // (Layout 이 끝난 겹창을 한 뿌리 아래로 — 한 번에 하나 규칙은 UI 스레드 단위)
            root.Children.Add(ts);
            Scenario.Layout(root, 900, 700);
            var pbTip = Parts.Descendants(pb).OfType<HelpTip>().Single();
            var tsTip = Parts.Descendants(ts).OfType<HelpTip>().Single();
            Assert.Equal("Help.Map.Playback.Window", AutomationProperties.GetAutomationId(pbTip));
            Assert.Equal("Help.Map.Tracking.Settings", AutomationProperties.GetAutomationId(tsTip));
            Assert.True(pbTip.IsAvailable && tsTip.IsAvailable);

            tsTip.Open();
            Assert.Equal("Help.Map.Tracking.Settings.Body", AutomationProperties.GetAutomationId(tsTip.Callout!));
            Assert.Equal("추적 설정", tsTip.Callout!.Entry!.Title);
            Assert.Contains(tsTip.Callout.Entry.Sections.SelectMany(s => s.Items), i => i.Contains("**최소 줌**"));

            pbTip.Open();                                                          // 재생 "?" 를 열면 추적 "?" 는 닫힌다
            Assert.NotEqual(true, tsTip.IsChecked);
            Assert.Same(pbTip, HelpTip.Current);
            Assert.Equal("Help.Map.Playback.Window.Body", AutomationProperties.GetAutomationId(pbTip.Callout!));
            Assert.Equal("추적 재생", pbTip.Callout!.Entry!.Title);

            Assert.True(source.Press(pbTip, Key.Escape));
            Assert.Null(HelpTip.Current);
            Assert.True(rig.Vm.IsPlaybackPanelVisible && rig.Vm.IsTrackingSettingsPanelVisible);   // 패널은 그대로
            Assert.False(source.Press(pbTip, Key.Escape));
        }
        finally { HelpTip.CloseCurrent(); root.Children.Clear(); source.RootVisual = null!; }
    });

    // ───────────────────────── AUX-017 ─────────────────────────

    private static TrackPointModel Pt(DateTime utc, double lat) => new()
    { TrackId = "a", CameraId = 7, ObservedAt = utc, Latitude = lat, Longitude = 127.0, Label = "person", ThreatLevel = "NORMAL" };

    [Fact]
    [Trait("Scenario", "AUX-017")]
    public void should_move_playback_marker_along_recorded_points_with_speed_and_stop_on_pause_when_playback_runs() => OneSta.StaAsync(async () =>
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        var engine = new PlaybackEngine(clock);
        var t0 = clock.UtcNow.AddMinutes(-10);
        var reader = new Reader { Points = { Pt(t0, 37.5000), Pt(t0.AddSeconds(4), 37.5004), Pt(t0.AddSeconds(8), 37.5008), Pt(t0.AddSeconds(12), 37.5012) } };
        var vm = new PlaybackViewModel(engine, reader, new PlaybackOverlayManager(engine), new TrackingSetupModel());
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        vm.AttachMap(map);
        void Tick(double seconds) { clock.UtcNow = clock.UtcNow.AddSeconds(seconds); Scenario.Call(engine, "OnTick", null, EventArgs.Empty); }
        double Lat() => map.Markers.OfType<GMapTrackingMarker>().Single().Position.Lat;

        vm.PresetCommand.Execute("30");
        for (int i = 0; i < 100 && !vm.HasData; i++) await Task.Delay(10);
        Assert.True(vm.HasData);
        Assert.Equal(37.5000, Lat(), 6);                                          // 불러오면 첫 점에 재생 마커
        Assert.Single(map.Markers.OfType<GMapTrailMarker>());

        vm.PlayPauseCommand.Execute(null);                                        // ▶
        Assert.True(vm.IsPlaying);
        Assert.True(Scenario.GetField<DispatcherTimer>(engine, "_timer").IsEnabled);
        Tick(2);
        Assert.Equal(37.5000, Lat(), 6);                                          // 다음 관측 전 = 제자리
        Tick(2);
        Assert.Equal(37.5004, Lat(), 6);                                          // 1× · 4 s → 둘째 점

        vm.SpeedCommand.Execute("4");                                             // 4× 칩
        Assert.Equal(4, vm.Speed);
        Tick(1);
        Assert.Equal(37.5008, Lat(), 6);                                          // 실제 1 s = 기록 4 s

        vm.PlayPauseCommand.Execute(null);                                        // ⏸
        Assert.False(vm.IsPlaying);
        Assert.Equal("▶", vm.PlayPauseGlyph);
        Assert.False(Scenario.GetField<DispatcherTimer>(engine, "_timer").IsEnabled);   // 멈추면 진행 타이머가 서서 마커가 움직이지 않는다

        vm.SpeedCommand.Execute("0.5");
        vm.PlayPauseCommand.Execute(null);
        Tick(4);                                                                  // 0.5× · 실제 4 s = 기록 2 s(아직 마지막 점 전)
        Assert.Equal(37.5008, Lat(), 6);
        Tick(4);
        Assert.Equal(37.5012, Lat(), 6);                                          // 끝 점 도달
        Assert.False(vm.IsPlaying);                                               // 끝나면 자동 정지
        vm.Close();
        Assert.Empty(map.Markers.OfType<GMapTrackingMarker>());                   // 닫으면 재생 마커 제거
    });

    // ───────────────────────── AUX-021 ─────────────────────────

    private static BroadcastPlayControl PlayPanelTemplated(MapVmRig rig)
        => Templated(rig.Vm.BroadcastPlayPanel!, "BroadcastPlayStyle.xaml", 280, 260);

    private static void TypeAndSend(BroadcastPlayControl panel, string group, string repeat)
    {
        var boxes = Parts.Descendants(panel).OfType<TextBox>().ToList();
        boxes[0].Text = group;
        boxes[1].Text = repeat;
        Parts.Press(Parts.Descendants(panel).OfType<Button>().Single(b => b.Content as string == "전송"));
    }

    [Fact]
    [Trait("Scenario", "AUX-021")]
    public void should_send_play_request_with_typed_group_and_repeat_and_close_panel_without_notice_when_speaker_accepts() => OneSta.StaAsync(async () =>
    {
        var rig = MapVmRig.Create();
        rig.Broadcast.Setup(b => b.PublishPlayAsync(51, 3, 2)).ReturnsAsync(BrokerRequestResult.Ok("ok", "req-1"));
        rig.Vm.ShowBroadcastPlayPanel(51);
        TypeAndSend(PlayPanelTemplated(rig), "3", "2");
        await Task.Delay(20);

        rig.Broadcast.Verify(b => b.PublishPlayAsync(51, 3, 2), Times.Once());   // 그 스피커 · 입력한 파일 그룹 · 반복
        Assert.False(rig.Vm.IsBroadcastPlayPanelVisible);                         // 성공 = 패널 닫힘
        Assert.Null(rig.Vm.BroadcastPlayPanel);
        Assert.DoesNotContain(rig.Sink.Messages, m => m is Ironwall.Dotnet.Libraries.ViewModel.Models.OpenInfoPopupMessageModel);
    });

    [Fact]
    [Trait("Scenario", "AUX-021")]
    public void should_keep_panel_and_show_speaker_reason_then_allow_resend_when_play_request_rejected_or_unanswered() => OneSta.StaAsync(async () =>
    {
        var rig = MapVmRig.Create();
        rig.Broadcast.SetupSequence(b => b.PublishPlayAsync(51, 4, 1))
            .ReturnsAsync(BrokerRequestResult.Fail(EnumBrokerFailure.Rejected, "스피커가 다른 방송 중입니다", "busy"))
            .ReturnsAsync(BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "응답이 없습니다"))
            .ReturnsAsync(BrokerRequestResult.Ok("ok", "req-3"));
        rig.Vm.ShowBroadcastPlayPanel(51);
        var panel = PlayPanelTemplated(rig);

        TypeAndSend(panel, "4", "1");
        await Task.Delay(20);
        Assert.True(rig.Vm.IsBroadcastPlayPanelVisible);                          // 실패 = 패널 유지(다시 보낼 수 있게)
        var notice = Assert.Single(rig.Sink.Messages.OfType<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenInfoPopupMessageModel>());
        Assert.Equal("음원 실행 실패", notice.Title);
        Assert.Equal("스피커가 다른 방송 중입니다", notice.Explain);               // 서버 사유를 그대로

        TypeAndSend(panel, "4", "1");                                             // 무응답
        await Task.Delay(20);
        Assert.Equal(2, rig.Sink.Messages.OfType<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenInfoPopupMessageModel>().Count());
        Assert.True(rig.Vm.IsBroadcastPlayPanelVisible);

        TypeAndSend(panel, "4", "1");                                             // 재시도 성공 → 닫힘
        await Task.Delay(20);
        Assert.False(rig.Vm.IsBroadcastPlayPanelVisible);
        rig.Broadcast.Verify(b => b.PublishPlayAsync(51, 4, 1), Times.Exactly(3));
    });

    // ───────────────────────── MAP-173 ─────────────────────────

    /// <remarks>
    /// 실제 앱에서는 "?" 의 창(Window) 클래스 처리기가 측정의 창 PreviewKeyDown 인스턴스 처리기보다 먼저 돈다(HelpTip.cs:43, r24).
    /// 창을 만들 수 없으므로 HWND 없는 뿌리에 같은 순서(말풍선 처리기 → 측정 처리기)로 붙여 실제 HelpTip · 실제 OnMeasureKeyDown 을 WPF 라우팅으로 돌린다.
    /// 기록: 말풍선이 열린 채 Esc = 말풍선만 닫히고 <b>측정은 취소되지 않는다</b> · 둘째 Esc = 측정 취소.
    /// </remarks>
    [Fact]
    [Trait("Scenario", "MAP-173")]
    public void should_close_measure_callout_first_without_cancelling_measure_then_cancel_measure_with_second_escape() => OneSta.Sta(() =>
    {
        var vm = Scenario.Uninit<MapViewModel>();
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        var deco = new AdornerDecorator { Child = map };
        Scenario.Layout(deco, 640, 480);
        if (Scenario.GetField<object?>(map, "_measureController") == null) Scenario.Call(map, "InitializeMeasure");
        Scenario.SetField(vm, "<MainMap>k__BackingField", map);
        Scenario.SetField(vm, "_eventAggregator", new EventAggregator());
        Scenario.Call(vm, "InitializeMeasureCommands");
        Scenario.Call(vm, "AttachMeasureEvents");

        vm.MeasureLengthCommand!.Execute(null);
        Assert.True(map.IsMeasuring && vm.IsMeasureReadoutVisible);

        // 측정 띠 = Border(범위) 안 "?"(MapView.xaml 의 Help.Map.Measure.Tool)
        var tip = new HelpTip { HelpKey = "Map.Measure.Tool" };
        var band = new Border { Child = new StackPanel { Children = { tip } } };
        tip.Scope = band;
        var root = new Grid { Children = { band } };
        NoPopupHelp.Install(root);
        var source = new AuxHeadlessSource { RootVisual = root };
        try
        {
            Scenario.Layout(root, 400, 120);
            Assert.True(tip.IsAvailable);
            tip.Open();
            Assert.Equal("Help.Map.Measure.Tool.Body", AutomationProperties.GetAutomationId(tip.Callout!));
            Assert.Contains(tip.Callout!.Entry!.Sections.SelectMany(s => s.Items), i => i.Contains("{Esc} = 취소"));
            root.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((s, e) => Scenario.Call(vm, "OnMeasureKeyDown", s, e)));   // 측정의 창 Esc(인스턴스 처리기 · handledEventsToo=false)

            Assert.True(source.Press(tip, Key.Escape));
            Assert.NotEqual(true, tip.IsChecked);                                  // 말풍선 닫힘
            Assert.True(map.IsMeasuring);                                          // 측정은 그대로(취소 안 됨)
            Assert.True(vm.IsMeasureLengthActive);

            Assert.True(source.Press(tip, Key.Escape));                            // 둘째 Esc = 측정 취소
            Assert.False(map.IsMeasuring);
            Assert.False(vm.IsMeasureReadoutVisible);
        }
        finally { HelpTip.CloseCurrent(); source.RootVisual = null!; }
    });

    // ───────────────────────── MAP-191 ─────────────────────────

    /// <summary>겹창을 지도 Canvas 의 ContentPresenter 에 올린다(MapView.xaml 과 같은 구조, 창 없음).</summary>
    private static (Canvas canvas, ContentPresenter host) OnCanvas(FrameworkElement overlay, double left = 100, double top = 100)
    {
        var host = new ContentPresenter { Content = overlay };
        Canvas.SetLeft(host, left); Canvas.SetTop(host, top);
        var canvas = new Canvas { Width = 900, Height = 600, Children = { host } };
        Scenario.Layout(canvas, 900, 600);
        return (canvas, host);
    }

    /// <summary>
    /// 머리를 잡고 (dx, dy) 만큼 끈 것과 같은 상태로 실제 MouseMove 처리기를 부른다 — 마우스 캡처는 창 없이 못 하므로 「눌림 · 직전 위치」만 주입한다
    /// (처리기는 e.GetPosition(canvas) 로 현재 위치를 읽고 직전 위치와의 차이만큼 옮긴다).
    /// </summary>
    private static void DragHeader(FrameworkElement overlay, Canvas canvas, double dx, double dy)
    {
        var cur = Mouse.PrimaryDevice.GetPosition(canvas);
        var state = DragState(overlay);
        Scenario.SetField(state, "_isDragging", true);
        Scenario.SetField(state, "_lastMousePosition", new Point(cur.X - dx, cur.Y - dy));
        overlay.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent });
        Scenario.SetField(state, "_isDragging", false);
    }

    /// <summary>끌기 상태(눌림 · 직전 위치)를 가진 객체 — 겹창 자신, 또는 공용 머리 끌기(Helpers.OverlayHeaderDrag.Attach)를 쓰는 겹창이면
    /// 그 MouseMove 처리기의 주인(사이트 고정 창 · 묶음 회전 판, 2026-10-18 공용화).</summary>
    private static object DragState(FrameworkElement overlay)
    {
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        if (overlay.GetType().GetField("_isDragging", F) != null) return overlay;
        var store = typeof(UIElement).GetProperty("EventHandlersStore", F)!.GetValue(overlay)!;
        var handlers = (RoutedEventHandlerInfo[])store.GetType().GetMethod("GetRoutedEventHandlers")!.Invoke(store, new object[] { UIElement.MouseMoveEvent })!;
        return handlers.Select(h => h.Handler.Target).Single(t => t?.GetType().Name == "OverlayHeaderDrag")!;
    }

    [Fact]
    [Trait("Scenario", "MAP-191")]
    public void should_keep_layer_panel_and_palette_header_inside_map_when_dragged_past_window_edges() => OneSta.Sta(() =>
    {
        // 레이어 패널 — 머리 끌기 클램프(LAY-D3): 패널 폭 전체 · 머리(36) 가 지도 안
        var layer = new LayerPanelControl { Height = 400 };
        var (canvas, host) = OnCanvas(layer);
        DragHeader(layer, canvas, -3000, -3000);                                  // 왼쪽 위 창 끝 너머로
        Assert.Equal(0, Canvas.GetLeft(host), 6);
        Assert.Equal(0, Canvas.GetTop(host), 6);                                  // 지도 위(호스트 제목줄 · 툴바)로 올라가지 않는다
        DragHeader(layer, canvas, 3000, 3000);                                    // 오른쪽 아래 너머로
        Assert.Equal(900 - layer.Width, Canvas.GetLeft(host), 6);
        Assert.Equal(600 - 36, Canvas.GetTop(host), 6);                           // 머리는 다시 잡을 수 있게 남는다
        DragHeader(layer, canvas, -100, -50);                                     // 안쪽 이동은 그대로
        Assert.Equal(900 - layer.Width - 100, Canvas.GetLeft(host), 6);
        Assert.Equal(600 - 36 - 50, Canvas.GetTop(host), 6);

        // 아이콘 팔레트 — 머리 Thumb DragDelta 의 클램프(여백 8)
        var palette = new SymbolPaletteView { Width = 384, Height = 500 };
        palette.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) });
        palette.Style = (Style)palette.Resources[typeof(SymbolPaletteView)];
        var (pCanvas, pHost) = OnCanvas(palette);
        palette.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));     // 렌더 펌프가 없어 Loaded 를 직접(겹창 자리 · Canvas 찾기)
        var thumb = Parts.ById<Thumb>(palette, "GMaps.SymbolPalette.Header");
        thumb.RaiseEvent(new DragDeltaEventArgs(-3000, -3000));
        Assert.Equal(8, Canvas.GetLeft(pHost), 6);
        Assert.Equal(8, Canvas.GetTop(pHost), 6);
        thumb.RaiseEvent(new DragDeltaEventArgs(3000, 3000));
        Assert.Equal(900 - 384 - 8, Canvas.GetLeft(pHost), 6);
        Assert.Equal(600 - 500 - 8, Canvas.GetTop(pHost), 6);
        Assert.True(pCanvas.ActualWidth > 0);
    });

    /// <summary>
    /// 결함 AUX-D7: 카탈로그 MAP-191 「머리를 창 끝으로 → 머리는 닿을 수 있게 · 제목줄 안 덮음」 — 레이어 패널(LAY-D3) · 팔레트만 클램프하고
    /// 상세 · 센서 정보 · 추적 설정 · 재생 · 사이트 고정 겹창은 머리 끌기 위치를 자르지 않아 지도 밖(음수 좌표 · 위쪽 = 호스트 제목줄/툴바 쪽)으로 나간다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-191")]
    [Trait("Defect", "AUX-D7")]
    public void should_keep_every_overlay_header_inside_map_when_dragged_past_top_left_edge() => OneSta.Sta(() =>
    {
        var overlays = new (string Name, FrameworkElement Control)[]
        {
            ("SymbolDetailControl", new SymbolDetailControl()),
            ("SensorInfoPanelControl", new SensorInfoPanelControl()),
            ("TrackingSettingsControl", new TrackingSettingsControl()),
            ("PlaybackConsoleControl", new PlaybackConsoleControl()),
            ("MapAnchorPanelControl", new MapAnchorPanelControl()),
        };
        var escaped = new List<string>();
        foreach (var (name, control) in overlays)
        {
            var (canvas, host) = OnCanvas(control);
            DragHeader(control, canvas, -3000, -3000);
            double left = Canvas.GetLeft(host), top = Canvas.GetTop(host);
            if (left < 0 || top < 0) escaped.Add($"{name}(Left={left:0}, Top={top:0})");
        }
        Assert.True(escaped.Count == 0,
            $"머리를 왼쪽 위 창 끝 너머로 끌면 지도 밖으로 나가 머리를 다시 잡을 수 없다(레이어 패널 · 팔레트는 0/8 로 자름): {string.Join(", ", escaped)}");
    });
}
