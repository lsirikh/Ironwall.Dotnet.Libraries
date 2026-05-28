using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

public interface IFatalErrorViewModelBase : IScreen
{
    void Configure(string jobName, string detail);
}
