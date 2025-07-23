using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
public interface IActionEventViewModel: IBaseEventViewModel<IActionEventModel>
{
    string? Content { get; set; }
    IExEventModel OriginEvent { get; set; }
    string? User { get; set; }
}