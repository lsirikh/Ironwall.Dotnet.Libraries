using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IBaseEventModel : IBaseModel
    {
        EnumEventType MessageType { get; set; }
        DateTime DateTime { get; set; }
    }
}