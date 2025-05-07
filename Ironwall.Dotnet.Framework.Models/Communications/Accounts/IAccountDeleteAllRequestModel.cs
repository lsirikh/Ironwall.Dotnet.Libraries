using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountDeleteAllRequestModel : IBaseMessageModel
    {
        string Token { get; set; }
        string UserId { get; set; }
        List<AccountDetailModel> UserList { get; set; }
        string UserPass { get; set; }
    }
}