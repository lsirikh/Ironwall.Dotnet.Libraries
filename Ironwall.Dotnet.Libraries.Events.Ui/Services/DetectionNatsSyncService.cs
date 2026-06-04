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

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS DETECTION 메시지를 구독하여 심볼 탐지 비주얼(Detecting) 처리
   Created By   : GHLee
   Created On   : 2026-03-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionNatsSyncService : IDetectionNatsSyncService
{
    #region - Ctors -
    public DetectionNatsSyncService(
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
        _natsService.NatsSubscribeEventAsync += OnNatsDetectionAsync;
        _log?.Info($"{nameof(DetectionNatsSyncService)} started — DETECTION 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsDetectionAsync;
        _log?.Info($"{nameof(DetectionNatsSyncService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private Task OnNatsDetectionAsync(MessageArgsModel e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return Task.CompletedTask;

            var jObj = JObject.Parse(e.Data);
            var natsMessageId = jObj.Value<string>("id"); // NATS 메시지 고유 UUID
            var cmd = jObj.Value<string>("cmd");
            if (cmd != "DETECT") return Task.CompletedTask;

            var body = jObj["body"]?.ToObject<DetectionEventDto>();
            if (body == null) return Task.CompletedTask;

            // NATS JSON: body.device.id에 실제 ID가 있음 (body.device_id는 0일 수 있음)
            var deviceId = body.Device?.Id ?? body.DeviceId;
            var eventId = body.Id; // 서버 이벤트 ID (1:1 카드 매칭 키)
            var typeEvent = body.TypeEvent;

            // type_event → EnumEventType 매핑
            if (!Enum.TryParse<EnumEventType>(typeEvent, ignoreCase: true, out var eventType))
            {
                _log?.Warning($"DETECTION: 알 수 없는 type_event '{typeEvent}'");
                return Task.CompletedTask;
            }

            // device.device_groups → List<int> 변환
            List<int>? deviceGroups = body.Device?.DeviceGroups?
                .Where(g => g.Id > 0)
                .Select(g => g.Id)
                .ToList();

            // 실제 DeviceType을 NATS 메시지 body에서 파싱 (하드코딩 금지)
            var deviceTypeStr = body.Device?.TypeDevice ?? string.Empty;
            if (!Enum.TryParse<EnumDeviceType>(deviceTypeStr, ignoreCase: true, out var deviceType))
            {
                _log?.Error($"DETECTION: DeviceType 파싱 실패 '{deviceTypeStr}' (deviceId={deviceId}) — 이벤트 무시");
                return Task.CompletedTask;
            }

            _log?.Info($"DETECTION 수신: deviceId={deviceId}, deviceType={deviceType}, event={eventType}, groups=[{string.Join(",", deviceGroups ?? [])}]");

            // EventQueue에 이벤트 등록
            // 심볼 Detecting은 EventQueueManager 전이 이벤트로 일원화:
            //   - 개별 심볼: OnDeviceFirstEvent → SetDeviceDetecting()
            //   - 그룹 심볼: OnGroupFirstEvent → SetGroupDetecting()
            var entryId = _eventQueueManager.Enqueue(new EventEntry
            {
                DeviceId = deviceId,
                DeviceType = deviceType,
                GroupIds = deviceGroups,
                EventType = eventType,
                EventId = eventId,
                TimeoutSeconds = _eventSetupModel.TimeDiscardSec,
                IsAutoReportEnabled = _eventSetupModel.IsAutoEventDiscard
            }, natsMessageId); // NATS UUID를 entryId로 사용

            _log?.Info($"DETECTION Enqueue 완료: entryId={entryId}, eventId={eventId}");

            // entryId + eventId를 EventAggregator로 발행 → 카드 1:1 매칭에 사용
            _eventAggregator?.PublishOnUIThreadAsync(
                new EventEntryEnqueuedMessage(entryId, eventId, deviceId, deviceType, eventType));
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsDetectionAsync 오류: {ex.Message}");
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
