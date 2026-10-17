using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Xml.Linq;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using PopupMode = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupMode;
using PopupSettings = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupSettings;
using ProtocolProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 지도 카메라 팝업 시나리오 헤드리스 시험(MAP-183~187 · EVM-137)
                  헤디드 원본(호스트 앱 UiTests) — CameraPopupE2ETests.Self(T1 · K1~K4) · DragPtz(S1 · S5) · Modes · TiltRuntimeDiagTests(e)
                  가 화면으로 보던 것을 창 없이 같은 제품 경로로 단언한다:
                  지도 더블클릭 처리기(OnMapMarkerDoubleClicked) → 분기기 → OpenCameraStreamPopupAsync → 팝업 VM → 호스트(가짜) 영상 열기 ·
                  PTZ 준비 / 실제 팝업 컨트롤 입력(헤더 끌기 · 영상 끌기 · ✕) / 실제 지도 팬 처리기 / 호스트 상태 안내 · 브로커 토스트.
                  화면 글 · 보이기는 실제 XAML 조각(MapView.xaml · CameraStreamPopupStyle.xaml)을 시각 트리로 읽어 AutomationId 로 단언한다.
                  창 · 실제 호스트 프로세스 · LibVLC · 카메라 · RTSP · 지도 DB · 서버 · NATS 없음(전부 가짜).
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(MapPopupRig.Collection)]
public sealed class MapCameraPopupScenarioTests
{
    private const string MapXaml = MapPopupRig.MapViewXaml;
    private const string PopupXaml = MapPopupRig.PopupStyleXaml;

    private static readonly PointLatLng SecondCamera = new(37.4996, 126.9990);

    // ───────── 실제 XAML 조각 ─────────

    /// <summary>상자 안 안내판(연결 중 · 연결 안 됨 · 사용할 수 없음 + [다시 시도] · [다시 시작]) — 보이기는 IsVideoMessageVisible.</summary>
    private static Grid VideoMessage(CameraStreamPopupViewModel popup) => RealXaml.LoadAround(PopupXaml, "GMaps.CameraPopup.VideoStatus", 2, popup);

    /// <summary>오른쪽 위 "재생" 표시 — 보이기는 IsVideoPlaying.</summary>
    private static Grid VideoPlaying(CameraStreamPopupViewModel popup) => RealXaml.LoadAround(PopupXaml, "GMaps.CameraPopup.VideoPlaying", 2, popup);

    /// <summary>PTZ 못 하는 이유 배지(⚪) — 보이기는 MultiDataTrigger(IsPtzLoading=False · IsPtzCapable=False).</summary>
    private static Grid PtzReason(CameraStreamPopupViewModel popup) => RealXaml.LoadAround(PopupXaml, "GMaps.CameraPopup.PtzReason", 2, popup);

    /// <summary>지도 연결선 레이어 — MapView.xaml 에서 Line X1="{Binding LineX1}" 을 그리는 ItemsControl 그대로.</summary>
    private static XElement LeaderLayerSource()
        => XDocument.Load(MapPopupRig.RepoFile(MapXaml)).Descendants(RealXaml.Pres + "ItemsControl")
                    .Single(ic => ic.Descendants(RealXaml.Pres + "Line").Any(l => (string?)l.Attribute("X1") == "{Binding LineX1}"));

    private static bool Shown(Grid root, string automationId) => RealXaml.IsShown(RealXaml.ById(root, automationId));
    private static string Text(Grid root, string automationId) => RealXaml.TextOf(RealXaml.ById(root, automationId));

    private static IEnumerable<PtzCommand> PtzOf(MapFakeHost host, string cameraId) => host.PtzCommands.Where(c => c.CameraId == cameraId);

    // ═════════════════════════════ MAP-183 ═════════════════════════════

    [Fact]
    [Trait("Scenario", "MAP-183")]
    public void should_play_overlay_and_reopen_at_moved_spot_when_camera_symbol_double_clicked() => MapPopupRig.Sta(async () =>
    {
        // Arrange
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var db = new Mock<IGMapDbService>();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings(), db.Object);
        var marker = MapPopupRig.CameraMarker();

        // Act ① 카메라 심볼 더블클릭(실제 지도 처리기)
        var popup = await MapPopupRig.OpenPopupAsync(vm, host, marker);

        // Assert ① 겹창 열림 — 심볼 우상단(중점 +100 · 위 100 + 상자 높이) · 재생 요청(호스트 오버레이 열기)
        var symbol = MapPopupRig.ScreenOf(map, MapPopupRig.CameraPosition);
        Assert.Equal(symbol.X + 100, popup.CanvasLeft, 6);
        Assert.Equal(symbol.Y - 100 - CameraStreamPopupViewModel.DefaultHeight, popup.CanvasTop, 6);
        Assert.Equal("정문 PTZ", popup.Title);
        var first = Assert.Single(host.Opened);
        Assert.Equal("map-cam-7", first.Request.StreamId);
        Assert.Equal("7", first.Request.Camera?.CameraId);
        Assert.Equal(ProtocolProviderKind.Rtsp, first.Request.Provider?.Kind);
        Assert.StartsWith("rtsp://10.0.0.5/sub", first.Request.Provider?.Uri);
        Assert.Equal((384, 258), (first.Request.Width, first.Request.Height));   // 상자 384 × (300 − 머리 42)
        Assert.Same(first, popup.FrameSource);

        // 실제 XAML — 스트림이 열리는 중이면 "연결 중…" 안내, "재생" 표시는 숨김
        var message = VideoMessage(popup);
        var playing = VideoPlaying(popup);
        Assert.True(Shown(message, "GMaps.CameraPopup.VideoStatus"), "열리는 중인데 상자 안내판(GMaps.CameraPopup.VideoStatus)이 안 보인다");
        Assert.Equal("연결 중…", Text(message, "GMaps.CameraPopup.VideoStatus"));
        Assert.False(Shown(playing, "GMaps.CameraPopup.VideoPlaying"));

        // Act ② 호스트가 재생을 알린다
        first.Set(StreamState.Playing);

        // Assert ② 영상 — "재생" 표시가 보이고 안내판은 사라진다
        Assert.Equal(CameraPopupVideoStatus.Playing, popup.VideoStatus);
        Assert.True(Shown(playing, "GMaps.CameraPopup.VideoPlaying"), "재생인데 GMaps.CameraPopup.VideoPlaying 이 안 보인다");
        Assert.Equal("재생", Text(playing, "GMaps.CameraPopup.VideoPlaying"));
        Assert.False(Shown(message, "GMaps.CameraPopup.VideoStatus"));

        // Act ③ 실제 팝업 컨트롤 머리(Y ≤ 42) 끌기 → 뗌
        var control = new PopupControlRig(popup);
        var opened = (popup.CanvasLeft, popup.CanvasTop);
        control.Down(60, 12);
        // 컨트롤 OnMouseMove 가 하는 일(캔버스 기준 이동량을 CanvasLeft/Top 에 더함) — 창이 없으면 캔버스 기준 마우스 위치를 읽을 수 없어 이동량만 넣는다
        popup.CanvasLeft += 160;
        popup.CanvasTop += 90;
        control.Up();   // 컨트롤 → RaiseDragCompleted → MapViewModel.OnCameraPopupDragCompleted(앵커 재계산 + 저장)

        // Assert ③ 옮긴 자리가 위경도 앵커로 저장된다(다중 관제석 공유 — DB upsert)
        Assert.True(await MapPopupRig.Until(() => db.Invocations.Any(i => i.Method.Name == nameof(IGMapDbService.UpsertCameraPopupPositionAsync))),
                    "머리 끌기를 뗀 뒤 팝업 위치가 저장되지 않았다");
        var saved = (ICameraPopupPositionModel)db.Invocations.Last(i => i.Method.Name == nameof(IGMapDbService.UpsertCameraPopupPositionAsync)).Arguments[0];
        Assert.Equal(7, saved.CameraId);
        Assert.Equal(popup.AnchorGeo.Lat, saved.Latitude, 9);
        Assert.Equal(popup.AnchorGeo.Lng, saved.Longitude, 9);
        var moved = (popup.CanvasLeft, popup.CanvasTop);

        // Act ④ ✕(실제 템플릿 PART_CloseButton) → 닫힘
        control.ClickClose();
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopups.Count == 0), "✕ 뒤 팝업이 닫히지 않았다");
        Assert.True(first.Disposed, "닫았는데 영상(공유 메모리 원본)이 해제되지 않았다");

        // Act ⑤ 다시 더블클릭
        var again = await MapPopupRig.OpenPopupAsync(vm, host, marker, expectedOverlays: 2);

        // Assert ⑤ 옮긴 곳에서 다시 열리고(처음 자리 아님) 다시 재생 요청
        Assert.NotSame(popup, again);
        Assert.InRange(Math.Abs(again.CanvasLeft - moved.CanvasLeft), 0, 1.5);   // 위경도 ↔ 화면 정수 반올림 1px 안
        Assert.InRange(Math.Abs(again.CanvasTop - moved.CanvasTop), 0, 1.5);
        Assert.True(Math.Abs(again.CanvasLeft - opened.CanvasLeft) > 100 && Math.Abs(again.CanvasTop - opened.CanvasTop) > 60,
                    $"다시 연 자리 ({again.CanvasLeft:0.#},{again.CanvasTop:0.#}) 가 옮기기 전 자리 ({opened.CanvasLeft:0.#},{opened.CanvasTop:0.#}) 로 돌아갔다");
        var reopened = host.Opened[1];
        Assert.Equal("map-cam-7", reopened.Request.StreamId);
        reopened.Set(StreamState.Playing);
        Assert.Equal(CameraPopupVideoStatus.Playing, again.VideoStatus);
        Assert.Equal("재생", Text(VideoPlaying(again), "GMaps.CameraPopup.VideoPlaying"));
    });

    // ═════════════════════════════ MAP-184 ═════════════════════════════

    /// <summary>연결선 끝점1 = 카메라 심볼 중점(화면) · 끝점2 = 카메라→팝업 중심 선분이 팝업 테두리와 만나는 점.</summary>
    private static void AssertLeader(GMap.NET.WindowsPresentation.GMapControl map, CameraStreamPopupViewModel popup, Line line, string when)
    {
        var cam = MapPopupRig.ScreenOf((Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms.GMapCustomControl)map, MapPopupRig.CameraPosition);
        Assert.True(Math.Abs(line.X1 - cam.X) < 1e-6 && Math.Abs(line.Y1 - cam.Y) < 1e-6,
                    $"[{when}] 연결선 끝점1 ({line.X1:0.##},{line.Y1:0.##}) 이 카메라 심볼 중점 ({cam.X:0.##},{cam.Y:0.##}) 이 아니다");
        Assert.True(MapPopupRig.OnPopupEdge(line.X2, line.Y2, popup),
                    $"[{when}] 연결선 끝점2 ({line.X2:0.##},{line.Y2:0.##}) 가 팝업 테두리({popup.CanvasLeft:0.##},{popup.CanvasTop:0.##} {popup.PopupWidth}×{popup.PopupHeight}) 위가 아니다");
        double cx = popup.CanvasLeft + popup.PopupWidth / 2, cy = popup.CanvasTop + popup.PopupHeight / 2;
        double cross = (line.X2 - line.X1) * (cy - line.Y1) - (line.Y2 - line.Y1) * (cx - line.X1);
        Assert.True(Math.Abs(cross) < 1e-3, $"[{when}] 끝점2 가 카메라→팝업 중심 선분 위가 아니다(외적 {cross:0.####})");
    }

    [Fact]
    [Trait("Scenario", "MAP-184")]
    public void should_keep_leader_line_from_symbol_to_popup_edge_inside_map_when_panned_and_remove_it_when_closed() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 실제 팬 처리기 결선 + 실제 연결선 레이어(XAML)
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        MapPopupScenario.WirePan(map, vm);
        var popup = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());
        var layer = RealXaml.Load(LeaderLayerSource(), vm);
        var items = layer.Children.OfType<ItemsControl>().Single();
        Line LineNow() { RealXaml.Layout(layer); return Assert.Single(RealXaml.Walk(items).OfType<Line>()); }

        // Assert ① 지도 안 — 연결선 레이어는 지도 영역으로 잘린다(ClipToBounds) · 입력을 막지 않는다 · 점선
        Assert.True(items.ClipToBounds, "연결선 레이어가 지도 영역으로 잘리지 않는다(ClipToBounds=False) — 맵 밖 크롬을 덮는다");
        Assert.False(items.IsHitTestVisible);
        var line = LineNow();
        Assert.NotNull(line.StrokeDashArray);
        Assert.NotEmpty(line.StrokeDashArray);
        AssertLeader(map, popup, line, "열림");

        // Act ② 지도 팬(실제 지도 위치 변경 → 실제 처리기)
        var camBefore = MapPopupRig.ScreenOf(map, MapPopupRig.CameraPosition);
        var popBefore = (popup.CanvasLeft, popup.CanvasTop);
        map.Position = new PointLatLng(MapPopupRig.Lat + 0.0003, MapPopupRig.Lng - 0.0005);
        MapPopupRig.Pump();
        var camAfter = MapPopupRig.ScreenOf(map, MapPopupRig.CameraPosition);

        // Assert ② 심볼이 움직인 만큼 팝업 · 연결선이 따라간다
        Assert.True(Math.Abs(camAfter.X - camBefore.X) > 10 && Math.Abs(camAfter.Y - camBefore.Y) > 10,
                    $"팬이 지도에 반영되지 않았다(심볼 {camBefore} → {camAfter})");
        Assert.InRange(Math.Abs((popup.CanvasLeft - popBefore.CanvasLeft) - (camAfter.X - camBefore.X)), 0, 1.0);
        Assert.InRange(Math.Abs((popup.CanvasTop - popBefore.CanvasTop) - (camAfter.Y - camBefore.Y)), 0, 1.0);
        AssertLeader(map, popup, LineNow(), "팬 뒤");

        // Act ③ 팝업만 옮김(심볼은 그대로 — 심볼 왼쪽 아래로) — 끝점2 가 새 테두리(이번엔 오른쪽 · 위 변)로 따라간다
        var x2Before = LineNow().X2;
        popup.CanvasLeft = camAfter.X - 600;
        popup.CanvasTop = camAfter.Y + 150;
        var moved = LineNow();
        AssertLeader(map, popup, moved, "팝업 이동 뒤");
        Assert.NotEqual(x2Before, moved.X2);

        // Act ④ ✕(실제 컨트롤 PART_CloseButton)
        new PopupControlRig(popup).ClickClose();
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopups.Count == 0), "✕ 뒤 팝업이 닫히지 않았다");
        RealXaml.Layout(layer);

        // Assert ④ 닫힘 — 연결선이 지도에서 사라지고 영상 해제 · 이동 정지 · 포커스 정지가 호스트로 간다
        Assert.Empty(items.Items);
        Assert.Empty(RealXaml.Walk(items).OfType<Line>());
        Assert.True(host.Opened[0].Disposed);
        Assert.Contains(PtzOf(host, "7"), c => c.Operation == PtzOperation.Stop);
        Assert.Contains(host.Sent.Select(s => s.Message).OfType<PtzFocusCommand>(), f => f.CameraId == "7" && f.Direction == 0);
        Assert.Null(vm.SelectedCameraPopup);
    });

    // ═════════════════════════════ MAP-185 ═════════════════════════════

    [Fact]
    [Trait("Scenario", "MAP-185")]
    public void should_send_one_relative_drag_move_when_ptz_overlay_video_dragged_and_none_when_clicked_or_cancelled() => MapPopupRig.Sta(async () =>
    {
        // Arrange — PTZ 카메라(더미 PTZ: PreparePtz → PtzCapable)
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost { PtzCapable = true };
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        var popup = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());
        Assert.True(popup.IsPtzCapable);
        Assert.Null(popup.PtzUnavailableReason);
        Assert.False(Shown(PtzReason(popup), "GMaps.CameraPopup.PtzReason"), "PTZ 가능한데 막힌 이유 배지가 보인다");
        var control = new PopupControlRig(popup);

        // Act ① 영상 상자 안 끌기 — 오른쪽 96px · 위 25.8px(상자 384×258)
        control.DragVideo(100, 100, 196, 74.2);

        // Assert ① 상대 이동 한 건(DragMove · 끈 길이 비율) — 연속 이동 · 정지 없음
        var move = Assert.Single(host.PtzCommands);
        Assert.Equal(PtzOperation.DragMove, move.Operation);
        Assert.Equal("7", move.CameraId);
        Assert.Equal(0.25, move.ViewX, 6);
        Assert.Equal(-0.1, move.ViewY, 6);
        Assert.Equal(384d / 258d, move.ViewAspect, 6);
        Assert.Equal(ProtocolProviderKind.Rtsp, move.Provider?.Kind);
        Assert.Contains(host.Sent, s => ReferenceEquals(s.Message, move) && s.Key == "ptz:7");   // 최신 우선 합치기 키

        // Act ② 8 DIU 안 짧은 끌기(= 클릭) · ③ 끄는 중 캡처 잃음(= 취소)
        control.DragVideo(100, 100, 104, 105);
        control.Down(100, 100);
        control.Move(250, 100);
        control.Control.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        control.Up();

        // Assert ②③ 명령 0
        Assert.Single(host.PtzCommands);
    });

    [Fact]
    [Trait("Scenario", "MAP-185")]
    public void should_show_reason_and_send_no_ptz_when_fixed_or_unpermitted_camera_overlay_dragged() => MapPopupRig.Sta(async () =>
    {
        // Arrange ① 고정 카메라(PreparePtz → PtzCapable=false)
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost { PtzCapable = false };
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        var popup = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());

        // Assert ① 사유 — "PTZ 지원안함" 배지가 영상 위에 보인다
        Assert.False(popup.IsPtzCapable);
        Assert.Equal(CameraStreamPopupViewModel.PtzReasonNotSupported, popup.PtzUnavailableReason);
        var badge = PtzReason(popup);
        Assert.True(Shown(badge, "GMaps.CameraPopup.PtzReason"), "고정 카메라인데 막힌 이유 배지(GMaps.CameraPopup.PtzReason)가 안 보인다");
        Assert.Equal("PTZ 지원안함", Text(badge, "GMaps.CameraPopup.PtzReason"));

        // Act ① 영상 끌기 · 휠
        var control = new PopupControlRig(popup);
        control.DragVideo(100, 100, 250, 100);
        control.Control.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });

        // Assert ① PTZ 명령 0
        Assert.Empty(host.PtzCommands);

        // Arrange ② PTZ 카메라지만 cameras:control 권한 없음
        var host2 = new MapFakeHost { PtzCapable = true };
        var vm2 = MapPopupRig.NewVm(MapPopupRig.NewMap(), host2, MapPopupRig.SelfSettings(), canControlCamera: false);
        var popup2 = await MapPopupRig.OpenPopupAsync(vm2, host2, MapPopupRig.CameraMarker());

        // Assert ② 사유 "PTZ 권한 없음"
        Assert.False(popup2.IsPtzCapable);
        Assert.Equal(CameraStreamPopupViewModel.PtzReasonNoPermission, popup2.PtzUnavailableReason);
        var badge2 = PtzReason(popup2);
        Assert.True(Shown(badge2, "GMaps.CameraPopup.PtzReason"));
        Assert.Equal("PTZ 권한 없음", Text(badge2, "GMaps.CameraPopup.PtzReason"));

        // Act ② 컨트롤 끌기 + 컨트롤 게이트를 지나친 요청이 지도 VM 에 직접 와도(이중 방어)
        new PopupControlRig(popup2).DragVideo(100, 100, 250, 100);
        typeof(CameraStreamPopupViewModel).GetMethod("RaisePtzDrag", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup2, new object[] { 150d, 0d, 384d, 258d });

        // Assert ② PTZ 명령 0
        Assert.Empty(host2.PtzCommands);
    });

    // ═════════════════════════════ MAP-186 ═════════════════════════════

    [Fact]
    [Trait("Scenario", "MAP-186")]
    public void should_show_host_notice_with_restart_on_map_when_host_suspended_and_restart_host_from_it() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 활성화 때 하는 호스트 상태 구독(StartCameraPopupHostNotice) + 실제 지도 하단 안내 · 토스트 XAML + 열린 팝업 하나
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        MapPopupRig.Call(vm, "StartCameraPopupHostNotice");
        var notice = RealXaml.LoadAround(MapXaml, "Map.CameraPopup.HostNotice", 2, vm);
        var toast = RealXaml.LoadAround(MapXaml, "Map.CameraPopup.Toast", 2, vm);
        var popup = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());
        var source = host.Opened[0];
        source.Set(StreamState.Playing);
        var overlay = VideoMessage(popup);
        Assert.False(Shown(notice, "Map.CameraPopup.HostNotice"), "정상인데 지도 하단 호스트 안내가 보인다");

        // Act ① 호스트 죽음 → 감시자 재시작 중
        host.ChangeState(CameraPopupHostState.Restarting);

        // Assert ① 지도 안내는 아직 없음(재시작 중) · 상자는 "연결 중…" + [다시 시작]
        Assert.False(Shown(notice, "Map.CameraPopup.HostNotice"));
        Assert.Equal("연결 중…", Text(overlay, "GMaps.CameraPopup.VideoStatus"));
        Assert.True(Shown(overlay, "GMaps.CameraPopup.HostRestart"));

        // Act ② 재시작 예산 소진 → 일시 중지
        host.ChangeState(CameraPopupHostState.Suspended);

        // Assert ② 지도 하단 "영상 기능 일시 중지" + [다시 시작](오버레이와 별개로 계속 떠 있다)
        Assert.True(Shown(notice, "Map.CameraPopup.HostNotice"), "일시 중지인데 Map.CameraPopup.HostNotice 가 안 보인다");
        Assert.Equal("영상 기능 일시 중지", Text(notice, "Map.CameraPopup.HostNotice"));
        Assert.True(Shown(notice, "Map.CameraPopup.HostNotice.Restart"));
        Assert.Equal("다시 시작", Text(notice, "Map.CameraPopup.HostNotice.Restart"));
        Assert.Equal("다시 시작", System.Windows.Automation.AutomationProperties.GetName(RealXaml.ById(notice, "Map.CameraPopup.HostNotice.Restart")));
        Assert.Equal("영상 기능을 사용할 수 없습니다", Text(overlay, "GMaps.CameraPopup.VideoStatus"));
        Assert.True(Shown(overlay, "GMaps.CameraPopup.HostRestart"));
        Assert.False(Shown(overlay, "GMaps.CameraPopup.VideoRetry"));

        // Act ③ 지도 [다시 시작] — 실제 XAML 의 cal:Message.Attach 이름이 가리키는 VM 동작 그대로
        var attach = RealXaml.SourceAround(MapXaml, "Map.CameraPopup.HostNotice.Restart", 0)
                             .Attributes().Single(a => a.Name.LocalName == "Message.Attach").Value;
        Assert.Equal(nameof(MapViewModel.RestartCameraPopupHost), attach);
        typeof(MapViewModel).GetMethod(attach, BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes)!.Invoke(vm, null);

        // Assert ③ 호스트 재시작(감시자 예산 비우고 다시 띄움)
        Assert.Equal(1, host.Restarts);

        // Act ④ 상자의 [다시 시작](실제 XAML 단추 Command 바인딩)
        ((Button)RealXaml.ById(overlay, "GMaps.CameraPopup.HostRestart")).Command.Execute(null);
        Assert.Equal(2, host.Restarts);

        // Act ⑤ 다시 뜸 → 감시자가 같은 스트림 복원
        host.ChangeState(CameraPopupHostState.Restarting);
        host.ChangeState(CameraPopupHostState.Running);

        // Assert ⑤ 안내 사라짐 · 상자는 다시 재생 · [다시 시작] 숨김
        Assert.False(Shown(notice, "Map.CameraPopup.HostNotice"));
        Assert.Equal(CameraPopupVideoStatus.Playing, popup.VideoStatus);
        Assert.False(Shown(overlay, "GMaps.CameraPopup.VideoStatus"));
        Assert.False(popup.CanRestartHost);

        // Act ⑥ 호스트 실행 파일 없음(사용할 수 없음)
        host.ChangeState(CameraPopupHostState.Unavailable);

        // Assert ⑥ 계속 떠 있는 안내가 아니라 토스트 한 줄
        Assert.False(Shown(notice, "Map.CameraPopup.HostNotice"));
        Assert.True(await MapPopupRig.Until(() => Shown(toast, "Map.CameraPopup.Toast")), "호스트를 찾지 못했는데 토스트가 안 보인다");
        Assert.Equal(CameraPopupHostNotices.Unavailable, Text(toast, "Map.CameraPopup.Toast"));
    });

    // ═════════════════════════════ MAP-187 ═════════════════════════════

    [Fact]
    [Trait("Scenario", "MAP-187")]
    public void should_show_broker_toast_and_open_no_overlay_when_camera_double_clicked_in_broker_mode() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 브로커 모드 · 실제 브로커 서비스(CameraPopupBrokerService) + 가짜 요청 실행기(네트워크 없음)
        var settings = new FakeOverlaySettings
        {
            Value = new PopupSettings { Mode = PopupMode.Broker, BrokerMonitor = 2, BrokerCell = 5, BrokerResponseTimeoutSeconds = 3 },
        };
        var reply = new TaskCompletionSource<BrokerRequestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<(string Subject, string Command, CameraPopupOpenBodyDto Body)>();
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .Callback<string, string, CameraPopupOpenBodyDto, TimeSpan?, CancellationToken>((s, c, b, _, _) => sent.Add((s, c, b)))
              .Returns(reply.Task);
        var nats = new Mock<INatsSetupModel>();
        nats.SetupGet(n => n.DomainNats).Returns("sensorway");
        nats.SetupGet(n => n.GroupNats).Returns("unit999");
        var broker = new CameraPopupBrokerService(client.Object, nats.Object, new TestLog());
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(MapPopupRig.NewMap(), host, settings, broker: broker);
        var toast = RealXaml.LoadAround(MapXaml, "Map.CameraPopup.Toast", 2, vm);
        Assert.False(Shown(toast, "Map.CameraPopup.Toast"));

        // Act ① 카메라 심볼 더블클릭
        MapPopupRig.DoubleClick(vm, MapPopupRig.CameraMarker());

        // Assert ① 대기 토스트 · CAMERA_POPUP_OPEN 한 건(명세 주제 · 대상 관제석 · 자리) · 지도 겹창 없음
        Assert.True(await MapPopupRig.Until(() => Shown(toast, "Map.CameraPopup.Toast")), "브로커 모드 더블클릭 뒤 Map.CameraPopup.Toast 가 안 보인다");
        Assert.Equal("NVR 관제석에 정문 PTZ 팝업을 요청했습니다", Text(toast, "Map.CameraPopup.Toast"));
        var request = Assert.Single(sent);
        Assert.Equal("sensorway.unit999.nvr_manager.popup", request.Subject);
        Assert.Equal(CameraPopupBrokerService.OpenCommand, request.Command);
        Assert.Equal(7, request.Body.CameraId);
        Assert.Equal("gis-7f3a9c2e41b6", request.Body.TargetClientId);
        Assert.Equal((2, 5), (request.Body.Monitor, request.Body.Cell));
        Assert.Empty(vm.CameraPopups);
        Assert.Empty(host.Opened);

        // Act ② 응답 전 연타 → 합침(두 번째 요청 없음)
        MapPopupRig.DoubleClick(vm, MapPopupRig.CameraMarker());
        MapPopupRig.Pump();
        Assert.Single(sent);

        // Act ③ NVR Manager 응답(OK · 모니터 2 · 칸 5)
        var body = JObject.FromObject(new { popup_id = "pp-1", monitor = 2, cell = 5 });
        reply.SetResult(BrokerRequestResult.Ok("OK", "req-1",
            BrokerMessageHelper.CreateResponse(body, "req-1", "NVRManager", CameraPopupBrokerService.OpenCommand, "OK").ToJson()));

        // Assert ③ 결과 토스트 · 여전히 지도 겹창 · 영상 열기 없음
        Assert.True(await MapPopupRig.Until(() => Text(toast, "Map.CameraPopup.Toast") == "모니터 2 · 칸 5에 띄웠습니다"),
                    $"응답 뒤 토스트 글이 바뀌지 않았다: '{Text(toast, "Map.CameraPopup.Toast")}'");
        Assert.True(Shown(toast, "Map.CameraPopup.Toast"));
        Assert.Empty(vm.CameraPopups);
        Assert.Empty(host.Opened);
        Assert.Empty(host.Sent);
    });

    [Theory]
    [InlineData(EnumBrokerFailure.Rejected, "칸 사용 중", "NVR 팝업 거부 — 칸 사용 중")]
    [InlineData(EnumBrokerFailure.NoResponse, "응답 없음", "NVR Manager 응답 없음(3초) — 이 관제석에서 돌고 있는지 확인하세요")]
    [Trait("Scenario", "MAP-187")]
    public void should_show_failure_toast_and_open_no_overlay_when_nvr_rejects_or_never_answers(EnumBrokerFailure failure, string reason, string expected)
        => MapPopupRig.Sta(async () =>
        {
            // Arrange — 실제 브로커 서비스 + 거부/무응답을 돌려주는 가짜 실행기
            var settings = new FakeOverlaySettings { Value = new PopupSettings { Mode = PopupMode.Broker, BrokerResponseTimeoutSeconds = 3 } };
            var client = new Mock<IBrokerRequestClient>();
            client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                             It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(BrokerRequestResult.Fail(failure, reason));
            var nats = new Mock<INatsSetupModel>();
            nats.SetupGet(n => n.DomainNats).Returns("sensorway");
            nats.SetupGet(n => n.GroupNats).Returns("unit999");
            var host = new MapFakeHost();
            var vm = MapPopupRig.NewVm(MapPopupRig.NewMap(), host, settings, broker: new CameraPopupBrokerService(client.Object, nats.Object, new TestLog()));
            var toast = RealXaml.LoadAround(MapXaml, "Map.CameraPopup.Toast", 2, vm);

            // Act
            MapPopupRig.DoubleClick(vm, MapPopupRig.CameraMarker());

            // Assert — 결과 토스트 · 지도 겹창 없음
            Assert.True(await MapPopupRig.Until(() => Shown(toast, "Map.CameraPopup.Toast") && Text(toast, "Map.CameraPopup.Toast") == expected),
                        $"토스트 '{Text(toast, "Map.CameraPopup.Toast")}' (기대 '{expected}')");
            Assert.Empty(vm.CameraPopups);
            Assert.Empty(host.Opened);
        });

    [Fact]
    [Trait("Scenario", "MAP-187")]
    public void should_show_no_broker_toast_when_broker_mode_has_no_broker_service() => MapPopupRig.Sta(async () =>
    {
        var settings = new FakeOverlaySettings { Value = new PopupSettings { Mode = PopupMode.Broker } };
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(MapPopupRig.NewMap(), host, settings, broker: null);
        var toast = RealXaml.LoadAround(MapXaml, "Map.CameraPopup.Toast", 2, vm);

        MapPopupRig.DoubleClick(vm, MapPopupRig.CameraMarker());

        Assert.True(await MapPopupRig.Until(() => Shown(toast, "Map.CameraPopup.Toast")), "브로커 창구가 없는데 안내 토스트가 없다(조용한 무동작)");
        Assert.Equal(CameraPopupBrokerToasts.NoBroker, Text(toast, "Map.CameraPopup.Toast"));
        Assert.Empty(vm.CameraPopups);
        Assert.Empty(host.Opened);
    });

    [Fact]
    [Trait("Scenario", "MAP-183")]
    public void should_bring_open_popup_to_front_without_second_stream_when_same_camera_double_clicked_again() => MapPopupRig.Sta(async () =>
    {
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        var marker = MapPopupRig.CameraMarker();
        var front = await MapPopupRig.OpenPopupAsync(vm, host, marker);
        var other = await MapPopupScenario.OpenNthAsync(vm, host, MapPopupRig.CameraMarker(8, "후문 PTZ", SecondCamera), 2);
        Assert.True(other.ZIndex > front.ZIndex);
        Assert.Same(other, vm.SelectedCameraPopup);
        int opens = host.Opened.Count;

        MapPopupRig.DoubleClick(vm, marker);
        Assert.True(await MapPopupRig.Until(() => ReferenceEquals(vm.SelectedCameraPopup, front)), "같은 카메라 재더블클릭이 열린 팝업을 고르지 않았다");

        Assert.Equal(2, vm.CameraPopups.Count);          // 새 상자 없음
        Assert.Equal(opens, host.Opened.Count);           // 영상 다시 열기 없음(재생 유지)
        Assert.True(front.ZIndex > other.ZIndex);         // 맨 앞
        Assert.True(front.IsSelected);
        Assert.False(other.IsSelected);
    });

    // ═════════════════════════════ EVM-137 ═════════════════════════════

    [Fact]
    [Trait("Scenario", "EVM-137")]
    public void should_fail_only_the_dead_stream_popup_and_retry_only_it_when_one_camera_stream_dies() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 카메라 두 대 재생 중
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        var front = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());
        var back = await MapPopupScenario.OpenNthAsync(vm, host, MapPopupRig.CameraMarker(8, "후문 PTZ", SecondCamera), 2);
        MapPopupScenario.SourceOf(host, front).Set(StreamState.Playing);
        MapPopupScenario.SourceOf(host, back).Set(StreamState.Playing);
        var frontMsg = VideoMessage(front);
        var backMsg = VideoMessage(back);
        int opensBefore = host.Opened.Count;

        // Act ① 후문 카메라 스트림만 끊김(호스트가 그 스트림 실패를 알림)
        MapPopupScenario.SourceOf(host, back).Set(StreamState.Failed, "rtsp timeout");

        // Assert ① 그 상자만 "연결 안 됨" + [다시 시도] · 다른 상자는 재생 그대로 · 호스트는 정상
        Assert.Equal(CameraPopupVideoStatus.Failed, back.VideoStatus);
        Assert.True(Shown(backMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.Equal("연결 안 됨", Text(backMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.True(Shown(backMsg, "GMaps.CameraPopup.VideoRetry"));
        Assert.False(Shown(backMsg, "GMaps.CameraPopup.HostRestart"));
        Assert.Equal(CameraPopupVideoStatus.Playing, front.VideoStatus);
        Assert.False(Shown(frontMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.Equal(2, vm.CameraPopups.Count);
        Assert.Equal(CameraPopupHostState.Running, host.State);

        // Act ② 실패한 상자의 [다시 시도](실제 XAML 단추 Command 바인딩)
        ((Button)RealXaml.ById(backMsg, "GMaps.CameraPopup.VideoRetry")).Command.Execute(null);

        // Assert ② 그 스트림만 다시 열고 재생으로 돌아온다
        Assert.Equal(opensBefore + 1, host.Opened.Count);
        Assert.Equal(back.StreamId, host.Opened[^1].Request.StreamId);
        Assert.Single(host.Opened, o => o.StreamId == front.StreamId);
        host.Opened[^1].Set(StreamState.Playing);
        Assert.Equal(CameraPopupVideoStatus.Playing, back.VideoStatus);
        Assert.False(Shown(backMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.Equal(CameraPopupVideoStatus.Playing, front.VideoStatus);
    });

    [Fact]
    [Trait("Scenario", "EVM-137")]
    public void should_keep_gis_alive_show_connecting_and_restore_every_popup_when_host_dies_and_comes_back() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 두 팝업 재생 중
        var map = MapPopupRig.NewMap();
        var host = new MapFakeHost();
        var vm = MapPopupRig.NewVm(map, host, MapPopupRig.SelfSettings());
        var a = await MapPopupRig.OpenPopupAsync(vm, host, MapPopupRig.CameraMarker());
        var b = await MapPopupScenario.OpenNthAsync(vm, host, MapPopupRig.CameraMarker(8, "후문 PTZ", SecondCamera), 2);
        MapPopupScenario.SourceOf(host, a).Set(StreamState.Playing);
        MapPopupScenario.SourceOf(host, b).Set(StreamState.Playing);
        var aMsg = VideoMessage(a);

        // Act ① 호스트 강제 종료 → 감시자 재시작 중
        host.ChangeState(CameraPopupHostState.Restarting);

        // Assert ① GIS(지도 VM)는 그대로 · 두 상자 모두 "연결 중…" + [다시 시작](감시자가 멈춰 서도 사람이 풀 수 있게)
        Assert.Equal(2, vm.CameraPopups.Count);
        Assert.All(new[] { a, b }, p => Assert.Equal(CameraPopupVideoStatus.Connecting, p.VideoStatus));
        Assert.All(new[] { a, b }, p => Assert.True(p.CanRestartHost));
        Assert.Equal("연결 중…", Text(aMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.True(Shown(aMsg, "GMaps.CameraPopup.HostRestart"));
        // 죽은 동안의 PTZ 는 보내지 않는다(GIS 는 던지지 않는다)
        var ex = Record.Exception(() => new PopupControlRig(a).DragVideo(100, 100, 250, 100));
        Assert.Null(ex);
        Assert.Empty(host.PtzCommands);

        // Act ② 다시 뜸(감시자가 같은 스트림 id 로 복원 → 원본이 재생을 알림)
        host.ChangeState(CameraPopupHostState.Running);

        // Assert ② 두 상자 모두 재생으로 복구 · [다시 시작] 숨김 · 새 열기 없이 같은 원본
        Assert.All(new[] { a, b }, p => Assert.Equal(CameraPopupVideoStatus.Playing, p.VideoStatus));
        Assert.All(new[] { a, b }, p => Assert.False(p.CanRestartHost));
        Assert.False(Shown(aMsg, "GMaps.CameraPopup.VideoStatus"));
        Assert.Equal(2, host.Opened.Count);
        new PopupControlRig(a).DragVideo(100, 100, 196, 100);
        Assert.Single(host.PtzCommands);   // 복구 뒤에는 PTZ 가 다시 간다
    });

    private sealed class FixedMonitors : Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.IDisplayMonitorProvider
    {
        private static readonly Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.DisplayMonitorInfo Primary =
            new(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true);
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.DisplayMonitorInfo> GetMonitors() => new[] { Primary };
    }

    [Fact]
    [Trait("Scenario", "EVM-137")]
    public void should_keep_restore_list_and_offer_restart_when_real_supervisor_has_no_host() => MapPopupRig.Sta(async () =>
    {
        // Arrange — 실제 감시자(CameraPopupHostSupervisor) · 호스트 실행 파일 없음(프로세스를 띄우지 않는다) · 이벤트 창 관리자 · 지도 팝업
        using var supervisor = new CameraPopupHostSupervisor(
            new CameraPopupHostOptions { HostExecutablePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-camhost-" + Guid.NewGuid().ToString("N") + ".exe") },
            new TestLog());
        var vm = MapPopupRig.NewVm(MapPopupRig.NewMap(), supervisor, MapPopupRig.SelfSettings());
        MapPopupRig.Call(vm, "StartCameraPopupHostNotice");
        var manager = new EventWindowManager(supervisor, new DelegateCameraPopupSettingsSource(() => new PopupSettings()), new FixedMonitors(), new TestLog());

        // Act ① 이벤트 창(카메라 3대) + 지도 팝업
        var opened = manager.Open(new EventWindowRequest
        {
            Kind = EventWindowKind.Detection,
            EventId = "41",
            Header = new EventWindowHeader { KindLabel = "탐지", DeviceName = "펜스 #41" },
            Cameras = Enumerable.Range(1, 3).Select(i => new EventWindowCamera
            {
                CameraId = "c" + i,
                Provider = new VideoProviderInfo { Kind = ProtocolProviderKind.TestPattern },
            }).ToList(),
        });
        MapPopupRig.DoubleClick(vm, MapPopupRig.CameraMarker());
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopups.Count == 1), "지도 팝업이 열리지 않았다");
        var popup = vm.CameraPopups[0];
        Assert.True(await MapPopupRig.Until(() => supervisor.Registry.OverlayCount == 1), "지도 팝업 영상이 복원 목록에 오르지 않았다");
        Assert.True(await MapPopupRig.Until(() => popup.PtzUnavailableReason != null && !popup.IsPtzLoading));

        // Assert ① 복구 — 호스트가 다시 뜨면 그대로 다시 보낼 목록(창 → 오버레이)에 둘 다 있다
        Assert.Equal(EventWindowOpenResult.Opened, opened);
        var (windows, overlays) = supervisor.Registry.Snapshot();
        var window = Assert.Single(windows);
        Assert.Equal("detection-41", window.EventKey);
        Assert.Equal(new[] { "c1", "c2", "c3" }, window.Cameras.Select(c => c.CameraId));
        var overlay = Assert.Single(overlays);
        Assert.Equal("map-cam-7", overlay.StreamId);
        Assert.Equal("7", overlay.Camera?.CameraId);
        Assert.Equal((384, 258), (overlay.Width, overlay.Height));

        // Assert ① 호스트 없음 — GIS 정상 · 상자는 "영상 기능을 사용할 수 없습니다" + [다시 시작] · PTZ 는 연결 실패 사유
        var message = VideoMessage(popup);
        Assert.Equal(CameraPopupVideoStatus.HostUnavailable, popup.VideoStatus);
        Assert.Equal("영상 기능을 사용할 수 없습니다", Text(message, "GMaps.CameraPopup.VideoStatus"));
        Assert.True(Shown(message, "GMaps.CameraPopup.HostRestart"));
        Assert.Equal(CameraStreamPopupViewModel.PtzReasonConnectFailed, popup.PtzUnavailableReason);

        // Act ② 상자 [다시 시작](실제 XAML Command) → 실제 감시자 수동 재시작(예산 비움 → 실행 파일 찾기)
        ((Button)RealXaml.ById(message, "GMaps.CameraPopup.HostRestart")).Command.Execute(null);

        // Assert ② 감시자가 재시작을 시도했고(실행 파일 없음 → 사용할 수 없음) 지도에 토스트로 알린다 · 복원 목록은 그대로
        Assert.True(await MapPopupRig.Until(() => supervisor.State == CameraPopupHostState.Unavailable), $"[다시 시작] 뒤 감시자 상태 {supervisor.State}");
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopupToastMessage == CameraPopupHostNotices.Unavailable),
                    $"사용할 수 없음 토스트가 안 떴다: '{vm.CameraPopupToastMessage}'");
        Assert.Equal(1, supervisor.Registry.WindowCount);
        Assert.Equal(1, supervisor.Registry.OverlayCount);
        Assert.Equal(CameraPopupVideoStatus.HostUnavailable, popup.VideoStatus);

        // Act ③ 사람이 팝업을 닫고 이벤트 창이 조치보고로 닫힘
        new PopupControlRig(popup).ClickClose();
        Assert.True(await MapPopupRig.Until(() => vm.CameraPopups.Count == 0));
        Assert.True(manager.CloseForActionReport(EventWindowKind.Detection, "41"));

        // Assert ③ 닫은 것은 복원 목록에서 빠진다(재시작 때 되살아나지 않는다)
        Assert.True(await MapPopupRig.Until(() => supervisor.Registry.OverlayCount == 0), "닫은 팝업 영상이 복원 목록에 남았다");
        Assert.Equal(0, supervisor.Registry.WindowCount);
    });
}
