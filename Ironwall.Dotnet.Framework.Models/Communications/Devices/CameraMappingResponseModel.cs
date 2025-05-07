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
        Created On   : 7/4/2023 3:46:43 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingResponseModel : ResponseModel, ICameraMappingResponseModel
    {

        #region - Ctors -
        public CameraMappingResponseModel()
        {
            Command = EnumCmdType.CAMERA_MAPPING_DATA_RESPONSE;
        }

        public CameraMappingResponseModel(bool success, string content, List<ICameraMappingModel> list)
            : base(EnumCmdType.CAMERA_MAPPING_DATA_RESPONSE, success, content)
        {
            Body = list.OfType<CameraMappingModel>().ToList();
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
        [JsonProperty("mapping_list", Order = 4)]
        public List<CameraMappingModel> Body { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
