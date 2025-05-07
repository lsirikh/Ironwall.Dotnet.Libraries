using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using Sensorway.Accounts.Base.Models;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:47:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiKeepAliveRequestModel : BaseMessageModel, IVmsApiKeepAliveRequestModel
    {
        public VmsApiKeepAliveRequestModel()
        {
        }


        public VmsApiKeepAliveRequestModel(string token)
            : base(EnumCmdType.API_KEEP_ALIVE_USER_REQUEST)
        {
            Token = token;
        }

        [JsonProperty("token", Order = 2)]
        public string? Token { get; set; }
    }
}