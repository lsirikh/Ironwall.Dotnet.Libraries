using Caliburn.Micro;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

public interface ISplashViewModelBase : IScreen
{
    IProgress<StartupProgress> Progress { get; }
    void AllowClose();
    Task WhenActivated { get; }
}
