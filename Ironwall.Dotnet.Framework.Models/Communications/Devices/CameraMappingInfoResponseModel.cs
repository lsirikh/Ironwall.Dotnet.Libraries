using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 7/4/2023 6:00:08 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingInfoResponseModel : ResponseModel, ICameraMappingInfoResponseModel
    {

        #region - Ctors -
        public CameraMappingInfoResponseModel()
        {
            Command = EnumCmdType.MAPPING_INFO_RESPONSE;
        }

        public CameraMappingInfoResponseModel(bool success, string content, MappingInfoModel detail)
            : base(success, content)
        {
            Command = EnumCmdType.MAPPING_INFO_RESPONSE;
            Detail = detail;
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
        [JsonProperty("detail", Order = 4)]
        public MappingInfoModel Detail { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
