using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D 전환 안내(R5) · 영역 그리기 크기(R6) · 영역 표시(R4) · 창 열 때 2D 복귀(R7) 순수 판정.</summary>
public class Map3DGuideTests
{
    // ── R5 전환 안내 ──

    [Fact]
    public void should_use_spec_titles_when_entering_or_leaving_3d()
    {
        Assert.Equal("3D 지도로 전환합니다", Map3DGuide.EnterTitle);
        Assert.Equal("2D 지도로 돌아갑니다", Map3DGuide.LeaveTitle);
    }

    [Fact]
    public void should_keep_enter_guide_at_least_point6_seconds_when_scene_reused()
    {
        Assert.Equal(TimeSpan.FromSeconds(0.6), Map3DGuide.MinEnterShow);
        Assert.True(Map3DGuide.LeaveShow <= Map3DGuide.MinEnterShow);   // 복귀는 짧게
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(250, 350)]
    [InlineData(600, 0)]
    [InlineData(2000, 0)]
    public void should_return_remaining_time_when_guide_shown_for_elapsed(int elapsedMs, int expectedMs)
        => Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), Map3DGuide.Remaining(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(600)));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(300, 50)]
    [InlineData(600, 100)]
    [InlineData(900, 100)]
    public void should_fill_bar_by_time_when_reusing_scene(int elapsedMs, double expected)
        => Assert.Equal(expected, Map3DGuide.TimedPercent(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(600)), 6);

    [Fact]
    public void should_fill_bar_when_minimum_is_zero()
        => Assert.Equal(100d, Map3DGuide.TimedPercent(TimeSpan.Zero, TimeSpan.Zero));

    [Theory]
    [InlineData("reuse", "만들어 둔 3D 화면을 펼치는 중…")]
    [InlineData("model", "3D 장면을 받는 중…")]
    [InlineData("scene", "3D 장면 정보를 읽는 중…")]
    [InlineData("page", "3D 화면을 여는 중…")]
    [InlineData(null, "3D 화면을 여는 중…")]
    [InlineData("모름", "3D 화면을 여는 중…")]
    public void should_map_stage_text_when_progress_stage_given(string? stage, string expected)
        => Assert.Equal(expected, Map3DGuide.StageText(stage));

    [Theory]
    [InlineData(42.4, "받는 중 42 %")]
    [InlineData(150d, "받는 중 100 %")]
    [InlineData(double.NaN, "받는 중")]
    public void should_append_percent_when_progress_known(double percent, string expected)
        => Assert.Equal(expected, Map3DGuide.WithPercent("받는 중", percent));

    [Fact]
    public void should_keep_text_when_percent_unknown()
        => Assert.Equal("받는 중", Map3DGuide.WithPercent("받는 중", null));

    [Theory]
    [InlineData(false, true)]   // 토큰 없음 = 로그인 화면 뒤
    [InlineData(true, false)]
    [InlineData(null, false)]   // 토큰 저장소 미등록 = 로그인 게이트 없음
    public void should_detect_login_screen_when_token_state_known(bool? isAuthenticated, bool expected)
        => Assert.Equal(expected, Map3DGuide.IsBehindLogin(isAuthenticated));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]   // 이미 2D 로 돌아옴
    [InlineData(false, true, false)]   // 로그인 뒤에서 들어간 게 아님(이미 안내 봄)
    public void should_guide_once_after_login_when_3d_revealed_behind_login(bool revealedBehindLogin, bool isShown, bool expected)
        => Assert.Equal(expected, Map3DGuide.ShouldGuideAfterLogin(revealedBehindLogin, isShown));

    // ── R6 영역 크기 ──

    [Fact]
    public void should_measure_area_in_meters_when_bbox_given()
    {
        // 위도 0.01° ≈ 1112 m, 경도 0.01° × cos(37.005°) ≈ 888 m
        var (w, h) = Map3DGuide.AreaSizeMeters(new[] { 127.0, 37.0, 127.01, 37.01 });
        Assert.InRange(w, 886, 890);
        Assert.InRange(h, 1110, 1114);
    }

    [Fact]
    public void should_return_zero_size_when_bbox_malformed()
        => Assert.Equal((0d, 0d), Map3DGuide.AreaSizeMeters(new[] { 1.0, 2.0 }));

    [Theory]
    [InlineData(2000, 1000, false)]
    [InlineData(2000.1, 10, true)]
    [InlineData(10, 2500, true)]
    public void should_flag_too_large_when_any_side_over_2km(double w, double h, bool expected)
        => Assert.Equal(expected, Map3DGuide.IsAreaTooLarge(w, h));

    [Theory]
    [InlineData(850.4, "850 m")]
    [InlineData(1000, "1.00 km")]
    [InlineData(2345, "2.35 km")]
    public void should_format_length_when_meters_given(double m, string expected)
        => Assert.Equal(expected, Map3DGuide.FormatLength(m));

    [Fact]
    public void should_warn_amber_when_drawn_area_exceeds_2km()
    {
        // 위도 0.03° ≈ 3.3 km
        var (text, warn) = Map3DGuide.AreaDrawLabel(new[] { 127.0, 37.0, 127.005, 37.03 });
        Assert.True(warn);
        Assert.EndsWith("— 2 km 초과", text);
        Assert.Contains(" × ", text);
    }

    [Fact]
    public void should_show_plain_size_when_drawn_area_within_limit()
    {
        var (text, warn) = Map3DGuide.AreaDrawLabel(new[] { 127.0, 37.0, 127.005, 37.005 });
        Assert.False(warn);
        Assert.Equal("444 m × 556 m", text);
    }

    [Fact]
    public void should_explain_reason_when_released_area_too_large()
    {
        var text = Map3DGuide.AreaTooLargeText(3300, 500);
        Assert.Contains("3.30 km × 500 m", text);
        Assert.Contains("2 km 이하", text);
        Assert.Contains("ESC", text);
    }

    // ── R4 영역 표시 ──

    [Theory]
    [InlineData(12.5, false)]
    [InlineData(13.0, true)]
    [InlineData(18.0, true)]
    [InlineData(double.NaN, false)]
    public void should_show_area_outlines_when_zoom_13_or_more(double zoom, bool expected)
        => Assert.Equal(expected, Map3DGuide.ShouldShowAreaOutlines(zoom));

    [Theory]
    [InlineData(16.5, true, true, true)]
    [InlineData(13.0, true, true, true)]
    [InlineData(17.0, true, true, false)]    // 이미 3D 줌
    [InlineData(16.5, false, true, false)]   // 영역 밖
    [InlineData(16.5, true, false, false)]   // 3D 중 · 들어가는 중
    [InlineData(double.NaN, true, true, false)]
    public void should_show_3d_chip_when_center_inside_area_and_zoom_below_17(double zoom, bool inside, bool off, bool expected)
        => Assert.Equal(expected, Map3DGuide.ShouldShowChip(zoom, inside, off));

    [Fact]
    public void should_use_spec_chip_text()
        => Assert.Equal("3D 가능 — 줌 17 이상", Map3DGuide.ChipText);

    [Fact]
    public void should_center_view_at_area_middle_when_viewing_area()
    {
        var (lat, lon) = Map3DGuide.AreaCenter(new[] { 127.0, 37.0, 127.02, 37.01 });
        Assert.Equal(37.005, lat, 9);
        Assert.Equal(127.01, lon, 9);
        Assert.Equal(17.5, Map3DGuide.AreaViewZoom);
    }

    [Theory]
    [InlineData("a1", null, "a1")]
    [InlineData("a1", " ", "a1")]
    [InlineData("a1", "북문", "북문")]
    public void should_title_area_by_name_or_id(string id, string? name, string expected)
        => Assert.Equal(expected, Map3DGuide.AreaTitle(id, name));

    [Fact]
    public void should_list_name_date_and_buildings_when_tooltip_built()
        => Assert.Equal("북문\n만든 날짜: 2026-10-17 09:05\n건물: 42개",
                        Map3DGuide.AreaToolTip("북문", new DateTime(2026, 10, 17, 9, 5, 0), 42));

    [Fact]
    public void should_show_dash_when_tooltip_values_unknown()
        => Assert.Equal("a1\n만든 날짜: -\n건물: -", Map3DGuide.AreaToolTip("a1", null, null));
}
