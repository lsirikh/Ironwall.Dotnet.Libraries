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
        Created On   : 8/4/2023 1:07:28 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingSaveResponseModel : ResponseModel, ICameraMappingSaveResponseModel
    {

        #region - Ctors -
        public CameraMappingSaveResponseModel()
        {
            Command = EnumCmdType.CAMERA_MAPPING_SAVE_RESPONSE;
        }

        public CameraMappingSaveResponseModel(List<ICameraMappingModel> body, bool success = true, string content = default)
            : base(EnumCmdType.CAMERA_MAPPING_SAVE_RESPONSE, success, content)
        {
            Body = body.OfType<CameraMappingModel>().ToList();
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
        public List<CameraMappingModel> Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
