namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IAccountEditRequestModel : IBaseMessageModel
    {
        AccountDetailModel Details { get; set; }
        string IdUser { get; set; }
        string Password { get; set; }
    }
}