using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 7/4/2023 3:41:53 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingRequestModel : BaseMessageModel, ICameraMappingRequestModel
    {

        #region - Ctors -
        public CameraMappingRequestModel(EnumCmdType command = EnumCmdType.CAMERA_MAPPING_DATA_REQUEST)
         : base(command)
        {
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
