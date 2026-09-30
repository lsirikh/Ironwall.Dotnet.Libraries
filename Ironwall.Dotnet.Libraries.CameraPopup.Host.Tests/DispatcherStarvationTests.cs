using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// K7b — 호스트 UI 스레드가 Normal · Render 우선순위 일로 20초 동안 쉬지 않고 바쁠 때(실측: 2026-09-30 22:19~22:30,
/// Background 일이 10분 밀림)에도 영상 복구 · 창 명령 · PTZ 정지가 제때 나가는가. 실제 디스패처(STA 스레드) · 실제 창 관리자(헤드리스) ·
/// 가짜 생산자 · 가짜 PTZ 로 한 번 돌리고(<see cref="DispatcherFloodScenario"/>), 잰 값을 시험마다 하나씩 단언한다.
/// </summary>
public sealed class DispatcherStarvationTests : IClassFixture<DispatcherFloodScenario>
{
    private readonly DispatcherFloodScenario _run;

    public DispatcherStarvationTests(DispatcherFloodScenario run) => _run = run;

    [Fact]
    public void should_keep_background_priority_work_starved_when_dispatcher_is_flooded_for_20_seconds()
    {
        // 시험 장치 자체의 확인 — 이 홍수는 Background 일을 한 번도 돌리지 않는다(22:19 조건의 재현).
        Assert.True(_run.FloodSeconds >= 20, $"flood lasted {_run.FloodSeconds:0.0} s");
        Assert.False(_run.BackgroundMarkerRanDuringFlood);
    }

    [Fact]
    public void should_retry_failed_tile_within_backoff_window_when_dispatcher_is_flooded()
    {
        Assert.Equal(2, _run.Recoveries.Count);
        foreach (var r in _run.Recoveries)
        {
            Assert.True(r.RetryCreatedMs is not null, $"{r.Label}: retry producer was never created");
            // 첫 실패의 재시도 간격은 2초 — 그보다 이르지 않고, 1초 넘게 늦지 않는다.
            Assert.InRange(r.RetryCreatedMs!.Value, StreamRetryBackoff.First.TotalMilliseconds - 300, StreamRetryBackoff.First.TotalMilliseconds + 1000);
        }
    }

    [Fact]
    public void should_show_recovered_tile_as_playing_within_backoff_window_when_dispatcher_is_flooded()
    {
        foreach (var r in _run.Recoveries)
        {
            Assert.True(r.UiPlayingMs is not null, $"{r.Label}: tile never returned to Playing on the UI");
            Assert.InRange(r.UiPlayingMs!.Value, 0, StreamRetryBackoff.First.TotalMilliseconds + 1500);
        }
    }

    [Fact]
    public void should_execute_open_event_window_command_within_one_second_when_dispatcher_is_flooded()
    {
        Assert.True(_run.OpenLatencyMs is not null, "OpenEventWindow was not executed during the flood");
        Assert.InRange(_run.OpenLatencyMs!.Value, 0, 1000);
    }

    [Fact]
    public void should_execute_close_event_window_command_within_one_second_when_dispatcher_is_flooded()
    {
        Assert.True(_run.CloseLatencyMs is not null, "CloseEventWindow was not executed during the flood");
        Assert.InRange(_run.CloseLatencyMs!.Value, 0, 1000);
    }

    [Fact]
    public void should_send_ptz_stop_within_100_ms_when_dispatcher_is_flooded()
    {
        // GIS 가 보낸 정지(파이프 읽기 줄 → 제공자) — 디스패처를 타지 않는다.
        Assert.True(_run.GisStopLatencyMs is not null, "stop never reached the PTZ provider");
        Assert.InRange(_run.GisStopLatencyMs!.Value, 0, 100);
    }

    [Fact]
    public void should_send_ptz_stop_for_moving_tile_within_one_second_when_window_is_closed_under_flood()
    {
        // 누르고 있는 채로 창이 닫힌다 — 정지는 닫기 명령과 함께 나간다(Background 뒤에 서지 않는다).
        Assert.True(_run.CloseStopLatencyMs is not null, "stop was not sent for the moving tile");
        Assert.InRange(_run.CloseStopLatencyMs!.Value, 0, 1000);
    }

    [Fact]
    public void should_run_window_commands_in_received_order_when_many_are_posted_at_once()
    {
        Assert.Equal(Enumerable.Range(0, 20).ToArray(), _run.BurstOrder);
        Assert.True(_run.BurstLatencyMs is not null, "burst of 20 commands did not finish");
        // 명령 사이 숨 고르기는 한 번에 50 ms 까지 — 20개가 몰려도 2초 안에 끝난다.
        Assert.InRange(_run.BurstLatencyMs!.Value, 0, 2000);
    }
}

/// <summary>홍수 20초 시나리오 한 번(시험 클래스가 공유) — 잰 값만 남긴다.</summary>
public sealed class DispatcherFloodScenario : IDisposable
{
    public sealed record Recovery(string Label, double? RetryCreatedMs, double? UiPlayingMs);

    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly ConcurrentQueue<(IIpcMessage Message, double AtMs)> _sent = new();
    private readonly Dispatcher _dispatcher;
    private volatile bool _floodStop;

    public double FloodSeconds { get; }
    public bool BackgroundMarkerRanDuringFlood { get; }
    public List<Recovery> Recoveries { get; } = new();
    public double? OpenLatencyMs { get; }
    public double? CloseLatencyMs { get; }
    public double? GisStopLatencyMs { get; }
    public double? CloseStopLatencyMs { get; }
    public int[] BurstOrder { get; }
    public double? BurstLatencyMs { get; }

    public DispatcherFloodScenario()
    {
        _dispatcher = StartUiThread();
        var log = new HostLog(Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", "flood-" + Guid.NewGuid().ToString("N")[..8]));
        var producers = new ScriptedProducerFactory(_watch);
        var controls = new RecordingControls(_watch);
        using var manager = new EventWindowManager(_dispatcher, producers, controls, SystemHostClock.Instance,
            m => _sent.Enqueue((m, _watch.Elapsed.TotalMilliseconds)), log, headless: true);
        var commands = new UiCommandQueue(_dispatcher, (_, _) => { });

        var ptzProvider = new StopClockPtzProvider(_watch);
        var services = new HostCameraServices(new CameraProviderRegistry(ptzProvider, Array.Empty<ICameraVideoProvider>()), log, _ => { });
        var onvif = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = 80 };

        // Arrange — 홍수 전에 창 하나(카메라 2대)를 열고 재생까지.
        var first = Window("1", "cam-a", "cam-b");
        _dispatcher.Invoke(() => manager.Open(first));
        WaitUntil(() => TileState(manager, first.EventKey, "cam-a") == StreamState.Playing, 5000);

        // Act — 홍수 시작
        bool markerRan = false;
        double floodStart = _watch.Elapsed.TotalMilliseconds;
        StartFlood();
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => markerRan = true));
        Thread.Sleep(500);

        // ① 재생 중이던 타일이 끊긴다(K7: Stalled/end-reached) → 2초 뒤 재시도 → 재생
        Recoveries.Add(FailAndMeasure("first failure", manager, producers, first.EventKey, "cam-a"));

        // ② 창 열기 명령
        var second = Window("2", "cam-c");
        double posted = _watch.Elapsed.TotalMilliseconds;
        commands.Post("open-window", () => manager.Open(second));
        OpenLatencyMs = WaitSent<WindowOpened>(m => m.EventKey == second.EventKey, posted, 3000);

        // ③ GIS 가 보낸 PTZ 이동 → 정지(파이프 읽기 줄과 같은 길 — 디스패처 없음)
        services.HandlePtz(new PtzCommand { CameraId = "cam-gis", Operation = PtzOperation.ContinuousMove, Pan = 0.5, Provider = onvif });
        Thread.Sleep(200);
        double stopPosted = _watch.Elapsed.TotalMilliseconds;
        services.HandlePtz(new PtzCommand { CameraId = "cam-gis", Operation = PtzOperation.Stop, Provider = onvif });
        GisStopLatencyMs = WaitValue(() => ptzProvider.StopAtMs, stopPosted, 2000);

        // ④ 명령 20개가 한꺼번에 — 받은 순서대로
        var order = new ConcurrentQueue<int>();
        double burstPosted = _watch.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 20; i++)
        {
            int n = i;
            commands.Post("burst", () => order.Enqueue(n));
        }
        BurstLatencyMs = WaitValue(() => order.Count == 20 ? _watch.Elapsed.TotalMilliseconds : null, burstPosted, 5000);
        BurstOrder = order.ToArray();

        // ⑤ 홍수 후반에 한 번 더 끊긴다
        SleepUntil(floodStart + 12_000);
        Recoveries.Add(FailAndMeasure("second failure", manager, producers, first.EventKey, "cam-a"));

        // ⑥ 누르고 있는 채로(연속 이동 중) 창 닫기 명령 → 닫힘 + 정지
        _dispatcher.InvokeAsync(() => _ = manager.Find(second.EventKey)?.CameraTiles.First().PtzMoveAsync(0.5, 0, 0));
        WaitUntil(() => controls.Get("cam-c")?.MoveAtMs is not null, 2000);
        double closePosted = _watch.Elapsed.TotalMilliseconds;
        commands.Post("close-window", () => manager.Close(second.EventKey, EventWindowCloseReason.User, returnHome: false));
        CloseLatencyMs = WaitSent<WindowClosed>(m => m.EventKey == second.EventKey, closePosted, 3000);
        CloseStopLatencyMs = WaitValue(() => controls.Get("cam-c")?.StopAtMs, closePosted, 3000);

        SleepUntil(floodStart + 20_000);
        BackgroundMarkerRanDuringFlood = markerRan;
        FloodSeconds = (_watch.Elapsed.TotalMilliseconds - floodStart) / 1000.0;
        _floodStop = true;
    }

    public void Dispose()
    {
        _floodStop = true;
        try { _dispatcher.InvokeShutdown(); }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException) { /* 이미 내려감 */ }
    }

    // ───────── 장치 ─────────

    private static Dispatcher StartUiThread()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "flood-ui" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("UI thread did not start");
        return dispatcher!;
    }

    /// <summary>Normal 4줄 + Render 4줄 — 일 하나가 4 ms 를 태우고 곧바로 자기를 다시 넣는다(디스패처에 늘 Normal · Render 일이 있다).</summary>
    private void StartFlood()
    {
        foreach (var priority in new[] { DispatcherPriority.Normal, DispatcherPriority.Render })
        {
            for (int i = 0; i < 4; i++)
            {
                Action? chain = null;
                chain = () =>
                {
                    if (_floodStop) return;
                    var spin = Stopwatch.StartNew();
                    while (spin.ElapsedMilliseconds < 4) { }
                    _dispatcher.BeginInvoke(priority, chain!);
                };
                _dispatcher.BeginInvoke(priority, chain);
            }
        }
    }

    private static OpenEventWindow Window(string eventId, params string[] cameraIds)
    {
        var msg = new OpenEventWindow { Kind = EventWindowKind.Detection, EventId = eventId, Header = new EventWindowHeader { ZoneName = "구역-01" } };
        foreach (var id in cameraIds)
        {
            msg.Cameras.Add(new EventWindowCamera
            {
                CameraId = id,
                Name = id,
                IsPtz = true,
                PtzAllowed = true,
                Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://127.0.0.1:1/" + id },
            });
        }
        return msg;
    }

    private Recovery FailAndMeasure(string label, EventWindowManager manager, ScriptedProducerFactory producers, string eventKey, string cameraId)
    {
        string streamId = $"{eventKey}#{cameraId}";
        int before = producers.CreatedCount(streamId);
        double failedAt = _watch.Elapsed.TotalMilliseconds;
        producers.Latest(streamId)!.Report(StreamState.Stalled, "end-reached");

        double? retryAt = WaitValue(() => producers.CreatedCount(streamId) > before ? producers.LatestCreatedAtMs(streamId) : null, failedAt, 5000);
        double? playingAt = null;
        if (retryAt is not null)
        {
            // 화면 상태(UI 스레드) — 끊김(멈춤)을 거쳐 다시 재생으로
            playingAt = WaitValue(() => TileState(manager, eventKey, cameraId) == StreamState.Playing && producers.Latest(streamId)!.Attempt > 0
                ? _watch.Elapsed.TotalMilliseconds : null, failedAt, 5000);
        }
        return new Recovery(label, retryAt, playingAt);
    }

    /// <summary>UI 스레드에서 읽는다(Normal — 홍수 속에서도 차례가 온다). 못 읽으면 null.</summary>
    private StreamState? TileState(EventWindowManager manager, string eventKey, string cameraId)
    {
        var op = _dispatcher.InvokeAsync(() => manager.Find(eventKey)?.CameraTiles.FirstOrDefault(t => t.CameraId == cameraId)?.StreamState);
        return op.Wait(TimeSpan.FromSeconds(2)) == DispatcherOperationStatus.Completed ? op.Result : null;
    }

    private double? WaitSent<T>(Func<T, bool> match, double sinceMs, int timeoutMs) where T : class, IIpcMessage
        => WaitValue(() =>
        {
            foreach (var (message, at) in _sent)
                if (at >= sinceMs && message is T typed && match(typed)) return at;
            return null;
        }, sinceMs, timeoutMs);

    /// <summary><paramref name="read"/> 가 시각(ms)을 돌려줄 때까지 기다리고, <paramref name="sinceMs"/> 부터 걸린 시간을 돌려준다.</summary>
    private double? WaitValue(Func<double?> read, double sinceMs, int timeoutMs)
    {
        var limit = Stopwatch.StartNew();
        while (limit.ElapsedMilliseconds < timeoutMs)
        {
            if (read() is { } at) return Math.Max(0, at - sinceMs);
            Thread.Sleep(5);
        }
        return null;
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var limit = Stopwatch.StartNew();
        while (limit.ElapsedMilliseconds < timeoutMs && !condition()) Thread.Sleep(10);
    }

    private void SleepUntil(double atMs)
    {
        double left = atMs - _watch.Elapsed.TotalMilliseconds;
        if (left > 0) Thread.Sleep(TimeSpan.FromMilliseconds(left));
    }

    // ───────── 가짜 ─────────

    /// <summary>손으로 상태를 알리는 생산자 — 첫 시도든 재시도든 시작하면 곧 재생을 알린다.</summary>
    private sealed class ScriptedProducer : IFrameProducer
    {
        private Action<StreamState, string?>? _onState;

        public ScriptedProducer(int attempt, double createdAtMs)
        {
            Attempt = attempt;
            CreatedAtMs = createdAtMs;
        }

        public int Attempt { get; }
        public double CreatedAtMs { get; }

        public void Start(IFrameSink sink, Action<StreamState, string?> onState)
        {
            _onState = onState;
            _ = Task.Run(() => onState(StreamState.Playing, null));
        }

        public void Report(StreamState state, string? detail) => _onState?.Invoke(state, detail);

        public void Dispose() => _onState = null;
    }

    private sealed class ScriptedProducerFactory : IFrameProducerFactory
    {
        private readonly Stopwatch _watch;
        private readonly ConcurrentDictionary<string, List<ScriptedProducer>> _created = new();

        public ScriptedProducerFactory(Stopwatch watch) => _watch = watch;

        public IFrameProducer Create(VideoProviderInfo provider, int width, int height, string name, string? cameraId = null, bool priority = false, int attempt = 0)
        {
            var producer = new ScriptedProducer(attempt, _watch.Elapsed.TotalMilliseconds);
            var list = _created.GetOrAdd(name, _ => new List<ScriptedProducer>());
            lock (list) list.Add(producer);
            return producer;
        }

        public int CreatedCount(string name)
        {
            if (!_created.TryGetValue(name, out var list)) return 0;
            lock (list) return list.Count;
        }

        public ScriptedProducer? Latest(string name)
        {
            if (!_created.TryGetValue(name, out var list)) return null;
            lock (list) return list.Count == 0 ? null : list[^1];
        }

        public double? LatestCreatedAtMs(string name) => Latest(name)?.CreatedAtMs;
    }

    private sealed class RecordingControl : ITileCameraControl
    {
        private readonly Stopwatch _watch;
        private double _moveAt = -1;
        private double _stopAt = -1;

        public RecordingControl(Stopwatch watch) => _watch = watch;

        public double? MoveAtMs => Volatile.Read(ref _moveAt) < 0 ? null : Volatile.Read(ref _moveAt);
        public double? StopAtMs => Volatile.Read(ref _stopAt) < 0 ? null : Volatile.Read(ref _stopAt);

        public string? PtzUnavailableReason => null;

        public Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct)
        {
            Volatile.Write(ref _moveAt, _watch.Elapsed.TotalMilliseconds);
            return Task.FromResult(true);
        }

        public Task StopAsync(CancellationToken ct)
        {
            Volatile.Write(ref _stopAt, _watch.Elapsed.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public Task<bool> DragMoveAsync(double viewX, double viewY, double viewAspect, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<TilePreset>>(Array.Empty<TilePreset>());
        public Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class RecordingControls : ITileCameraControlFactory
    {
        private readonly Stopwatch _watch;
        private readonly ConcurrentDictionary<string, RecordingControl> _created = new();

        public RecordingControls(Stopwatch watch) => _watch = watch;

        public RecordingControl? Get(string cameraId) => _created.TryGetValue(cameraId, out var c) ? c : null;

        public ITileCameraControl Create(EventWindowCamera camera) => _created[camera.CameraId] = new RecordingControl(_watch);
    }

    /// <summary>준비가 끝난 PTZ 제공자 — 정지가 닿은 시각만 적는다.</summary>
    private sealed class StopClockPtzProvider : ICameraPtzProvider
    {
        private readonly Stopwatch _watch;
        private double _stopAt = -1;

        public StopClockPtzProvider(Stopwatch watch) => _watch = watch;

        public double? StopAtMs => Volatile.Read(ref _stopAt) < 0 ? null : Volatile.Read(ref _stopAt);

        public Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct) => Task.FromResult(new PtzReadiness(true, true, false));
        public bool IsPrepared(string cameraId) => true;
        public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct) => Task.FromResult(true);

        public Task StopAsync(string cameraId, CancellationToken ct)
        {
            Volatile.Write(ref _stopAt, _watch.Elapsed.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public Task<PtzDragOutcome> DragMoveAsync(string cameraId, double viewX, double viewY, double viewAspect, CancellationToken ct)
            => Task.FromResult(new PtzDragOutcome(true, PtzDragMoveKind.RelativeGeneric, viewX, viewY, null));
        public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PtzPresetInfo>?>(null);
        public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public bool IsImagingCapable(string cameraId) => false;
        public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct) => Task.FromResult<CameraImagingState?>(null);
        public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct) => Task.FromResult(false);
        public Task StopFocusAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
        public void Release(string cameraId) { }
    }
}
