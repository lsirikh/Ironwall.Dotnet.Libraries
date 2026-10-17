using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

public class SharedFrameViewTests
{
    private static string NewName() => SharedFrameLayout.BuildName(Environment.ProcessId, Guid.NewGuid().ToString("N")[..12]);

    private static byte[] Frame(int width, int height, byte value)
    {
        var bytes = new byte[width * height * 4];
        Array.Fill(bytes, value);
        return bytes;
    }

    [Fact]
    public void should_return_no_frame_when_nothing_was_written()
    {
        using var gis = SharedFrameView.CreateNew(NewName(), 8, 4);
        var buffer = new byte[8 * 4 * 4];

        Assert.Equal(0, gis.PublishedSequence);
        Assert.False(gis.TryCopyLatest(buffer, out _));
    }

    [Fact]
    public void should_read_latest_frame_when_other_side_writes()
    {
        var name = NewName();
        using var gis = SharedFrameView.CreateNew(name, 8, 4);
        using var host = SharedFrameView.OpenExisting(name, 8, 4);

        host.WriteFrame(Frame(8, 4, 0x11), 32);
        host.WriteFrame(Frame(8, 4, 0x22), 32);
        var buffer = new byte[8 * 4 * 4];

        Assert.True(gis.TryCopyLatest(buffer, out var sequence));
        Assert.Equal(2, sequence);
        Assert.All(buffer, b => Assert.Equal(0x22, b));
    }

    [Fact]
    public void should_continue_sequence_when_host_reopens_after_restart()
    {
        var name = NewName();
        using var gis = SharedFrameView.CreateNew(name, 4, 4);
        using (var first = SharedFrameView.OpenExisting(name, 4, 4))
        {
            first.WriteFrame(Frame(4, 4, 1), 16);
            first.WriteFrame(Frame(4, 4, 2), 16);
        } // 첫 호스트 사라짐 — GIS 쪽 매핑은 유지

        using var second = SharedFrameView.OpenExisting(name, 4, 4);
        Assert.Equal(3, second.WriteFrame(Frame(4, 4, 3), 16));
        Assert.Equal(3, gis.PublishedSequence);
    }

    [Fact]
    public void should_refuse_open_when_size_differs_from_header()
    {
        var name = NewName();
        using var gis = SharedFrameView.CreateNew(name, 8, 8);

        Assert.ThrowsAny<Exception>(() => SharedFrameView.OpenExisting(name, 16, 4));
    }

    [Fact]
    public async Task should_never_return_torn_frame_when_writer_races_reader()
    {
        const int w = 64, h = 64;
        var name = NewName();
        using var gis = SharedFrameView.CreateNew(name, w, h);
        using var host = SharedFrameView.OpenExisting(name, w, h);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
        var writer = Task.Run(() =>
        {
            byte v = 0;
            var frame = new byte[w * h * 4];
            while (!stop.IsCancellationRequested)
            {
                Array.Fill(frame, ++v);
                host.WriteFrame(frame, w * 4);
            }
        });

        var buffer = new byte[w * h * 4];
        int reads = 0, torn = 0;
        while (!stop.IsCancellationRequested)
        {
            if (!gis.TryCopyLatest(buffer, out _)) continue;
            reads++;
            byte first = buffer[0];
            if (buffer.AsSpan().IndexOfAnyExcept(first) >= 0) torn++;
        }
        await writer;

        Assert.True(reads > 100, $"reads={reads}");
        Assert.Equal(0, torn);
    }

    [Fact]
    public void should_ignore_calls_when_view_is_disposed()
    {
        var gis = SharedFrameView.CreateNew(NewName(), 4, 4);
        gis.Dispose();

        Assert.Equal(-1, gis.PublishedSequence);
        Assert.False(gis.TryCopyLatest(new byte[64], out _));
        Assert.Equal(-1, gis.WriteFrame(Frame(4, 4, 1), 16));
        gis.Dispose();
    }
}
