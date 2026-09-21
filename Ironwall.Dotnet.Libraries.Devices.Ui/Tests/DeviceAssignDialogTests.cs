using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 배정 창의 차분 계산 — 순수 함수라 창도 서버도 없이 돈다.
/// 정본: window-layout-system-storyboard.html #h-dlg L1436 · all-windows-drag-wireframe.html L392-396 · L424.
/// </summary>
public class AssignDeltaTests
{
    [Fact]
    public void should_send_nothing_when_the_set_did_not_change()
    {
        var plan = AssignDelta.Plan(10, new[] { 1, 2, 3 }, new[] { 3, 2, 1 });

        Assert.False(plan.HasChanges);
        Assert.False(plan.CanSend);
        Assert.Equal("바뀐 것이 없다", plan.BlockReason);
        Assert.Equal(0, plan.CallCount);
    }

    [Fact]
    public void should_split_added_and_removed_when_both_directions_changed()
    {
        var plan = AssignDelta.Plan(10, new[] { 1, 2, 3 }, new[] { 2, 3, 9 });

        Assert.Equal(new[] { 9 }, plan.Added);
        Assert.Equal(new[] { 1 }, plan.Removed);
        // 장비마다가 아니라 방향마다 한 번이다 — 여기서는 두 번.
        Assert.Equal(2, plan.CallCount);
    }

    [Fact]
    public void should_call_once_when_only_one_direction_changed()
    {
        Assert.Equal(1, AssignDelta.Plan(10, new[] { 1 }, new[] { 1, 2, 3 }).CallCount);
        Assert.Equal(1, AssignDelta.Plan(10, new[] { 1, 2, 3 }, new[] { 1 }).CallCount);
    }

    [Fact]
    public void should_collapse_duplicates_when_planning()
    {
        var plan = AssignDelta.Plan(10, new[] { 1, 1, 1 }, new[] { 1, 2, 2, 2 });

        Assert.Equal(new[] { 2 }, plan.Added);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void should_exclude_draft_rows_when_planning()
    {
        // Id ≤ 0 은 서버가 모르는 장비다 — 보내면 404 가 되고, 말없이 버리면 사람이 속는다. 세어 둔다.
        var plan = AssignDelta.Plan(10, Array.Empty<int>(), new[] { 0, -1, 7 });

        Assert.Equal(new[] { 7 }, plan.Added);
        Assert.Equal(2, plan.DraftExcluded);
        Assert.Contains("저장 전 2대", AssignDelta.Summary(plan));
    }

    [Fact]
    public void should_block_when_the_group_is_not_saved_yet()
    {
        var plan = AssignDelta.Plan(0, Array.Empty<int>(), new[] { 7 });

        Assert.False(plan.CanSend);
        Assert.Contains("저장되지 않은 그룹", plan.BlockReason);
    }

    [Fact]
    public void should_stay_in_two_calls_when_the_set_is_large()
    {
        var plan = AssignDelta.Plan(10, Enumerable.Range(1, 500), Enumerable.Range(251, 500));

        Assert.Equal(250, plan.Added.Count);
        Assert.Equal(250, plan.Removed.Count);
        Assert.Equal(2, plan.CallCount);
    }

    [Fact]
    public void should_sort_the_ids_when_planning()
    {
        var plan = AssignDelta.Plan(10, Array.Empty<int>(), new[] { 9, 3, 7 });
        Assert.Equal(new[] { 3, 7, 9 }, plan.Added);
    }

    [Fact]
    public void should_report_no_drift_when_the_server_still_agrees()
    {
        Assert.Null(AssignDelta.Drift(new[] { 1, 2 }, new[] { 2, 1 }));
    }

    [Fact]
    public void should_report_drift_when_someone_else_changed_the_group()
    {
        var drift = AssignDelta.Drift(new[] { 1, 2 }, new[] { 2, 5 });

        Assert.NotNull(drift);
        Assert.Contains("1대가 더 들어와 있다", drift);
        Assert.Contains("1대가 빠져 있다", drift);
        Assert.Contains("아무것도 보내지 않았다", drift);
    }

    [Fact]
    public void should_count_only_what_the_server_applied_when_writing_the_result_line()
    {
        var line = AssignDelta.ResultLine("동측 1구역", new AssignLegOutcome(5, 3, 2, Failed: false), null);

        Assert.Contains("3대를 넣었다", line);
        Assert.Contains("2대는 서버가 건너뛰었다", line);
    }

    [Fact]
    public void should_say_it_failed_when_the_call_failed()
    {
        var line = AssignDelta.ResultLine("동측 1구역", null, new AssignLegOutcome(4, 0, 0, Failed: true));

        Assert.Contains("4대를 빼지 못했다", line);
    }

    [Fact]
    public void should_stay_open_when_one_direction_failed_or_was_partial()
    {
        Assert.True(AssignDelta.ShouldStayOpen(new AssignLegOutcome(3, 3, 0, false), new AssignLegOutcome(2, 0, 0, true)));
        Assert.True(AssignDelta.ShouldStayOpen(new AssignLegOutcome(3, 1, 2, false), null));
        Assert.False(AssignDelta.ShouldStayOpen(new AssignLegOutcome(3, 3, 0, false), new AssignLegOutcome(2, 2, 0, false)));
        Assert.False(AssignDelta.ShouldStayOpen(null, null));
    }
}

/// <summary>
/// 장비 배정 창 — 좌 ↔ 우 이동 · Draft · [저장] 한 번. ▶ ◀ 와 드롭은 같은 메서드를 부른다.
/// </summary>
public class DeviceAssignDialogViewModelTests
{
    private static ControllerDeviceModel Device(int id, int number, params int[] groups)
        => new() { Id = id, DeviceNumber = number, DeviceName = $"CTL-{number:00}", DeviceGroups = groups.ToList() };

    /// <summary>
    /// 닫기만 가로챈 창. Caliburn 의 <c>TryCloseAsync</c> 는 <c>PlatformProvider.Current</c> 를 건드려
    /// 테스트 스레드에 디스패처를 붙박아 버린다 — 그러면 <b>다른 테스트</b>의 UI 스레드 발행이 조용히 죽는다(실측).
    /// </summary>
    private sealed class ClosableAssignDialog : DeviceAssignDialogViewModel
    {
        public ClosableAssignDialog(MockDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> devices,
            Func<System.Threading.CancellationToken, Task>? refresh)
            : base(api, devices, refresh) { }

        public bool? ClosedWith { get; private set; }

        public override Task TryCloseAsync(bool? dialogResult = null)
        {
            ClosedWith = dialogResult;
            return Task.CompletedTask;
        }
    }

    private static (ClosableAssignDialog Vm, MockDeviceApiService Api, List<IBaseDeviceModel> Models) Build(
        Func<System.Threading.CancellationToken, Task>? refresh = null)
    {
        var models = new List<IBaseDeviceModel> { Device(1, 1, 10), Device(2, 2), Device(3, 3), Device(4, 4, 10) };
        var api = new MockDeviceApiService();
        var vm = new ClosableAssignDialog(api, () => models, refresh);
        vm.Initialize(10, "동측 1구역", new[] { 1, 4 });
        return (vm, api, models);
    }

    [Fact]
    public void should_split_candidates_and_members_when_initializing()
    {
        var (vm, _, _) = Build();

        Assert.Equal(new[] { 2, 3 }, vm.Available.Select(i => i.Id));
        Assert.Equal(new[] { 1, 4 }, vm.Assigned.Select(i => i.Id));
        Assert.False(vm.IsDirty);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void should_keep_draft_devices_out_of_the_candidate_list_when_initializing()
    {
        var models = new List<IBaseDeviceModel> { Device(0, 9), Device(5, 5) };
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models);
        vm.Initialize(10, "동측 1구역", Array.Empty<int>());

        Assert.Equal(new[] { 5 }, vm.Available.Select(i => i.Id));
    }

    [Fact]
    public void should_refuse_everything_when_the_group_is_not_saved_yet()
    {
        var models = new List<IBaseDeviceModel> { Device(5, 5) };
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models);
        vm.Initialize(0, "새 그룹", Array.Empty<int>());

        Assert.Empty(vm.Available);
        Assert.Empty(vm.Assigned);
        Assert.Contains("저장되지 않은 그룹", vm.Message);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void should_move_the_selected_row_when_assign_selected()
    {
        var (vm, _, _) = Build();
        vm.SetSelection(AssignSide.Available, new[] { vm.Available.First(i => i.Id == 3) });

        vm.AssignSelected();

        Assert.Equal(new[] { 2 }, vm.Available.Select(i => i.Id));
        Assert.Equal(new[] { 1, 3, 4 }, vm.Assigned.Select(i => i.Id));      // 번호 순서를 지킨다
        Assert.True(vm.IsDirty);
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void should_move_the_row_back_when_unassign_selected()
    {
        var (vm, _, _) = Build();
        vm.SetSelection(AssignSide.Assigned, new[] { vm.Assigned.First(i => i.Id == 4) });

        vm.UnassignSelected();

        Assert.Equal(new[] { 2, 3, 4 }, vm.Available.Select(i => i.Id));
        Assert.Equal(new[] { 1 }, vm.Assigned.Select(i => i.Id));
        Assert.Equal(new[] { 4 }, vm.CurrentPlan.Removed);
    }

    [Fact]
    public void should_keep_the_moved_rows_selected_when_moving()
    {
        var (vm, _, _) = Build();
        var moved = vm.Available.ToList();
        vm.SetSelection(AssignSide.Available, moved);

        vm.AssignSelected();

        Assert.Equal(moved.Select(i => i.Id).OrderBy(i => i), vm.SelectionOf(AssignSide.Assigned).Select(i => i.Id).OrderBy(i => i));
        Assert.Empty(vm.SelectionOf(AssignSide.Available));
    }

    [Fact]
    public void should_move_everything_when_the_double_arrow_is_used()
    {
        var (vm, _, _) = Build();

        vm.AssignAll();
        Assert.Empty(vm.Available);
        Assert.Equal(4, vm.Assigned.Count);

        vm.UnassignAll();
        Assert.Empty(vm.Assigned);
        Assert.Equal(4, vm.Available.Count);
    }

    [Fact]
    public void should_go_back_to_the_opening_state_when_reverting()
    {
        var (vm, _, _) = Build();
        vm.AssignAll();
        Assert.True(vm.IsDirty);

        vm.RevertAll();

        Assert.Equal(new[] { 2, 3 }, vm.Available.Select(i => i.Id));
        Assert.Equal(new[] { 1, 4 }, vm.Assigned.Select(i => i.Id));
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task should_send_one_batch_per_direction_when_saving()
    {
        var (vm, api, _) = Build();
        var assignCalls = new List<List<int>>();
        var removeCalls = new List<List<int>>();
        api.AssignHook = (_, dto) =>
        {
            assignCalls.Add(dto.DeviceIds.ToList());
            return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.ToList() });
        };
        api.RemoveHook = (_, dto) =>
        {
            removeCalls.Add(dto.DeviceIds.ToList());
            return ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(new DeviceGroupBulkRemoveResultDto { RemovedDeviceIds = dto.DeviceIds.ToList() });
        };

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        vm.SetSelection(AssignSide.Assigned, new[] { vm.Assigned.First(i => i.Id == 1) });
        vm.UnassignSelected();

        await vm.SaveAsync();

        Assert.Single(assignCalls);
        Assert.Single(removeCalls);
        Assert.Equal(new[] { 2, 3 }, assignCalls[0]);
        Assert.Equal(new[] { 1 }, removeCalls[0]);
        Assert.True(vm.Saved);
        Assert.True(vm.ClosedWith);
    }

    [Fact]
    public async Task should_send_nothing_when_there_is_no_change()
    {
        var (vm, api, _) = Build();
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        await vm.SaveAsync();

        Assert.False(called);
        Assert.False(vm.Saved);
    }

    [Fact]
    public async Task should_send_nothing_when_the_group_drifted_while_the_dialog_was_open()
    {
        var models = new List<IBaseDeviceModel> { Device(1, 1, 10), Device(2, 2), Device(3, 3) };
        var api = new MockDeviceApiService();
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        // 재조회에서 다른 창이 3번 장비를 이미 넣어 둔 것을 본다.
        var vm = new ClosableAssignDialog(api, () => models, _ =>
        {
            models[2].DeviceGroups = new List<int> { 10 };
            return Task.CompletedTask;
        });
        vm.Initialize(10, "동측 1구역", new[] { 1 });
        vm.SetSelection(AssignSide.Available, new[] { vm.Available.First(i => i.Id == 2) });
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.False(called);
        Assert.False(vm.Saved);
        Assert.Contains("다른 곳에서 그룹이 바뀌었다", vm.Message);
    }

    [Fact]
    public async Task should_stay_open_and_say_so_when_the_server_only_did_part_of_it()
    {
        var (vm, api, _) = Build();
        api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.Take(1).ToList(), SkippedDeviceIds = dto.DeviceIds.Skip(1).ToList() });

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.False(vm.Saved);
        Assert.Contains("서버가 건너뛰었다", vm.Message);
        // 화면은 서버가 아는 상태로 다시 선다 — 넣힌 한 대만 오른쪽에 남는다.
        Assert.Equal(3, vm.Assigned.Count);
    }

    [Fact]
    public async Task should_not_report_success_when_the_call_failed()
    {
        var (vm, api, _) = Build();
        api.AssignHook = (_, _) => ApiResponse<DeviceGroupAssignResultDto>.CreateError("BOOM", "서버 오류");

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.False(vm.Saved);
        Assert.Contains("넣지 못했다", vm.Message);
        Assert.Equal(2, vm.Assigned.Count);      // 기준선 그대로
    }

    [Fact]
    public void should_accept_a_drop_on_the_other_side_when_dragging()
    {
        var (vm, _, _) = Build();
        var payload = Payload(vm.Available.Take(1));

        Assert.True(((IDragDropHandler)vm).CanDrop(payload, new DropTarget(AssignDelta.AssignedZone, null, -1)));
    }

    [Fact]
    public void should_refuse_a_drop_on_the_side_it_came_from_with_a_reason()
    {
        var (vm, _, _) = Build();
        var payload = Payload(vm.Assigned.Take(1));

        Assert.False(((IDragDropHandler)vm).CanDrop(payload, new DropTarget(AssignDelta.AssignedZone, null, -1)));
        Assert.Contains("이미 이 그룹에 들어 있습니다", vm.Message);
    }

    [Fact]
    public void should_refuse_an_unknown_drop_zone_with_a_reason()
    {
        var (vm, _, _) = Build();
        var payload = Payload(vm.Available.Take(1));

        Assert.False(((IDragDropHandler)vm).CanDrop(payload, new DropTarget("assembly-board", null, -1)));
        Assert.Contains("여기에는 놓을 수 없습니다", vm.Message);
    }

    [Fact]
    public void should_move_on_drop_exactly_like_the_arrow_button()
    {
        var (byDrop, _, _) = Build();
        var (byButton, _, _) = Build();

        ((IDragDropHandler)byDrop).Drop(Payload(byDrop.Available.ToList()), new DropTarget(AssignDelta.AssignedZone, null, -1));

        byButton.SetSelection(AssignSide.Available, byButton.Available.ToList());
        byButton.AssignSelected();

        Assert.Equal(byButton.Assigned.Select(i => i.Id), byDrop.Assigned.Select(i => i.Id));
        Assert.Equal(byButton.Available.Select(i => i.Id), byDrop.Available.Select(i => i.Id));
    }

    /// <summary>
    /// 끌고 있는 것. 출처 <see cref="System.Windows.Controls.ItemsControl"/> 은 <c>null</c> 로 둔다 —
    /// 이 창의 판정은 <b>항목이 어느 목록에 들어 있는가</b>로만 하고 출처 컨트롤을 보지 않는다.
    /// (WPF 요소를 만들면 STA 스레드가 필요해 헤드리스로 돌지 않는다.)
    /// </summary>
    private static DragPayload Payload(IEnumerable<DeviceAssignItemViewModel> items)
        => new(null!, items.Cast<object>().ToList(), "행");
}
