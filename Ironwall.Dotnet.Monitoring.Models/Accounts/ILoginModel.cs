using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Accounts;

public interface ILoginModel : IBaseModel
{
    string Username { get; set; }
    bool IsIdSaved { get; set; }
}