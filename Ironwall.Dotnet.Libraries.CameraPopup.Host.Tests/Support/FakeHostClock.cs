using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;

/// <summary>손으로 넘기는 시계.</summary>
internal sealed class FakeHostClock : IHostClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 30, 0, 41, 7, DateTimeKind.Utc);

    public void Advance(double seconds) => UtcNow = UtcNow.AddSeconds(seconds);
}
