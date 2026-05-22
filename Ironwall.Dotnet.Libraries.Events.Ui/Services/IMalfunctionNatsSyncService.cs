namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

public interface IMalfunctionNatsSyncService
{
    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
