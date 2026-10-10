using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.4(RPT) 중 보고서 콘솔 · 조치보고 문구 항목을 창 없이 확인한다.
               진짜 뷰모델을 가짜 API(FakeReportApiService · FakeActionReportTemplateApiService) 위에 세운다.
   Notes     : 큰 창(별도 최상위 창) · Win32 저장창 · 실제 마우스 끌기 · "?" 말풍선은 헤디드 몫이다.
****************************************************************************/
[Collection("ReportsCaliburnIoC")]
public class ReportScenarioTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    /// <summary>발행된 안내 · 확인 팝업을 모은다(진짜 EventAggregator 에 구독).</summary>
    private sealed class PopupSpy : IHandle<OpenInfoPopupMessageModel>, IHandle<OpenConfirmPopupMessageModel>
    {
        public List<OpenInfoPopupMessageModel> Infos { get; } = new();
        public List<OpenConfirmPopupMessageModel> Confirms { get; } = new();
        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken) { Infos.Add(message); return Task.CompletedTask; }
        public Task HandleAsync(OpenConfirmPopupMessageModel message, CancellationToken cancellationToken) { Confirms.Add(message); return Task.CompletedTask; }
    }

    private static async Task<(ReportConsoleViewModel Console, FakeReportApiService Api, PopupSpy Spy)> OpenReportsAsync()
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        api.Generations.Add(ReportSeed.Generation(3, "9월 정기 보고서"));
        api.Generations.Add(ReportSeed.Generation(2, "장애 요약", "GENERATING", 40));
        var failed = ReportSeed.Generation(1, "실패한 보고서", "FAILED");
        failed.ErrorMessage = "자료를 모으지 못했습니다";
        api.Generations.Add(failed);
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c"));

        var console = new ReportConsoleViewModel(
            events, log, new FakePermissionService(),
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        var spy = new PopupSpy();
        events.SubscribeOnPublishedThread(spy);
        await ((IActivate)console).ActivateAsync();
        return (console, api, spy);
    }

    private static async Task<(ActionReportTemplateConsoleViewModel Console, FakeActionReportTemplateApiService Api, PopupSpy Spy)> OpenPhrasesAsync()
    {
        var events = new EventAggregator();
        var api = new FakeActionReportTemplateApiService();
        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        api.Templates.Add(ActionReportTemplateSeed.Template(3, "오경보", 2));
        var console = new ActionReportTemplateConsoleViewModel(events, new FakeLogService(), new FakePermissionService(), api);
        var spy = new PopupSpy();
        events.SubscribeOnPublishedThread(spy);
        await ((IActivate)console).ActivateAsync();
        return (console, api, spy);
    }

    // ── RPT-003: 거름 상태 글 ──────────────────────────────────────────────────
    [Trait("Scenario", "RPT-003")]
    [Fact]
    public async Task should_name_the_pressed_status_chip_in_the_filter_summary()
    {
        var (console, _, _) = await OpenReportsAsync();
        Assert.Equal(ReportStatusChipRules.AllLabel, console.FilterSummary);

        await console.ListViewModel.ToggleStatusAsync(console.ListViewModel.StatusChips.First(c => c.Value == "COMPLETED"));

        Assert.Equal("완료", console.FilterSummary);
        Assert.All(console.ListViewModel.Rows, r => Assert.True(r.IsCompleted));
    }

    // ── RPT-007: 상세 메타 · 진행 · 실패 ─────────────────────────────────────────
    [Trait("Scenario", "RPT-007")]
    [Fact]
    public async Task should_show_meta_and_progress_for_a_running_report_and_the_reason_for_a_failed_one()
    {
        var (console, _, _) = await OpenReportsAsync();

        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 2));   // 생성 중 40%
        Assert.Equal("표준", console.PreviewViewModel.MetaTypeText);
        Assert.Equal("관리자", console.PreviewViewModel.MetaRequesterText);
        Assert.NotEqual("—", console.PreviewViewModel.MetaPeriodText);
        Assert.True(console.PreviewViewModel.HasProgress);
        Assert.Contains("40%", console.PreviewViewModel.ProgressDetailText);
        Assert.False(console.PreviewViewModel.HasFailure);

        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 1));   // 실패
        Assert.False(console.PreviewViewModel.HasProgress);
        Assert.True(console.PreviewViewModel.HasFailure);
        Assert.Equal("자료를 모으지 못했습니다", console.PreviewViewModel.FailureText);
    }

    // ── RPT-011: 내려받기 게이트 — 완료만 켜짐 ───────────────────────────────────
    [Trait("Scenario", "RPT-011")]
    [Theory]
    [InlineData(3, true)]     // 완료
    [InlineData(2, false)]    // 생성 중
    [InlineData(1, false)]    // 실패
    public async Task should_enable_downloads_only_for_a_completed_report(int id, bool expected)
    {
        var (console, _, _) = await OpenReportsAsync();

        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == id));

        Assert.Equal(expected, console.CanDownloadPdf);
    }

    // ── RPT-014: 생성 취소 — 「취소했습니다」 ─────────────────────────────────────
    [Trait("Scenario", "RPT-014")]
    [Fact]
    public async Task should_say_the_generation_was_cancelled_after_the_cancel_is_confirmed()
    {
        var (console, api, spy) = await OpenReportsAsync();
        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 2));
        Assert.True(console.CanCancelGeneration);

        await console.CancelGenerationAsync();
        Assert.Empty(api.CancelledIds);                                 // 묻기만 했다
        await console.ListViewModel.HandleAsync(new CallCancelReportGenerationProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 2 }, api.CancelledIds.ToArray());
        Assert.Equal("보고서 생성을 취소했습니다.", spy.Infos.Last().Explain);
        Assert.Equal("CANCELLED", console.ListViewModel.Items.Single(r => r.Id == 2).Dto.Status);
    }

    // ── RPT-021: 문구 창 — 검색 없음 ────────────────────────────────────────────
    [Trait("Scenario", "RPT-021")]
    [Fact]
    public void should_not_offer_a_search_box_on_the_phrase_console()
    {
        var view = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Reports.Ui", "Views", "Panels", "ActionReportTemplateConsoleView.xaml"));

        var toolbar = view[view.IndexOf("<c:ConsoleShell.Toolbar>", StringComparison.Ordinal)..view.IndexOf("</c:ConsoleShell.Toolbar>", StringComparison.Ordinal)];
        Assert.Contains("ShowSearch=\"False\"", toolbar);
    }

    // ── RPT-022: 문구 CRUD — 501자 · 고치기 · 삭제 ──────────────────────────────
    [Trait("Scenario", "RPT-022")]
    [Fact]
    public async Task should_refuse_a_501_character_phrase_and_accept_500()
    {
        var (console, api, _) = await OpenPhrasesAsync();
        await console.AddAsync();

        console.DraftContent = new string('가', 501);
        Assert.Equal("최대 500자까지 입력할 수 있습니다. (현재 501자)", console.DraftValidationError);
        Assert.False(console.DetailCanApply);
        await console.ApplyAsync();
        Assert.Empty(api.CreateCalls);

        console.DraftContent = new string('가', 500);
        Assert.Null(console.DraftValidationError);
        Assert.Equal("500 / 500자", console.DraftContentCountText);
    }

    [Trait("Scenario", "RPT-022")]
    [Fact]
    public async Task should_send_the_edited_phrase_and_show_it_in_the_list_when_an_edit_is_applied()
    {
        var (console, api, _) = await OpenPhrasesAsync();
        console.OnRowSelected(console.Items.Single(i => i.Id == 2));

        console.DraftContent = "강풍 · 폭우";
        await console.ApplyAsync();

        var (id, dto) = Assert.Single(api.UpdateCalls);
        Assert.Equal((2, "강풍 · 폭우"), (id, dto.Content));
        Assert.Equal("강풍 · 폭우", console.Items.Single(i => i.Id == 2).Content);
        Assert.False(console.Detail.IsDirty);
    }

    [Trait("Scenario", "RPT-022")]
    [Fact]
    public async Task should_confirm_then_delete_the_phrase_and_say_so()
    {
        var (console, api, spy) = await OpenPhrasesAsync();
        console.OnRowSelected(console.Items.Single(i => i.Id == 3));

        await console.DeleteAsync();
        Assert.Contains("'오경보' 문구를 삭제하시겠습니까?", spy.Confirms.Last().Explain);
        Assert.Empty(api.DeletedIds);

        await console.HandleAsync(new CallDeleteActionReportTemplateProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 3 }, api.DeletedIds.ToArray());
        Assert.DoesNotContain(console.Items, i => i.Id == 3);
        Assert.Equal("문구를 삭제했습니다.", spy.Infos.Last().Explain);
    }

    // ── RPT-026: 문구 수 ──────────────────────────────────────────────────────
    [Trait("Scenario", "RPT-026")]
    [Fact]
    public async Task should_update_the_phrase_count_when_a_phrase_is_registered()
    {
        var (console, _, _) = await OpenPhrasesAsync();
        Assert.Equal("3건", console.CountText);

        await console.AddAsync();
        console.DraftContent = "차량 통제";
        await console.ApplyAsync();

        Assert.Equal("4건", console.CountText);
    }
}
