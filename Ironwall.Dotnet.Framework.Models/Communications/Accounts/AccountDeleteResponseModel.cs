using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountDeleteResponseModel
        : ResponseModel, IAccountDeleteResponseModel
    {
        public AccountDeleteResponseModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_DELETE_RESPONSE;
        }

        public AccountDeleteResponseModel(bool success, string msg)
            : base(success, msg)
        {
            Command = EnumCmdType.USER_ACCOUNT_DELETE_RESPONSE;
        }

        //public void Insert(bool success, string msg)
        //{
        //    Success = success;
        //    Message = msg;
        //}
    }
}
