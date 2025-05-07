using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 10:36:24 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ContactOnResponseModel : ResponseModel, IContactOnResponseModel
    {
        public ContactOnResponseModel()
        {

        }

        public ContactOnResponseModel(bool success, string msg, IContactOnRequestModel model)
            : base(EnumCmdType.EVENT_CONTACT_ON_RESPONSE, success, msg)
        {
            RequestModel = model is ContactOnRequestModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(ContactOnRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public ContactOnRequestModel? RequestModel { get; set; }
    }
}