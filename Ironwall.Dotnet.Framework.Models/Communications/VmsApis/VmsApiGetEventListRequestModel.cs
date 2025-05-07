using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:47:59 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiGetEventListRequestModel : BaseMessageModel, IVmsApiGetEventListRequestModel
    {
        public VmsApiGetEventListRequestModel() : base(EnumCmdType.API_LIST_EVENT_REQUEST)
        {
        }


        public VmsApiGetEventListRequestModel(string token = default)
            : base(EnumCmdType.API_LIST_EVENT_REQUEST)
        {
            Token = token;
        }

        [JsonProperty("token", Order = 2)]
        public string? Token { get; set; }
    }
}