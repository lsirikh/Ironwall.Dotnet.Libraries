using System.Collections.Concurrent;
using System.Diagnostics;
using DummyCameras.Infra;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;

namespace DummyCameras.Tests;

/// <summary>
/// 카메라 팝업이 쓰는 ONVIF 기능 전수 시나리오(2026-10-22 "팝업 ONVIF 철저 확인").
/// cam1·cam2 PTZ · cam3 PTZ 시계 +3시간 · cam4 PTZ 무응답↔복구 전환용 · cam5 PTZ 계정 거부↔복구 전환용 ·
/// cam6 PTZ 첫 프로파일에 PTZ 구성 없음 · cam7 고정.
/// 모두 실제 클라이언트 경로(CameraProviderRegistry → OnvifPtzProvider · OnvifVideoProvider → PtzController → OnvifSolution)로 돈다.
/// </summary>
public sealed class PopupOnvifFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Requests { get; } = new();
    public ConcurrentQueue<IReadOnlyDictionary<string, object?>> Ptz { get; } = new();

    public async Task InitializeAsync()
    {
        var o = new FarmOptions
        {
            Count = 7,
            PtzCount = 6,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            GotoSeconds = 0.6,
            OutDir = TestDirs.NewOutDir("popup"),
        };
        o.FaultSetters[3] = f => f.ClockSkewSec = 3 * 3600;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
        Farm.Find(6)!.Spec.FirstProfileWithoutPtz = true;
        Farm.OnvifLog.Written += e => Requests.Enqueue(e);
        Farm.PtzLog.Written += e => Ptz.Enqueue(e);
    }

    public async Task DisposeAsync() => await Farm.DisposeAsync();

    public VideoProviderInfo Info(int cam, string? password = null) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = password ?? Farm.Options.Password,
    };

    public List<IReadOnlyDictionary<string, object?>> PtzOf(string cam) => Ptz.Where(e => (string?)e["cam"] == cam).ToList();
    public List<IReadOnlyDictionary<string, object?>> RequestsOf(string cam) => Requests.Where(e => (string?)e["cam"] == cam).ToList();
}

public class PopupOnvifScenarioTests : IClassFixture<PopupOnvifFarmFixture>
{
    private readonly PopupOnvifFarmFixture _fx;

    public PopupOnvifScenarioTests(PopupOnvifFarmFixture fx) => _fx = fx;

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private TestLog _log = new();

    /// <summary>실패 메시지에 붙이는 클라이언트 로그(PtzController 의 판단 · 시간).</summary>
    private string Log => string.Join(Environment.NewLine, _log.Lines);

    private ICameraPtzProvider NewPtz(out CameraProviderRegistry registry)
    {
        _log = new TestLog();
        registry = CameraProviderRegistry.CreateDefault(_log);
        return registry.GetPtz(VideoProviderKind.Onvif)!;
    }

    private static bool Ok(IReadOnlyDictionary<string, object?> e) => ((string?)e["result"] ?? "").Split(' ')[0] == "ok";

    // ── 이미징(영상 옵션 탭: 주야간 · 자동 초점 · 수동 초점) ─────────────────────────

    [Fact]
    public async Task should_read_and_write_day_night_and_auto_focus_preserving_the_other_field_when_imaging_tab()
    {
        var ptz = NewPtz(out _);
        var onvif = _fx.Farm.Find(1)!.Onvif;

        var ready = await ptz.PrepareAsync("img1", _fx.Info(1), Within(10_000));
        var before = await ptz.GetImagingAsync("img1", Within(5_000));
        var setDay = await ptz.SetIrCutFilterAsync("img1", "ON", Within(5_000));
        var afterDay = onvif.ImagingState;
        var setManual = await ptz.SetAutoFocusAsync("img1", false, Within(5_000));
        var afterManual = onvif.ImagingState;
        var read = await ptz.GetImagingAsync("img1", Within(5_000));
        var badMode = await ptz.SetIrCutFilterAsync("img1", "DUSK", Within(5_000));
        await ptz.SetIrCutFilterAsync("img1", "AUTO", Within(5_000));
        await ptz.SetAutoFocusAsync("img1", true, Within(5_000));

        Assert.True(ready.ImagingCapable);
        Assert.Equal(new CameraImagingStateView("AUTO", true), View(before));
        Assert.True(setDay);
        Assert.Equal(("ON", true), (afterDay.IrCutFilter, afterDay.AutoFocus));     // 초점은 그대로(read-modify-write)
        Assert.True(setManual);
        Assert.Equal(("ON", false), (afterManual.IrCutFilter, afterManual.AutoFocus)); // 주야간은 그대로
        Assert.Equal(new CameraImagingStateView("ON", false), View(read));
        Assert.False(badMode);                                                        // 모르는 값은 카메라에 보내지 않는다
        Assert.DoesNotContain(_fx.PtzOf("cam1"), e => (string?)e["op"] == "Imaging.SetImagingSettings" && !Ok(e));
        ptz.Release("img1");
    }

    private sealed record CameraImagingStateView(string IrCutFilter, bool AutoFocus);

    private static CameraImagingStateView? View(Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif.CameraImagingState? s)
        => s is null ? null : new(s.IrCutFilter, s.AutoFocus);

    [Theory]
    [InlineData(1, 0.5)]    // 원경(far) — 팝업 기본 0.7 을 카메라 범위 ±0.5 로 줄인다(GetMoveOptions)
    [InlineData(-1, -0.5)]  // 근경(near)
    public async Task should_move_focus_within_camera_speed_range_and_stop_on_release_when_manual_focus(int direction, double expected)
    {
        var ptz = NewPtz(out _);
        var onvif = _fx.Farm.Find(2)!.Onvif;
        await ptz.PrepareAsync("foc2", _fx.Info(2), Within(10_000));

        var started = await ptz.StartFocusAsync("foc2", direction, Within(5_000));
        var moving = onvif.ImagingState.FocusSpeed;
        await ptz.StopFocusAsync("foc2", Within(5_000));
        var stopped = onvif.ImagingState.FocusSpeed;

        Assert.True(started);
        Assert.NotNull(moving);
        Assert.Equal(expected, moving!.Value, 3);
        Assert.Null(stopped);
        Assert.DoesNotContain(_fx.PtzOf("cam2"), e => ((string?)e["op"] ?? "").StartsWith("Imaging.", StringComparison.Ordinal) && !Ok(e));
        ptz.Release("foc2");
    }

    [Fact]
    public async Task should_offer_imaging_but_refuse_ptz_quickly_when_fixed_camera()
    {
        var ptz = NewPtz(out _);

        var ready = await ptz.PrepareAsync("fix6", _fx.Info(7), Within(10_000));
        var sw = Stopwatch.StartNew();
        var moved = await ptz.ContinuousMoveAsync("fix6", 1, 0, 0, Within(5_000));
        var presets = await ptz.GetPresetsAsync("fix6", Within(5_000));
        var goneTo = await ptz.GotoPresetAsync("fix6", "1", Within(5_000));
        var home = await ptz.GotoHomeAsync("fix6", Within(5_000));
        var saved = await ptz.SetPresetAsync("fix6", "x", Within(5_000));
        await ptz.StopAsync("fix6", Within(5_000));
        var refusedMs = sw.ElapsedMilliseconds;
        var imaging = await ptz.GetImagingAsync("fix6", Within(5_000));

        Assert.True(ready.Connected);
        Assert.False(ready.PtzCapable);         // 팝업은 PTZ 단추를 끈다
        Assert.True(ready.ImagingCapable);      // 고정 카메라도 주야간 · 초점은 된다
        Assert.False(moved);
        Assert.Null(presets);
        Assert.False(goneTo);
        Assert.False(home);
        Assert.False(saved);
        Assert.True(refusedMs < 200, $"PTZ refusals took {refusedMs} ms (no SOAP should be sent)");
        Assert.DoesNotContain(_fx.PtzOf("cam7"), e => !((string?)e["op"] ?? "").StartsWith("Imaging.", StringComparison.Ordinal));
        Assert.NotNull(imaging);
        ptz.Release("fix6");
    }

    // ── PTZ 연속 이동 8방향 · 줌 · 정지 ───────────────────────────────────────────

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(-1, -1, 0)]
    [InlineData(0, 0, 1)]    // 줌 인
    [InlineData(0, 0, -1)]   // 줌 아웃
    public async Task should_send_velocity_in_pressed_direction_and_stop_both_axes_without_one_second_stall_when_pad_pressed(double pan, double tilt, double zoom)
    {
        var ptz = NewPtz(out _);
        var sim = _fx.Farm.Find(1)!.Ptz!;
        sim.Reset();
        if (zoom < 0) sim.AbsoluteMove(new DummyCameras.Ptz.PtzVector(0, 0, 0.8), DateTime.UtcNow.AddSeconds(-10));   // 줌 아웃할 여지
        Assert.True((await ptz.PrepareAsync("dir1", _fx.Info(1), Within(10_000))).PtzCapable);
        var start = sim.Status(DateTime.UtcNow).Position;
        int before = _fx.PtzOf("cam1").Count;

        var sw = Stopwatch.StartNew();
        var moved = await ptz.ContinuousMoveAsync("dir1", pan * 0.5, tilt * 0.5, zoom * 0.5, Within(5_000));
        var moveMs = sw.ElapsedMilliseconds;
        await Task.Delay(250);
        sw.Restart();
        await ptz.StopAsync("dir1", Within(5_000));
        var stopMs = sw.ElapsedMilliseconds;
        var end = sim.Status(DateTime.UtcNow);
        ptz.Release("dir1");

        Assert.True(moved);
        Assert.False(end.IsMoving);
        var mine = _fx.PtzOf("cam1").Skip(before).ToList();
        var move = Assert.Single(mine, e => (string?)e["op"] == "ContinuousMove");
        Assert.True(Ok(move), (string?)move["result"]);
        Assert.Equal(Math.Sign(pan), Math.Sign((double?)move["pan"] ?? 0));
        Assert.Equal(Math.Sign(tilt), Math.Sign((double?)move["tilt"] ?? 0));
        Assert.Equal(Math.Sign(zoom), Math.Sign((double?)move["zoom"] ?? 0));
        Assert.Equal(0.5, Math.Abs((double?)move["pan"] ?? 0) + Math.Abs((double?)move["tilt"] ?? 0) + Math.Abs((double?)move["zoom"] ?? 0) is var sum && sum > 0 ? Math.Max(Math.Abs((double?)move["pan"] ?? 0), Math.Max(Math.Abs((double?)move["tilt"] ?? 0), Math.Abs((double?)move["zoom"] ?? 0))) : 0, 3);
        Assert.Equal("VelocityGenericSpace", move["space"]);
        Assert.Equal("PT2S", move["timeout"]);
        var stop = Assert.Single(mine, e => (string?)e["op"] == "Stop");
        Assert.Equal(("true", "true"), ((string?)stop["stopPanTilt"], (string?)stop["stopZoom"]));
        Assert.True(moveMs < 500 && stopMs < 500, $"move {moveMs} ms · stop {stopMs} ms (Expect: 100-continue stall would be ~1000 ms)");
        // 실제로 그 방향으로 갔다(시뮬레이터 위치)
        if (pan != 0) Assert.Equal(Math.Sign(pan), Math.Sign(end.Position.Pan - start.Pan));
        if (tilt != 0) Assert.Equal(Math.Sign(tilt), Math.Sign(end.Position.Tilt - start.Tilt));
        if (zoom != 0) Assert.Equal(Math.Sign(zoom), Math.Sign(end.Position.Zoom - start.Zoom));
    }

    [Fact]
    public async Task should_send_stop_ahead_of_queued_moves_and_end_idle_when_release_races_presses()
    {
        var ptz = NewPtz(out _);
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();
        await ptz.PrepareAsync("race2", _fx.Info(2), Within(10_000));
        int before = _fx.PtzOf("cam2").Count;

        // 누름 다섯 번이 줄을 선 사이 뗌 — 정지가 대기 중 이동을 버리고 먼저 나가야 한다(정지 뒤에 이동이 다시 나가지 않는다)
        var moves = Enumerable.Range(0, 5).Select(i => ptz.ContinuousMoveAsync("race2", 0.3 + i * 0.1, 0, 0, Within(5_000))).ToList();
        await ptz.StopAsync("race2", Within(5_000));
        await Task.WhenAll(moves);
        await Task.Delay(700);   // 유지 재전송 간격보다 길게 — 정지 뒤 이동이 다시 나가는지 본다

        var mine = _fx.PtzOf("cam2").Skip(before).Select(e => (string?)e["op"]).Where(op => op is "ContinuousMove" or "Stop").ToList();
        Assert.False(sim.Status(DateTime.UtcNow).IsMoving);
        Assert.Equal("Stop", mine.Last());
        Assert.True(mine.Count(op => op == "ContinuousMove") <= 1, string.Join(",", mine));
        ptz.Release("race2");
    }

    // ── 프리셋 목록 · 저장 · 삭제 · 홈 ────────────────────────────────────────────

    [Fact]
    public async Task should_save_list_goto_and_remove_preset_and_set_then_goto_home_when_preset_tab()
    {
        var ptz = NewPtz(out _);
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();
        await ptz.PrepareAsync("pre2", _fx.Info(2), Within(10_000));

        // 저장 — 지금 자리를 새 이름으로(토큰은 카메라가 정한다)
        sim.AbsoluteMove(new DummyCameras.Ptz.PtzVector(0.3, -0.2, 0.4), DateTime.UtcNow.AddSeconds(-10));
        var saved = await ptz.SetPresetAsync("pre2", "북측 울타리", Within(5_000));
        var list = await ptz.GetPresetsAsync("pre2", Within(5_000));
        var mine = list?.SingleOrDefault(p => p.Name == "북측 울타리");

        // 다른 데로 갔다가 그 프리셋으로
        sim.AbsoluteMove(new DummyCameras.Ptz.PtzVector(-0.5, 0.5, 0), DateTime.UtcNow.AddSeconds(-10));
        var goneTo = mine is null ? false : await ptz.GotoPresetAsync("pre2", mine.Token, Within(5_000));
        var arrived = await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3));
        var at = sim.Status(DateTime.UtcNow).Position;

        // 홈 저장 → 다른 데로 → 홈으로
        var homeSet = await ptz.SetHomeAsync("pre2", Within(5_000));
        sim.AbsoluteMove(new DummyCameras.Ptz.PtzVector(0, 0, 0), DateTime.UtcNow.AddSeconds(-10));
        var homeGo = await ptz.GotoHomeAsync("pre2", Within(5_000));
        var homeArrived = await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3));
        var homeAt = sim.Status(DateTime.UtcNow).Position;

        // 삭제 · 없는 것 삭제
        var removed = mine is null ? false : await ptz.RemovePresetAsync("pre2", mine.Token, Within(5_000));
        var after = await ptz.GetPresetsAsync("pre2", Within(5_000));
        var removedAgain = mine is null ? true : await ptz.RemovePresetAsync("pre2", mine.Token, Within(5_000));
        ptz.Release("pre2");
        sim.Reset();

        Assert.True(saved);
        Assert.NotNull(mine);
        Assert.True(goneTo);
        Assert.True(arrived);
        Assert.Equal((0.3, -0.2, 0.4), (Math.Round(at.Pan, 3), Math.Round(at.Tilt, 3), Math.Round(at.Zoom, 3)));
        Assert.True(homeSet);
        Assert.True(homeGo);
        Assert.True(homeArrived);
        Assert.True(Wait.Near(at, homeAt), $"{at} vs {homeAt}");
        Assert.True(removed);
        Assert.DoesNotContain(after!, p => p.Token == mine!.Token);
        Assert.False(removedAgain);
        var log = _fx.PtzOf("cam2");
        Assert.Contains(log, e => (string?)e["op"] == "SetPreset" && Ok(e) && e["preset"] is null);   // 토큰 없이 저장(카메라 자동 할당)
    }

    // ── 연결 · 인증 · 시계 · 끊김 뒤 재연결 ────────────────────────────────────────

    [Fact]
    public async Task should_connect_drive_and_resolve_stream_when_camera_clock_is_three_hours_ahead()
    {
        var ptz = NewPtz(out var registry);
        var sim = _fx.Farm.Find(3)!.Ptz!;
        sim.Reset();

        var stream = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync("skew3", _fx.Info(3), Within(10_000));
        var ready = await ptz.PrepareAsync("skew3", _fx.Info(3), Within(10_000));
        var goneTo = await ptz.GotoPresetAsync("skew3", "2", Within(5_000));
        var imaging = await ptz.GetImagingAsync("skew3", Within(5_000));
        ptz.Release("skew3");

        Assert.True(stream.Success, stream.ToString());
        Assert.True(ready.PtzCapable);
        Assert.True(goneTo);
        Assert.NotNull(imaging);
        Assert.DoesNotContain(_fx.RequestsOf("cam3"), e => ((string?)e["result"] ?? "").Contains("Stale", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_give_up_within_limit_then_reconnect_when_camera_answers_again()
    {
        var ptz = NewPtz(out _);
        var faults = _fx.Farm.Find(4)!.Spec.Faults;
        try
        {
            faults.NoOnvifReply = true;
            var sw = Stopwatch.StartNew();
            var silent = await ptz.PrepareAsync("re4", _fx.Info(4), Within(1_500));
            var silentMs = sw.ElapsedMilliseconds;

            faults.NoOnvifReply = false;   // 카메라가 돌아왔다 — 팝업을 다시 열면(다음 준비) 붙어야 한다
            sw.Restart();
            var back = await ptz.PrepareAsync("re4", _fx.Info(4), Within(10_000));
            var backMs = sw.ElapsedMilliseconds;

            Assert.False(silent.Connected);
            Assert.True(silentMs < 2_300, $"silent prepare returned after {silentMs} ms");
            Assert.True(back.Connected && back.PtzCapable, $"reconnect connected={back.Connected} ptz={back.PtzCapable} in {backMs} ms");
            Assert.True(backMs < 3_000, $"reconnect took {backMs} ms (stuck behind the abandoned first attempt?)");
        }
        finally
        {
            faults.NoOnvifReply = false;
            ptz.Release("re4");
        }
    }

    [Fact]
    public async Task should_fail_commands_within_limit_while_camera_is_down_and_work_again_after_it_returns()
    {
        var ptz = NewPtz(out _);
        var spec = _fx.Farm.Find(4)!.Spec;
        var sim = _fx.Farm.Find(4)!.Ptz!;
        sim.Reset();
        Assert.True((await ptz.PrepareAsync("drop4", _fx.Info(4), Within(10_000))).PtzCapable);
        try
        {
            spec.Faults.NoOnvifReply = true;   // 연결된 뒤 카메라가 끊겼다
            var sw = Stopwatch.StartNew();
            var downGoto = await ptz.GotoPresetAsync("drop4", "2", Within(1_000));
            var downMs = sw.ElapsedMilliseconds;

            spec.Faults.NoOnvifReply = false;
            sw.Restart();
            bool upGoto = false;
            while (!upGoto && sw.ElapsedMilliseconds < 15_000)   // 사용자 재시도 — 호스트(HostCameraServices.ExecuteCoreAsync)처럼 준비가 풀렸으면 다시 준비한 뒤 보낸다
            {
                if (!ptz.IsPrepared("drop4")) await ptz.PrepareAsync("drop4", _fx.Info(4), Within(2_000));
                upGoto = await ptz.GotoPresetAsync("drop4", "2", Within(2_000));
            }
            var upMs = sw.ElapsedMilliseconds;

            Assert.False(downGoto);
            Assert.True(downMs < 1_700, $"command while down returned after {downMs} ms");
            Assert.True(upGoto, $"camera came back but GotoPreset still failed after {upMs} ms");
            Assert.True(upMs < 3_000, $"first working command after recovery took {upMs} ms" + Environment.NewLine + Log);
        }
        finally
        {
            spec.Faults.NoOnvifReply = false;
            ptz.Release("drop4");
        }
    }

    [Fact]
    public async Task should_not_report_connected_with_wrong_password_and_connect_once_password_is_fixed()
    {
        var ptz = NewPtz(out var registry);
        var video = registry.GetVideo(VideoProviderKind.Onvif)!;

        var wrong = await ptz.PrepareAsync("pw5", _fx.Info(5, password: "wrong-password"), Within(10_000));
        var wrongStream = await video.ResolveStreamAsync("pw5", _fx.Info(5, password: "wrong-password"), Within(10_000));
        var right = await ptz.PrepareAsync("pw5", _fx.Info(5), Within(10_000));
        var rightStream = await video.ResolveStreamAsync("pw5", _fx.Info(5), Within(10_000));
        var presets = await ptz.GetPresetsAsync("pw5", Within(5_000));
        ptz.Release("pw5");

        Assert.False(wrong.Connected, "wrong password must not look connected (GIS shows PTZ/imaging as available)");
        Assert.False(wrong.PtzCapable);
        Assert.False(wrongStream.Success);
        Assert.True(right.Connected && right.PtzCapable, $"after fixing the password: connected={right.Connected} ptz={right.PtzCapable}");
        Assert.True(rightStream.Success, rightStream.ToString());
        Assert.NotNull(presets);
    }

    [Fact]
    public async Task should_recover_without_host_restart_when_camera_refused_credentials_for_a_while()
    {
        var ptz = NewPtz(out _);
        var faults = _fx.Farm.Find(5)!.Spec.Faults;
        try
        {
            faults.AuthFail = true;   // 카메라 쪽 계정이 잠깐 잠겼다(또는 비밀번호가 바뀌는 중)
            var refused = await ptz.PrepareAsync("lock5", _fx.Info(5), Within(10_000));
            faults.AuthFail = false;
            var back = await ptz.PrepareAsync("lock5", _fx.Info(5), Within(10_000));
            var presets = await ptz.GetPresetsAsync("lock5", Within(5_000));

            Assert.False(refused.PtzCapable);
            Assert.True(back.PtzCapable, "camera accepts the account again but the popup stays broken until the host restarts");
            Assert.NotNull(presets);
        }
        finally
        {
            faults.AuthFail = false;
            ptz.Release("lock5");
        }
    }

    // ── 미디어: 메인/서브 고르기 ───────────────────────────────────────────────────

    [Theory]
    [InlineData(true, 0, 0, "cam1_sub")]       // 크기 모름 + 서브 선호
    [InlineData(false, 0, 0, "cam1")]          // 크기 모름 + 메인
    [InlineData(true, 320, 180, "cam1_sub")]   // 작은 타일
    [InlineData(true, 1280, 720, "cam1")]      // 큰 상자 — 서브(640)로는 덮지 못한다
    public async Task should_pick_main_or_sub_profile_by_preference_and_draw_size_when_stream_resolved(bool preferSub, int w, int h, string path)
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var info = new VideoProviderInfo
        {
            Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = _fx.Farm.Find(1)!.Spec.OnvifPort,
            Username = _fx.Farm.Options.User, Password = _fx.Farm.Options.Password,
            PreferSubStream = preferSub, TargetWidth = w, TargetHeight = h,
        };

        var r = await registry.GetVideo(VideoProviderKind.Onvif)!.ResolveStreamAsync($"ms{preferSub}{w}", info, Within(10_000));

        Assert.True(r.Success, r.ToString());
        Assert.EndsWith("/" + path, r.Uri);
        Assert.StartsWith($"rtsp://{_fx.Farm.Options.User}:{_fx.Farm.Options.Password}@", r.Uri);
    }

    // ── 프로파일 · 설정 변경 · 같은 카메라 두 팝업 ─────────────────────────────────

    [Fact]
    public async Task should_drive_ptz_through_the_profile_that_carries_ptz_when_first_profile_has_none()
    {
        var ptz = NewPtz(out _);
        var sim = _fx.Farm.Find(6)!.Ptz!;
        sim.Reset();

        var ready = await ptz.PrepareAsync("np6", _fx.Info(6), Within(10_000));
        var moved = await ptz.ContinuousMoveAsync("np6", 0.5, 0, 0, Within(5_000));
        var sawMoving = await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).PanTiltMoving, TimeSpan.FromSeconds(2));
        await ptz.StopAsync("np6", Within(5_000));
        var presets = await ptz.GetPresetsAsync("np6", Within(5_000));
        var goneTo = await ptz.GotoPresetAsync("np6", "2", Within(5_000));
        ptz.Release("np6");

        Assert.True(ready.PtzCapable);
        Assert.True(moved);
        Assert.True(sawMoving, "ContinuousMove did not move the camera");
        Assert.NotNull(presets);
        Assert.True(goneTo);
        Assert.DoesNotContain(_fx.PtzOf("cam6"), e => (string?)e["result"] == "fault:NoPTZProfile");
    }

    [Fact]
    public async Task should_connect_to_the_new_address_when_camera_settings_change_while_host_runs()
    {
        var ptz = NewPtz(out var registry);
        var video = registry.GetVideo(VideoProviderKind.Onvif)!;

        // 같은 장비 id 가 처음엔 고정 카메라(cam7) 주소였다가, 장비 설정에서 PTZ 카메라(cam1) 주소로 바뀌었다
        var before = await ptz.PrepareAsync("edit", _fx.Info(7), Within(10_000));
        var beforeStream = await video.ResolveStreamAsync("edit", _fx.Info(7), Within(10_000));
        var after = await ptz.PrepareAsync("edit", _fx.Info(1), Within(10_000));
        var afterStream = await video.ResolveStreamAsync("edit", _fx.Info(1), Within(10_000));
        ptz.Release("edit");

        Assert.False(before.PtzCapable);
        Assert.EndsWith("/cam7_sub", beforeStream.Uri);
        Assert.True(after.PtzCapable, "settings changed to a PTZ camera but the host kept the old connection");
        Assert.EndsWith("/cam1_sub", afterStream.Uri);
    }

    [Fact]
    public async Task should_serve_every_request_correctly_when_two_popups_use_the_same_camera_at_once()
    {
        // 호스트 안 제공자는 하나 — 이벤트 창 타일과 더블클릭 팝업이 같은 카메라 id 로 동시에 부른다
        var ptz = NewPtz(out var registry);
        var video = registry.GetVideo(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();
        int profilesBefore = _fx.RequestsOf("cam2").Count(e => (string?)e["op"] == "GetProfiles");

        var prepares = await Task.WhenAll(
            ptz.PrepareAsync("same2", _fx.Info(2), Within(10_000)),
            ptz.PrepareAsync("same2", _fx.Info(2), Within(10_000)));
        var streams = await Task.WhenAll(
            video.ResolveStreamAsync("same2", _fx.Info(2), Within(10_000)),
            video.ResolveStreamAsync("same2", _fx.Info(2), Within(10_000)));
        var gotoTask = ptz.GotoPresetAsync("same2", "4", Within(5_000));      // 타일: 이벤트 프리셋으로
        var listTask = ptz.GetPresetsAsync("same2", Within(5_000));           // 팝업: 프리셋 탭
        var imgTask = ptz.GetImagingAsync("same2", Within(5_000));            // 팝업: 옵션 탭
        await Task.WhenAll(gotoTask, listTask, imgTask);
        var arrived = await Wait.UntilAsync(() => !sim.Status(DateTime.UtcNow).IsMoving, TimeSpan.FromSeconds(3));
        ptz.Release("same2");

        Assert.All(prepares, r => Assert.True(r.PtzCapable));
        Assert.All(streams, r => Assert.True(r.Success, r.ToString()));
        Assert.True(gotoTask.Result);
        Assert.Equal(5, listTask.Result!.Count);
        Assert.NotNull(imgTask.Result);
        Assert.True(arrived);
        Assert.True(Wait.Near(sim.FindPreset("4")!.Position, sim.Status(DateTime.UtcNow).Position), $"{sim.Status(DateTime.UtcNow).Position}");
        Assert.Equal(1, _fx.RequestsOf("cam2").Count(e => (string?)e["op"] == "GetProfiles") - profilesBefore);   // 연결(초기화)은 한 번만
    }
}
