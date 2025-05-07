namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IMalfunctionResponseModel : IResponseModel
    {
        MalfunctionRequestModel? RequestModel { get; set; }
    }
}