namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountDeleteRequestModel : IBaseMessageModel
    {
        string Token { get; set; }
        string UserId { get; set; }
        string UserPass { get; set; }
    }
}