namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

internal sealed class SystemHostClock : IHostClock
{
    public static readonly SystemHostClock Instance = new();

    public DateTime UtcNow => DateTime.UtcNow;
}
