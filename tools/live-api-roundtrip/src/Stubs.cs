using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

/// <summary>
/// The three write services only ever call <c>FetchAllDevicesAsync</c> on the provider
/// (a post-save refresh of the in-memory device cache, which a console has no use for).
/// Everything else is a no-op. This is HARNESS scaffolding, not product code.
/// </summary>
public sealed class NullDeviceProvider : IDeviceProviderService
{
    public int FetchAllCount;

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StartService(CancellationToken token = default) => Task.CompletedTask;
    public Task TriggerInitFetchAsync(CancellationToken externalToken = default) => Task.CompletedTask;
    public void CancelInitFetch() { }
    public Task FetchAllDevicesAsync(CancellationToken token = default) { FetchAllCount++; return Task.CompletedTask; }
    public Task FetchDeviceGroupsAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task FetchServersAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task<IBaseDeviceModel> FetchDeviceByIdAsync(string typeDevice, int resourceId, CancellationToken token = default)
        => Task.FromResult<IBaseDeviceModel>(null);
    public Task RemoveDeviceByIdAsync(string typeDevice, int resourceId) => Task.CompletedTask;
    public Task FetchDeviceGroupByIdAsync(int resourceId, CancellationToken token = default) => Task.CompletedTask;
    public Task RemoveDeviceGroupByIdAsync(int resourceId) => Task.CompletedTask;
}
