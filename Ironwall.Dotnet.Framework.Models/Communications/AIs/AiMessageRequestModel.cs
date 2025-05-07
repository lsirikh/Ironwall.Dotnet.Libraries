using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 1/14/2025 6:57:12 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class AiMessageRequestModel : BaseMessageModel, IAiMessageRequestModel
    {
        #region - Ctors -
        public AiMessageRequestModel() : base(EnumCmdType.AI_MESSAGE_REQUEST)
        {
        }
        public AiMessageRequestModel(string idUser, string password, string message)
            : base(EnumCmdType.AI_MESSAGE_REQUEST)
        {
            IdUser = idUser;
            Password = password;
            Message = message;
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
        [JsonProperty("user_id", Order = 1)]
        public string IdUser { get; set; }

        [JsonProperty("user_pass", Order = 2)]
        public string Password { get; set; }

        [JsonProperty("message", Order = 3)]
        public string Message { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}