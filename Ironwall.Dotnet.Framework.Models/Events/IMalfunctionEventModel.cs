using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IMalfunctionEventModel : IMetaEventModel
    {
        EnumFaultType Reason { get; set; }
        int FirstEnd { get; set; }
        int FirstStart { get; set; }
        int SecondEnd { get; set; }
        int SecondStart { get; set; }
    }
}