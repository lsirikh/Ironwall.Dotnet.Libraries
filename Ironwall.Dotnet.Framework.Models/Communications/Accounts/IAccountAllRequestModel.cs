namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountAllRequestModel : IBaseMessageModel
    {
        string IdUser { get; set; }
        string Password { get; set; }
    }
}