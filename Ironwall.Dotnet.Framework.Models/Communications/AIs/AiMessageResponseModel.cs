using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 1/14/2025 6:57:41 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class AiMessageResponseModel : ResponseModel, IAiMessageResponseModel
    {
        #region - Ctors -
        public AiMessageResponseModel() : base(EnumCmdType.AI_MESSAGE_RESPONSE, false, null)
        {
        }

        public AiMessageResponseModel(bool success, string msg, string idUser, string response)
             : base(EnumCmdType.AI_MESSAGE_RESPONSE, success, msg)
        {
            IdUser = idUser;
            Response = response;
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
        [JsonProperty("user_id", Order = 4)]
        public string IdUser { get; set; }
        [JsonProperty("message", Order = 5)]
        public string Response { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}