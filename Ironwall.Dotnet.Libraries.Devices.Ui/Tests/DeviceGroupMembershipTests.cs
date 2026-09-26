using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 그룹 소속 왕복 — 라이브 하네스(tools/live-api-roundtrip device-groups, 서버 8.0.2)가 잡은 결함의 헤드리스 고정.
/// </summary>
/// <remarks>
/// 서버 7.0+ 는 장비 소속을 <c>group_ids</c> 로 싣고(<c>device_groups</c> 제거) 쓰기의 <c>group_ids</c> 는 통째 교체다.
/// 매핑이 <c>device_groups</c> 만 읽어 소속이 늘 비었고, 그 빈 목록이 상세 저장으로 되돌아가 다른 소속을 지웠다.
/// </remarks>
public class DeviceGroupMembershipTests
{
    private const string SensorHead = "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"s\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
                                    + "\"category_device\": \"sensor\", \"type_sensor\": \"PIR\", \"controller_id\": 12";

    private static SensorDeviceModel ReadSensor(string tail) => JsonConvert.DeserializeObject<SensorDeviceDto>(SensorHead + tail + " }")!.ToSensorDeviceModel();

    #region - 읽기 (R1) -
    [Fact]
    public void should_read_group_ids_into_model_when_server_sends_group_ids()
    {
        var model = ReadSensor(", \"group_ids\": [3, 4]");

        Assert.Equal(new[] { 3, 4 }, model.DeviceGroups);
    }

    [Fact]
    public void should_read_legacy_device_groups_when_group_ids_are_absent()
    {
        var model = ReadSensor(", \"device_groups\": [ { \"id\": 9, \"name\": \"g\" } ]");

        Assert.Equal(new[] { 9 }, model.DeviceGroups);
    }

    [Fact]
    public void should_leave_membership_unknown_when_response_carries_no_group_key()
    {
        Assert.Null(ReadSensor(string.Empty).DeviceGroups);
    }
    #endregion

    #region - 상세 저장 (R1b) -
    [Fact]
    public void should_mark_group_ids_unchanged_when_membership_is_not_edited()
    {
        var model = ReadSensor(", \"group_ids\": [3, 4]");
        model.DeviceName = "edited";

        var dto = model.ToSensorDeviceDto();

        Assert.True(dto.GroupIdsUnchanged);
    }

    [Fact]
    public void should_mark_group_ids_changed_when_membership_is_edited()
    {
        var model = ReadSensor(", \"group_ids\": [3, 4]");
        model.DeviceGroups!.Remove(4);

        var dto = model.ToSensorDeviceDto();

        Assert.False(dto.GroupIdsUnchanged);
        Assert.Equal(new[] { 3 }, dto.GroupIds);
    }

    [Fact]
    public void should_stay_unchanged_when_server_confirmed_group_assign_is_reflected()
    {
        var model = ReadSensor(", \"group_ids\": [3, 4]");

        GroupMembershipBaseline.ApplyConfirmed(model, 5, member: true);

        Assert.Equal(new[] { 3, 4, 5 }, model.DeviceGroups);
        Assert.True(model.ToSensorDeviceDto().GroupIdsUnchanged);
    }

    [Fact]
    public void should_not_treat_partial_list_as_edit_when_membership_was_unknown()
    {
        var model = ReadSensor(string.Empty);    // 소속을 받지 못했다

        GroupMembershipBaseline.ApplyConfirmed(model, 5, member: true);

        Assert.Equal(new[] { 5 }, model.DeviceGroups);
        Assert.True(model.ToSensorDeviceDto().GroupIdsUnchanged);   // [5] 를 group_ids 로 되보내면 나머지 소속이 지워진다
    }

    [Fact]
    public void should_send_group_ids_when_model_was_not_read_from_server()
    {
        var draft = new SensorDeviceModel { DeviceName = "new", DeviceGroups = new List<int> { 3 } };

        Assert.False(draft.ToSensorDeviceDto().GroupIdsUnchanged);
    }

    [Fact]
    public async Task should_keep_membership_unchanged_when_group_drop_is_reflected()
    {
        var model = ReadSensor(", \"group_ids\": [3, 4]");
        var api = new MockDeviceApiService
        {
            AssignHook = (g, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
                new DeviceGroupAssignResultDto { GroupId = g, AssignedDeviceIds = dto.DeviceIds.ToList() }),
        };
        var handler = new DeviceGroupDropHandler(api, () => new IBaseDeviceModel[] { model });

        await handler.AssignAsync(5, "C", new IBaseDeviceModel[] { model });

        Assert.Equal(new[] { 3, 4, 5 }, model.DeviceGroups);
        Assert.True(model.ToSensorDeviceDto().GroupIdsUnchanged);
    }
    #endregion

    #region - 캐시 병합 (R1b 재조회) -
    private static (DeviceProviderService service, DeviceProvider provider) CreateService(MockDeviceApiService api)
    {
        var log = new MockLogService();
        var provider = new DeviceProvider();
        var service = new DeviceProviderService(
            logService: log,
            eventAggregator: new MockEventAggregator(),
            apiService: api,
            deviceProvider: provider,
            controllerProvider: new ControllerDeviceProvider(log, provider),
            sensorProvider: new SensorDeviceProvider(log, provider),
            cameraProvider: new CameraDeviceProvider(log, provider),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log));
        return (service, provider);
    }

    private static ApiListResponse<ControllerDeviceDto> Controllers(string groupTail)
        => ApiListResponse<ControllerDeviceDto>.CreateSuccess(new List<ControllerDeviceDto>
        {
            JsonConvert.DeserializeObject<ControllerDeviceDto>(
                "{ \"id\": 1, \"number_device\": 1, \"name_device\": \"c\", \"status\": \"ACTIVATED\", \"category_device\": \"controller\", "
              + "\"type_controller\": \"Controller\", \"connection\": { \"type\": \"IP_DIRECT\", \"ip_address\": \"10.0.0.1\", \"ip_port\": 9000 }"
              + groupTail + " }")!,
        });

    [Fact]
    public async Task should_keep_cached_membership_when_refetch_carries_no_group_key()
    {
        var api = new MockDeviceApiService();
        api.ControllerResponses.Add(Controllers(", \"group_ids\": [3]"));
        api.ControllerResponses.Add(Controllers(string.Empty));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        await service.FetchAllDevicesAsync();
        var cached = provider.OfType<ControllerDeviceModel>().Single();

        Assert.Equal(new[] { 3 }, cached.DeviceGroups);                  // "받지 못함" 을 "소속 없음" 으로 비우지 않는다
        Assert.True(cached.ToControllerDeviceDto().GroupIdsUnchanged);
    }

    [Fact]
    public async Task should_adopt_refetched_membership_as_baseline_when_cache_instance_is_kept()
    {
        var api = new MockDeviceApiService();
        api.ControllerResponses.Add(Controllers(", \"group_ids\": [3]"));
        api.ControllerResponses.Add(Controllers(", \"group_ids\": [3, 4]"));
        var (service, provider) = CreateService(api);

        await service.FetchAllDevicesAsync();
        var first = provider.OfType<ControllerDeviceModel>().Single();
        await service.FetchAllDevicesAsync();
        var second = provider.OfType<ControllerDeviceModel>().Single();

        Assert.Same(first, second);
        Assert.Equal(new[] { 3, 4 }, second.DeviceGroups);
        Assert.True(second.ToControllerDeviceDto().GroupIdsUnchanged);
    }
    #endregion

    #region - 결선 방식 보존 (CONN) -
    [Fact]
    public void should_carry_stored_connection_type_when_controller_model_is_saved()
    {
        var model = JsonConvert.DeserializeObject<ControllerDeviceDto>(
            "{ \"id\": 1, \"number_device\": 1, \"name_device\": \"c\", \"status\": \"ACTIVATED\", \"category_device\": \"controller\", "
          + "\"type_controller\": \"Controller\", \"connection\": { \"type\": \"IP_CONVERTER\", \"ip_address\": \"10.0.0.1\", \"ip_port\": 9000 } }")!
            .ToControllerDeviceModel();
        model.DeviceName = "edited";

        var dto = model.ToControllerDeviceDto();

        Assert.Equal("IP_CONVERTER", dto.ConnectionAxis!.Type);
    }
    #endregion

    #region - 100대 넘는 빼기 (G5 / G7) -
    [Fact]
    public async Task should_split_undo_into_chunks_of_100_when_more_than_100_devices_are_undone()
    {
        var calls = new List<int>();
        var api = new MockDeviceApiService
        {
            RemoveHook = (g, dto) =>
            {
                calls.Add(dto.DeviceIds.Count);
                return dto.DeviceIds.Count > 100
                    ? ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("VALIDATION_ERROR", "at most 100")
                    : ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(new DeviceGroupBulkRemoveResultDto { GroupId = g, RemovedDeviceIds = dto.DeviceIds.ToList() });
            },
        };
        var models = Enumerable.Range(1, 150).Select(i => (IBaseDeviceModel)new LampDeviceModel { Id = i, DeviceGroups = new List<int> { 9 } }).ToList();
        var handler = new DeviceGroupDropHandler(api, () => models);

        var line = await handler.UndoAsync(new GroupDropUndo(9, "G", models.Select(m => m.Id).ToList()));

        Assert.Equal(new[] { 100, 50 }, calls);
        Assert.All(models, m => Assert.DoesNotContain(9, m.DeviceGroups!));
        Assert.Contains("150대를 되돌렸습니다", line);
    }

    [Fact]
    public async Task should_return_remaining_undo_when_a_chunk_fails()
    {
        var call = 0;
        var api = new MockDeviceApiService
        {
            RemoveHook = (g, dto) => ++call == 2
                ? ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("SERVER", "down")
                : ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(new DeviceGroupBulkRemoveResultDto { GroupId = g, RemovedDeviceIds = dto.DeviceIds.ToList() }),
        };
        var models = Enumerable.Range(1, 120).Select(i => (IBaseDeviceModel)new LampDeviceModel { Id = i, DeviceGroups = new List<int> { 9 } }).ToList();
        var handler = new DeviceGroupDropHandler(api, () => models);
        GroupDropResult? result = null;
        handler.Completed += r => result = r;

        await handler.UndoAsync(new GroupDropUndo(9, "G", models.Select(m => m.Id).ToList()));

        Assert.NotNull(result?.Undo);
        Assert.Equal(Enumerable.Range(101, 20), result!.Undo!.DeviceIds);          // 남은 것만 다시 되돌릴 수 있다
        Assert.All(models.Take(100), m => Assert.DoesNotContain(9, m.DeviceGroups!));
        Assert.All(models.Skip(100), m => Assert.Contains(9, m.DeviceGroups!));
    }

    [Fact]
    public async Task should_split_wiring_group_removal_into_chunks_of_100_when_more_than_100_sensors_leave_a_group()
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, 130).Select(i => (
            Id: 101 + i, Channel: (int?)null, Facts: WiringDoubles.Facts(1101 + i, i + 1),
            Placement: (WiringPlacement?)null, Issue: (string?)null, Groups: (IReadOnlyList<int>?)new List<int> { 7 })));
        foreach (var row in board.Rows) row.Groups.Remove(7);
        var gateway = new WiringFakeGateway();
        var service = new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy());

        var result = await service.ApplyAsync(10, board);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 100, 30 }, gateway.GroupCalls.Where(c => !c.Add).Select(c => c.DeviceIds.Count).ToArray());
    }
    #endregion

    #region - 결선 창 그룹 기준선 (W-R3 / W-R4) -
    private static WiringViewModel OpenWiring(WiringFakeGateway gateway, params int[] groups)
    {
        var seeds = new[]
        {
            new WiringSensorSeed(101, 1, new SensorFacts(1101, "북측 1구간 펜스", "Fence", "북측 7구간"), null, null, new List<int> { 7 }),
        };
        gateway.Fetched[101] = WiringDoubles.ServerSensor(101, 1101, 1, null);
        return WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103"),
            seeds,
            new[] { "Fence" },
            new WiringApplyService(gateway, null, null, WiringDoubles.AxisPolicy()),
            new WiringFakeDialogs { Confirm = true },
            groups.Select(g => new WiringGroupInfo(g, $"그룹 {g}")).ToList());
    }

    [Fact]
    public async Task should_become_clean_when_group_only_change_is_saved()
    {
        var gateway = new WiringFakeGateway();
        var vm = OpenWiring(gateway, 7, 8);
        vm.OnSelectionChanged(new[] { vm.Rows.Single() });
        vm.ToggleGroup(vm.GroupChecks.Single(g => g.Id == 8));
        vm.ApplyEdit();

        await vm.SaveAsync();

        Assert.Single(gateway.GroupCalls);
        Assert.False(vm.HasChanges);                  // 종전: 그룹만 바꾼 저장은 기준선이 안 옮겨져 계속 더러웠다
    }

    [Fact]
    public async Task should_keep_group_change_pending_when_row_patch_succeeds_but_group_call_fails()
    {
        var gateway = new WiringFakeGateway();
        gateway.GroupFails.Add(8);
        var vm = OpenWiring(gateway, 7, 8);
        vm.OnSelectionChanged(new[] { vm.Rows.Single() });
        vm.EditName = "북측 1구간 펜스 (수정)";
        vm.ToggleGroup(vm.GroupChecks.Single(g => g.Id == 8));
        vm.ApplyEdit();

        await vm.SaveAsync();

        Assert.Single(gateway.Patched);
        var row = vm.Rows.Single().Row;
        Assert.False(row.FactsChanged);               // 표 값은 저장됐다
        Assert.True(row.GroupsChanged);               // 그룹 변경은 실패했으니 남는다(종전: 조용히 기준선으로 먹었다)
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void should_keep_old_group_baseline_when_new_row_is_promoted()
    {
        var board = new WiringBoard();
        var row = board.AddRow(new SensorFacts(1201, "새 센서", "Fence", string.Empty));
        row.Groups.Add(8);

        var promoted = board.Promote(row.Key, 950)!;

        Assert.True(promoted.GroupsChanged);          // 그룹 넣기가 끝나기 전에는 "바뀐 것"이다
        board.MarkGroupsSaved(new[] { (8, true, (IReadOnlyList<int>)new List<int> { 950 }) });
        Assert.False(promoted.GroupsChanged);
    }
    #endregion
}
