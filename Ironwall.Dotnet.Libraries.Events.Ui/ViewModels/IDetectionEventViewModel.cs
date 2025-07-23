using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;

public interface IDetectionEventViewModel : IExEventViewModel
{
    EnumDetectionType Result { get; set; }
}