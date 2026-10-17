using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// L6 — <see cref="HostedVideoView"/> 의 화면 갱신 구독(<c>CompositionTarget.Rendering</c>, 정적 이벤트)이
/// Unloaded 없이 떨어진 표시기를 붙잡아 두면 안 된다(팝업을 열고 닫을 때마다 새는 누수).
/// </summary>
public class HostedVideoViewLifetimeTests
{
    private sealed class IdleFrameSource : IFrameSource
    {
        public string StreamId => "idle";
        public int Width => 16;
        public int Height => 16;
        public int Stride => 64;
        public long PublishedSequence => 0;
        public StreamState State => StreamState.Opening;
        public string? StateDetail => null;
        public event EventHandler? StateChanged { add { } remove { } }
        public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence)
        {
            sequence = 0;
            return false;
        }
        public void Dispose() { }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA body timed out");
        if (failure is not null) throw new Xunit.Sdk.XunitException($"STA body failed: {failure}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateHookedViewWithoutUnload()
    {
        var view = new HostedVideoView { FrameSource = new IdleFrameSource() };
        view.Hook();   // 화면 갱신 구독 — 이후 Unloaded 가 오지 않는다(창이 Unloaded 없이 닫힌 경우 등)
        Assert.True(view.IsHooked);
        return new WeakReference(view);
    }

    [Fact]
    public void should_let_view_be_collected_when_dropped_while_hooked_without_unloaded()
    {
        RunSta(() =>
        {
            // Arrange · Act
            var weak = CreateHookedViewWithoutUnload();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // Assert
            Assert.False(weak.IsAlive, "HostedVideoView leaked through CompositionTarget.Rendering");
        });
    }

    [Fact]
    public void should_unhook_when_removed_from_visual_parent()
    {
        RunSta(() =>
        {
            // Arrange
            var parent = new Grid();
            var view = new HostedVideoView { FrameSource = new IdleFrameSource() };
            parent.Children.Add(view);
            view.Hook();
            Assert.True(view.IsHooked);

            // Act — Unloaded 없이 시각 부모에서 떨어진다
            parent.Children.Remove(view);

            // Assert
            Assert.False(view.IsHooked);
        });
    }
}
