using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface IMalfunctionEventMapper : IMetaEventMapper
    {
        int FirstEnd { get; set; }
        int FirstStart { get; set; }
        EnumFaultType Reason { get; set; }
        int SecondEnd { get; set; }
        int SecondStart { get; set; }
    }
}