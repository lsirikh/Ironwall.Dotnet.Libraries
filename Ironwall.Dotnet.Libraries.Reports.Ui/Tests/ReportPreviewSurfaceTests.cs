using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// ★ 공역(airspace) 판정 — 이 노드의 핵심 계약(설계 정본 L1290 · 인벤토리 §3 L30).
/// WebView2 는 네이티브 창이라 같은 창의 WPF 위에 그려진다. "언제 만들어도 되는가"를 여기서 전수 단언한다.
/// </summary>
public class ReportPreviewSurfaceTests
{
    private static ReportPreviewSurface Resolve(
        double pane = 347,                                  // 도킹 380 칸의 미리보기 폭(실측)
        bool large = false,
        bool overlay = false,
        bool runtime = true,
        ReportPreviewContent content = ReportPreviewContent.Ready)
        => ReportPreviewSurfaceRules.Resolve(pane, large, overlay, runtime, content);

    [Fact]
    public void should_draw_a_live_browser_when_docked_and_the_report_is_ready()
    {
        var surface = Resolve();

        Assert.True(surface.IsLive);
        Assert.Equal(string.Empty, surface.Reason);
    }

    [Fact]
    public void should_fall_back_to_a_placeholder_when_the_pane_is_below_the_readable_minimum()
    {
        // 2026-09-28 — 서랍 · 접힘이라는 배치만으로는 내리지 않는다(칸 폭이 기준 — ReportPreviewFitTests).
        var surface = Resolve(pane: ReportPreviewFit.MinLiveWidth - 1);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.NarrowReason, surface.Reason);
        Assert.Equal(ReportPreviewSurfaceRules.NarrowHint, surface.Hint);
    }

    [Fact]
    public void should_fall_back_to_a_placeholder_when_a_wpf_popup_is_open_over_the_same_window()
    {
        var surface = Resolve(overlay: true);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.OverlayReason, surface.Reason);
    }

    [Fact]
    public void should_fall_back_to_a_placeholder_when_the_large_window_is_showing_the_same_report()
    {
        var surface = Resolve(large: true);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.LargeViewReason, surface.Reason);
    }

    [Fact]
    public void should_explain_and_point_at_the_pdf_when_the_webview_runtime_is_missing()
    {
        var surface = Resolve(runtime: false);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.RuntimeMissingReason, surface.Reason);
        Assert.Equal(ReportPreviewSurfaceRules.RuntimeMissingHint, surface.Hint);
    }

    [Fact]
    public void should_ask_for_a_selection_first_when_no_report_is_picked()
    {
        var surface = Resolve(content: ReportPreviewContent.NoSelection);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(ReportPreviewSurfaceRules.NoSelectionReason, surface.Reason);
    }

    [Theory]
    [InlineData(ReportPreviewContent.InProgress, ReportPreviewSurfaceRules.InProgressReason)]
    [InlineData(ReportPreviewContent.Failed, ReportPreviewSurfaceRules.FailedReason)]
    [InlineData(ReportPreviewContent.Cancelled, ReportPreviewSurfaceRules.CancelledReason)]
    [InlineData(ReportPreviewContent.Loading, ReportPreviewSurfaceRules.LoadingReason)]
    public void should_name_the_reason_when_the_report_cannot_be_previewed_yet(ReportPreviewContent content, string reason)
    {
        var surface = Resolve(content: content);

        Assert.True(surface.IsPlaceholder);
        Assert.Equal(reason, surface.Reason);
    }

    [Fact]
    public void should_never_return_an_empty_reason_when_it_is_a_placeholder()
    {
        foreach (var pane in new[] { 0d, 200d, 267d, 326d, 347d })
            foreach (var large in new[] { true, false })
                foreach (var overlay in new[] { true, false })
                    foreach (var runtime in new[] { true, false })
                        foreach (ReportPreviewContent content in System.Enum.GetValues<ReportPreviewContent>())
                        {
                            var surface = Resolve(pane, large, overlay, runtime, content);
                            if (surface.IsPlaceholder)
                                Assert.False(string.IsNullOrWhiteSpace(surface.Reason));
                        }
    }

    [Fact]
    public void should_prefer_the_runtime_reason_when_several_blockers_apply_at_once()
    {
        // 런타임이 없으면 폭 · 팝업을 고쳐도 소용이 없다 — 가장 근본적인 까닭을 먼저 말한다.
        // 이 조합은 실제로 일어난다: 프로브가 활성화 때 한 번 판정하므로 좁은 창에서도 runtime=false 가 온다.
        var surface = Resolve(pane: 200, large: true, overlay: true, runtime: false);

        Assert.Equal(ReportPreviewSurfaceRules.RuntimeMissingReason, surface.Reason);
    }

    [Fact]
    public void should_allow_the_large_window_for_a_finished_report_whatever_the_layout_is()
    {
        // 큰 창은 자체 HWND 라 공역 제약을 받지 않는다 — 그것이 이 창의 존재 이유다.
        // (판정에 폭이 들어가지 않는다는 사실 자체가 요점이라, 폭별 표면 판정과 함께 본다.)
        Assert.True(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.Ready));

        foreach (var pane in new[] { 0d, 200d, 326d, 347d })
            Assert.True(Resolve(pane).IsLive || Resolve(pane).IsPlaceholder);   // 언제나 판정이 나온다
    }

    [Fact]
    public void should_refuse_the_large_window_while_it_is_still_loading()
    {
        // 다 받기 전에 열면 빈 창이 뜨고 상세 칸 미리보기까지 내려간다.
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.Loading));
    }

    [Fact]
    public void should_refuse_the_large_window_when_the_report_is_not_finished()
    {
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.InProgress));
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.Failed));
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.NoSelection));
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: true, ReportPreviewContent.Cancelled));
    }

    [Fact]
    public void should_refuse_the_large_window_when_the_webview_runtime_is_missing()
    {
        Assert.False(ReportPreviewSurfaceRules.CanOpenLargeView(isRuntimeReady: false, ReportPreviewContent.Ready));
    }
}
