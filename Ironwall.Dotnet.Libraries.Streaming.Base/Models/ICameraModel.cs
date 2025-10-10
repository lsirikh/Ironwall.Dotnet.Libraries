using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;
public interface ICameraModel : IBaseModel
{
    bool AutoPlay { get; set; }
    string? Title { get; set; }
    RtspConnectionInfo ConnectionInfo { get; set; }
    StreamingOptions StreamingOptions { get; set; }
    string Guid { get; set; }
    bool ShowControls { get; set; }
}