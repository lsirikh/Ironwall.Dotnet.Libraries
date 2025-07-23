using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Dappers;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Extends;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

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

    public class CameraDeviceModel : ConnectionModel, ICameraDeviceModel
    {

        #region - Ctors -
        public CameraDeviceModel()
        {
            Type = EnumCameraType.NONE;
            CameraStatus = EnumCameraStatus.NONE;
            CameraMedia = new CameraMediaModel();
        }

        public CameraDeviceModel(IConnectionModel model) : base(model)
        {
            Type = EnumCameraType.NONE;
            CameraStatus = EnumCameraStatus.NONE;
            CameraMedia = new CameraMediaModel();
        }

        public CameraDeviceModel(int id, string deviceName, CameraZoneModel zone, string userName, string password, string ipAddress, int port, int portOnvif, int portRtsp, bool rtspAuthRequrired, bool isDummy, string dummyOption, EnumCameraType type, EnumCameraStatus status, CameraMediaModel cameraMedia, DateTime update) 
            : base(id, deviceName, zone, userName, password, ipAddress, port, portOnvif, portRtsp, rtspAuthRequrired, isDummy, dummyOption)
        {
            Type = type;
            CameraStatus = status;
            CameraMedia = cameraMedia;
            UpdateTime = update;
        }

        public CameraDeviceModel(ICameraDeviceModel model) : base(model)
        {
            Manufacturer = model.Manufacturer;
            DeviceModel = model.DeviceModel;
            FirmwareVersion = model.FirmwareVersion;
            SerialNumber = model.SerialNumber;
            HardwareId = model.HardwareId;

            Type = model.Type;
            CameraStatus = model.CameraStatus;
            CameraMedia = model.CameraMedia;
            UpdateTime = model.UpdateTime;
        }

        public CameraDeviceModel(ICameraDeviceDapper model) : base(model.Id, model.DeviceName, zone:null, model.UserName, model.Password, model.IpAddress, model.Port, model.PortOnvif, model.PortRtsp, model.RtspAuthRequired, model.IsDummy, model.DummyOption)
        {
            Manufacturer = model.Manufacturer;
            DeviceModel = model.DeviceModel;
            FirmwareVersion = model.FirmwareVersion;
            SerialNumber = model.SerialNumber;
            HardwareId = model.HardwareId;

            Type = EnumCameraType.NONE;
            CameraStatus = EnumCameraStatus.NONE;
            CameraMedia = new CameraMediaModel();
            UpdateTime = DateTime.Now;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        /// <summary>
        /// CameraDeviceModel의 정보 변경에 따른 업데이트
        /// </summary>
        /// <param name="model">ICameraDeviceModel 타입</param>
        public void Update(ICameraDeviceModel model)
        {
            base.Update(model);

            Manufacturer = model.Manufacturer;
            DeviceModel = model.DeviceModel;
            FirmwareVersion = model.FirmwareVersion;
            SerialNumber = model.SerialNumber;
            HardwareId = model.HardwareId;

            Type = model.Type;
            CameraStatus = model.CameraStatus;
            CameraMedia = model.CameraMedia;
            UpdateTime = DateTime.Now;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        /// <summary>
        /// 카메라 Info 정보 : 제조사
        /// </summary>
        [JsonProperty("manufacturer", Order = 13)]
        public string Manufacturer { get; set; }

        /// <summary>
        /// 카메라 Info 정보 : 장비모델
        /// </summary>
        [JsonProperty("device_model", Order = 14)]
        public string DeviceModel { get; set; }

        /// <summary>
        /// 카메라 Info 정보 : 펌웨어버전
        /// </summary>
        [JsonProperty("firmware_version", Order = 15)]
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// 카메라 Info 정보 : 시리얼넘버
        /// </summary>
        [JsonProperty("serial_number", Order = 16)]
        public string SerialNumber { get; set; }

        /// <summary>
        /// 카메라 Info 정보 : 하드웨어아이디
        /// </summary>
        [JsonProperty("hardware_id", Order = 17)]
        public string HardwareId { get; set; }
        /// <summary>
        /// 카메라 타입정보 :NONE, FIXED, PTZ
        /// </summary>
        [JsonProperty("camera_type", Order = 18)]
        public EnumCameraType Type { get; set; }
        /// <summary>
        /// 카메라 Onvif 설정 상태
        /// </summary>
        [JsonProperty("camera_status", Order = 20)]
        public EnumCameraStatus CameraStatus { get; set; }
        /// <summary>
        /// Class한 Profile 정보
        /// </summary>
        [JsonProperty("camera_media", Order = 21)]
        public CameraMediaModel CameraMedia { get; set; }

        [JsonProperty("update_time", Order = 22)]
        public DateTime UpdateTime { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
