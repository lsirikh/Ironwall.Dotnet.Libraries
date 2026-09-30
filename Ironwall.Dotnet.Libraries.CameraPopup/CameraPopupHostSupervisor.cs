using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 호스트 감시자(PRD camera-popup-modes §0 · FR-24~29)
                  - 호스트 기동 · 파이프 연결 · 심박(1초) · 3초 무응답/프로세스 종료 → 강제 종료 + 재시작 + 복원
                  - 60초에 3번 넘게 죽으면 Suspended(Restart() 로 재개) · 실행 파일 없으면 Unavailable
                  - 공개 호출은 전부 기다리지 않고 던지지 않는다(FR-27/28)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 상태 전이는 전부 단일 제어 루프(배경 작업 하나)에서 직렬로 일어난다 — 경합 없이 "지금 몇 번째 기동인가"를 판단한다.
/// 공개 호출은 기록부(<see cref="HostSessionRegistry"/>)를 갱신하고, 살아 있는 연결이 있으면 대기열에 넣기만 한다.
/// </summary>
public sealed class CameraPopupHostSupervisor : ICameraPopupHost
{
    private const int ExitCodeWaitMs = 500;

    private readonly CameraPopupHostOptions _options;
    private readonly ILogService _log;
    private readonly Func<long> _clockMs;
    private readonly Channel<ControlEvent> _control = Channel.CreateUnbounded<ControlEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<IIpcMessage> _status = Channel.CreateBounded<IIpcMessage>(new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly HostSessionRegistry _registry = new();
    private readonly ConcurrentDictionary<string, SharedFrameSource> _sources = new(StringComparer.Ordinal);
    private readonly RestartBudget _crashBudget;
    private readonly RestartBudget _plannedBudget;
    private readonly Timer _heartbeatTimer;
    private readonly Task _controlLoop;
    private readonly Task _statusLoop;

    // ── 제어 루프 전용 상태 ──
    private Process? _process;
    private EventHandler? _processExitedHandler;
    private CameraPopupClient? _client;
    private int _incarnation;
    private int _activeIncarnation;
    private long _heartbeatSequence;

    // ── 어느 스레드에서나 읽는 상태 ──
    private volatile CameraPopupClient? _liveClient;
    private volatile CameraPopupHostState _state = CameraPopupHostState.NotStarted;
    private volatile string? _stateReason;
    private volatile string? _theme;
    private int _tickPending;
    private int _disposed;

    public CameraPopupHostSupervisor(CameraPopupHostOptions options, ILogService log)
        : this(options, log, () => Environment.TickCount64)
    {
    }

    internal CameraPopupHostSupervisor(CameraPopupHostOptions options, ILogService log, Func<long> clockMs)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _clockMs = clockMs;
        _crashBudget = new RestartBudget(options.MaxRestartsInWindow, options.CrashWindow);
        _plannedBudget = new RestartBudget(options.MaxPlannedRestartsInWindow, options.CrashWindow);
        _controlLoop = Task.Run(RunControlLoopAsync);
        _statusLoop = Task.Run(RunStatusLoopAsync);
        _heartbeatTimer = new Timer(OnHeartbeatTimer, null, options.HeartbeatInterval, options.HeartbeatInterval);
    }

    public CameraPopupHostState State => _state;
    public string? StateReason => _stateReason;

    /// <summary>호스트가 마지막 심박 응답에 실어 보낸 전용 메모리(바이트).</summary>
    public long LastHostPrivateBytes { get; private set; }

    /// <summary>지금 호스트 pid(없으면 null).</summary>
    public int? HostProcessId { get; private set; }

    internal HostSessionRegistry Registry => _registry;
    internal CameraPopupClient? LiveClient => _liveClient;

    public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;
    public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived;

    // ───────────────────────── 공개 호출(기다리지 않음 · 던지지 않음) ─────────────────────────

    public void Start() => Guard(nameof(Start), () => Post(new ControlEvent(ControlKind.Start)));

    public void Restart() => Guard(nameof(Restart), () => Post(new ControlEvent(ControlKind.ManualRestart)));

    public IFrameSource? OpenOverlay(OverlayStreamRequest request)
    {
        try
        {
            if (IsDisposed || request is null) return null;
            if (!SharedFrameLayout.IsValidSize(request.Width, request.Height))
            {
                _log.Warning($"[CameraPopup] overlay size out of range {request.Width}x{request.Height}");
                return null;
            }
            var streamId = string.IsNullOrWhiteSpace(request.StreamId) ? Guid.NewGuid().ToString("N") : request.StreamId!;
            if (_sources.ContainsKey(streamId)) CloseOverlay(streamId);

            var name = SharedFrameLayout.BuildName(Environment.ProcessId, Guid.NewGuid().ToString("N")[..12]);
            var view = SharedFrameView.CreateNew(name, request.Width, request.Height);
            var source = new SharedFrameSource(streamId, view, s => CloseOverlay(s.StreamId), _log);
            _sources[streamId] = source;

            var message = new OpenOverlayStream
            {
                StreamId = streamId,
                Camera = request.Camera,
                Provider = request.Provider,
                Width = request.Width,
                Height = request.Height,
                SharedMemoryName = name,
            };
            _registry.SetOverlay(message);
            SendIfLive(message);
            return source;
        }
        catch (Exception ex)
        {
            _log.Error($"[CameraPopup] OpenOverlay failed: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    public void CloseOverlay(string streamId) => Guard(nameof(CloseOverlay), () =>
    {
        if (string.IsNullOrEmpty(streamId)) return;
        bool known = _registry.RemoveOverlay(streamId);
        if (_sources.TryRemove(streamId, out var source)) source.Release();
        if (known) SendIfLive(new CloseStream { StreamId = streamId });
    });

    public void OpenEventWindow(OpenEventWindow request) => Guard(nameof(OpenEventWindow), () =>
    {
        if (IsDisposed || request is null || string.IsNullOrWhiteSpace(request.EventKey)) return;
        _registry.SetWindow(request);
        SendIfLive(request);
    });

    public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) => Guard(nameof(CloseEventWindow), () =>
    {
        if (string.IsNullOrEmpty(eventKey)) return;
        if (_registry.RemoveWindow(eventKey))
            SendIfLive(new CloseEventWindow { EventKey = eventKey, Reason = reason, ReturnHome = returnHome });
    });

    public void BringEventWindowToFront(string eventKey) => Guard(nameof(BringEventWindowToFront), () =>
    {
        if (string.IsNullOrEmpty(eventKey) || !_registry.HasWindow(eventKey)) return;
        SendIfLive(new BringToFront { EventKey = eventKey }, "front:" + eventKey);
    });

    public void SetTheme(string theme) => Guard(nameof(SetTheme), () =>
    {
        if (string.IsNullOrWhiteSpace(theme)) return;
        _theme = theme;
        SendIfLive(new SetTheme { Theme = theme }, "theme");
    });

    public void SendPtz(PtzCommand command) => Guard(nameof(SendPtz), () =>
    {
        if (command is null || string.IsNullOrEmpty(command.CameraId)) return;
        if (!SendIfLive(command, "ptz:" + command.CameraId))
            _log.Info($"[CameraPopup] PTZ dropped (host {State}) camera={command.CameraId} op={command.Operation}");
    });

    /// <summary>시험 전용 고장 주입 — <see cref="CameraPopupHostOptions.EnableDebugCommands"/> 일 때만 보낸다.</summary>
    internal bool SendDebugCommand(DebugCommandKind kind)
        => _options.EnableDebugCommands && SendIfLive(new DebugCommand { Kind = kind });

    private bool SendIfLive(IIpcMessage message, string? coalesceKey = null)
    {
        var client = _liveClient;
        return client is not null && client.TrySend(message, coalesceKey);
    }

    private void Guard(string entryPoint, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            _log.Error($"[CameraPopup] {entryPoint} failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private void Post(ControlEvent ev)
    {
        if (!_control.Writer.TryWrite(ev) && ev.Kind != ControlKind.Tick)
            _log.Warning($"[CameraPopup] control event {ev.Kind} dropped (disposed)");
    }

    private void OnHeartbeatTimer(object? state)
    {
        if (Interlocked.Exchange(ref _tickPending, 1) == 0) Post(new ControlEvent(ControlKind.Tick));
    }

    // ───────────────────────── 제어 루프 ─────────────────────────

    private async Task RunControlLoopAsync()
    {
        await foreach (var ev in _control.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (!await HandleAsync(ev).ConfigureAwait(false)) break;
            }
            catch (Exception ex)
            {
                _log.Error($"[CameraPopup] control loop {ev.Kind} failed: {ex}");
            }
        }
    }

    private async Task<bool> HandleAsync(ControlEvent ev)
    {
        switch (ev.Kind)
        {
            case ControlKind.Start:
                if (_state == CameraPopupHostState.NotStarted) await LaunchAsync(restart: false, reason: "start").ConfigureAwait(false);
                break;

            case ControlKind.ManualRestart:
                _crashBudget.Reset();
                _plannedBudget.Reset();
                TeardownHost("manual restart");
                await LaunchAsync(restart: false, reason: "manual restart").ConfigureAwait(false);
                break;

            case ControlKind.Relaunch:
                if (ev.Incarnation == _incarnation && _state == CameraPopupHostState.Restarting)
                    await LaunchAsync(restart: true, reason: ev.Reason ?? "relaunch").ConfigureAwait(false);
                break;

            case ControlKind.Tick:
                Interlocked.Exchange(ref _tickPending, 0);
                OnTick();
                break;

            case ControlKind.ProcessExited:
            case ControlKind.ClientFaulted:
                if (ev.Incarnation != 0 && ev.Incarnation == _activeIncarnation)
                    HandleFailure(ev.Reason ?? ev.Kind.ToString(), ev.ExitCode, awaitExit: ev.Kind == ControlKind.ClientFaulted);
                break;

            case ControlKind.Dispose:
                TeardownHost("dispose");
                SetState(CameraPopupHostState.Disposed, "dispose");
                _control.Writer.TryComplete();
                return false;
        }
        return true;
    }

    private void OnTick()
    {
        var client = _client;
        if (_state != CameraPopupHostState.Running || client is null) return;
        long silentMs = client.MillisecondsSinceLastReceive;
        if (silentMs > (long)_options.HeartbeatTimeout.TotalMilliseconds)
        {
            HandleFailure($"heartbeat timeout ({silentMs} ms)", null);
            return;
        }
        client.TrySend(new Heartbeat { Sequence = ++_heartbeatSequence }, "heartbeat");
    }

    private async Task LaunchAsync(bool restart, string reason)
    {
        var path = _options.ResolveExecutablePath();
        if (!File.Exists(path))
        {
            SetState(CameraPopupHostState.Unavailable, $"host executable not found: {path}");
            return;
        }

        int incarnation = ++_incarnation;
        if (!restart || _state != CameraPopupHostState.Restarting)
            SetState(restart ? CameraPopupHostState.Restarting : CameraPopupHostState.Starting, reason);

        var token = PipeNaming.CreateToken();
        var launch = new HostLaunchArguments
        {
            ParentProcessId = Environment.ProcessId,
            PipeName = PipeNaming.Build(Environment.ProcessId, token),
            Token = token,
            MemoryLimitMb = _options.HostMemoryLimitMb,
            Headless = _options.Headless,
            DebugCommands = _options.EnableDebugCommands,
            LogDirectory = _options.HostLogDirectory,
        };
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory,
        };
        foreach (var arg in launch.ToArgumentList()) startInfo.ArgumentList.Add(arg);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        using var exitCts = new CancellationTokenSource();
        EventHandler exitHandler = (_, _) =>
        {
            int? code = SafeExitCode(process);
            Post(new ControlEvent(ControlKind.ProcessExited, incarnation, $"host exited code={code?.ToString() ?? "?"}", code));
            try { exitCts.Cancel(); } catch (ObjectDisposedException) { }
        };
        process.Exited += exitHandler;
        _activeIncarnation = incarnation;

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Process.Start returned false");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            process.Exited -= exitHandler;
            process.Dispose();
            _log.Error($"[CameraPopup] host start failed: {ex.Message}");
            HandleFailure($"start failed: {ex.Message}", null);
            return;
        }
        _process = process;
        _processExitedHandler = exitHandler;
        HostProcessId = SafeProcessId(process);

        var client = new CameraPopupClient(launch.PipeName, token, _options.CommandQueueCapacity, _options.WriteTimeout, _log);
        HelloAck ack;
        try
        {
            ack = await client.ConnectAsync(_options.ConnectTimeout, exitCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or TimeoutException or InvalidDataException or EndOfStreamException or UnauthorizedAccessException)
        {
            client.Dispose();
            if (_activeIncarnation != incarnation) return; // 이미 종료 이벤트로 처리됨
            int? code = process.HasExited ? SafeExitCode(process) : null;
            HandleFailure($"connect failed: {ex.GetType().Name} {ex.Message}", code);
            return;
        }

        if (_activeIncarnation != incarnation)
        {
            client.Dispose();
            return;
        }
        if (!ProtocolVersion.IsCompatible(ack.ProtocolVersion))
        {
            client.Dispose();
            TeardownHost("protocol mismatch");
            SetState(CameraPopupHostState.Unavailable, $"host protocol {ack.ProtocolVersion} incompatible");
            return;
        }

        client.MessageReceived += m => OnHostMessage(m);
        client.Faulted += r => Post(new ControlEvent(ControlKind.ClientFaulted, incarnation, $"pipe fault: {r}", null));
        client.StartLoops();
        _client = client;
        _liveClient = client;

        // 복원 — 기록부를 그대로 다시 보낸다(창 → 오버레이). 이미 보낸 것과 겹쳐도 호스트 쪽 열기는 멱등이다.
        var (windows, overlays) = _registry.Snapshot();
        if (_theme is { } theme) client.TrySend(new SetTheme { Theme = theme }, "theme");
        foreach (var w in windows) client.TrySend(w);
        foreach (var o in overlays)
        {
            if (_sources.TryGetValue(o.StreamId, out var src)) src.SetState(StreamState.Opening, "host restarted");
            client.TrySend(o);
        }
        SetState(CameraPopupHostState.Running,
            $"host pid={ack.HostProcessId} replayed windows={windows.Length} overlays={overlays.Length}", ack.HostProcessId);
    }

    private void HandleFailure(string reason, int? exitCode, bool awaitExit = false)
    {
        // 파이프가 먼저 끊긴 경우(충돌 · 메모리 한도 종료) 프로세스가 곧 끝난다 — 종료 코드를 잠깐 기다려 읽는다.
        // 코드를 못 읽고 죽이면 메모리 한도(20) 같은 계획된 재시작이 충돌로 잘못 세어진다.
        if (exitCode is null && awaitExit && _process is { } running)
        {
            try
            {
                if (running.WaitForExit(ExitCodeWaitMs)) exitCode = running.ExitCode;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                _log.Warning($"[CameraPopup] exit code read failed: {ex.Message}");
            }
        }
        if (exitCode is not null) reason = $"{reason} (exit {exitCode})";
        TeardownHost(reason);
        bool planned = exitCode.HasValue && HostExitCodes.IsPlannedRestart(exitCode.Value);
        var budget = planned ? _plannedBudget : _crashBudget;
        long now = _clockMs();
        if (!budget.TryRecordFailure(now))
        {
            _log.Error($"[CameraPopup] host failed {budget.CountInWindow(now)} times in {_options.CrashWindow.TotalSeconds:0}s — suspended. last: {reason}");
            SetState(CameraPopupHostState.Suspended, reason, null, exitCode);
            return;
        }
        _log.Warning($"[CameraPopup] host failure ({(planned ? "planned" : "crash")}): {reason} — restarting");
        SetState(CameraPopupHostState.Restarting, reason, null, exitCode);
        Post(new ControlEvent(ControlKind.Relaunch, _incarnation, reason, null));
    }

    private void TeardownHost(string reason)
    {
        _activeIncarnation = 0;
        _liveClient = null;
        var client = _client;
        _client = null;
        client?.Dispose();

        var process = _process;
        _process = null;
        HostProcessId = null;
        if (process is null) return;
        if (_processExitedHandler is not null) process.Exited -= _processExitedHandler;
        _processExitedHandler = null;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
                _log.Info($"[CameraPopup] host killed ({reason})");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            _log.Warning($"[CameraPopup] host kill failed: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void OnHostMessage(IIpcMessage message)
    {
        switch (message)
        {
            case HeartbeatAck ack:
                LastHostPrivateBytes = ack.PrivateBytes;
                return; // 심박 응답은 알리지 않는다(소음)
            case StreamStateChanged s when s.EventKey is null:
                if (_sources.TryGetValue(s.StreamId, out var source)) source.SetState(s.State, s.Detail);
                break;
            case WindowClosed closed:
                _registry.RemoveWindow(closed.EventKey); // 사람 · 타이머로 닫힌 창은 복원하지 않는다
                break;
            case PinChanged or WindowMoved or TileClosed:
                _registry.ApplyHostNotice(message); // 재시작 복원 때 사람 조작을 되살린다
                break;
            case HostError error:
                _log.Warning($"[CameraPopup] host error {error.Code} scope={error.Scope} {error.Message}");
                break;
        }
        _status.Writer.TryWrite(message);
    }

    private async Task RunStatusLoopAsync()
    {
        await foreach (var message in _status.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var handlers = StatusReceived;
            if (handlers is null) continue;
            var args = new CameraPopupStatusEventArgs(message);
            foreach (EventHandler<CameraPopupStatusEventArgs> h in handlers.GetInvocationList())
            {
                try { h(this, args); }
                catch (Exception ex) { _log.Error($"[CameraPopup] StatusReceived subscriber failed: {ex.Message}"); }
            }
        }
    }

    private void SetState(CameraPopupHostState state, string? reason, int? hostProcessId = null, int? exitCode = null)
    {
        var old = _state;
        _state = state;
        _stateReason = reason;
        _log.Info($"[CameraPopup] host state {old} -> {state}: {reason}");
        var handlers = StateChanged;
        if (handlers is null) return;
        var args = new CameraPopupHostStateChangedEventArgs(old, state, reason, hostProcessId, exitCode);
        // 구독자(보통 UI 안내)가 느려도 제어 루프는 멈추지 않는다.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            foreach (EventHandler<CameraPopupHostStateChangedEventArgs> h in handlers.GetInvocationList())
            {
                try { h(this, args); }
                catch (Exception ex) { _log.Error($"[CameraPopup] StateChanged subscriber failed: {ex.Message}"); }
            }
        });
    }

    private static int? SafeExitCode(Process process)
    {
        try { return process.HasExited ? process.ExitCode : null; }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException) { return null; }
    }

    private static int? SafeProcessId(Process process)
    {
        try { return process.Id; }
        catch (InvalidOperationException) { return null; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try
        {
            _heartbeatTimer.Dispose();
            Post(new ControlEvent(ControlKind.Dispose));
            if (!_controlLoop.Wait(TimeSpan.FromSeconds(5)))
                _log.Warning("[CameraPopup] supervisor dispose timed out");
            _status.Writer.TryComplete();
            foreach (var source in _sources.Values) source.Release();
            _sources.Clear();
        }
        catch (Exception ex)
        {
            _log.Error($"[CameraPopup] supervisor dispose failed: {ex.Message}");
        }
    }

    private enum ControlKind
    {
        Start,
        ManualRestart,
        Relaunch,
        Tick,
        ProcessExited,
        ClientFaulted,
        Dispose,
    }

    private readonly record struct ControlEvent(ControlKind Kind, int Incarnation = 0, string? Reason = null, int? ExitCode = null);
}
