using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;
using VideoProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 카탈로그 §2 EVM-030 — 카메라 심볼 더블클릭 팝업(지도 오버레이 영상)이 떠 있는 채 탐지 · 장애 이벤트가 몰려와도
                  이벤트 창 관리자는 그 팝업을 닫지 않는다. 더블클릭 팝업은 CameraStreamPopupViewModel 이 호스트 창구의
                  OpenOverlay/CloseOverlay 로 여닫는다 — 관리자는 자기 장부(이벤트 키)의 창만 CloseEventWindow 로 닫는다.
                  호스트는 기록용 가짜(RecordingHost) — 프로세스 · 창 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventWindowPopupCoexistenceTests
{
    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors()
            => new[] { new DisplayMonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true) };
    }

    private sealed class Settings : ICameraPopupSettingsSource
    {
        public CameraPopupSettings Current { get; init; } = new();
    }

    private static EventWindowRequest Request(int id, EventWindowKind kind) => new()
    {
        Kind = kind,
        EventId = id.ToString(),
        Header = new EventWindowHeader { KindLabel = kind == EventWindowKind.Detection ? "탐지" : "장애", DeviceName = "펜스 #" + id },
        Cameras = new List<EventWindowCamera>
        {
            new() { CameraId = "c1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
        },
    };

    [Fact]
    [Trait("Scenario", "EVM-030")]
    public void should_never_close_the_double_click_camera_popup_while_event_windows_open_evict_and_close_evm030()
    {
        var (host, recorder) = RecordingHost.Create();
        var manager = new EventWindowManager(host, new Settings { Current = new CameraPopupSettings { MaxOpenWindows = 3, EventWindowOnMalfunction = true } },
                                             new OneMonitor(), new TestLog());

        // 운영자가 카메라 심볼을 더블클릭 — 팝업 뷰모델이 호스트에 오버레이 영상을 연다(CameraStreamPopupViewModel 과 같은 호출).
        host.OpenOverlay(new OverlayStreamRequest { StreamId = "popup-camera-7", Camera = new CameraRef { CameraId = "7", Name = "카메라 7" } });

        // 탐지 4건 + 장애 1건이 몰려온다(창 한도 3 → 오래된 창 정리), 그 가운데 하나는 조치보고로 닫힌다.
        for (var id = 1; id <= 4; id++) manager.Open(Request(id, EventWindowKind.Detection));
        manager.Open(Request(9, EventWindowKind.Malfunction));
        Assert.True(manager.CloseForActionReport(EventWindowKind.Detection, "4"));

        var calls = recorder.Calls.Select(c => c.Member).ToList();
        Assert.DoesNotContain(nameof(ICameraPopupHost.CloseOverlay), calls);                  // 더블클릭 팝업은 그대로
        Assert.Single(recorder.Calls, c => c.Member == nameof(ICameraPopupHost.OpenOverlay)); // 다시 열지도 않는다
        Assert.NotEmpty(recorder.Closed);
        Assert.All(recorder.Closed, c => Assert.Matches("^(detection|malfunction)-\\d+$", c.Key));   // 닫은 것은 이벤트 창 열쇠뿐
        Assert.Contains(recorder.Closed, c => c.Reason == EventWindowCloseReason.Evicted);
        Assert.Contains(recorder.Closed, c => c.Key == EventKeys.Build(EventWindowKind.Detection, "4") && c.Reason == EventWindowCloseReason.ActionReported);
    }
}
