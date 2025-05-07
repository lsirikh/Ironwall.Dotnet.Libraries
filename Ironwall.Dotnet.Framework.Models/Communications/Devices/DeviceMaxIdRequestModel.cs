using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/14/2024 3:07:23 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DeviceMaxIdRequestModel : UserSessionBaseRequestModel, IDeviceMaxIdRequestModel
    {
        #region - Ctors -
        public DeviceMaxIdRequestModel()
        {
            Command = EnumCmdType.DEVICE_MAX_ID_REQUEST;
        }

        public DeviceMaxIdRequestModel(ILoginSessionModel model)
            : base(model)
        {
            Command = EnumCmdType.DEVICE_MAX_ID_REQUEST;
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