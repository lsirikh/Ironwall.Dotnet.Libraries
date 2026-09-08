using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json.Linq;
using System;
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
/// 임계치 경보 등 개폐와 무관한 운영 이벤트는 무시한다(카드는 별도 차수). 서버 미구현 상태에서는 접점 폴백(FR-13 ③)이 같은 상태로 수렴한다.
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
    private Task OnNatsOperationAsync(MessageArgsModel e)
    {
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;   // 로그인 게이팅(DETECT 와 동일)
        try
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return Task.CompletedTask;
            var jObj = JObject.Parse(e.Data);
            if (jObj.Value<string>("cmd") != "OPERATION_EVENT") return Task.CompletedTask;
            var body = jObj["body"];
            if (body is null) return Task.CompletedTask;

            var device = body["device"];
            int deviceId = device?.Value<int?>("id") ?? body.Value<int?>("device_id") ?? 0;
            var typeStr = device?.Value<string>("type_device") ?? string.Empty;
            if (deviceId <= 0 || !Enum.TryParse<EnumDeviceType>(typeStr, ignoreCase: true, out var deviceType)) return Task.CompletedTask;
            if (!DoorStateMachine.HasDoor(deviceType)) return Task.CompletedTask;

            var state = Resolve(body["detail"]?.Value<string>("door_status") ?? body["detail"]?.Value<string>("gate_status"), body.Value<string>("reason"));
            if (state is null) return Task.CompletedTask;   // 온도/전압 임계치 등 개폐 무관

            _symbolEventManager.SetDoorState(deviceId, deviceType, state.Value);
            _log?.Info($"OPERATION_EVENT 개폐: deviceId={deviceId}, {deviceType}, reason={body.Value<string>("reason")} → {state}");
        }
        catch (Exception ex)
        {
            _log?.Error($"{nameof(OnNatsOperationAsync)} 오류: {ex.Message}");
        }
        return Task.CompletedTask;
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
    #endregion
}
