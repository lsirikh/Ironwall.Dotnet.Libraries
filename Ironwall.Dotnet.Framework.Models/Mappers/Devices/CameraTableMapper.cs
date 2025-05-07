using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class CameraTableMapper 
        : DeviceMapperBase, ICameraTableMapper
    {
        public CameraTableMapper()
        {

        }

        public CameraTableMapper(ICameraDeviceModel model)
            : base(model)
        {
            IpAddress = model.IpAddress;
            Port = model.Port;
            UserName = model.UserName;
            Password = model.Password;
            //Preset = model.Id;
            //Profile = model.Id;
            Category = model.Category;
            DeviceModel = model.DeviceModel;
            RtspUri = model.RtspUri;
            RtspPort = model.RtspPort;
            Mode = model.Mode;
        }

        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        //public string Preset { get; set; }
        //public string Profile { get; set; }
        public EnumCameraType Category { get; set; }
        public string DeviceModel { get; set; }
        public string RtspUri { get; set; }
        public int RtspPort { get; set; }
        public int Mode { get; set; }
    }
}
