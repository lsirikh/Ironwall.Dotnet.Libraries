using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class KeepAliveResponseModel
        : ResponseModel, IKeepAliveResponseModel
    {
        public KeepAliveResponseModel()
        {
            Command = EnumCmdType.SESSION_REFRESH_RESPONSE;
        }

        public KeepAliveResponseModel(bool success, string msg, DateTime expiredTime)
            : base(success, msg)
        {
            Command = EnumCmdType.SESSION_REFRESH_RESPONSE;
            TimeExpired = expiredTime;
        }

        [JsonProperty("expired_time", Order = 3)]
        public DateTime TimeExpired { get; set; }

        public void Insert(bool success, string msg, DateTime expiredTime)
        {
            Success = success;
            Message = msg;
            TimeExpired = expiredTime;
        }
    }
}
