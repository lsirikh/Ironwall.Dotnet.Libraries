using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumDataType : int
    {
        None = 0,
        MapRoot = 1,
        Map = 101,

        DeviceRoot = 2,
        Controller = 201,
        Sensor = 202,
        
        GroupRoot = 3,
        Group = 301,

        GroupSymbolRoot = 4,
        GroupSymbol = 401,

        CameraRoot = 5,
        Camera = 501,

        CameraLabelRoot = 6,
        CameraLabel = 601,

    }
}
