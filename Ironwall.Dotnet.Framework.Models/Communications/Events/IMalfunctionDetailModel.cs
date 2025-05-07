using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IMalfunctionDetailModel
    {
        int FirstEnd { get; set; }
        int FirstStart { get; set; }
        EnumFaultType Reason { get; set; }
        int SecondEnd { get; set; }
        int SecondStart { get; set; }

        //void Insert(int reason, int fStart, int fEnd, int sStart, int sEnd);
    }
}