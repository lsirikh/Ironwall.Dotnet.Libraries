using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IModeWindyEventModel : IBaseEventModel
    {
        EnumWindyMode ModeWindy { get; set; }
    }
}