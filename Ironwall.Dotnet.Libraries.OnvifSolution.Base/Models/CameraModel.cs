using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/5/2024 1:48:53 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraModel : ConnectionModel, ICameraModel
    {
        #region - Ctors -
        private void InitializeDefaults()
        {
            Type = EnumCameraType.NONE;
            CameraStatus = EnumCameraStatus.NONE;
            CameraMedia = new CameraMediaModel();
            UpdateTime = DateTime.Now;
        }
        public CameraModel()
        {
            InitializeDefaults();
        }

        public CameraModel(IConnectionModel model) : base(model)
        {
            InitializeDefaults();
        }

        public CameraModel(ICameraModel model) : base(model)
        {
            InitializeDefaults();
            CopyFrom(model);
        }


        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        private void CopyFrom(ICameraModel s)
        {
            Name = s.Name;
            Location = s.Location;
            Manufacturer = s.Manufacturer;
            DeviceModel = s.DeviceModel;
            Hardware = s.Hardware;
            FirmwareVersion = s.FirmwareVersion;
            SerialNumber = s.SerialNumber;
            HardwareId = s.HardwareId;
            MacAddress = s.MacAddress;
            OnvifVersion = s.OnvifVersion;
            ServiceUri = s.ServiceUri;
            Type = s.Type;
            CameraStatus = s.CameraStatus;
            CameraMedia = s.CameraMedia;
            UpdateTime = s.UpdateTime;
        }
        #endregion
        #region - Processes -
        /* ───────── 동기화 메서드 ───────── */
        public void Update(ICameraModel src)
        {
            base.Update(src);        // ConnectionModel 부분
            CopyFrom(src);           // Camera 고유 부분
            UpdateTime = DateTime.Now;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        [JsonProperty("name", Order = 13)] public string? Name { get; set; }
        [JsonProperty("location", Order = 14)] public string? Location { get; set; }

        [JsonProperty("manufacturer", Order = 15)] public string? Manufacturer { get; set; }
        [JsonProperty("device_model", Order = 16)] public string? DeviceModel { get; set; }
        [JsonProperty("hardware_version", Order = 17)] public string? Hardware { get; set; }

        [JsonProperty("firmware_version", Order = 18)] public string? FirmwareVersion { get; set; }
        [JsonProperty("serial_number", Order = 19)] public string? SerialNumber { get; set; }

        [JsonProperty("hardware_id", Order = 20)] public string? HardwareId { get; set; }
        [JsonProperty("mac_address", Order = 21)] public string? MacAddress { get; set; }
        [JsonProperty("onvif_version", Order = 22)] public string? OnvifVersion { get; set; }
        [JsonProperty("service_uri", Order = 23)] public string? ServiceUri { get; set; }

        [JsonProperty("camera_type", Order = 24)] public EnumCameraType Type { get; set; }
        [JsonProperty("camera_status", Order = 25)] public EnumCameraStatus CameraStatus { get; set; }

        [JsonProperty("camera_media", Order = 26)] public CameraMediaModel? CameraMedia { get; set; }

        [JsonProperty("update_time", Order = 27)] public DateTime UpdateTime { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
