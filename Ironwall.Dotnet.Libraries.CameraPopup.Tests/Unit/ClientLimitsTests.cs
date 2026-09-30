using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>L5 — 파이프 한 메시지 상한(1 MiB)을 넘는 메시지는 대기열에 들어가기 전에 그 하나만 거절한다.</summary>
public class ClientLimitsTests
{
    [Fact]
    public void should_reject_and_log_when_message_exceeds_frame_limit()
    {
        // Arrange
        var log = new TestLog();
        using var client = new CameraPopupClient("unused-pipe", "token", 8, TimeSpan.FromSeconds(1), log);
        var huge = new OpenEventWindow { EventId = "huge", Title = new string('x', FrameCodec.MaxPayloadBytes + 1) };

        // Act
        bool accepted = client.TrySend(huge);
        bool smallAccepted = client.TrySend(new OpenEventWindow { EventId = "small", Title = "ok" });

        // Assert
        Assert.False(accepted);
        Assert.True(smallAccepted);
        Assert.Equal(1, client.QueuedCount);
        Assert.Contains(log.Lines, l => l.Contains("too large") && l.Contains(nameof(OpenEventWindow)));
    }

    [Fact]
    public void should_report_fits_when_message_within_frame_limit()
    {
        Assert.True(CameraPopupClient.FitsInFrame(new OpenEventWindow { EventId = "a" }, out var small));
        Assert.InRange(small, 1, FrameCodec.MaxPayloadBytes);
        Assert.False(CameraPopupClient.FitsInFrame(new OpenEventWindow { EventId = "b", Title = new string('x', FrameCodec.MaxPayloadBytes) }, out var big));
        Assert.True(big > FrameCodec.MaxPayloadBytes);
    }
}
