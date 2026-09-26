using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 배정 창의 차분 계산 — 순수 함수라 창도 서버도 없이 돈다.
/// 정본: window-layout-system-storyboard.html #h-dlg L1436 · all-windows-drag-wireframe.html L392-396 · L424.
/// 서버 계약: app/schemas/device_group.py:78(넣기 상한 없음) · :108(빼기 max_length=100).
/// </summary>
public class AssignDeltaTests
{
    [Fact]
    public void should_send_nothing_when_the_set_did_not_change()
    {
        var plan = AssignDelta.Plan(10, new[] { 1, 2, 3 }, new[] { 3, 2, 1 });

        Assert.False(plan.HasChanges);
        Assert.False(plan.CanSend);
        Assert.Equal(AssignDelta.NoChangeText, plan.BlockReason);
        Assert.Equal(0, plan.CallCount);
    }

    [Fact]
    public void should_split_added_and_removed_when_both_directions_changed()
    {
        var plan = AssignDelta.Plan(10, new[] { 1, 2, 3 }, new[] { 2, 3, 9 });

        Assert.Equal(new[] { 9 }, plan.Added);
        Assert.Equal(new[] { 1 }, plan.Removed);
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
        Assert.Contains("등록 전 장비 2대", AssignDelta.Summary(plan));
    }

    [Fact]
    public void should_block_when_the_group_is_not_saved_yet()
    {
        var plan = AssignDelta.Plan(0, Array.Empty<int>(), new[] { 7 });

        Assert.False(plan.CanSend);
        Assert.Contains("저장되지 않은 그룹", plan.BlockReason);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(200, 2)]
    [InlineData(201, 3)]
    public void should_split_removals_at_the_server_cap_when_counting_calls(int removed, int expectedCalls)
    {
        // 서버는 한 번에 100대까지만 뺀다(device_group.py:108). 넘겨 보내면 422 로 한 대도 빠지지 않는다.
        Assert.Equal(expectedCalls, AssignDelta.RemoveCallCount(removed));
    }

    [Fact]
    public void should_chunk_removals_at_a_hundred_when_the_set_is_large()
    {
        var chunks = AssignDelta.ChunkRemovals(Enumerable.Range(1, 250).ToList());

        Assert.Equal(3, chunks.Count);
        Assert.Equal(100, chunks[0].Count);
        Assert.Equal(100, chunks[1].Count);
        Assert.Equal(50, chunks[2].Count);
        Assert.Equal(Enumerable.Range(1, 250), chunks.SelectMany(c => c));      // 아무것도 잃지 않는다
    }

    [Fact]
    public void should_not_chunk_the_assign_leg_because_the_server_has_no_cap()
    {
        // 넣기 상한은 없다(device_group.py:78) — 250대도 한 번이다.
        var plan = AssignDelta.Plan(10, Array.Empty<int>(), Enumerable.Range(1, 250));

        Assert.Equal(250, plan.Added.Count);
        Assert.Equal(1, plan.CallCount);
    }

    [Fact]
    public void should_count_both_legs_when_the_set_is_large()
    {
        // 넣기 250(1회) + 빼기 250(3회)
        var plan = AssignDelta.Plan(10, Enumerable.Range(1, 500), Enumerable.Range(251, 500));

        Assert.Equal(250, plan.Added.Count);
        Assert.Equal(250, plan.Removed.Count);
        Assert.Equal(4, plan.CallCount);
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
        Assert.Null(AssignDelta.DriftByCount(2, 2));
    }

    [Fact]
    public void should_report_drift_when_someone_else_changed_the_group()
    {
        var drift = AssignDelta.Drift(new[] { 1, 2 }, new[] { 2, 5 });

        Assert.NotNull(drift);
        Assert.Contains("1대가 더 들어와 있습니다", drift);
        Assert.Contains("1대가 빠져 있습니다", drift);
        Assert.Contains("저장하지 않았습니다", drift);
    }

    [Fact]
    public void should_report_drift_by_count_when_the_number_moved()
    {
        Assert.Contains("1대가 더 들어와 있습니다", AssignDelta.DriftByCount(2, 3));
        Assert.Contains("2대가 빠져 있습니다", AssignDelta.DriftByCount(4, 2));
    }

    [Fact]
    public void should_refuse_to_send_when_the_group_could_not_be_read()
    {
        // 읽지 못한 것과 같다고 보는 것은 다르다 — 못 읽었으면 보내지 않는다.
        var reason = AssignDelta.DriftByCount(2, null);

        Assert.NotNull(reason);
        Assert.Contains("다시 불러오지 못해", reason);
    }

    [Fact]
    public void should_count_only_what_the_server_applied_when_writing_the_result_line()
    {
        var line = AssignDelta.ResultLine("동측 1구역", new AssignLegOutcome(5, 3, 2, Failed: false), null);

        Assert.Contains("3대를 넣었습니다", line);
        Assert.Contains("2대는 처리되지 않았습니다", line);
    }

    [Fact]
    public void should_say_it_failed_when_the_call_failed()
    {
        var line = AssignDelta.ResultLine("동측 1구역", null, new AssignLegOutcome(4, 0, 0, Failed: true));

        Assert.Contains("4대를 빼지 못했습니다", line);
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
/// <remarks>
/// <c>[Collection("CaliburnIoC")]</c>: 이 클래스는 <c>Say</c>/<c>NotifyAll</c> 로 Caliburn 의 정적 경로를 밟는다.
/// 같은 어셈블리의 다른 클래스들이 <c>IoC.GetInstance</c> 를 바꿨다 되돌리므로 병렬로 돌면 서로를 깨뜨린다.
/// </remarks>
[Collection("CaliburnIoC")]
public class DeviceAssignDialogViewModelTests
{
    private static ControllerDeviceModel Device(int id, int number, params int[] groups)
        => new() { Id = id, DeviceNumber = number, DeviceName = $"CTL-{number:00}", DeviceGroups = groups.ToList() };

    /// <summary>서버가 아는 소속 수를 흉내 낸다. 기본은 "그대로" — 창을 연 시점과 같다.</summary>
    private sealed class FakeProbe : IGroupMembershipProbe
    {
        private readonly Func<int?> _count;
        public FakeProbe(Func<int?> count) => _count = count;
        public int Calls { get; private set; }

        public Task<int?> CountAsync(int groupId, CancellationToken token = default)
        {
            Calls++;
            return Task.FromResult(_count());
        }
    }

    /// <summary>
    /// 닫기만 가로챈 창. Caliburn 의 <c>TryCloseAsync</c> 는 <c>PlatformProvider.Current</c> 를 건드려
    /// 테스트 스레드에 디스패처를 붙박아 버린다 — 그러면 <b>다른 테스트</b>의 UI 스레드 발행이 조용히 죽는다(실측).
    /// </summary>
    private sealed class ClosableAssignDialog : DeviceAssignDialogViewModel
    {
        public ClosableAssignDialog(MockDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> devices, IGroupMembershipProbe probe)
            : base(api, devices, probe) { }

        public bool? ClosedWith { get; private set; }

        public override Task TryCloseAsync(bool? dialogResult = null)
        {
            ClosedWith = dialogResult;
            return Task.CompletedTask;
        }
    }

    private sealed class Rig
    {
        public required ClosableAssignDialog Vm { get; init; }
        public required MockDeviceApiService Api { get; init; }
        public required List<IBaseDeviceModel> Models { get; init; }
        public required FakeProbe Probe { get; init; }

        public void Deconstruct(out ClosableAssignDialog vm, out MockDeviceApiService api, out List<IBaseDeviceModel> models)
            => (vm, api, models) = (Vm, Api, Models);
    }

    /// <summary>후보 2·3, 소속 1·4 로 시작한다. 탐침은 기본적으로 프로바이더의 실제 소속 수를 돌려준다.</summary>
    private static Rig Build(Func<List<IBaseDeviceModel>, int?>? probeCount = null)
    {
        var models = new List<IBaseDeviceModel> { Device(1, 1, 10), Device(2, 2), Device(3, 3), Device(4, 4, 10) };
        var api = new MockDeviceApiService();
        var probe = new FakeProbe(() => probeCount is null
            ? models.Count(m => m.DeviceGroups?.Contains(10) == true)
            : probeCount(models));
        var vm = new ClosableAssignDialog(api, () => models, probe);
        vm.Initialize(10, "동측 1구역", new[] { 1, 4 });
        return new Rig { Vm = vm, Api = api, Models = models, Probe = probe };
    }

    private static void SucceedEverything(MockDeviceApiService api)
    {
        api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.ToList() });
        api.RemoveHook = (_, dto) => ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(
            new DeviceGroupBulkRemoveResultDto { RemovedDeviceIds = dto.DeviceIds.ToList() });
    }

    #region - Setup and moves -
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
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models, new FakeProbe(() => 0));
        vm.Initialize(10, "동측 1구역", Array.Empty<int>());

        Assert.Equal(new[] { 5 }, vm.Available.Select(i => i.Id));
    }

    [Fact]
    public void should_refuse_everything_when_the_group_is_not_saved_yet()
    {
        var models = new List<IBaseDeviceModel> { Device(5, 5) };
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models, new FakeProbe(() => 0));
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
    #endregion

    #region - Save -
    [Fact]
    public async Task should_send_one_batch_per_direction_when_saving()
    {
        var rig = Build();
        var (vm, api, _) = rig;
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
        Assert.Equal(1, rig.Probe.Calls);      // 그룹 하나만, 한 번만 다시 읽는다
    }

    [Fact]
    public async Task should_split_the_remove_leg_into_batches_of_a_hundred_when_saving_a_large_set()
    {
        // 서버가 한 번에 100대까지만 받는다(device_group.py:108) — 넘겨 보내면 422 로 한 대도 안 빠진다.
        var models = Enumerable.Range(1, 250).Select(i => (IBaseDeviceModel)Device(i, i, 10)).ToList();
        var api = new MockDeviceApiService();
        var sent = new List<List<int>>();
        api.RemoveHook = (_, dto) =>
        {
            sent.Add(dto.DeviceIds.ToList());
            return ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(new DeviceGroupBulkRemoveResultDto { RemovedDeviceIds = dto.DeviceIds.ToList() });
        };
        var vm = new ClosableAssignDialog(api, () => models, new FakeProbe(() => 250));
        vm.Initialize(10, "큰 구역", Enumerable.Range(1, 250));

        vm.UnassignAll();
        await vm.SaveAsync();

        Assert.Equal(3, sent.Count);
        Assert.All(sent, chunk => Assert.InRange(chunk.Count, 1, AssignDelta.RemoveChunkSize));
        Assert.Equal(Enumerable.Range(1, 250), sent.SelectMany(c => c).OrderBy(i => i));
        Assert.True(vm.Saved);
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
        // 창을 연 뒤 다른 창이 한 대를 더 넣었다 — 서버가 아는 수가 기준선과 다르다.
        var rig = Build(models => models.Count(m => m.DeviceGroups?.Contains(10) == true) + 1);
        var (vm, api, _) = rig;
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        vm.SetSelection(AssignSide.Available, new[] { vm.Available.First(i => i.Id == 2) });
        vm.AssignSelected();

        await vm.SaveAsync();

        Assert.False(called);
        Assert.False(vm.Saved);
        Assert.Contains("다른 사용자가 이 그룹을 바꿨습니다", vm.Message);
        Assert.Equal(DialogMessageSeverity.Warning, vm.MessageSeverity);
    }

    [Fact]
    public async Task should_send_nothing_when_the_group_could_not_be_re_read()
    {
        var rig = Build(_ => null);
        var (vm, api, _) = rig;
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        vm.AssignAll();
        await vm.SaveAsync();

        Assert.False(called);
        Assert.False(vm.Saved);
        Assert.Contains("다시 불러오지 못해", vm.Message);
        Assert.Equal(DialogMessageSeverity.Critical, vm.MessageSeverity);
    }
    #endregion

    #region - After the save: the cache, the draft, the retry -
    [Fact]
    public async Task should_write_the_membership_back_into_the_shared_cache_when_saving()
    {
        // 캐시를 안 고치면 다시 열었을 때 방금 넣은 장비가 후보로 돌아오고, 서버는 멱등하게 skipped 로 답해
        // 영원히 "건너뛰었다" 인 창이 된다(device_groups.py:354).
        var rig = Build();
        var (vm, api, models) = rig;
        SucceedEverything(api);

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        vm.SetSelection(AssignSide.Assigned, new[] { vm.Assigned.First(i => i.Id == 1) });
        vm.UnassignSelected();
        await vm.SaveAsync();

        Assert.Contains(10, models.First(m => m.Id == 2).DeviceGroups!);
        Assert.Contains(10, models.First(m => m.Id == 3).DeviceGroups!);
        Assert.DoesNotContain(10, models.First(m => m.Id == 1).DeviceGroups!);
    }

    [Fact]
    public async Task should_show_the_members_when_the_dialog_is_opened_a_second_time()
    {
        var rig = Build();
        var (vm, api, models) = rig;
        SucceedEverything(api);

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        await vm.SaveAsync();
        Assert.True(vm.Saved);

        // 두 번째로 연다 — 런처는 소속을 넘기지 않고 프로바이더에서 읽는다(assignedIds == null).
        var again = new ClosableAssignDialog(api, () => models, new FakeProbe(() => 4));
        again.Initialize(10, "동측 1구역");

        Assert.Equal(new[] { 1, 2, 3, 4 }, again.Assigned.Select(i => i.Id));
        Assert.Empty(again.Available);
        Assert.False(again.IsDirty);                 // 보낼 것이 없다 — 되풀이 고리가 끊긴다
        Assert.Equal(AssignDelta.NoChangeText, again.CurrentPlan.BlockReason);
    }

    [Fact]
    public async Task should_keep_the_moves_and_leave_only_the_remainder_when_the_server_skipped_some()
    {
        var rig = Build();
        var (vm, api, _) = rig;
        // 서버가 첫 한 대만 넣고 나머지는 건너뛴다.
        api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.Take(1).ToList(), SkippedDeviceIds = dto.DeviceIds.Skip(1).ToList() });

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        await vm.SaveAsync();

        Assert.False(vm.Saved);
        Assert.Null(vm.ClosedWith);                                    // 창은 열려 있다
        Assert.Contains("처리되지 않았습니다", vm.Message);
        Assert.Equal(DialogMessageSeverity.Warning, vm.MessageSeverity);   // 회색 안내로 주저앉지 않는다
        Assert.Equal(4, vm.Assigned.Count);                            // 손으로 옮긴 것은 그대로다
        Assert.Empty(vm.Available);
        // 건너뛴 것은 이미 서버가 그 상태라는 뜻이다 — 다시 보낼 것이 남지 않는다.
        Assert.False(vm.CurrentPlan.HasChanges);
    }

    [Fact]
    public async Task should_keep_the_draft_and_allow_a_retry_when_the_call_failed()
    {
        var rig = Build();
        var (vm, api, _) = rig;
        api.AssignHook = (_, _) => ApiResponse<DeviceGroupAssignResultDto>.CreateError("BOOM", "서버 오류");

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        await vm.SaveAsync();

        Assert.False(vm.Saved);
        Assert.Contains("넣지 못했습니다", vm.Message);
        // 한 번의 타임아웃이 끌어다 놓은 것을 전부 지우지 않는다.
        Assert.Equal(4, vm.Assigned.Count);
        Assert.Equal(new[] { 2, 3 }, vm.CurrentPlan.Added);
        Assert.True(vm.CanSave);                                       // 그 자리에서 다시 보낼 수 있다

        SucceedEverything(api);
        await vm.SaveAsync();

        Assert.True(vm.Saved);
    }

    [Fact]
    public async Task should_retry_only_the_failed_leg_when_one_direction_worked()
    {
        var rig = Build();
        var (vm, api, _) = rig;
        api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.ToList() });
        api.RemoveHook = (_, _) => ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("BOOM", "서버 오류");

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        vm.AssignSelected();
        vm.SetSelection(AssignSide.Assigned, new[] { vm.Assigned.First(i => i.Id == 1) });
        vm.UnassignSelected();
        await vm.SaveAsync();

        Assert.False(vm.Saved);
        Assert.Empty(vm.CurrentPlan.Added);                 // 된 쪽은 사라진다
        Assert.Equal(new[] { 1 }, vm.CurrentPlan.Removed);  // 안 된 쪽만 남는다
    }
    #endregion

    #region - The legacy host entry point -
    [Fact]
    public async Task should_move_the_selection_and_save_when_the_legacy_confirm_is_used()
    {
        // 호스트 래퍼(DeviceAssignPropertyDialogViewModel)의 유일한 입구 — ▶ 와 [저장]이 한 번에 붙는다.
        var rig = Build();
        var (vm, api, _) = rig;
        var sent = new List<List<int>>();
        api.AssignHook = (_, dto) =>
        {
            sent.Add(dto.DeviceIds.ToList());
            return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.ToList() });
        };

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        await vm.ConfirmButton();

        Assert.Single(sent);
        Assert.Equal(new[] { 2, 3 }, sent[0]);
        Assert.True(vm.Saved);
        Assert.True(vm.ClosedWith);
    }

    [Fact]
    public async Task should_close_without_sending_when_the_legacy_confirm_has_nothing_selected()
    {
        var rig = Build();
        var (vm, api, _) = rig;
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        await vm.ConfirmButton();

        Assert.False(called);
        Assert.True(vm.ClosedWith);
        Assert.False(vm.Saved);      // 보낸 것이 없으니 저장도 아니다
    }

    [Fact]
    public async Task should_check_for_drift_on_the_legacy_path_too()
    {
        // 옛 경로에도 눈이 있다 — 탐침이 선택 인자였을 때 이 경로만 조용히 덮어쓰고 있었다.
        var rig = Build(models => models.Count(m => m.DeviceGroups?.Contains(10) == true) + 1);
        var (vm, api, _) = rig;
        var called = false;
        api.AssignHook = (_, _) => { called = true; return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto()); };

        vm.SetSelection(AssignSide.Available, vm.Available.ToList());
        await vm.ConfirmButton();

        Assert.False(called);
        Assert.False(vm.Saved);
        Assert.Contains("다른 사용자가 이 그룹을 바꿨습니다", vm.Message);
    }

    [Fact]
    public void should_refuse_to_be_built_without_an_eye_on_the_server()
    {
        var models = new List<IBaseDeviceModel>();
        Assert.Throws<ArgumentNullException>(
            () => new DeviceAssignDialogViewModel(new MockDeviceApiService(), () => models, null!));
    }
    #endregion

    #region - Drag and drop -
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
        Assert.Equal(DialogMessageSeverity.Warning, vm.MessageSeverity);
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
    #endregion
}
