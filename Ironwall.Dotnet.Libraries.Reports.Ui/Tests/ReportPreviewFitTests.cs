using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 상세 칸 미리보기의 폭 맞춤 — 칸 폭(DIU) → 배율 · 실을지 말지. 콘솔이 1280 보다 좁아(서랍) 미리보기가
/// "창이 좁아…" 자리표시자로만 뜨던 것을, 칸 폭에 맞춰 축소해 싣는 규칙이다(2026-09-28 사용자 콘솔 약 1100).
/// </summary>
public class ReportPreviewFitTests
{
    [Theory]
    [InlineData(347, 0.41)]   // 도킹 380 칸의 미리보기 폭(실측)
    [InlineData(326, 0.38)]   // 서랍 360 칸(콘솔 1100 · 900, 실측)
    [InlineData(267, 0.31)]   // 도킹 S 300 칸
    public void should_shrink_the_a4_page_to_fit_the_pane_when_the_pane_is_narrower_than_the_page(double pane, double expected)
    {
        Assert.Equal(expected, ReportPreviewFit.FitZoom(pane), 2);
        // 맞춘 배율에서 쪽 + 막대가 칸 안에 든다(가로 스크롤 없음).
        Assert.True(ReportPreviewFit.PageWidth * ReportPreviewFit.FitZoom(pane) + ReportPreviewFit.ScrollbarAllowance <= pane);
    }

    [Fact]
    public void should_not_enlarge_past_the_original_size_when_the_pane_is_wider_than_the_page()
    {
        Assert.Equal(1.0, ReportPreviewFit.FitZoom(1200));
    }

    [Fact]
    public void should_show_a_live_preview_when_the_pane_is_as_narrow_as_the_drawer()
    {
        Assert.True(ReportPreviewFit.CanShowLive(326));
        Assert.True(ReportPreviewFit.CanShowLive(267));
    }

    [Fact]
    public void should_refuse_a_live_preview_when_the_pane_is_below_the_readable_minimum()
    {
        Assert.Equal(256, ReportPreviewFit.MinLiveWidth);
        Assert.False(ReportPreviewFit.CanShowLive(255));
        Assert.True(ReportPreviewFit.CanShowLive(256));
        Assert.Equal(ReportPreviewFit.MinZoom, ReportPreviewFit.FitZoom(200));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void should_not_block_the_preview_when_the_pane_is_not_measured_yet(double pane)
    {
        Assert.True(ReportPreviewFit.CanShowLive(pane));
        Assert.Equal(1.0, ReportPreviewFit.FitZoom(pane));
    }

    [Fact]
    public void should_never_zoom_out_above_the_fitted_zoom_when_zooming_out()
    {
        // 예전 축소 하한 0.5 는 맞춤 배율(0.41)보다 커서 [−] 가 오히려 키웠다.
        Assert.Equal(0.31, ReportPreviewFit.Step(0.41, -1), 2);
        Assert.Equal(ReportPreviewFit.MinZoom, ReportPreviewFit.Step(0.31, -1), 2);
        Assert.Equal(0.51, ReportPreviewFit.Step(0.41, +1), 2);
    }

    [Fact]
    public void should_draw_a_live_browser_in_a_drawer_width_pane_when_the_report_is_ready()
    {
        var surface = ReportPreviewSurfaceRules.Resolve(326, false, false, true, ReportPreviewContent.Ready);

        Assert.True(surface.IsLive);
    }

    [Fact]
    public void should_point_at_the_large_window_when_the_pane_is_below_the_readable_minimum()
    {
        var surface = ReportPreviewSurfaceRules.Resolve(200, false, false, true, ReportPreviewContent.Ready);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.NarrowReason, surface.Reason);
        Assert.Contains("[크게 보기]", surface.Hint);
    }
}
