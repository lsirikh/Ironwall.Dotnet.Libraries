using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 셋업 · 결선 창이 센서 그룹 소속을 바꾸면 "소속이 바뀌었다"(DeviceGroupMembershipChangedMessage)를 알리는가.
                  결선 저장도 그룹 넣기 · 빼기를 배치로 보낸다 — 콘솔 그룹 넣기와 같은 규칙으로 알린다
                  (지도의 구역선 조회표는 이 알림으로 새 소속을 안다).
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("CaliburnIoC")]
public class WiringMembershipAnnounceTests
{
    private readonly EventAggregator _events = new();
    private readonly Recorder _recorder = new();

    public WiringMembershipAnnounceTests() => _events.SubscribeOnPublishedThread(_recorder);

    private sealed class Recorder : IHandle<DeviceGroupMembershipChangedMessage>
    {
        public List<DeviceGroupMembershipChangedMessage> Messages { get; } = new();

        public Task HandleAsync(DeviceGroupMembershipChangedMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>센서 101 · 102 가 그룹 7 에 들어 있는 보드.</summary>
    private static WiringBoard BoardInGroup7()
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, 2).Select(i => (
            Id: 101 + i, Channel: (int?)null, Facts: WiringDoubles.Facts(1101 + i, i + 1),
            Placement: (WiringPlacement?)null, Issue: (string?)null, Groups: (IReadOnlyList<int>?)new List<int> { 7 })));
        return board;
    }

    private WiringApplyService Service(WiringFakeGateway gateway)
        => new(gateway, null, null, WiringDoubles.AxisPolicy(), _events);

    private IReadOnlyList<int> Announced() => Assert.Single(_recorder.Messages).GroupIds.OrderBy(g => g).ToList();

    [Fact]
    public async Task should_announce_added_and_removed_groups_when_wiring_save_changes_membership()
    {
        // 센서 101 을 7 → 8 로 옮긴다: 7 에서 빼고 8 에 넣는 호출 두 건
        var board = BoardInGroup7();
        var row = board.Rows.First(r => r.Id == 101);
        row.Groups.Remove(7);
        row.Groups.Add(8);
        var gateway = new WiringFakeGateway();

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 7, 8 }, Announced());
    }

    [Fact]
    public async Task should_announce_only_the_group_the_server_accepted_when_one_group_call_fails()
    {
        var board = BoardInGroup7();
        var row = board.Rows.First(r => r.Id == 101);
        row.Groups.Remove(7);
        row.Groups.Add(8);
        var gateway = new WiringFakeGateway();
        gateway.GroupFails.Add(8);

        await Service(gateway).ApplyAsync(10, board);

        Assert.Equal(new[] { 7 }, Announced());
    }

    [Fact]
    public async Task should_not_announce_when_every_group_call_fails()
    {
        var board = BoardInGroup7();
        board.Rows.First(r => r.Id == 101).Groups.Add(8);
        var gateway = new WiringFakeGateway();
        gateway.GroupFails.Add(8);

        await Service(gateway).ApplyAsync(10, board);

        Assert.Empty(_recorder.Messages);
    }

    [Fact]
    public async Task should_not_announce_when_wiring_save_leaves_groups_unchanged()
    {
        // 표 값만 바꾼 저장 — 그룹 호출이 없다
        var board = BoardInGroup7();
        var gateway = new WiringFakeGateway();
        gateway.Fetched[101] = WiringDoubles.ServerSensor(101, 1101, 1, null);
        board.Rows.First(r => r.Id == 101).Facts = board.Rows.First(r => r.Id == 101).Facts with { Name = "바뀐 이름" };

        var result = await Service(gateway).ApplyAsync(10, board);

        Assert.Equal(1, gateway.PatchCount);
        Assert.Empty(gateway.GroupCalls);
        Assert.Empty(_recorder.Messages);
        Assert.True(result.IsSuccess);
    }

    // ── 입구(WiringLauncher) 배선 ─────────────────────────────────────

    /// <summary>창을 열면 그 자리에서 센서 101 을 그룹 8 에도 넣고 저장한다. 확인 창에는 "예".</summary>
    private sealed class DrivingWindows : IWindowManager
    {
        public WiringViewModel? Shown { get; private set; }

        public async Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            switch (rootModel)
            {
                case WiringPromptViewModel prompt:
                    // 확인 단추(ConfirmAsync)는 Caliburn 닫기 경로를 밟는다 — 결과만 "예"로 둔다.
                    typeof(WiringPromptViewModel).GetField("<Result>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(prompt, true);
                    return true;
                case WiringViewModel vm:
                    Shown = vm;
                    vm.OnSelectionChanged(new[] { vm.Rows.Single() });
                    vm.ToggleGroup(vm.GroupChecks.Single(g => g.Id == 8));
                    vm.ApplyEdit();
                    await vm.SaveAsync();
                    return false;   // 창은 OS ✕ 로 닫힌다
                default:
                    return false;
            }
        }

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task should_announce_group_when_wiring_window_opened_from_console_launcher_saves_group_change()
    {
        var log = new MockLogService();
        var devices = new DeviceProvider();
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 1, DeviceName = "북측 제어기 B" };
        devices.Add(controller);
        devices.Add(new SensorDeviceModel
        {
            Id = 101, DeviceNumber = 1101, DeviceName = "북측 1구간 펜스", Controller = controller,
            DeviceGroups = new List<int> { 7 },
        });
        var groups = new DeviceGroupProvider(log);
        groups.Add(new DeviceGroupModel { Id = 7, Name = "그룹 7" });
        groups.Add(new DeviceGroupModel { Id = 8, Name = "그룹 8" });
        var api = new MockDeviceApiService
        {
            AssignHook = (g, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
                new DeviceGroupAssignResultDto { GroupId = g, AssignedDeviceIds = dto.DeviceIds.ToList() }),
        };
        var windows = new DrivingWindows();
        var launcher = new WiringLauncher(windows, api, new MockDeviceProviderService(), devices, WiringDoubles.AxisPolicy(),
                                          groups, log: log, eventAggregator: _events);

        var saved = await launcher.OpenAsync(controller);

        Assert.True(saved);
        Assert.Equal(new[] { 8 }, Announced());
    }
}
