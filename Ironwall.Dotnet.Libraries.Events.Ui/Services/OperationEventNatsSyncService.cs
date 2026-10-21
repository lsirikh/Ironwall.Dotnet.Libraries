using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS OPERATION_EVENT → 통문/함체 DoorState (PRD FR-13 ②, 서버 PRD operation-event v1.4)
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 서버가 <c>operation_events</c> INSERT 시 발행하는 Full-DTO(<c>cmd=OPERATION_EVENT</c>, subject <c>sensorway.{unit}.all.event.operation</c>)에서
/// 개폐 관련 항목만 골라 심볼 형태 축을 갱신한다. 판정 우선순위: <c>detail.door_status</c>(OPEN/CLOSED) → <c>reason</c>(GATE_OPEN/…_DOOR_CLOSED …).
/// 임계치 경보 등 개폐와 무관한 운영 이벤트는 지도에 반영하지 않는다. 서버 미구현 상태에서는 접점 폴백(FR-13 ③)이 같은 상태로 수렴한다.
/// 카드: <see cref="IEventAggregator"/> 를 받은 생성자면 모든 운영 이벤트를 <see cref="OperationEventCardMessage"/> 로도 낸다(operation-event-card PRD).
/// </summary>
public class OperationEventNatsSyncService : IOperationEventNatsSyncService, IService
{
    #region - Ctors -
    public OperationEventNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager,
        ITokenStorageService? tokenStorage = null)
    {
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// 카드까지 띄우는 생성자 — 받은 운영 이벤트마다 <see cref="OperationEventCardMessage"/> 를 <paramref name="eventAggregator"/> 로 낸다
    /// (이벤트 패널이 받아 운영 카드로 올린다, operation-event-card PRD §7). <paramref name="deviceProvider"/> 가 있으면 카드 장비를 캐시 장비로 잇는다.
    /// </summary>
    public OperationEventNatsSyncService(
        ILogService? log,
        INatsService natsService,
        ISymbolEventManager symbolEventManager,
        ITokenStorageService? tokenStorage,
        IEventAggregator? eventAggregator,
        DeviceProvider? deviceProvider = null)
        : this(log, natsService, symbolEventManager, tokenStorage)
    {
        _eventAggregator = eventAggregator;
        _deviceProvider = deviceProvider;
    }
    #endregion

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default) => StartService(token);

    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsOperationAsync;   // 멱등(이중 구독 방지)
        _natsService.NatsSubscribeEventAsync += OnNatsOperationAsync;
        _log?.Info($"{nameof(OperationEventNatsSyncService)} started — OPERATION_EVENT 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsOperationAsync;
        _log?.Info($"{nameof(OperationEventNatsSyncService)} stopped");
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    private async Task OnNatsOperationAsync(MessageArgsModel e)
    {
        if (_tokenStorage is { IsAuthenticated: false }) return;   // 로그인 게이팅(DETECT 와 동일)
        // 배열 봉투는 항목마다 — 호스트 라우터와 같은 의미(WP-1 ⑰).
        foreach (var envelope in NatsEnvelopeItems.Parse(e.Data, _log, "OPERATION_EVENT"))
        {
            // cmd 대문자 토큰 · 응답(m_type=RSP) 제외 — 호스트 라우터와 같은 판정(프로브 S21 · S27)
            if (!NatsEnvelopeItems.IsNotice(envelope, "OPERATION_EVENT")) continue;
            if (envelope["body"] is not JObject body) continue;
            ProcessEnvelope(envelope, body);
            await PublishCardAsync(body);
        }
    }

    /// <summary>
    /// 운영 카드 메시지 — 개폐 · 환경 경보 · 모르는 사유 모두(PRD §1). 본문은 이벤트 콘솔 '운영' 레일과 같은 변환(<see cref="DtoToModelHelper.ToOperationEventModel"/>).
    /// 서버 번호가 없으면 중복을 가를 수 없어 띄우지 않는다.
    /// </summary>
    private async Task PublishCardAsync(JObject body)
    {
        if (_eventAggregator is null) return;
        try
        {
            var model = body.ToObject<OperationEventDto>()?.ToOperationEventModel(_deviceProvider);
            if (model is not { Id: > 0 })
            {
                _log?.Info($"OPERATION_EVENT: 서버 번호 없음 — 카드 건너뜀 (reason={body.Value<string>("reason")})");
                return;
            }
            await _eventAggregator.PublishOnCurrentThreadAsync(new OperationEventCardMessage(model));
        }
        catch (Exception ex)
        {
            _log?.Error($"OPERATION_EVENT 카드 발행 오류: {ex.Message}");
        }
    }

    private void ProcessEnvelope(JObject jObj, JObject body)
    {
        try
        {
            if (PerfTrace.IsEnabled)   // 계측: 수신 직후
                PerfTrace.Mark(PerfStages.NatsReceived, PerfKinds.Operation, body.Value<int?>("id") ?? 0,
                               PerfTrace.Extra(("cmd", "OPERATION_EVENT"), ("envelope", jObj.Value<string>("id")), ("reason", body.Value<string>("reason"))));

            // 지워진 장비는 device: null(JSON null = JValue) — 거기서 "id" 를 파고들면 예외였다(프로브 S17.g ERROR).
            //   객체일 때만 장비 참조로 읽고, 아니면 장비 없음으로 조용히 넘긴다(브로커 N-3: null 과 키 없음을 같게).
            var device = body["device"] as JObject;
            int deviceId = device?.Value<int?>("id") ?? body.Value<int?>("device_id") ?? 0;
            if (deviceId <= 0)
            {
                _log?.Info($"OPERATION_EVENT: 장비 없음(삭제된 장비) — 개폐 반영 건너뜀 (reason={body.Value<string>("reason")})");
                return;
            }
            // v7.0+ 이벤트의 장비는 참조 {id, category_device}(서버 D5) — type_device 는 없다. 예전엔 type_device 만 읽어
            //   7.0+ 서버의 함체 · 통문 개폐가 지도에 한 번도 반영되지 않았다(2026-09-30 발견). 개폐가 있는 카테고리만 종류로 옮긴다.
            if (ResolveDoorDeviceType(device?.Value<string>("category_device"), device?.Value<string>("type_device")) is not { } deviceType)
                return;
            if (!DoorStateMachine.HasDoor(deviceType)) return;

            var detail = body["detail"] as JObject;   // detail: null 도 같은 함정(JValue) — 객체일 때만
            var state = Resolve(detail?.Value<string>("door_status") ?? detail?.Value<string>("gate_status"), body.Value<string>("reason"));
            if (state is null) return;   // 온도/전압 임계치 등 개폐 무관

            // 한 장비에 문 위치 부품이 둘 이상이면 서버가 부품마다 따로 이벤트를 낸다(detail.component, G47) —
            //   부품별로 기억하고 '하나라도 열림 = 열림' 으로 합친다. component 가 없으면 지금처럼 장비 단위.
            if (detail?.Value<string>("component") is { Length: > 0 } component)
                state = CombineComponentDoors(deviceId, component, state.Value);

            _symbolEventManager.SetDoorState(deviceId, deviceType, state.Value);
            _log?.Info($"OPERATION_EVENT 개폐: deviceId={deviceId}, {deviceType}, reason={body.Value<string>("reason")} → {state}");
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(OnNatsOperationAsync)} 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 이벤트 장비 참조 → 개폐 판정용 종류. <c>category_device</c>(7.0+) 가 먼저, 옛 서버의 <c>type_device</c> 가 다음.
    /// 개폐가 없는 장비면 null.
    /// </summary>
    internal static EnumDeviceType? ResolveDoorDeviceType(string? categoryDevice, string? typeDevice)
    {
        EnumDeviceType type;
        if (Enum.TryParse(typeDevice, ignoreCase: true, out EnumDeviceType legacy) && Enum.IsDefined(legacy))
            type = legacy;
        else
            type = DeviceTypeResolver.ResolveCategory(categoryDevice, null) switch
            {
                EnumDeviceCategory.Enclosure => EnumDeviceType.Enclosure,
                EnumDeviceCategory.Gate => EnumDeviceType.Gate,
                _ => EnumDeviceType.NONE,
            };
        return DoorStateMachine.HasDoor(type) ? type : null;
    }

    /// <summary>부품 하나의 문 상태를 기억하고 그 장비의 합친 상태를 돌려준다 — 하나라도 열림이면 열림, 아니면 이번 상태.</summary>
    internal EnumDoorState CombineComponentDoors(int deviceId, string component, EnumDoorState state)
    {
        lock (_componentDoors)
        {
            _componentDoors[(deviceId, component)] = state;
            foreach (var pair in _componentDoors)
                if (pair.Key.DeviceId == deviceId && pair.Value == EnumDoorState.Open) return EnumDoorState.Open;
            return state;
        }
    }

    /// <summary>detail 상태 문자열이 있으면 그것(권위), 없으면 reason 접미(_OPEN/_CLOSED)로 판정. 둘 다 없으면 null.</summary>
    internal static EnumDoorState? Resolve(string? detailStatus, string? reason)
    {
        var fromDetail = DoorStateMachine.FromServer(detailStatus);
        if (fromDetail != EnumDoorState.Unknown) return fromDetail;
        return DoorStateMachine.FromOperationEvent(reason);
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly ISymbolEventManager _symbolEventManager;
    private readonly ITokenStorageService? _tokenStorage;
    private readonly IEventAggregator? _eventAggregator;
    private readonly DeviceProvider? _deviceProvider;
    private readonly Dictionary<(int DeviceId, string Component), EnumDoorState> _componentDoors = new();   // 부품별 문 상태(G47)
    #endregion
}
