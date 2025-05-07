using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountAllResponseModel : IResponseModel
    {
        List<AccountDetailModel> Details { get; set; }
    }
}