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
    public class AccountEditRequestModel
        : BaseMessageModel, IAccountEditRequestModel
    {
        public AccountEditRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_EDIT_REQUEST;
        }

        public AccountEditRequestModel(string userId, string userPass, IUserModel details)
        {
            Command = EnumCmdType.USER_ACCOUNT_EDIT_REQUEST;
            IdUser = userId;
            Password = userPass;
            Details = new AccountDetailModel(details);

        }

        /* [JsonIgnore]
         public int Id { get; set; }*/

        [JsonProperty("user_id", Order = 1)]
        public string IdUser { get; set; }

        [JsonProperty("user_pass", Order = 2)]
        public string Password { get; set; }

        [JsonProperty("details", Order = 3)]
        public AccountDetailModel Details { get; set; }

        public void Insert(string idUser, string pass, AccountDetailModel details)
        {
            IdUser = idUser;
            Password = pass;
            Details = details;
        }

    }
}
