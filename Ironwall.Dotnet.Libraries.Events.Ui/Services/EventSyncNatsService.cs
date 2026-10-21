using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 이벤트 수정 · 삭제 알림 자가 필터 수신(G45 · G46, 브로커 v2.0.12 §9.21 · §9.22 · §9.14).
                  · SYNC_MALFUNCTION   DELETED → 장애 카드 · 큐 닫기 / UPDATED → 큐에 있는 장애만 다시 읽어 조치 · 재분류 반영(404=삭제)
                  · SYNC_ACTION_REPORT 조치를 다시 읽고 원본의 조치 상태를 다시 읽어 알린다(열린 조치 이력 · 원본 카드)
                  · SYNC_OPERATION_EVENT DELETED → 운영 카드 · 레일 행 닫기 / UPDATED → 카드가 있을 때만 다시 읽어 심각도 갱신(404=삭제)
                  새 이벤트로 취급하지 않는다 — 큐에 넣지 않고 팝업 연동도 돌리지 않는다. 봉투 id 로 중복을 거른다.
                  수신 예외는 모두 여기서 막는다(GIS 본체 보호). SYNC_DETECTION 은 DetectionSyncNatsService 가 맡는다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventSyncNatsService : IEventSyncNatsService, IService
{
    public const string CMD_MALFUNCTION = "SYNC_MALFUNCTION";
    public const string CMD_ACTION_REPORT = "SYNC_ACTION_REPORT";
    public const string CMD_OPERATION = "SYNC_OPERATION_EVENT";

    #region - Ctors -
    /// <param name="hasOperationCard">운영 카드가 목록에 있는가 — 없으면 UPDATED 를 다시 읽지 않는다. <c>null</c> 이면 늘 다시 읽는다.</param>
    public EventSyncNatsService(
        ILogService? log,
        INatsService natsService,
        IEventQueueManager eventQueueManager,
        IEventApiService apiService,
        IEventAggregator? eventAggregator = null,
        ITokenStorageService? tokenStorage = null,
        Func<int, bool>? hasOperationCard = null,
        DeviceProvider? deviceProvider = null)
    {
        _log = log;
        _natsService = natsService;
        _eventQueueManager = eventQueueManager;
        _apiService = apiService;
        _eventAggregator = eventAggregator;
        _tokenStorage = tokenStorage;
        _hasOperationCard = hasOperationCard;
        _deviceProvider = deviceProvider;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;   // 멱등(이중 구독 방지)
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(EventSyncNatsService)} started — SYNC_MALFUNCTION · SYNC_ACTION_REPORT · SYNC_OPERATION_EVENT 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _log?.Info($"{nameof(EventSyncNatsService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsAsync(MessageArgsModel e)
    {
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;   // 로그인 게이팅
        foreach (var envelope in NatsEnvelopeItems.Parse(e.Data, _log, "EVENT_SYNC"))
            ProcessEnvelope(envelope);
        return Task.CompletedTask;
    }

    private void ProcessEnvelope(JObject envelope)
    {
        try
        {
            var cmd = envelope.Value<string>("cmd");
            if (cmd is not (CMD_MALFUNCTION or CMD_ACTION_REPORT or CMD_OPERATION)) return;
            if (!NatsEnvelopeItems.IsNotice(envelope, cmd)) return;
            if (envelope["body"] is not JObject body) return;
            if (!_recent.TryAdd(envelope.Value<string>("id")))
            {
                _log?.Info($"{cmd}: 같은 봉투가 다시 와 건너뜀(id={envelope.Value<string>("id")})");
                return;
            }

            var action = body.Value<string>("action");
            var resourceId = body.Value<int?>("resource_id") ?? 0;
            var deleted = string.Equals(action, "DELETED", StringComparison.OrdinalIgnoreCase);
            if (!deleted && !string.Equals(action, "UPDATED", StringComparison.OrdinalIgnoreCase))
            {
                _log?.Info($"{cmd} 무시(action={action}, resource_id={resourceId}) — UPDATED · DELETED 만 처리");
                return;
            }
            if (resourceId <= 0)
            {
                _log?.Warning($"{cmd}: resource_id 없음/유효하지 않음 — 무시");
                return;
            }

            // 다시 읽기는 수신 펌프를 막지 않도록 떼어 낸다(DetectionSyncNatsService 와 같은 방식). 예외는 안에서 막는다.
            _lastProcessingTask = cmd switch
            {
                CMD_MALFUNCTION => HandleMalfunctionAsync(resourceId, deleted),
                CMD_ACTION_REPORT => HandleActionReportAsync(resourceId, deleted),
                _ => HandleOperationAsync(resourceId, deleted),
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(EventSyncNatsService)} 처리 오류: {ex.Message}");
        }
    }

    private async Task HandleMalfunctionAsync(int id, bool deleted)
    {
        try
        {
            if (deleted)
            {
                _log?.Info($"SYNC_MALFUNCTION DELETED: id={id} — 장애 카드 닫기 요청");
                await PublishOnUiAsync(new MalfunctionDeletedOnServerMessage(id)).ConfigureAwait(false);
                return;
            }
            // 큐에 있는 장애만 — 탐지 번호와 겹쳐도 종류(Fault)로 찾는다.
            if (_eventQueueManager.FindEntryByEventId(id, EnumEventType.Fault) is null)
            {
                _log?.Info($"SYNC_MALFUNCTION UPDATED: id={id} 큐에 없음 — 다시 읽지 않음");
                return;
            }
            var res = await _apiService.GetMalfunctionEventByIdAsync(id).ConfigureAwait(false);
            if (res?.StatusCode == 404)
            {
                _log?.Info($"SYNC_MALFUNCTION UPDATED: id={id} 재조회 404 — 지워진 것으로 본다");
                await PublishOnUiAsync(new MalfunctionDeletedOnServerMessage(id)).ConfigureAwait(false);
                return;
            }
            if (res is not { Success: true, Data: { } dto })
            {
                _log?.Info($"SYNC_MALFUNCTION 재조회 실패: id={id}, status={res?.StatusCode}");
                return;
            }
            await PublishOnUiAsync(new MalfunctionSyncedMessage(id, IsTrue(dto.ActionReported), dto.TypeEvent)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"SYNC_MALFUNCTION 처리 오류(id={id}): {ex.Message}");
        }
    }

    private async Task HandleActionReportAsync(int actionId, bool deleted)
    {
        try
        {
            if (deleted)
            {
                await PublishOnUiAsync(new ActionReportSyncedMessage(actionId, true, null, null, null)).ConfigureAwait(false);
                return;
            }
            var res = await _apiService.GetActionEventByIdAsync(actionId).ConfigureAwait(false);
            var origin = res is { Success: true } ? res.Data?.FromEvent : null;
            var kind = OriginKind(origin);
            if (kind is null || origin is not { Id: > 0 })
            {
                // 원본을 모른다(지워져 from_event 가 null · 재조회 실패) — 열린 조치 이력만 다시 읽게 알린다.
                _log?.Info($"SYNC_ACTION_REPORT UPDATED: action={actionId}, 원본 모름(status={res?.StatusCode})");
                await PublishOnUiAsync(new ActionReportSyncedMessage(actionId, false, null, null, null)).ConfigureAwait(false);
                return;
            }
            bool? reported = await ReadOriginActionReportedAsync(kind, origin.Id).ConfigureAwait(false);
            _log?.Info($"SYNC_ACTION_REPORT UPDATED: action={actionId} → 원본 {kind} {origin.Id}, action_reported={reported?.ToString() ?? "모름"}");
            await PublishOnUiAsync(new ActionReportSyncedMessage(actionId, false, kind, origin.Id, reported)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"SYNC_ACTION_REPORT 처리 오류(action={actionId}): {ex.Message}");
        }
    }

    private async Task HandleOperationAsync(int id, bool deleted)
    {
        try
        {
            if (deleted)
            {
                _log?.Info($"SYNC_OPERATION_EVENT DELETED: id={id} — 운영 카드 · 레일 행 닫기 요청");
                await PublishOnUiAsync(new OperationEventDeletedOnServerMessage(id)).ConfigureAwait(false);
                return;
            }
            if (_hasOperationCard is { } has && !has(id))
            {
                _log?.Info($"SYNC_OPERATION_EVENT UPDATED: id={id} 카드 없음 — 다시 읽지 않음");
                return;
            }
            var res = await _apiService.GetOperationEventByIdAsync(id).ConfigureAwait(false);
            if (res?.StatusCode == 404)
            {
                await PublishOnUiAsync(new OperationEventDeletedOnServerMessage(id)).ConfigureAwait(false);
                return;
            }
            if (res is not { Success: true, Data: { } dto })
            {
                _log?.Info($"SYNC_OPERATION_EVENT 재조회 실패: id={id}, status={res?.StatusCode}");
                return;
            }
            await PublishOnUiAsync(new OperationEventSyncedMessage(dto.ToOperationEventModel(_deviceProvider))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"SYNC_OPERATION_EVENT 처리 오류(id={id}): {ex.Message}");
        }
    }

    /// <summary>원본 종류 — 조치 응답의 <c>from_event</c> 모양으로 가른다. 조치 대상이 아니거나 모르면 <c>null</c>.</summary>
    internal static string? OriginKind(IEventDto? origin) => origin switch
    {
        DetectionEventDto => ActionReportKind.Detection,
        MalfunctionEventDto => ActionReportKind.Malfunction,
        OperationEventDto => EventCardKind.Operation,
        _ => null,
    };

    private async Task<bool?> ReadOriginActionReportedAsync(string kind, int id)
    {
        IActionReportableEventDto? data = kind switch
        {
            ActionReportKind.Detection => (await _apiService.GetDetectionEventByIdAsync(id).ConfigureAwait(false)) is { Success: true } d ? d.Data : null,
            ActionReportKind.Malfunction => (await _apiService.GetMalfunctionEventByIdAsync(id).ConfigureAwait(false)) is { Success: true } m ? m.Data : null,
            _ => (await _apiService.GetOperationEventByIdAsync(id).ConfigureAwait(false)) is { Success: true } o ? o.Data as IActionReportableEventDto : null,
        };
        return data is null ? null : IsTrue(data.ActionReported);
    }

    private static bool IsTrue(string? value) => string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>UI 스레드(Background)에서 발행 — 헤드리스면 현재 스레드. 예외는 막는다.</summary>
    private async Task PublishOnUiAsync(object message)
    {
        try
        {
            var ea = _eventAggregator;
            if (ea == null) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                await ea.PublishOnCurrentThreadAsync(message).ConfigureAwait(false);
                return;
            }
            await dispatcher.InvokeAsync(() => ea.PublishOnCurrentThreadAsync(message), DispatcherPriority.Background).Task.Unwrap();
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(EventSyncNatsService)} 발행 오류({message.GetType().Name}): {ex.Message}");
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IEventQueueManager _eventQueueManager;
    private readonly IEventApiService _apiService;
    private readonly IEventAggregator? _eventAggregator;
    private readonly ITokenStorageService? _tokenStorage;
    private readonly Func<int, bool>? _hasOperationCard;
    private readonly DeviceProvider? _deviceProvider;
    private readonly RecentEnvelopeFilter _recent = new();

    // 시험 동기화 시드(같은 어셈블리): 마지막 다시 읽기 태스크.
    private Task? _lastProcessingTask;
    internal Task? LastProcessingTask => _lastProcessingTask;
    #endregion
}

/// <summary>SYNC_MALFUNCTION · SYNC_ACTION_REPORT · SYNC_OPERATION_EVENT 자가 필터 수신(G45 · G46).</summary>
public interface IEventSyncNatsService
{
    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
