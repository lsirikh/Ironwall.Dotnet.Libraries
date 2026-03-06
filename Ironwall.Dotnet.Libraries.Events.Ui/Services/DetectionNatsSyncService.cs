using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
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
        ISymbolEventManager symbolEventManager)
    {
        _log = log;
        _natsService = natsService;
        _symbolEventManager = symbolEventManager;
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
            var cmd = jObj.Value<string>("cmd");
            if (cmd != "DETECTION") return Task.CompletedTask;

            var body = jObj["body"]?.ToObject<DetectionEventDto>();
            if (body == null) return Task.CompletedTask;

            var deviceId = body.DeviceId;
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

            _log?.Info($"DETECTION 수신: deviceId={deviceId}, event={eventType}, groups=[{string.Join(",", deviceGroups ?? [])}]");

            // deviceId로 검색 (NATS DETECTION은 generic "Sensor" 타입만 전송하므로 복합키 대신 ID 검색)
            _symbolEventManager.ProcessDetectionById(deviceId, deviceGroups, eventType);
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
    #endregion
}
