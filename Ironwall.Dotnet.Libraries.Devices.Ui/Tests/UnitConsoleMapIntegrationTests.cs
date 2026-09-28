using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-31 (FR-01 · FR-02 · FR-08 · FR-32 · FR-48 · FR-49) — 부대 콘솔 ↔ 관계도 결선.
/// 시나리오: SIM-R001~004 · SIM-N064~067 · SIM-F100 · SIM-F105 · SIM-Q046~048 · SIM-M031.
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleMapIntegrationTests
{
    #region - 레일 (FR-01 · ISSUE-58) -
    [Fact]
    public void should_offer_map_rail_between_tree_and_devices()
    {
        var kit = ConsoleKit.Create();

        Assert.Equal(new[] { UnitConsoleViewModel.RAIL_TREE, UnitConsoleViewModel.RAIL_ADJACENCY, UnitConsoleViewModel.RAIL_DEVICES },
                     kit.Console.RailEntries.Select(e => e.Key));
        Assert.Equal("부대 관계도", kit.Console.RailEntries[1].Label);
    }

    [Fact]
    public async Task should_not_show_a_bare_number_badge_on_map_rail_and_describe_pairs_in_list_status()
    {
        // 조정자 — 배지가 부대 수로 읽히면 안 된다: 숫자 배지는 두지 않고 목록 상태 줄이 "인접 N쌍"을 말한다
        var kit = await ConsoleKit.OpenAsync();
        var map = kit.Console.RailEntries.First(e => e.Key == UnitConsoleViewModel.RAIL_ADJACENCY);

        kit.Console.SelectedRail = map;

        Assert.False(map.ShowCount);
        Assert.Equal(string.Empty, map.CountText);
        Assert.Equal(2, kit.Console.AdjacencyPairCount);
        Assert.Equal("부대 8 · 인접 2쌍", kit.Console.ListStatusText);
        Assert.True(kit.Console.IsAdjacencyView);
    }

    [Fact]
    public async Task should_restore_map_rail_from_prefs_when_last_rail_was_adjacency()
    {
        var kit = ConsoleKit.Create(lastRail: "adjacency");
        await kit.ActivateAsync();

        Assert.True(kit.Console.IsAdjacencyView);
    }

    [Fact]
    public async Task should_remember_rail_choice_in_prefs()
    {
        var kit = await ConsoleKit.OpenAsync();

        kit.Console.SelectedRail = kit.Console.RailEntries.First(e => e.Key == UnitConsoleViewModel.RAIL_ADJACENCY);

        Assert.Equal(UnitConsoleViewModel.RAIL_ADJACENCY, kit.Prefs.Entry.LastRailKey);
        Assert.True(kit.Prefs.Saves >= 1);
    }
    #endregion

    #region - 선택 공유 (FR-02) -
    [Fact]
    public async Task should_share_one_selection_between_tree_and_map()
    {
        var kit = await ConsoleKit.OpenAsync();

        kit.Console.Map.RequestSelect(6);
        await kit.Console.WhenDetailSettledAsync().WaitAsync(ConsoleKit.Timeout);
        Assert.Equal(6, kit.Console.SelectedRow?.Id);

        await kit.Console.SelectByIdAsync(4);
        Assert.Equal(4, kit.Console.Map.SelectedUnitId);
    }

    [Fact]
    public async Task should_block_map_selection_and_say_why_when_detail_is_dirty()
    {
        // v1.3 FR-02 — 가드가 막고 띠로 알린다
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.Form.Description = "손댄 설명";

        kit.Console.Map.RequestSelect(4);

        Assert.Equal(6, kit.Console.SelectedRow?.Id);
        Assert.Equal(6, kit.Console.Map.SelectedUnitId);
    }

    [Fact]
    public void should_implement_map_commands_and_bridge()
    {
        var kit = ConsoleKit.Create();

        Assert.IsAssignableFrom<IUnitMapCommands>(kit.Console);
        Assert.IsAssignableFrom<IUnitMapConsoleBridge>(kit.Console);
        Assert.NotNull(kit.Console.Map);
    }
    #endregion

    #region - 데이터 결선 (FR-03 · FR-48) -
    [Fact]
    public async Task should_hand_tree_and_devices_to_map_after_every_reload()
    {
        var kit = await ConsoleKit.OpenAsync();

        Assert.Equal(8, kit.Console.Map.Scene.Positions.Count);
        Assert.Equal(1, kit.Console.Map.Scene.FactsOf(6).DeviceCount);

        kit.Units.AddNode(9, "c09", "Company", 2);
        await kit.Console.ReloadAsync();

        Assert.Equal(9, kit.Console.Map.Scene.Positions.Count);
    }

    [Fact]
    public async Task should_redraw_map_when_topology_changes_elsewhere()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Units.AddNode(9, "c09", "Company", 2);

        await kit.Console.HandleAsync(new UnitTopologyChangedMessage("CREATED", 9), CancellationToken.None);
        kit.Delay.ElapseAll();
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);

        Assert.Equal(9, kit.Console.Map.Scene.Positions.Count);
    }

    [Fact]
    public async Task should_keep_picture_and_show_band_when_detail_dirty_and_topology_changes()
    {
        // 결정 D-2026-09-27-215b6d — 그림 불변 + 띠 + [다시 읽기]
        var kit = await ConsoleKit.OpenAsync();
        await kit.Console.SelectByIdAsync(6);
        kit.Console.Form.Description = "손댄 설명";
        kit.Units.AddNode(9, "c09", "Company", 2);

        await kit.Console.HandleAsync(new UnitTopologyChangedMessage("CREATED", 9), CancellationToken.None);
        kit.Delay.ElapseAll();
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);

        Assert.Equal(8, kit.Console.Map.Scene.Positions.Count);
        Assert.True(kit.Console.IsExternallyChanged);
    }

    [Fact]
    public async Task should_defer_remote_reload_while_map_confirm_overlay_is_open_then_reload_once()
    {
        // #49 · ISSUE-9 — 관계도의 확인 · M · 끌기가 콘솔 연기 게이트에 들어간다
        var kit = await ConsoleKit.OpenAsync();
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));    // 상위 드롭(대대 3 → 7) → 확인 대기
        Assert.True(kit.Console.Map.IsConfirming);
        var reads = kit.Units.GraphReads;

        await kit.Console.HandleAsync(new UnitTopologyChangedMessage("UPDATED", 4), CancellationToken.None);
        kit.Delay.ElapseAll();
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);
        Assert.Equal(reads, kit.Units.GraphReads);
        Assert.True(kit.Console.IsExternallyChanged);

        kit.Console.Map.CancelConfirm();
        kit.Delay.ElapseAll();
        await kit.Console.ExternalChangeTask.WaitAsync(ConsoleKit.Timeout);

        Assert.Equal(reads + 1, kit.Units.GraphReads);
    }
    #endregion

    #region - 상위 이동 뒤 배치 정리 (FR-08 v1.3 · ISSUE-4) -
    [Fact]
    public async Task should_clear_moved_units_layout_once_when_moved_from_tree_and_row_exists()
    {
        var layout = new FakeUnitLayoutApi();
        layout.SimulateOtherWrite(6, 30, 0);
        var kit = await ConsoleKit.OpenAsync(layout);

        var moved = await kit.Console.MoveAsync(6, 7);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.True(moved);
        var clear = Assert.Single(layout.Writes);
        Assert.Equal(new[] { 6 }, clear.Change.Clear);
        Assert.Equal(layout.Writes[0].VersionBefore, clear.IfMatch);
        Assert.False(layout.Current.Deltas.ContainsKey(6));
    }

    [Fact]
    public async Task should_send_no_patch_when_moved_from_tree_and_no_layout_row()
    {
        var layout = new FakeUnitLayoutApi();
        var kit = await ConsoleKit.OpenAsync(layout);

        await kit.Console.MoveAsync(6, 7);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.Empty(layout.Writes);
    }

    [Fact]
    public async Task should_clean_up_only_once_when_move_comes_from_map()
    {
        var layout = new FakeUnitLayoutApi();
        layout.SimulateOtherWrite(6, 30, 0);
        var kit = await ConsoleKit.OpenAsync(layout);
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        Assert.True(kit.Console.Map.IsConfirming);

        await kit.Console.Map.ConfirmAsync().WaitAsync(ConsoleKit.Timeout);
        await kit.Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);

        Assert.Single(layout.Writes);
    }
    #endregion

    #region - 장비 소속 발행 (FR-49) -
    [Fact]
    public async Task should_publish_device_unit_changed_for_each_applied_device_only()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Devices.FailIds.Add(502);
        kit.Console.QueueAssign(6, kit.Console.DeviceRows.ToList());

        await kit.Console.ApplyAssignsAsync();

        var published = kit.Published.OfType<DeviceUnitChangedMessage>().ToList();
        Assert.Equal(new[] { new DeviceUnitChangedMessage(501, 6) }, published);
    }
    #endregion

    #region - 닫기 (IMPL-31 — CanCloseAsync 앞 취소만) -
    [Fact]
    public async Task should_cancel_map_overlay_without_server_call_when_closing()
    {
        var kit = await ConsoleKit.OpenAsync();
        kit.Console.Map.CompleteDrag(new UnitMapDropRequest(6, 0, 0, 7, false));
        Assert.True(kit.Console.Map.IsConfirming);

        var canClose = await kit.Console.CanCloseAsync();

        Assert.True(canClose);
        Assert.False(kit.Console.Map.IsConfirming);
        Assert.Empty(kit.Units.Patches);
    }
    #endregion
}

#region - 콘솔 시험 도구 -
/// <summary>부대 콘솔 + 관계도 시험 한 벌 — 편제 8(사단 1 · 연대 1 · 대대 2 · 중대 3 · 소초 1) · 가짜 편제 · 장비 · 배치 서버.</summary>
internal sealed class ConsoleKit
{
    /// <summary>시험 대기의 안전 상한 — 무한 대기 대신 실패로 끝낸다(시간 대기가 아니다: 정상이면 곧바로 끝난다).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public FakeConsoleUnitApi Units { get; }
    public FakeConsoleDeviceApi Devices { get; } = new();
    public FakeUnitLayoutApi Layout { get; }
    public ManualDelay Delay { get; } = new();
    public MutableClock Clock { get; } = new();
    public List<object> Published { get; } = new();
    public ConsolePrefsProbe Prefs { get; } = new();
    public UnitConsoleViewModel Console { get; }
    public bool Dragging { get; set; }

    private ConsoleKit(FakeUnitLayoutApi layout, string? lastRail, bool canEdit)
    {
        Units = new FakeConsoleUnitApi();
        Layout = layout;
        Devices.Items.Add(new UnitDeviceItem(501, 1, "센서-1", EnumDeviceCategory.Sensor, null));
        Devices.Items.Add(new UnitDeviceItem(502, 2, "센서-2", EnumDeviceCategory.Sensor, null));
        Devices.Items.Add(new UnitDeviceItem(503, 3, "카메라-1", EnumDeviceCategory.Camera, 6));
        if (lastRail is not null) Prefs.Entry.LastRailKey = lastRail;
        Console = new UnitConsoleViewModel(Units, Devices, log: null, myUnitCode: () => "c06",
                                           canEdit: () => canEdit, canDelete: () => true, canView: () => true, canPlaceDevices: () => true,
                                           events: new CapturingEventAggregator(Published),
                                           isDragging: () => Dragging, delay: Delay.Run,
                                           layoutApi: layout, prefs: Prefs.Entry, savePrefs: Prefs.Save, clock: Clock);
    }

    public static ConsoleKit Create(FakeUnitLayoutApi? layout = null, string? lastRail = null, bool canEdit = true)
        => new(layout ?? new FakeUnitLayoutApi(), lastRail, canEdit);

    public static async Task<ConsoleKit> OpenAsync(FakeUnitLayoutApi? layout = null, bool canEdit = true)
    {
        var kit = Create(layout, null, canEdit);
        await kit.ActivateAsync();
        return kit;
    }

    public async Task ActivateAsync()
    {
        await ((IActivate)Console).ActivateAsync();
        await Console.Map.WhenIdleAsync().WaitAsync(ConsoleKit.Timeout);
    }
}

/// <summary>편제 가짜 — 부모 · 인접 PATCH 를 실제로 반영한다(재조회가 결과를 본다).</summary>
internal sealed class FakeConsoleUnitApi : IUnitGraphApi
{
    private readonly Dictionary<int, UnitListDto> _nodes = new();
    private readonly HashSet<(int, int)> _adjacency = new();

    public FakeConsoleUnitApi()
    {
        AddNode(1, "d01", "Division", null);
        AddNode(2, "r01", "Regiment", 1);
        AddNode(3, "b01", "Battalion", 2);
        AddNode(7, "b02", "Battalion", 2);
        AddNode(4, "c04", "Company", 3);
        AddNode(5, "c05", "Company", 7);
        AddNode(6, "c06", "Company", 3);
        AddNode(8, "p061", "Outpost", 6);
        _adjacency.Add((4, 6));
        _adjacency.Add((3, 7));
    }

    public bool IsAvailable => true;
    public int GraphReads { get; private set; }
    public int DetailReads { get; private set; }
    public List<(int Id, UnitUpdateDto Dto)> Patches { get; } = new();

    /// <summary>다음 PATCH 를 이 오류 코드로 실패시킨다.</summary>
    public string? FailNextPatchWith { get; set; }

    /// <summary>상세 GET 응답을 붙잡는다(빠른 선택의 GET 폭주 시험).</summary>
    public bool HoldDetails { get; set; }
    private TaskCompletionSource _detailHold = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseDetails() { HoldDetails = false; _detailHold.TrySetResult(); }

    /// <summary>다른 곳에서 바뀐 인접(재조회에만 보인다).</summary>
    public void SetAdjacency(int a, int b, bool present)
    {
        var key = a < b ? (a, b) : (b, a);
        if (present) _adjacency.Add(key); else _adjacency.Remove(key);
    }

    public void AddNode(int id, string code, string echelon, int? parentId)
        => _nodes[id] = new UnitListDto { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId, IsEnable = true };

    public void Remove(int id) => _nodes.Remove(id);

    public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
    {
        GraphReads++;
        var nodes = _nodes.Values.OrderBy(n => n.Id).Select(n => new UnitListDto { Id = n.Id, Code = n.Code, Name = n.Name, EchelonRaw = n.EchelonRaw, ParentId = n.ParentId, IsEnable = n.IsEnable }).ToList();
        var graph = new UnitGraphDto
        {
            Nodes = nodes,
            Edges = new UnitGraphEdgesDto
            {
                Hierarchy = nodes.Where(n => n.ParentId is int p && _nodes.ContainsKey(p)).Select(n => new List<int> { n.ParentId!.Value, n.Id }).ToList(),
                Adjacency = _adjacency.Where(p => _nodes.ContainsKey(p.Item1) && _nodes.ContainsKey(p.Item2)).OrderBy(p => p).Select(p => new List<int> { p.Item1, p.Item2 }).ToList(),
            },
        };
        return Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(graph));
    }

    public async Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
    {
        DetailReads++;
        if (HoldDetails) await _detailHold.Task.ConfigureAwait(false);
        return await DetailCore(unitId).ConfigureAwait(false);
    }

    private Task<ApiResponse<UnitDetailDto>> DetailCore(int unitId)
    {
        if (!_nodes.TryGetValue(unitId, out var node)) return Task.FromResult(ApiResponse<UnitDetailDto>.CreateError(ApiErrorCodes.NotFound, "없음"));
        return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
        {
            Id = unitId, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId, IsEnable = node.IsEnable,
            AdjacentUnitIds = _adjacency.Where(p => p.Item1 == unitId || p.Item2 == unitId).Select(p => p.Item1 == unitId ? p.Item2 : p.Item1).OrderBy(x => x).ToList(),
        }));
    }

    public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
        => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 900 }));

    public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
    {
        Patches.Add((unitId, dto));
        if (FailNextPatchWith is { } code)
        {
            FailNextPatchWith = null;
            return Task.FromResult(ApiResponse<UnitDto>.CreateError(code, "거절"));
        }
        if (dto.IsParentIdSpecified && _nodes.TryGetValue(unitId, out var node)) node.ParentId = dto.ParentId;
        if (dto.AdjacentUnitIds is { } ids)
        {
            foreach (var pair in _adjacency.Where(p => p.Item1 == unitId || p.Item2 == unitId).ToList()) _adjacency.Remove(pair);
            foreach (var other in ids) SetAdjacency(unitId, other, true);
        }
        return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));
    }

    public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
    {
        _nodes.Remove(unitId);
        return Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));
    }
}

/// <summary>장비 가짜 — 전량 읽기 횟수 · 배치 결과(실패 id 주입).</summary>
internal sealed class FakeConsoleDeviceApi : IUnitDeviceApi
{
    public bool IsAvailable => true;
    public List<UnitDeviceItem> Items { get; } = new();
    public HashSet<int> FailIds { get; } = new();
    public int Loads { get; private set; }

    public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
    {
        Loads++;
        return Task.FromResult(new UnitDeviceLoadResult(Items.ToList(), Array.Empty<string>()));
    }

    public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
    {
        if (FailIds.Contains(device.Id)) return Task.FromResult(new UnitDeviceAssignResult(false, "거절"));
        var index = Items.FindIndex(i => i.Id == device.Id);
        if (index >= 0) Items[index] = Items[index] with { UnitId = unitId };
        return Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
}
#endregion
