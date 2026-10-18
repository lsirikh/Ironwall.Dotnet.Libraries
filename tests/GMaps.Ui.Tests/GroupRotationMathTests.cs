using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 선택 묶음 회전 순수 수학(<see cref="GroupRotationMath"/>) — 실코드 소스링크.
/// 위경도 ↔ 메르카토르 왕복 · 중심 · 중심 기준 회전 · 방향 각 누적 / 360° 감기 · 드래그 각도 · Shift 스냅 · 표시 글.
/// </summary>
public class GroupRotationMathTests
{
    private const double Lat = 37.39423226055614, Lng = 126.96720778942108;   // 편집 시험 기준점(EditRig.Home)

    private static double Meters((double lat, double lng) a, (double lat, double lng) b)
    {
        const double R = 6371000.0;
        double p1 = a.lat * Math.PI / 180, p2 = b.lat * Math.PI / 180;
        double dp = p2 - p1, dl = (b.lng - a.lng) * Math.PI / 180;
        double h = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    // ── 위경도 ↔ 메르카토르 왕복 ──

    [Theory]
    [InlineData(0, 0)]
    [InlineData(37.394232, 126.967207)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(60, -150)]
    public void should_round_trip_lat_lng_when_converted_to_mercator_and_back(double lat, double lng)
    {
        var (x, y) = GroupRotationMath.ToMercator(lat, lng);
        var (lat2, lng2) = GroupRotationMath.FromMercator(x, y);
        Assert.Equal(lat, lat2, 9);
        Assert.Equal(lng, lng2, 9);
    }

    [Fact]
    public void should_clamp_latitude_when_point_is_beyond_mercator_limit()
    {
        var (_, y) = GroupRotationMath.ToMercator(90, 0);
        Assert.False(double.IsInfinity(y));
        Assert.InRange(GroupRotationMath.FromMercator(0, y).lat, 85.05, 85.06);
    }

    // ── 중심 ──

    [Fact]
    public void should_return_null_center_when_no_points()
        => Assert.Null(GroupRotationMath.BoundsCenter(Array.Empty<(double, double)>()));

    [Fact]
    public void should_return_bounds_center_not_mean_when_points_are_uneven()
    {
        // 한쪽에 몰린 점 3개 + 반대쪽 1개 — 평균이 아니라 경계 상자 가운데
        var pts = new[] { (Lat, Lng), (Lat, Lng + 0.0001), (Lat, Lng + 0.0002), (Lat, Lng + 0.002) };
        var c = GroupRotationMath.BoundsCenter(pts)!.Value;
        Assert.Equal(Lng + 0.001, c.lng, 9);
        Assert.Equal(Lat, c.lat, 9);
    }

    [Fact]
    public void should_center_on_the_point_itself_when_single_point()
    {
        var c = GroupRotationMath.BoundsCenter(new[] { (Lat, Lng) })!.Value;
        Assert.Equal(Lat, c.lat, 9);
        Assert.Equal(Lng, c.lng, 9);
    }

    // ── 중심 기준 회전 ──

    [Fact]
    public void should_move_north_point_to_east_when_rotated_ninety_degrees_clockwise()
    {
        var center = (Lat, Lng);
        var north = (Lat + 0.001, Lng);
        var r = GroupRotationMath.RotateAround(north, center, 90);
        Assert.Equal(Lat, r.lat, 6);              // 같은 위도(동쪽)
        Assert.True(r.lng > Lng);                 // 시계 방향 + = 북 → 동
        Assert.InRange(Meters(center, r), Meters(center, north) * 0.999, Meters(center, north) * 1.001);   // 거리 보존
    }

    [Fact]
    public void should_keep_distance_to_center_when_rotated_any_angle()
    {
        var center = (Lat, Lng);
        var p = (Lat + 0.0007, Lng - 0.0013);
        double d0 = Meters(center, p);
        foreach (var a in new[] { 1.0, 15, 33.3, 90, 179.9, -45, -170 })
            Assert.InRange(Meters(center, GroupRotationMath.RotateAround(p, center, a)), d0 * 0.999, d0 * 1.001);
    }

    [Fact]
    public void should_keep_pairwise_distances_when_group_is_rotated()
    {
        // 묶음 모양(서로의 거리) 유지 — 위경도를 직접 돌리면 동서가 cos(위도) 만큼 찌그러진다
        var a = (Lat, Lng); var b = (Lat + 0.0005, Lng + 0.0011);
        var c = GroupRotationMath.BoundsCenter(new[] { a, b })!.Value;
        double d0 = Meters(a, b);
        var a2 = GroupRotationMath.RotateAround(a, c, 63);
        var b2 = GroupRotationMath.RotateAround(b, c, 63);
        Assert.InRange(Meters(a2, b2), d0 * 0.999, d0 * 1.001);
    }

    [Fact]
    public void should_return_original_when_rotated_back_by_the_same_angle()
    {
        var center = (Lat, Lng);
        var p = (Lat - 0.0004, Lng + 0.0009);
        var r = GroupRotationMath.RotateAround(GroupRotationMath.RotateAround(p, center, 37.5), center, -37.5);
        Assert.Equal(p.Item1, r.lat, 10);
        Assert.Equal(p.Item2, r.lng, 10);
    }

    [Fact]
    public void should_not_move_center_when_rotated()
    {
        var r = GroupRotationMath.RotateAround((Lat, Lng), (Lat, Lng), 123);
        Assert.Equal(Lat, r.lat, 10);
        Assert.Equal(Lng, r.lng, 10);
    }

    // ── 방향 각 누적 · 360° 감기 ──

    [Theory]
    [InlineData(350, 20, 10)]
    [InlineData(10, -20, 350)]
    [InlineData(0, 360, 0)]
    [InlineData(90, 725, 95)]
    [InlineData(45, 0.1, 45.1)]
    public void should_wrap_bearing_into_zero_to_360_when_rotation_is_added(double bearing, double delta, double expected)
        => Assert.Equal(expected, GroupRotationMath.AddBearing(bearing, delta), 9);

    [Fact]
    public void should_coerce_to_zero_when_angle_is_not_a_number()
    {
        Assert.Equal(0, GroupRotationMath.Normalize360(double.NaN));
        Assert.Equal(0, GroupRotationMath.Normalize360(double.PositiveInfinity));
        Assert.False(double.IsNegative(GroupRotationMath.Normalize360(-0.0)));
    }

    [Theory]
    [InlineData(190, -170)]
    [InlineData(180, 180)]
    [InlineData(-180, 180)]
    [InlineData(-190, 170)]
    [InlineData(370, 10)]
    public void should_wrap_rotation_into_signed_half_turn_when_beyond_180(double input, double expected)
        => Assert.Equal(expected, GroupRotationMath.WrapSigned(input), 9);

    // ── 드래그 각도 · Shift 스냅 ──

    [Fact]
    public void should_measure_clockwise_drag_as_positive_when_screen_y_points_down()
    {
        // 중심(100,100) 위쪽(100,50)을 눌러 오른쪽(150,100)으로 = 시계 방향 90°
        Assert.Equal(90, GroupRotationMath.DragAngle(100, 100, 100, 50, 150, 100, snap: false), 9);
        Assert.Equal(-90, GroupRotationMath.DragAngle(100, 100, 100, 50, 50, 100, snap: false), 9);
    }

    [Fact]
    public void should_take_shortest_signed_angle_when_drag_crosses_the_back()
    {
        // 위쪽에서 시계 방향으로 거의 한 바퀴(-10° 쪽) = -10°
        double a = 350 * Math.PI / 180;
        double x = 100 + 50 * Math.Sin(a), y = 100 - 50 * Math.Cos(a);
        Assert.Equal(-10, GroupRotationMath.DragAngle(100, 100, 100, 50, x, y, snap: false), 6);
    }

    [Theory]
    [InlineData(7.4, 0)]
    [InlineData(7.5, 15)]
    [InlineData(22, 15)]
    [InlineData(-38, -45)]
    [InlineData(173, 180)]
    public void should_snap_to_fifteen_degree_steps_when_shift_is_held(double raw, double expected)
        => Assert.Equal(expected, GroupRotationMath.Snap(raw, GroupRotationMath.SnapStepDegrees), 9);

    [Fact]
    public void should_snap_drag_angle_when_snap_is_on()
    {
        double a = 22 * Math.PI / 180;   // 22° → 15°
        double x = 100 + 50 * Math.Sin(a), y = 100 - 50 * Math.Cos(a);
        Assert.Equal(15, GroupRotationMath.DragAngle(100, 100, 100, 50, x, y, snap: true), 9);
        Assert.Equal(22, GroupRotationMath.DragAngle(100, 100, 100, 50, x, y, snap: false), 6);
    }

    [Fact]
    public void should_keep_angle_when_snap_step_is_zero()
        => Assert.Equal(7.3, GroupRotationMath.Snap(7.3, 0), 9);

    [Fact]
    public void should_put_handle_to_the_right_when_top_handle_is_rotated_ninety_on_screen()
    {
        var (x, y) = GroupRotationMath.RotateScreen(100, 100, 100, 50, 90);
        Assert.Equal(150, x, 9);
        Assert.Equal(100, y, 9);
    }

    [Fact]
    public void should_match_drag_angle_when_handle_is_rotated_by_it()
    {
        // 손잡이를 끈 각도만큼 돌리면 다시 재도 같은 각(드래그 ↔ 손잡이 표시 일관)
        var (x, y) = GroupRotationMath.RotateScreen(200, 300, 230, 180, 41);
        Assert.Equal(41, GroupRotationMath.DragAngle(200, 300, 230, 180, x, y, snap: false), 6);
    }

    // ── 키 단위 · 표시 글 ──

    [Fact]
    public void should_step_one_degree_and_fine_tenth_when_keys_are_used()
    {
        Assert.Equal(1.0, GroupRotationMath.KeyStepDegrees);
        Assert.Equal(0.1, GroupRotationMath.FineKeyStepDegrees);
    }

    [Theory]
    [InlineData(12, "+12.0°")]
    [InlineData(-0.5, "-0.5°")]
    [InlineData(0, "0.0°")]
    [InlineData(0.04, "0.0°")]
    [InlineData(190, "-170.0°")]
    public void should_format_signed_angle_when_shown_on_screen(double angle, string expected)
        => Assert.Equal(expected, GroupRotationMath.FormatAngle(angle));
}
