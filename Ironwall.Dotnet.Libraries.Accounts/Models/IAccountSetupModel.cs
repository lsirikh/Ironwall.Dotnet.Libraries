namespace Ironwall.Dotnet.Libraries.Accounts.Models;

public interface IAccountSetupModel
{
    bool IsSession { get; set; }
    int SessionExpiration { get; set; }
}