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
        Created On   : 8/9/2023 2:34:48 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraDataSaveRequestModel : UserSessionBaseRequestModel, ICameraDataSaveRequestModel
    {

        #region - Ctors -
        public CameraDataSaveRequestModel()
        {
            Command = EnumCmdType.CAMERA_DATA_SAVE_REQUEST;
        }

        public CameraDataSaveRequestModel(ILoginSessionModel model, List<ICameraDeviceModel> camera)
         : base(model)
        {
            Command = EnumCmdType.CAMERA_DATA_SAVE_REQUEST;
            Body = camera.OfType<CameraDeviceModel>().ToList();
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
        public List<CameraDeviceModel> Body { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
