using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// event-map-flyto PRD — 이벤트 시 지도 이동의 보간 · 정규화 · 양보 · 몰림 · 종류 켜기(헤드리스, 창 없음).
/// </summary>
public class MapEventFlyToTests
{
    private static readonly FlyToPose A = new(37.0, 127.0, 15.0);
    private static readonly FlyToPose B = new(37.1, 127.2, 19.0);

    // ── 보간 ──────────────────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-001")]
    public void should_return_exact_start_and_end_when_elapsed_is_at_bounds()
    {
        Assert.Equal(A, MapFlyToMath.Sample(A, B, 0, 1000));
        Assert.Equal(A, MapFlyToMath.Sample(A, B, -50, 1000));
        Assert.Equal(B, MapFlyToMath.Sample(A, B, 1000, 1000));
        Assert.Equal(B, MapFlyToMath.Sample(A, B, 1234.5, 1000));
    }

    [Fact, Trait("Scenario", "FLY-002")]
    public void should_move_monotonically_when_time_advances()
    {
        var prev = A;
        for (var ms = 0; ms <= 1000; ms += 7)
        {
            var p = MapFlyToMath.Sample(A, B, ms, 1000);
            Assert.True(p.Lat >= prev.Lat && p.Lng >= prev.Lng && p.Zoom >= prev.Zoom, $"{ms} ms 에서 뒤로 감");
            prev = p;
        }
    }

    [Fact, Trait("Scenario", "FLY-003")]
    public void should_ease_in_and_out_when_curve_is_sampled()
    {
        Assert.Equal(0.0, MapFlyToMath.Ease(0));
        Assert.Equal(1.0, MapFlyToMath.Ease(1));
        Assert.Equal(0.5, MapFlyToMath.Ease(0.5), 12);
        // 양 끝은 느리고(첫 10% 에 10% 미만) 가운데는 빠르다
        Assert.True(MapFlyToMath.Ease(0.1) < 0.1);
        Assert.True(MapFlyToMath.Ease(0.9) > 0.9);
        Assert.True(MapFlyToMath.Ease(0.55) - MapFlyToMath.Ease(0.45) > 0.1);
    }

    [Fact, Trait("Scenario", "FLY-004")]
    public void should_finish_pan_before_zoom_when_flight_overlaps_them()
    {
        var at80 = MapFlyToMath.Sample(A, B, 800, 1000);
        Assert.Equal(B.Lat, at80.Lat, 12);           // 위치는 80% 에 도착
        Assert.True(at80.Zoom < B.Zoom);             // 줌은 아직 당기는 중
        var at20 = MapFlyToMath.Sample(A, B, 200, 1000);
        Assert.Equal(A.Zoom, at20.Zoom, 12);         // 줌은 20% 부터
        Assert.True(at20.Lat > A.Lat);               // 팬은 이미 출발
    }

    [Fact, Trait("Scenario", "FLY-005")]
    public void should_depend_on_elapsed_ms_only_when_frame_rate_differs()
    {
        // 60fps 로 5번 샘플하든 1번 샘플하든 같은 시각엔 같은 자세 — 프레임 수와 무관.
        var once = MapFlyToMath.Sample(A, B, 500, 1000);
        FlyToPose last = A;
        foreach (var ms in new[] { 16.7, 100.2, 250.0, 433.3, 500.0 }) last = MapFlyToMath.Sample(A, B, ms, 1000);
        Assert.Equal(once, last);
    }

    // ── 0 ms · 상한 · 정수 ────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-006")]
    public void should_jump_instantly_when_duration_is_zero()
    {
        Assert.Equal(B, MapFlyToMath.Sample(A, B, 0, 0));
        Assert.True(new MapFlyToAnimation(A, B, 0).IsDone(0));
    }

    [Theory, Trait("Scenario", "FLY-007")]
    [InlineData(9999, 5000)]
    [InlineData(5000.4, 5000)]
    [InlineData(5001, 5000)]
    [InlineData(-5, 0)]
    [InlineData(-0.4, 0)]
    [InlineData(999.9999, 1000)]
    [InlineData(1000.5, 1001)]
    [InlineData(2500, 2500)]
    [InlineData(double.NaN, MapEventFlyToModel.DefaultDurationMs)]
    [InlineData(double.PositiveInfinity, MapEventFlyToModel.DefaultDurationMs)]
    public void should_clamp_and_round_duration_when_input_is_out_of_range_or_fractional(double input, int expected)
        => Assert.Equal(expected, MapEventFlyToModel.NormalizeDurationMs(input));

    [Fact, Trait("Scenario", "FLY-008")]
    public void should_write_integers_only_when_settings_are_serialized()
    {
        var m = new MapEventFlyToModel { DurationMs = MapEventFlyToModel.NormalizeDurationMs(999.9999) };
        var json = JsonConvert.SerializeObject(m);
        Assert.Contains("\"DurationMs\":1000", json);
        Assert.DoesNotContain(".", json);   // 소수점이 파일 어디에도 없다
        Assert.Equal(1000, JsonConvert.DeserializeObject<MapEventFlyToModel>(json)!.DurationMs);
    }

    [Fact, Trait("Scenario", "FLY-009")]
    public void should_clamp_loaded_values_when_file_holds_out_of_range_values()
    {
        var m = new MapEventFlyToModel { DurationMs = 99999, MaxZoom = 40 };
        m.Normalize(2, 21);
        Assert.Equal(5000, m.DurationMs);
        Assert.Equal(21, m.MaxZoom);
        m.MaxZoom = -3; m.DurationMs = -1;
        m.Normalize(2, 21);
        Assert.Equal(2, m.MaxZoom);
        Assert.Equal(0, m.DurationMs);
    }

    [Fact, Trait("Scenario", "FLY-010")]
    public void should_use_defaults_when_model_is_new()
    {
        var m = new MapEventFlyToModel();
        Assert.True(m.IsDetectionEnabled);
        Assert.True(m.IsFaultEnabled);
        Assert.Equal(19, m.MaxZoom);
        Assert.Equal(1000, m.DurationMs);
    }

    // ── 최대 줌 ───────────────────────────────────────────────────────

    [Theory, Trait("Scenario", "FLY-011")]
    [InlineData(19.4, 2, 21, 19)]
    [InlineData(18.5, 2, 21, 19)]
    [InlineData(25, 2, 21, 21)]
    [InlineData(0, 2, 21, 2)]
    [InlineData(double.NaN, 2, 21, 19)]
    public void should_keep_max_zoom_integer_within_map_range_when_normalized(double input, int min, int max, int expected)
        => Assert.Equal(expected, MapEventFlyToModel.NormalizeMaxZoom(input, min, max));

    [Fact, Trait("Scenario", "FLY-012")]
    public void should_zoom_in_to_setting_when_map_is_zoomed_out()
        => Assert.Equal(19.0, MapFlyToMath.ResolveTargetZoom(19, 2, 21, 12.5));

    [Fact, Trait("Scenario", "FLY-013")]
    public void should_not_zoom_out_when_map_is_already_closer()
        => Assert.Equal(20.5, MapFlyToMath.ResolveTargetZoom(19, 2, 21, 20.5));

    [Fact, Trait("Scenario", "FLY-014")]
    public void should_cap_target_zoom_when_setting_exceeds_map_max()
        => Assert.Equal(18.0, MapFlyToMath.ResolveTargetZoom(22, 2, 18, 10));

    // ── 시간 계획 ─────────────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-015")]
    public void should_skip_flight_when_already_centered_at_same_zoom()
        => Assert.Null(MapFlyToMath.PlanDurationMs(1000, isTargetInView: true, isAtCenter: true, zoomChanges: false));

    [Fact, Trait("Scenario", "FLY-016")]
    public void should_halve_duration_when_target_in_view_and_zoom_same()
        => Assert.Equal(500, MapFlyToMath.PlanDurationMs(1000, isTargetInView: true, isAtCenter: false, zoomChanges: false));

    [Fact, Trait("Scenario", "FLY-017")]
    public void should_use_full_duration_capped_when_zoom_changes()
    {
        Assert.Equal(1000, MapFlyToMath.PlanDurationMs(1000, true, true, zoomChanges: true));
        Assert.Equal(5000, MapFlyToMath.PlanDurationMs(9999, false, false, true));
    }

    // ── 양보 ──────────────────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-018")]
    public void should_fly_when_user_is_idle()
        => Assert.False(MapFlyToMath.ShouldYield(new FlyToUserState(false, false, false, false, 10_000)));

    [Theory, Trait("Scenario", "FLY-019")]
    [InlineData(true, false, false, false, 10_000)]   // 끌기 중
    [InlineData(false, true, false, false, 10_000)]   // 마우스 캡처
    [InlineData(false, false, true, false, 10_000)]   // 편집 모드
    [InlineData(false, false, false, true, 10_000)]   // 그리기 · 측정 · 배치
    [InlineData(false, false, false, false, 500)]     // 방금 휠 · 클릭
    public void should_yield_when_user_is_touching_the_map(bool drag, bool captured, bool edit, bool exclusive, double sinceInput)
        => Assert.True(MapFlyToMath.ShouldYield(new FlyToUserState(drag, captured, edit, exclusive, sinceInput)));

    // ── 몰림 ──────────────────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-020")]
    public void should_continue_from_current_pose_when_new_event_arrives_mid_flight()
    {
        var running = new MapFlyToAnimation(A, B, 1000);
        var c = new FlyToPose(36.0, 126.0, 17.0);
        var next = MapFlyToMath.Retarget(running, 400, current: A, newEnd: c, durationMs: 1000);
        Assert.Equal(running.Sample(400), next.Start);   // 순간 이동 없이 지금 자세에서
        Assert.Equal(c, next.End);                        // 마지막 이벤트가 이긴다
    }

    [Fact, Trait("Scenario", "FLY-021")]
    public void should_start_from_map_when_no_flight_is_running()
    {
        var c = new FlyToPose(36.0, 126.0, 17.0);
        Assert.Equal(A, MapFlyToMath.Retarget(null, 0, A, c, 800).Start);
        Assert.Equal(A, MapFlyToMath.Retarget(new MapFlyToAnimation(B, B, 300), 999, A, c, 800).Start);   // 끝난 비행은 무시
    }

    // ── 종류 켜기 ─────────────────────────────────────────────────────

    [Fact, Trait("Scenario", "FLY-022")]
    public void should_follow_toggles_when_event_type_is_detection_or_fault()
    {
        var m = new MapEventFlyToModel { IsDetectionEnabled = true, IsFaultEnabled = false };
        Assert.True(MapFlyToMath.IsEnabledFor(m, EnumEventType.Intrusion));
        Assert.True(MapFlyToMath.IsEnabledFor(m, EnumEventType.Alert));
        Assert.False(MapFlyToMath.IsEnabledFor(m, EnumEventType.Fault));
        m.IsDetectionEnabled = false; m.IsFaultEnabled = true;
        Assert.False(MapFlyToMath.IsEnabledFor(m, EnumEventType.Intrusion));
        Assert.True(MapFlyToMath.IsEnabledFor(m, EnumEventType.Fault));
    }

    [Fact, Trait("Scenario", "FLY-023")]
    public void should_not_fly_when_event_type_is_not_detection_or_fault()
    {
        var m = new MapEventFlyToModel();
        Assert.False(MapFlyToMath.IsEnabledFor(m, EnumEventType.Action));
        Assert.False(MapFlyToMath.IsEnabledFor(m, EnumEventType.WindyMode));
        Assert.False(MapFlyToMath.IsEnabledFor(null, EnumEventType.Intrusion));
    }

    // ── 줌 스냅 ───────────────────────────────────────────────────────

    [Theory, Trait("Scenario", "FLY-024")]
    [InlineData(17.24, 17.0)]
    [InlineData(17.25, 17.5)]
    [InlineData(17.74, 17.5)]
    [InlineData(17.75, 18.0)]
    public void should_snap_zoom_to_half_steps_when_applied(double zoom, double expected)
        => Assert.Equal(expected, MapFlyToMath.SnapHalf(zoom));
}
