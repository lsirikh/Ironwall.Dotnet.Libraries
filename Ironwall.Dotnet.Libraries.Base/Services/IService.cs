using System.Threading;
using System.Threading.Tasks;


namespace Ironwall.Dotnet.Libraries.Base.Services;

public interface IService
{   
    Task ExecuteAsync(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
