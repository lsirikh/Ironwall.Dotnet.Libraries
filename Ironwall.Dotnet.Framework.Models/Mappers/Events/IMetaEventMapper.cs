using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Mappers;

public interface IMetaEventMapper : IEventMapperBase
{
    string EventGroup { get; set; }
    int Device { get; set; }
    bool Status { get; set; }
}