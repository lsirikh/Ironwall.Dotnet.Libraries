using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 8/10/2023 3:26:23 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraPresetResponseModel : ResponseModel, ICameraPresetResponseModel
    {

        #region - Ctors -
        public CameraPresetResponseModel()
        {
            Command = EnumCmdType.CAMERA_PRESET_DATA_RESPONSE;
        }

        public CameraPresetResponseModel(bool success, string content, List<CameraPresetModel> list)
            : base(EnumCmdType.CAMERA_PRESET_DATA_RESPONSE, success, content)
        {
            Body = list;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        [JsonProperty("body", Order = 4)]
        public List<CameraPresetModel> Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
