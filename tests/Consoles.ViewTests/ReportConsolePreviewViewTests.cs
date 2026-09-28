using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Tests;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
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

    private static (ReportConsoleView View, System.Windows.Window Window, ReportConsoleViewModel Console) HostConsole()
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
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(false);
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new ReportConsoleView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }
}
