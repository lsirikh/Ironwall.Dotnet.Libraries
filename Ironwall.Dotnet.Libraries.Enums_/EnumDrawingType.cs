using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumDrawingType : int
    {
        NONE = 0,
        
        Controller = 1,
        Sensor = 2,
        DeviceLabel=3,
        
        Group = 4,
        GroupLabel = 5,
        
        IpCamera = 6,
        IpCameraLabel = 7,
        
        Symbol = 8,
        SymbolLabel = 9,

    }
}
