using System.Globalization;
using DummyCameras.Infra;

namespace DummyCameras.Streams;

/// <summary>
/// ffmpeg command lines (pure - unit-testable). ffmpeg runs with the overlay folder as its working directory so the
/// drawtext files are RELATIVE names (no Windows drive-colon escaping inside the filtergraph).
/// <list type="bullet">
/// <item>No video: <c>testsrc2</c>, hue-shifted per camera, big camera number + name/PTZ text (reloaded) + clock →
/// main (H.264) and, when enabled, a scaled sub stream from one process.</item>
/// <item>Video + copy: <c>-re -stream_loop -1 -ss off -i file -c:v copy</c> (no overlay, no sub).</item>
/// <item>Video + transcode: same overlay as testsrc2 on the user's footage.</item>
/// </list>
/// Corrupt stream = <c>noise</c> bitstream filter on the encoded packets (decoders see damaged slices).
/// </summary>
public static class FfmpegArgs
{
    public const string FontFile = "font.ttf";
    public const int ReloadEveryFrames = 5;

    public static string OverlayFile(CameraSpec cam) => $"{cam.Id}.txt";

    public static IReadOnlyList<string> Build(CameraSpec cam, bool withOverlayFont)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "warning", "-nostats", "-nostdin" };
        string fps = cam.Fps.ToString(CultureInfo.InvariantCulture);

        if (cam.VideoFile is null)
        {
            a.AddRange(new[] { "-re", "-f", "lavfi", "-i", $"testsrc2=size={cam.MainWidth}x{cam.MainHeight}:rate={fps}" });
        }
        else
        {
            a.AddRange(new[] { "-re", "-stream_loop", "-1" });
            if (cam.VideoOffsetSec > 0) a.AddRange(new[] { "-ss", cam.VideoOffsetSec.ToString("0.###", CultureInfo.InvariantCulture) });
            a.AddRange(new[] { "-i", cam.VideoFile });
        }

        if (cam.VideoFile is not null && cam.CopyVideo)
        {
            a.AddRange(new[] { "-map", "0:v:0", "-c:v", "copy", "-an" });
            if (cam.Faults.CorruptStream) a.AddRange(Corrupt());
            a.AddRange(Output(cam.MainRtspUrl));
            return a;
        }

        var chain = new List<string>();
        if (cam.VideoFile is not null)
            chain.Add($"scale={cam.MainWidth}:{cam.MainHeight}:force_original_aspect_ratio=decrease,pad={cam.MainWidth}:{cam.MainHeight}:(ow-iw)/2:(oh-ih)/2,fps={fps}");
        else
            chain.Add($"hue=h={(cam.Index - 1) * 45 % 360}");
        string font = withOverlayFont ? $":fontfile={FontFile}" : string.Empty;
        int big = Math.Max(48, cam.MainHeight / 3);
        chain.Add($"drawtext=text='{cam.Index}'{font}:fontsize={big}:fontcolor=white@0.35:x=(w-tw)/2:y=(h-th)/2");
        chain.Add($"drawtext=textfile={OverlayFile(cam)}:reload={ReloadEveryFrames}{font}:fontsize={Math.Max(16, cam.MainHeight / 26)}:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=8:line_spacing=6:x=16:y=16");
        chain.Add($"drawtext=text='%{{localtime}}'{font}:fontsize={Math.Max(14, cam.MainHeight / 30)}:fontcolor=yellow:box=1:boxcolor=black@0.6:boxborderw=6:x=w-tw-16:y=h-th-16");

        string graph = "[0:v]" + string.Join(',', chain);
        graph += cam.HasSub ? $",split=2[m][s0];[s0]scale={cam.SubWidth}:{cam.SubHeight}[s]" : "[m]";
        a.AddRange(new[] { "-filter_complex", graph });

        a.AddRange(new[] { "-map", "[m]" });
        a.AddRange(Encoder(cam.Fps, "1500k"));
        if (cam.Faults.CorruptStream) a.AddRange(Corrupt());
        a.AddRange(Output(cam.MainRtspUrl));

        if (cam.HasSub)
        {
            a.AddRange(new[] { "-map", "[s]" });
            a.AddRange(Encoder(cam.Fps, "300k"));
            if (cam.Faults.CorruptStream) a.AddRange(Corrupt());
            a.AddRange(Output($"rtsp://{cam.Host}:{cam.RtspPort}/{cam.SubPath}"));
        }
        return a;
    }

    private static IEnumerable<string> Encoder(int fps, string bitrate) => new[]
    {
        "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-profile:v", "main", "-pix_fmt", "yuv420p",
        "-g", (fps * 2).ToString(CultureInfo.InvariantCulture), "-bf", "0",
        "-b:v", bitrate, "-maxrate", bitrate, "-bufsize", bitrate, "-an",
    };

    /// <summary>Flip roughly one byte in 2000 of every packet (keeps the stream "alive" but undecodable in places).</summary>
    private static IEnumerable<string> Corrupt() => new[] { "-bsf:v", "noise=amount=2000" };

    private static IEnumerable<string> Output(string url) => new[] { "-f", "rtsp", "-rtsp_transport", "tcp", url };
}
