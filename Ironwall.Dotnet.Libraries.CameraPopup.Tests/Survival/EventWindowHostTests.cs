using System.Collections.Concurrent;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;
using Xunit.Abstractions;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;
using VideoProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/****************************************************************************
   Purpose      : 실제 호스트 프로세스로 이벤트 창 관리자 왕복 (PRD camera-popup-modes FR-09 · 12, T-06)
                  이 시험 프로세스가 GIS 대역 — 탐지 2건 → OpenEventWindow 2개가 계단 자리로 나가고 호스트가 받아 창을 연다.
                  호스트는 헤드리스(창 없음) · 영상은 시험 무늬(LibVLC 없음).
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class EventWindowHostTests
{
    private static readonly DisplayMonitorInfo Primary = new(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), true);
    private readonly ITestOutputHelper _output;

    public EventWindowHostTests(ITestOutputHelper output) => _output = output;

    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => new[] { Primary };
    }

    private static EventWindowRequest Detection(string id) => new()
    {
        Kind = EventWindowKind.Detection,
        EventId = id,
        Header = new EventWindowHeader { KindLabel = "탐지", DeviceName = "펜스 센서 #" + id, ZoneName = "구역-07" },
        Cameras = new[] { "c1", "c2" }.Select(c => new EventWindowCamera
        {
            CameraId = c,
            Name = "시험 " + c,
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
        }).ToList(),
    };

    [Fact]
    public async Task should_send_two_cascaded_windows_and_host_opens_both_when_two_detections_arrive()
    {
        using var supervisor = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        }, new TestLog());
        var opened = new ConcurrentQueue<WindowOpened>();
        supervisor.StatusReceived += (_, e) => { if (e.Message is WindowOpened w) opened.Enqueue(w); };
        supervisor.Start();
        Assert.True(await WaitAsync(() => supervisor.State == CameraPopupHostState.Running, 20_000), $"호스트가 뜨지 않음: {supervisor.State} {supervisor.StateReason}");

        using var manager = new EventWindowManager(supervisor,
            new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings { FirstWindowX = 24, FirstWindowY = 24, CascadeStepPx = 24, CloseByTimer = false }),
            new OneMonitor(), new TestLog());

        var sw = Stopwatch.StartNew();
        Assert.Equal(EventWindowOpenResult.Opened, manager.Open(Detection("9001")));
        Assert.Equal(EventWindowOpenResult.Opened, manager.Open(Detection("9002")));
        var sendMs = sw.ElapsedMilliseconds;

        Assert.True(await WaitAsync(() => opened.Count >= 2, 10_000), $"호스트 WindowOpened {opened.Count}/2");
        var windows = supervisor.Registry.Snapshot().Windows;
        Assert.Equal(2, windows.Length);
        Assert.Equal((24, 24), (windows[0].Window.X, windows[0].Window.Y));
        Assert.Equal((48, 48), (windows[1].Window.X, windows[1].Window.Y));   // 오른쪽 아래로 24px
        Assert.Equal(new[] { EventKeys.Build(EventWindowKind.Detection, "9001"), EventKeys.Build(EventWindowKind.Detection, "9002") },
                     opened.Select(o => o.EventKey).OrderBy(k => k));
        _output.WriteLine($"open x2 sent in {sendMs} ms; host acks {opened.Count}");
        Assert.True(sendMs < 50, $"창 명령 2건에 {sendMs} ms");

        // 같은 이벤트가 다시 오면 새 창 없이 앞으로, 조치보고면 닫힌다.
        Assert.Equal(EventWindowOpenResult.BroughtToFront, manager.Open(Detection("9001")));
        Assert.True(manager.CloseForActionReport(EventWindowKind.Detection, "9001"));
        Assert.Single(supervisor.Registry.Snapshot().Windows);
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
