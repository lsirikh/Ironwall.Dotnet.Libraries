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
