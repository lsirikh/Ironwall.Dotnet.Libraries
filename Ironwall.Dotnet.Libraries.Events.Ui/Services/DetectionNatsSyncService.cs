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
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using System;
using System.Windows;
using System.Windows.Threading;
using Ironwall.Dotnet.Monitoring.Models.Helpers;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS DETECTION 메시지를 구독하여 심볼 탐지 비주얼(Detecting) 처리
   Created By   : GHLee
   Created On   : 2026-03-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionNatsSyncService : IDetectionNatsSyncService, IService
{
    #region - Ctors -
    public DetectionNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager,
        IEventQueueManager eventQueueManager,
        IEventSetupModel eventSetupModel,
        IEventAggregator? eventAggregator = null,
        ITokenStorageService? tokenStorage = null,
        Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider? deviceProvider = null,
        IDoorContactPolicy? doorContactPolicy = null)
    {
        _doorContactPolicy = doorContactPolicy;   // FR-13 ③: 미주입 시 기본 true(DefaultDoorContactPolicy 와 동일)
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
        _eventQueueManager = eventQueueManager;
        _eventSetupModel = eventSetupModel;
        _eventAggregator = eventAggregator;
        _tokenStorage = tokenStorage;
        _deviceProvider = deviceProvider;   // 소속 제어기 해석용(Controller_Fault_AutoRecovery_Extension). 미주입 시 자동복구 태깅 생략.
    }
    #endregion

    #region - IService -
    // IService.ExecuteAsync — OnStartup→Start() 가 OrderBy(Metadata["Order"]) 정렬 후 호출. StartService 위임.
    // (EB1) IService 등록으로 OnExit 가 동일 정렬 후 StopAsync 를 호출 → NATS 구독 해제. Order 메타데이터는
    //       BaseStart 가 아니라 Start()/OnExit() 의 OrderBy 때문에 필수(누락 시 KeyNotFoundException).
    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        // 멱등: 빌드콜백/ExecuteAsync 중복 호출돼도 단일 구독 유지 (이중 구독 방지)
        _natsService.NatsSubscribeEventAsync -= OnNatsDetectionAsync;
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
        // 로그인 게이팅(Login_Gated_GIS_Init): 로그인 전 NATS 이벤트 수신 차단 — 맵/캐시 미구축 상태에서 알람 방지.
        // 미조치 이벤트는 서버 DB에 영속 → 로그인 후 이력 패널 조회로 후처리(EventProviderService).
        // _tokenStorage 미주입(null) 시 게이트 비활성(하위호환). 물리 NATS 구독은 유지하고 앱 레이어에서 드롭.
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;
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

            // FR-13 ③ 접점 폴백: 통문/함체의 ContactOn/Off 는 '개폐 형태' 신호다 — 카드·탐지음·자동조치보고 큐에 넣지 않고
            //   심볼 DoorState 만 유도한다(설정 DoorContactFallback=false 면 형태도 바꾸지 않고 버린다). 접점 센서(Contact) 는 종전대로 큐잉.
            //   서버 SYNC_DEVICE/OPERATION_EVENT 가 구현되면 그 채널이 권위이며 이 폴백은 같은 상태로 수렴한다(R-02).
            if (DoorStateMachine.IsContactEvent(eventType) && DoorStateMachine.HasDoor(deviceType))
            {
                bool fallback = _doorContactPolicy?.FallbackEnabled ?? true;
                if (fallback) _symbolEventManager.ApplyDoorEvent(deviceId, deviceType, eventType);
                _log?.Info($"DETECTION 접점→개폐 형태(큐 제외): deviceId={deviceId}, {deviceType}, {eventType}, fallback={fallback}");
                return Task.CompletedTask;
            }

            // 탐지 센서의 소속 제어기 Id 해석 — 제어기 고장 자동복구 매칭용(Controller_Fault_AutoRecovery_Extension FR-02).
            //   provider 미주입/센서 미발견/Controller.Id<=0이면 null → 자동복구 트리거 스킵(안전실패).
            int? owningControllerId = null;
            var sensor = _deviceProvider?
                .OfType<Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel>()
                .FirstOrDefault(s => s.Id == deviceId);
            if (sensor?.Controller?.Id is int ctrlId && ctrlId > 0) owningControllerId = ctrlId;

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
                OwningControllerId = owningControllerId,   // 제어기 고장 자동복구 매칭(FR-02)
                TimeoutSeconds = _eventSetupModel.TimeDiscardSec,
                IsAutoReportEnabled = _eventSetupModel.IsAutoEventDiscard
            }, natsMessageId); // NATS UUID를 entryId로 사용

            _log?.Info($"DETECTION Enqueue 완료: entryId={entryId}, eventId={eventId}");

            // entryId + eventId를 EventAggregator로 발행 → 카드 1:1 매칭에 사용
            // Background(4) 우선순위: PublishOnUIThreadAsync(Normal=9)가 Input(5) 기아 유발 → Background로 하강
            Application.Current?.Dispatcher.InvokeAsync(
                () => _eventAggregator!.PublishOnCurrentThreadAsync(
                    new EventEntryEnqueuedMessage(entryId, eventId, deviceId, deviceType, eventType)),
                DispatcherPriority.Background);
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
    private readonly IDoorContactPolicy? _doorContactPolicy;
    private readonly IEventSetupModel _eventSetupModel;
    private readonly IEventAggregator? _eventAggregator;
    private readonly ITokenStorageService? _tokenStorage;   // 로그인 게이팅 — IsAuthenticated 단일 소스
    private readonly Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider? _deviceProvider;   // 소속 제어기 해석용(Controller_Fault_AutoRecovery_Extension)
    #endregion
}
