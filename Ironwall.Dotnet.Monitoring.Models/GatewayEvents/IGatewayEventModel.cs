using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.GatewayEvents;

public interface IGatewayEventModel : IBaseModel
{
    string EventName { get; set; }
    int Group { get; set; }
    bool IsEnable { get; set; }
    string? Description { get; set; }
}
