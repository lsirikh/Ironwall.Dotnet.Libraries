namespace Ironwall.Dotnet.Framework.Models.Communications.Settings
{
    public interface IHeartBeatResponseModel : IResponseModel
    {
        string? TimeExpired { get; set; }
    }
}