using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 그룹 넣기 100 단위(GOP API 8.0.15 대조 G31) — 명세 §5.6 그룹 할당 <c>device_ids</c> 는 1~100개(빼기와 같은 상한).
/// 예전엔 "넣기 상한 없음"으로 보고 한 번에 보내 101대부터 422 로 한 대도 안 들어갔다.
/// </summary>
[Collection("CaliburnIoC")]
public class AssignChunkTests
{
    private static ControllerDeviceModel Device(int id, params int[] groups)
        => new() { Id = id, DeviceNumber = id, DeviceName = $"CTL-{id:000}", DeviceGroups = groups.ToList() };

    private sealed class CountProbe : IGroupMembershipProbe
    {
        private readonly Func<int?> _count;
        public CountProbe(Func<int?> count) => _count = count;
        public Task<int?> CountAsync(int groupId, CancellationToken token = default) => Task.FromResult(_count());
    }

    private sealed class ClosableAssignDialog : DeviceAssignDialogViewModel
    {
        public ClosableAssignDialog(MockDeviceApiService api, Func<IEnumerable<IBaseDeviceModel>> devices, IGroupMembershipProbe probe)
            : base(api, devices, probe) { }
        public bool? ClosedWith { get; private set; }
        public override Task TryCloseAsync(bool? dialogResult = null) { ClosedWith = dialogResult; return Task.CompletedTask; }
    }

    /// <summary>호출을 적고, <paramref name="failOn"/> 번째(1부터) 호출만 거부한다.</summary>
    private static List<List<int>> RecordAssigns(MockDeviceApiService api, int failOn = 0)
    {
        var calls = new List<List<int>>();
        api.AssignHook = (_, dto) =>
        {
            calls.Add(dto.DeviceIds.ToList());
            if (calls.Count == failOn)
                return new ApiResponse<DeviceGroupAssignResultDto> { Success = false, Error = new ApiError { Code = "VALIDATION_ERROR", Message = "거부" } };
            return ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.ToList() });
        };
        return calls;
    }

    [Fact]
    public void should_cut_assign_list_into_chunks_of_a_hundred()
    {
        var chunks = AssignDelta.ChunkAssigns(Enumerable.Range(1, 250).ToList());

        Assert.Equal(new[] { 100, 100, 50 }, chunks.Select(c => c.Count));
        Assert.Equal(Enumerable.Range(1, 250), chunks.SelectMany(c => c));
        Assert.Empty(AssignDelta.ChunkAssigns(Array.Empty<int>()));
        Assert.Equal(1, AssignDelta.AssignCallCount(100));
        Assert.Equal(2, AssignDelta.AssignCallCount(101));
    }

    [Fact]
    public async Task should_send_three_assign_calls_when_dialog_saves_250_devices()
    {
        var models = Enumerable.Range(1, 250).Select(i => (IBaseDeviceModel)Device(i)).ToList();
        var api = new MockDeviceApiService();
        var calls = RecordAssigns(api);
        var vm = new ClosableAssignDialog(api, () => models, new CountProbe(() => 0));
        vm.Initialize(10, "큰 구역", Array.Empty<int>());

        vm.AssignAll();
        await vm.SaveAsync();

        Assert.Equal(3, calls.Count);
        Assert.All(calls, c => Assert.InRange(c.Count, 1, AssignDelta.AssignChunkSize));
        Assert.Equal(Enumerable.Range(1, 250), calls.SelectMany(c => c).OrderBy(i => i));
        Assert.True(vm.Saved);
    }

    [Fact]
    public async Task should_absorb_only_successful_chunks_and_stay_open_when_second_assign_call_fails()
    {
        var models = Enumerable.Range(1, 250).Select(i => (IBaseDeviceModel)Device(i)).ToList();
        var api = new MockDeviceApiService();
        var calls = RecordAssigns(api, failOn: 2);
        var vm = new ClosableAssignDialog(api, () => models, new CountProbe(() => 0));
        vm.Initialize(10, "큰 구역", Array.Empty<int>());

        vm.AssignAll();
        await vm.SaveAsync();

        Assert.Equal(3, calls.Count);
        Assert.False(vm.Saved);                       // 한 묶음이 실패했으니 창은 닫지 않는다(빼기와 같은 규칙)
        Assert.Null(vm.ClosedWith);
        var joined = models.Where(m => m.DeviceGroups?.Contains(10) == true).Select(m => m.Id).OrderBy(i => i).ToList();
        Assert.Equal(calls[0].Concat(calls[2]).OrderBy(i => i), joined);   // 1·3번째 묶음만 반영
        Assert.Equal(calls[1].OrderBy(i => i), vm.CurrentPlan.Added);         // 남은 것 = 실패한 묶음만
    }

    [Fact]
    public async Task should_send_three_assign_calls_when_group_drop_carries_250_devices()
    {
        var models = Enumerable.Range(1, 250).Select(i => (IBaseDeviceModel)Device(i)).ToList();
        var api = new MockDeviceApiService();
        var calls = RecordAssigns(api);
        var drop = new DeviceGroupDropHandler(api, () => models);

        var line = await drop.AssignAsync(10, "큰 구역", models);

        Assert.Equal(3, calls.Count);
        Assert.All(calls, c => Assert.InRange(c.Count, 1, AssignDelta.AssignChunkSize));
        Assert.Contains("250대를 넣었습니다", line);
    }

    [Fact]
    public async Task should_reflect_only_successful_chunks_when_second_group_drop_call_fails()
    {
        var models = Enumerable.Range(1, 250).Select(i => (IBaseDeviceModel)Device(i)).ToList();
        var api = new MockDeviceApiService();
        var calls = RecordAssigns(api, failOn: 2);
        var drop = new DeviceGroupDropHandler(api, () => models);
        GroupDropResult? result = null;
        drop.Completed += r => result = r;

        var line = await drop.AssignAsync(10, "큰 구역", models);

        Assert.Equal(3, calls.Count);
        var joined = models.Where(m => m.DeviceGroups?.Contains(10) == true).Select(m => m.Id).OrderBy(i => i);
        Assert.Equal(calls[0].Concat(calls[2]).OrderBy(i => i), joined);
        Assert.Contains("150대를 넣었습니다", line);
        Assert.Contains("100대는 넣지 못했습니다", line);
        Assert.NotNull(result?.Undo);
        Assert.Equal(150, result!.Undo!.DeviceIds.Count);   // 되돌리기는 실제로 들어간 것만
    }
}
