using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Modules;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Ironwall.Dotnet.Libraries.CameraPopup.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

public class SupervisorUnitTests
{
    [Fact]
    public void should_suspend_on_fourth_failure_when_three_restarts_allowed_per_minute()
    {
        var budget = new RestartBudget(3, TimeSpan.FromSeconds(60));

        Assert.True(budget.TryRecordFailure(0));
        Assert.True(budget.TryRecordFailure(1_000));
        Assert.True(budget.TryRecordFailure(2_000));
        Assert.False(budget.TryRecordFailure(3_000));
    }

    [Fact]
    public void should_allow_restart_again_when_old_failures_leave_window()
    {
        var budget = new RestartBudget(3, TimeSpan.FromSeconds(60));
        budget.TryRecordFailure(0);
        budget.TryRecordFailure(1);
        budget.TryRecordFailure(2);

        Assert.True(budget.TryRecordFailure(60_002));
        Assert.Equal(1, budget.CountInWindow(60_002));
        budget.Reset();
        Assert.Equal(0, budget.CountInWindow(60_002));
    }

    [Fact]
    public void should_replay_windows_in_open_order_when_snapshot_taken()
    {
        var registry = new HostSessionRegistry();
        registry.SetWindow(new OpenEventWindow { EventKey = "b" });
        registry.SetWindow(new OpenEventWindow { EventKey = "a" });
        registry.SetWindow(new OpenEventWindow { EventKey = "b", Title = "updated" });
        registry.SetOverlay(new OpenOverlayStream { StreamId = "s" });
        registry.RemoveWindow("a");

        var (windows, overlays) = registry.Snapshot();

        Assert.Equal(new[] { "b" }, windows.Select(w => w.EventKey));
        Assert.Equal("updated", windows[0].Title);
        Assert.Single(overlays);
    }

    [Fact]
    public void should_recognize_popup_origin_when_exception_thrown_inside_popup_code()
    {
        Exception? inner = null;
        try { new RestartBudget(-1, TimeSpan.FromSeconds(1)); }
        catch (Exception ex) { inner = ex; }

        Assert.True(CameraPopupFaults.IsPopupOrigin(inner));
        Assert.True(CameraPopupFaults.IsPopupOrigin(new AggregateException(new InvalidOperationException("x", inner))));
        Assert.False(CameraPopupFaults.IsPopupOrigin(new InvalidOperationException("elsewhere")));
        Assert.False(CameraPopupFaults.IsPopupOrigin(null));
    }

    [Fact]
    public void should_resolve_single_supervisor_when_module_registered()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestLog()).As<ILogService>();
        builder.RegisterModule(new CameraPopupModule(new CameraPopupHostOptions { HostExecutablePath = @"C:\nope\x.exe" }));
        using var container = builder.Build();

        var host = container.Resolve<ICameraPopupHost>();

        Assert.Same(host, container.Resolve<CameraPopupHostSupervisor>());
        Assert.Equal(CameraPopupHostState.NotStarted, host.State);
    }

    [Fact]
    public void should_draw_shared_frame_when_hosted_video_view_renders()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var name = SharedFrameLayout.BuildName(Environment.ProcessId, Guid.NewGuid().ToString("N")[..12]);
                using var gisView = SharedFrameView.CreateNew(name, 4, 2);
                using var hostView = SharedFrameView.OpenExisting(name, 4, 2);
                using var source = new SharedFrameSource("v", gisView, null);
                var view = new HostedVideoView { FrameSource = source };

                Assert.False(view.RenderLatest()); // 아직 프레임 없음

                var frame = new byte[4 * 2 * 4];
                for (int i = 0; i < frame.Length; i += 4) { frame[i] = 0x10; frame[i + 1] = 0x20; frame[i + 2] = 0x30; frame[i + 3] = 0xFF; }
                hostView.WriteFrame(frame, 16);

                Assert.True(view.RenderLatest());
                Assert.False(view.RenderLatest()); // 같은 순번은 다시 그리지 않는다
                var bitmap = (System.Windows.Media.Imaging.WriteableBitmap)view.Source;
                var pixels = new byte[4 * 2 * 4];
                bitmap.CopyPixels(pixels, 16, 0);
                Assert.Equal(0x10, pixels[0]);
                Assert.Equal(0x20, pixels[1]);
                Assert.Equal(0x30, pixels[2]);
                Assert.Equal(1, view.FramesRendered);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
