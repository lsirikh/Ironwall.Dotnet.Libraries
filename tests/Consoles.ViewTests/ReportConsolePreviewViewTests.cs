using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Tests;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 보고서 콘솔 <b>실제 뷰</b> — '새 보고서' 레일 옆 이력에서 고른 보고서의 미리보기가 <b>화면에 보여야</b> 한다.
/// </summary>
/// <remarks>
/// 미리보기는 생성 이력 상세(<see cref="ReportGenerationDetailView"/>)에만 있다. '새 보고서' 레일의 상세 칸은 생성 폼이라,
/// 거기서 줄을 고르면 뷰모델은 HTML 을 받아 살아 있는 미리보기로 판정하는데 화면에는 폼만 남았다(2026-09-28 실창 보고).
/// 뷰모델만 보는 시험은 "미리보기 판정 = Live" 로 통과해 이 결함을 못 잡는다 — 그래서 실제 뷰의 가시성을 본다.
/// </remarks>
public class ReportConsolePreviewViewTests
{
    [Fact]
    public void should_show_the_picked_reports_detail_instead_of_the_create_form_when_a_recent_generation_is_picked_on_the_create_rail() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            RailProbe.Wait(console.SelectRailAsync(ReportConsoleRails.Create));
            AppHost.Pump();
            var grid = RailProbe.Find<DataGrid>(view, g => System.Windows.Automation.AutomationProperties.GetAutomationId(g) == "Reports.List.ReportGrid")!;
            var row = console.ListViewModel.Rows.First(r => r.Id == 3);

            grid.SelectedItem = row;                    // 사람이 줄을 누른 것과 같은 SelectionChanged 길
            for (var i = 0; i < 10; i++) AppHost.Pump(DispatcherPriority.ApplicationIdle);

            var generation = RailProbe.Find<ReportGenerationDetailView>(view, _ => true)!;
            var create = RailProbe.Find<ReportCreateDetailView>(view, _ => true)!;
            Assert.True(generation.IsVisible, "고른 보고서의 상세(미리보기)가 보여야 한다");
            Assert.False(create.IsVisible, "미리보기 자리를 생성 폼이 덮고 있으면 안 된다");
            Assert.Equal(3, (grid.SelectedItem as ReportGenerationRow)?.Id);
            Assert.Equal(ReportPreviewContent.Ready, console.PreviewViewModel.Content);
        }
        finally { window.Close(); }
    });

    /// <summary>
    /// ★ 사용자 콘솔 폭(약 1100 — 1280 미만이라 상세가 서랍)에서도 고른 보고서의 미리보기(WebView2)가 <b>실제로 보여야</b> 한다.
    /// 예전엔 서랍이라는 이유만으로 "창이 좁아…" 자리표시자였다. 칸 폭(실측 326)에 맞춰 축소해 싣는다.
    /// </summary>
    /// <remarks>화면 밖 창이지만 WebView2 를 실제로 만든다 — 이 PC 의 런타임이 필요하다(없으면 첫 단언이 그 사실을 말한다).</remarks>
    [Fact]
    public void should_show_the_live_preview_fitted_to_the_drawer_when_the_console_is_1100_wide() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole(width: 1100, runtime: WebViewRuntimeProbe.Instance);
        try
        {
            Assert.True(console.PreviewViewModel.IsRuntimeReady, "이 시험은 WebView2 런타임이 설치된 PC 가 필요하다");
            var shell = RailProbe.Find<ConsoleShell>(view, _ => true)!;
            Assert.Equal(ConsoleLayoutMode.Drawer, shell.LayoutMode);

            SelectRow(view, console, 3);

            var browser = RailProbe.Find<Microsoft.Web.WebView2.Wpf.WebView2>(view, _ => true);
            Assert.True(browser is not null, $"서랍에서도 미리보기 브라우저가 있어야 한다 · 판정='{console.PreviewViewModel.SurfaceReason}' 칸={console.PreviewViewModel.PaneWidth}");
            Assert.True(browser!.IsVisible, "미리보기 브라우저가 보여야 한다");
            var pane = console.PreviewViewModel.PaneWidth;
            Assert.True(pane >= ReportPreviewFit.MinLiveWidth, $"서랍 칸 폭 {pane}");
            Assert.Equal(ReportPreviewFit.FitZoom(pane), console.PreviewViewModel.ZoomFactor, 2);
            Assert.True(browser.ActualWidth <= pane + 0.5, $"브라우저 {browser.ActualWidth} 가 칸 {pane} 안에 있어야 한다");
        }
        finally { window.Close(); }
    });

    /// <summary>실측 — 콘솔 폭 · 상세 폭별 미리보기 칸 폭. 폭 맞춤 하한(<see cref="ReportPreviewFit.MinLiveWidth"/>)의 근거다.</summary>
    [Theory]
    [InlineData(1400, 380, 347)]   // 도킹 기본(보고서 콘솔 DetailWidth 380)
    [InlineData(1400, 300, 267)]   // 도킹 S — 가장 좁은 도킹
    [InlineData(1100, 380, 326)]   // 서랍(360, 왼쪽 굵은 선 2) — 사용자 콘솔 폭
    [InlineData(900, 380, 326)]    // 접힘 + 서랍(360)
    public void should_measure_a_preview_pane_wide_enough_for_the_fitted_page_at_every_real_console_width(double consoleWidth, double detailWidth, double expectedPane) => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole(width: consoleWidth, runtime: new FixedWebViewRuntimeProbe(false));
        try
        {
            var shell = RailProbe.Find<ConsoleShell>(view, _ => true)!;
            shell.DetailWidth = detailWidth;
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            SelectRow(view, console, 3);

            var preview = RailProbe.Find<ReportPreviewView>(view, _ => true)!;
            Assert.Equal(expectedPane, preview.ActualWidth, 0);
            Assert.Equal(expectedPane, console.PreviewViewModel.PaneWidth, 0);
            Assert.True(ReportPreviewFit.CanShowLive(preview.ActualWidth));
        }
        finally { window.Close(); }
    });

    private static void SelectRow(ReportConsoleView view, ReportConsoleViewModel console, int id)
    {
        var grid = RailProbe.Find<DataGrid>(view, g => System.Windows.Automation.AutomationProperties.GetAutomationId(g) == "Reports.List.ReportGrid")!;
        grid.SelectedItem = console.ListViewModel.Rows.First(r => r.Id == id);
        for (var i = 0; i < 10; i++) AppHost.Pump(DispatcherPriority.ApplicationIdle);
    }

    private static (ReportConsoleView View, System.Windows.Window Window, ReportConsoleViewModel Console) HostConsole(double width = 1400, IWebViewRuntimeProbe? runtime = null)
    {
        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);

        var log = new FakeLogService();
        var api = new FakeReportApiService();
        api.Generations.Add(ReportSeed.Generation(3, "9월 정기 보고서"));
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b"));

        var console = new ReportConsoleViewModel(
            events, log, new FakePermissionService { Edit = true },
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        // 화면 밖 시험에서 WebView2 를 띄우지 않는다 — 미리보기는 '런타임 없음' 안내로 그린다.
        console.PreviewViewModel.RuntimeProbe = runtime ?? new FixedWebViewRuntimeProbe(false);
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new ReportConsoleView { Width = width, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }
}
