using Ironwall.Dotnet.Framework.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class ControllerTableMapper
        : DeviceMapperBase, IControllerTableMapper
    {
        public ControllerTableMapper()
        {

        }

        public ControllerTableMapper(IControllerDeviceModel model)
            : base(model)
        {
            IpAddress = model.IpAddress;
            Port = model.Port;
        }

        public string IpAddress { get; set; }
        public int Port { get; set; }
    }
}
