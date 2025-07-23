using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Accounts;

public interface IUserModel : IBaseModel
{
    string Username { get; set; }
    string Password { get; set; }
    string Name { get; set; }
    EnumLevelType Level { get; set; }
    EnumUsedType Used { get; set; }
}