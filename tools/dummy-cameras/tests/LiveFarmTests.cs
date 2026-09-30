using System.Diagnostics;
using System.Text.Json;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;

namespace DummyCameras.Tests;

/// <summary>
/// Full farm: MediaMTX + ffmpeg publishers on NON-default ports (so a running dummy farm is not disturbed):
/// cam1 PTZ · cam2 fixed · cam3 auth-fail · cam4 killed after 6 s · cam5 corrupt stream.
/// </summary>
public sealed class LiveFarmFixture : IAsyncLifetime
{
    public Farm? Farm { get; private set; }
    public string? SkipReason { get; private set; }
    public string? Ffprobe { get; private set; }
    public bool Ready { get; private set; }

    public async Task InitializeAsync()
    {
        var ffmpeg = ToolPaths.FindOnPath("ffmpeg");
        Ffprobe = ToolPaths.FindOnPath("ffprobe");
        if (ffmpeg is null || Ffprobe is null) { SkipReason = "ffmpeg/ffprobe not on PATH"; return; }
        if (!File.Exists(MediaMtxInstaller.ExePath)) { SkipReason = "MediaMTX not installed - run: DummyCameras fetch-mediamtx"; return; }

        var o = new FarmOptions
        {
            Count = 5,
            PtzCount = 1,
            OnvifBasePort = 0,
            ControlPort = 0,
            RtspPort = 18554,
            RtpPort = 18100,
            MediaMtxApiPort = 19997,
            Width = 640, Height = 360, SubWidth = 320, SubHeight = 180, Fps = 10,
            OutDir = TestDirs.NewOutDir("live"),
        };
        o.FaultSetters[3] = f => f.AuthFail = true;
        o.FaultSetters[4] = f => f.KillStreamAfterSec = 6;
        o.FaultSetters[5] = f => f.CorruptStream = true;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Ready = await Farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(20), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (Farm is not null) await Farm.DisposeAsync();
    }

    public async Task<(int Exit, string Out)> FfprobeAsync(string url, int timeoutSec = 10)
    {
        var psi = new ProcessStartInfo(Ffprobe!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-v", "error", "-rtsp_transport", "tcp", "-timeout", "5000000", "-show_entries", "stream=codec_name,width,height", "-of", "json", url })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { p.Kill(true); return (-1, "timeout"); }
        return (p.ExitCode, await stdout + await stderr);
    }
}

public class LiveFarmTests : IClassFixture<LiveFarmFixture>
{
    private readonly LiveFarmFixture _fx;

    public LiveFarmTests(LiveFarmFixture fx) => _fx = fx;

    private void Require()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        Assert.True(_fx.Ready, "streams did not become ready - see " + _fx.Farm!.LogDir);
    }

    private VideoProviderInfo Info(int cam) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = _fx.Farm!.Find(cam)!.Spec.OnvifPort,
        Username = _fx.Farm.Options.User,
        Password = _fx.Farm.Options.Password,
        PreferSubStream = true,
    };

    [SkippableFact]
    public async Task should_play_h264_sub_and_main_when_ffprobe_opens_the_uri_our_client_resolved()
    {
        Require();
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        // Act — GetStreamUri through the production provider, then open exactly that URI
        var sub = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("L1", Info(1), new CancellationTokenSource(10_000).Token);
        var (subExit, subOut) = await _fx.FfprobeAsync(sub.Uri!);
        var main = _fx.Farm!.Find(2)!.Spec.MainRtspUrl.Replace("rtsp://", "rtsp://dummy:dummy1234@", StringComparison.Ordinal);
        var (mainExit, mainOut) = await _fx.FfprobeAsync(main);

        // Assert
        Assert.True(sub.Success, sub.ToString());
        Assert.EndsWith("/cam1_sub", sub.Uri);
        Assert.Equal(0, subExit);
        var s = JsonDocument.Parse(subOut[..(subOut.LastIndexOf('}') + 1)]).RootElement.GetProperty("streams")[0];
        Assert.Equal("h264", s.GetProperty("codec_name").GetString());
        Assert.Equal(320, s.GetProperty("width").GetInt32());
        Assert.Equal(0, mainExit);
        Assert.Contains("\"width\": 640", mainOut);
    }

    [SkippableFact]
    public async Task should_refuse_rtsp_when_credentials_missing_or_camera_has_auth_fail()
    {
        Require();
        var anon = await _fx.FfprobeAsync(_fx.Farm!.Find(1)!.Spec.MainRtspUrl);
        var authFail = await _fx.FfprobeAsync(_fx.Farm.Find(3)!.Spec.MainRtspUrl.Replace("rtsp://", "rtsp://dummy:dummy1234@", StringComparison.Ordinal));

        Assert.NotEqual(0, anon.Exit);
        Assert.Contains("401", anon.Out);
        Assert.NotEqual(0, authFail.Exit);
    }

    [SkippableFact]
    public async Task should_stop_publishing_and_log_it_when_kill_stream_after_elapses()
    {
        Require();
        var pub = _fx.Farm!.Find(4)!.Publisher!;
        Assert.True(await Wait.UntilAsync(() => pub.IsKilled, TimeSpan.FromSeconds(15)), "cam4 publisher was not killed");

        var url = _fx.Farm.Find(4)!.Spec.MainRtspUrl.Replace("rtsp://", "rtsp://dummy:dummy1234@", StringComparison.Ordinal);
        bool gone = false;
        for (int i = 0; i < 10 && !gone; i++)
        {
            gone = (await _fx.FfprobeAsync(url, 8)).Exit != 0;
            if (!gone) await Task.Delay(500);
        }

        Assert.True(gone, "cam4 stream still playable after kill");
        Assert.False(pub.IsRunning);
    }

    [SkippableFact]
    public async Task should_keep_the_session_up_but_damage_frames_when_corrupt_stream()
    {
        Require();
        var url = _fx.Farm!.Find(5)!.Spec.MainRtspUrl.Replace("rtsp://", "rtsp://dummy:dummy1234@", StringComparison.Ordinal);
        var psi = new ProcessStartInfo(_fx.Ffprobe!) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-v", "error", "-rtsp_transport", "tcp", "-read_intervals", "%+4", "-show_entries", "frame=pict_type", "-of", "csv", url })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var errTask = p.StandardError.ReadToEndAsync();
        var outTask = p.StandardOutput.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await p.WaitForExitAsync(cts.Token);
        var err = await errTask;
        var frames = (await outTask).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(frames > 0, "no frames decoded at all");
        Assert.False(string.IsNullOrWhiteSpace(err), "expected decoder errors from the noise bitstream filter");
    }
}
