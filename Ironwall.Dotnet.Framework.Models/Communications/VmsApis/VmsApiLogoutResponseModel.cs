using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:47:00 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiLogoutResponseModel : ResponseModel, IVmsApiLogoutResponseModel
    {
        public VmsApiLogoutResponseModel()
        {

        }

        public VmsApiLogoutResponseModel(bool success, string msg)
            : base(EnumCmdType.API_LOGOUT_RESPONSE, success, msg)
        {
        }
    }
}