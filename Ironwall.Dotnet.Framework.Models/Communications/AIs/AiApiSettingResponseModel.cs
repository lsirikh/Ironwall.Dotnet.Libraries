using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 1/14/2025 6:56:34 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class AiApiSettingResponseModel : ResponseModel, IAiApiSettingResponseModel
    {
        #region - Ctors -
        public AiApiSettingResponseModel()
            : base(EnumCmdType.AI_API_SETTING_RESPONSE, false, null)
        {
            
        }

        public AiApiSettingResponseModel(bool success, string msg)
            : base(EnumCmdType.AI_API_SETTING_RESPONSE, success, msg)
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