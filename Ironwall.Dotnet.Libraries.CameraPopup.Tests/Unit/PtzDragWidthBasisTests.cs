using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// 끌기 이동량 기준(사용자 결정 2026-10-15): m = (dx, dy) ÷ (영상 가로/2), 길이 1 에서 자름. m = 1 → 화면 한 폭(수평 화각 1개).
/// 세로도 같은 각도/픽셀. 네 방식(화각 상대 · 일반 상대 · 절대 · 시간 제한 연속) · 목표 표시가 같은 말을 한다.
/// </summary>
public class PtzDragWidthBasisTests
{
    private const double Wide = 16d / 9d;
    private static readonly PtzLensModel Lens = PtzLensModel.Default;   // 광각 60° · 팬 360° · 틸트 180°

    private static PtzDragSpaces Spaces(bool fov = false, bool rel = true, bool abs = true, bool cont = true) => new(
        fov, -1, 1, -1, 1, rel, -1, 1, -1, 1, abs, -1, 1, -1, 1, 0, 1, cont);

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-101")]
    [InlineData(0.5, 0, 1, 0)]          // 가로 1/2 = 1(최대)
    [InlineData(0.25, 0, 0.5, 0)]       // 가로 1/4 = 0.5
    [InlineData(0.9, 0, 1, 0)]          // 1/2 를 넘게 끌어도 1 에서 멈춤
    [InlineData(0, 0.4, 0, 0.45)]       // 세로도 가로 픽셀로: 0.4×h = 0.225×w → 0.45
    public void should_measure_drag_in_half_view_widths_and_cap_at_one(double viewX, double viewY, double mx, double my)
    {
        var (x, y) = PtzDragGesture.WidthVector(viewX, viewY, Wide);

        Assert.Equal(mx, x, 6);
        Assert.Equal(my, y, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-102")]
    public void should_keep_direction_when_diagonal_drag_is_capped()
    {
        var (x, y) = PtzDragGesture.WidthVector(0.5, 0.5 * Wide, Wide);   // 가로 · 세로 같은 픽셀 → 45°

        Assert.Equal(1d, Math.Sqrt((x * x) + (y * y)), 6);
        Assert.Equal(x, y, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-103")]
    [InlineData(0.5, 60)]     // 가로 1/2 = 화각 하나(60°) = 화면 한 폭
    [InlineData(0.25, 30)]    // 가로 1/4 = 반 화각
    [InlineData(0.8, 60)]     // 1/2 초과는 자름
    public void should_turn_by_view_widths_when_generic_relative_space(double viewX, double expectedDeg)
    {
        var (pan, _) = PtzDragMath.GenericTranslation(viewX, 0, Wide, 0, Lens, 2, 2, -1, 1, -1, 1);

        Assert.Equal(expectedDeg / 360d * 2d, pan, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-104")]
    public void should_use_same_angle_per_pixel_for_tilt_as_for_pan()
    {
        // 가로 0.2×w 와 세로 0.2×w(= 0.2×가로세로비 ×h) — 같은 픽셀 수
        var (pan, _) = PtzDragMath.GenericTranslation(0.2, 0, Wide, 0, Lens, 2, 2, -1, 1, -1, 1);
        var (_, tilt) = PtzDragMath.GenericTranslation(0, -0.2 * Wide, Wide, 0, Lens, 2, 2, -1, 1, -1, 1);

        double panDeg = pan / 2d * Lens.PanSpanDeg, tiltDeg = tilt / 2d * Lens.TiltSpanDeg;
        Assert.Equal(panDeg, tiltDeg, 6);
        Assert.Equal(0.4 * 60d, panDeg, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-105")]
    public void should_split_fov_move_into_two_half_steps_when_half_width_is_dragged()
    {
        // ±1 = 반 화각인 공간 — 한 폭(2)은 한 번에 못 간다 → 1 씩 두 번
        var plan = PtzDragMath.Plan(Spaces(fov: true), PtzKnownPosition.Unknown, 0.5, 0, Wide);

        Assert.Equal(PtzDragMoveKind.RelativeFov, plan.Kind);
        Assert.Equal(2, plan.Steps);
        Assert.Equal(1d, plan.Pan, 6);
        Assert.Equal(2d, plan.Pan * plan.Steps, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-106")]
    public void should_send_quarter_width_once_as_view_edge_when_fov_space()
    {
        var (pan, tilt, steps) = PtzDragMath.FovSteps(0.25, 0, Wide, -1, 1, -1, 1);

        Assert.Equal((1d, 0d, 1), (Math.Round(pan, 6), Math.Round(tilt, 6), steps));
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-107")]
    public void should_split_tilt_by_screen_heights_when_a_full_width_is_dragged_upwards()
    {
        // 위로 한 폭 = 16:9 에서 화면 높이 1.78개 = 틸트(±1 = 반 높이) 3.56 → 1 이하 몫 4번
        var (pan, tilt, steps) = PtzDragMath.FovSteps(0, -0.5 * Wide, Wide, -1, 1, -1, 1);

        Assert.Equal(0d, pan, 6);
        Assert.Equal(4, steps);
        Assert.Equal(2d * Wide, tilt * steps, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-108")]
    public void should_move_absolute_target_by_one_view_width_when_half_width_is_dragged()
    {
        var plan = PtzDragMath.Plan(Spaces(rel: false), new PtzKnownPosition(0.1, 0.2, 0, true, true), 0.5, 0, Wide);

        Assert.Equal(PtzDragMoveKind.Absolute, plan.Kind);
        Assert.Equal(0.1 + (60d / 360d * 2d), plan.Pan, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-109")]
    [InlineData(0.25, 350)]   // 0.5 × 700 ms
    [InlineData(0.5, 700)]    // 최대
    [InlineData(0.9, 700)]    // 자름
    public void should_scale_timed_continuous_by_half_width_and_use_given_speed(double viewX, int expectedMs)
    {
        var plan = PtzDragMath.ContinuousPulse(viewX, 0, Wide, speed: 0.8);

        Assert.Equal(expectedMs, plan.PulseMs);
        Assert.Equal(0.8, plan.Pan, 6);   // 속도 = 설정 속도(크기와 무관)
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-110")]
    [InlineData(50, 0, 300, 150)]     // 가로 1/8 → 중심 + 1/4 폭
    [InlineData(100, 0, 400, 150)]    // 가로 1/4 → 오른쪽 가장자리(반 화각)
    [InlineData(200, 0, 400, 150)]    // 가로 1/2 = 한 폭 → 화면 밖 → 그 방향 가장자리
    [InlineData(0, -100, 200, 0)]     // 위로 100 px → 중심에서 200 px 위 → 위 가장자리
    public void should_place_target_where_the_new_center_will_be(double dx, double dy, double x, double y)
    {
        var (tx, ty) = PtzDragGesture.TargetPoint(400, 300, dx, dy);

        Assert.Equal(x, tx, 6);
        Assert.Equal(y, ty, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-111")]
    [InlineData(100, "0.5폭")]
    [InlineData(200, "1폭 · 최대")]
    [InlineData(500, "1폭 · 최대")]
    public void should_tell_drag_amount_in_view_widths(double dx, string expected)
        => Assert.Equal(expected, PtzDragGesture.AmountText(PtzDragGesture.DragMagnitude(400, 300, dx, 0)));

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-112")]
    [InlineData(0.7, 0, 1, 0.7)]
    [InlineData(0.02, 0, 1, 0.1)]     // 슬라이더 최소
    [InlineData(5, 0, 10, 0.5)]       // 카메라 범위 0..10
    [InlineData(1, 1, 1, 1)]          // 범위가 이상하면 0..1
    public void should_normalize_camera_default_speed_into_slider_range(double value, double min, double max, double expected)
        => Assert.Equal(expected, PtzController.NormalizeSpeed(value, min, max)!.Value, 6);
}

/// <summary>영상 오른쪽 아래 "이동 중 → 이동 완료" 판정(PtzMotionTracker) — 이동 중에만 묻고 끝나면 멈춘다.</summary>
public class PtzMotionTrackerTests
{
    private static PtzMotionTracker Fast(Func<CancellationToken, Task<bool?>> probe, List<PtzMotionState> seen) => new(probe, seen.Add)
    {
        Interval = TimeSpan.FromMilliseconds(20),
        DoneDuration = TimeSpan.FromMilliseconds(40),
        MinMoving = TimeSpan.FromMilliseconds(50),
        MaxTracking = TimeSpan.FromSeconds(2),
    };

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-120")]
    public async Task should_show_moving_then_done_then_hide_and_stop_polling_when_camera_reports_idle()
    {
        var answers = new Queue<bool?>(new bool?[] { true, true, false });
        int probes = 0;
        var seen = new List<PtzMotionState>();
        var tracker = Fast(_ => { probes++; return Task.FromResult(answers.Count > 0 ? answers.Dequeue() : false); }, seen);

        await tracker.Begin(TimeSpan.FromSeconds(1));
        int after = probes;
        await Task.Delay(100);

        Assert.Equal(new[] { PtzMotionState.Moving, PtzMotionState.Done, PtzMotionState.Idle }, seen);
        Assert.Equal(3, after);
        Assert.Equal(after, probes);   // 끝난 뒤에는 묻지 않는다
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-121")]
    public async Task should_finish_after_estimate_when_camera_gives_no_move_status()
    {
        var seen = new List<PtzMotionState>();
        var tracker = Fast(_ => Task.FromResult<bool?>(null), seen);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await tracker.Begin(TimeSpan.FromMilliseconds(200));

        Assert.True(sw.ElapsedMilliseconds >= 200, $"{sw.ElapsedMilliseconds} ms");
        Assert.Equal(new[] { PtzMotionState.Moving, PtzMotionState.Done, PtzMotionState.Idle }, seen);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-122")]
    public async Task should_not_call_it_done_before_move_starts_when_first_answer_is_idle()
    {
        // 막 보낸 이동이 아직 시작 전(IDLE) — MinMoving 전에는 멈춤으로 치지 않는다
        var seen = new List<PtzMotionState>();
        var tracker = Fast(_ => Task.FromResult<bool?>(false), seen);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await tracker.Begin(TimeSpan.FromSeconds(1));

        Assert.True(sw.ElapsedMilliseconds >= 50, $"{sw.ElapsedMilliseconds} ms");
        Assert.Equal(PtzMotionState.Idle, tracker.State);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-123")]
    public async Task should_keep_moving_without_flicker_when_a_new_move_begins()
    {
        var seen = new List<PtzMotionState>();
        var tracker = Fast(_ => Task.FromResult<bool?>(true), seen);

        var first = tracker.Begin(TimeSpan.FromSeconds(1));
        await Task.Delay(60);
        var second = tracker.Begin(TimeSpan.FromSeconds(1));
        await first;   // 앞 판정은 버려졌다

        Assert.Equal(new[] { PtzMotionState.Moving }, seen);
        tracker.Cancel();
        await second;
        Assert.Equal(PtzMotionState.Idle, tracker.State);
    }
}

/// <summary>실제 제공자 경로(루프백 가짜 ONVIF): 한 폭을 나눠 보내기 · 속도 싣기 · MoveStatus 읽기.</summary>
public class PtzDragWidthProviderTests
{
    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static async Task<ICameraPtzProvider> PrepareAsync(FakeOnvifCamera camera, string id)
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var ready = await ptz.PrepareAsync(id, info, Within(15_000));
        Assert.True(ready.Connected && ready.PtzCapable, ready.ToString());
        await Task.Delay(300);   // 준비 뒤 배경 위치 읽기가 끝나게
        return ptz;
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-130")]
    public async Task should_reach_one_full_view_width_in_two_fov_moves_when_half_width_is_dragged()
    {
        // 화각 상대 공간 ±1(= 반 화각)만 받는 카메라, 위치를 알려준다(MoveStatus IDLE)
        await using var camera = new FakeOnvifCamera { HasRelativeFov = true, HasRelativeGeneric = false, Status = (0, 0, 0) };
        var ptz = await PrepareAsync(camera, "fov-split");

        var outcome = await ptz.DragMoveAsync("fov-split", 0.5, 0, 16d / 9d, Within(5000));
        Assert.True(await StateRecorder.WaitUntilAsync(() => camera.Count("RelativeMove") >= 2, TimeSpan.FromSeconds(5)), "두 번째 몫이 안 나갔다");
        await Task.Delay(300);

        Assert.Equal(PtzDragMoveKind.RelativeFov, outcome.Kind);
        var moves = camera.PtzCommands.Where(c => c.Op == "RelativeMove").Select(c => FakeOnvifCamera.PanTiltOf(c.Body)!.Value).ToList();
        Assert.Equal(2, moves.Count);
        Assert.All(moves, m => Assert.Equal(1d, m.X, 4));   // 1 + 1 = 2 = 화면 한 폭
        Assert.All(moves, m => Assert.Equal(FakeOnvifCamera.RelFovUri, m.Space));
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-131")]
    public async Task should_drop_remaining_fov_step_when_stop_arrives_in_between()
    {
        await using var camera = new FakeOnvifCamera { HasRelativeFov = true, HasRelativeGeneric = false };   // MoveStatus 없음 → 몫 사이 어림 대기
        var ptz = await PrepareAsync(camera, "fov-stop");

        await ptz.DragMoveAsync("fov-stop", -0.5, 0, 16d / 9d, Within(5000));
        await ptz.StopAsync("fov-stop", Within(5000));
        await Task.Delay(PtzMotionPolicy.StepEstimate + TimeSpan.FromMilliseconds(600));

        Assert.Equal(new[] { "RelativeMove", "Stop" }, camera.PtzCommands.Select(c => c.Op));
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-132")]
    public async Task should_carry_popup_speed_on_relative_move_when_speed_is_given()
    {
        await using var camera = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        var ptz = await PrepareAsync(camera, "speed-1");

        await ptz.DragMoveAsync("speed-1", 0.1, 0, 16d / 9d, 0.7, Within(5000));

        var body = camera.PtzCommands.Single(c => c.Op == "RelativeMove").Body;
        var speed = System.Text.RegularExpressions.Regex.Match(body, "Speed\\b[^>]*>\\s*<(?:\\w+:)?PanTilt\\b([^>]*)>");
        Assert.True(speed.Success, body);
        Assert.Contains("x=\"0.7\"", speed.Groups[1].Value);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-133")]
    public async Task should_report_idle_from_move_status_and_unknown_when_camera_has_no_status()
    {
        await using var withStatus = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        await using var noStatus = new FakeOnvifCamera();
        var a = await PrepareAsync(withStatus, "ms-1");
        var b = await PrepareAsync(noStatus, "ms-2");

        Assert.False(await a.GetMoveStatusAsync("ms-1", Within(5000)));
        Assert.Null(await b.GetMoveStatusAsync("ms-2", Within(5000)));
        Assert.Null(await a.GetMoveStatusAsync("never-prepared", Within(5000)));
    }
}
