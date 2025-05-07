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
    public class AccountInfoResponseModel
        : ResponseModel, IAccountInfoResponseModel
    {
        public AccountInfoResponseModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_INFO_RESPONSE;
        }

        public AccountInfoResponseModel(bool success, string msg, IUserModel detail)
            : base(success, msg)
        {
            Command = EnumCmdType.USER_ACCOUNT_INFO_RESPONSE;
            Details = detail as AccountDetailModel;
        }

        [JsonProperty("details", Order = 4)]
        public AccountDetailModel Details { get; set; }

        public void Insert(bool success, string msg, AccountDetailModel detail)
        {
            Success = success;
            Message = msg;
            Details = detail;
        }
    }
}
