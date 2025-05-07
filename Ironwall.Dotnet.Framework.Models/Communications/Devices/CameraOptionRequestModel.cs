using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/20/2024 5:14:37 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraOptionRequestModel : BaseMessageModel, ICameraOptionRequestModel
    {
        #region - Ctors -
        public CameraOptionRequestModel(EnumCmdType command = EnumCmdType.CAMERA_OPTION_REQUEST)
            :base(EnumCmdType.CAMERA_OPTION_REQUEST)
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