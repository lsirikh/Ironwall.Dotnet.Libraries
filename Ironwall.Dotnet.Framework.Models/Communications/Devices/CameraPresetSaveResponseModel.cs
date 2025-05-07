using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 8/11/2023 10:24:49 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraPresetSaveResponseModel : ResponseModel, ICameraPresetSaveResponseModel
    {

        #region - Ctors -
        public CameraPresetSaveResponseModel()
        {
            Command = EnumCmdType.CAMERA_PRESET_SAVE_RESPONSE;
        }

        public CameraPresetSaveResponseModel(List<ICameraPresetModel> presets, bool success, string content)
            : base(EnumCmdType.CAMERA_PRESET_SAVE_RESPONSE, success, content)
        {
            Body = presets.OfType<CameraPresetModel>().ToList();
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
