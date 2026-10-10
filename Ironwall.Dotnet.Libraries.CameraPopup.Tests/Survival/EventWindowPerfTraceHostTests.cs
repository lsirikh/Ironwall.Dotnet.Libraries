using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Xunit;
using Xunit.Abstractions;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;
using VideoProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/****************************************************************************
   Purpose      : 실제 호스트 프로세스 왕복 계측 — 창 명령 보냄 → 호스트 열림 → 첫 영상(호스트가 실어 보낸 FirstFrameMs)이
                  같은 이벤트 id 로 GIS 계측에 남는다 (카탈로그 PERF-003 · 004 · 012). 호스트는 헤드리스(창 없음) · 영상은 시험 무늬.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class EventWindowPerfTraceHostTests
{
    private readonly ITestOutputHelper _output;

    public EventWindowPerfTraceHostTests(ITestOutputHelper output) => _output = output;

    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors()
            => new[] { new DisplayMonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true) };
    }

    [Fact]
    [Trait("Scenario", "PERF-003,PERF-004,PERF-012")]
    public async Task should_trace_sent_opened_and_first_frame_with_same_event_id_when_real_host_opens_window()
    {
        using var capture = PerfTrace.Capture();
        using var supervisor = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        }, new TestLog());
        supervisor.Start();
        Assert.True(await WaitAsync(() => supervisor.State == CameraPopupHostState.Running, 20_000), $"호스트가 뜨지 않음: {supervisor.State} {supervisor.StateReason}");

        using var manager = new EventWindowManager(supervisor,
            new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings { CloseByTimer = false }), new OneMonitor(), new TestLog());
        var id = Random.Shared.Next(100_000, 999_999).ToString();

        Assert.Equal(EventWindowOpenResult.Opened, manager.Open(new EventWindowRequest
        {
            Kind = EventWindowKind.Detection,
            EventId = id,
            Header = new EventWindowHeader { KindLabel = "탐지", ZoneName = "구역-07" },
            Cameras = new[] { "c1", "c2" }.Select(c => new EventWindowCamera
            {
                CameraId = c, Name = c, Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
            }).ToList(),
        }));

        Assert.True(await WaitAsync(() => capture.For(PerfKinds.Detection, id).Count(r => r.Stage == PerfStages.FirstFrame) >= 2, 15_000),
                    "첫 영상 2건이 계측에 오지 않음: " + string.Join(", ", capture.For(PerfKinds.Detection, id).Select(r => r.Stage)));
        Assert.True(await WaitAsync(() => capture.Records.Any(r => r.Stage == PerfStages.HostHeartbeat), 5_000), "호스트 하트비트 계측 없음");

        var mine = capture.For(PerfKinds.Detection, id);
        var sent = Assert.Single(mine, r => r.Stage == PerfStages.PopupOpenSent);
        var opened = Assert.Single(mine, r => r.Stage == PerfStages.PopupOpened);
        var frames = mine.Where(r => r.Stage == PerfStages.FirstFrame).ToList();
        Assert.True(sent.T <= opened.T, "보냄 → 열림 순서");
        Assert.All(frames, f => Assert.True(f.T >= sent.T));
        Assert.All(frames, f => Assert.NotNull(f.ExtraNumber("host_ms")));   // 새 호스트는 열기 요청 받은 뒤 첫 프레임까지 ms 를 싣는다
        var heartbeat = capture.Records.First(r => r.Stage == PerfStages.HostHeartbeat);
        Assert.True(heartbeat.ExtraNumber("private_bytes") > 0);

        var report = PerfTraceAnalyzer.Analyze(capture.Records);
        _output.WriteLine(report.ToMarkdown());
        Assert.Equal(1, report.Latency("보냄→열림")!.Count);
        Assert.Equal(1, report.Latency("보냄→첫영상")!.Count);
        manager.CloseForActionReport(EventWindowKind.Detection, id);
    }

    private static async Task<bool> WaitAsync(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(50);
        }
        return condition();
    }
}
