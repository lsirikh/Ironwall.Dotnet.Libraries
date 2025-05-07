using Ironwall.Dotnet.Framework.Models.Accounts;
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
        Created On   : 8/11/2023 10:24:39 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraPresetSaveRequestModel : UserSessionBaseRequestModel, ICameraPresetSaveRequestModel
    {

        #region - Ctors -
        public CameraPresetSaveRequestModel()
        {
            Command = EnumCmdType.CAMERA_PRESET_SAVE_REQUEST;
        }

        public CameraPresetSaveRequestModel(ILoginSessionModel model, List<ICameraPresetModel> presets)
         : base(model)
        {
            Command = EnumCmdType.CAMERA_PRESET_SAVE_REQUEST;
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
        [JsonProperty("body", Order = 5)]
        public List<CameraPresetModel> Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
