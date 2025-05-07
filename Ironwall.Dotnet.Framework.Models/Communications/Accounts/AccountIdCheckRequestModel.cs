using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountIdCheckRequestModel
        : BaseMessageModel, IAccountIdCheckRequestModel
    {
        public AccountIdCheckRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_REQUEST;
        }
        public AccountIdCheckRequestModel(string idUser)
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_REQUEST;
            IdUser = idUser;
        }

        [JsonProperty("user_id", Order = 1)]
        public string? IdUser { get; set; }
    }
}
