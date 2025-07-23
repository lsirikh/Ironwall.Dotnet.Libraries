using Ironwall.Dotnet.Libraries.ViewModel.Models;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
public interface IBasePanelViewModel
{
    Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken);
}