using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Tests;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 보고서 콘솔 <b>실제 뷰</b> — 템플릿 폼 미적용 중 레일 전환이 막히면 화면의 레일도 '템플릿'으로 돌아와야 한다.
/// </summary>
/// <remarks>
/// 뷰모델은 막았을 때 알림을 곧바로 + 한 박자 뒤 두 번 울렸다. 곧바로의 알림은 목록이 자기 선택 변경 중이라 SelectedItem 만 되돌리고,
/// 한 박자 뒤의 알림은 SelectedItem 이 이미 같은 값이라 아무 일도 하지 않았다 — 항목 컨테이너는 누른 레일에 남았다.
/// </remarks>
public class ReportConsoleRailViewTests
{
    [Fact]
    public void should_put_the_rail_selection_back_to_templates_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            RailProbe.Wait(console.SelectRailAsync(ReportConsoleRails.Template));
            AppHost.Pump();
            console.OnRowSelected(console.TemplateViewModel.Rows.First(t => t.Id == 11));
            console.EditViewModel.Name = "고친 이름";
            AppHost.Pump();
            Assert.True(console.Detail.IsDirty);

            var rail = RailProbe.Rail(view, "Console.Reports.Rail");
            Assert.Equal(ReportConsoleRails.Template, RailProbe.SelectedKey(rail));

            RailProbe.Select(rail, ReportConsoleRails.List);
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(ReportConsoleRails.Template, console.SelectedRailKey);
            Assert.Equal("고친 이름", console.EditViewModel.Name);
            Assert.True(console.Detail.IsDirty, "막힌 뒤에도 고친 칸은 남아야 한다");
            RailProbe.AssertShows(rail, ReportConsoleRails.Template, refused: ReportConsoleRails.List);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            var rail = RailProbe.Rail(view, "Console.Reports.Rail");
            RailProbe.Select(rail, ReportConsoleRails.Template);
            Assert.True(RailProbe.ItemSelected(rail, ReportConsoleRails.Template), "누른 즉시 새 레일이 선택으로 보여야 한다 · " + RailProbe.State(rail));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(ReportConsoleRails.Template, console.SelectedRailKey);
            RailProbe.AssertShows(rail, ReportConsoleRails.Template, refused: ReportConsoleRails.List);
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
        api.Templates.Add(ReportSeed.Template(12, "월간 상세", "b", "c"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c", "d"));

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
