using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 10:36:59 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ContactOffResponseModel : ResponseModel, IContactOffResponseModel
    {
        public ContactOffResponseModel()
        {

        }

        public ContactOffResponseModel(bool success, string msg, IContactOffRequestModel model)
            : base(EnumCmdType.EVENT_CONTACT_OFF_RESPONSE, success, msg)
        {
            RequestModel = model is ContactOffRequestModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(ContactOffRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public ContactOffRequestModel? RequestModel { get; set; }
    }
}