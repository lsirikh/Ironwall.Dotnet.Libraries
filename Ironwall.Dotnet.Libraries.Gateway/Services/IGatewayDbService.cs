using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;

namespace Ironwall.Dotnet.Libraries.Gateway.Services;

public interface IGatewayDbService: IService
{
    Task StartService(CancellationToken token = default);
    Task StopService(CancellationToken token = default);
    Task Connect(CancellationToken token = default);
    Task Disconnect(CancellationToken token = default);
    Task BuildSchemeAsync(CancellationToken token = default);
    Task FetchInstanceAsync(CancellationToken token = default);
    Task<List<IGatewayEventModel>?> FetchGatewayEventsAsync(CancellationToken token = default);
    Task<IGatewayEventModel?> FetchGatewayEventAsync(int id, CancellationToken token = default);
    Task<IGatewayEventModel?> InsertGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default);
    Task<IGatewayEventModel?> UpdateGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default);
    Task<bool> DeleteGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default);
    bool IsConnected { get; }
}