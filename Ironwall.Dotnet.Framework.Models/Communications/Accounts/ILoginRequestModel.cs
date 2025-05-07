namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface ILoginRequestModel : IBaseMessageModel
    {
        bool IsForceLogin { get; set; }
        string UserId { get; set; }
        string UserPass { get; set; }

    }
}