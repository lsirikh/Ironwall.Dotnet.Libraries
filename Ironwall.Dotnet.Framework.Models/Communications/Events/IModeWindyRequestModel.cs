using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IModeWindyRequestModel : IBaseMessageModel
    {
        EnumWindyMode ModeWindy { get; set; }
    }
}