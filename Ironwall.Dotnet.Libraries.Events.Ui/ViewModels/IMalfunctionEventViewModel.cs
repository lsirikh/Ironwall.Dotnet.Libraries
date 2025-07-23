using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
public interface IMalfunctionEventViewModel: IExEventViewModel
{
    int FirstEnd { get; set; }
    int FirstStart { get; set; }
    EnumFaultType Reason { get; set; }
    int SecondEnd { get; set; }
    int SecondStart { get; set; }
}