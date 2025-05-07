namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    public interface IAiMessageRequestModel : IBaseMessageModel
    {
        string Message { get; set; }
    }
}