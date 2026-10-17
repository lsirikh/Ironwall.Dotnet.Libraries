using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/// <summary>NATS OPERATION_EVENT(all.event.operation) → 통문/함체 개폐 형태(FR-13 ②).</summary>
public interface IOperationEventNatsSyncService
{
    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
