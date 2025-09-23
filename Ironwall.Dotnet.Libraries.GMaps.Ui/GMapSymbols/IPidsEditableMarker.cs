using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    public interface IPidsEditableMarker : IEditableMarker
    {
        int LinkedDeviceId { get; set; }
        EnumEventStatus EventStatus { get; set; }
        EnumDeviceType DeviceType { get; set; }
        bool ShowFOV { get; set; }
        EnumColorType FOVColor { get; set; }
        double FOVOpacity { get; set; }
        double DetectionRange { get; set; }
        double DetectionAngle { get; set; }
        double DetectionBearing { get; set; }
    }
}
