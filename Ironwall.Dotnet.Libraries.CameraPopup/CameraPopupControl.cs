using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/****************************************************************************
   Purpose      : GIS 쪽 카메라 조작 창구 — PTZ · 프리셋 · 영상 옵션을 호스트로(PRD camera-popup-modes FR-04 · 17 · 26~28)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 더블클릭 팝업의 PTZ · 프리셋 · 옵션 탭이 부르는 유일한 창구. GIS 프로세스는 ONVIF 를 직접 부르지 않는다(§0) —
/// 조작은 호스트로 가고, 결과는 <see cref="CameraResponse"/> 로 돌아온다.
/// <list type="bullet">
/// <item><b>순간 조작</b>(<see cref="Move"/> · <see cref="Stop"/> · <see cref="Focus"/>) — 보내고 잊기. 같은 카메라의 밀린 PTZ 는
/// 최신 하나로 합쳐진다(이동 뒤 정지 → 정지만). 포커스는 합치기 키가 따로다.</item>
/// <item><b>요청</b>(<see cref="RequestAsync"/>) — 절대 던지지 않고 UI 스레드를 막지 않는다. 호스트가 없으면 즉시,
/// 응답이 늦으면 제한 시간 뒤, 호스트가 재시작되면 그 순간 실패 응답으로 끝난다.</item>
/// </list>
/// </summary>
public interface ICameraPopupControl : IDisposable
{
    /// <summary>호스트가 명령을 받을 수 있는 상태인가(<see cref="CameraPopupHostState.Running"/>).</summary>
    bool IsAvailable { get; }

    Task<CameraResponse> RequestAsync(CameraRequest request, CancellationToken ct = default);

    /// <summary>연속 이동(속도 -1..1). 호스트가 없으면 false.</summary>
    bool Move(string cameraId, VideoProviderInfo provider, double pan, double tilt, double zoom);

    bool Stop(string cameraId, VideoProviderInfo provider);

    /// <summary>
    /// 영상 위 드래그 → 드래그 길이만큼 상대 이동 <b>한 건</b>(보내고 잊기). <paramref name="viewX"/>/<paramref name="viewY"/> =
    /// 영상 상자 크기에 대한 드래그 비율(오른쪽 +, 아래 +), <paramref name="viewAspect"/> = 상자 가로/세로.
    /// 환산(화각 · 줌 · 카메라가 지원하는 이동 방식)은 호스트가 한다 — GIS 는 기다리지도 시간을 재지도 않는다.
    /// 같은 카메라의 밀린 PTZ 명령은 최신 하나로 합쳐진다(빠른 연속 드래그는 마지막 것만). 호스트가 없으면 false.
    /// </summary>
    bool DragMove(string cameraId, VideoProviderInfo provider, double viewX, double viewY, double viewAspect);

    /// <summary>수동 포커스(+1 원경 · -1 근경 · 0 정지).</summary>
    bool Focus(string cameraId, VideoProviderInfo provider, int direction);
}

/// <inheritdoc cref="ICameraPopupControl"/>
public sealed class CameraPopupControl : ICameraPopupControl
{
    /// <summary>호스트 제한 시간에 더하는 여유 — 호스트가 먼저 시간 초과 응답을 보낼 수 있게.</summary>
    public static readonly TimeSpan ResponseGrace = TimeSpan.FromSeconds(3);
    public const int DefaultRequestTimeoutMs = 12_000;

    private readonly ICameraPopupHost _host;
    private readonly ILogService? _log;
    private readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private int _disposed;

    public CameraPopupControl(ICameraPopupHost host, ILogService? log = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = log;
        _host.StatusReceived += OnStatusReceived;
        _host.StateChanged += OnStateChanged;
    }

    public bool IsAvailable => _host.State == CameraPopupHostState.Running;

    /// <summary>진행 중인 요청 수(시험 · 진단).</summary>
    public int PendingCount => _pending.Count;

    public Task<CameraResponse> RequestAsync(CameraRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        try
        {
            var stamped = string.IsNullOrEmpty(request.RequestId) ? WithId(request, Guid.NewGuid().ToString("N")) : request;
            if (Volatile.Read(ref _disposed) == 1) return Task.FromResult(CameraResponse.Fail(stamped, CameraErrorCodes.HostUnavailable, "disposed"));
            if (!IsAvailable) return Task.FromResult(CameraResponse.Fail(stamped, CameraErrorCodes.HostUnavailable, _host.State.ToString()));

            int hostTimeout = stamped.TimeoutMs > 0 ? stamped.TimeoutMs : DefaultRequestTimeoutMs;
            var pending = new Pending(stamped);
            _pending[stamped.RequestId] = pending;
            pending.Arm(TimeSpan.FromMilliseconds(hostTimeout) + ResponseGrace, ct, () => _pending.TryRemove(stamped.RequestId, out _));
            if (!_host.Send(stamped) && _pending.TryRemove(stamped.RequestId, out _))
                pending.Complete(CameraResponse.Fail(stamped, CameraErrorCodes.HostUnavailable, "send failed"));
            return pending.Task;
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraPopup] request {request.Kind} failed: {ex.GetType().Name} {ex.Message}");
            return Task.FromResult(CameraResponse.Fail(request, CameraErrorCodes.Failed, ex.GetType().Name));
        }
    }

    public bool Move(string cameraId, VideoProviderInfo provider, double pan, double tilt, double zoom)
        => SendPtz(new PtzCommand { CameraId = cameraId, Operation = PtzOperation.ContinuousMove, Pan = pan, Tilt = tilt, Zoom = zoom, Provider = provider });

    public bool Stop(string cameraId, VideoProviderInfo provider)
        => SendPtz(new PtzCommand { CameraId = cameraId, Operation = PtzOperation.Stop, Provider = provider });

    public bool DragMove(string cameraId, VideoProviderInfo provider, double viewX, double viewY, double viewAspect)
    {
        if (!double.IsFinite(viewX) || !double.IsFinite(viewY)) return false;
        return SendPtz(new PtzCommand
        {
            CameraId = cameraId,
            Operation = PtzOperation.DragMove,
            ViewX = Math.Clamp(viewX, -1d, 1d),
            ViewY = Math.Clamp(viewY, -1d, 1d),
            ViewAspect = double.IsFinite(viewAspect) && viewAspect > 0 ? viewAspect : 0d,
            Provider = provider,
        });
    }

    public bool Focus(string cameraId, VideoProviderInfo provider, int direction)
    {
        if (string.IsNullOrEmpty(cameraId) || !IsAvailable) return false;
        return _host.Send(new PtzFocusCommand { CameraId = cameraId, Direction = Math.Sign(direction), Provider = provider }, "focus:" + cameraId);
    }

    private bool SendPtz(PtzCommand command)
    {
        if (string.IsNullOrEmpty(command.CameraId) || !IsAvailable) return false;
        return _host.Send(command, "ptz:" + command.CameraId);
    }

    private void OnStatusReceived(object? sender, CameraPopupStatusEventArgs e)
    {
        if (e.Message is not CameraResponse response || string.IsNullOrEmpty(response.RequestId)) return;
        if (_pending.TryRemove(response.RequestId, out var pending)) pending.Complete(response);
    }

    private void OnStateChanged(object? sender, CameraPopupHostStateChangedEventArgs e)
    {
        if (e.NewState == CameraPopupHostState.Running) return;
        // 재시작 · 일시 중지 · 없음 — 옛 호스트에 보낸 요청의 응답은 오지 않는다.
        string code = e.NewState == CameraPopupHostState.Restarting ? CameraErrorCodes.HostRestarted : CameraErrorCodes.HostUnavailable;
        FailAll(code, e.NewState.ToString());
    }

    private void FailAll(string code, string message)
    {
        foreach (var key in _pending.Keys.ToArray())
            if (_pending.TryRemove(key, out var pending)) pending.Complete(CameraResponse.Fail(pending.Request, code, message));
    }

    private static CameraRequest WithId(CameraRequest r, string id) => new()
    {
        RequestId = id,
        Kind = r.Kind,
        CameraId = r.CameraId,
        Provider = r.Provider,
        PresetToken = r.PresetToken,
        PresetName = r.PresetName,
        IrCutFilter = r.IrCutFilter,
        AutoFocus = r.AutoFocus,
        TimeoutMs = r.TimeoutMs,
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _host.StatusReceived -= OnStatusReceived;
        _host.StateChanged -= OnStateChanged;
        FailAll(CameraErrorCodes.HostUnavailable, "disposed");
    }

    private sealed class Pending
    {
        private readonly TaskCompletionSource<CameraResponse> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource? _timer;
        private CancellationTokenRegistration _callerRegistration;

        public Pending(CameraRequest request) => Request = request;

        public CameraRequest Request { get; }
        public Task<CameraResponse> Task => _tcs.Task;

        public void Arm(TimeSpan timeout, CancellationToken callerCt, Action remove)
        {
            _timer = new CancellationTokenSource(timeout);
            _timer.Token.Register(() => { remove(); Complete(CameraResponse.Fail(Request, CameraErrorCodes.Timeout, $"{timeout.TotalMilliseconds:0} ms")); });
            if (callerCt.CanBeCanceled)
                _callerRegistration = callerCt.Register(() => { remove(); Complete(CameraResponse.Fail(Request, CameraErrorCodes.Timeout, "cancelled")); });
        }

        public void Complete(CameraResponse response)
        {
            if (!_tcs.TrySetResult(response)) return;
            _callerRegistration.Dispose();
            _timer?.Dispose();
        }
    }
}
