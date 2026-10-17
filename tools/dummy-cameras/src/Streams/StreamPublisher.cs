using DummyCameras.Infra;

namespace DummyCameras.Streams;

/// <summary>
/// Supervises one camera's ffmpeg publisher: restarts it 2 s after an unexpected exit (e.g. MediaMTX not up yet),
/// but stays down after an intentional kill (<see cref="Kill"/> · <c>--kill-stream-after</c>) until <see cref="Restart"/>.
/// </summary>
public sealed class StreamPublisher : IAsyncDisposable
{
    private readonly CameraSpec _cam;
    private readonly string _ffmpeg;
    private readonly string _workDir;
    private readonly string _logPath;
    private readonly bool _withFont;
    private readonly JsonlLog _events;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private ManagedProcess? _proc;
    private volatile bool _killed;
    private TaskCompletionSource _restartSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _loop;
    private Timer? _killTimer;

    public StreamPublisher(CameraSpec cam, string ffmpeg, string workDir, string logDir, bool withFont, JsonlLog events)
    {
        _cam = cam;
        _ffmpeg = ffmpeg;
        _workDir = workDir;
        _logPath = Path.Combine(logDir, $"ffmpeg-{cam.Id}.log");
        _withFont = withFont;
        _events = events;
    }

    public int Starts { get; private set; }
    public bool IsKilled => _killed;
    public bool IsRunning { get { lock (_gate) return _proc is { HasExited: false }; } }
    public IReadOnlyList<string> Tail { get { lock (_gate) return _proc?.Tail ?? Array.Empty<string>(); } }

    public void Start()
    {
        _loop = Task.Run(LoopAsync);
        if (_cam.Faults.KillStreamAfterSec is int s && s >= 0)
            _killTimer = new Timer(_ => Kill("kill-stream-after"), null, TimeSpan.FromSeconds(s), Timeout.InfiniteTimeSpan);
    }

    private async Task LoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            if (_killed)
            {
                Task wait;
                lock (_gate) wait = _restartSignal.Task;
                try { await wait.WaitAsync(ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                continue;
            }

            ManagedProcess proc;
            lock (_gate)
            {
                proc = ManagedProcess.Start(_ffmpeg, FfmpegArgs.Build(_cam, _withFont), _workDir, _logPath);
                _proc = proc;
                Starts++;
            }
            Event("stream-start", $"pid={proc.Id}");
            try { await proc.WaitForExitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            Event("stream-exit", $"code={proc.ExitCode} killed={_killed}");
            if (!_killed)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Camera "power off": stop publishing and stay down.</summary>
    public void Kill(string reason)
    {
        lock (_gate)
        {
            _killed = true;
            _proc?.Kill();
        }
        Event("stream-kill", reason);
    }

    public void Restart()
    {
        lock (_gate)
        {
            _killed = false;
            _proc?.Kill();
            _restartSignal.TrySetResult();
            _restartSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Event("stream-restart", null);
    }

    private void Event(string kind, string? detail) => _events.Write(new Dictionary<string, object?>
    {
        ["ts"] = DateTime.UtcNow.ToString("O"),
        ["cam"] = _cam.Id,
        ["event"] = kind,
        ["detail"] = detail,
    });

    public async ValueTask DisposeAsync()
    {
        _killTimer?.Dispose();
        _cts.Cancel();
        lock (_gate) _proc?.Dispose();
        if (_loop is not null) { try { await _loop.ConfigureAwait(false); } catch { } }
        _cts.Dispose();
    }
}
