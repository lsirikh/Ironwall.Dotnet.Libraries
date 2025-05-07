using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountDeleteRequestModel
        : BaseMessageModel, IAccountDeleteRequestModel
    {
        public AccountDeleteRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_DELETE_REQUEST;
        }

        public AccountDeleteRequestModel(string userId, string userPass, string token)
        {
            Command = EnumCmdType.USER_ACCOUNT_DELETE_REQUEST;
            UserId = userId;
            UserPass = userPass;
            Token = token;
        }

        [JsonProperty("auth_token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("user_id", Order = 3)]
        public string UserId { get; set; }

        [JsonProperty("user_pass", Order = 4)]
        public string UserPass { get; set; }

        public void Insert(string token, string userId, string userPass)
        {
            Token = token;
            UserId = userId;
            UserPass = userPass;
        }
    }
}
