using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    public interface IDeviceInfoModel
    {
        string Manufacturer { get; set; }
        string DeviceModel { get; set; }
        string FirmwareVersion { get; set; }
        string SerialNumber { get; set; }
        string HardwareId { get; set; }
        
    }
}
