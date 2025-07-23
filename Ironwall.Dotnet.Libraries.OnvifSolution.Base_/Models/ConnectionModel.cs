using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Defines;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Extends;
using Newtonsoft.Json;
using System.Reflection;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/15/2023 3:12:27 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ConnectionModel : BaseModel, IConnectionModel
    {
        public ConnectionModel()
        {
        }

        public ConnectionModel(string name)
        {
            DeviceName = name;
        }

        public ConnectionModel(IConnectionModel model) : base(model.Id)
        {
            DeviceName = model.DeviceName;
            Zone = model.Zone;
            UserName = model.UserName;
            Password = model.Password;
            IpAddress = model.IpAddress;
            Port = model.Port;
            PortOnvif = model.PortOnvif;
            PortRtsp = model.PortRtsp;
            RtspAuthRequired = model.RtspAuthRequired;
            IsDummy = model.IsDummy;
            DummyOption = model.DummyOption;
        }

        public ConnectionModel(int id, string deviceName, CameraZoneModel zone,  string userName, string password, string ipAddress, int port, int portOnvif, int portRtsp, bool rtspAuthRequrired,  bool isDummy, string dummyOption) : base(id)
        {
            DeviceName = deviceName;
            Zone = zone;
            UserName = userName;
            Password = password;
            IpAddress = ipAddress;
            Port = port;
            PortOnvif = portOnvif;
            PortRtsp = portRtsp;
            RtspAuthRequired = rtspAuthRequrired;
            IsDummy = isDummy;
            DummyOption = dummyOption;
        }

        /// <summary>
        /// ConnectionModel의 정보 변경에 따른 업데이트
        /// </summary>
        /// <param name="model">IConnectionModel 타입</param>
        public void Update(IConnectionModel model)
        {
            base.Update(model);

            DeviceName = model.DeviceName;
            Zone = model.Zone;
            UserName = model.UserName;
            Password = model.Password;
            IpAddress = model.IpAddress;
            Port = model.Port;
            PortOnvif = model.PortOnvif;
            PortRtsp = model.PortRtsp;
            RtspAuthRequired = model.RtspAuthRequired;
            IsDummy = model.IsDummy;
            DummyOption = model.DummyOption;
        }

        /// <summary>
        /// Device Name Property
        /// </summary>
        [JsonProperty("device_name", Order = 1)]
        public string DeviceName { get; set; }
        /// <summary>
        /// Zone information
        /// </summary>
        [JsonProperty("zone", Order = 2)]
        public CameraZoneModel Zone { get; set; }
        /// <summary>
        /// Username for Authentication
        /// </summary>
        [JsonProperty("user_name", Order = 3)]
        public string UserName { get; set; }
        /// <summary>
        /// Password for Authentication
        /// </summary>
        [JsonProperty("password", Order = 4)]
        public string Password { get; set; }
        /// <summary>
        /// Ip Address for Ip Camera.
        /// </summary>
        [JsonProperty("ip_address", Order = 5)]
        public string IpAddress { get; set; }
        /// <summary>
        /// Web Server Port
        /// </summary>
        [JsonProperty("port", Order = 6)]
        public int Port { get; set; } = 80;
        /// <summary>
        /// Onvif Port
        /// </summary>
        [JsonProperty("port_onvif", Order = 7)]
        public int PortOnvif { get; set; } = 80;
        /// <summary>
        /// Rtsp Port
        /// </summary>
        [JsonProperty("port_rtsp", Order = 8)]
        public int PortRtsp { get; set; } = 554;
        /// <summary>
        /// Check RTSP streaming Authentication 
        /// </summary>
        [JsonProperty("rtsp_auth_required", Order = 9)]
        public bool RtspAuthRequired { get; set; } = true;
        /// <summary>
        /// Option for a streaming function without physical ip camera 
        /// </summary>
        [JsonProperty("is_dummy", Order = 10)]
        public bool IsDummy { get; set; } = false;
        /// <summary>
        /// Option for dummy setting
        /// </summary>
        [JsonProperty("dummy_option", Order = 11)]
        public string DummyOption { get; set; }
    }
}
