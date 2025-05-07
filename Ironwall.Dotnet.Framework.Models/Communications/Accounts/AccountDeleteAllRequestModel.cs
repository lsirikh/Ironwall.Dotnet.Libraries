using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountDeleteAllRequestModel
        : BaseMessageModel, IAccountDeleteAllRequestModel
    {
        public AccountDeleteAllRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_ALL_DELETE_REQUEST;
        }

        public AccountDeleteAllRequestModel(string userId, string userPass, string token, List<AccountDetailModel> details)
        {
            Command = EnumCmdType.USER_ACCOUNT_ALL_DELETE_REQUEST;
            UserId = userId;
            UserPass = userPass;
            Token = token;
            UserList = details;
        }

        [JsonProperty("auth_token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("user_id", Order = 3)]
        public string UserId { get; set; }

        [JsonProperty("user_pass", Order = 4)]
        public string UserPass { get; set; }

        [JsonProperty("user_list", Order = 5)]
        public List<AccountDetailModel> UserList { get; set; }
    }
}
