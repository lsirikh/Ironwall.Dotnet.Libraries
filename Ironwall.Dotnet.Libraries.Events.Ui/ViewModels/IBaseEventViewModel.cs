using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
public interface IBaseEventViewModel<T>: IBaseCustomViewModel<T> where T : IBaseEventModel
{
    DateTime DateTime { get; set; }
    int Index { get; set; }
    EnumEventType MessageType { get; set; }
}