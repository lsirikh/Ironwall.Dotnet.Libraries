using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/20/2024 5:42:25 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraOptionResponseModel : ResponseModel, ICameraOptionResponseModel
    {
        public CameraOptionResponseModel()
        {
            Command = EnumCmdType.CAMERA_OPTION_RESPONSE;
        }
        public CameraOptionResponseModel(List<CameraPresetModel> presets, List<CameraProfileModel> profiles, bool success = true, string content = default)
            : base(EnumCmdType.CAMERA_OPTION_RESPONSE, success, content)
        {
            Presets = presets;
            Profiles = profiles;
        }
        [JsonProperty("presets", Order = 4)]
        public List<CameraPresetModel> Presets { get; private set; }
        [JsonProperty("profiles", Order = 5)]
        public List<CameraProfileModel> Profiles { get; private set; }
    }
}