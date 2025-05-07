
using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountEditResponseModel
        : AccountInfoResponseModel, IAccountEditResponseModel
    {
        public AccountEditResponseModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_EDIT_RESPONSE;
        }

        public AccountEditResponseModel(bool success, string msg, IUserModel detail)
            : base(success, msg, detail)
        {
            Command = EnumCmdType.USER_ACCOUNT_EDIT_RESPONSE;
        }
    }
}
