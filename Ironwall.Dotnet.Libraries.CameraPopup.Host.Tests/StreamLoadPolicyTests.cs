using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Watchdogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 타일 60개 부하 대책의 순수 부분(T-09 T7 · 심박 오탐): 연결 줄 · 재시도 간격 · 디코딩 옵션 · 메모리 한도 · UI 점검 표식.
/// </summary>
public class StreamLoadPolicyTests
{
    // ───────── 연결 줄 ─────────

    [Fact]
    public async Task should_admit_only_max_concurrent_opens_when_many_streams_start_at_once()
    {
        // Arrange
        var gate = new StreamOpenGate(6);

        // Act — 60개가 한꺼번에 온다
        var tasks = Enumerable.Range(0, 60).Select(_ => gate.EnterAsync(false, CancellationToken.None)).ToArray();

        // Assert — 6개만 들어가고 54개는 줄을 선다
        Assert.Equal(6, tasks.Count(t => t.IsCompletedSuccessfully));
        Assert.Equal(6, gate.Active);
        Assert.Equal(54, gate.Waiting);

        // 자리를 돌려주면 한 명씩 들어간다(넘치지 않는다)
        (await tasks[0]).Dispose();
        await tasks[6];
        Assert.Equal(6, gate.Active);
        Assert.Equal(53, gate.Waiting);
    }

    [Fact]
    public async Task should_serve_waiters_in_arrival_order_when_slots_free_up()
    {
        var gate = new StreamOpenGate(1);
        var first = await gate.EnterAsync(false, CancellationToken.None);
        var a = gate.EnterAsync(false, CancellationToken.None);
        var b = gate.EnterAsync(false, CancellationToken.None);

        first.Dispose();
        var leaseA = await a;
        Assert.False(b.IsCompleted);

        leaseA.Dispose();
        (await b).Dispose();
        Assert.Equal(0, gate.Active);
    }

    [Fact]
    public async Task should_put_priority_stream_ahead_of_queued_tiles_when_user_opens_overlay()
    {
        var gate = new StreamOpenGate(1);
        var first = await gate.EnterAsync(false, CancellationToken.None);
        var tile = gate.EnterAsync(false, CancellationToken.None);
        var overlay = gate.EnterAsync(true, CancellationToken.None);

        first.Dispose();

        var lease = await overlay;
        Assert.False(tile.IsCompleted);
        lease.Dispose();
        (await tile).Dispose();
    }

    [Fact]
    public async Task should_leave_queue_without_taking_a_slot_when_waiting_stream_is_closed()
    {
        var gate = new StreamOpenGate(1);
        var first = await gate.EnterAsync(false, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var closed = gate.EnterAsync(false, cts.Token);
        var next = gate.EnterAsync(false, CancellationToken.None);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closed);
        Assert.Equal(1, gate.Waiting);

        first.Dispose();
        (await next).Dispose();
        Assert.Equal(0, gate.Active);
    }

    [Fact]
    public async Task should_release_slot_once_when_lease_is_disposed_twice()
    {
        var gate = new StreamOpenGate(2);
        var lease = await gate.EnterAsync(false, CancellationToken.None);
        var other = await gate.EnterAsync(false, CancellationToken.None);

        lease.Dispose();
        lease.Dispose();   // 첫 프레임 · 실패 · 닫기가 겹쳐도 한 번만

        Assert.Equal(1, gate.Active);
        other.Dispose();
        Assert.Equal(0, gate.Active);
    }

    // ───────── 자동 재시도 ─────────

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(50, 30)]
    public void should_back_off_up_to_thirty_seconds_when_tile_keeps_failing(int attempt, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), StreamRetryBackoff.DelayFor(attempt));

    [Fact]
    public void should_schedule_retry_and_reset_count_when_tile_fails_then_plays()
    {
        // Arrange
        var schedule = new TileRetrySchedule();
        var now = new DateTime(2026, 9, 30, 7, 0, 0, DateTimeKind.Utc);

        // Act / Assert — 첫 실패: 2초 뒤
        Assert.True(schedule.OnState(StreamState.Failed, "open-timeout", now));
        Assert.False(schedule.TryTake(now.AddSeconds(1.9), out _));
        Assert.True(schedule.TryTake(now.AddSeconds(2), out int attempt));
        Assert.Equal(1, attempt);
        Assert.False(schedule.TryTake(now.AddSeconds(10), out _));   // 한 번만 꺼낸다

        // 또 실패: 4초 뒤
        Assert.True(schedule.OnState(StreamState.Stalled, "end-reached", now.AddSeconds(3)));
        Assert.False(schedule.TryTake(now.AddSeconds(6.9), out _));
        Assert.True(schedule.TryTake(now.AddSeconds(7), out attempt));
        Assert.Equal(2, attempt);

        // 재생되면 처음부터
        Assert.False(schedule.OnState(StreamState.Playing, null, now.AddSeconds(8)));
        Assert.Equal(0, schedule.Failures);
        Assert.True(schedule.OnState(StreamState.Failed, "libvlc-error", now.AddSeconds(9)));
        Assert.True(schedule.TryTake(now.AddSeconds(11), out attempt));
        Assert.Equal(1, attempt);
    }

    [Theory]
    [InlineData("not-supported")]
    [InlineData("bad-request")]
    [InlineData("empty-uri")]
    public void should_not_retry_when_failure_cannot_be_fixed_by_retrying(string detail)
    {
        var schedule = new TileRetrySchedule();

        Assert.False(schedule.OnState(StreamState.Failed, detail, DateTime.UtcNow));

        Assert.Null(schedule.DueAt);
    }

    [Fact]
    public void should_ignore_queued_and_connecting_states_when_stream_is_still_opening()
    {
        var schedule = new TileRetrySchedule();

        Assert.False(schedule.OnState(StreamState.Opening, LibVlcFrameProducer.DetailQueued, DateTime.UtcNow));

        Assert.Null(schedule.DueAt);
        Assert.Equal(0, schedule.Failures);
    }

    // ───────── 디코딩 옵션 ─────────

    [Fact]
    public void should_use_one_software_decoder_thread_over_tcp_without_audio_when_box_is_a_small_tile()
    {
        var options = DecodeProfile.MediaOptions(266, 218, attempt: 0);

        Assert.Contains(":rtsp-tcp", options);           // 들어오는 포트를 열지 않는다(방화벽 창 없음)
        Assert.Contains(":no-audio", options);
        Assert.Contains(":avcodec-threads=1", options);
        Assert.Contains(":avcodec-hw=none", options);
        Assert.Contains(":drop-late-frames", options);
        Assert.False(DecodeProfile.UsesHardware(266, 218, 0));
    }

    [Fact]
    public void should_try_hardware_decoding_first_and_fall_back_to_software_when_large_box_is_retried()
    {
        Assert.Contains(":avcodec-hw=d3d11va", DecodeProfile.MediaOptions(960, 536, attempt: 0));
        Assert.Contains(":avcodec-hw=none", DecodeProfile.MediaOptions(960, 536, attempt: 1));
        Assert.Contains(":rtsp-tcp", DecodeProfile.MediaOptions(960, 536, attempt: 1));
    }

    [Theory]
    [InlineData(266, 218, false)]
    [InlineData(640, 480, false)]
    [InlineData(800, 436, true)]
    [InlineData(960, 536, true)]
    public void should_classify_box_as_large_when_bigger_than_640x480(int width, int height, bool large)
        => Assert.Equal(large, DecodeProfile.IsLarge(width, height));

    // ───────── 메모리 한도 ─────────

    [Theory]
    [InlineData(1536, 24, 0, 1536)]     // 스트림 없음 — 설정 한도
    [InlineData(1536, 24, 10, 1536)]    // 적으면 설정 한도가 크다
    [InlineData(1536, 24, 60, 1952)]    // 타일 60개 — 512 + 60×24 (T-09: 1548~1811 MB 에서 20초마다 재시작)
    [InlineData(1536, 0, 60, 1536)]     // 스트림당 0 → 고정 한도
    [InlineData(1, 24, 0, 1)]           // 시험용 아주 낮은 한도는 그대로
    public void should_scale_memory_limit_with_open_streams_when_many_tiles_are_open(int configured, int perStream, int streams, int expected)
        => Assert.Equal(expected, HostLaunchArguments.EffectiveMemoryLimitMb(configured, perStream, streams));

    [Fact]
    public void should_round_trip_stream_limits_when_launch_arguments_are_parsed()
    {
        var token = PipeNaming.CreateToken();
        var original = new HostLaunchArguments
        {
            ParentProcessId = 4321,
            PipeName = PipeNaming.Build(4321, token),
            Token = token,
            MemoryLimitMb = 2048,
            MemoryPerStreamMb = 30,
            MaxConcurrentOpens = 4,
        };

        Assert.True(HostLaunchArguments.TryParse(original.ToArgumentList(), out var parsed, out var error), error);

        Assert.Equal((2048, 30, 4), (parsed!.MemoryLimitMb, parsed.MemoryPerStreamMb, parsed.MaxConcurrentOpens));
    }

    // ───────── UI 점검 표식(심박과 분리) ─────────

    [Fact]
    public void should_report_no_stall_while_ui_is_pumping_and_growing_stall_when_ui_thread_is_blocked()
    {
        // Arrange — 실제 디스패처(STA 스레드) + 손으로 넘기는 시계
        long clock = 1_000;
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
        using var probe = new UiPumpProbe(dispatcher!, () => Interlocked.Read(ref clock), startTimer: false);
        try
        {
            // Act 1 — UI 가 돌고 있다: 표식이 곧 처리된다
            probe.Probe();
            dispatcher!.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Interlocked.Add(ref clock, 5_000);
            Assert.Equal(0, probe.StallMs);

            // Act 2 — UI 스레드가 한 가지 일에서 돌아오지 않는다
            using var blocked = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            dispatcher.BeginInvoke(() => { blocked.Set(); release.Wait(TimeSpan.FromSeconds(30)); });
            Assert.True(blocked.Wait(TimeSpan.FromSeconds(10)));
            probe.Probe();
            Interlocked.Add(ref clock, 500);
            Assert.Equal(0, probe.StallMs);             // 1초 미만은 소음
            Interlocked.Add(ref clock, 11_500);
            Assert.Equal(12_000, probe.StallMs);        // 표식을 넣은 때부터

            // Act 3 — 풀리면 0 으로
            release.Set();
            dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Assert.Equal(0, probe.StallMs);
        }
        finally
        {
            dispatcher!.InvokeShutdown();
        }
    }
}
