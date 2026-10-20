using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 묶음 회전 컨트롤러 판 순수 규칙(<see cref="GroupRotationPanelRules"/>) — 실코드 소스링크.
/// 각도 · 위경도 입력 검증(범위 · 형식) · ±1° · ±0.1° 한 칸 · 입력칸 글 · 종류별 요약 · 확정 한 줄 확인 글 ·
/// 중심 바꾼 뒤 회전 수학(<see cref="GroupRotationMath.RotateAround"/>).
/// </summary>
public class GroupRotationPanelRulesTests
{
    private const double Lat = 37.39423226055614, Lng = 126.96720778942108;

    // ── 각도 입력 ──

    [Theory]
    [InlineData("15", 15.0)]
    [InlineData("+15.5", 15.5)]
    [InlineData("-0.1", -0.1)]
    [InlineData(" 90° ", 90.0)]
    [InlineData("12.34", 12.3)]
    [InlineData("12.36", 12.4)]
    [InlineData("-180", -180.0)]
    [InlineData("180", 180.0)]
    [InlineData("-0", 0.0)]
    public void should_accept_angle_and_round_to_one_decimal_when_input_is_a_number_in_range(string text, double expected)
    {
        Assert.True(GroupRotationPanelRules.TryParseAngle(text, out var deg, out var err));
        Assert.Null(err);
        Assert.Equal(expected, deg, 9);
    }

    [Theory]
    [InlineData("", "각도를 넣어")]
    [InlineData("   ", "각도를 넣어")]
    [InlineData(null, "각도를 넣어")]
    [InlineData("abc", "숫자로")]
    [InlineData("1,5", "숫자로")]
    [InlineData("NaN", "숫자로")]
    [InlineData("180.1", "−180 ~ 180")]
    [InlineData("-181", "−180 ~ 180")]
    [InlineData("720", "−180 ~ 180")]
    public void should_reject_angle_with_message_when_input_is_empty_not_a_number_or_out_of_range(string? text, string message)
    {
        Assert.False(GroupRotationPanelRules.TryParseAngle(text, out _, out var err));
        Assert.Contains(message, err);
    }

    // ── 위경도 입력 ──

    [Theory]
    [InlineData("37.39423226", 37.39423226)]
    [InlineData("-85.05", -85.05)]
    [InlineData("0", 0.0)]
    public void should_accept_latitude_when_inside_mercator_limit(string text, double expected)
    {
        Assert.True(GroupRotationPanelRules.TryParseLatitude(text, out var v, out var err));
        Assert.Null(err);
        Assert.Equal(expected, v, 9);
    }

    [Theory]
    [InlineData("86", "위도는 −85.05 ~ 85.05")]
    [InlineData("-90", "위도는 −85.05 ~ 85.05")]
    [InlineData("북위37", "위도는 십진 도")]
    [InlineData("", "위도를 넣어")]
    public void should_reject_latitude_with_message_when_out_of_range_or_not_decimal_degrees(string text, string message)
    {
        Assert.False(GroupRotationPanelRules.TryParseLatitude(text, out _, out var err));
        Assert.Contains(message, err);
    }

    [Theory]
    [InlineData("126.96720779", true)]
    [InlineData("-180", true)]
    [InlineData("180.0001", false)]
    [InlineData("동경127", false)]
    public void should_validate_longitude_range_and_format_when_center_longitude_is_typed(string text, bool ok)
    {
        Assert.Equal(ok, GroupRotationPanelRules.TryParseLongitude(text, out _, out var err));
        Assert.Equal(ok, err == null);
    }

    // ── 한 칸 · 입력칸 글 ──

    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(0.0, -0.1, -0.1)]
    [InlineData(15.0, 0.1, 15.1)]
    [InlineData(179.5, 1.0, -179.5)]     // 180 을 넘으면 반대쪽으로 감긴다
    [InlineData(-179.5, -1.0, 179.5)]    // −180 아래도 감긴다
    [InlineData(0.1, -0.1, 0.0)]
    [InlineData(179.9, 0.1, 180.0)]
    public void should_add_step_and_wrap_to_half_turn_when_plus_minus_buttons_are_pressed(double current, double step, double expected)
        => Assert.Equal(expected, GroupRotationPanelRules.Step(current, step), 9);

    [Theory]
    [InlineData(15.0, "15.0")]
    [InlineData(-0.04, "0.0")]
    [InlineData(-12.25, "-12.3")]
    [InlineData(180.0, "180.0")]
    public void should_format_angle_input_with_one_decimal_when_session_angle_changes(double deg, string expected)
        => Assert.Equal(expected, GroupRotationPanelRules.FormatAngleInput(deg));

    [Fact]
    public void should_format_coordinate_input_like_the_map_coordinate_chip_when_center_is_shown()
    {
        Assert.Equal("37.39423226", GroupRotationPanelRules.FormatCoordinateInput(Lat));
        Assert.Equal("126.96720779", GroupRotationPanelRules.FormatCoordinateInput(Lng));
        // 입력칸 글을 다시 읽으면 같은 값(왕복)
        Assert.True(GroupRotationPanelRules.TryParseLatitude(GroupRotationPanelRules.FormatCoordinateInput(Lat), out var back, out _));
        Assert.Equal(Lat, back, 7);
    }

    // ── 요약 · 확인 글 ──

    [Fact]
    public void should_list_only_present_kinds_and_mention_locked_when_summary_is_built()
    {
        Assert.Equal("5개 · 마커 3 · 선/구역 1 · 이미지 1", GroupRotationPanelRules.KindSummary(3, 1, 0, 1, 0));
        Assert.Equal("2개 · 도형 2 (잠금 1개 제외)", GroupRotationPanelRules.KindSummary(0, 0, 2, 0, 1));
        Assert.Equal("4개 · 마커 1 · 선/구역 1 · 도형 1 · 이미지 1", GroupRotationPanelRules.KindSummary(1, 1, 1, 1, 0));
    }

    [Theory]
    [InlineData(true, false, false, "37.394232, 126.967208")]
    [InlineData(false, true, false, "52S CG 13084 42135")]
    [InlineData(true, true, true, "37.394232, 126.967208")]
    [InlineData(false, false, true, "52S 321084E 4142135N")]
    [InlineData(false, false, false, "37.394232, 126.967208")]
    public void should_pick_center_text_in_coordinate_setting_order_when_confirm_line_is_built(bool wgs, bool mgrs, bool utm, string expected)
        => Assert.Equal(expected, GroupRotationPanelRules.CenterText(Lat, Lng, "52S CG 13084 42135", "52S 321084E 4142135N", wgs, mgrs, utm));

    [Fact]
    public void should_say_count_center_and_signed_angle_when_rotation_is_pending()
    {
        Assert.Equal("5개를 (37.394232, 126.967208)에서 +15.0° 돌립니다",
            GroupRotationPanelRules.ConfirmSentence(5, "37.394232, 126.967208", 15));
        Assert.Equal("3개를 (52S CG 1 2)에서 -0.5° 돌립니다", GroupRotationPanelRules.ConfirmSentence(3, "52S CG 1 2", -0.5));
        Assert.Equal("2개 · 중심 (X) · 아직 돌리지 않았습니다", GroupRotationPanelRules.ConfirmSentence(2, "X", 0.04));
    }

    // ── 중심을 바꾼 뒤 회전 수학 ──

    [Fact]
    public void should_rotate_about_moved_center_so_that_center_stays_and_distance_is_kept_when_pivot_is_changed()
    {
        var center = (lat: Lat + 0.001, lng: Lng - 0.0015);   // 묶음 가운데가 아닌 다른 자리
        var p = (lat: Lat, lng: Lng);
        var q = GroupRotationMath.RotateAround(p, center, 90);
        // 중심 자신은 그대로
        var c2 = GroupRotationMath.RotateAround(center, center, 90);
        Assert.Equal(center.lat, c2.lat, 9);
        Assert.Equal(center.lng, c2.lng, 9);
        // 메르카토르 평면에서 중심까지 거리 유지 + 90° 돌아감
        var (px, py) = GroupRotationMath.ToMercator(p.lat, p.lng);
        var (qx, qy) = GroupRotationMath.ToMercator(q.lat, q.lng);
        var (cx, cy) = GroupRotationMath.ToMercator(center.lat, center.lng);
        double d1 = Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        double d2 = Math.Sqrt((qx - cx) * (qx - cx) + (qy - cy) * (qy - cy));
        Assert.Equal(d1, d2, 12);
        double dot = (px - cx) * (qx - cx) + (py - cy) * (qy - cy);
        Assert.Equal(0, dot / (d1 * d2), 9);
        // 다른 중심이면 결과도 다르다(중심이 실제로 쓰인다)
        var q0 = GroupRotationMath.RotateAround(p, (Lat, Lng + 0.002), 90);
        Assert.NotEqual(q.lat, q0.lat, 6);
    }
}
