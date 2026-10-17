using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// 루프백 RTSP 원본 — MediaMTX(127.0.0.1 에만 묶음) + ffmpeg 시험 무늬(H.264) 게시자. 영상 디코딩 경로를 실제 RTSP 로 시험한다(FW).
/// mediamtx.exe: <c>IRONWALL_MEDIAMTX</c> → 상위 폴더들의 <c>tools\dummy-cameras\bin\mediamtx\*\mediamtx.exe</c>(dummy-cameras 가 받아 둔 것).
/// ffmpeg.exe: PATH. 둘 중 하나라도 없으면 <see cref="TryStartAsync"/> 가 null(시험은 건너뛴다).
/// </summary>
internal sealed class LoopbackRtspServer : IDisposable
{
    private readonly Process _mediaMtx;
    private readonly Process _ffmpeg;

    private LoopbackRtspServer(Process mediaMtx, Process ffmpeg, string url)
    {
        _mediaMtx = mediaMtx;
        _ffmpeg = ffmpeg;
        Url = url;
    }

    public string Url { get; }

    public static string? FindMediaMtx()
    {
        var env = Environment.GetEnvironmentVariable("IRONWALL_MEDIAMTX");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var bin = Path.Combine(dir.FullName, "tools", "dummy-cameras", "bin", "mediamtx");
            if (Directory.Exists(bin) && Directory.EnumerateFiles(bin, "mediamtx.exe", SearchOption.AllDirectories).FirstOrDefault() is { } exe)
                return exe;
        }
        return null;
    }

    public static string? FindFfmpeg()
        => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d.Trim('"'), "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);

    /// <param name="udpOnly">RTSP 를 UDP 로만 내주는 서버(TCP SETUP 에 461) — UDP 만 되는 카메라 흉내.</param>
    public static async Task<LoopbackRtspServer?> TryStartAsync(string workDir, bool udpOnly = false)
    {
        if (FindMediaMtx() is not { } mediaMtxExe || FindFfmpeg() is not { } ffmpegExe) return null;
        int rtsp = FreePort(), rtmp = FreePort(), rtp = FreePort() & ~1;   // MediaMTX: RTP 포트는 짝수(RTCP = +1)
        var config = Path.Combine(workDir, "mediamtx.yml");
        File.WriteAllText(config, string.Join("\n",
            "logLevel: warn",
            "authMethod: internal",
            "authInternalUsers:",
            "  - user: any",
            "    pass:",
            "    ips: []",
            "    permissions:",
            "      - action: publish",
            "      - action: read",
            "api: false",
            "metrics: false",
            "pprof: false",
            "playback: false",
            "rtsp: true",
            udpOnly ? "rtspTransports: [udp]" : "rtspTransports: [udp, tcp]",
            "rtspEncryption: \"no\"",
            $"rtspAddress: 127.0.0.1:{rtsp}",
            $"rtpAddress: 127.0.0.1:{rtp}",
            $"rtcpAddress: 127.0.0.1:{rtp + 1}",
            "rtmp: true",   // 게시는 RTMP(TCP) — UDP 전용 RTSP 서버에서도 게시자가 UDP 를 열지 않게
            "rtmpEncryption: \"no\"",
            $"rtmpAddress: 127.0.0.1:{rtmp}",
            "hls: false",
            "webrtc: false",
            "srt: false",
            "moq: false",
            "paths:",
            "  all_others:",
            ""));
        var mediaMtx = Start(mediaMtxExe, $"\"{config}\"", workDir);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!CanConnect(rtsp) || !CanConnect(rtmp))
        {
            if (DateTime.UtcNow > deadline || mediaMtx.HasExited)
            {
                Kill(mediaMtx);
                return null;
            }
            await Task.Delay(100);
        }
        var ffmpeg = Start(ffmpegExe,
            "-hide_banner -loglevel error -re -f lavfi -i testsrc2=size=640x360:rate=15 -c:v libx264 -preset ultrafast -tune zerolatency " +
            $"-g 15 -pix_fmt yuv420p -f flv rtmp://127.0.0.1:{rtmp}/fw", workDir);
        var server = new LoopbackRtspServer(mediaMtx, ffmpeg, $"rtsp://127.0.0.1:{rtsp}/fw");
        // 게시가 붙을 때까지(DESCRIBE 200) — 붙기 전에 열면 404 로 실패한다.
        deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await server.IsPublishedAsync())
        {
            if (DateTime.UtcNow > deadline)
            {
                server.Dispose();
                return null;
            }
            await Task.Delay(200);
        }
        return server;
    }

    private async Task<bool> IsPublishedAsync()
    {
        try
        {
            var uri = new Uri(Url);
            using var c = new TcpClient();
            await c.ConnectAsync(IPAddress.Loopback, uri.Port);
            var s = c.GetStream();
            await s.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"DESCRIBE {Url} RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n"));
            var buf = new byte[64];
            int n = await s.ReadAsync(buf).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            return System.Text.Encoding.ASCII.GetString(buf, 0, n).StartsWith("RTSP/1.0 200", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException) { return false; }
    }

    private static Process Start(string exe, string args, string workDir)
    {
        var log = Path.Combine(workDir, Path.GetFileNameWithoutExtension(exe) + ".log");
        var p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workDir, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        })!;
        void Append(object _, DataReceivedEventArgs e) { if (e.Data is not null) lock (log) File.AppendAllText(log, e.Data + Environment.NewLine); }
        p.OutputDataReceived += Append;
        p.ErrorDataReceived += Append;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; }
        finally { l.Stop(); }
    }

    private static bool CanConnect(int port)
    {
        try
        {
            using var c = new TcpClient();
            c.Connect(IPAddress.Loopback, port);
            return true;
        }
        catch (SocketException) { return false; }
    }

    private static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
        p.Dispose();
    }

    public void Dispose()
    {
        Kill(_ffmpeg);
        Kill(_mediaMtx);
    }
}
