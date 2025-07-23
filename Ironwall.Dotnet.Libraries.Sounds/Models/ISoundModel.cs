using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;

public interface ISoundModel : IBaseModel
{
    string Name { get; set; }
    string File { get; set; }
    EnumEventType? Type { get; set; }
    bool IsPlaying { get; set; }
}