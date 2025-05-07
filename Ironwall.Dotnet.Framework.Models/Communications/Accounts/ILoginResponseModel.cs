namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface ILoginResponseModel : IResponseModel
    {
        LoginResultModel Results { get; set; }
    }
}