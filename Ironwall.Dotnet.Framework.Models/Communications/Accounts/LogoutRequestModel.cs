using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class LogoutRequestModel : BaseMessageModel, ILogoutRequestModel
    {
        public LogoutRequestModel()
        {
            Command = EnumCmdType.LOGOUT_REQUEST;
        }

        public LogoutRequestModel(string userId, string token)
        {
            Command = EnumCmdType.LOGOUT_REQUEST;
            UserId = userId;
            Token = token;
        }

        public LogoutRequestModel(EnumCmdType cmd, string userId, string token)
            : base(cmd)
        {
            UserId = userId;
            Token = token;
        }

        [JsonProperty("user_id", Order = 1)]
        public string UserId { get; set; }
        [JsonProperty("auth_token", Order = 2)]
        public string Token { get; set; }

        public void Insert(string userId, string token)
        {
            UserId = userId;
            Token = token;
        }

        public void Insert(EnumCmdType cmd, string userId, string token)
        {
            Command = cmd;
            UserId = userId;
            Token = token;
        }
    }
}
