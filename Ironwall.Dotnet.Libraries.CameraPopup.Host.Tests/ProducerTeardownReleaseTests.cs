using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// K7 장시간 실측(창 10개 x 타일 6개, 10분마다 창 3개 교체 + 스트림 1개 끊기)에서 찾은 누수:
/// 닫힌 생산자가 싱크(타일 비트맵 + 232 KB 버퍼)와 상태 콜백(창 세션 · 타일 VM)을 계속 붙잡아,
/// 스트림을 다시 열 때마다 호스트 전용 메모리가 약 0.7 MB 씩 늘었다(2시간에 +270 MB).
/// 생산자 객체가 어디엔가 붙잡혀 있어도(LibVLC 이벤트 관리자) 정리된 뒤에는 싱크 · 콜백을 놓아야 한다.
/// </summary>
public class ProducerTeardownReleaseTests
{
    private sealed class RecordingSink : IFrameSink
    {
        public int Width => 64;
        public int Height => 48;
        public void Write(IntPtr bgra, int stride) { }
    }

    private sealed class StateTarget
    {
        public readonly TaskCompletionSource<string?> Failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnState(StreamState state, string? detail)
        {
            if (state == StreamState.Failed) Failed.TrySetResult(detail);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LibVlcFrameProducer Producer, WeakReference Sink, WeakReference Target, Task<string?> Failed) StartProducerThatFails()
    {
        var log = new HostLog(Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", "teardown-" + Guid.NewGuid().ToString("N")[..8]));
        // 주소가 빈 RTSP — LibVLC 를 올리지 않고 "실패 → 정리" 길만 탄다.
        var producer = new LibVlcFrameProducer(new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "" }, 64, 48, "teardown-test", log);
        var sink = new RecordingSink();
        var target = new StateTarget();
        producer.Start(sink, target.OnState);
        return (producer, new WeakReference(sink), new WeakReference(target), target.Failed.Task);
    }

    [Fact]
    public async Task should_release_sink_and_state_callback_when_producer_is_torn_down_but_still_referenced()
    {
        // Arrange — 생산자는 시험이 끝까지 붙잡고 있다(LibVLC 이벤트 관리자가 붙잡는 상황과 같다)
        var (producer, sink, target, failed) = StartProducerThatFails();
        Assert.Equal("empty-uri", await failed.WaitAsync(TimeSpan.FromSeconds(10)));

        // Act
        producer.Dispose();
        bool released = false;
        for (int i = 0; i < 100 && !released; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            released = !sink.IsAlive && !target.IsAlive;
            if (!released) await Task.Delay(20);
        }

        // Assert
        Assert.False(sink.IsAlive, "torn-down producer still holds the frame sink");
        Assert.False(target.IsAlive, "torn-down producer still holds the state callback target");
        GC.KeepAlive(producer);
    }
}
