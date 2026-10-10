using System.Text;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Xunit;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;
using VideoProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/****************************************************************************
   Purpose      : 카메라 팝업 경로 계측 — 창 관리자 popup_open_sent · 호스트 알림 popup_opened · first_frame · host_heartbeat,
                  그리고 StreamStateChanged.FirstFrameMs 선택 필드의 옛/새 호환 (카탈로그 PERF-003 · 004 · 012 · 013)
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(PopupPerfTraceCollection.Name)]
public class PopupPerfTraceTests
{
    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors()
            => new[] { new DisplayMonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true) };
    }

    private static string NewId() => Random.Shared.Next(100_000, 999_999).ToString();

    [Fact]
    [Trait("Scenario", "PERF-003")]
    public void should_mark_popup_open_sent_after_command_handed_to_host_when_window_opens()
    {
        var (host, recorder) = RecordingHost.Create();
        var manager = new EventWindowManager(host, new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings()), new OneMonitor(), new TestLog());
        var id = NewId();
        using var capture = PerfTrace.Capture();

        var result = manager.Open(new EventWindowRequest
        {
            Kind = EventWindowKind.Malfunction,
            EventId = id,
            Cameras = new List<EventWindowCamera>
            {
                new() { CameraId = "c1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
                new() { CameraId = "c2", Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern } },
            },
        });

        Assert.Equal(EventWindowOpenResult.Opened, result);
        Assert.Single(recorder.Opened);
        var line = Assert.Single(capture.For(PerfKinds.Malfunction, id));
        Assert.Equal(PerfStages.PopupOpenSent, line.Stage);
        Assert.Equal(2, line.ExtraNumber("cameras"));
        Assert.Equal(EventKeys.Build(EventWindowKind.Malfunction, id), line.ExtraString("key"));
    }

    [Fact]
    [Trait("Scenario", "PERF-003,PERF-004")]
    public void should_mark_opened_then_first_frame_once_per_stream_when_host_reports()
    {
        var recorder = new HostPerfRecorder(() => 0);
        var id = NewId();
        var key = EventKeys.Build(EventWindowKind.Detection, id);
        using var capture = PerfTrace.Capture();

        recorder.OnHostMessage(new WindowOpened { EventKey = key });
        recorder.OnHostMessage(new StreamStateChanged { StreamId = key + "#c1", EventKey = key, State = StreamState.Opening });
        recorder.OnHostMessage(new StreamStateChanged { StreamId = key + "#c1", EventKey = key, State = StreamState.Playing, FirstFrameMs = 420 });
        recorder.OnHostMessage(new StreamStateChanged { StreamId = key + "#c1", EventKey = key, State = StreamState.Playing });   // 재시도 뒤 다시 재생 — 첫 영상 아님
        recorder.OnHostMessage(new StreamStateChanged { StreamId = key + "#c2", EventKey = key, State = StreamState.Playing });   // 옛 호스트 — 시간 없이
        recorder.OnHostMessage(new StreamStateChanged { StreamId = "overlay-1", EventKey = null, State = StreamState.Playing });  // 오버레이는 이벤트가 아니다

        var mine = capture.For(PerfKinds.Detection, id);
        Assert.Equal(new[] { PerfStages.PopupOpened, PerfStages.FirstFrame, PerfStages.FirstFrame }, mine.Select(r => r.Stage));
        Assert.Equal(420, mine[1].ExtraNumber("host_ms"));
        Assert.Equal(key + "#c1", mine[1].ExtraString("stream"));
        Assert.Null(mine[2].ExtraNumber("host_ms"));
        Assert.DoesNotContain(capture.Records, r => r.ExtraString("stream") == "overlay-1");
    }

    [Fact]
    [Trait("Scenario", "PERF-012,PERF-013")]
    public void should_record_host_heartbeat_at_most_once_per_second()
    {
        long now = 0;
        var recorder = new HostPerfRecorder(() => now) { HostProcessId = 4242 };
        using var capture = PerfTrace.Capture();

        recorder.OnHostMessage(new HeartbeatAck { Sequence = 1, UiStallMs = 0, PrivateBytes = 100 });
        now = 500;
        recorder.OnHostMessage(new HeartbeatAck { Sequence = 2, UiStallMs = 0, PrivateBytes = 110 });
        now = 1000;
        recorder.OnHostMessage(new HeartbeatAck { Sequence = 3, UiStallMs = 1500, PrivateBytes = 120, OpenWindows = 4, OpenStreams = 8 });

        var lines = capture.Records.Where(r => r.Stage == PerfStages.HostHeartbeat && r.ExtraNumber("pid") == 4242).ToList();
        Assert.Equal(new double?[] { 100, 120 }, lines.Select(l => l.ExtraNumber("private_bytes")));
        Assert.Equal(1500, lines[1].ExtraNumber("ui_stall_ms"));
        Assert.Equal((4d, 8d), (lines[1].ExtraNumber("open_windows")!.Value, lines[1].ExtraNumber("open_streams")!.Value));
        Assert.All(lines, l => Assert.Equal(PerfKinds.CameraHost, l.Kind));
    }

    [Fact]
    [Trait("Scenario", "PERF-004")]
    public void should_omit_first_frame_field_when_null_and_round_trip_when_set()
    {
        var plain = Encoding.UTF8.GetString(IpcSerializer.Serialize(new StreamStateChanged { StreamId = "s", EventKey = "detection-1", State = StreamState.Opening }, 1));
        Assert.DoesNotContain("firstFrameMs", plain);   // 옛 GIS 가 보는 봉투 모양 그대로

        var bytes = IpcSerializer.Serialize(new StreamStateChanged { StreamId = "s", EventKey = "detection-1", State = StreamState.Playing, FirstFrameMs = 812 }, 2);
        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(bytes, out var message, out _, out _));
        Assert.Equal(812, Assert.IsType<StreamStateChanged>(message).FirstFrameMs);
    }

    [Fact]
    [Trait("Scenario", "PERF-004")]
    public void should_decode_old_host_message_without_first_frame_field()
    {
        var oldHost = "{\"v\":" + ProtocolVersion.Current + ",\"type\":\"StreamStateChanged\",\"id\":9,"
                      + "\"body\":{\"streamId\":\"s\",\"eventKey\":\"detection-5\",\"state\":\"Playing\"}}";

        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(Encoding.UTF8.GetBytes(oldHost), out var message, out _, out _));
        var s = Assert.IsType<StreamStateChanged>(message);
        Assert.Null(s.FirstFrameMs);
        Assert.Equal(StreamState.Playing, s.State);
    }
}

[CollectionDefinition(Name)]
public sealed class PopupPerfTraceCollection
{
    public const string Name = "PopupPerfTrace";
}
