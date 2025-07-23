using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
public interface IBaseEventModel : IBaseModel
{
    DateTime DateTime { get; set; }
    EnumEventType MessageType { get; set; }
}