using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json.Linq;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS MALFUNCTION 메시지를 구독하여 EventQueueManager에 Enqueue — EntryId 부여
   Created By   : GHLee
   Created On   : 2026-05-19
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MalfunctionNatsSyncService : IMalfunctionNatsSyncService
{
    #region - Ctors -
    public MalfunctionNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager,
        IEventQueueManager eventQueueManager,
        IEventSetupModel eventSetupModel,
        IEventAggregator? eventAggregator = null)
    {
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
        _eventQueueManager = eventQueueManager;
        _eventSetupModel = eventSetupModel;
        _eventAggregator = eventAggregator;
    }
    #endregion

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync += OnNatsMalfunctionAsync;
        _log?.Info($"{nameof(MalfunctionNatsSyncService)} started — MALFUNCTION 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsMalfunctionAsync;
        _log?.Info($"{nameof(MalfunctionNatsSyncService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsMalfunctionAsync(MessageArgsModel e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return Task.CompletedTask;

            var jObj = JObject.Parse(e.Data);
            var natsMessageId = jObj.Value<string>("id");
            var cmd = jObj.Value<string>("cmd");
            if (cmd != "MALFUNCTION") return Task.CompletedTask;

            var body = jObj["body"]?.ToObject<MalfunctionEventDto>();
            if (body == null) return Task.CompletedTask;

            var deviceId = body.Device?.Id ?? body.DeviceId;
            var eventId = body.Id;

            // DeviceType을 NATS 메시지의 실제 타입 문자열로 동적 결정 (Fence 하드코딩 금지)
            var deviceTypeStr = body.Device?.TypeDevice ?? string.Empty;
            if (!Enum.TryParse<EnumDeviceType>(deviceTypeStr, ignoreCase: true, out var deviceType))
            {
                _log?.Error($"MALFUNCTION: DeviceType 파싱 실패 '{deviceTypeStr}' (deviceId={deviceId}) — 이벤트 무시");
                return Task.CompletedTask;
            }

            List<int>? deviceGroups = body.Device?.DeviceGroups?
                .Where(g => g.Id > 0)
                .Select(g => g.Id)
                .ToList();

            _log?.Info($"MALFUNCTION 수신: deviceId={deviceId}, deviceType={deviceType}, groups=[{string.Join(",", deviceGroups ?? [])}]");

            var entryId = _eventQueueManager.Enqueue(new EventEntry
            {
                DeviceId = deviceId,
                DeviceType = deviceType,
                GroupIds = deviceGroups,
                EventType = EnumEventType.Fault,
                EventId = eventId,
                TimeoutSeconds = _eventSetupModel.TimeDiscardSec,
                IsAutoReportEnabled = _eventSetupModel.IsAutoEventDiscard
            }, natsMessageId);

            _log?.Info($"MALFUNCTION Enqueue 완료: entryId={entryId}, eventId={eventId}");

            // Background(4) 우선순위: PublishOnUIThreadAsync(Normal=9)가 Input(5) 기아 유발 → Background로 하강
            Application.Current?.Dispatcher.InvokeAsync(
                () => _eventAggregator!.PublishOnCurrentThreadAsync(
                    new EventEntryEnqueuedMessage(entryId, eventId, deviceId, deviceType, EnumEventType.Fault)),
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsMalfunctionAsync 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ISymbolEventManager _symbolEventManager;
    private readonly IEventQueueManager _eventQueueManager;
    private readonly IEventSetupModel _eventSetupModel;
    private readonly IEventAggregator? _eventAggregator;
    #endregion
}
