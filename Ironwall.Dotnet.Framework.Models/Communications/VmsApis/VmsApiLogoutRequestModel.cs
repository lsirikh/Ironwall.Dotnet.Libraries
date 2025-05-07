using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using Sensorway.Accounts.Base.Models;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:46:47 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiLogoutRequestModel : BaseMessageModel, IVmsApiLogoutRequestModel
    {
        public VmsApiLogoutRequestModel()
        {
        }


        public VmsApiLogoutRequestModel(ILoginSessionModel model)
            : base(EnumCmdType.API_LOGOUT_REQUEST)
        {
            Body = model is LoginSessionModel targetModel ? targetModel
               : throw new InvalidCastException($"model은 {typeof(LoginSessionModel)} 타입이어야 합니다.");
        }

        [JsonProperty("body", Order = 2)]
        public LoginSessionModel? Body { get; set; }
    }
}