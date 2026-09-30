using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DummyCameras.Infra;
using DummyCameras.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Xunit.Abstractions;

namespace DummyCameras.Tests;

/// <summary>
/// ONVIF-only farm (cam1 PTZ · cam2 PTZ answering 300 ms late) + the REAL camera host exe (headless).
/// The test process plays the GIS: it only sends IPC messages (DragMove / Move / Stop); the dummy's
/// ptz-commands.jsonl (timestamps · op · vector · space) is the assertion surface.
/// </summary>
public sealed class DragPtzFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;
    public string? SkipReason { get; private set; }
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Ptz { get; } = new();
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Requests { get; } = new();

    public async Task InitializeAsync()
    {
        if (CameraHostExe.Find() is null) { SkipReason = $"{CameraHostExe.ExeName} not built (or set IRONWALL_CAMHOST_EXE)"; return; }
        var o = new FarmOptions
        {
            Count = 2,
            PtzCount = 2,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            GotoSeconds = 0.4,
            OutDir = TestDirs.NewOutDir("dragptz"),
        };
        o.FaultSetters[2] = f => f.SlowOnvifMs = 300;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.PtzLog.Written += e => Ptz.Enqueue(e);
        Farm.OnvifLog.Written += e => Requests.Enqueue(e);
    }

    public async Task DisposeAsync()
    {
        if (Farm is not null) await Farm.DisposeAsync();
    }

    public VideoProviderInfo Info(int cam) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = Farm.Options.Password,
    };

    public PtzSimulator Sim(int cam) => Farm.Find(cam)!.Ptz!;

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Commands(int cam, DateTime sinceUtc)
        => Ptz.Where(e => (string?)e["cam"] == $"cam{cam}" && Ts(e) >= sinceUtc).ToList();

    public int RequestCount(int cam, DateTime sinceUtc)
        => Requests.Count(e => (string?)e["cam"] == $"cam{cam}" && Ts(e) >= sinceUtc);

    public static DateTime Ts(IReadOnlyDictionary<string, object?> e)
        => DateTime.Parse((string)e["ts"]!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static string Op(IReadOnlyDictionary<string, object?> e) => (string)e["op"]!;
}

public class DragPtzHostTests : IClassFixture<DragPtzFarmFixture>
{
    private const double Aspect = 16d / 9d;
    // 광각 60° 화면 너비의 1/4 = 15° → 일반 공간(폭 2 = 360°) 이동량
    private const double QuarterWidthPan = 0.25 * 60d / 360d * 2d;

    private readonly DragPtzFarmFixture _fx;
    private readonly ITestOutputHelper _output;

    public DragPtzHostTests(DragPtzFarmFixture fx, ITestOutputHelper output)
    {
        _fx = fx;
        _output = output;
    }

    private async Task<(CameraPopupHostSupervisor Host, CameraPopupControl Control)> StartHostAsync()
    {
        var host = new CameraPopupHostSupervisor(new CameraPopupHostOptions
        {
            HostExecutablePath = CameraHostExe.Find()!,
            Headless = true,
            HostLogDirectory = TestDirs.NewOutDir("dragptz-host"),
        }, new TestLog());
        host.Start();
        Assert.True(await Wait.UntilAsync(() => host.State == CameraPopupHostState.Running, TimeSpan.FromSeconds(20)), $"host state={host.State}");
        return (host, new CameraPopupControl(host));
    }

    /// <summary>Prepare the camera in the host and let its one background position read finish (so the drag path is warm).</summary>
    private async Task PrepareAsync(CameraPopupControl control, int cam, string id)
    {
        _fx.Sim(cam).Reset();
        var t0 = DateTime.UtcNow;
        var r = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = id, Provider = _fx.Info(cam), TimeoutMs = 20_000 });
        Assert.True(r.Success && r.PtzCapable, r.ToString());
        Assert.True(await Wait.UntilAsync(() => _fx.Requests.Any(e => (string?)e["cam"] == $"cam{cam}" && (string?)e["op"] == "GetStatus" && DragPtzFarmFixture.Ts(e) >= t0),
            TimeSpan.FromSeconds(10)), "host never read the position after prepare");
        await Task.Delay(cam == 2 ? 500 : 150);
    }

    private static double Num(object? v) => Convert.ToDouble(v, CultureInfo.InvariantCulture);

    [SkippableFact]
    public async Task should_send_exactly_one_relative_move_with_right_direction_and_size_when_video_is_dragged()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        var (host, control) = await StartHostAsync();
        using var _h = host;
        using var _c = control;
        await PrepareAsync(control, 1, "d1");

        // Act — 화면 너비의 1/4 오른쪽, 높이의 1/5 위로 끌고 뗐다(GIS 는 메시지 한 건만 보낸다)
        var t0 = DateTime.UtcNow;
        Assert.True(control.DragMove("d1", _fx.Info(1), 0.25, -0.2, Aspect));
        Assert.True(await Wait.UntilAsync(() => _fx.Commands(1, t0).Count >= 1, TimeSpan.FromSeconds(3), pollMs: 5), "drag never reached the camera");
        await Task.Delay(600);   // 뒤따르는 명령이 없는지

        // Assert — JSONL 에 이동 명령 정확히 1건(RelativeMove · 일반 상대 공간), 그 밖의 ONVIF 호출 없음
        var commands = _fx.Commands(1, t0);
        var only = Assert.Single(commands);
        Assert.Equal("RelativeMove", DragPtzFarmFixture.Op(only));
        Assert.Equal("TranslationGenericSpace", (string?)only["space"]);
        Assert.Equal(QuarterWidthPan, Num(only["pan"]), 3);
        Assert.True(Num(only["tilt"]) > 0, "위로 끌었는데 틸트가 올라가지 않았다");
        Assert.Null(only["zoom"]);
        Assert.Equal(1, _fx.RequestCount(1, t0));   // 드래그당 ONVIF 호출 1건(GetStatus · GetNode · GetProfiles 없음)
        double latencyMs = (DragPtzFarmFixture.Ts(only) - t0).TotalMilliseconds;
        Assert.InRange(latencyMs, 0, 500);
        // 도착 뒤 카메라(모의)가 요청한 만큼 갔다
        Assert.True(await Wait.UntilAsync(() => !_fx.Sim(1).Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3)));
        Assert.Equal(QuarterWidthPan, _fx.Sim(1).Status(DateTime.UtcNow).Position.Pan, 3);
        _output.WriteLine($"mouse-up → RelativeMove at camera: {latencyMs:F0} ms · pan={Num(only["pan"]):F4} tilt={Num(only["tilt"]):F4}");
    }

    [SkippableFact]
    public async Task should_not_build_a_backlog_and_end_with_the_last_drag_when_ten_drags_are_sent_rapidly()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        var (host, control) = await StartHostAsync();
        using var _h = host;
        using var _c = control;
        await PrepareAsync(control, 2, "d2");   // 응답이 300 ms 늦는 카메라 — 드래그가 몰리는 조건

        // Act — 20 ms 간격 드래그 10번(마지막 = 화면 너비의 1/4 왼쪽)
        var t0 = DateTime.UtcNow;
        for (int i = 1; i <= 10; i++)
        {
            Assert.True(control.DragMove("d2", _fx.Info(2), i == 10 ? -0.25 : i * 0.02, 0, Aspect));
            await Task.Delay(20);
        }
        Assert.True(await Wait.UntilAsync(() => _fx.Commands(2, t0).Any(c => Math.Abs(Num(c["pan"]) + QuarterWidthPan) < 1e-3), TimeSpan.FromSeconds(5)),
            "마지막 드래그가 카메라에 닿지 않았다");
        await Task.Delay(1200);   // 밀린 명령이 뒤늦게 나오지 않는지

        // Assert — 10건이 줄 서지 않는다: 이미 나간 것 + 마지막(최대 3건), 마지막 명령 = 마지막 드래그
        var commands = _fx.Commands(2, t0);
        Assert.All(commands, c => Assert.Equal("RelativeMove", DragPtzFarmFixture.Op(c)));
        Assert.InRange(commands.Count, 1, 3);
        Assert.Equal(-QuarterWidthPan, Num(commands[^1]["pan"]), 3);
        _output.WriteLine($"10 rapid drags → {commands.Count} command(s) at camera: {string.Join(", ", commands.Select(c => Num(c["pan"]).ToString("F4", CultureInfo.InvariantCulture)))}");
    }

    [SkippableFact]
    public async Task should_keep_moving_while_held_and_stop_at_once_when_button_is_released()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        var (host, control) = await StartHostAsync();
        using var _h = host;
        using var _c = control;
        await PrepareAsync(control, 1, "h1");

        // Act — 누름(이동 1건) → 3.2초 유지 → 뗌(정지 1건) → 1.8초 더 본다
        var t0 = DateTime.UtcNow;
        Assert.True(control.Move("h1", _fx.Info(1), 0.3, 0, 0));
        await Task.Delay(3200);
        var movingBeforeRelease = _fx.Sim(1).Status(DateTime.UtcNow).IsMoving;
        var released = DateTime.UtcNow;
        Assert.True(control.Stop("h1", _fx.Info(1)));
        Assert.True(await Wait.UntilAsync(() => _fx.Commands(1, t0).Any(c => DragPtzFarmFixture.Op(c) == "Stop"), TimeSpan.FromSeconds(3), pollMs: 5), "정지가 카메라에 닿지 않았다");
        await Task.Delay(1800);

        // Assert
        var commands = _fx.Commands(1, t0);
        var moves = commands.Where(c => DragPtzFarmFixture.Op(c) == "ContinuousMove").ToList();
        var stop = Assert.Single(commands, c => DragPtzFarmFixture.Op(c) == "Stop");
        Assert.True(movingBeforeRelease, "누르는 동안 카메라가 멈췄다(2초 안전 제한에 걸림 — 유지 재전송 실패)");
        Assert.InRange(moves.Count, 4, 5);                                     // 0 · 1 · 2 · 3초 (재전송 1초마다)
        Assert.All(moves, m => Assert.Equal("PT2S", (string?)m["timeout"]));   // 재전송이 끊기면 2초 안에 스스로 멈춘다
        Assert.Equal("Stop", DragPtzFarmFixture.Op(commands[^1]));             // 뗀 뒤에는 이동이 다시 나가지 않는다
        Assert.False(_fx.Sim(1).Status(DateTime.UtcNow).IsMoving);
        double stopMs = (DragPtzFarmFixture.Ts(stop) - released).TotalMilliseconds;
        Assert.InRange(stopMs, 0, 500);
        _output.WriteLine($"held 3.2 s → {moves.Count} ContinuousMove(PT2S) · release → Stop at camera in {stopMs:F0} ms");
    }

    [SkippableFact]
    public async Task should_stop_by_itself_within_two_seconds_when_host_dies_while_button_is_held()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        var (host, control) = await StartHostAsync();
        await PrepareAsync(control, 1, "k1");

        // Act — 누른 채로 호스트가 사라진다(정지는 끝내 가지 않는다)
        var t0 = DateTime.UtcNow;
        Assert.True(control.Move("k1", _fx.Info(1), 0.3, 0, 0));
        Assert.True(await Wait.UntilAsync(() => _fx.Commands(1, t0).Count >= 2, TimeSpan.FromSeconds(4)), "유지 재전송이 없었다");
        control.Dispose();
        host.Dispose();
        var died = DateTime.UtcNow;
        var lastMove = _fx.Commands(1, t0).Where(c => DragPtzFarmFixture.Op(c) == "ContinuousMove").Max(DragPtzFarmFixture.Ts);

        // Assert — 마지막 재전송 뒤 2초(+여유) 안에 스스로 멈춘다. 정지 명령은 없다.
        Assert.True(await Wait.UntilAsync(() => !_fx.Sim(1).Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(4)), "호스트가 죽었는데 카메라가 계속 돈다");
        double afterLastMoveMs = (DateTime.UtcNow - lastMove).TotalMilliseconds;
        Assert.InRange(afterLastMoveMs, 0, 2600);
        await Task.Delay(1200);
        Assert.DoesNotContain(_fx.Commands(1, died), c => DragPtzFarmFixture.Op(c) == "ContinuousMove");
        _output.WriteLine($"host gone while held → camera idle {afterLastMoveMs:F0} ms after the last resend (no Stop needed)");
    }

    /// <summary>
    /// 전/후 실측(단언은 최소한 — 숫자를 남기는 시험). "전" = 이전 GIS 코드 그대로: ContinuousMove → 드래그 길이 비례 대기 → Stop.
    /// "후" = DragMove 한 건. 같은 드래그(화면 너비의 1/4 오른쪽)를 광각 · 최대 줌에서 각각 5번.
    /// 결과는 <c>IRONWALL_DRAGPTZ_RESULTS</c>(파일 경로)가 있으면 JSON 으로도 쓴다.
    /// </summary>
    [SkippableFact]
    public async Task should_measure_old_timed_continuous_drag_against_new_single_relative_move()
    {
        Skip.If(_fx.SkipReason is not null, _fx.SkipReason);
        var (host, control) = await StartHostAsync();
        using var _h = host;
        using var _c = control;
        await PrepareAsync(control, 1, "m1");
        var info = _fx.Info(1);
        var sim = _fx.Sim(1);
        var rows = new List<Dictionary<string, object>>();

        foreach (double zoom in new[] { 0.0, 1.0 })
        {
            // 줌을 맞추고(다른 자리가 바꾼 것처럼) 호스트가 멈춘 자리를 다시 읽게 한다(정지 → 배경 위치 읽기)
            sim.AbsoluteMove(new PtzVector(0, 0, zoom), DateTime.UtcNow);
            await Task.Delay(700);
            control.Stop("m1", info);
            await Task.Delay(900);
            double wanted = 0.25 * Hfov(zoom) / 360d * 2d;   // 끈 점이 화면 중심으로 오려면 가야 하는 팬

            foreach (string mode in new[] { "old", "new" })
            {
                var latency = new List<double>();
                var calls = new List<int>();
                var moved = new List<double>();
                var done = new List<double>();
                for (int i = 0; i < 5; i++)
                {
                    sim.AbsoluteMove(new PtzVector(0, 0, zoom), DateTime.UtcNow);
                    await Task.Delay(600);
                    var t0 = DateTime.UtcNow;
                    var sw = Stopwatch.StartNew();
                    if (mode == "old")
                    {
                        // 이전 MapViewModel.HandlePtzDragAsync: 384×258 상자에서 96 px(너비의 1/4) 오른쪽
                        const double w = 384, h = 258, dx = 96;
                        double maxLen = Math.Max(60.0, Math.Min(w, h) * 0.65), mag = Math.Min(1.0, dx / maxLen);
                        control.Move("m1", info, 0.6 * (0.3 + 0.7 * mag), 0, 0);
                        await Task.Delay(Math.Max(120, (int)(mag * 700)));
                        control.Stop("m1", info);
                    }
                    else
                    {
                        control.DragMove("m1", info, 0.25, 0, Aspect);
                    }
                    await Wait.UntilAsync(() => _fx.Commands(1, t0).Count >= (mode == "old" ? 2 : 1), TimeSpan.FromSeconds(3), pollMs: 5);
                    await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3), pollMs: 10);
                    done.Add(sw.Elapsed.TotalMilliseconds);
                    var commands = _fx.Commands(1, t0);
                    latency.Add((DragPtzFarmFixture.Ts(commands[0]) - t0).TotalMilliseconds);
                    calls.Add(commands.Count);
                    moved.Add(sim.Status(DateTime.UtcNow).Position.Pan);
                    await Task.Delay(500);   // 호스트의 배경 위치 읽기가 끝나게(다음 측정과 섞이지 않게)
                }
                double avgMoved = moved.Average();
                var row = new Dictionary<string, object>
                {
                    ["zoom"] = zoom,
                    ["mode"] = mode,
                    ["latencyMsAvg"] = Math.Round(latency.Average(), 1),
                    ["latencyMsMax"] = Math.Round(latency.Max(), 1),
                    ["commandsPerDrag"] = calls.Average(),
                    ["wantedPan"] = Math.Round(wanted, 5),
                    ["movedPanAvg"] = Math.Round(avgMoved, 5),
                    ["errorRatio"] = Math.Round(avgMoved / wanted, 2),
                    ["doneMsAvg"] = Math.Round(done.Average(), 0),
                };
                rows.Add(row);
                _output.WriteLine(string.Join(" · ", row.Select(kv => $"{kv.Key}={Convert.ToString(kv.Value, CultureInfo.InvariantCulture)}")));
            }
        }

        var path = Environment.GetEnvironmentVariable("IRONWALL_DRAGPTZ_RESULTS");
        if (!string.IsNullOrWhiteSpace(path)) File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));

        // 새 방식: 드래그당 명령 1건, 광각 · 망원 모두 목표의 ±5% 안
        Assert.All(rows.Where(r => (string)r["mode"] == "new"), r =>
        {
            Assert.Equal(1d, (double)r["commandsPerDrag"]);
            Assert.InRange((double)r["errorRatio"], 0.95, 1.05);
        });
    }

    private static double Hfov(double zoom)
    {
        double ratio = 1 + zoom * 29;
        return 2 * Math.Atan(Math.Tan(Math.PI / 6) / ratio) * 180 / Math.PI;
    }
}
