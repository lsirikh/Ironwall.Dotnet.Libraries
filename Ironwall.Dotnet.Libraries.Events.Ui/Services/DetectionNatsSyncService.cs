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
    private async Task OnNatsDetectionAsync(MessageArgsModel e)
    {
        // 로그인 게이팅(Login_Gated_GIS_Init): 로그인 전 NATS 이벤트 수신 차단 — 맵/캐시 미구축 상태에서 알람 방지.
        // 미조치 이벤트는 서버 DB에 영속 → 로그인 후 이력 패널 조회로 후처리(EventProviderService).
        // _tokenStorage 미주입(null) 시 게이트 비활성(하위호환). 물리 NATS 구독은 유지하고 앱 레이어에서 드롭.
        if (_tokenStorage is { IsAuthenticated: false }) return;

        // 배열 봉투는 항목마다 — 호스트 라우터와 같은 의미(WP-1 ⑰). 한 항목의 실패가 나머지를 버리지 않는다.
        foreach (var envelope in NatsEnvelopeItems.Parse(e.Data, _log, "DETECTION"))
            await ProcessEnvelopeAsync(envelope);
    }

    private Task ProcessEnvelopeAsync(JObject jObj)
    {
        try
        {
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

            // 장비가 지워졌으면 device: null(브로커 §6.1) — 깜빡일 심볼도 큐 키도 없다. 카드는 호스트가 스냅샷으로 띄운다.
            if (body.Device is null && deviceId <= 0)
            {
                _log?.Info($"DETECTION: 장비 없음(삭제된 장비) — 심볼 · 큐 건너뜀 (eventId={eventId})");
                return Task.CompletedTask;
            }

            // 종류 · 그룹 — v7.0+ 는 장비 참조 {id, category_device} 뿐이라 캐시에서 읽는다(옛 전문은 본문이 이긴다).
            if (!NatsEventDeviceResolver.TryResolve(body.Device, deviceId, _deviceProvider, out var deviceType, out var deviceGroups))
            {
                // 캐시 미스 + 카테고리만으로 종류를 못 정함(sensor = Fence · PIR · Multi …) — 종류를 <b>추측하지 않고</b> NONE 으로 큐에 넣는다(WP-1 ⑱).
                //   호스트는 이 이벤트의 카드를 띄운다 — 큐 엔트리가 없으면 자동 조치보고 · 원격 해제 · 알람이 그 카드와 따로 논다(종전: 이벤트 무시).
                //   심볼은 칠하지 않는다 — NONE 은 Id 폴백을 쓰지 않는다(WP-8 H2: 같은 Id 의 카메라 · 다른 종류 심볼을 칠하고,
                //   그 조치가 그 심볼의 진짜 장애색을 지웠다). 캐시에 없는 장비라 원래 그릴 심볼도 없다.
                deviceType = EnumDeviceType.NONE;
                deviceGroups = body.Device?.GroupIds?.Where(g => g > 0).ToList();
                _log?.Warning($"DETECTION: 장비 종류를 정하지 못함 (deviceId={deviceId}, category_device='{body.Device?.CategoryDevice}', type_device='{body.Device?.TypeDevice}', 캐시 미스) — 종류 NONE 으로 큐 적재(자동조치 · 원격 해제용)");
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

            // 같은 봉투(id)를 두 번 받으면 큐에 한 번만 — 두 번째는 알람 · EntryId 를 두 번 만든다(WP-1 ⑦).
            if (!_recentEnvelopes.TryAdd(natsMessageId))
            {
                _log?.Info($"DETECTION 같은 봉투를 다시 받아 건너뜀: id={natsMessageId}, eventId={eventId}");
                return Task.CompletedTask;
            }

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
            return PublishEnqueued(new EventEntryEnqueuedMessage(entryId, eventId, deviceId, deviceType, eventType));
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsDetectionAsync 오류: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// EventEntryEnqueuedMessage 발행. UI 스레드 Background(4) 우선순위 — PublishOnUIThreadAsync(Normal=9)가 Input(5) 기아 유발(3e78d574).
    /// 헤드리스(Application 없음: 테스트/DB모드)면 현재 스레드 발행 폴백 — Dispatcher 가 없다고 발행을 조용히 버리지 않는다
    /// (DetectionSyncNatsService 와 동일 규약).
    /// </summary>
    private Task PublishEnqueued(EventEntryEnqueuedMessage message)
    {
        var ea = _eventAggregator;
        if (ea == null) return Task.CompletedTask;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return ea.PublishOnCurrentThreadAsync(message);

        dispatcher.InvokeAsync(() => ea.PublishOnCurrentThreadAsync(message), DispatcherPriority.Background);
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
    private readonly RecentEnvelopeFilter _recentEnvelopes = new();   // 같은 봉투 두 번 → 큐 한 번(WP-1 ⑦)
    #endregion
}
