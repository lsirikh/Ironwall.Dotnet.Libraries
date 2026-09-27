using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : 콘솔에서 바꾼 장비 그룹 소속이 지도의 구역선(PidsGroup) 이벤트 조회표까지 닿는가.
                  종전: 조회표는 부팅 · 전량 재조회 · 편집 모드 해제 때만 만들어져, 부팅 때 비어 있던 그룹에
                  콘솔로 첫 장비를 넣어도 그 구역선은 탐지 · 장애 · 제어기 무통신 색을 끝내 받지 못했다
                  ("그룹 심볼 미등록 … no-op"). 마지막 장비를 빼도 등록이 남았다.
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class ZoneLineMembershipRegistrationTests
{
    private const int GroupId = 7;

    private static SensorDeviceModel Sensor(int id, params int[] groups)
        => new() { Id = id, DeviceType = EnumDeviceType.Fence, DeviceGroups = new List<int>(groups) };

    /// <summary>실제 EQM → SEM 배선(EventUiModule 과 같다) — 이벤트가 큐를 거쳐 구역선 색이 되는지 끝까지 본다.</summary>
    private static (EventQueueManager Queue, SymbolEventManager Symbols) Pipeline()
    {
        var queue = new EventQueueManager();
        var symbols = new SymbolEventManager(new Mock<IEventAggregator>().Object,
                                             new Mock<ILogService>().Object,
                                             new EventSetupModel(new Mock<IEventSetupModel>().Object),
                                             queue);
        queue.OnGroupStateChanged += symbols.HandleGroupStateChanged;
        return (queue, symbols);
    }

    private static void Detect(EventQueueManager queue, IBaseDeviceModel device)
        => queue.Enqueue(new EventEntry
        {
            DeviceId = device.Id,
            DeviceType = device.DeviceType,
            GroupIds = device.DeviceGroups?.ToList(),
            EventType = EnumEventType.Intrusion,
            EventId = 1,
            TimeoutSeconds = 600,
        });

    [Fact]
    public void should_colour_zone_line_when_group_empty_at_boot_gets_a_device_from_console()
    {
        // Arrange — 부팅: 그룹 7 에는 장비가 없어 구역선이 등록되지 않았다
        var (queue, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId, Title = "구역 7" };
        var device = Sensor(101);
        var devices = new List<IBaseDeviceModel> { device };

        // 콘솔이 장비를 그룹 7 에 넣었다(공용 모델을 제자리에서 고친다) → 알림
        device.DeviceGroups!.Add(GroupId);
        var message = DeviceGroupMembershipChangedMessage.For(new[] { GroupId })!;

        // Act
        var outcome = ZoneLineRegistration.Sync(symbols, devices, new IPidsGroupSymbolModel[] { line }, message.GroupIds);
        Detect(queue, device);

        // Assert — 등록됐고, 그 장비의 탐지가 구역선을 칠한다
        Assert.Equal(ZoneLineRegistrationOutcome.Registered, outcome[GroupId]);
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
    }

    [Fact]
    public void should_leave_zone_line_uncoloured_when_membership_change_is_not_synced()
    {
        // 결함 재현(대조군) — 알림을 처리하지 않으면 같은 탐지가 구역선에 닿지 않는다
        var (queue, _) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        var device = Sensor(101);
        device.DeviceGroups!.Add(GroupId);

        Detect(queue, device);

        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);
    }

    [Fact]
    public void should_unregister_zone_line_when_last_member_is_removed()
    {
        // Arrange — 등록된 구역선
        var (queue, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        var device = Sensor(101, GroupId);
        var devices = new List<IBaseDeviceModel> { device };
        ZoneLineRegistration.Sync(symbols, devices, new IPidsGroupSymbolModel[] { line }, new[] { GroupId });

        // 콘솔이 마지막 장비를 뺐다
        device.DeviceGroups!.Remove(GroupId);

        // Act
        var outcome = ZoneLineRegistration.Sync(symbols, devices, new IPidsGroupSymbolModel[] { line }, new[] { GroupId });

        // Assert — 해제됐고, 옛 소속(그룹 7 을 실은) 이벤트가 와도 선은 그대로다
        Assert.Equal(ZoneLineRegistrationOutcome.Unregistered, outcome[GroupId]);
        queue.Enqueue(new EventEntry { DeviceId = 101, DeviceType = EnumDeviceType.Fence, GroupIds = new List<int> { GroupId }, EventType = EnumEventType.Intrusion, EventId = 2, TimeoutSeconds = 600 });
        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);
    }

    [Fact]
    public void should_apply_live_queue_state_when_zone_line_is_registered_late()
    {
        // 그룹 7 에 이미 살아 있는 탐지가 큐에 있는데 구역선이 늦게 등록된다 — 등록 즉시 큐 상태를 받는다
        var (queue, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        var device = Sensor(101, GroupId);
        Detect(queue, device);
        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);   // 아직 미등록

        ZoneLineRegistration.Sync(symbols, new List<IBaseDeviceModel> { device }, new IPidsGroupSymbolModel[] { line }, new[] { GroupId });

        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
    }

    [Fact]
    public void should_touch_only_affected_groups_when_syncing()
    {
        // 다른 그룹(8)의 구역선은 건드리지 않는다 — 전량 재구성이 아니다
        var (_, symbols) = Pipeline();
        var line7 = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        var line8 = new PidsGroupSymbolModel { LinkedDeviceGroup = 8 };
        var devices = new List<IBaseDeviceModel> { Sensor(101, GroupId), Sensor(102, 8) };
        var lines = new IPidsGroupSymbolModel[] { line7, line8 };

        var outcome = ZoneLineRegistration.Sync(symbols, devices, lines, new[] { GroupId });

        Assert.Single(outcome);
        Assert.True(outcome.ContainsKey(GroupId));
        symbols.SetGroupDetecting(8, EnumEventType.Intrusion);
        Assert.Equal(EnumCompositeEventStatus.Normal, line8.CompositeStatus);   // 8 은 등록되지 않았다
    }

    [Fact]
    public void should_report_no_zone_line_when_group_has_no_linked_line()
    {
        var (_, symbols) = Pipeline();

        var outcome = ZoneLineRegistration.Sync(symbols, new List<IBaseDeviceModel> { Sensor(101, GroupId) },
            Array.Empty<IPidsGroupSymbolModel>(), new[] { GroupId, 0, -3 });

        Assert.Equal(ZoneLineRegistrationOutcome.NoZoneLine, Assert.Single(outcome).Value);
    }

    [Fact]
    public void should_move_registration_when_zone_line_is_relinked_to_another_group()
    {
        // 속성창에서 구역선의 연결 그룹을 5 → 7 로 바꿨다 — 옛 5 는 내리고 7 은 칠해지며, 5 를 내리는 것이 7 의 색을 지우지 않는다
        var (queue, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = 5 };
        var devices = new List<IBaseDeviceModel> { Sensor(101, 5), Sensor(102, GroupId) };
        ZoneLineRegistration.Sync(symbols, devices, new IPidsGroupSymbolModel[] { line }, new[] { 5 });
        Detect(queue, devices[1]);   // 그룹 7 탐지 — 아직 선은 5 에 연결

        line.LinkedDeviceGroup = GroupId;
        ZoneLineRegistration.Sync(symbols, devices, new IPidsGroupSymbolModel[] { line }, new[] { GroupId, 5 });

        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
        symbols.RestoreGroupSymbol(5);   // 옛 그룹의 복원 신호가 와도 선은 이제 5 가 아니다
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);
    }

    // ── 지도 뷰모델 배선 ──────────────────────────────────────────────

    private static MapViewModel MapWith(SymbolEventManager symbols, DeviceProvider provider)
    {
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(MapViewModel).GetField("_symbolEventManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, symbols);
        typeof(MapViewModel).GetField("<DeviceProvider>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, provider);
        return vm;
    }

    [Fact]
    public void should_handle_membership_message_when_map_view_model_subscribes()
    {
        Assert.True(typeof(IHandle<DeviceGroupMembershipChangedMessage>).IsAssignableFrom(typeof(MapViewModel)));
    }

    [Fact]
    public async Task should_unregister_zone_line_when_map_receives_membership_message_for_emptied_group()
    {
        // Arrange — 그룹 7 이 등록돼 있다가 소속이 비었다(공용 프로바이더에는 그 그룹 장비가 없다)
        var (_, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        symbols.RegisterGroupSymbol(GroupId, Sensor(101, GroupId), line);
        var provider = new DeviceProvider();
        provider.Add(Sensor(101));
        var vm = MapWith(symbols, provider);

        // Act
        await vm.HandleAsync(new DeviceGroupMembershipChangedMessage(new[] { GroupId }), CancellationToken.None);

        // Assert — 조회표에서 내려가 그룹 이벤트가 더는 닿지 않는다
        symbols.SetGroupDetecting(GroupId, EnumEventType.Intrusion);
        Assert.Equal(EnumCompositeEventStatus.Normal, line.CompositeStatus);
    }

    [Fact]
    public async Task should_ignore_empty_membership_message_when_no_groups()
    {
        var (_, symbols) = Pipeline();
        var line = new PidsGroupSymbolModel { LinkedDeviceGroup = GroupId };
        symbols.RegisterGroupSymbol(GroupId, Sensor(101, GroupId), line);
        var vm = MapWith(symbols, new DeviceProvider());

        await vm.HandleAsync(new DeviceGroupMembershipChangedMessage(Array.Empty<int>()), CancellationToken.None);

        symbols.SetGroupDetecting(GroupId, EnumEventType.Intrusion);
        Assert.Equal(EnumCompositeEventStatus.Detecting, line.CompositeStatus);   // 아무것도 내리지 않았다
    }

    // ── 메시지 계약 ───────────────────────────────────────────────

    [Fact]
    public void should_carry_added_and_removed_groups_when_membership_diff_changes()
    {
        var message = DeviceGroupMembershipChangedMessage.FromDiff(new[] { 1, 2, 3 }, new[] { 2, 3, 4 });

        Assert.NotNull(message);
        Assert.Equal(new[] { 1, 4 }, message!.GroupIds.OrderBy(x => x));
    }

    [Fact]
    public void should_return_null_when_membership_is_unchanged_or_unknown()
    {
        Assert.Null(DeviceGroupMembershipChangedMessage.FromDiff(new[] { 2, 1 }, new[] { 1, 2 }));
        Assert.Null(DeviceGroupMembershipChangedMessage.FromDiff(new[] { 1 }, null));   // 받지 못한 소속 = 변화 아님
        Assert.Null(DeviceGroupMembershipChangedMessage.For(new[] { 0, -1 }));
    }

    [Fact]
    public void should_treat_missing_before_as_empty_when_device_is_new()
    {
        var message = DeviceGroupMembershipChangedMessage.FromDiff(null, new[] { 9 });

        Assert.Equal(new[] { 9 }, message!.GroupIds);
    }
}
