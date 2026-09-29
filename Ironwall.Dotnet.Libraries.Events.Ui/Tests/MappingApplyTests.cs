using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : [적용] 한 번을 통째로 — 호출 수 · 드리프트 · 부분 실패 · 판본 관문 · 스레드
   Created By   : Claude
   Created On   : 2026-09-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 서버 왕복을 <b>세는</b> 가짜 게이트웨이.
/// </summary>
/// <remarks>
/// 🔴 <b>반드시 한 번 쉬었다가</b>(<see cref="Task.Yield"/>) 돌려준다.
/// <c>Task.FromResult</c> 로 즉시 끝나는 가짜는 <c>await</c> 가 <b>동기로 이어져</b>
/// 스레드 관련 결함을 전부 감춘다 — 실서버는 절대 그렇게 돌려주지 않는다.
/// </remarks>
internal sealed class CountingGateway : IMappingWorkbenchGateway
{
    public List<string> Calls { get; } = new();

    /// <summary>드리프트 시늉 — 대조용 재조회에서 다른 시각을 돌려준다.</summary>
    public bool ParentDrifted { get; set; }

    /// <summary>배선 행이 남의 손에 바뀐 것으로 꾸민다.</summary>
    public bool ChildDrifted { get; set; }

    /// <summary>벌크 등록에서 마지막 항목을 실패시킨다.</summary>
    public bool FailLastCreate { get; set; }

    /// <summary>판본 관문에 걸린 것처럼 전부 거절한다.</summary>
    public bool Unsupported { get; set; }

    public List<MappingCameraReadDto> Cameras { get; } = new();

    private int _nextConfigId = 900;

    private async Task<MappingCallResult<T>> Reply<T>(string call, Func<T> value)
    {
        Calls.Add(call);
        await Task.Yield();                       // ★ 실서버처럼 한 번 쉰다
        if (Unsupported) return MappingCallResult<T>.Fail(MappingWorkbenchGateway.ContractRefusal, null, 0);
        return MappingCallResult<T>.Ok(value());
    }

    /// <summary>있으면 목록 조회가 이 문이 열릴 때까지 기다린다 — 조회가 도는 사이에 사람이 손대는 경합을 만든다.</summary>
    public TaskCompletionSource? ListGate { get; set; }

    public async Task<MappingCallResult<IReadOnlyList<EventMappingReadDto>>> ListMappingsAsync(CancellationToken token = default)
    {
        if (ListGate is { } gate) await gate.Task;
        return await Reply<IReadOnlyList<EventMappingReadDto>>("list-mappings", () => new[]
        {
            new EventMappingReadDto { Id = 1, NameEvent = "울타리 침입", CategoryEventMapping = "FENCE_SENSOR_ONLY", DeviceGroupId = 3, Status = true, UpdatedAt = "T0" },
        });
    }

    public Task<MappingCallResult<EventMappingReadDto>> GetMappingAsync(int mappingId, CancellationToken token = default)
        => Reply("get-mapping", () => new EventMappingReadDto
        {
            Id = mappingId, NameEvent = "울타리 침입", CategoryEventMapping = "FENCE_SENSOR_ONLY",
            DeviceGroupId = 3, Status = true, UpdatedAt = ParentDrifted ? "T9" : "T0",
        });

    public Task<MappingCallResult<IReadOnlyList<MappingCameraReadDto>>> ListCamerasAsync(int mappingId, CancellationToken token = default)
        => Reply<IReadOnlyList<MappingCameraReadDto>>("list-cameras", () => Cameras
            .Select(c => new MappingCameraReadDto
            {
                ConfigId = c.ConfigId, EventMappingId = c.EventMappingId, Camera = c.Camera,
                DelayTime = c.DelayTime, IsEnable = c.IsEnable, Priority = c.Priority,
                UpdatedAt = ChildDrifted ? "T9" : c.UpdatedAt,
            })
            .ToList());

    public Task<MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>> ListSpeakersAsync(int mappingId, CancellationToken token = default)
        => Reply<IReadOnlyList<MappingSpeakerReadDto>>("list-speakers", () => Array.Empty<MappingSpeakerReadDto>());

    public Task<MappingCallResult<IReadOnlyList<MappingLampReadDto>>> ListLampsAsync(int mappingId, CancellationToken token = default)
        => Reply<IReadOnlyList<MappingLampReadDto>>("list-lamps", () => Array.Empty<MappingLampReadDto>());

    public Task<MappingCallResult<EventMappingReadDto>> CreateMappingAsync(EventMappingCreateDto body, CancellationToken token = default)
        => Reply("create-mapping", () => new EventMappingReadDto { Id = 2, NameEvent = body.NameEvent, Status = body.Status, UpdatedAt = "T0" });

    public Task<MappingCallResult<EventMappingReadDto>> PatchMappingAsync(int mappingId, EventMappingUpdateDto body, CancellationToken token = default)
        => Reply("patch-mapping", () => new EventMappingReadDto { Id = mappingId, UpdatedAt = "T1" });

    public Task<MappingCallResult<MappingBulkCreateResultDto>> BulkCreateAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<object> items, CancellationToken token = default)
        => Reply($"bulk-create:{kind}:{items.Count}", () =>
        {
            var failing = FailLastCreate && items.Count > 0;
            var created = new List<int>();
            for (var i = 0; i < items.Count - (failing ? 1 : 0); i++)
            {
                var id = _nextConfigId++;
                created.Add(id);
                if (items[i] is MappingCameraCreateDto c)
                    Cameras.Add(new MappingCameraReadDto
                    {
                        ConfigId = id, EventMappingId = mappingId, DelayTime = c.DelayTime,
                        IsEnable = c.IsEnable, Priority = c.Priority, UpdatedAt = "T0",
                        Camera = new MappingDeviceRefDto { Id = c.CameraId, CategoryDevice = "Camera" },
                    });
            }
            return new MappingBulkCreateResultDto
            {
                MappingId = mappingId,
                CreatedIds = created,
                FailedItems = failing
                    ? new List<MappingBulkFailedItemDto> { new() { Index = items.Count - 1, Error = "Camera not found" } }
                    : new List<MappingBulkFailedItemDto>(),
            };
        });

    public Task<MappingCallResult<MappingBulkUnassignResultDto>> BulkUnassignAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<int> configIds, CancellationToken token = default)
        => Reply($"bulk-unassign:{kind}:{configIds.Count}", () =>
        {
            Cameras.RemoveAll(c => configIds.Contains(c.ConfigId));
            return new MappingBulkUnassignResultDto { MappingId = mappingId, RemovedConfigIds = configIds.ToList() };
        });

    public Task<MappingCallResult<bool>> PatchConfigAsync(
        int mappingId, MappingActionKind kind, int configId, object body, CancellationToken token = default)
        => Reply($"patch-config:{kind}:{configId}", () => true);

    /// <summary>같은 접두사로 시작하는 호출 수.</summary>
    public int CountOf(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));
}

/// <summary>팔레트·이름 조인용 가짜 장비 캐시.</summary>
internal sealed class StubDevices : IMappingDeviceSource
{
    private readonly List<MappingDeviceInfo> _cameras = Enumerable.Range(370, 6)
        .Select(i => new MappingDeviceInfo(i, $"카메라{i}", "PTZ", new[] { 3 }, true))
        .ToList();

    public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind)
        => kind == MappingActionKind.Camera ? _cameras : Array.Empty<MappingDeviceInfo>();

    public IReadOnlyList<MappingGroupInfo> Groups() => new[] { new MappingGroupInfo(3, "1구역") };

    public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId)
        => Devices(kind).FirstOrDefault(d => d.Id == deviceId);
}

/// <summary>
/// [적용] 을 통째로 돌려 본다 — C1~C6 은 이 한 파일에서 잡혔어야 했다.
/// </summary>
public class MappingApplyTests
{
    private static async Task<(MappingWorkbenchViewModel Vm, CountingGateway Gateway)> LoadedAsync(
        Action<CountingGateway>? arrange = null, int rows = 3, bool serverNumbered = false)
    {
        var gateway = new CountingGateway();
        for (var i = 0; i < rows; i++)
            gateway.Cameras.Add(new MappingCameraReadDto
            {
                ConfigId = 700 + i, EventMappingId = 1, DelayTime = 0, IsEnable = true,
                // 기본은 서버가 한 번도 번호를 안 매긴 보드다(app/schemas/integration.py:221 의 기본값이 None).
                Priority = serverNumbered ? i + 1 : null,
                UpdatedAt = "T0",
                Camera = new MappingDeviceRefDto { Id = 370 + i, CategoryDevice = "Camera" },
            });
        arrange?.Invoke(gateway);

        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices());
        await vm.ReloadAsync();
        gateway.Calls.Clear();
        return (vm, gateway);
    }

    #region - C6 : 건드리지 않은 순서를 보내지 않는다 -
    [Fact]
    public async Task should_send_no_priority_patch_when_order_was_never_touched()
    {
        // 서버가 priority 를 null 로 내려 주는 보드에서 카메라 하나만 넣었다.
        var (vm, gateway) = await LoadedAsync();

        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        Assert.Equal(0, gateway.CountOf("patch-config"));
        Assert.Equal(1, gateway.CountOf("bulk-create"));
    }

    [Fact]
    public async Task should_patch_only_moved_rows_when_the_server_already_numbered_them()
    {
        var (vm, gateway) = await LoadedAsync(serverNumbered: true);

        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[2]);
        vm.OnSelectionChanged();
        vm.MoveUp();                                  // 3번 행을 2번 자리로 — 두 행만 번호가 바뀐다
        await vm.ApplyAsync();

        Assert.Equal(2, gateway.CountOf("patch-config"));
    }

    [Fact]
    public async Task should_number_every_row_on_the_first_move_when_the_server_left_them_null()
    {
        // 서버가 번호를 안 매겼으면 보존할 순서 자체가 없다 — 첫 이동에서 전 행에 번호를 준다.
        // C6 의 결함은 이것이 아니라 "끌지도 않았는데" 그렇게 되던 것이었다.
        var (vm, gateway) = await LoadedAsync();

        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[2]);
        vm.OnSelectionChanged();
        vm.MoveUp();
        await vm.ApplyAsync();

        Assert.Equal(3, gateway.CountOf("patch-config"));
    }
    #endregion

    #region - 저장 한 번의 호출 수 -
    [Fact]
    public async Task should_send_one_bulk_call_when_many_devices_are_added()
    {
        var (vm, gateway) = await LoadedAsync();

        vm.AddDevices(new[] { 373, 374, 375 }, -1);
        await vm.ApplyAsync();

        Assert.Equal(1, gateway.CountOf("bulk-create"));
        Assert.Contains("bulk-create:Camera:3", gateway.Calls);
    }

    [Fact]
    public async Task should_release_before_creating_when_both_are_pending()
    {
        var (vm, gateway) = await LoadedAsync();

        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        vm.ReleaseSelected();
        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        var release = gateway.Calls.FindIndex(c => c.StartsWith("bulk-unassign", StringComparison.Ordinal));
        var create = gateway.Calls.FindIndex(c => c.StartsWith("bulk-create", StringComparison.Ordinal));
        Assert.True(release >= 0 && create >= 0);
        Assert.True(release < create, "해제가 등록보다 먼저 나가야 '뺐다가 다시 넣기' 가 중복으로 막히지 않는다");
    }

    [Fact]
    public async Task should_send_nothing_when_nothing_changed()
    {
        var (vm, gateway) = await LoadedAsync();

        await vm.ApplyAsync();

        Assert.Empty(gateway.Calls);
    }
    #endregion

    #region - C3 : 드리프트면 한 건도 보내지 않는다 -
    [Fact]
    public async Task should_send_nothing_when_the_parent_mapping_drifted()
    {
        var (vm, gateway) = await LoadedAsync(g => g.ParentDrifted = true);

        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        Assert.Equal(0, gateway.CountOf("bulk-create"));
        Assert.Contains("다른 사용자", vm.StatusText);
        Assert.True(vm.IsDirty, "Draft 를 버리지 않는다");
    }

    [Fact]
    public async Task should_send_nothing_when_a_wiring_row_drifted()
    {
        // 🔴 서버의 onupdate 는 그 행에만 걸린다 — 배선을 고쳐도 맵핑 본체 시각은 그대로다.
        //    부모만 보면 두 사람이 같은 보드를 고쳐도 둘 다 통과한다.
        var (vm, gateway) = await LoadedAsync();
        gateway.ChildDrifted = true;

        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        Assert.Equal(0, gateway.CountOf("bulk-create"));
        Assert.Contains("다른 사용자", vm.StatusText);
    }

    [Fact]
    public async Task should_read_child_rows_when_checking_for_drift()
    {
        var (vm, gateway) = await LoadedAsync();

        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        Assert.True(gateway.CountOf("list-cameras") >= 1, "부모만 읽고 끝내면 배선 드리프트를 못 본다");
    }
    #endregion

    #region - C4 : 부분 실패는 화면에 남는다 -
    [Fact]
    public async Task should_keep_failed_rows_when_the_bulk_create_partly_failed()
    {
        var (vm, gateway) = await LoadedAsync();
        gateway.FailLastCreate = true;

        vm.AddDevices(new[] { 374, 375 }, -1);
        await vm.ApplyAsync();

        // 하나는 만들어졌고 하나는 서버에 없다 — 없는 쪽이 재조회에 쓸려 사라지면 안 된다.
        Assert.Equal(5, vm.BoardRows.Count);
        Assert.Contains(vm.BoardRows, r => r.HasFailure);
    }

    [Fact]
    public async Task should_stay_dirty_when_the_apply_partly_failed()
    {
        var (vm, gateway) = await LoadedAsync();
        gateway.FailLastCreate = true;

        vm.AddDevices(new[] { 374, 375 }, -1);
        await vm.ApplyAsync();

        Assert.True(vm.IsDirty, "보낼 것이 남았는데 깨끗하다고 표시하면 안 된다");
        Assert.Contains("부분 적용", vm.StatusText);
    }

    [Fact]
    public async Task should_report_clean_when_the_apply_fully_succeeded()
    {
        var (vm, _) = await LoadedAsync();

        vm.AddDevices(new[] { 375 }, -1);
        await vm.ApplyAsync();

        Assert.False(vm.IsDirty);
        Assert.StartsWith("적용 완료", vm.StatusText);
    }
    #endregion

    #region - C7 : 판본 관문 -
    [Fact]
    public async Task should_refuse_every_call_when_the_server_contract_is_too_old()
    {
        var gateway = new CountingGateway { Unsupported = true };
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices());

        await vm.ReloadAsync();

        Assert.Empty(vm.Mappings);
        Assert.Contains("지원하지", vm.StatusText);
    }
    #endregion

    #region - C2 : 이어지는 코드가 부른 쪽 스레드로 돌아온다 -
    [Fact]
    public async Task should_mutate_bound_collections_on_the_calling_thread()
    {
        // 🔴 ConfigureAwait(false) 를 쓰면 이어지는 코드가 스레드풀로 떨어지고,
        //    바로 뒤에서 ObservableCollection 을 고쳐 실서버에서 매 조회가 크로스스레드로 터진다.
        var previous = SynchronizationContext.Current;
        var context = new RecordingSyncContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var gateway = new CountingGateway();
            gateway.Cameras.Add(new MappingCameraReadDto
            {
                ConfigId = 700, EventMappingId = 1, UpdatedAt = "T0",
                Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
            });
            var vm = new MappingWorkbenchViewModel(gateway, new StubDevices());

            var caller = Environment.CurrentManagedThreadId;
            var offThread = new List<string>();
            void Watch(string name, INotifyCollectionChanged collection)
                => collection.CollectionChanged += (_, _) =>
                {
                    if (Environment.CurrentManagedThreadId != caller) offThread.Add(name);
                };

            Watch(nameof(vm.Mappings), vm.Mappings);
            Watch(nameof(vm.BoardRows), vm.BoardRows);
            Watch(nameof(vm.PaletteItems), vm.PaletteItems);

            await vm.ReloadAsync();

            Assert.Empty(offThread);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>이어붙인 작업을 <b>같은 스레드에서</b> 돌리는 최소 컨텍스트(WPF Dispatcher 흉내).</summary>
    private sealed class RecordingSyncContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }
    #endregion
}

/// <summary>
/// C1 회귀 가드 — 순서 드롭존이 <b>ItemsControl</b> 위에 있는가.
/// </summary>
/// <remarks>
/// 커널은 삽입 위치를 <c>zone is ItemsControl</c> 일 때만 계산하고(<c>CaptureDragBehavior.cs:205</c>),
/// 그 index 가 음수면 후보로 올리지 않는다(<c>:218</c>). <c>ContentControl</c> 인 <c>DropZoneChrome</c> 에
/// 걸어 두면 <b>점선은 보이는데 Drop 이 영영 안 온다</b> — 눈으로도 미리보기로도 안 잡힌다.
/// 그리고 <c>ReorderKeyboardBehavior</c> 는 <c>DropZone.GetKey</c> 를 <b>목록에서</b> 읽으므로
/// 키가 다른 곳에 있으면 <c>Alt+↑↓</c> 가 통째로 죽는다.
/// </remarks>
public class MappingDropZoneMarkupTests
{
    private static string Markup()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.Events.Ui.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "Consoles", "Mapping", "MappingWorkbenchView.xaml");
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    /// <summary>그 속성이 붙은 여는 태그의 요소 이름.</summary>
    private static IReadOnlyList<string> OwnersOf(string attribute)
    {
        var markup = Markup();
        var owners = new List<string>();
        foreach (Match m in Regex.Matches(markup, @"<(?<tag>[\w:.]+)(?<body>[^>]*?)/?>", RegexOptions.Singleline))
        {
            if (m.Groups["body"].Value.Contains(attribute, StringComparison.Ordinal))
                owners.Add(m.Groups["tag"].Value);
        }
        return owners;
    }

    [Fact]
    public void should_put_the_reorder_zone_on_a_list_when_declaring_drop_targets()
    {
        var owners = OwnersOf("drag:DropZone.IsReorder");

        Assert.NotEmpty(owners);
        Assert.All(owners, tag => Assert.Equal("ListBox", tag));
    }

    [Fact]
    public void should_put_the_reorder_zone_key_on_the_same_list()
    {
        // 키가 목록에 없으면 ReorderKeyboardBehavior.MoveSelection 이 즉시 false 를 돌려준다.
        var reorder = OwnersOf("drag:DropZone.IsReorder");
        var keys = OwnersOf("drag:DropZone.Key");

        Assert.All(reorder, tag => Assert.Contains(tag, keys));
    }

    [Fact]
    public void should_keep_the_handler_next_to_the_zone_key()
    {
        var keys = OwnersOf("drag:DropZone.Key");
        var handlers = OwnersOf("drag:DropZone.Handler");

        Assert.Equal(keys.Count, handlers.Count);
        Assert.All(keys, tag => Assert.Contains(tag, handlers));
    }

    [Fact]
    public void should_declare_a_keyboard_fallback_on_every_drag_source()
    {
        var markup = Markup();
        var sources = Regex.Matches(markup, @"<drag:CaptureDragBehavior(?<body>.*?)/>", RegexOptions.Singleline);

        Assert.NotEmpty(sources);
        foreach (Match m in sources)
            Assert.Contains("KeyboardFallback=", m.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void should_not_put_automation_ids_on_peerless_types()
    {
        // Border · Grid · StackPanel · ContentControl 파생은 UIA 트리에 나오지 않는다.
        var peerless = new[] { "Border", "Grid", "StackPanel", "drag:DropZoneChrome", "TextBlock" };

        foreach (var tag in peerless)
            Assert.DoesNotContain(tag, OwnersOf("AutomationProperties.AutomationId"));
    }
}
