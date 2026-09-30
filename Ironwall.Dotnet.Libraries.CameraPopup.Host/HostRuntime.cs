using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Ipc;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Streams;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Watchdogs;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

/// <summary>
/// 호스트 본체. 파이프 읽기는 배경 스레드, 창 조작은 UI 스레드(디스패처), 스트림 생산은 각자의 스레드.
/// 메시지 처리기 하나의 실패는 <see cref="HostError"/> 로 알리고 호스트는 계속 산다.
/// 연결이 끊기면(GIS 가 닫음 · 감시자가 버림) 호스트는 스스로 내려간다 — 새 호스트는 감시자가 띄운다.
/// </summary>
internal sealed class HostRuntime
{
    private readonly HostLaunchArguments _launch;
    private readonly HostLog _log;
    private readonly Dispatcher _dispatcher;
    private readonly OverlayStreamManager _streams;
    private readonly EventWindowManager _windows;
    private readonly HostCameraServices? _cameras;
    private HostConnection? _connection;
    private MemoryWatchdog? _memoryWatchdog;

    public HostRuntime(HostLaunchArguments launch, HostLog log, Dispatcher dispatcher)
    {
        _launch = launch;
        _log = log;
        _dispatcher = dispatcher;
        _cameras = CreateCameraServices(log);
        var factory = new FrameProducerFactory(log, _cameras);
        _streams = new OverlayStreamManager(factory, Send, log);
        _windows = new EventWindowManager(dispatcher, factory, Send, log, launch.Headless);
    }

    public void Start()
    {
        ParentProcessWatch.Start(_launch.ParentProcessId, _log);
        _memoryWatchdog = new MemoryWatchdog((long)_launch.MemoryLimitMb * 1024 * 1024, TimeSpan.FromSeconds(2), OnMemoryExceeded);
        _ = Task.Run(RunPipeAsync);
        if (!_launch.Headless) _ = Task.Run(PrewarmLibVlc);
    }

    /// <summary>
    /// 카메라 제공자 창구(T-02 — ONVIF 영상 주소 · PTZ). 만들다 실패해도 호스트는 뜬다 — 그때 카메라 스트림은
    /// 저장 RTSP 주소를 그대로 열고, PTZ 는 "지원 안 함"으로 답한다.
    /// </summary>
    private HostCameraServices? CreateCameraServices(HostLog log)
    {
        try
        {
            var registry = CameraProviderRegistry.CreateDefault(new HostLogService(log));
            return new HostCameraServices(registry, log, Send);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Error($"camera providers unavailable: {ex}");
            return null;
        }
    }

    /// <summary>
    /// LibVLC 코어를 미리 올린다(T-02) — 첫 영상의 냉시작 비용(Core.Initialize + 플러그인 탐색, T-00 실측 9.4 s)을
    /// 첫 더블클릭 전에 배경에서 낸다. 헤드리스(시험) 호스트는 건너뛴다. 실패해도 첫 스트림이 다시 시도한다.
    /// </summary>
    private void PrewarmLibVlc()
    {
        try { LibVlcRuntime.Get(_log); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Warn($"libvlc prewarm failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    private void OnMemoryExceeded(long bytes)
    {
        _log.Warn($"memory limit exceeded: {bytes / (1024 * 1024)} MB > {_launch.MemoryLimitMb} MB — planned restart");
        Send(new HostError { Code = "memory-limit", Message = $"{bytes / (1024 * 1024)} MB" });
        Thread.Sleep(200); // 알림이 파이프로 나갈 시간
        HostExit.Now(HostExitCodes.MemoryLimit, "memory limit");
    }

    private void Send(IIpcMessage message) => _connection?.Send(message);

    private async Task RunPipeAsync()
    {
        var connection = await PipeServer.AcceptAsync(_launch, _log).ConfigureAwait(false);
        if (connection is null) return;
        _connection = connection;
        connection.StartWriter();

        try
        {
            while (true)
            {
                var frame = await FrameCodec.ReadFrameAsync(connection.Stream, connection.Token).ConfigureAwait(false);
                if (frame is null) break;
                var status = IpcSerializer.TryDeserialize(frame, out var message, out _, out var type);
                if (status != DecodeStatus.Ok || message is null)
                {
                    _log.Warn($"skip message type={type} status={status}");
                    if (status == DecodeStatus.Malformed) Send(new HostError { Code = "bad-message", Message = type });
                    continue;
                }
                Dispatch(message);
            }
            _log.Info("client closed the pipe");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or OperationCanceledException or ObjectDisposedException)
        {
            _log.Warn($"pipe read ended: {ex.GetType().Name} {ex.Message}");
        }
        HostExit.Now(HostExitCodes.ClientDisconnected, "client disconnected");
    }

    private void Dispatch(IIpcMessage message)
    {
        try
        {
            switch (message)
            {
                case Heartbeat hb:
                    // UI 스레드를 한 바퀴 돌아 응답 — UI 멈춤도 무응답으로 드러난다.
                    _dispatcher.BeginInvoke(() => Guard("heartbeat", () => Send(new HeartbeatAck
                    {
                        Sequence = hb.Sequence,
                        PrivateBytes = MemoryWatchdog.CurrentPrivateBytes(),
                        OpenStreams = _streams.Count,
                        OpenWindows = _windows.Count,
                    })));
                    break;
                case OpenOverlayStream open:
                    _streams.Open(open);
                    break;
                case CloseStream close:
                    _streams.Close(close.StreamId);
                    break;
                case OpenEventWindow window:
                    _dispatcher.BeginInvoke(() => Guard("open-window", () => _windows.Open(window)));
                    break;
                case CloseEventWindow closeWindow:
                    _dispatcher.BeginInvoke(() => Guard("close-window", () => _windows.Close(closeWindow.EventKey, closeWindow.Reason, closeWindow.ReturnHome)));
                    break;
                case BringToFront front:
                    _dispatcher.BeginInvoke(() => Guard("bring-to-front", () => _windows.BringToFront(front.EventKey)));
                    break;
                case SetTheme theme:
                    _dispatcher.BeginInvoke(() => Guard("set-theme", () => _windows.SetTheme(theme.Theme)));
                    break;
                case PtzCommand ptz:
                    // 받은 순서대로 제공자 게이트에 줄을 세운다(정지 우선 — FR-22). 기다리지 않는다.
                    if (_cameras is null) Send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = ptz.CameraId, Message = "ptz" });
                    else _cameras.HandlePtz(ptz);
                    break;
                case PtzFocusCommand focus:
                    if (_cameras is null) Send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = focus.CameraId, Message = "focus" });
                    else _cameras.HandleFocus(focus);
                    break;
                case CameraRequest request:
                    if (_cameras is null) Send(CameraResponse.Fail(request, CameraErrorCodes.NotSupported, "camera providers unavailable"));
                    else _cameras.HandleRequest(request);
                    break;
                case DebugCommand debug:
                    if (_launch.DebugCommands) DebugCrasher.Execute(debug.Kind, _dispatcher, _log);
                    else Send(new HostError { Code = "debug-disabled" });
                    break;
                default:
                    _log.Warn($"unexpected message {message.GetType().Name}");
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"handler failed {message.GetType().Name}: {ex}");
            Send(new HostError { Code = "handler-failed", Message = message.GetType().Name });
        }
    }

    private void Guard(string name, Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"{name} failed: {ex}");
            Send(new HostError { Code = "handler-failed", Message = name });
        }
    }
}
