using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountDeleteAllResponseModel
        : ResponseModel, IAccountDeleteAllResponseModel
    {
        public AccountDeleteAllResponseModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_ALL_DELETE_RESPONSE;
        }

        public AccountDeleteAllResponseModel(bool success, string msg, List<string> deletedAccounts)
            : base(success, msg)
        {
            Command = EnumCmdType.USER_ACCOUNT_ALL_DELETE_RESPONSE;
            DeletedAccounts = deletedAccounts;
        }

        [JsonProperty("deleted_count", Order = 4)]
        public List<string> DeletedAccounts { get; set; }
    }
}
