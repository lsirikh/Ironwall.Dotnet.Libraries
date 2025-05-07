using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountInfoRequestModel
        : BaseMessageModel, IAccountInfoRequestModel
    {
        public AccountInfoRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_INFO_REQUEST;
        }

        public AccountInfoRequestModel(string idUser, string pass)
        {
            Command = EnumCmdType.USER_ACCOUNT_INFO_REQUEST;
            IdUser = idUser;
            Password = pass;
        }

        [JsonProperty("user_id", Order = 1)]
        public string IdUser { get; set; }

        [JsonProperty("user_pass", Order = 2)]
        public string Password { get; set; }

        //public void Insert(string idUser, string pass)
        //{
        //    IdUser = idUser;
        //    Password = pass;
        //}
    }
}
