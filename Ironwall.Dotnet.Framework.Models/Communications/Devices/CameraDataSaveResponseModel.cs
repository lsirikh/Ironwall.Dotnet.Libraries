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
        Created On   : 8/9/2023 2:42:23 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraDataSaveResponseModel : ResponseModel, ICameraDataSaveResponseModel
    {

        #region - Ctors -
        public CameraDataSaveResponseModel()
        {
            Command = EnumCmdType.CAMERA_DATA_SAVE_RESPONSE;
        }

        public CameraDataSaveResponseModel(List<ICameraDeviceModel> body, bool success = true, string content = default)
            : base(EnumCmdType.CAMERA_DATA_SAVE_RESPONSE, success, content)
        {
            Body = body.OfType<CameraDeviceModel>().ToList();
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
        public List<CameraDeviceModel> Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
