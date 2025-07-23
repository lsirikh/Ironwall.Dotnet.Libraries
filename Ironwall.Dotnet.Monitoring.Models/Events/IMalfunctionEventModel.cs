using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
public interface IMalfunctionEventModel : IExEventModel
{
    EnumFaultType Reason { get; set; }
    int FirstEnd { get; set; }
    int FirstStart { get; set; }
    int SecondEnd { get; set; }
    int SecondStart { get; set; }
}