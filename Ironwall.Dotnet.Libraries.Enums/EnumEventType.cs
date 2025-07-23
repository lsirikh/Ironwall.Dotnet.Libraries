using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    //public enum EnumEventType
    //{
    //    // 침입 (90: 0x5A)
    //    Intrusion = 0x5A,
    //    // 접점 켜기 (86: 0x56)        
    //    ContactOn = 0x56,
    //    // 접점 끄기 (102: 0x66)
    //    ContactOff = 0x66,
    //    // 연결보고
    //    Connection = 0x68,
    //    // 조치보고 (192: 0xC0)       
    //    Action = 0xC0,
    //    // 장애보고 (115: 0x73)
    //    Fault = 0x73,
    //    // 풍량모드 (118: 0x76)
    //    WindyMode = 0x76
    //}

    public enum EnumEventType : int
    {
        None = 0,
        // 침입 (90: 0x5A)
        Intrusion = 90,
        // 접점 켜기 (86: 0x56)        
        ContactOn = 86,
        // 접점 끄기 (102: 0x66)
        ContactOff = 102,
        // 연결보고 (104: 0x68)
        Connection = 104,
        // 조치보고 (192: 0xC0)       
        Action = 192,
        // 장애보고 (115: 0x73)
        Fault = 115,
        // 풍량모드 (118: 0x76)
        WindyMode = 118
    }
}
