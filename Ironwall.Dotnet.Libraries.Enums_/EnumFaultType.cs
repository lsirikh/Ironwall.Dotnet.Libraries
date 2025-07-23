using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumFaultType : int
    {
        FaultController = 1,
        FaultCompound = 2,
        FaultFence = 3,
        FaultCable = 4,
        FaultUnderground = 5,
        FaultPIR = 6,
        FaultLASER = 7,
        FaultIOController = 8,
        FaultContact = 9,
        FaultIpCamera = 10,
        FaultIpSpeaker = 11,
        FaultRadar = 12,
        FaultOpticalCable = 13,

        //가림장애 
        //쇼트장애
        //추가안됨
    }
}
