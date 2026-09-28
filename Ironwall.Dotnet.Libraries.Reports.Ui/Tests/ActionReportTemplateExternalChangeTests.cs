using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/****************************************************************************
   Purpose      : 조치보고 문구 콘솔이 "다른 곳에서 문구 목록이 바뀌었다"(SYNC_ACTION_REPORT_TEMPLATE →
                  ActionReportTemplatesChangedMessage)를 받았을 때 — 깨끗하면 한 번 다시 읽고, 적용 안 한 편집 · 끌기 ·
                  순서 저장 중이면 덮지 않고 알린다.
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("ReportsCaliburnIoC")]
public class ActionReportTemplateExternalChangeTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();
        public Task Delay(TimeSpan window, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pending) _pending.Add(tcs);
            token.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }
        public void ReleaseAll()
        {
            List<TaskCompletionSource> now;
            lock (_pending) { now = _pending.ToList(); _pending.Clear(); }
            foreach (var t in now) t.TrySetResult();
        }
    }

    private sealed record Rig(ActionReportTemplateConsoleViewModel Console, FakeActionReportTemplateApiService Api, ManualDelay Delay);

    private bool _dragging;

    private async Task<Rig> OpenAsync()
    {
        var api = new FakeActionReportTemplateApiService();
        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        api.Templates.Add(ActionReportTemplateSeed.Template(3, "오경보", 2));
        var delay = new ManualDelay();
        var console = new ActionReportTemplateConsoleViewModel(new EventAggregator(), new FakeLogService(),
                                                               new FakePermissionService { Edit = true, View = true }, api)
        {
            ExternalChangeDelay = delay.Delay,
            IsDragging = () => _dragging,
        };
        await ((IActivate)console).ActivateAsync();
        return new Rig(console, api, delay);
    }

    private static async Task SettleAsync(Rig rig, int count = 1, string action = "UPDATED", int id = 0)
    {
        for (var i = 0; i < count; i++)
            await rig.Console.HandleAsync(new ActionReportTemplatesChangedMessage(action, id), CancellationToken.None);
        var pending = rig.Console.ExternalChangeTask;
        rig.Delay.ReleaseAll();
        await pending;
    }

    [Fact]
    public async Task should_read_the_list_once_when_changes_arrive_and_nothing_is_pending()
    {
        var rig = await OpenAsync();
        var readsBefore = rig.Api.ListReads;
        rig.Api.Templates.Add(ActionReportTemplateSeed.Template(4, "순찰 중 확인", 3));

        await SettleAsync(rig, count: 3);

        Assert.Equal(readsBefore + 1, rig.Api.ListReads);
        Assert.Equal(new[] { 1, 2, 3, 4 }, rig.Console.Items.Select(i => i.Id).ToArray());
        Assert.False(rig.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_keep_the_chosen_line_when_the_list_is_read_again()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Api.Templates.Single(t => t.Id == 2).Content = "강풍/폭우(수정)";

        await SettleAsync(rig, action: "UPDATED", id: 2);

        Assert.Equal(2, rig.Console.SelectedItem?.Id);
        Assert.Equal("강풍/폭우(수정)", rig.Console.DraftContent);   // 깨끗한 폼은 서버의 새 글로 바뀐다
    }

    [Fact]
    public async Task should_not_read_again_and_show_the_notice_when_the_content_has_unapplied_edits()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "고치는 중";
        var readsBefore = rig.Api.ListReads;

        await SettleAsync(rig);

        Assert.Equal(readsBefore, rig.Api.ListReads);
        Assert.True(rig.Console.IsExternallyChanged);
        Assert.Equal(ActionReportTemplateConsoleViewModel.ExternalChangeNotice, rig.Console.ExternalChangeText);
        Assert.Equal("고치는 중", rig.Console.DraftContent);         // 사용자의 글은 그대로다
    }

    [Fact]
    public async Task should_hold_the_read_during_a_drag_and_run_it_after_the_drag_ends()
    {
        var rig = await OpenAsync();
        var readsBefore = rig.Api.ListReads;
        _dragging = true;

        await SettleAsync(rig);
        Assert.Equal(readsBefore, rig.Api.ListReads);
        Assert.True(rig.Console.IsExternallyChanged);

        _dragging = false;
        var retry = rig.Console.ExternalChangeTask;
        rig.Delay.ReleaseAll();
        await retry;

        Assert.Equal(readsBefore + 1, rig.Api.ListReads);
        Assert.False(rig.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_keep_undo_when_the_change_is_the_echo_of_our_own_reorder()
    {
        var rig = await OpenAsync();
        var payload = new DragPayload(null!, new object[] { rig.Console.Items.Single(i => i.Id == 3) }, "오경보");
        rig.Console.Drop.Drop(payload, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
        await Task.Delay(10);                                  // Reordered 는 async void 로 커밋한다(기존 시험과 같은 관용구)
        Assert.True(rig.Console.CanUndoReorder);

        await SettleAsync(rig, action: "UPDATED", id: 0);      // 서버가 우리 reorder 를 알린다(UPDATED + 0)

        Assert.True(rig.Console.CanUndoReorder);               // 서버 순서 = 우리가 보낸 순서 → 되돌리기 유지
    }

    [Fact]
    public async Task should_drop_undo_when_someone_else_reordered_after_us()
    {
        var rig = await OpenAsync();
        var payload = new DragPayload(null!, new object[] { rig.Console.Items.Single(i => i.Id == 3) }, "오경보");
        rig.Console.Drop.Drop(payload, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
        await Task.Delay(10);

        // 다른 운영자가 순서를 또 바꿨다 — 우리 되돌리기는 그 사람의 순서를 덮어쓴다.
        foreach (var t in rig.Api.Templates) t.DisplayOrder = t.Id == 2 ? 0 : t.Id == 3 ? 1 : 2;
        await SettleAsync(rig, action: "UPDATED", id: 0);

        Assert.False(rig.Console.CanUndoReorder);
    }

    /// <summary>
    /// 화면의 목록(ListBox)처럼 — <c>Board.Load</c> 의 <c>Items.Clear()</c> 에 선택이 비면 뷰가 <c>OnRowSelected(null)</c> 를 부른다
    /// (ActionReportTemplateConsoleView.OnListSelectionChanged).
    /// </summary>
    private static void MimicListDroppingSelectionOnClear(ActionReportTemplateConsoleViewModel console)
        => console.Items.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset && console.SelectedItem != null)
                console.OnRowSelected(null);
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task should_keep_the_registered_message_and_the_new_line_when_the_echo_of_our_own_create_arrives(bool listDropsSelection)
    {
        // 헤디드 3회차 SC-ART-005c: [등록] 뒤 우리 쓰기의 SYNC_ACTION_REPORT_TEMPLATE 메아리가 다시 읽기를 부르면
        // 상세 막대가 "문구를 등록했습니다." 대신 "변경 없음" 이 되었다.
        var rig = await OpenAsync();
        if (listDropsSelection) MimicListDroppingSelectionOnClear(rig.Console);
        await rig.Console.AddAsync();
        rig.Console.DraftContent = "순찰 중 확인";
        await rig.Console.ApplyAsync();
        var created = rig.Api.Templates.Max(t => t.Id);
        Assert.Contains("등록했습니다", rig.Console.Detail.FooterText);

        await SettleAsync(rig, action: "CREATED", id: created);

        Assert.Contains("등록했습니다", rig.Console.Detail.FooterText);
        Assert.Equal(created, rig.Console.SelectedItem?.Id);
    }

    [Fact]
    public async Task should_keep_the_applied_message_when_the_echo_of_our_own_edit_arrives()
    {
        var rig = await OpenAsync();
        MimicListDroppingSelectionOnClear(rig.Console);
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍/폭우(수정)";
        await rig.Console.ApplyAsync();
        Assert.Equal("적용했습니다.", rig.Console.Detail.FooterText);

        await SettleAsync(rig, action: "UPDATED", id: 2);

        Assert.Equal("적용했습니다.", rig.Console.Detail.FooterText);
        Assert.Equal(2, rig.Console.SelectedItem?.Id);
    }

    [Fact]
    public async Task should_not_keep_the_message_when_the_chosen_line_was_deleted_elsewhere()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍/폭우(수정)";
        await rig.Console.ApplyAsync();

        rig.Api.Templates.RemoveAll(t => t.Id == 2);
        await SettleAsync(rig, action: "DELETED", id: 2);

        Assert.Null(rig.Console.SelectedItem);
        Assert.DoesNotContain("적용했습니다", rig.Console.Detail.FooterText);   // 사라진 줄의 알림을 남기지 않는다
    }

    [Fact]
    public async Task should_not_keep_the_applied_message_when_someone_else_edits_the_chosen_line_after_our_apply()
    {
        // 적대 검토 M1: 우리가 #2 를 적용한 뒤(한참 뒤) 다른 운영자가 #2 를 고쳤다 — 다시 읽기가 그 사람의 글을 폼에 싣는데
        // 바닥 막대는 "적용했습니다." 로 되살아나 우리가 쓰지 않은 글을 우리가 적용한 것처럼 말했다.
        var rig = await OpenAsync();
        MimicListDroppingSelectionOnClear(rig.Console);
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍/폭우(수정)";
        await rig.Console.ApplyAsync();
        Assert.Equal("적용했습니다.", rig.Console.Detail.FooterText);

        rig.Api.Templates.Single(t => t.Id == 2).Content = "강풍/폭우(다른 운영자)";
        await SettleAsync(rig, action: "UPDATED", id: 2);

        Assert.Equal(2, rig.Console.SelectedItem?.Id);
        Assert.Equal("강풍/폭우(다른 운영자)", rig.Console.DraftContent);
        Assert.DoesNotContain("적용했습니다", rig.Console.DetailFooterText);
        Assert.Equal(ActionReportTemplateConsoleViewModel.SelectedChangedElsewhereText, rig.Console.DetailFooterText);
    }

    [Fact]
    public async Task should_keep_the_applied_message_when_only_another_line_changed_elsewhere()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍/폭우(수정)";
        await rig.Console.ApplyAsync();

        rig.Api.Templates.Single(t => t.Id == 3).Content = "오경보(다른 운영자)";   // 고른 줄의 글은 그대로다
        await SettleAsync(rig, action: "UPDATED", id: 3);

        Assert.Equal("적용했습니다.", rig.Console.DetailFooterText);
    }

    [Theory]
    [InlineData(409, "이미 등록된 문구입니다.")]
    [InlineData(422, "문구는 1~500자여야 합니다.")]
    [InlineData(500, "문구를 고치지 못했습니다. 잠시 후 다시 시도하세요.")]
    public async Task should_keep_the_rejected_draft_and_show_the_notice_when_a_change_arrives_after_a_failed_apply(int status, string failure)
    {
        // 적대 검토 시나리오 B: 실패한 [적용] 이 Settle(오류)로 손댄 칸을 비웠다 — 거절된 글이 폼에 남은 채 "미적용 없음"이 되어
        // 뒤이은 SYNC 다시 읽기가 그 글을 말없이 서버 글로 덮고, 실패 문구를 실패하지 않은 글 옆에 되살렸다.
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍(거절될 글)";
        rig.Api.FailWrite = true;
        rig.Api.StatusCodeOnFailure = status;
        await rig.Console.ApplyAsync();

        Assert.True(rig.Console.Detail.IsDirty);                        // 거절된 글은 여전히 적용되지 않은 글이다
        Assert.True(rig.Console.DetailCanApply);                        // 다시 [적용]할 수 있다
        Assert.Equal(failure, rig.Console.DetailFooterText);            // 실패 문구는 거절된 글 옆에 보인다

        rig.Api.FailWrite = false;
        rig.Api.Templates.Single(t => t.Id == 2).Content = "강풍/폭우(다른 운영자)";
        var readsBefore = rig.Api.ListReads;
        await SettleAsync(rig, action: "UPDATED", id: 2);

        Assert.Equal(readsBefore, rig.Api.ListReads);                   // 덮지 않았다
        Assert.Equal("강풍(거절될 글)", rig.Console.DraftContent);
        Assert.True(rig.Console.IsExternallyChanged);                   // 알리고 사람이 고른다
        Assert.Equal(failure, rig.Console.DetailFooterText);            // 실패 문구는 여전히 거절된 글 옆이다
    }

    [Fact]
    public async Task should_not_show_the_failure_message_when_the_form_no_longer_holds_the_rejected_draft()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍(거절될 글)";
        rig.Api.FailWrite = true;
        await rig.Console.ApplyAsync();

        rig.Console.DraftContent = "강풍/폭우";                          // 사람이 원래 글로 되돌려 적었다 — 미적용 없음
        Assert.False(rig.Console.Detail.IsDirty);
        Assert.DoesNotContain("못했습니다", rig.Console.DetailFooterText);

        rig.Api.FailWrite = false;
        rig.Api.Templates.Single(t => t.Id == 2).Content = "강풍/폭우(다른 운영자)";
        await SettleAsync(rig, action: "UPDATED", id: 2);

        Assert.Equal("강풍/폭우(다른 운영자)", rig.Console.DraftContent);
        Assert.DoesNotContain("못했습니다", rig.Console.DetailFooterText);   // 서버 글 옆에 옛 실패 문구가 없다
    }

    [Fact]
    public async Task should_keep_the_rejected_new_phrase_when_a_change_arrives_after_a_failed_create()
    {
        var rig = await OpenAsync();
        await rig.Console.AddAsync();
        rig.Console.DraftContent = "순찰 중 확인";
        rig.Api.FailWrite = true;
        await rig.Console.ApplyAsync();

        Assert.True(rig.Console.Detail.IsCreating);
        Assert.True(rig.Console.DetailCanApply);                        // 다시 [등록]할 수 있다(전에는 꺼져 다시 적어야 했다)
        Assert.Equal("문구를 등록하지 못했습니다. 잠시 후 다시 시도하세요.", rig.Console.DetailFooterText);

        rig.Api.FailWrite = false;
        rig.Api.Templates.Add(ActionReportTemplateSeed.Template(9, "다른 운영자 문구", 9));
        await SettleAsync(rig, action: "CREATED", id: 9);

        Assert.Equal("순찰 중 확인", rig.Console.DraftContent);
        Assert.True(rig.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_reload_the_list_when_apply_hits_404_and_the_form_holds_no_unapplied_text()
    {
        // 404 문구는 "목록을 다시 불러옵니다." 라고 약속했지만 아무것도 다시 읽지 않았다 — 지운 줄이 목록 · 상세에 남았다.
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Api.Templates.RemoveAll(t => t.Id == 2);                    // 다른 운영자가 지웠다
        var readsBefore = rig.Api.ListReads;

        await rig.Console.ApplyAsync();                                  // 손댄 글 없이 [적용] → 서버 404

        Assert.Equal(readsBefore + 1, rig.Api.ListReads);                // 정말 다시 읽었다
        Assert.DoesNotContain(rig.Console.Items, i => i.Id == 2);
        Assert.Null(rig.Console.SelectedItem);                           // 지운 줄을 고르고 있지 않다
        Assert.True(rig.Console.IsDetailEmpty);                          // 상세도 비었다
        Assert.False(rig.Console.Detail.IsDirty);
        Assert.Equal(ActionReportTemplateConsoleViewModel.DeletedElsewhereReloadedText, rig.Console.DetailFooterText);
    }

    [Fact]
    public async Task should_keep_the_draft_and_show_the_notice_when_apply_hits_404_with_unapplied_text()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Console.DraftContent = "강풍(고친 글)";
        rig.Api.Templates.RemoveAll(t => t.Id == 2);
        var readsBefore = rig.Api.ListReads;

        await rig.Console.ApplyAsync();

        Assert.Equal(readsBefore, rig.Api.ListReads);                    // 적은 글을 덮지 않는다 — 다시 읽지 않는다
        Assert.Equal("강풍(고친 글)", rig.Console.DraftContent);
        Assert.True(rig.Console.Detail.IsDirty);
        Assert.True(rig.Console.IsExternallyChanged);                    // [다시 읽기] 띠로 알린다
        Assert.Equal(ActionReportTemplateConsoleViewModel.DeletedElsewhereDraftKeptText, rig.Console.DetailFooterText);
        Assert.DoesNotContain("불러옵니다", rig.Console.DetailFooterText);   // 하지 않은 다시 읽기를 약속하지 않는다
    }

    [Fact]
    public async Task should_reload_the_list_when_delete_hits_404()
    {
        var rig = await OpenAsync();
        rig.Console.OnRowSelected(rig.Console.Items.Single(i => i.Id == 2));
        rig.Api.Templates.RemoveAll(t => t.Id == 2);
        rig.Api.FailDelete = true;
        rig.Api.StatusCodeOnFailure = 404;
        var readsBefore = rig.Api.ListReads;

        await rig.Console.DeleteAsync();
        await rig.Console.HandleAsync(new CallDeleteActionReportTemplateProcessMessageModel(), CancellationToken.None);

        Assert.Equal(readsBefore + 1, rig.Api.ListReads);
        Assert.DoesNotContain(rig.Console.Items, i => i.Id == 2);
        Assert.Null(rig.Console.SelectedItem);
    }

    [Fact]
    public async Task should_ignore_changes_when_the_console_is_not_open()
    {
        var rig = await OpenAsync();
        await ((IDeactivate)rig.Console).DeactivateAsync(close: true);
        var readsBefore = rig.Api.ListReads;

        await SettleAsync(rig);

        Assert.Equal(readsBefore, rig.Api.ListReads);
    }
}
