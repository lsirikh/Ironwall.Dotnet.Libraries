using System.Runtime.InteropServices;
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
/// ROI 설정 창 — 상자 꽉 채우기(<see cref="VideoProviderInfo.FillBox"/>). 3:2 원본(서브 720×480 과 같은 비율)을 16:9 상자에 열면
/// 기본(비율 유지)은 양옆 검은 띠, 꽉 채우기는 띠 없이 상자 끝까지 영상이다. 실제 호스트 · LibVLC · 공유 메모리.
/// </summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "LibVlc")]
public class LibVlcFillBoxHostTests
{
    private const int BoxW = 192, BoxH = 108;   // 16:9 — 3:2 를 맞춰 넣으면 162×108, 양옆 15 px 띠
    private readonly ITestOutputHelper _output;

    public LibVlcFillBoxHostTests(ITestOutputHelper output) => _output = output;

    private static string WriteSolidPng(int w, int h, byte r, byte g, byte b)
    {
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = 0xFF; }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(HostPaths.NewLogDirectory(), $"solid-3x2-{Guid.NewGuid():N}.png");
        using (var fs = File.Create(path)) encoder.Save(fs);
        return path;
    }

    private static byte[] Copy(IFrameSource source)
    {
        var buffer = new byte[BoxW * BoxH * 4];
        var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            for (int i = 0; i < 5; i++)
                if (source.TryCopyLatest(pin.AddrOfPinnedObject(), BoxW * 4, buffer.Length, out _)) return buffer;
        }
        finally
        {
            pin.Free();
        }
        throw new InvalidOperationException("프레임 복사 실패");
    }

    private static (byte B, byte G, byte R) Pixel(byte[] frame, int x, int y)
    {
        int i = (y * BoxW + x) * 4;
        return (frame[i], frame[i + 1], frame[i + 2]);
    }

    private static bool IsBlack((byte B, byte G, byte R) p) => p.B < 0x18 && p.G < 0x18 && p.R < 0x18;

    [Fact]
    public async Task should_fill_the_box_edge_to_edge_only_when_fill_box_is_asked()
    {
        var png = WriteSolidPng(360, 240, 0xE0, 0x80, 0x20);
        var log = new TestLog();
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = HostPaths.RequireHostExe(),
            Headless = true,
            HostLogDirectory = HostPaths.NewLogDirectory(),
        }, log);
        var recorder = new StateRecorder(host);
        host.Start();
        Assert.NotNull(await recorder.WaitForStateAsync(CameraPopupHostState.Running, 0, TimeSpan.FromSeconds(15)));

        IFrameSource Open(string id, bool fill) => host.OpenOverlay(new OverlayStreamRequest
        {
            StreamId = id,
            Camera = new CameraRef { CameraId = id },
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.File, Uri = png, OpenTimeoutMs = 20_000, FillBox = fill },
            Width = BoxW,
            Height = BoxH,
        })!;
        using var fit = Open("fit", fill: false);
        using var filled = Open("fill", fill: true);
        bool got = await StateRecorder.WaitUntilAsync(() => fit.PublishedSequence > 0 && filled.PublishedSequence > 0, TimeSpan.FromSeconds(25));
        if (!got) foreach (var line in log.Lines) _output.WriteLine(line);
        Assert.True(got, "LibVLC produced no frame");

        var a = Copy(fit);
        var b = Copy(filled);
        _output.WriteLine($"fit edge={Pixel(a, 3, BoxH / 2)} center={Pixel(a, BoxW / 2, BoxH / 2)} · fill edge={Pixel(b, 3, BoxH / 2)} right={Pixel(b, BoxW - 4, BoxH / 2)}");

        // 비율 유지(프리셋 관리 창 · 지도 팝업): 가운데는 영상, 양옆은 검은 띠.
        Assert.False(IsBlack(Pixel(a, BoxW / 2, BoxH / 2)));
        Assert.True(IsBlack(Pixel(a, 3, BoxH / 2)), "비율 유지 상자에 띠가 없다");
        Assert.True(IsBlack(Pixel(a, BoxW - 4, BoxH / 2)));
        // 꽉 채우기(ROI 설정 창): 끝에서 끝까지 영상 색.
        foreach (var x in new[] { 1, 3, BoxW / 2, BoxW - 4, BoxW - 2 })
        {
            var p = Pixel(b, x, BoxH / 2);
            Assert.InRange(p.R, 0xE0 - 0x18, 0xE0 + 0x18);
            Assert.InRange(p.G, 0x80 - 0x18, 0x80 + 0x18);
            Assert.InRange(p.B, 0x20 - 0x18, 0x20 + 0x18);
        }
        Assert.Equal(CameraPopupHostState.Running, host.State);
        Assert.Equal(0, CrashWitness.Unhandled);
    }
}
