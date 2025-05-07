using Ironwall.Dotnet.Framework.Enums;
using Sensorway.Accounts.Base;
using Newtonsoft.Json;
using System;
using Sensorway.Accounts.Base.Models;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:46:13 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiLoginRequestModel : BaseMessageModel, IVmsApiLoginRequestModel
    {
        public VmsApiLoginRequestModel()
        {
        }


        public VmsApiLoginRequestModel(ILoginUserModel model)
            : base(EnumCmdType.API_LOGIN_REQUEST)
        {
            Body = model is LoginUserModel targetModel ? targetModel
               : throw new InvalidCastException($"model은 {typeof(LoginUserModel)} 타입이어야 합니다.");
        }

        [JsonProperty("body", Order = 2)]
        public LoginUserModel? Body { get; set; }
    }
}