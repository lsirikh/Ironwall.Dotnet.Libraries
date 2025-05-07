namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountIdCheckRequestModel : IBaseMessageModel
    {
        string? IdUser { get; set; }
    }
}