namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

public interface IDetectionNatsSyncService
{
    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
