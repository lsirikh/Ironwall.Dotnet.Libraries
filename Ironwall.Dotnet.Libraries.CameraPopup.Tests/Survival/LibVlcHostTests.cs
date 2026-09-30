using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>
/// 실제 LibVLC 디코딩이 호스트 안에서 돌고 프레임이 공유 메모리로 GIS(시험 프로세스)에 오는지.
/// 원본: 시험이 만든 단색 PNG(LibVLC 이미지 디먹서) — 카메라 · 네트워크 불필요.
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "LibVlc")]
public class LibVlcHostTests
{
    private readonly ITestOutputHelper _output;

    public LibVlcHostTests(ITestOutputHelper output) => _output = output;

    private static string WriteSolidPng(byte r, byte g, byte b)
    {
        const int w = 320, h = 180;
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = 0xFF; }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(HostPaths.NewLogDirectory(), $"solid-{Guid.NewGuid():N}.png");
        using (var fs = File.Create(path)) encoder.Save(fs);
        return path;
    }

    [Fact]
    public async Task should_deliver_decoded_frames_through_shared_memory_when_libvlc_plays_local_file()
    {
        var png = WriteSolidPng(0xE0, 0x80, 0x20);
        var log = new TestLog();
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            EnableDebugCommands = true,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        }, log);
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15)));

        var sw = Stopwatch.StartNew();
        using var source = host.OpenOverlay(new OverlayStreamRequest
        {
            StreamId = "vlc",
            Camera = new CameraRef { CameraId = "file" },
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.File, Uri = png, OpenTimeoutMs = 20_000 },
            Width = 160,
            Height = 90,
        })!;
        bool gotFrame = await StateRecorder.WaitUntilAsync(() => source.PublishedSequence > 0, TimeSpan.FromSeconds(25));
        var states = recorder.Messages.Select(m => m.Message).OfType<StreamStateChanged>().Select(s => $"{s.State}:{s.Detail}");
        _output.WriteLine($"states: {string.Join(" ", states)}");
        if (!gotFrame) foreach (var line in log.Lines) _output.WriteLine(line);
        Assert.True(gotFrame, "LibVLC produced no frame");
        long firstFrameMs = sw.ElapsedMilliseconds;

        var buffer = new byte[160 * 90 * 4];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            Assert.True(source.TryCopyLatest(pin.AddrOfPinnedObject(), 640, buffer.Length, out _) ||
                        source.TryCopyLatest(pin.AddrOfPinnedObject(), 640, buffer.Length, out _));
        }
        finally
        {
            pin.Free();
        }
        int center = (45 * 160 + 80) * 4;
        _output.WriteLine($"first frame {firstFrameMs} ms, center BGR=({buffer[center]:X2},{buffer[center + 1]:X2},{buffer[center + 2]:X2}) state={source.State}");
        Assert.InRange(buffer[center], 0x20 - 0x18, 0x20 + 0x18);
        Assert.InRange(buffer[center + 1], 0x80 - 0x18, 0x80 + 0x18);
        Assert.InRange(buffer[center + 2], 0xE0 - 0x18, 0xE0 + 0x18);

        // 디코딩 중인 호스트를 네이티브 충돌로 죽여도 GIS(이 프로세스)는 산다 — 그리고 LibVLC 스트림이 다시 열린다.
        long t0 = recorder.NowMs;
        long seq = source.PublishedSequence;
        Assert.True(host.SendDebugCommand(DebugCommandKind.NativeAccessViolation));
        var running = await recorder.WaitForStateAsync(CameraPopupHostState.Running, t0, TimeSpan.FromSeconds(8));
        Assert.NotNull(running);
        Assert.True(await StateRecorder.WaitUntilAsync(() => source.PublishedSequence > seq, TimeSpan.FromSeconds(20)), "LibVLC stream not replayed");
        _output.WriteLine($"libvlc replay: running after {running!.Value.AtMs - t0} ms, frames after {recorder.NowMs - t0} ms");
        Assert.Equal(0, CrashWitness.Unhandled);
    }
}
