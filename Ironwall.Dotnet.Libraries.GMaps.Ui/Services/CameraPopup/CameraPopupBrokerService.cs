using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 모드 더블클릭 → CAMERA_POPUP_OPEN · 설정 → POPUP_LAYOUT_GET
                  (camera-popup-modes T-07 · FR-05~08 · FR-26~28, 브로커 연동설계 v2.0.7 §11.5.9)
                  Subject: "{DomainNats}.{GroupNats}.nvr_manager.popup" — 큐 그룹 없음, 대상은 body target_client_id.
                  REQ/RSP 왕복은 BrokerRequestClient(공통 실행기) 재사용 — CameraAimControlService 와 같은 틀.
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class CameraPopupBrokerService : ICameraPopupBrokerService
{
    public const string OpenCommand = "CAMERA_POPUP_OPEN";
    public const string LayoutGetCommand = "POPUP_LAYOUT_GET";
    public const string SubjectLeaf = "nvr_manager.popup";

    /// <summary>
    /// 전송 계층이 제 시간 제한을 못 지킬 때(연결 재시도에 붙잡힘 등)의 안전 여유. 이 초를 넘기면 우리 쪽에서 끊고 무응답으로 친다(FR-26).
    /// </summary>
    internal static readonly TimeSpan GuardGrace = TimeSpan.FromSeconds(1);

    private readonly IBrokerRequestClient _brokerClient;
    private readonly INatsSetupModel _natsSetup;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly HashSet<int> _pending = new();

    public CameraPopupBrokerService(IBrokerRequestClient brokerClient, INatsSetupModel natsSetupModel, ILogService? log = null)
    {
        _brokerClient = brokerClient ?? throw new ArgumentNullException(nameof(brokerClient));
        _natsSetup = natsSetupModel ?? throw new ArgumentNullException(nameof(natsSetupModel));
        _log = log;
    }

    public string BuildSubject()
    {
        var domain = _natsSetup.DomainNats;
        var group = _natsSetup.GroupNats;
        return string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(group)
            ? string.Empty
            : $"{domain}.{group}.{SubjectLeaf}";
    }

    public bool IsPending(int cameraId)
    {
        lock (_gate) return _pending.Contains(cameraId);
    }

    public async Task<CameraPopupBrokerOutcome> RequestOpenAsync(CameraPopupOpenRequest request, Action<CameraPopupBrokerNotice>? notify = null,
                                                                 CancellationToken ct = default)
    {
        // ① 보내기 전 검사 — 걸리면 보내지 않고 이유만 알린다.
        CameraPopupBrokerOutcome? invalid = null;
        string subject = string.Empty;
        try
        {
            if (request is null) invalid = Invalid(CameraPopupBrokerToasts.InvalidCamera);
            else if (request.CameraId <= 0) invalid = Invalid(CameraPopupBrokerToasts.InvalidCamera);
            else if (string.IsNullOrWhiteSpace(request.TargetClientId)) invalid = Invalid(CameraPopupBrokerToasts.NoClientId);
            else if ((subject = BuildSubject()).Length == 0) invalid = Invalid(CameraPopupBrokerToasts.NoSubject);
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraPopupBroker] 요청 준비 실패: {ex.Message}");
            invalid = new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Failed, CameraPopupBrokerToasts.Failed);
        }
        if (invalid is not null)
        {
            _log?.Warning($"[CameraPopupBroker] {OpenCommand} 보내지 않음 — {invalid.Toast} (camera={request?.CameraId})");
            Notify(notify, invalid.Toast, pending: false);
            return invalid;
        }

        var req = request!;

        // ② 연타 합치기 — 같은 카메라가 응답 전이면 1건으로 묶는다(FR-06). 대기 토스트가 이미 떠 있으므로 다시 알리지 않는다.
        lock (_gate)
        {
            if (!_pending.Add(req.CameraId))
            {
                _log?.Info($"[CameraPopupBroker] {OpenCommand} 합침 — camera={req.CameraId} 응답 대기 중");
                return new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Coalesced, string.Empty);
            }
        }

        try
        {
            Notify(notify, CameraPopupBrokerToasts.Requested(req.CameraName), pending: true);

            var body = req.ToBody();
            _log?.Info($"[CameraPopupBroker] {OpenCommand} → {subject} camera={body.CameraId} target={body.TargetClientId} " +
                       $"monitor={body.Monitor?.ToString() ?? "-"} cell={body.Cell?.ToString() ?? "자동"} on_occupied={body.OnOccupied}");

            var result = await SendAsync(subject, OpenCommand, body, req.TimeoutSeconds, ct).ConfigureAwait(false);
            var outcome = MapOpen(result, req, ct);

            if (outcome.Kind == CameraPopupBrokerOutcomeKind.Opened)
                _log?.Info($"[CameraPopupBroker] {OpenCommand} 성공 — camera={req.CameraId} popup_id={outcome.PopupId ?? "(없음)"}");
            else
                _log?.Warning($"[CameraPopupBroker] {OpenCommand} {outcome.Kind} — camera={req.CameraId}: {result.UserMessage}");

            if (outcome.Toast.Length > 0) Notify(notify, outcome.Toast, pending: false);
            return outcome;
        }
        catch (Exception ex)
        {
            // 예외 경계(FR-27) — 로그 + 안내만. GIS 는 계속 돈다.
            _log?.Error($"[CameraPopupBroker] {OpenCommand} 처리 실패 — camera={req.CameraId}: {ex.Message}");
            var failed = new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Failed, CameraPopupBrokerToasts.Failed);
            Notify(notify, failed.Toast, pending: false);
            return failed;
        }
        finally
        {
            lock (_gate) _pending.Remove(req.CameraId);
        }
    }

    public async Task<NvrPopupLayoutResult> GetLayoutAsync(string clientId, int timeoutSeconds, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return NvrPopupLayoutResult.Fail("관제석 식별자가 없어 모니터 목록을 가져올 수 없습니다");
            var subject = BuildSubject();
            if (subject.Length == 0)
                return NvrPopupLayoutResult.Fail("NATS 부대 설정(도메인 · 부대)이 비어 모니터 목록을 가져올 수 없습니다");

            var seconds = ClampSeconds(timeoutSeconds);
            var result = await SendAsync(subject, LayoutGetCommand, new PopupLayoutGetBodyDto { TargetClientId = clientId.Trim() },
                                         seconds, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                var message = result.Reason switch
                {
                    EnumBrokerFailure.Rejected => $"NVR Manager 가 거부했습니다 — {result.UserMessage}",
                    EnumBrokerFailure.ParseError => CameraPopupBrokerToasts.ParseError,
                    EnumBrokerFailure.Cancelled when ct.IsCancellationRequested => "취소했습니다",
                    _ => CameraPopupBrokerToasts.NoResponse(seconds),
                };
                _log?.Warning($"[CameraPopupBroker] {LayoutGetCommand} 실패({result.Reason}) — {message}");
                return NvrPopupLayoutResult.Fail(message);
            }

            var dto = ParseBody<PopupLayoutGetResultDto>(result.RawReply);
            var monitors = (dto?.Monitors ?? new List<PopupLayoutMonitorDto>())
                .Where(m => m is not null && m.Index >= 1)
                .GroupBy(m => m.Index)
                .Select(g => g.First())
                .OrderBy(m => m.Index)
                .Select(m => new NvrPopupMonitor(m.Index, m.IsPrimary, m.Width, m.Height))
                .ToList();
            if (monitors.Count == 0)
            {
                _log?.Warning($"[CameraPopupBroker] {LayoutGetCommand} 응답에 모니터 없음 — raw={Truncate(result.RawReply)}");
                return NvrPopupLayoutResult.Fail("NVR Manager 응답에 모니터가 없습니다");
            }

            _log?.Info($"[CameraPopupBroker] {LayoutGetCommand} 성공 — 모니터 {monitors.Count}대");
            return NvrPopupLayoutResult.Ok(monitors, dto?.DefaultSlot?.Monitor, dto?.DefaultSlot?.Cell);
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraPopupBroker] {LayoutGetCommand} 처리 실패: {ex.Message}");
            return NvrPopupLayoutResult.Fail("모니터 목록을 가져오지 못했습니다 — 로그를 확인하세요");
        }
    }

    #region - 내부 -
    private static int ClampSeconds(int seconds)
        => Math.Clamp(seconds, CameraPopupSettings.MinBrokerTimeoutSeconds, CameraPopupSettings.MaxBrokerTimeoutSeconds);

    /// <summary>
    /// 한 번 보내고 RSP 를 기다린다. 시간 제한은 두 겹이다 — 전송 계층에 설정 초를 넘기고(정상 경로),
    /// 전송 계층이 그 약속을 못 지켜도 설정 초 + 여유에서 우리가 끊는다(FR-26). 우리 쪽 끊김은 무응답으로 친다.
    /// </summary>
    private async Task<BrokerRequestResult> SendAsync<TBody>(string subject, string command, TBody body, int timeoutSeconds,
                                                             CancellationToken ct) where TBody : class
    {
        var timeout = TimeSpan.FromSeconds(ClampSeconds(timeoutSeconds));
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(ct);
        guard.CancelAfter(timeout + GuardGrace);
        try
        {
            return await _brokerClient.RequestAsync(subject, command, body, timeout, guard.Token)
                                      .WaitAsync(guard.Token)
                                      .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log?.Warning($"[CameraPopupBroker] {command} 전송 계층이 {timeout.TotalSeconds:F0}초 안에 돌아오지 않음 — 무응답 처리");
            return BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "응답 없음");
        }
    }

    private static CameraPopupBrokerOutcome MapOpen(BrokerRequestResult result, CameraPopupOpenRequest request, CancellationToken ct)
    {
        if (result.Success)
        {
            var dto = ParseBody<CameraPopupOpenResultDto>(result.RawReply);
            var monitor = dto?.Monitor ?? (request.Monitor >= 1 ? request.Monitor : (int?)null);
            var cell = dto?.Cell ?? (request.Cell >= 1 ? request.Cell : (int?)null);
            var popupId = string.IsNullOrWhiteSpace(dto?.PopupId) ? null : dto!.PopupId;
            return new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Opened, CameraPopupBrokerToasts.Opened(monitor, cell), popupId);
        }

        return result.Reason switch
        {
            EnumBrokerFailure.Rejected => new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Rejected,
                                                                       CameraPopupBrokerToasts.Rejected(result.UserMessage)),
            EnumBrokerFailure.ParseError => new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.ParseError, CameraPopupBrokerToasts.ParseError),
            EnumBrokerFailure.Invalid => new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Invalid, CameraPopupBrokerToasts.Failed),
            // 호출 쪽 취소는 알리지 않는다. 우리 시간 제한(guard)이 끊은 것은 무응답이다.
            EnumBrokerFailure.Cancelled when ct.IsCancellationRequested
                => new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Cancelled, string.Empty),
            _ => new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.NoResponse,
                                              CameraPopupBrokerToasts.NoResponse(ClampSeconds(request.TimeoutSeconds))),
        };
    }

    /// <summary>RSP 봉투의 <c>body</c> 를 읽는다(문자열로 감싸 온 JSON 도). 못 읽으면 null.</summary>
    private static T? ParseBody<T>(string? raw) where T : class
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return BrokerMessageHelper.ParseSingleEventFromBrokerMessage<T>(raw); }
        catch (Exception) { return null; }   // 관용 파싱 — body 모양이 달라도 성공 결과는 살린다
    }

    private static CameraPopupBrokerOutcome Invalid(string toast) => new(CameraPopupBrokerOutcomeKind.Invalid, toast);

    private void Notify(Action<CameraPopupBrokerNotice>? notify, string text, bool pending)
    {
        if (notify is null || string.IsNullOrEmpty(text)) return;
        try { notify(new CameraPopupBrokerNotice(text, pending)); }
        catch (Exception ex) { _log?.Warning($"[CameraPopupBroker] 토스트 알림 실패(무시): {ex.Message}"); }
    }

    private static string Truncate(string? s) => s is null ? "(null)" : s.Length <= 200 ? s : s[..200] + "…";
    #endregion
}
