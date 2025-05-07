using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public interface IUserBaseModel : IAccountBaseModel
{
    string IdUser { get; set; }
    string Name { get; set; }
    string Password { get; set; }
    EnumAccountLevel Level { get; set; }
    bool Used { get; set; }
}