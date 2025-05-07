namespace Ironwall.Dotnet.Framework.Models.Communications.Settings
{
    public interface IHeartBeatRequestModel
    {
        string? IpAddress { get; set; }
        int Port { get; set; }
    }
}