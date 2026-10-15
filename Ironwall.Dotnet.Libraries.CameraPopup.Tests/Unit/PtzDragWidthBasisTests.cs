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
/// 화살표 이동량 기준(사용자 결정 2026-10-16 — 옛 2026-10-15 "가로 1/2 = 한 폭"을 대체): 화살표 길이 ÷ 보이는 영상 크기
/// (가로 성분 ÷ 폭, 세로 성분 ÷ 높이, 성분마다 ±1 에서 자름) = 지금 보이는 화면(시야)의 몇 배. 화면 끝까지(폭 전체) = 화면 한 폭(수평 화각 1개),
/// 높이 전체 = 수직 화각 1개. 네 방식(화각 상대 · 일반 상대 · 절대 · 시간 제한 연속) · 표시가 같은 말을 한다.
/// 옛 기준보다 같은 끌기의 이동량이 정확히 절반이므로 값 단언은 새 기준으로 다시 계산했다(같은 정밀도).
/// </summary>
public class PtzDragWidthBasisTests
{
    private const double Wide = 16d / 9d;
    private static readonly PtzLensModel Lens = PtzLensModel.Default;   // 광각 60° · 팬 360° · 틸트 180°

    private static PtzDragSpaces Spaces(bool fov = false, bool rel = true, bool abs = true, bool cont = true) => new(
        fov, -1, 1, -1, 1, rel, -1, 1, -1, 1, abs, -1, 1, -1, 1, 0, 1, cont);

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-101")]
    [InlineData(1, 0, 1, 0)]            // 폭 전체 = 1(한 폭)
    [InlineData(0.5, 0, 0.5, 0)]        // 폭 1/2 = 0.5
    [InlineData(1.4, 0, 1, 0)]          // 화면 밖까지 끌어도 1 에서 멈춤
    [InlineData(0, 0.4, 0, 0.225)]      // 세로 0.4×h = 0.225×w(폭 단위로 바꿔 돌려준다 — 높이 전체 = 수직 화각 1개)
    public void should_measure_arrow_in_view_widths_and_cap_at_full_screen(double viewX, double viewY, double mx, double my)
    {
        var (x, y) = PtzDragGesture.WidthVector(viewX, viewY, Wide);

        Assert.Equal(mx, x, 6);
        Assert.Equal(my, y, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-102")]
    public void should_cap_each_component_at_screen_edge_when_arrow_leaves_view()
    {
        var (x, y) = PtzDragGesture.WidthVector(2, -3, Wide);   // 오른쪽 위로 화면 밖 — 성분마다 화면 하나에서 멈춘다

        Assert.Equal(1d, x, 6);
        Assert.Equal(-1d / Wide, y, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-103")]
    [InlineData(1, 60)]       // 폭 전체 = 화각 하나(60°) = 화면 한 폭
    [InlineData(0.5, 30)]     // 폭 1/2 = 반 화각
    [InlineData(1.6, 60)]     // 화면 밖은 자름
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
        Assert.Equal(0.2 * 60d, panDeg, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-105")]
    public void should_split_fov_move_into_two_half_steps_when_full_width_is_dragged()
    {
        // ±1 = 반 화각인 공간 — 한 폭(2)은 한 번에 못 간다 → 1 씩 두 번
        var plan = PtzDragMath.Plan(Spaces(fov: true), PtzKnownPosition.Unknown, 1, 0, Wide);

        Assert.Equal(PtzDragMoveKind.RelativeFov, plan.Kind);
        Assert.Equal(2, plan.Steps);
        Assert.Equal(1d, plan.Pan, 6);
        Assert.Equal(2d, plan.Pan * plan.Steps, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-106")]
    public void should_send_half_width_once_as_view_edge_when_fov_space()
    {
        var (pan, tilt, steps) = PtzDragMath.FovSteps(0.5, 0, Wide, -1, 1, -1, 1);

        Assert.Equal((1d, 0d, 1), (Math.Round(pan, 6), Math.Round(tilt, 6), steps));
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-107")]
    public void should_turn_one_vertical_view_when_full_height_is_dragged_upwards()
    {
        // 위로 높이 전체 = 수직 화각 1개 = 틸트(±1 = 반 높이) 2 → 1 씩 두 번
        var (pan, tilt, steps) = PtzDragMath.FovSteps(0, -1, Wide, -1, 1, -1, 1);

        Assert.Equal(0d, pan, 6);
        Assert.Equal(2, steps);
        Assert.Equal(2d, tilt * steps, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-108")]
    public void should_move_absolute_target_by_one_view_width_when_full_width_is_dragged()
    {
        var plan = PtzDragMath.Plan(Spaces(rel: false), new PtzKnownPosition(0.1, 0.2, 0, true, true), 1, 0, Wide);

        Assert.Equal(PtzDragMoveKind.Absolute, plan.Kind);
        Assert.Equal(0.1 + (60d / 360d * 2d), plan.Pan, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-109")]
    [InlineData(0.5, 350)]    // 0.5 × 700 ms
    [InlineData(1, 700)]      // 최대(폭 전체)
    [InlineData(1.8, 700)]    // 자름
    public void should_scale_timed_continuous_by_view_width_and_use_given_speed(double viewX, int expectedMs)
    {
        var plan = PtzDragMath.ContinuousPulse(viewX, 0, Wide, speed: 0.8);

        Assert.Equal(expectedMs, plan.PulseMs);
        Assert.Equal(0.8, plan.Pan, 6);   // 속도 = 설정 속도(크기와 무관)
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-109b")]
    public void should_not_pulse_longer_than_one_width_when_arrow_is_diagonal_to_corner()
        => Assert.Equal(PtzDragMath.PulseMaxMs, PtzDragMath.ContinuousPulse(1, 1, Wide).PulseMs);

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-110")]
    [InlineData(50, 0, 250, 150)]     // 떼면 중심으로 올 점 = 중심 + 화살표
    [InlineData(200, 0, 400, 150)]    // 폭 1/2 → 오른쪽 가장자리(반 화각)
    [InlineData(400, 0, 400, 150)]    // 폭 전체 = 한 폭 → 화면 밖 → 그 방향 가장자리
    [InlineData(0, -100, 200, 50)]    // 위로 100 px → 중심에서 100 px 위
    [InlineData(0, -200, 200, 0)]     // 위로 200 px → 위 가장자리
    public void should_place_target_where_the_new_center_will_be(double dx, double dy, double x, double y)
    {
        var (tx, ty) = PtzDragGesture.TargetPoint(400, 300, dx, dy);

        Assert.Equal(x, tx, 6);
        Assert.Equal(y, ty, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-111")]
    [InlineData(200, 0, "0.5화면")]
    [InlineData(400, 0, "1.0화면")]
    [InlineData(900, 0, "1.0화면")]     // 화면 밖은 화면 하나에서 멈춤
    [InlineData(0, 150, "0.5화면")]     // 세로는 높이 기준
    public void should_tell_arrow_amount_in_screens(double dx, double dy, string expected)
        => Assert.Equal(expected, PtzDragGesture.AmountText(PtzDragGesture.DragMagnitude(400, 300, dx, dy)));

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-113")]
    public void should_turn_exactly_one_screen_width_in_every_kind_when_arrow_spans_the_full_view()
    {
        var fov = PtzDragMath.Plan(Spaces(fov: true), PtzKnownPosition.Unknown, 1, 0, Wide);
        var (generic, _) = PtzDragMath.GenericTranslation(1, 0, Wide, 0, Lens, 2, 2, -1, 1, -1, 1);
        var abs = PtzDragMath.Plan(Spaces(rel: false), new PtzKnownPosition(0, 0, 0, true, true), 1, 0, Wide);

        Assert.Equal(2d, fov.Pan * fov.Steps, 6);                  // 화각 상대: 2 = 화각 1개
        Assert.Equal(Lens.WideHorizontalFovDeg, generic / 2d * Lens.PanSpanDeg, 6);
        Assert.Equal(Lens.WideHorizontalFovDeg, abs.Pan / 2d * Lens.PanSpanDeg, 6);
    }

    [Theory]
    [Trait("Scenario", "PTZ-DRAG-114")]
    [InlineData(100, 90, 260, 90, 0)]       // 오른쪽
    [InlineData(100, 90, 100, 170, 90)]     // 아래(화면 좌표 y 아래 +)
    [InlineData(200, 90, 100, 90, 180)]     // 왼쪽
    [InlineData(100, 90, 140, 50, -45)]     // 오른쪽 위
    public void should_point_arrow_head_from_start_to_pointer(double sx, double sy, double ex, double ey, double angle)
    {
        var arrow = PtzDragGesture.Arrow(sx, sy, ex, ey, headLength: 12);

        Assert.Equal(angle, arrow.AngleDeg, 6);
        double len = Math.Sqrt(((ex - sx) * (ex - sx)) + ((ey - sy) * (ey - sy)));
        // 몸통은 화살촉 안에서 끝난다(둥근 끝이 촉 끝을 뚫고 나오지 않게) — 끝점에서 촉 길이만큼 물러난 자리, 방향은 같다
        Assert.Equal(ex - ((ex - sx) / len * 12), arrow.ShaftEndX, 6);
        Assert.Equal(ey - ((ey - sy) / len * 12), arrow.ShaftEndY, 6);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-115")]
    public void should_not_draw_shaft_backwards_when_arrow_is_shorter_than_its_head()
    {
        var arrow = PtzDragGesture.Arrow(100, 90, 105, 90, headLength: 12);

        Assert.Equal((100d, 90d), (arrow.ShaftEndX, arrow.ShaftEndY));   // 몸통 없음(시작점에서 멈춤) — 촉만
        Assert.Equal(0d, arrow.AngleDeg, 6);
    }

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
    public async Task should_reach_one_full_view_width_in_two_fov_moves_when_full_width_is_dragged()
    {
        // 화각 상대 공간 ±1(= 반 화각)만 받는 카메라, 위치를 알려준다(MoveStatus IDLE)
        await using var camera = new FakeOnvifCamera { HasRelativeFov = true, HasRelativeGeneric = false, Status = (0, 0, 0) };
        var ptz = await PrepareAsync(camera, "fov-split");

        // 2026-10-16 사용자 결정: 화살표가 화면 끝까지(폭 전체) = 화면 한 폭(옛 기준은 폭 1/2)
        var outcome = await ptz.DragMoveAsync("fov-split", 1, 0, 16d / 9d, Within(5000));
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

        await ptz.DragMoveAsync("fov-stop", -1, 0, 16d / 9d, Within(5000));   // 한 폭 = 두 몫(2026-10-16 기준)
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
