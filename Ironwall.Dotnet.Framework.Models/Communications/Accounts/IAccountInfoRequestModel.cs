namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountInfoRequestModel : IBaseMessageModel
    {
        string IdUser { get; set; }
        string Password { get; set; }
    }
}