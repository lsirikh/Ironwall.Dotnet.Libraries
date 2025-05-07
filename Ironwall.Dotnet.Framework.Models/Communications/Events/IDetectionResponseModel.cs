namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IDetectionResponseModel : IResponseModel
    {
        DetectionRequestModel? RequestModel { get; set; }
    }
}