using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Xunit.Abstractions;
using PixelRect = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages.PixelRect;

namespace DummyCameras.Tests;

/// <summary>
/// 스트리밍 단계별 측정(IRONWALL_STREAM_TRACE): 실제 호스트 exe(헤드리스 — 창 없음) + 가짜 카메라(960x540 · 12fps)로
/// 스트림 1 · 4 · 8 · 16 · 60 개를 차례로 열고, 호스트가 5초마다 남기는 TOTAL 요약 줄을 평균 내 표로 낸다.
/// 선택 실행(수 분 · CPU 많이 씀): <c>IRONWALL_STREAM_TRACE_LOAD=1</c>.
/// <list type="bullet">
/// <item><c>IRONWALL_STREAM_TRACE_COUNTS</c> — 스트림 수 목록(기본 <c>1,4,8,16,60</c>).</item>
/// <item><c>IRONWALL_STREAM_TRACE_SECONDS</c> — 모두 재생된 뒤 재는 시간(기본 30초).</item>
/// <item><c>IRONWALL_STREAM_TRACE_OUT</c> — 결과 표(markdown)를 쓸 파일.</item>
/// <item><c>IRONWALL_STREAM_TRACE_MEDIAMTX</c> — mediamtx.exe 경로(워크트리에 내려받지 않았을 때).</item>
/// <item><c>IRONWALL_STREAM_TRACE_LOGDIR</c> — 호스트 로그 · 계측 파일 폴더(기본: 임시 폴더).</item>
/// </list>
/// </summary>
public class StreamTraceLoadTests
{
    private const int TilesPerWindow = 6;
    private readonly ITestOutputHelper _output;

    public StreamTraceLoadTests(ITestOutputHelper output) => _output = output;

    [SkippableFact]
    [Trait("Category", "Load")]
    public async Task should_report_stage_metrics_when_streams_play_through_the_host()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_LOAD") == "1", "opt-in: IRONWALL_STREAM_TRACE_LOAD=1");
        Skip.If(ToolPaths.FindOnPath("ffmpeg") is null, "ffmpeg not on PATH");
        var mediaMtx = Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_MEDIAMTX") is { Length: > 0 } m ? m : MediaMtxInstaller.ExePath;
        Skip.IfNot(File.Exists(mediaMtx), "MediaMTX not installed - run: DummyCameras fetch-mediamtx");
        var exe = CameraHostExe.Find();
        Skip.If(exe is null, $"{CameraHostExe.ExeName} not built");
        var counts = (Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_COUNTS") ?? "1,4,8,16,60")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();
        int seconds = int.TryParse(Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_SECONDS"), out var s) ? s : 30;

        // 준비 — 가짜 카메라 8대, 본 · 보조 스트림 모두 960x540 · 12fps
        var options = new FarmOptions
        {
            Count = 8,
            PtzCount = 0,
            OnvifBasePort = 0,
            ControlPort = 0,
            RtspPort = 18764,
            RtpPort = 18310,
            MediaMtxApiPort = 19807,
            Width = 960, Height = 540, SubWidth = 960, SubHeight = 540, Fps = 12,
            MediaMtxPath = mediaMtx,
            OutDir = TestDirs.NewOutDir("trace"),
        };
        await using var farm = await Farm.StartAsync(options, TextWriter.Null, CancellationToken.None);
        Assert.True(await farm.WaitStreamsReadyAsync(TimeSpan.FromSeconds(40), CancellationToken.None), "farm streams not ready - see " + farm.LogDir);

        // 호스트는 이 프로세스의 환경변수를 물려받는다 → 계측 켜짐.
        var previous = Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE");
        Environment.SetEnvironmentVariable("IRONWALL_STREAM_TRACE", "1");
        var hostLogDir = Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_LOGDIR") is { Length: > 0 } logDir ? logDir : TestDirs.NewOutDir("trace-host");
        var rows = new List<(int Count, int Playing, Dictionary<string, double> Avg)>();
        var transitions = new ConcurrentQueue<string>();
        try
        {
            var last = new ConcurrentDictionary<string, StreamState>(StringComparer.Ordinal);
            using var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions { HostExecutablePath = exe!, Headless = true, HostLogDirectory = hostLogDir }, new TestLog());
            host.StatusReceived += (_, e) =>
            {
                if (e.Message is StreamStateChanged msg && msg.EventKey is not null) last[msg.StreamId] = msg.State;
            };
            host.StateChanged += (_, e) => transitions.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {e.OldState}->{e.NewState} {e.Reason}");
            host.Start();
            Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");
            var tracePath = Path.Combine(hostLogDir, $"camera-stream-trace-{DateTime.Now:yyyyMMdd}.log");
            var settings = new CameraPopupSettings().Normalize();

            foreach (int count in counts)
            {
                last.Clear();
                var opens = BuildWindows(count, farm, options, settings);
                foreach (var open in opens) host.OpenEventWindow(open);
                int Playing() => last.Values.Count(v => v == StreamState.Playing);
                await Wait.UntilAsync(() => Playing() == count, TimeSpan.FromSeconds(60), 200);
                await Task.Delay(TimeSpan.FromSeconds(6)); // 첫 구간(연결 중이 섞인 요약)을 건너뛴다
                long offset = File.Exists(tracePath) ? new FileInfo(tracePath).Length : 0;
                await Task.Delay(TimeSpan.FromSeconds(seconds));
                var totals = ReadTotals(tracePath, offset);
                rows.Add((count, Playing(), Average(totals)));
                foreach (var open in opens) host.CloseEventWindow(open.EventKey, EventWindowCloseReason.Evicted, returnHome: false);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(CameraPopupHostState.Running, host.State);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IRONWALL_STREAM_TRACE", previous);
        }

        var table = Table(rows) + Environment.NewLine + "호스트 상태 전이:" + Environment.NewLine + string.Join(Environment.NewLine, transitions);
        _output.WriteLine(table);
        _output.WriteLine("host log: " + hostLogDir);
        if (Environment.GetEnvironmentVariable("IRONWALL_STREAM_TRACE_OUT") is { Length: > 0 } outFile) File.WriteAllText(outFile, table, new UTF8Encoding(true));
        foreach (var row in rows) Assert.Equal(row.Count, row.Playing);
    }

    private static List<OpenEventWindow> BuildWindows(int count, Farm farm, FarmOptions options, CameraPopupSettings settings)
    {
        var opens = new List<OpenEventWindow>();
        int windows = (count + TilesPerWindow - 1) / TilesPerWindow;
        for (int w = 0; w < windows; w++)
        {
            var open = new OpenEventWindow
            {
                Kind = EventWindowKind.Detection,
                EventId = $"trace-{count}-{w}",
                Header = EventWindowPlanning.BuildHeader(EnumEventType.Intrusion, "구역-TRACE", $"센서 {w}", DateTime.Now),
                GridColumns = 3,
                GridRows = 2,
                Window = new PixelRect { X = 40 + w * 24, Y = 40 + w * 24, Width = 800, Height = 500 },
                TimerCloseSeconds = 0,
            };
            for (int t = 0; t < TilesPerWindow && w * TilesPerWindow + t < count; t++)
            {
                int cam = (w + t) % options.Count + 1;
                var device = new CameraDeviceModel(cam)
                {
                    DeviceName = $"DUMMY-CAM{cam}",
                    DeviceType = EnumDeviceType.IpCamera,
                    Category = EnumCameraType.FIXED,
                    IpAddress = "127.0.0.1",
                    IpPort = farm.Find(cam)!.Spec.OnvifPort,
                    UserName = options.User,
                    UserPassword = options.Password,
                };
                open.Cameras.Add(EventWindowPlanning.BuildCamera(device, new MappingCameraEntry(cam, cam, t + 1, 0, true), settings, ptzAllowed: false));
            }
            opens.Add(open);
        }
        return opens;
    }

    private static List<string> ReadTotals(string path, long offset)
    {
        if (!File.Exists(path)) return new List<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Split('\n').Where(l => l.Contains(" TOTAL ", StringComparison.Ordinal)).ToList();
    }

    // ">1234ms(pending)" 도 숫자로 읽는다(아직 처리 안 된 탐침 = 그만큼 이상 기다림).
    private static readonly Regex Number = new(@"^>?(?<n>-?\d+(\.\d+)?)", RegexOptions.Compiled);

    /// <summary>
    /// TOTAL 줄 → 키(구간.이름) = 값. 구간은 " | " 로 나뉘고 구간 첫 단어(의 = 앞)가 구간 이름(첫 구간은 total),
    /// "avg" · "max" 앞의 = 없는 단어는 그 둘의 앞이름이다(예: <c>dispatcher.wait.avg</c>).
    /// </summary>
    internal static Dictionary<string, double> ParseTotal(string line)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        int start = line.IndexOf("TOTAL ", StringComparison.Ordinal);
        if (start < 0) return map;
        var sections = line[(start + 6)..].Trim().Split(" | ");
        for (int i = 0; i < sections.Length; i++)
        {
            var tokens = sections[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;
            string name = i == 0 ? "total" : tokens[0].Split('=')[0];
            string prefix = "";
            for (int t = 0; t < tokens.Length; t++)
            {
                int eq = tokens[t].IndexOf('=');
                if (eq < 0)
                {
                    if (t > 0) prefix = tokens[t];
                    continue;
                }
                var key = tokens[t][..eq];
                var match = Number.Match(tokens[t][(eq + 1)..]);
                if (!match.Success) continue;
                string full = key is "avg" or "max" && prefix.Length > 0 ? $"{name}.{prefix}.{key}" : $"{name}.{key}";
                map[full] = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
            }
        }
        return map;
    }

    private static Dictionary<string, double> Average(List<string> lines)
    {
        var parsed = lines.Select(ParseTotal).Where(m => m.Count > 0).ToList();
        var avg = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var key in parsed.SelectMany(m => m.Keys).Distinct())
        {
            var values = parsed.Where(m => m.ContainsKey(key)).Select(m => m[key]).ToList();
            avg[key] = key.EndsWith(".max", StringComparison.Ordinal) ? values.Max() : values.Average();
        }
        avg["samples"] = parsed.Count;
        return avg;
    }

    private static readonly (string Key, string Title)[] Columns =
    {
        ("total.cpu", "CPU%"),
        ("rtsp.play→es", "RTSP 협상(play→ES) ms"),
        ("rtsp.first-frame", "첫 프레임 ms"),
        ("rtsp.queue", "줄 대기 ms"),
        ("rtp.in", "RTP 입력 kbps"),
        ("nal.demux", "NAL 디먹스 kbps"),
        ("nal.corrupt", "깨짐"),
        ("nal.discont", "끊김"),
        ("decode.decode", "디코드 fps"),
        ("decode.lock→display.avg", "lock→display 평균 ms"),
        ("decode.lock→display.max", "lock→display 최대 ms"),
        ("vout.displayed", "표시 fps"),
        ("vout.lost", "버린 그림/5s"),
        ("vout.callback.avg", "콜백 평균 ms"),
        ("sink.copy.avg", "싱크 복사 평균 ms"),
        ("dispatcher.flush", "디스패처 그리기/s"),
        ("dispatcher.wait.avg", "넘김 대기 평균 ms"),
        ("dispatcher.wait.max", "넘김 대기 최대 ms"),
        ("dispatcher.coalesced", "합쳐 버린 프레임/s"),
        ("render.writepixels.avg", "WritePixels 평균 ms"),
        ("render.ui", "UI 스레드 점유 %"),
        ("ui-probe.send", "UI 탐침 Send ms"),
        ("ui-probe.normal", "UI 탐침 Normal ms"),
        ("ui-probe.render", "UI 탐침 Render ms"),
        ("ui-probe.background", "UI 탐침 Background ms"),
        ("samples", "표본 수"),
    };

    private static string Table(List<(int Count, int Playing, Dictionary<string, double> Avg)> rows)
    {
        var sb = new StringBuilder();
        sb.Append("| 스트림 | 재생 | ").AppendJoin(" | ", Columns.Select(c => c.Title)).AppendLine(" |");
        sb.Append("|---|---|").AppendJoin("", Columns.Select(_ => "---|")).AppendLine();
        foreach (var row in rows)
        {
            sb.Append(CultureInfo.InvariantCulture, $"| {row.Count} | {row.Playing} | ");
            sb.AppendJoin(" | ", Columns.Select(c => row.Avg.TryGetValue(c.Key, out var v) ? v.ToString("0.##", CultureInfo.InvariantCulture) : "-"));
            sb.AppendLine(" |");
        }
        return sb.ToString();
    }
}
