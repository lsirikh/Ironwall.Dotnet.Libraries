using System.Runtime.InteropServices;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit.Abstractions;

namespace DummyCameras.Tests;

/// <summary>
/// ROI window video fill: a farm like <c>run --size 1280x720 --sub-size 640x480</c> (16:9 main, 4:3 sub — the cam66 shape).
/// A 960x540 overlay box picks the SUB (lowest covering profile). Opened the preset-manager way it is letterboxed
/// (black side bands); opened the ROI-window way (fill) the picture reaches both edges — through the real host process.
/// </summary>
public class RoiVideoFillFarmTests
{
    private const int BoxW = 960, BoxH = 540;   // 4:3 fitted = 720x540 → 120 px bands each side
    private readonly ITestOutputHelper _output;

    public RoiVideoFillFarmTests(ITestOutputHelper output) => _output = output;

    private sealed class Directory1 : IEventWindowDeviceDirectory
    {
        private readonly ICameraDeviceModel _camera;
        public Directory1(ICameraDeviceModel camera) => _camera = camera;
        public ICameraDeviceModel? FindCamera(int cameraId) => cameraId == _camera.Id ? _camera : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => null;
        public string? GroupName(int groupId) => null;
    }

    private static byte[]? Copy(IFrameSource source)
    {
        var buffer = new byte[BoxW * BoxH * 4];
        var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            for (int i = 0; i < 5; i++)
                if (source.TryCopyLatest(pin.AddrOfPinnedObject(), BoxW * 4, buffer.Length, out _)) return buffer;
            return null;
        }
        finally
        {
            pin.Free();
        }
    }

    /// <summary>Share of near-black pixels in a vertical band [x0, x1).</summary>
    private static double BlackShare(byte[] frame, int x0, int x1)
    {
        int black = 0, total = 0;
        for (int y = 0; y < BoxH; y += 6)
            for (int x = x0; x < x1; x += 3)
            {
                int i = (y * BoxW + x) * 4;
                total++;
                if (frame[i] < 0x14 && frame[i + 1] < 0x14 && frame[i + 2] < 0x14) black++;
            }
        return black / (double)total;
    }

    [SkippableFact]
    public async Task should_fill_the_roi_box_even_when_the_host_picks_a_sub_stream_of_another_aspect()
    {
        Skip.If(ToolPaths.FindOnPath("ffmpeg") is null, "ffmpeg not on PATH");
        Skip.If(!File.Exists(MediaMtxInstaller.ExePath), "MediaMTX not installed - run: DummyCameras fetch-mediamtx");
        Skip.If(CameraHostExe.Find() is null, $"{CameraHostExe.ExeName} not built (or set IRONWALL_CAMHOST_EXE)");

        // Arrange — one fixed camera, 16:9 main + 4:3 sub
        await using var farm = await Farm.StartAsync(new FarmOptions
        {
            Count = 1,
            PtzCount = 0,
            OnvifBasePort = 0,
            ControlPort = 0,
            RtspPort = 18854,
            RtpPort = 18400,
            MediaMtxApiPort = 19697,
            Width = 1280, Height = 720, SubWidth = 640, SubHeight = 480, Fps = 10,
            OutDir = TestDirs.NewOutDir("roifill"),
        }, TextWriter.Null, CancellationToken.None);
        Assert.True(await farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(20), CancellationToken.None), "streams not ready - see " + farm.LogDir);
        var camera = new CameraDeviceModel(1)
        {
            DeviceName = "DUMMY-CAM1",
            DeviceType = EnumDeviceType.IpCamera,
            Category = EnumCameraType.FIXED,
            IpAddress = "127.0.0.1",
            IpPort = farm.Find(1)!.Spec.OnvifPort,
            UserName = farm.Options.User,
            UserPassword = farm.Options.Password,
        };
        using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = CameraHostExe.Find()!,
            Headless = true,
            HostLogDirectory = TestDirs.NewOutDir("roifill-host"),
        }, new TestLog());
        host.Start();
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");
        var channel = new HostMappingCameraVideo(host, new Directory1(camera));

        // Act — the preset manager way (fit) and the ROI window way (fill), same box
        var fit = channel.OpenVideo(1, "mapping-presets-1", BoxW, BoxH);
        var fill = channel.OpenVideo(1, "mapping-roi-1", BoxW, BoxH, fillBox: true);
        Assert.True(fit.IsSuccess && fill.IsSuccess, $"{fit.Message} {fill.Message}");
        using var fitSource = fit.Value!;
        using var fillSource = fill.Value!;
        var playing = await Wait.UntilAsync(() => fitSource.PublishedSequence > 3 && fillSource.PublishedSequence > 3, TimeSpan.FromSeconds(40), 100);
        Assert.True(playing, $"no frames: fit={fitSource.State}/{fitSource.StateDetail} fill={fillSource.State}/{fillSource.StateDetail}");
        var a = Copy(fitSource);
        var b = Copy(fillSource);
        Assert.NotNull(a);
        Assert.NotNull(b);

        // Assert — fit: side bands are black (proves the sub 4:3 was picked); fill: no bands
        double fitLeft = BlackShare(a!, 0, 100), fitRight = BlackShare(a!, BoxW - 100, BoxW), fitMid = BlackShare(a!, 400, 560);
        double fillLeft = BlackShare(b!, 0, 100), fillRight = BlackShare(b!, BoxW - 100, BoxW);
        _output.WriteLine($"fit black: left={fitLeft:P0} mid={fitMid:P0} right={fitRight:P0} · fill black: left={fillLeft:P0} right={fillRight:P0}");
        Assert.True(fitLeft > 0.95 && fitRight > 0.95, "preset-manager box should keep its letterbox (sub 4:3 in 16:9)");
        Assert.True(fitMid < 0.5);
        Assert.True(fillLeft < 0.5 && fillRight < 0.5, "ROI box still has black side bands");
        Assert.Equal(CameraPopupHostState.Running, host.State);
    }
}
