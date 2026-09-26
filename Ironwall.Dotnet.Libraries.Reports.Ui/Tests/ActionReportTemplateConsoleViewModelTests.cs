using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 조치보고 문구 관리 콘솔 — 적재 · 등록/수정(검증) · 삭제 확인 · 재정렬 커밋(단일 호출) · 되돌리기 · 권한 게이팅.
/// </summary>
[Collection("ReportsCaliburnIoC")]
public class ActionReportTemplateConsoleViewModelTests : System.IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private sealed class Rig
    {
        public required ActionReportTemplateConsoleViewModel Console { get; init; }
        public required FakeActionReportTemplateApiService Api { get; init; }
        public required FakePermissionService Permission { get; init; }
        public required EventAggregator Events { get; init; }
    }

    private static async Task<Rig> OpenAsync(System.Action<FakeActionReportTemplateApiService>? seed = null, bool canEdit = true, bool canView = true, System.Func<string, bool>? viewFor = null)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeActionReportTemplateApiService();
        var permission = new FakePermissionService { Edit = canEdit, View = canView, ViewFor = viewFor };

        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        api.Templates.Add(ActionReportTemplateSeed.Template(3, "오경보", 2));
        seed?.Invoke(api);

        var console = new ActionReportTemplateConsoleViewModel(events, log, permission, api);
        await ((IActivate)console).ActivateAsync();
        return new Rig { Console = console, Api = api, Permission = permission, Events = events };
    }

    [Fact]
    public async Task should_show_an_empty_state_instead_of_a_blank_content_box_when_nothing_is_chosen()
    {
        // V-32 — 고르지 않았는데 빈 문구 칸 · "0 / 500자" 가 서 있었다.
        var rig = await OpenAsync();

        Assert.True(rig.Console.IsDetailEmpty);
        Assert.False(rig.Console.IsDetailFormVisible);
        Assert.False(rig.Console.Detail.ShowButtons);
        Assert.Contains("[새 문구]", rig.Console.DetailEmptyHint);
    }

    [Fact]
    public async Task should_open_the_form_when_a_line_is_chosen_or_a_new_one_begins()
    {
        var rig = await OpenAsync();

        rig.Console.OnRowSelected(rig.Console.Items[1]);
        Assert.False(rig.Console.IsDetailEmpty);
        Assert.True(rig.Console.IsDetailFormVisible);

        rig.Console.OnRowSelected(null);
        Assert.True(rig.Console.IsDetailEmpty);

        await rig.Console.AddAsync();
        Assert.False(rig.Console.IsDetailEmpty);
        Assert.True(rig.Console.Detail.ShowButtons);
    }

    [Fact]
    public async Task should_not_ask_to_register_in_the_empty_state_when_editing_is_not_allowed()
    {
        var rig = await OpenAsync(canEdit: false);

        Assert.DoesNotContain("[새 문구]", rig.Console.DetailEmptyHint);
    }

    [Fact]
    public async Task should_load_templates_ordered_by_display_order_when_opened()
    {
        var rig = await OpenAsync();

        Assert.Equal(new[] { 1, 2, 3 }, rig.Console.Items.Select(i => i.Id).ToArray());
        Assert.Equal("3건", rig.Console.CountText);
    }

    [Fact]
    public async Task should_show_the_empty_state_text_without_a_hardcoded_fallback_when_the_load_fails()
    {
        var rig = await OpenAsync(api => api.FailList = true);

        Assert.True(rig.Console.IsEmpty);
        Assert.Contains("불러오지 못했습니다", rig.Console.EmptyStateText);
    }

    #region - 드래그 재정렬 커밋(단일 호출) -
    [Fact]
    public async Task should_commit_a_reorder_with_exactly_one_call_carrying_the_full_order()
    {
        var rig = await OpenAsync();
        var drop = rig.Console.Drop;

        // 3(오경보)를 맨 앞으로 — 마우스 드래그 · Alt+↑/↓ 둘 다 이 Drop() 하나를 부른다.
        var payload = new DragPayload(null!, new object[] { rig.Console.Items.Single(i => i.Id == 3) }, "오경보");
        drop.Drop(payload, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
        await Task.Delay(10);   // Reordered 는 async void 로 커밋한다

        Assert.Single(rig.Api.ReorderCalls);
        var sent = rig.Api.ReorderCalls[0];
        Assert.Equal(3, sent.Count);   // 부분이 아니라 화면에 보이는 전체 목록
        Assert.Equal(new[] { 3, 1, 2 }, sent.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async Task should_offer_undo_after_a_successful_reorder_and_send_a_second_reorder_call_with_the_previous_order()
    {
        var rig = await OpenAsync();
        var drop = rig.Console.Drop;
        var payload = new DragPayload(null!, new object[] { rig.Console.Items.Single(i => i.Id == 3) }, "오경보");
        drop.Drop(payload, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
        await Task.Delay(10);

        Assert.True(rig.Console.CanUndoReorder);

        await rig.Console.UndoReorderAsync();

        Assert.Equal(2, rig.Api.ReorderCalls.Count);            // 되돌리기 = 두 번째 reorder 호출
        Assert.Equal(new[] { 1, 2, 3 }, rig.Api.ReorderCalls[1].Select(s => s.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, rig.Console.Items.Select(i => i.Id).ToArray());
        Assert.False(rig.Console.CanUndoReorder);               // 한 번만 되돌린다(스택 아님)
    }

    [Fact]
    public async Task should_refetch_the_server_order_when_the_reorder_commit_fails()
    {
        var rig = await OpenAsync(api => api.FailReorder = true);
        var drop = rig.Console.Drop;
        var payload = new DragPayload(null!, new object[] { rig.Console.Items.Single(i => i.Id == 3) }, "오경보");

        drop.Drop(payload, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
        await Task.Delay(10);

        // 실패 시 행마다 되돌리지 않고 서버를 다시 불러온다(드래그 규칙).
        Assert.Equal(new[] { 1, 2, 3 }, rig.Console.Items.Select(i => i.Id).ToArray());
        Assert.False(rig.Console.CanUndoReorder);
    }

    [Fact]
    public async Task should_move_the_same_way_through_the_up_down_button_path_as_through_drop()
    {
        var rig = await OpenAsync();
        var item = rig.Console.Items.Single(i => i.Id == 3);

        rig.Console.MoveSelected(item, -1);   // ▲ 단추 경로
        await Task.Delay(10);

        Assert.Equal(new[] { 1, 3, 2 }, rig.Console.Items.Select(i => i.Id).ToArray());
        Assert.Single(rig.Api.ReorderCalls);
        Assert.Equal(new[] { 1, 3, 2 }, rig.Api.ReorderCalls[0].Select(s => s.Id).ToArray());
    }
    #endregion

    #region - 등록 · 수정(클라 선제 검증) -
    [Fact]
    public async Task should_block_apply_when_the_draft_is_blank()
    {
        var rig = await OpenAsync();
        await rig.Console.AddAsync();
        rig.Console.DraftContent = "   ";

        Assert.False(rig.Console.DetailCanApply);
        Assert.Equal("문구를 입력하세요.", rig.Console.DraftValidationError);
    }

    [Fact]
    public async Task should_block_apply_when_the_draft_duplicates_an_existing_template()
    {
        var rig = await OpenAsync();
        await rig.Console.AddAsync();
        rig.Console.DraftContent = "오경보";

        Assert.Equal("이미 등록된 문구입니다.", rig.Console.DraftValidationError);
    }

    [Fact]
    public async Task should_create_a_template_and_select_it_when_apply_succeeds()
    {
        var rig = await OpenAsync();
        await rig.Console.AddAsync();
        rig.Console.DraftContent = "차량 통제";

        await rig.Console.ApplyAsync();

        Assert.Single(rig.Api.CreateCalls);
        Assert.Equal("차량 통제", rig.Api.CreateCalls[0].Content);
        Assert.NotNull(rig.Console.SelectedItem);
        Assert.Equal("차량 통제", rig.Console.SelectedItem!.Content);
    }
    #endregion

    #region - 권한 게이팅 -
    [Fact]
    public async Task should_disable_add_and_delete_when_edit_permission_is_missing()
    {
        var rig = await OpenAsync(canEdit: false);
        rig.Console.SelectedItem = rig.Console.Items.First();

        Assert.False(rig.Console.CanAdd);
        Assert.True(rig.Console.Detail.IsReadOnly);
        Assert.Equal("문구 편집 권한이 없습니다.", rig.Console.AddBlockedReason);
    }

    [Fact]
    public async Task should_show_a_view_permission_message_instead_of_the_hardcoded_empty_state_when_view_is_denied()
    {
        var rig = await OpenAsync(canView: false);

        Assert.True(rig.Console.IsEmpty);
        Assert.Equal("문구를 볼 권한이 없습니다.", rig.Console.LoadError);
    }

    [Fact]
    public async Task should_load_the_list_when_events_view_is_granted_even_without_template_view()
    {
        // 서버는 목록 GET 을 events:view 로 거른다 — action_report_templates:view 가 없어도 읽을 수 있어야 한다.
        var rig = await OpenAsync(viewFor: module => module == ActionReportTemplateConsoleViewModel.ReadPermissionModuleKey);

        Assert.False(rig.Console.IsEmpty);
        Assert.Null(rig.Console.LoadError);
    }

    #region - 구 서버(라우터 없음) -
    [Fact]
    public async Task should_disable_writes_and_say_the_server_does_not_support_it_when_the_list_returns_404()
    {
        // 운영 6.3.2 에는 /events/action-report-templates 가 없다 — 서비스가 404 를 NOT_SUPPORTED 로 번역한다.
        var rig = await OpenAsync(api => api.Unsupported = true);

        Assert.False(rig.Console.CanAdd);
        Assert.True(rig.Console.Detail.IsReadOnly);
        Assert.Equal(ActionReportTemplateConsoleViewModel.UnsupportedText, rig.Console.EmptyStateText);
        Assert.Equal(ActionReportTemplateConsoleViewModel.UnsupportedText, rig.Console.AddBlockedReason);
    }

    [Fact]
    public async Task should_not_send_a_write_or_blame_a_deleted_row_when_the_server_does_not_support_the_api()
    {
        var rig = await OpenAsync(api => api.Unsupported = true);

        await rig.Console.AddAsync();
        rig.Console.DraftContent = "차량 통제";
        await rig.Console.ApplyAsync();

        Assert.Empty(rig.Api.CreateCalls);
        Assert.Empty(rig.Api.UpdateCalls);
        Assert.DoesNotContain("다른 곳에서 삭제된", rig.Console.Detail.LastMessage ?? string.Empty);
        Assert.Equal(ActionReportTemplateConsoleViewModel.UnsupportedText, rig.Console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_enable_writes_again_when_a_later_load_succeeds()
    {
        var rig = await OpenAsync(api => api.Unsupported = true);

        rig.Api.Unsupported = false;   // 서버를 올렸다
        await rig.Console.ReloadAsync();

        Assert.True(rig.Console.CanAdd);
        Assert.False(rig.Console.Detail.IsReadOnly);
        Assert.Equal(3, rig.Console.Items.Count);
    }
    #endregion

    [Fact]
    public async Task should_show_the_view_permission_message_when_only_template_view_is_granted()
    {
        var rig = await OpenAsync(viewFor: module => module == ActionReportTemplateConsoleViewModel.PermissionModuleKey);

        Assert.True(rig.Console.IsEmpty);
        Assert.Equal("문구를 볼 권한이 없습니다.", rig.Console.LoadError);
    }
    #endregion
}
