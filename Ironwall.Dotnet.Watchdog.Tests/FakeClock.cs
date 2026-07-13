using System;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Watchdog.Tests;
/****************************************************************************
   Purpose      : 테스트용 고정/전진 시계(규칙 I-02).
****************************************************************************/
public sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
    public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan delta)
    {
        Now += delta;
        UtcNow += delta;
    }

    public void AdvanceMs(long ms) => Advance(TimeSpan.FromMilliseconds(ms));
}
