using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumShapeType : int
    {
        NONE = 0,
        
        TEXT = 1,

        LINE = 11,
        TRIANGLE = 12,
        RECTANGLE = 13,
        POLYGON = 14,
        ELLIPSE = 15,
        POLYLINE = 16,

        FENCE = 20,
        CONTROLLER = 21,
        MULTI_SNESOR = 22,
        FENCE_SENSOR = 23,
        UNDERGROUND_SENSOR = 24,
        CONTACT_SWITCH = 25,
        PIR_SENSOR = 26,
        IO_CONTROLLER = 27,
        LASER_SENSOR = 28,
        CABLE = 29,

        IP_CAMERA = 30,
        FIXED_CAMERA = 31,
        PTZ_CAMERA = 32,
        SPEEDDOM_CAMERA = 33,

        IP_SPEAKER = 40,
        RADAR = 50,
        OPTICAL_CABLE = 60,

    }
}
