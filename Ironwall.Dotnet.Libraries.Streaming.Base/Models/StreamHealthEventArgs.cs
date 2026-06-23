namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

public enum StreamHealth
{
    Connected,
    Reconnecting,
    Disconnected,
    Failed
}

public sealed class StreamHealthEventArgs(string cameraId, StreamHealth health, string message) : EventArgs
{
    public string CameraId { get; } = cameraId;
    public StreamHealth Health { get; } = health;
    public string Message { get; } = message;
}
