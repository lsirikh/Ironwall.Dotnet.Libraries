using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Enums;

public enum EnumFaultType
{
    FaultController,
    FaultCompound,
    FaultFence,
    FaultCable,
    FaultUnderground,
    FaultPIR,
    FaultLASER,
    FaultIOController,
    FaultContact,
    FaultIpCamera,
    FaultIpSpeaker,
    FaultRadar,
    FaultOpticalCable,

    //가림장애 
    //쇼트장애
    //추가안됨
}
