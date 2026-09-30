using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

/****************************************************************************
   Purpose      : 호스트 안 카메라 제공자 창구 — 영상 주소 · PTZ · 영상 옵션(PRD camera-popup-modes FR-17/18 · FR-22 · FR-26~28)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// GIS 가 보낸 PTZ 명령 · 요청을 제공자(<see cref="CameraProviderRegistry"/>)로 옮긴다. 이 클래스 안의 무엇이 실패해도
/// 그 카메라 · 그 요청만 실패한다(응답 · <see cref="HostError"/>) — 호스트는 산다.
/// <list type="bullet">
/// <item><b>순서</b>: <see cref="HandlePtz"/> · <see cref="HandleFocus"/> 는 파이프 읽기 줄에서 <b>받은 순서대로 동기 호출</b>해
/// 제공자 게이트에 줄을 세운다(정지 우선 게이트는 호출 순서를 본다 — FR-22). 준비 안 된 카메라는 준비가 끝날 때까지
/// 카메라별 "마지막 동작 하나"만 들고 있다가 보낸다(밀린 이동 뒤 정지 → 정지만).</item>
/// <item><b>R-1 보상</b>: 연속 이동이 실패하면 그 뒤로 새 동작이 오지 않았을 때만 정지를 한 번 보낸다.</item>
/// <item><b>제한 시간</b>: 요청마다 <see cref="CameraRequest.TimeoutMs"/>(기본 <see cref="DefaultRequestTimeoutMs"/>) —
/// 넘으면 <see cref="CameraErrorCodes.Timeout"/> 응답(FR-26).</item>
/// </list>
/// 이벤트 창 타일(T-05)도 같은 창구를 쓴다: <see cref="ExecuteAsync"/>(프리셋 이동 · 복귀) · <see cref="HandlePtz"/>.
/// </summary>
internal sealed class HostCameraServices
{
    public const int DefaultRequestTimeoutMs = 12_000;
    public const int DefaultResolveTimeoutMs = 12_000;
    public const int MotionTimeoutMs = 5_000;

    private readonly CameraProviderRegistry _registry;
    private readonly HostLog _log;
    private readonly Action<IIpcMessage> _send;
    private readonly object _gate = new();
    private readonly Dictionary<string, VideoProviderInfo> _endpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _warming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingMotion> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _motionSequence = new(StringComparer.Ordinal);

    public HostCameraServices(CameraProviderRegistry registry, HostLog log, Action<IIpcMessage> send)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log;
        _send = send;
    }

    // ───────────────────────── 영상 주소 ─────────────────────────

    /// <summary>
    /// 재생 주소 얻기(오버레이 · 타일 생산자가 부른다). 던지지 않는다 — 지원 안 함은 <see cref="CameraErrorCodes.NotSupported"/>.
    /// </summary>
    public async Task<StreamResolution> ResolveStreamAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        Remember(cameraId, info);
        var provider = _registry.GetVideo(info.Kind);
        if (provider is null) return StreamResolution.Fail(CameraErrorCodes.NotSupported, $"no video provider for {info.Kind}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(DefaultResolveTimeoutMs);
        var started = Environment.TickCount64;
        try
        {
            var result = await provider.ResolveStreamAsync(cameraId, info, timeout.Token).ConfigureAwait(false);
            _log.Info($"resolve cam={cameraId} {info.Kind} → {result} in {Environment.TickCount64 - started} ms");
            return result;
        }
        catch (NotSupportedException ex)
        {
            _log.Warn($"resolve cam={cameraId} {info.Kind} not supported: {ex.Message}");
            return StreamResolution.Fail(CameraErrorCodes.NotSupported, info.Kind.ToString());
        }
        catch (OperationCanceledException)
        {
            return StreamResolution.Fail(CameraErrorCodes.Timeout, "resolve cancelled");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"resolve cam={cameraId} failed: {HostLogService.Mask(ex.Message)}");
            return StreamResolution.Fail(CameraErrorCodes.ResolveFailed, ex.GetType().Name);
        }
    }

    // ───────────────────────── 보내고 잊는 PTZ · 포커스 ─────────────────────────

    /// <summary>파이프 읽기 줄에서 받은 순서대로 부른다(기다리지 않는다).</summary>
    public void HandlePtz(PtzCommand command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.CameraId)) return;
        var info = Remember(command.CameraId, command.Provider);
        var provider = info is null ? null : _registry.GetPtz(info.Kind);
        if (provider is null)
        {
            _send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = command.CameraId, Message = $"ptz {command.Operation}" });
            return;
        }
        Execute(command.CameraId, info!, provider, new PendingMotion(command, null));
    }

    public void HandleFocus(PtzFocusCommand command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.CameraId)) return;
        var info = Remember(command.CameraId, command.Provider);
        var provider = info is null ? null : _registry.GetPtz(info.Kind);
        if (provider is null)
        {
            _send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = command.CameraId, Message = "focus" });
            return;
        }
        Execute(command.CameraId, info!, provider, new PendingMotion(null, command));
    }

    private void Execute(string cameraId, VideoProviderInfo info, ICameraPtzProvider provider, PendingMotion motion)
    {
        try
        {
            long sequence;
            lock (_gate)
            {
                if (motion.Ptz is not null)
                {
                    sequence = _motionSequence.TryGetValue(cameraId, out var s) ? s + 1 : 1;
                    _motionSequence[cameraId] = sequence;
                }
                else
                {
                    sequence = 0;
                }

                if (!provider.IsPrepared(cameraId))
                {
                    // 준비 전 — 마지막 PTZ 동작 · 마지막 포커스 동작만 남긴다(밀린 이동 뒤 정지 → 정지만).
                    var pending = _pending.TryGetValue(cameraId, out var p) ? p : new PendingMotion(null, null);
                    _pending[cameraId] = new PendingMotion(motion.Ptz ?? pending.Ptz, motion.Focus ?? pending.Focus);
                    if (!_warming.ContainsKey(cameraId))
                        _warming[cameraId] = WarmThenFlushAsync(cameraId, info, provider);
                    return;
                }
            }
            Dispatch(cameraId, provider, motion, sequence);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"ptz execute cam={cameraId} failed: {ex.GetType().Name} {HostLogService.Mask(ex.Message)}");
        }
    }

    private async Task WarmThenFlushAsync(string cameraId, VideoProviderInfo info, ICameraPtzProvider provider)
    {
        try
        {
            using var timeout = new CancellationTokenSource(DefaultRequestTimeoutMs);
            var readiness = await provider.PrepareAsync(cameraId, info, timeout.Token).ConfigureAwait(false);
            _log.Info($"ptz warm cam={cameraId} connected={readiness.Connected} ptz={readiness.PtzCapable}");
        }
        catch (NotSupportedException)
        {
            _send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = cameraId, Message = "ptz" });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Warn($"ptz warm cam={cameraId} failed: {ex.GetType().Name} {HostLogService.Mask(ex.Message)}");
        }

        PendingMotion? pending;
        long sequence;
        lock (_gate)
        {
            _warming.Remove(cameraId);
            _pending.Remove(cameraId, out pending);
            sequence = _motionSequence.TryGetValue(cameraId, out var s) ? s : 0;
        }
        if (pending is null || !provider.IsPrepared(cameraId)) return;
        Dispatch(cameraId, provider, pending, sequence);
    }

    private void Dispatch(string cameraId, ICameraPtzProvider provider, PendingMotion motion, long sequence)
    {
        if (motion.Ptz is { } ptz) DispatchPtz(cameraId, provider, ptz, sequence);
        if (motion.Focus is { } focus)
        {
            var ct = Timeout(MotionTimeoutMs);
            Observe(focus.Direction == 0
                ? provider.StopFocusAsync(cameraId, ct)
                : provider.StartFocusAsync(cameraId, focus.Direction, ct), cameraId, "focus");
        }
    }

    private void DispatchPtz(string cameraId, ICameraPtzProvider provider, PtzCommand ptz, long sequence)
    {
        var ct = Timeout(MotionTimeoutMs);
        switch (ptz.Operation)
        {
            case PtzOperation.ContinuousMove:
                var move = provider.ContinuousMoveAsync(cameraId, Clamp(ptz.Pan), Clamp(ptz.Tilt), Clamp(ptz.Zoom), ct);
                _ = move.ContinueWith(t =>
                {
                    // R-1: 이동 SOAP 실패 → 그 뒤로 새 동작이 없을 때만 정지(나중 이동을 끊지 않게).
                    if (t.IsCompletedSuccessfully && t.Result) return;
                    if (!IsLatestMotion(cameraId, sequence)) return;
                    Observe(provider.StopAsync(cameraId, Timeout(MotionTimeoutMs)), cameraId, "stop(r-1)");
                }, TaskScheduler.Default);
                break;
            case PtzOperation.Stop:
                Observe(provider.StopAsync(cameraId, ct), cameraId, "stop");
                break;
            case PtzOperation.DragMove:
                ObserveDrag(provider.DragMoveAsync(cameraId, ptz.ViewX, ptz.ViewY, ptz.ViewAspect, ct), cameraId, Environment.TickCount64);
                break;
            case PtzOperation.GotoPreset when !string.IsNullOrWhiteSpace(ptz.PresetToken):
                Observe(provider.GotoPresetAsync(cameraId, ptz.PresetToken!, ct), cameraId, "goto-preset");
                break;
            case PtzOperation.GotoHome:
                Observe(provider.GotoHomeAsync(cameraId, ct), cameraId, "goto-home");
                break;
            default:
                _log.Warn($"ptz cam={cameraId} ignored op={ptz.Operation}");
                break;
        }
    }

    /// <summary>드래그 이동 결과 기록 — 못 보낸 이유(밀려서 버려짐 · 좌표 공간 없음 · 실패)를 호스트 로그에 남긴다.</summary>
    private void ObserveDrag(Task<PtzDragOutcome> task, string cameraId, long startedTick)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception?.GetBaseException() is NotSupportedException)
            {
                _send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = cameraId, Message = "drag" });
                return;
            }
            if (t.IsFaulted)
            {
                _log.Warn($"ptz drag cam={cameraId} failed: {HostLogService.Mask(t.Exception?.GetBaseException().Message)}");
                return;
            }
            if (t.IsCanceled) return;
            var r = t.Result;
            long ms = Environment.TickCount64 - startedTick;
            if (r.Sent) _log.Info(FormattableString.Invariant($"ptz drag cam={cameraId} kind={r.Kind} pan={r.Pan:F4} tilt={r.Tilt:F4} in {ms} ms"));
            else _log.Info($"ptz drag cam={cameraId} not sent: {r.Reason}");
        }, TaskScheduler.Default);
    }

    private long NextSequence(string cameraId)
    {
        lock (_gate)
        {
            long next = (_motionSequence.TryGetValue(cameraId, out var s) ? s : 0) + 1;
            _motionSequence[cameraId] = next;
            return next;
        }
    }

    private bool IsLatestMotion(string cameraId, long sequence)
    {
        lock (_gate) { return _motionSequence.TryGetValue(cameraId, out var s) && s == sequence; }
    }

    private static double Clamp(double v) => double.IsFinite(v) ? Math.Clamp(v, -1d, 1d) : 0d;

    private static CancellationToken Timeout(int ms) => new CancellationTokenSource(ms).Token;

    private void Observe(Task task, string cameraId, string what)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception?.GetBaseException() is NotSupportedException)
                _send(new HostError { Code = CameraErrorCodes.NotSupported, Scope = cameraId, Message = what });
            else if (t.IsFaulted)
                _log.Warn($"ptz {what} cam={cameraId} failed: {HostLogService.Mask(t.Exception?.GetBaseException().Message)}");
        }, TaskScheduler.Default);
    }

    // ───────────────────────── 호스트 안 호출(이벤트 창 타일) ─────────────────────────

    /// <summary>PTZ 제공자(없으면 null — 시험 무늬 · 파일).</summary>
    public ICameraPtzProvider? PtzProviderFor(VideoProviderInfo info) => info is null ? null : _registry.GetPtz(info.Kind);

    /// <summary>타일 누름 이동(호스트 UI 에서 직접). 준비 전이면 먼저 준비한다. 던지지 않는다(지원 안 함만 예외).</summary>
    public async Task<bool> MoveAsync(string cameraId, VideoProviderInfo info, double pan, double tilt, double zoom, CancellationToken ct)
    {
        // 순번을 준비 전에 받는다 — 준비(ONVIF 연결 수 초)하는 사이 뗌(정지)이 오면 이 이동은 나가지 않는다.
        // (이전: 뗌이 "준비 안 됨"으로 버려진 뒤 이동이 나가 유지 재전송과 함께 계속 돌았다.)
        long sequence = NextSequence(cameraId);
        var provider = await PreparedAsync(cameraId, info, ct).ConfigureAwait(false);
        if (provider is null) return false;
        if (!IsLatestMotion(cameraId, sequence)) return true;   // 그 사이 정지 · 새 동작이 왔다
        return await provider.ContinuousMoveAsync(cameraId, Clamp(pan), Clamp(tilt), Clamp(zoom), ct).ConfigureAwait(false);
    }

    /// <summary>타일 영상 위 드래그 → 상대 이동 한 번. 준비 전이면 먼저 준비한다. 더 새 동작에 밀렸으면 "superseded".</summary>
    public async Task<PtzDragOutcome> DragMoveAsync(string cameraId, VideoProviderInfo info, double viewX, double viewY, double viewAspect, CancellationToken ct)
    {
        long sequence = NextSequence(cameraId);
        var provider = await PreparedAsync(cameraId, info, ct).ConfigureAwait(false);
        if (provider is null) return PtzDragOutcome.NotSent("not-ready");
        if (!IsLatestMotion(cameraId, sequence)) return PtzDragOutcome.NotSent("superseded");
        var started = Environment.TickCount64;
        var task = provider.DragMoveAsync(cameraId, viewX, viewY, viewAspect, ct);
        ObserveDrag(task, cameraId, started);
        return await task.ConfigureAwait(false);
    }

    /// <summary>타일 뗌 정지.</summary>
    public async Task StopAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        NextSequence(cameraId);   // 준비 중인 이동이 있으면 무효로 만든다(준비가 끝나도 나가지 않는다)
        var provider = PtzProviderFor(Remember(cameraId, info)!);
        if (provider is null || !provider.IsPrepared(cameraId)) return;   // 준비 안 된 카메라는 움직인 적도 없다
        await provider.StopAsync(cameraId, ct).ConfigureAwait(false);
    }

    private async Task<ICameraPtzProvider?> PreparedAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        var known = Remember(cameraId, info);
        var provider = known is null ? null : _registry.GetPtz(known.Kind);
        if (provider is null || known is null) return null;
        if (!provider.IsPrepared(cameraId)) await provider.PrepareAsync(cameraId, known, ct).ConfigureAwait(false);
        return provider.IsPrepared(cameraId) ? provider : null;
    }

    // ───────────────────────── 결과가 필요한 요청 ─────────────────────────

    /// <summary>요청 처리 후 응답을 보낸다(배경). 파이프 줄을 붙잡지 않는다.</summary>
    public void HandleRequest(CameraRequest request)
    {
        if (request is null) return;
        _ = Task.Run(async () =>
        {
            var response = await ExecuteAsync(request).ConfigureAwait(false);
            _send(response);
        });
    }

    /// <summary>요청 하나를 실행해 응답을 만든다(던지지 않는다). 이벤트 창 타일도 이 경로를 쓴다.</summary>
    public async Task<CameraResponse> ExecuteAsync(CameraRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CameraId))
            return CameraResponse.Fail(request, CameraErrorCodes.BadRequest, "empty camera id");
        var info = Remember(request.CameraId, request.Provider);
        var provider = info is null ? null : _registry.GetPtz(info.Kind);
        if (provider is null || info is null)
            return CameraResponse.Fail(request, CameraErrorCodes.NotSupported, $"no ptz provider for {request.Provider?.Kind}");

        int timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : DefaultRequestTimeoutMs;
        using var cts = new CancellationTokenSource(timeoutMs);
        var started = Environment.TickCount64;
        try
        {
            var response = await ExecuteCoreAsync(request, info, provider, cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested && !response.Success)
                response = CameraResponse.Fail(request, CameraErrorCodes.Timeout, $"{timeoutMs} ms");
            _log.Info($"request {response} in {Environment.TickCount64 - started} ms");
            return response;
        }
        catch (NotSupportedException ex)
        {
            return CameraResponse.Fail(request, CameraErrorCodes.NotSupported, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return CameraResponse.Fail(request, CameraErrorCodes.Timeout, $"{timeoutMs} ms");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"request {request} failed: {ex.GetType().Name} {HostLogService.Mask(ex.Message)}");
            return CameraResponse.Fail(request, CameraErrorCodes.Failed, ex.GetType().Name);
        }
    }

    private static async Task<CameraResponse> ExecuteCoreAsync(CameraRequest r, VideoProviderInfo info, ICameraPtzProvider p, CancellationToken ct)
    {
        string cam = r.CameraId;
        if (r.Kind == CameraRequestKind.PreparePtz || !p.IsPrepared(cam))
        {
            var readiness = await p.PrepareAsync(cam, info, ct).ConfigureAwait(false);
            if (r.Kind == CameraRequestKind.PreparePtz)
                return Result(r, readiness.Connected, readiness.Connected ? null : CameraErrorCodes.Failed,
                    ptzCapable: readiness.PtzCapable, imagingCapable: readiness.ImagingCapable);
            if (!readiness.Connected) return CameraResponse.Fail(r, CameraErrorCodes.Failed, "not connected");
        }

        switch (r.Kind)
        {
            case CameraRequestKind.GetPresets:
                var presets = await p.GetPresetsAsync(cam, ct).ConfigureAwait(false);
                return presets is null
                    ? CameraResponse.Fail(r, CameraErrorCodes.Failed, "GetPresets")
                    : new CameraResponse
                    {
                        RequestId = r.RequestId, Kind = r.Kind, CameraId = cam, Success = true,
                        Presets = presets.Select(x => new CameraPreset { Token = x.Token, Name = x.Name }).ToList(),
                    };
            case CameraRequestKind.GotoPreset:
                if (string.IsNullOrWhiteSpace(r.PresetToken)) return CameraResponse.Fail(r, CameraErrorCodes.BadRequest, "no preset token");
                return Result(r, await p.GotoPresetAsync(cam, r.PresetToken!, ct).ConfigureAwait(false));
            case CameraRequestKind.SetPreset:
                if (string.IsNullOrWhiteSpace(r.PresetName)) return CameraResponse.Fail(r, CameraErrorCodes.BadRequest, "no preset name");
                return Result(r, await p.SetPresetAsync(cam, r.PresetName!, ct).ConfigureAwait(false));
            case CameraRequestKind.RemovePreset:
                if (string.IsNullOrWhiteSpace(r.PresetToken)) return CameraResponse.Fail(r, CameraErrorCodes.BadRequest, "no preset token");
                return Result(r, await p.RemovePresetAsync(cam, r.PresetToken!, ct).ConfigureAwait(false));
            case CameraRequestKind.SetHome:
                return Result(r, await p.SetHomeAsync(cam, ct).ConfigureAwait(false));
            case CameraRequestKind.GotoHome:
                return Result(r, await p.GotoHomeAsync(cam, ct).ConfigureAwait(false));
            case CameraRequestKind.GetImaging:
                if (!p.IsImagingCapable(cam)) return CameraResponse.Fail(r, CameraErrorCodes.NotSupported, "imaging");
                var state = await p.GetImagingAsync(cam, ct).ConfigureAwait(false);
                return state is null
                    ? CameraResponse.Fail(r, CameraErrorCodes.Failed, "GetImaging")
                    : new CameraResponse
                    {
                        RequestId = r.RequestId, Kind = r.Kind, CameraId = cam, Success = true, ImagingCapable = true,
                        IrCutFilter = state.IrCutFilter, AutoFocus = state.AutoFocus,
                    };
            case CameraRequestKind.SetIrCutFilter:
                if (string.IsNullOrWhiteSpace(r.IrCutFilter)) return CameraResponse.Fail(r, CameraErrorCodes.BadRequest, "no mode");
                return Result(r, await p.SetIrCutFilterAsync(cam, r.IrCutFilter!, ct).ConfigureAwait(false));
            case CameraRequestKind.SetAutoFocus:
                return Result(r, await p.SetAutoFocusAsync(cam, r.AutoFocus, ct).ConfigureAwait(false));
            default:
                return CameraResponse.Fail(r, CameraErrorCodes.BadRequest, $"unknown kind {r.Kind}");
        }
    }

    private static CameraResponse Result(CameraRequest r, bool ok, string? errorCode = CameraErrorCodes.Failed, bool ptzCapable = false, bool imagingCapable = false) => new()
    {
        RequestId = r.RequestId,
        Kind = r.Kind,
        CameraId = r.CameraId,
        Success = ok,
        ErrorCode = ok ? null : errorCode,
        PtzCapable = ptzCapable,
        ImagingCapable = imagingCapable,
    };

    /// <summary>접속 정보 기억(재시작 직후 정보 없는 명령 대비). 정보가 없으면 기억한 값.</summary>
    private VideoProviderInfo? Remember(string cameraId, VideoProviderInfo? info)
    {
        lock (_gate)
        {
            if (info is not null && info.Kind != VideoProviderKind.TestPattern)
            {
                _endpoints[cameraId] = info;
                return info;
            }
            return _endpoints.TryGetValue(cameraId, out var known) ? known : info;
        }
    }

    private sealed record PendingMotion(PtzCommand? Ptz, PtzFocusCommand? Focus);
}
