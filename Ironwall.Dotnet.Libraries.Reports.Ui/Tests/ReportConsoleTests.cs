using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// Caliburn 의 <b>정적</b> IoC 델리게이트를 시험 동안만 설치하고 끝나면 되돌린다(Devices.Ui 선례).
/// </summary>
internal sealed class ReportIoCScope : IDisposable
{
    private readonly Func<Type, string, object> _getInstance;
    private readonly Func<Type, IEnumerable<object>> _getAllInstances;
    private readonly Action<object> _buildUp;

    public ReportIoCScope()
    {
        _getInstance = IoC.GetInstance;
        _getAllInstances = IoC.GetAllInstances;
        _buildUp = IoC.BuildUp;

        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
        IoC.GetAllInstances = _ => Enumerable.Empty<object>();
        IoC.BuildUp = _ => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = _getInstance;
        IoC.GetAllInstances = _getAllInstances;
        IoC.BuildUp = _buildUp;
    }
}

/// <summary>
/// 보고서 콘솔 뷰모델 — 레일 3 · 선택에서 미리보기 · 이동 차단 · 템플릿 적용/되돌리기 · 삭제 확인 · 공역 게이트.
/// 진짜 뷰모델 다섯을 가짜 API 위에 세워 본다(설계 정본 L1250-1295).
/// </summary>
[Collection("ReportsCaliburnIoC")]
public class ReportConsoleTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private sealed class Rig
    {
        public required ReportConsoleViewModel Console { get; init; }
        public required FakeReportApiService Api { get; init; }
        public required FakePermissionService Permission { get; init; }
        public required EventAggregator Events { get; init; }
    }

    private static async Task<Rig> OpenAsync(Action<FakeReportApiService>? seed = null, bool canEdit = true,
                                             Action<FakePermissionService>? permission_ = null)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        var permission = new FakePermissionService { Edit = canEdit };
        permission_?.Invoke(permission);

        api.Generations.Add(ReportSeed.Generation(3, "9월 정기 보고서"));
        api.Generations.Add(ReportSeed.Generation(2, "장애 요약", "GENERATING", 40));
        api.Generations.Add(ReportSeed.Generation(1, "실패한 보고서", "FAILED"));
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b"));
        api.Templates.Add(ReportSeed.Template(12, "월간 상세", "b", "c"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c", "d"));
        seed?.Invoke(api);

        var console = new ReportConsoleViewModel(
            events, log, permission,
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));

        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        await ((IActivate)console).ActivateAsync();
        return new Rig { Console = console, Api = api, Permission = permission, Events = events };
    }

    #region - 레일 -
    [Fact]
    public async Task should_show_three_rails_in_the_mockup_order_when_opened()
    {
        var rig = await OpenAsync();

        Assert.Equal(3, rig.Console.RailEntries.Count);
        Assert.Equal(new[] { "list", "create", "template" }, rig.Console.RailEntries.Select(r => r.Key).ToArray());
        Assert.Equal(ReportConsoleRails.List, rig.Console.SelectedRailKey);
        Assert.True(rig.Console.IsListRail);
    }

    [Fact]
    public async Task should_badge_the_history_rail_with_the_number_still_running()
    {
        var rig = await OpenAsync();

        var listRail = rig.Console.RailEntries.First(r => r.Key == ReportConsoleRails.List);
        Assert.True(listRail.ShowCount);
        Assert.Equal(1, listRail.Count);      // GENERATING 한 건
    }

    [Fact]
    public async Task should_reload_the_template_options_every_time_the_create_rail_is_opened()
    {
        // ★ 탭 호스트의 "콘솔 연 순간 스냅샷" 결함 재발 방지 — 레일 전환이 곧 활성화다.
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Api.Templates.Add(ReportSeed.Template(13, "새로 만든 템플릿", "a"));

        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);

        Assert.Contains(rig.Console.CreateViewModel.Templates, t => t.Id == 13);
    }

    [Fact]
    public async Task should_swap_the_columns_when_the_rail_changes()
    {
        var rig = await OpenAsync();
        Assert.Same(ReportColumnCatalog.Generations, rig.Console.Columns);

        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);

        Assert.Same(ReportColumnCatalog.Templates, rig.Console.Columns);
        Assert.Equal(2, rig.Console.TemplateViewModel.Rows.Count);
    }

    [Fact]
    public async Task should_show_recent_generations_beside_the_form_when_the_create_rail_is_open()
    {
        var rig = await OpenAsync();

        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);

        Assert.True(rig.Console.HasListCaption);
        Assert.Equal(3, rig.Console.ListViewModel.Rows.Count);
    }
    #endregion

    #region - 선택 → 미리보기 -
    [Fact]
    public async Task should_load_the_preview_into_the_detail_pane_when_a_report_is_selected()
    {
        var rig = await OpenAsync();
        var row = rig.Console.ListViewModel.Rows.First(r => r.Id == 3);

        Assert.True(rig.Console.OnRowSelected(row));

        Assert.Equal(1, rig.Console.Detail.SelectedCount);
        Assert.Equal("9월 정기 보고서", rig.Console.Detail.SingleTitle);
        Assert.Equal(3, rig.Console.PreviewViewModel.GenerationId);
        Assert.True(rig.Console.PreviewViewModel.HasHtml);
        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);
    }

    [Fact]
    public async Task should_not_fetch_html_when_the_selected_report_is_not_finished()
    {
        var rig = await OpenAsync();
        var running = rig.Console.ListViewModel.Rows.First(r => r.Id == 2);

        rig.Console.OnRowSelected(running);

        Assert.False(rig.Console.PreviewViewModel.HasHtml);
        Assert.Equal(ReportPreviewContent.InProgress, rig.Console.PreviewViewModel.Content);
        Assert.False(rig.Console.PreviewViewModel.IsSurfaceLive);
    }

    [Fact]
    public async Task should_clear_the_preview_when_the_selection_is_dropped()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        rig.Console.OnRowSelected(null);

        Assert.Equal(0, rig.Console.Detail.SelectedCount);
        Assert.Equal(ReportPreviewContent.NoSelection, rig.Console.PreviewViewModel.Content);
    }

    [Fact]
    public async Task should_offer_the_destructive_actions_only_for_what_the_server_allows()
    {
        var rig = await OpenAsync();

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));   // 완료
        Assert.True(rig.Console.CanDeleteGeneration);
        Assert.False(rig.Console.CanCancelGeneration);                                      // 완료는 취소 불가

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));   // 생성중
        Assert.True(rig.Console.CanCancelGeneration);
    }

    [Fact]
    public async Task should_hide_the_apply_bar_on_the_history_rail_because_it_is_a_preview()
    {
        var rig = await OpenAsync();

        Assert.False(rig.Console.ShowDetailButtons);

        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        Assert.True(rig.Console.ShowDetailButtons);
    }
    #endregion

    #region - ★ 공역(airspace) -
    [Fact]
    public async Task should_drop_the_preview_to_a_placeholder_when_the_console_becomes_a_drawer()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);

        rig.Console.LayoutMode = ConsoleLayoutMode.Drawer;

        Assert.False(rig.Console.PreviewViewModel.IsSurfaceLive);
        Assert.Equal(ReportPreviewSurfaceRules.NarrowReason, rig.Console.PreviewViewModel.SurfaceReason);
    }

    [Fact]
    public async Task should_drop_the_preview_while_a_blocking_popup_is_open_and_restore_it_after()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        using (rig.Console.Block())
        {
            Assert.False(rig.Console.PreviewViewModel.IsSurfaceLive);
            Assert.Equal(ReportPreviewSurfaceRules.OverlayReason, rig.Console.PreviewViewModel.SurfaceReason);
        }

        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);
    }

    [Fact]
    public async Task should_keep_the_preview_down_until_the_outermost_popup_is_closed()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        var outer = rig.Console.Block();
        var inner = rig.Console.Block();
        inner.Dispose();

        Assert.False(rig.Console.PreviewViewModel.IsSurfaceLive);
        outer.Dispose();
        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);
    }

    [Fact]
    public async Task should_drop_the_preview_while_the_large_window_shows_the_same_report()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        var asked = 0;
        rig.Console.LargePreviewRequested += _ => asked++;

        rig.Console.OpenLargePreview();

        Assert.Equal(1, asked);
        Assert.False(rig.Console.PreviewViewModel.IsSurfaceLive);

        rig.Console.OnLargePreviewClosed();
        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);
    }

    [Fact]
    public async Task should_not_advertise_the_large_window_when_the_runtime_is_missing_even_in_a_narrow_console()
    {
        // 좁은 창에서는 살아 있는 WebView2 를 만들지 않으므로, 시도로는 런타임 유무를 영영 알 수 없다.
        var rig = await OpenAsync();
        rig.Console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(false);
        rig.Console.PreviewViewModel.ProbeRuntime();
        rig.Console.LayoutMode = ConsoleLayoutMode.Compact;
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        Assert.False(rig.Console.PreviewViewModel.CanOpenLargeView);
        Assert.Equal(ReportPreviewSurfaceRules.RuntimeMissingReason, rig.Console.PreviewViewModel.SurfaceReason);
    }

    [Fact]
    public async Task should_not_offer_a_second_large_window_while_one_is_open()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        var opened = 0;
        var activated = 0;
        rig.Console.LargePreviewRequested += _ => opened++;
        rig.Console.LargePreviewActivateRequested += () => activated++;

        rig.Console.OpenLargePreview();
        rig.Console.OpenLargePreview();

        Assert.Equal(1, opened);
        Assert.Equal(1, activated);
        Assert.False(rig.Console.PreviewViewModel.CanOpenLargeView);
    }

    [Fact]
    public async Task should_not_open_the_large_window_for_a_report_that_is_not_finished()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));
        var asked = 0;
        rig.Console.LargePreviewRequested += _ => asked++;

        rig.Console.OpenLargePreview();

        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task should_block_the_airspace_before_publishing_the_delete_confirmation()
    {
        // 확인 창은 WPF 다 — 미리보기(네이티브)가 그 위에 그려지면 누를 수조차 없다.
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        var liveWhenAsked = true;
        rig.Events.SubscribeOnPublishedThread(new ConfirmSpy(() => liveWhenAsked = rig.Console.PreviewViewModel.IsSurfaceLive));

        await rig.Console.DeleteGenerationAsync();

        Assert.False(liveWhenAsked);
    }

    private sealed class ConfirmSpy : IHandle<OpenConfirmPopupMessageModel>
    {
        private readonly System.Action _onConfirm;
        public ConfirmSpy(System.Action onConfirm) => _onConfirm = onConfirm;
        public Task HandleAsync(OpenConfirmPopupMessageModel message, CancellationToken cancellationToken)
        {
            _onConfirm();
            return Task.CompletedTask;
        }
    }
    #endregion

    #region - 삭제 · 취소(확인 팝업 경로) -
    [Fact]
    public async Task should_not_delete_anything_until_the_confirmation_is_answered()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        await rig.Console.DeleteGenerationAsync();

        Assert.Empty(rig.Api.DeletedGenerationIds);
        Assert.Equal(3, rig.Console.ListViewModel.Items.Count);
    }

    [Fact]
    public async Task should_delete_and_keep_the_airspace_blocked_until_the_result_popup_closes()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();

        await rig.Console.ListViewModel.HandleAsync(new CallDeleteReportGenerationProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 3 }, rig.Api.DeletedGenerationIds.ToArray());
        Assert.DoesNotContain(rig.Console.ListViewModel.Items, r => r.Id == 3);
        // 결과 안내 팝업이 아직 떠 있다 — 그 위로 미리보기가 올라오면 안 된다.
        Assert.True(rig.Console.PreviewViewModel.IsOverlayOpen);

        await rig.Events.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        Assert.False(rig.Console.PreviewViewModel.IsOverlayOpen);
    }

    [Fact]
    public async Task should_release_the_airspace_when_the_confirmation_is_cancelled()
    {
        // 호스트의 확인 팝업은 [확인] 에서만 우리 메시지를 낸다 — [취소] 는 ClosePopup 만 낸다.
        // 그 길에서 풀지 않으면 미리보기가 세션 내내 자리표시자로 굳는다.
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();
        Assert.True(rig.Console.PreviewViewModel.IsOverlayOpen);

        await rig.Events.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());

        Assert.False(rig.Console.PreviewViewModel.IsOverlayOpen);
        Assert.True(rig.Console.PreviewViewModel.IsSurfaceLive);
        Assert.Empty(rig.Api.DeletedGenerationIds);
    }

    [Fact]
    public async Task should_block_the_airspace_again_in_a_second_session_after_a_cancelled_confirmation()
    {
        // 취소로 끝난 표가 남은 채 콘솔을 닫으면, 다음 세션의 첫 확인 창이 아예 안 잠긴다(원래 결함).
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();
        await ((IDeactivate)rig.Console).DeactivateAsync(close: true);

        await ((IActivate)rig.Console).ActivateAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();

        Assert.True(rig.Console.PreviewViewModel.IsOverlayOpen);
    }

    [Fact]
    public async Task should_release_the_airspace_even_when_the_confirmation_finds_nothing_selected()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();
        rig.Console.ListViewModel.SelectedItem = null;
        rig.Console.ListViewModel.ReleaseAirspace();   // 대상이 사라졌다 — 스스로 내려놓는다

        await rig.Console.ListViewModel.HandleAsync(new CallDeleteReportGenerationProcessMessageModel(), CancellationToken.None);

        await rig.Events.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        Assert.False(rig.Console.PreviewViewModel.IsOverlayOpen);
    }

    [Fact]
    public async Task should_delete_the_report_that_was_shown_in_the_confirmation_not_the_newly_selected_one()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));
        await rig.Console.DeleteGenerationAsync();

        // 확인 창이 떠 있는 동안 선택이 바뀌었다.
        rig.Console.ListViewModel.SelectedItem = rig.Console.ListViewModel.Rows.First(r => r.Id == 1);
        await rig.Console.ListViewModel.HandleAsync(new CallDeleteReportGenerationProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 3 }, rig.Api.DeletedGenerationIds.ToArray());
    }

    [Fact]
    public async Task should_refuse_to_delete_while_the_template_form_has_unapplied_changes()
    {
        // 삭제는 선택을 없애는 이동이다 — 막지 않으면 장부만 더러운 채 선택이 0 이 되어 막다른 골목이 된다.
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        await rig.Console.DeleteAsync();

        Assert.Empty(rig.Api.DeletedTemplateIds);
        Assert.Equal(ConsoleDetailStateMachine.BlockedNotice, rig.Console.Detail.FooterText);
        Assert.True(rig.Console.Detail.IsDirty);
    }

    [Fact]
    public async Task should_cancel_the_generation_when_the_confirmation_is_accepted()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));
        await rig.Console.CancelGenerationAsync();

        await rig.Console.ListViewModel.HandleAsync(new CallCancelReportGenerationProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 2 }, rig.Api.CancelledIds.ToArray());
    }
    #endregion

    #region - 상태 칩 · 검색 -
    [Fact]
    public async Task should_send_no_status_parameter_when_the_pressed_chip_is_pressed_again()
    {
        var rig = await OpenAsync();
        var completed = rig.Console.ListViewModel.StatusChips.First(c => c.Value == "COMPLETED");

        await rig.Console.ListViewModel.ToggleStatusAsync(completed);
        Assert.Equal("COMPLETED", rig.Api.LastStatusFilter);

        await rig.Console.ListViewModel.ToggleStatusAsync(completed);
        Assert.Null(rig.Api.LastStatusFilter);
    }

    [Fact]
    public async Task should_filter_rows_without_calling_the_server_when_searching()
    {
        var rig = await OpenAsync();
        var callsBefore = rig.Api.StatusFilterCallCount;

        rig.Console.SearchText = "정기";

        Assert.Single(rig.Console.ListViewModel.Rows);
        Assert.Equal(callsBefore, rig.Api.StatusFilterCallCount);
    }

    [Fact]
    public async Task should_drop_a_selection_that_the_search_has_hidden()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 1));

        rig.Console.SearchText = "정기";

        Assert.Null(rig.Console.ListViewModel.SelectedItem);
    }

    [Fact]
    public async Task should_search_templates_by_name_when_the_template_rail_is_open()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);

        rig.Console.SearchText = "월간";

        Assert.Single(rig.Console.TemplateViewModel.Rows);
    }
    #endregion

    #region - 템플릿 편집(오른쪽 칸) -
    [Fact]
    public async Task should_fill_the_right_pane_with_the_full_configuration_when_a_template_is_selected()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);

        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        Assert.Equal("주간 요약", rig.Console.EditViewModel.Name);
        Assert.Equal(new[] { "a", "b", "c", "d" }, rig.Console.EditViewModel.Board.Items.Select(i => i.Id).ToArray());
        Assert.Equal(2, rig.Console.EditViewModel.Board.EnabledCount);
        Assert.False(rig.Console.Detail.IsDirty);
    }

    [Fact]
    public async Task should_send_only_the_touched_fields_when_applied()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.EditViewModel.Name = "주간 요약 (개정)";
        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastUpdate);
        Assert.Equal("주간 요약 (개정)", rig.Api.LastUpdate!.Name);
        Assert.Null(rig.Api.LastUpdate.Description);
        Assert.Null(rig.Api.LastUpdate.DefaultPeriod);
        Assert.Null(rig.Api.LastUpdate.Components);
    }

    [Fact]
    public async Task should_leave_the_period_unselected_when_the_template_has_no_default_period()
    {
        // 서버 default_period 는 nullable 이다 — 없는 기간을 "최근 7일" 로 꾸며 고른 척하지 않는다.
        var rig = await OpenAsync(api => api.Templates.First(t => t.Id == 11).DefaultPeriod = null);
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);

        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        Assert.Null(rig.Console.EditViewModel.SelectedPeriod);
        Assert.False(rig.Console.Detail.IsDirty);
    }

    [Fact]
    public async Task should_not_send_default_period_when_only_the_name_changes_on_a_template_without_one()
    {
        var rig = await OpenAsync(api => api.Templates.First(t => t.Id == 11).DefaultPeriod = null);
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.EditViewModel.Name = "주간 요약 (개정)";
        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastUpdate);
        Assert.Null(rig.Api.LastUpdate!.DefaultPeriod);
        Assert.DoesNotContain("default_period", Newtonsoft.Json.JsonConvert.SerializeObject(rig.Api.LastUpdate));
        Assert.Null(rig.Api.Templates.First(t => t.Id == 11).DefaultPeriod);   // 서버 값은 그대로 null
    }

    [Fact]
    public async Task should_send_the_chosen_period_when_the_user_picks_one_for_a_template_without_one()
    {
        // 기준값을 "7d" 로 채워 두면 사용자가 7일을 골라도 "손댄 것 없음" 으로 삼켜진다.
        var rig = await OpenAsync(api => api.Templates.First(t => t.Id == 11).DefaultPeriod = null);
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.EditViewModel.SelectedPeriod = rig.Console.EditViewModel.Periods.First(p => p.Value == "7d");
        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastUpdate);
        Assert.Equal("7d", rig.Api.LastUpdate!.DefaultPeriod);
    }

    [Fact]
    public async Task should_show_the_server_cancel_reason_in_the_create_status_when_the_generation_is_cancelled()
    {
        var rig = await OpenAsync(api =>
        {
            api.GeneratedStatus = "CANCELLED";
            api.GeneratedErrorMessage = "사용자 admin 가 취소했습니다";
        });
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "제목";

        await rig.Console.ApplyAsync();

        Assert.Equal("취소됨: 사용자 admin 가 취소했습니다", rig.Console.CreateViewModel.StatusText);
    }

    [Fact]
    public async Task should_show_the_cancel_reason_in_the_detail_pane_when_a_cancelled_generation_is_selected()
    {
        var rig = await OpenAsync(api =>
        {
            var cancelled = ReportSeed.Generation(4, "취소된 보고서", "CANCELLED");
            cancelled.ErrorMessage = "사용자 admin 가 취소했습니다";
            api.Generations.Add(cancelled);
        });

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 4));

        Assert.True(rig.Console.PreviewViewModel.HasFailure);
        Assert.Equal("사용자 admin 가 취소했습니다", rig.Console.PreviewViewModel.FailureText);
    }

    [Fact]
    public async Task should_never_send_is_public_because_the_server_does_not_enforce_it()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.EditViewModel.Name = "고친 이름";
        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastUpdate!.IsPublic);
    }

    [Fact]
    public async Task should_send_the_reordered_components_in_a_single_patch_when_applied()
    {
        // ★ 드래그 1회가 N 왕복이 되면 안 된다 — 순서는 [적용] 한 번에 PATCH 1건으로 나간다.
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.EditViewModel.Board.Move(new[] { 1 }, 0);    // b 를 맨 앞으로
        Assert.True(rig.Console.Detail.IsDirty);

        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastUpdate!.Components);
        Assert.Equal(new[] { "b", "a" }, rig.Api.LastUpdate.Components!.Select(c => c.Id).ToArray());
        Assert.Equal(new[] { 0, 1 }, rig.Api.LastUpdate.Components.Select(c => c.Order).ToArray());
        Assert.Null(rig.Api.LastUpdate.Name);                     // 이름은 손대지 않았다
    }

    [Fact]
    public async Task should_settle_the_dirty_state_when_the_template_is_saved()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        await rig.Console.ApplyAsync();

        Assert.False(rig.Console.Detail.IsDirty);
        Assert.Equal(0, rig.Console.Detail.Tracker.Count);
    }

    [Fact]
    public async Task should_keep_the_changes_when_the_server_refuses_the_save()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";
        rig.Api.FailWrite = true;

        await rig.Console.ApplyAsync();

        Assert.True(rig.Console.Detail.IsDirty);
        Assert.Equal("고친 이름", rig.Console.EditViewModel.Name);
        Assert.Contains("저장 실패", rig.Console.EditViewModel.StatusText);
    }

    [Fact]
    public async Task should_restore_the_original_values_when_reverted()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";
        rig.Console.EditViewModel.Board.Move(new[] { 1 }, 0);

        rig.Console.Revert();

        Assert.Equal("주간 요약", rig.Console.EditViewModel.Name);
        Assert.Equal(new[] { "a", "b" }, rig.Console.EditViewModel.Board.ToConfig().Select(c => c.Id).ToArray());
        Assert.False(rig.Console.Detail.IsDirty);
    }

    [Fact]
    public async Task should_register_a_new_template_in_place_without_a_dialog()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);

        await rig.Console.AddAsync();
        Assert.Equal(ConsoleDetailState.Create, rig.Console.Detail.State);
        Assert.Equal("등록", rig.Console.DetailApplyText);

        rig.Console.EditViewModel.Name = "새 템플릿";
        rig.Console.EditViewModel.Board.Items.First(i => i.Id == "a").IsEnabled = true;
        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastCreate);
        Assert.Equal("새 템플릿", rig.Api.LastCreate!.Name);
        Assert.Equal("CUSTOM", rig.Api.LastCreate.ReportType);
        Assert.Contains(rig.Console.TemplateViewModel.Items, t => t.Name == "새 템플릿");
    }

    [Fact]
    public async Task should_refuse_a_new_template_with_no_components()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        await rig.Console.AddAsync();
        rig.Console.EditViewModel.Name = "구성 없는 템플릿";

        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastCreate);
        Assert.Contains("구성 요소", rig.Console.EditViewModel.StatusText);
    }

    [Fact]
    public async Task should_refresh_the_create_options_when_a_template_is_deleted()
    {
        // 안 하면 지운 템플릿으로 생성할 수 있다(실제로 있었던 결함).
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        await rig.Console.TemplateViewModel.Delete();

        await rig.Console.TemplateViewModel.HandleAsync(new CallDeleteReportTemplateProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { 11 }, rig.Api.DeletedTemplateIds.ToArray());
        Assert.DoesNotContain(rig.Console.CreateViewModel.Templates, t => t.Id == 11);
    }
    #endregion

    #region - 이동 차단(미적용 변경) -
    [Fact]
    public async Task should_block_row_selection_when_the_template_form_has_unapplied_changes()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        var moved = rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 12));

        Assert.False(moved);
        Assert.Equal(11, rig.Console.TemplateViewModel.SelectedItem!.Id);
        Assert.Equal(ConsoleDetailStateMachine.BlockedNotice, rig.Console.Detail.FooterText);
    }

    [Fact]
    public async Task should_block_the_rail_switch_when_the_template_form_has_unapplied_changes()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        rig.Console.SelectedRail = rig.Console.RailEntries.First(r => r.Key == ReportConsoleRails.List);

        Assert.Equal(ReportConsoleRails.Template, rig.Console.SelectedRailKey);
    }

    [Fact]
    public async Task should_let_the_search_through_even_with_unapplied_changes()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        rig.Console.SearchText = "월간";

        // 검색은 막지 않지만(커널 계약), 고치던 줄을 밀어내 미적용 변경을 삼키지도 않는다.
        Assert.True(rig.Console.Detail.IsDirty);
        Assert.Contains(rig.Console.TemplateViewModel.Rows, t => t.Id == 11);   // 고치던 줄은 검색 밖이어도 남는다
        Assert.Contains(rig.Console.TemplateViewModel.Rows, t => t.Id == 12);
        Assert.Equal(11, rig.Console.TemplateViewModel.SelectedItem!.Id);
    }

    [Fact]
    public async Task should_let_the_search_hide_a_template_that_has_no_unapplied_changes()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));

        rig.Console.SearchText = "월간";

        Assert.Single(rig.Console.TemplateViewModel.Rows);
        Assert.Null(rig.Console.TemplateViewModel.SelectedItem);
    }

    [Fact]
    public async Task should_move_again_once_the_changes_are_reverted()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";
        rig.Console.Revert();

        Assert.True(rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 12)));
    }
    #endregion

    #region - 생성 폼 -
    [Fact]
    public async Task should_refuse_to_generate_when_the_title_is_empty()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);

        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastGenerate);
        Assert.Contains("제목", rig.Console.CreateViewModel.StatusText);
    }

    [Fact]
    public async Task should_refuse_to_generate_when_a_template_is_required_but_not_chosen()
    {
        var rig = await OpenAsync(api => api.Templates.Clear());
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "제목";
        rig.Console.CreateViewModel.IsTemplateBased = true;

        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastGenerate);
        Assert.Contains("템플릿", rig.Console.CreateViewModel.StatusText);
    }

    [Fact]
    public async Task should_refuse_to_generate_when_the_end_date_is_before_the_start_date()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "제목";
        rig.Console.CreateViewModel.IsCustomRange = true;
        rig.Console.CreateViewModel.StartDate = new DateTime(2026, 9, 10);
        rig.Console.CreateViewModel.EndDate = new DateTime(2026, 9, 1);

        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastGenerate);
        Assert.Contains("끝일", rig.Console.CreateViewModel.StatusText);
    }

    [Fact]
    public async Task should_send_no_severity_key_when_no_severity_is_picked()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "제목";

        await rig.Console.ApplyAsync();

        Assert.NotNull(rig.Api.LastGenerate);
        Assert.Null(rig.Api.LastGenerate!.SeverityFilter);
        Assert.Equal("STANDARD", rig.Api.LastGenerate.ReportType);
    }

    [Fact]
    public async Task should_keep_the_apply_bar_off_until_the_form_has_a_title()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);

        Assert.False(rig.Console.DetailCanApply);
        Assert.Equal("생성", rig.Console.DetailApplyText);
        Assert.Equal("비우기", rig.Console.DetailRevertText);

        rig.Console.CreateViewModel.Title = "제목";
        Assert.True(rig.Console.DetailCanApply);
    }

    [Fact]
    public async Task should_empty_the_form_when_the_revert_button_is_pressed()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "제목";
        rig.Console.CreateViewModel.Severities[0].IsSelected = true;

        rig.Console.Revert();

        Assert.True(string.IsNullOrEmpty(rig.Console.CreateViewModel.Title));
        Assert.DoesNotContain(rig.Console.CreateViewModel.Severities, s => s.IsSelected);
    }
    #endregion

    #region - 권한 · 수명주기 -
    [Fact]
    public async Task should_open_read_only_and_disable_writing_when_the_user_cannot_edit()
    {
        var rig = await OpenAsync(canEdit: false);

        Assert.True(rig.Console.Detail.IsReadOnly);
        Assert.False(rig.Console.CanAdd);
        Assert.False(rig.Console.CanDelete);
        Assert.False(rig.Console.EditViewModel.CanEdit);
        Assert.Contains("권한", rig.Console.AddBlockedReason);
    }

    [Fact]
    public async Task should_disable_delete_and_cancel_when_the_user_can_edit_but_not_delete()
    {
        // 서버는 템플릿 · 생성 이력 삭제와 생성 취소를 reports:delete 로 거른다(routers/reports.py) —
        // edit 만 있는 사용자에게 켜 두면 눌러도 403 이다(라이브 하네스 rv.gen.1 실측).
        var rig = await OpenAsync(permission_: p => p.Delete = false);

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));   // 완료
        Assert.False(rig.Console.CanDeleteGeneration);
        Assert.False(rig.Console.CanDelete);
        Assert.Equal("지울 권한이 없습니다.", rig.Console.DeleteBlockedReason);

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));   // 생성중
        Assert.False(rig.Console.CanCancelGeneration);

        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        Assert.False(rig.Console.CanDelete);

        // 편집은 그대로 열려 있다 — delete 가 없다고 edit 까지 막지 않는다.
        Assert.True(rig.Console.CanEditReports);
        Assert.False(rig.Console.Detail.IsReadOnly);
    }

    [Fact]
    public async Task should_refuse_delete_and_cancel_at_the_view_model_when_delete_permission_is_missing()
    {
        var rig = await OpenAsync(permission_: p => p.Delete = false);
        var confirmations = 0;
        rig.Events.SubscribeOnPublishedThread(new ConfirmSpy(() => confirmations++));

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));   // 생성중
        await rig.Console.CancelGenerationAsync();
        await rig.Console.DeleteGenerationAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        await rig.Console.DeleteAsync();

        // 확인 창조차 뜨지 않는다 — 떴다면 [확인] 한 번에 서버 403 이 된다.
        Assert.Equal(0, confirmations);
        Assert.Empty(rig.Api.CancelledIds);
        Assert.Empty(rig.Api.DeletedGenerationIds);
        Assert.Empty(rig.Api.DeletedTemplateIds);
    }

    [Fact]
    public async Task should_enable_delete_and_cancel_when_the_user_has_delete_without_edit()
    {
        // 게이트는 한 동사만 본다 — delete 만 있어도 지우기 · 취소는 서버가 받는다.
        var rig = await OpenAsync(permission_: p => { p.Edit = false; p.Delete = true; });

        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 2));   // 생성중
        Assert.True(rig.Console.CanCancelGeneration);
        Assert.True(rig.Console.CanDeleteGeneration);
        Assert.False(rig.Console.CanAdd);
    }

    [Fact]
    public async Task should_follow_the_permission_service_when_rights_change_mid_session()
    {
        var rig = await OpenAsync(canEdit: false);

        rig.Permission.Edit = true;
        rig.Permission.RaiseChanged();

        Assert.False(rig.Console.Detail.IsReadOnly);
        Assert.True(rig.Console.CanAdd);
    }

    [Fact]
    public async Task should_not_load_or_download_anything_without_view_permission()
    {
        var rig = await OpenAsync(permission_: p => { p.View = false; p.Edit = false; });

        Assert.Empty(rig.Console.ListViewModel.Items);
        Assert.Equal(0, rig.Api.StatusFilterCallCount);
        Assert.Contains("권한", rig.Console.ListViewModel.EmptyStateText);

        await rig.Console.DownloadPdfAsync();
        await rig.Console.DownloadCsvAsync();
        // 내려받기 경로가 열리지 않았다 — 단추를 끄는 것만으로는 뷰모델 경로가 막히지 않는다.
        Assert.Null(rig.Console.ListViewModel.ActionStatus);
    }

    [Fact]
    public async Task should_refuse_to_apply_a_template_at_the_view_model_when_editing_is_not_allowed()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        rig.Permission.Edit = false;
        rig.Permission.RaiseChanged();
        await rig.Console.ApplyAsync();

        Assert.Null(rig.Api.LastUpdate);
        Assert.False(rig.Console.DetailCanApply);
    }

    [Fact]
    public async Task should_let_the_rail_switch_through_when_only_the_create_form_is_filled()
    {
        // 생성 폼은 장부에 올리지 않는다 — 입력이 사라지지 않으므로 막을 까닭이 없다.
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Create);
        rig.Console.CreateViewModel.Title = "쓰던 제목";

        await rig.Console.SelectRailAsync(ReportConsoleRails.List);

        Assert.Equal(ReportConsoleRails.List, rig.Console.SelectedRailKey);
        Assert.Equal("쓰던 제목", rig.Console.CreateViewModel.Title);
    }

    [Fact]
    public async Task should_block_add_and_reload_while_the_template_form_is_dirty()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        await rig.Console.AddAsync();
        Assert.False(rig.Console.EditViewModel.IsCreate);

        var before = rig.Api.TemplateListCallCount;
        await rig.Console.ReloadAsync();
        Assert.Equal(before, rig.Api.TemplateListCallCount);
    }

    [Fact]
    public async Task should_fetch_the_list_once_when_the_console_opens()
    {
        var rig = await OpenAsync();

        Assert.Equal(1, rig.Api.StatusFilterCallCount);
    }

    [Fact]
    public async Task should_keep_the_selection_and_the_preview_when_a_search_keystroke_narrows_the_list()
    {
        // Rows 를 Clear() 하면 묶인 DataGrid 의 선택이 그 자리에서 풀려 미리보기가 비워졌다.
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        rig.Console.SearchText = "정기";

        Assert.NotNull(rig.Console.ListViewModel.SelectedItem);
        Assert.Equal(3, rig.Console.ListViewModel.SelectedItem!.Id);
        Assert.True(rig.Console.PreviewViewModel.HasHtml);
    }

    [Fact]
    public async Task should_say_the_search_only_covers_the_newest_page_when_nothing_matches()
    {
        var rig = await OpenAsync();

        rig.Console.SearchText = "없는제목";

        Assert.Contains($"최근 {ReportListViewModel.PageLimit}건", rig.Console.ListViewModel.EmptyStateText);
    }

    [Fact]
    public async Task should_drop_selection_and_unapplied_changes_when_the_console_is_closed()
    {
        var rig = await OpenAsync();
        await rig.Console.SelectRailAsync(ReportConsoleRails.Template);
        rig.Console.OnRowSelected(rig.Console.TemplateViewModel.Rows.First(t => t.Id == 11));
        rig.Console.EditViewModel.Name = "고친 이름";

        await ((IDeactivate)rig.Console).DeactivateAsync(close: true);

        Assert.Equal(0, rig.Console.Detail.SelectedCount);
        Assert.False(rig.Console.Detail.IsDirty);
        Assert.False(rig.Console.PreviewViewModel.IsOverlayOpen);
        Assert.Equal(ReportPreviewContent.NoSelection, rig.Console.PreviewViewModel.Content);
    }

    [Fact]
    public async Task should_work_again_when_the_console_is_reopened()
    {
        // 싱글턴이라 두 번째 열 때 구독이 죽으면 화면이 통째로 멎는다.
        var rig = await OpenAsync();
        await ((IDeactivate)rig.Console).DeactivateAsync(close: true);

        await ((IActivate)rig.Console).ActivateAsync();
        rig.Console.OnRowSelected(rig.Console.ListViewModel.Rows.First(r => r.Id == 3));

        Assert.Equal(3, rig.Console.PreviewViewModel.GenerationId);
        Assert.Equal(1, rig.Console.Detail.SelectedCount);
    }

    [Fact]
    public async Task should_explain_the_failure_instead_of_showing_an_empty_list_when_the_server_is_down()
    {
        var rig = await OpenAsync(api => api.FailList = true);

        Assert.Empty(rig.Console.ListViewModel.Rows);
        Assert.Contains("서버에 연결하지 못했습니다", rig.Console.ListViewModel.EmptyStateText);
    }
    #endregion
}
