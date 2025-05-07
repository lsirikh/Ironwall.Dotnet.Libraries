using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Mappers;

public interface IEventMapperBase : IBaseModel
{
    EnumEventType MessageType { get; set; }
    string Datetime { get; set; }
}