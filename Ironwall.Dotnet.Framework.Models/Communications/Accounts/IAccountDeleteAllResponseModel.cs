using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountDeleteAllResponseModel : IResponseModel
    {
        List<string> DeletedAccounts { get; set; }
    }
}