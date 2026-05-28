using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

public interface ISplashViewModelBase : IScreen
{
    IProgress<StartupProgress> Progress { get; }
    void AllowClose();
}
