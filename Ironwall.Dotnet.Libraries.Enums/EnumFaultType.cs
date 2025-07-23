using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumFaultType : int
    {
        FAULT_CONTROLLER = 1,
        FAULT_FENCE = 2,
        FAULT_MULTI = 3,
        FAULT_CABLE_CUTTING = 4,
        FAULT_ETC = 5,
        //FAULT_Underground = 5,
        //FAULT_PIR = 6,
        //FAULT_LASER = 7,
        //FAULT_IOController = 8,
        //FAULT_Contact = 9,
        //FAULT_IpCamera = 10,
        //FAULT_IpSpeaker = 11,
        //FAULT_Radar = 12,
        //FAULT_OpticalCable = 13,

        //가림장애 
        //쇼트장애
        //추가안됨
    }
}
