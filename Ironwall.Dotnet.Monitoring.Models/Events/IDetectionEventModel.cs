using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;

public interface IDetectionEventModel : IExEventModel
{
    EnumDetectionType Result { get; set; }
}