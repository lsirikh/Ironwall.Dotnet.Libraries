using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS SYNC_DEVICE 구독 서비스
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// NATS SYNC_DEVICE 메시지를 구독하여 Device Status 변경을 실시간으로 처리합니다.
/// <para>UPDATED action 수신 → FetchDeviceByIdAsync → DeviceStatusChangedMessage 발행</para>
/// </summary>
public class DeviceNatsSyncService : IDeviceNatsSyncService
{
    #region - Ctors -
    public DeviceNatsSyncService(
        ILogService? log,
        INatsService natsService,
        IDeviceProviderService deviceProviderService,
        IEventAggregator eventAggregator)
    {
        _log = log;
        _natsService = natsService;
        _deviceProviderService = deviceProviderService;
        _ea = eventAggregator;
    }
    #endregion

    #region - IService -
    public Task StartService(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync += OnNatsSyncDeviceAsync;
        _log?.Info($"{nameof(DeviceNatsSyncService)} started — SYNC_DEVICE 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        _natsService.NatsSubscribeEventAsync -= OnNatsSyncDeviceAsync;
        _log?.Info($"{nameof(DeviceNatsSyncService)} stopped");
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - Processes -
    private async Task OnNatsSyncDeviceAsync(MessageArgsModel e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            // 외부 메시지 구조: { "cmd": "SYNC_DEVICE", "body": { ... } }
            // 또는 body를 직접 SyncDeviceBodyDto로 역직렬화 (설계 문서 기반)
            var body = JsonConvert.DeserializeObject<SyncDeviceBodyDto>(e.Data);
            if (body == null || body.Action != "UPDATED") return;

            _log?.Info($"SYNC_DEVICE 수신: action={body.Action}, type={body.TypeDevice}, id={body.ResourceId}");

            // 단일 Device 재조회 → Provider 캐시 갱신
            var updated = await _deviceProviderService.FetchDeviceByIdAsync(body.TypeDevice, body.ResourceId);
            if (updated == null)
            {
                _log?.Warning($"SYNC_DEVICE: Device({body.TypeDevice}, {body.ResourceId}) 조회 실패");
                return;
            }

            // DeviceStatusChangedMessage 발행 → SymbolEventManager.HandleAsync
            await _ea.PublishOnUIThreadAsync(
                new DeviceStatusChangedMessage(updated.Id, updated.DeviceType, updated.Status));

            _log?.Info($"DeviceStatusChangedMessage 발행: id={updated.Id}, type={updated.DeviceType}, status={updated.Status}");
        }
        catch (Exception ex)
        {
            _log?.Error($"OnNatsSyncDeviceAsync 오류: {ex.Message}");
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService _natsService;
    private readonly IDeviceProviderService _deviceProviderService;
    private readonly IEventAggregator _ea;
    #endregion
}
