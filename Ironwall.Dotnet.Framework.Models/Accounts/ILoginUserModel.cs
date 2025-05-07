using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public interface ILoginUserModel : ILoginBaseModel
{
    int ClientId { get; set; }
    int Mode { get; set; }
    EnumAccountLevel UserLevel { get; set; }
    string ToString();
}