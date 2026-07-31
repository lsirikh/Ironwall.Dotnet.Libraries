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

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS MALFUNCTION 메시지를 구독하여 EventQueueManager에 Enqueue — EntryId 부여
   Created By   : GHLee
   Created On   : 2026-05-19
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MalfunctionNatsSyncService : IMalfunctionNatsSyncService, IService
{
    #region - Ctors -
    public MalfunctionNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager,
        IEventQueueManager eventQueueManager,
        IEventSetupModel eventSetupModel,
        IEventAggregator? eventAggregator = null,
        ITokenStorageService? tokenStorage = null,
        Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider? deviceProvider = null)
    {
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
        _eventQueueManager = eventQueueManager;
        _eventSetupModel = eventSetupModel;
        _eventAggregator = eventAggregator;
        _tokenStorage = tokenStorage;
        _deviceProvider = deviceProvider;   // 제어기 무통신 그룹 확장용(GMap_Controller_Blackout). 미주입 시 확장 생략(자기 그룹만).
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
        _natsService.NatsSubscribeEventAsync -= OnNatsMalfunctionAsync;
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
        // 로그인 게이팅(Login_Gated_GIS_Init): 로그인 전 NATS 이벤트 수신 차단 — 맵/캐시 미구축 상태에서 알람 방지.
        // 미조치 이벤트는 서버 DB에 영속 → 로그인 후 이력 패널 조회로 후처리. _tokenStorage 미주입 시 게이트 비활성(하위호환).
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;
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

            // [GMap_Controller_Blackout] 제어기 무통신(FAULT_CONTROLLER) → 연결 센서 그룹으로 확장 + blackout 표식.
            // 연결 센서가 모두 먹통이므로 그 센서 그룹의 PidsGroupSymbol을 검은색(Blackout, 최상위 우선)으로.
            bool isControllerBlackout = string.Equals(body.Reason, nameof(EnumFaultType.FAULT_CONTROLLER),
                                                       StringComparison.OrdinalIgnoreCase);
            if (isControllerBlackout && _deviceProvider != null)
            {
                var sensors = _deviceProvider
                    .OfType<Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel>()
                    .Select(s => ((int?)s.Controller?.Id, (IEnumerable<int>?)s.DeviceGroups));
                var expanded = ControllerBlackoutModel.ExpandBlackoutGroups(deviceId, sensors, deviceGroups);
                if (expanded.Count > 0) deviceGroups = expanded.ToList();
                _log?.Info($"MALFUNCTION 제어기무통신: ctrl={deviceId} → blackout 그룹=[{string.Join(",", deviceGroups ?? [])}]");
            }

            _log?.Info($"MALFUNCTION 수신: deviceId={deviceId}, deviceType={deviceType}, reason={body.Reason}, blackout={isControllerBlackout}, groups=[{string.Join(",", deviceGroups ?? [])}]");

            var entryId = _eventQueueManager.Enqueue(new EventEntry
            {
                DeviceId = deviceId,
                DeviceType = deviceType,
                GroupIds = deviceGroups,
                EventType = EnumEventType.Fault,
                IsControllerBlackout = isControllerBlackout,
                EventId = eventId,
                // 장애 전용 설정(= "장애 이벤트 해제" 항목) — 탐지와 독립.
                // IsMalfunctionAutoEventDiscard=false면 IsAutoReportEnabled=false → EQM tick에서 skip → 장애 자동조치보고 미발송.
                TimeoutSeconds = _eventSetupModel.MalfunctionTimeDiscardSec,
                IsAutoReportEnabled = _eventSetupModel.IsMalfunctionAutoEventDiscard
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
    private readonly ITokenStorageService? _tokenStorage;   // 로그인 게이팅 — IsAuthenticated 단일 소스
    private readonly Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider? _deviceProvider;   // 제어기→센서→그룹 토폴로지(GMap_Controller_Blackout)
    #endregion
}
