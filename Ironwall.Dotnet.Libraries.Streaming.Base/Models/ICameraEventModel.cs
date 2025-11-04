
using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

public interface ICameraEventModel : IBaseModel
{
    string EventName { get; set; }
    List<string> CameraGuids { get; set; }
    string Description { get; set; }
    bool IsEnable { get; set; }
}