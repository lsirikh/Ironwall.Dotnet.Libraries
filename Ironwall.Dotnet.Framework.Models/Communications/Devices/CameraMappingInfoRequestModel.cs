using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 7/4/2023 5:51:46 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingInfoRequestModel : UserSessionBaseRequestModel, ICameraMappingInfoRequestModel
    {

        #region - Ctors -
        public CameraMappingInfoRequestModel()
        {
            Command = EnumCmdType.MAPPING_INFO_REQUEST;
        }

        public CameraMappingInfoRequestModel(ILoginSessionModel model)
            : base(model)
        {
            Command = EnumCmdType.MAPPING_INFO_REQUEST;
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
    #endregion
    #region - Attributes -
    #endregion
    }
}
