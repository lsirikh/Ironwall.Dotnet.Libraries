using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
public interface IModeWindyEventModel : IExEventModel
{
    EnumWindyMode ModeWindy { get; set; }
}