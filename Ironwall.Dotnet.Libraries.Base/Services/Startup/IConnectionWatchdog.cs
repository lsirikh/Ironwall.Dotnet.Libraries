namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

public interface IConnectionWatchdog : IDisposable
{
    void Start();
}
