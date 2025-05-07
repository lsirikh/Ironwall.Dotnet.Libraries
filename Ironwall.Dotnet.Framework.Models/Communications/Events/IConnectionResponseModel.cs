namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IConnectionResponseModel : IResponseModel
    {
        ConnectionRequestModel RequestModel { get; set; }
    }
}