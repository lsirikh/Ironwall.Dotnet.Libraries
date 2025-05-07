namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public interface IResponseModel : IBaseMessageModel
    {
        int Code { get; set; }
        string Message { get; set; }
        bool Success { get; set; }
    }
}