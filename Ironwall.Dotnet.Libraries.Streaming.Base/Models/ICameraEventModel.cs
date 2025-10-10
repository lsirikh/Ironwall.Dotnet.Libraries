
using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

public interface ICameraEventModel : IBaseModel
{
    EnumPopupCmd Command { get; set; }
    List<string> CameraGuids { get; set; }
    string EventName { get; set; }
    int DeviceGroup { get; set; }
    string Description { get; set; }
    EnumPopupStatus EventStatus { get; set; }
}