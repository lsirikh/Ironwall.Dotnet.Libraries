using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 — 부대 콘솔 뷰모델. 진짜 뷰모델을 가짜 창구 위에 세워 본다(서버 · 창 없음).
/// </summary>
/// <remarks>
/// 드래그 제스처 자체는 UIA 로 단언할 수 없다(.NET 8 WPF 에 드래그 패턴 타입이 없다) —
/// 회귀는 <b>판정(<see cref="UnitDropHandler.Verdict"/>)과 폴백 경로</b>로 잡는다(와이어프레임 L228-233).
/// </remarks>
[Collection("CaliburnIoC")]      // BindableCollection · Screen 이 Caliburn 의 정적 PlatformProvider 를 쓴다 — 같은 정적 상태를 만지는 클래스와 직렬화한다.
public class UnitConsoleViewModelTests
{
    #region - Fakes -
    private sealed class FakeUnitApi : IUnitGraphApi
    {
        public FakeUnitApi(bool available = true) => IsAvailable = available;

        public bool IsAvailable { get; set; }
        public UnitGraphDto Graph { get; set; } = new();
        public List<(int Id, UnitUpdateDto Dto)> Patches { get; } = new();
        public List<int> Deletes { get; } = new();
        public List<UnitCreateDto> Creates { get; } = new();
        public int GraphReads { get; private set; }
        public int DetailReads { get; private set; }
        public ApiError? PatchError { get; set; }
        public ApiError? DeleteError { get; set; }
        public List<int>? DetailAdjacency { get; set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            GraphReads++;
            return Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(Graph));
        }

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            DetailReads++;
            var node = Graph.Nodes.FirstOrDefault(n => n.Id == unitId);
            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = unitId,
                Code = node?.Code ?? "x",
                Name = node?.Name ?? "x",
                EchelonRaw = node?.EchelonRaw ?? "Company",
                ParentId = node?.ParentId,
                IsEnable = node?.IsEnable ?? true,
                AdjacentUnitIds = DetailAdjacency ?? new List<int>(),
            }));
        }

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
        {
            Creates.Add(dto);
            return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 900, Code = dto.Code, Name = dto.Name }));
        }

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
        {
            Patches.Add((unitId, dto));
            // 진짜 서버처럼 그래프에 반영한다 — 반영하지 않으면 되돌리기가 "이미 그 부대" 로 막혀 검증이 헛돈다.
            var node = Graph.Nodes.FirstOrDefault(n => n.Id == unitId);
            if (node is not null && PatchError is null)
            {
                if (dto.IsParentIdSpecified)
                {
                    node.ParentId = dto.ParentId;
                    Graph.Edges.Hierarchy.RemoveAll(e => e.Count == 2 && e[1] == unitId);
                    if (dto.ParentId is int parent) Graph.Edges.Hierarchy.Add(new List<int> { parent, unitId });
                }
                if (dto.IsEnable is bool enable) node.IsEnable = enable;
                if (dto.Name is not null) node.Name = dto.Name;
                // 인접도 진짜 서버처럼 갈아 끼운다 — 전삭제 후 재생성이다.
                if (dto.AdjacentUnitIds is { } adjacent)
                {
                    DetailAdjacency = adjacent.ToList();
                    Graph.Edges.Adjacency.RemoveAll(e => e.Count == 2 && (e[0] == unitId || e[1] == unitId));
                    foreach (var partner in adjacent)
                    {
                        var low = System.Math.Min(unitId, partner);
                        var high = System.Math.Max(unitId, partner);
                        Graph.Edges.Adjacency.Add(new List<int> { low, high });
                    }
                }
            }
            if (PatchError is not null)
            {
                var failed = ApiResponse<UnitDto>.CreateError(PatchError.Code, PatchError.Message);
                return Task.FromResult(failed);
            }
            return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));
        }

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
        {
            Deletes.Add(unitId);
            if (DeleteError is null) return Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));

            var response = ApiResponse<UnitDeleteResultDto>.CreateError(DeleteError.Code, "삭제할 수 없습니다");
            response.Error!.DetailsToken = DeleteError.DetailsToken;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeDeviceApi : IUnitDeviceApi
    {
        public bool IsAvailable { get; set; } = true;
        public List<UnitDeviceItem> Items { get; } = new();
        public List<(int DeviceId, int UnitId)> Assigns { get; } = new();
        public HashSet<int> FailFor { get; } = new();
        public int Loads { get; private set; }

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
        {
            Loads++;
            return Task.FromResult(new UnitDeviceLoadResult(Items.ToList(), Array.Empty<string>()));
        }

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
        {
            Assigns.Add((device.Id, unitId));
            return Task.FromResult(FailFor.Contains(device.Id)
                ? new UnitDeviceAssignResult(false, "서버가 거절했습니다")
                : new UnitDeviceAssignResult(true, "바꿨습니다"));
        }
    }
    #endregion

    #region - Fixture -
    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    /// <summary>사단1 → 연대2 → 대대3 → 중대5 · 중대6 · 대대8(1 밑).</summary>
    private static UnitGraphDto Sample() => new()
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "b01", "Battalion", 2),
            Node(5, "c05", "Company", 3),
            Node(6, "c06", "Company", 3),
            Node(8, "b08", "Battalion", 1),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 3 }, new() { 3, 5 }, new() { 3, 6 }, new() { 1, 8 } },
            Adjacency = new List<List<int>> { new() { 5, 6 } },
        },
    };

    private static async Task<(UnitConsoleViewModel Console, FakeUnitApi Units, FakeDeviceApi Devices)> OpenAsync(
        bool available = true, bool canEdit = true, bool canDelete = true, IEnumerable<UnitDeviceItem>? devices = null)
    {
        var units = new FakeUnitApi(available) { Graph = Sample() };
        var deviceApi = new FakeDeviceApi();
        if (devices is not null) deviceApi.Items.AddRange(devices);

        var console = new UnitConsoleViewModel(units, deviceApi, log: null, myUnitCode: () => "c06",
                                               canEdit: () => canEdit, canDelete: () => canDelete);
        await ((IActivate)console).ActivateAsync();
        return (console, units, deviceApi);
    }

    private static UnitNodeRowViewModel Row(UnitConsoleViewModel console, int id) => console.Rows.First(r => r.Id == id);
    #endregion

    #region - 계약 게이트 -
    [Fact]
    public async Task should_not_call_the_server_when_the_contract_is_below_8_0()
    {
        var (console, units, devices) = await OpenAsync(available: false);

        Assert.False(console.IsAvailable);
        Assert.Equal(0, units.GraphReads);
        Assert.Equal(0, devices.Loads);
        Assert.Equal(0, console.Rows.Count);
        Assert.Equal(UnitConsoleViewModel.NOT_SUPPORTED, console.StatusText);
        // 쓰기 입구도 전부 닫혀 있어야 한다 — 6.3 운영에서 새 요소가 하나도 보이지 않는 계약.
        Assert.False(console.CanAdd);
        Assert.False(console.CanReload);
        Assert.False(console.CanMoveSelected);
        Assert.False(console.CanAssignSelectedDevices);
    }

    [Fact]
    public async Task should_read_the_whole_graph_once_when_activated()
    {
        var (console, units, _) = await OpenAsync();

        Assert.Equal(1, units.GraphReads);
        Assert.Equal(6, console.Tree.Count);
        // 형제는 코드 오름차순 — 사단1 밑에서 b08 이 r01 보다 먼저다(서버에 순서 필드가 없어 코드가 유일한 기준).
        Assert.Equal(new[] { "d01", "b08", "r01", "b01", "c05", "c06" }, console.Rows.Select(r => r.Code));
        Assert.Equal(new[] { 0, 1, 1, 2, 3, 3 }, console.Rows.Select(r => r.Depth));
    }

    [Fact]
    public async Task should_mark_my_own_unit_when_the_nats_group_code_matches()
    {
        var (console, _, _) = await OpenAsync();

        Assert.True(Row(console, 6).IsMine);
        Assert.False(Row(console, 5).IsMine);
    }
    #endregion

    #region - 트리 이동 (호출 1회) -
    [Fact]
    public async Task should_send_exactly_one_patch_when_a_node_is_moved()
    {
        var (console, units, _) = await OpenAsync();

        var ok = await console.MoveAsync(6, 8);

        Assert.True(ok);
        Assert.Single(units.Patches.Where(p => p.Dto.IsParentIdSpecified));
        Assert.Equal(6, units.Patches[0].Id);
        Assert.Equal(8, units.Patches[0].Dto.ParentId);
    }

    [Fact]
    public async Task should_not_send_anything_when_the_move_is_not_allowed()
    {
        var (console, units, _) = await OpenAsync();

        var ok = await console.MoveAsync(6, 5);     // 같은 제대

        Assert.False(ok);
        Assert.Empty(units.Patches);
        Assert.Contains("더 높은 제대", console.StatusText);
    }

    [Fact]
    public async Task should_not_send_anything_when_the_operator_cannot_edit()
    {
        var (console, units, _) = await OpenAsync(canEdit: false);

        var ok = await console.MoveAsync(6, 8);

        Assert.False(ok);
        Assert.Empty(units.Patches);
        Assert.Contains("권한", console.StatusText);
    }

    [Fact]
    public async Task should_refetch_the_graph_when_a_move_fails()
    {
        var (console, units, _) = await OpenAsync();
        units.PatchError = new ApiError { Code = ApiErrorCodes.ValidationError, Message = "제대가 맞지 않습니다" };
        var before = units.GraphReads;

        var ok = await console.MoveAsync(6, 8);

        Assert.False(ok);
        Assert.Equal(before + 1, units.GraphReads);         // 실패 복구 = 재조회
        // 서버 원문은 화면에 붙이지 않는다(U-18 공통 규칙) — 무엇이 안 됐는지와 할 일만 남는다.
        Assert.DoesNotContain("제대가 맞지 않습니다", console.StatusText);
        Assert.Contains("옮기지 못했습니다", console.StatusText);
        Assert.False(console.CanUndoMove);                  // 실패한 이동은 되돌릴 것이 없다
    }

    [Fact]
    public async Task should_undo_the_last_move_with_a_reverse_patch()
    {
        var (console, units, _) = await OpenAsync();
        await console.MoveAsync(6, 8);
        Assert.True(console.CanUndoMove);

        await console.UndoMoveAsync();

        Assert.Equal(2, units.Patches.Count);
        Assert.Equal(3, units.Patches[1].Dto.ParentId);     // 원래 부모(대대 3)로
        Assert.False(console.CanUndoMove);                  // 되돌리기는 마지막 1회뿐이다
    }

    [Fact]
    public async Task should_send_an_explicit_null_parent_when_moving_to_root()
    {
        var (console, units, _) = await OpenAsync();

        await console.MoveAsync(6, null);

        Assert.True(units.Patches[0].Dto.IsParentIdSpecified);
        Assert.Null(units.Patches[0].Dto.ParentId);
    }

    [Fact]
    public async Task should_move_one_level_up_when_the_keyboard_fallback_is_used()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));      // 중대6, 부모는 대대3, 할아버지는 연대2

        await console.MoveSelectedUpAsync();

        Assert.Equal(2, units.Patches[0].Dto.ParentId);
    }

    [Fact]
    public async Task should_say_so_when_the_keyboard_fallback_has_nowhere_to_go()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 1));      // 최상위

        await console.MoveSelectedUpAsync();

        Assert.Empty(units.Patches);
        Assert.Contains("이미 최상위", console.StatusText);
    }
    #endregion

    #region - 드롭 판정 (드래그의 회귀 단언점) -
    /// <summary>끌린 항목만 — <c>DragPayload</c> 는 <c>ItemsControl</c> 을 요구해 STA 스레드가 있어야 만들어진다.</summary>
    private static object[] PayloadOf(params object[] items) => items;

    [Fact]
    public async Task should_accept_a_node_dropped_on_a_higher_echelon()
    {
        var (console, _, _) = await OpenAsync();

        Assert.True(console.Drop.Verdict(PayloadOf(Row(console, 6)), new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 8), -1)).IsAllowed);
        Assert.True(console.Drop.Verdict(PayloadOf(Row(console, 6)), new DropTarget(UnitDropRules.ZONE_ROOT, null, -1)).IsAllowed);
    }

    [Fact]
    public async Task should_refuse_a_node_dropped_on_the_same_echelon()
    {
        var (console, _, _) = await OpenAsync();

        Assert.False(console.Drop.Verdict(PayloadOf(Row(console, 6)), new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 5), -1)).IsAllowed);
    }

    [Fact]
    public async Task should_refuse_every_drop_when_the_operator_cannot_edit()
    {
        var (console, _, _) = await OpenAsync(canEdit: false);

        var verdict = console.Drop.Verdict(PayloadOf(Row(console, 6)), new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 8), -1));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(UnitConsoleViewModel.NO_EDIT_PERMISSION, verdict.Reason);
    }

    [Fact]
    public async Task should_refuse_an_unknown_zone()
    {
        var (console, _, _) = await OpenAsync();

        Assert.False(console.Drop.Verdict(PayloadOf(Row(console, 6)), new DropTarget("somewhere-else", null, -1)).IsAllowed);
    }
    #endregion

    #region - 인접 (호출 1회 · 전체 집합) -
    [Fact]
    public async Task should_refetch_before_replacing_the_adjacency_set()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        units.DetailAdjacency = new List<int> { 5, 9 };     // 그 사이 다른 세션이 9 를 붙였다
        var reads = units.DetailReads;

        await console.ChangeAdjacencyAsync(add: 5, remove: null);   // 이미 인접이라 막힌다

        Assert.Equal(reads, units.DetailReads);
        Assert.Contains("이미 인접", console.StatusText);
    }

    [Fact]
    public async Task should_send_the_server_set_plus_the_new_one_when_adding_an_adjacency()
    {
        var (console, units, _) = await OpenAsync();
        units.Graph.Edges.Adjacency.Clear();
        await console.ReloadAsync();
        await console.SelectRowAsync(Row(console, 6));
        units.DetailAdjacency = new List<int> { 5 };        // 서버에는 이미 5 가 있다
        units.Patches.Clear();

        await console.ChangeAdjacencyAsync(add: 5 == 5 ? 5 : 0, remove: null);

        // 5 는 트리에서도 후보라 허용된다 — 보낸 집합은 서버의 현재 값 + 새것이다.
        var sent = units.Patches.Single().Dto.AdjacentUnitIds;
        Assert.Equal(new[] { 5 }, sent);
        Assert.Contains("인접 부대로 이었습니다", console.StatusText);
    }

    [Fact]
    public async Task should_drop_only_the_removed_one_when_cutting_an_adjacency()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        units.DetailAdjacency = new List<int> { 5, 8 };
        units.Patches.Clear();

        await console.ChangeAdjacencyAsync(add: null, remove: 5);

        Assert.Equal(new[] { 8 }, units.Patches.Single().Dto.AdjacentUnitIds);
    }
    #endregion

    #region - 삭제 · 운용 중지 -
    [Fact]
    public async Task should_surface_the_counts_when_delete_is_blocked_by_409()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        units.DeleteError = new ApiError
        {
            Code = ApiErrorCodes.Conflict,
            DetailsToken = JObject.Parse("""{"counts":{"devices":18,"device_groups":2,"events":431}}"""),
        };

        await console.DeleteAsync();

        Assert.True(console.IsDeleteBlocked);
        Assert.Equal(new[] { "장비", "장비 그룹", "이벤트 이력" }, console.DeleteBlock!.Items.Select(i => i.Label));
        Assert.Contains("운용 중지", console.StatusText);
    }

    [Fact]
    public async Task should_send_is_enable_false_when_disabling_instead_of_deleting()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        units.Patches.Clear();

        await console.DisableAsync();

        Assert.False(units.Patches.Single().Dto.IsEnable);
        Assert.Empty(units.Deletes);
    }

    [Fact]
    public async Task should_not_offer_delete_when_the_operator_lacks_the_permission()
    {
        var (console, _, _) = await OpenAsync(canDelete: false);
        await console.SelectRowAsync(Row(console, 6));

        Assert.False(console.CanDeleteUnit);
    }
    #endregion

    #region - 미배치 장비 → 부대 (호출 N회 · Draft) -
    private static UnitDeviceItem Device(int id, int? unitId) => new(id, id, $"장비{id}", EnumDeviceCategory.Camera, unitId);

    [Fact]
    public async Task should_list_only_devices_without_a_unit_in_the_tree()
    {
        var (console, _, _) = await OpenAsync(devices: new[] { Device(1, null), Device(2, 6), Device(3, 777) });

        await console.ReloadAsync();
        console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_DEVICES);

        Assert.Equal(new[] { 1, 3 }, console.DeviceRows.Select(r => r.Id));      // 777 은 편제에 없는 부대다
    }

    [Fact]
    public async Task should_queue_a_draft_without_calling_the_server_when_devices_are_dropped()
    {
        var (console, _, devices) = await OpenAsync(devices: new[] { Device(1, null), Device(2, null) });
        await console.ReloadAsync();

        console.QueueAssign(6, console.DeviceRows.ToList());

        Assert.Equal(2, console.Tray.Count);
        Assert.Empty(devices.Assigns);                                   // 드롭은 서버를 부르지 않는다
        Assert.All(console.DeviceRows, r => Assert.True(r.IsPending));
    }

    [Fact]
    public async Task should_send_one_call_per_device_when_the_draft_is_applied()
    {
        var (console, _, devices) = await OpenAsync(devices: new[] { Device(1, null), Device(2, null) });
        await console.ReloadAsync();
        console.QueueAssign(6, console.DeviceRows.ToList());

        await console.ApplyAssignsAsync();

        Assert.Equal(new[] { (1, 6), (2, 6) }, devices.Assigns);
        Assert.Equal(0, console.Tray.Count);
    }

    [Fact]
    public async Task should_stop_at_the_first_failure_when_applying_the_draft()
    {
        var (console, _, devices) = await OpenAsync(devices: new[] { Device(1, null), Device(2, null), Device(3, null) });
        await console.ReloadAsync();
        devices.FailFor.Add(1);
        console.QueueAssign(6, console.DeviceRows.ToList());

        await console.ApplyAssignsAsync();

        Assert.Single(devices.Assigns);                                  // 첫 실패에서 멈춘다 — 2·3 은 보내지 않았다
        Assert.Contains("처음 실패한 곳에서 멈췄습니다", console.StatusText);

        // ★ 보내지 않은 것은 트레이에 남는다. 커널은 Skipped 를 목록에서 지우므로,
        //   "남겨 뒀다" 고 알리려면 멈춘 항목을 Failed 로 돌려주어야 한다.
        Assert.Equal(3, console.Tray.Count);
        Assert.Contains("남아 있습니다", console.StatusText);
    }

    [Fact]
    public async Task should_retry_only_what_failed_when_applied_again()
    {
        var (console, _, devices) = await OpenAsync(devices: new[] { Device(1, null), Device(2, null) });
        await console.ReloadAsync();
        devices.FailFor.Add(1);
        console.QueueAssign(6, console.DeviceRows.ToList());
        await console.ApplyAssignsAsync();
        Assert.Equal(2, console.Tray.Count);

        devices.FailFor.Clear();
        devices.Assigns.Clear();
        await console.ApplyAssignsAsync();

        Assert.Equal(new[] { (1, 6), (2, 6) }, devices.Assigns);
        Assert.Equal(0, console.Tray.Count);
    }

    [Fact]
    public async Task should_cap_the_number_of_calls_one_apply_can_make()
    {
        var many = Enumerable.Range(1, UnitConsoleViewModel.MAX_ASSIGN_PER_APPLY + 5).Select(i => Device(i, null)).ToList();
        var (console, _, _) = await OpenAsync(devices: many);
        await console.ReloadAsync();

        console.QueueAssign(6, console.DeviceRows.ToList());

        Assert.Equal(UnitConsoleViewModel.MAX_ASSIGN_PER_APPLY, console.Tray.Count);
        Assert.Contains("뺐습니다", console.StatusText);
    }

    [Fact]
    public async Task should_throw_nothing_away_on_the_server_when_the_draft_is_reverted()
    {
        var (console, _, devices) = await OpenAsync(devices: new[] { Device(1, null) });
        await console.ReloadAsync();
        console.QueueAssign(6, console.DeviceRows.ToList());

        console.RevertAssigns();

        Assert.Equal(0, console.Tray.Count);
        Assert.Empty(devices.Assigns);
        Assert.All(console.DeviceRows, r => Assert.False(r.IsPending));
    }

    [Fact]
    public async Task should_use_the_selected_unit_when_the_button_fallback_assigns_devices()
    {
        var (console, _, _) = await OpenAsync(devices: new[] { Device(1, null) });
        await console.ReloadAsync();
        await console.SelectRowAsync(Row(console, 6));
        console.SetSelectedDevices(console.DeviceRows.ToList());

        console.QueueAssignSelected();

        Assert.Equal(1, console.Tray.Count);
    }
    #endregion

    #region - 검토 회귀 (쓰기 뒤의 화면 상태) -
    [Fact]
    public async Task should_reload_the_detail_after_an_adjacency_write()
    {
        // 재조회가 같은 부대의 <b>새 행 인스턴스</b>를 만들면 참조 비교 조기 반환에 걸려
        // 상세가 영영 갱신되지 않았다 — 인접 칩이 그대로 남고 Original 이 저장 전 DTO 를 붙들었다.
        var (console, _, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        Assert.Equal(new[] { 5 }, console.Form.AdjacentIds);

        await console.ChangeAdjacencyAsync(add: null, remove: 5);

        Assert.Empty(console.Form.AdjacentIds);                  // 칩이 실제로 사라진다
        Assert.Empty(console.Tree.Find(6)!.AdjacentIds);
    }

    [Fact]
    public async Task should_refresh_even_when_the_form_has_unapplied_changes()
    {
        // 관문은 사용자가 손댄 칸을 지키는 것이지, 서버가 이미 바꾼 사실을 화면에서 막으라는 뜻이 아니다.
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        console.Form.Description = "손댄 설명";
        Assert.True(console.Detail.IsDirty);
        var before = units.GraphReads;

        await console.MoveAsync(6, 8);

        Assert.Equal(before + 1, units.GraphReads);              // 서버가 바뀌었으면 화면도 따라간다
        Assert.Equal(8, console.Tree.Find(6)!.ParentId);
    }

    [Fact]
    public async Task should_keep_collapsed_nodes_collapsed_across_a_refresh()
    {
        var (console, _, _) = await OpenAsync();
        console.ToggleExpand(Row(console, 3));
        Assert.DoesNotContain(console.Rows, r => r.Id is 5 or 6);

        await console.ReloadAsync();

        Assert.DoesNotContain(console.Rows, r => r.Id is 5 or 6);
        Assert.False(console.Rows.First(r => r.Id == 3).IsExpanded);
    }

    [Fact]
    public async Task should_keep_the_undo_token_when_the_reverse_write_fails()
    {
        var (console, units, _) = await OpenAsync();
        await console.MoveAsync(6, 8);
        Assert.True(console.CanUndoMove);

        units.PatchError = new ApiError { Code = ApiErrorCodes.ValidationError, Message = "거절" };
        await console.UndoMoveAsync();

        Assert.True(console.CanUndoMove);                        // 되돌릴 방법이 사라지면 안 된다
    }

    [Fact]
    public async Task should_flatten_the_list_when_a_filter_is_active()
    {
        // 필터가 부모를 걸러내면 자식이 원래 깊이로 남아 허공에 들여쓰기된다.
        var (console, _, _) = await OpenAsync();

        console.SelectEchelon(console.EchelonFilters.First(f => f.Echelon == EnumUnitEchelon.Company));

        Assert.True(console.IsFiltered);
        Assert.All(console.Rows, r => Assert.True(r.IsFlat));
        Assert.All(console.Rows, r => Assert.Equal(0d, r.Indent.Left));

        console.SelectEchelon(console.EchelonFilters.First(f => f.Echelon is null));

        Assert.False(console.IsFiltered);
        Assert.Equal(3 * UnitNodeRowViewModel.INDENT_PER_DEPTH, Row(console, 5).Indent.Left);
    }

    [Fact]
    public async Task should_say_why_when_a_drop_is_blocked()
    {
        var (console, _, _) = await OpenAsync();

        console.Drop.Drop(new object[] { Row(console, 6) },
                          new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 5), -1));

        Assert.Contains("더 높은 제대", console.StatusText);         // 조용히 끝나지 않는다
    }

    [Fact]
    public async Task should_offer_a_button_path_for_assigning_devices_without_the_tree()
    {
        // 트리에서 아무것도 고르지 않아도 콤보로 대상 부대를 정할 수 있어야 한다(드래그의 폴백).
        var (console, _, _) = await OpenAsync(devices: new[] { Device(1, null) });
        await console.ReloadAsync();
        console.SetSelectedDevices(console.DeviceRows.ToList());

        Assert.Null(console.SelectedRow);
        Assert.False(console.CanAssignSelectedDevices);

        console.AssignTargetUnitId = 6;

        Assert.True(console.CanAssignSelectedDevices);
        Assert.Contains("6", console.AssignTargetText);

        console.QueueAssignSelected();
        Assert.Equal(1, console.Tray.Count);
    }

    [Fact]
    public async Task should_list_every_unit_as_an_assign_target()
    {
        var (console, _, _) = await OpenAsync();

        Assert.Equal(console.Tree.Ordered.Select(n => n.Id).ToList(), console.AssignTargets.Select(o => o.Id!.Value).ToList());
    }
    #endregion

    #region - 상세 · 관문 -
    [Fact]
    public async Task should_block_the_rail_switch_when_the_detail_has_unapplied_changes()
    {
        var (console, _, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        console.Form.Name = "6중대 (개편)";
        Assert.True(console.Detail.IsDirty);

        console.SelectedRail = console.RailEntries.First(r => r.Key == UnitConsoleViewModel.RAIL_DEVICES);

        Assert.Equal(UnitConsoleViewModel.RAIL_TREE, console.SelectedRail.Key);
        Assert.Equal(ConsoleDetailStateMachine.BlockedNotice, console.Detail.FooterText);
    }

    [Fact]
    public async Task should_lock_the_code_field_when_an_existing_unit_is_selected()
    {
        var (console, _, _) = await OpenAsync();

        await console.SelectRowAsync(Row(console, 6));

        Assert.True(console.Form.IsCodeLocked);
        Assert.False(console.Form.IsCreating);
        Assert.Equal("c06", console.Form.Code);
    }

    [Fact]
    public async Task should_open_the_code_field_only_while_creating()
    {
        var (console, _, _) = await OpenAsync();

        console.BeginCreate();

        Assert.True(console.Form.IsCreating);
        Assert.False(console.Form.IsCodeLocked);
        Assert.Contains("바꿀 수 없습니다", console.Detail.CreateBanner);
    }

    [Fact]
    public async Task should_not_send_a_create_when_the_code_is_reserved()
    {
        var (console, units, _) = await OpenAsync();
        console.BeginCreate();
        console.Form.Code = "global";
        console.Form.Name = "전역";

        await console.ApplyAsync();

        Assert.Empty(units.Creates);
        Assert.Contains("전역 예약", console.Form.ErrorText);
    }

    [Fact]
    public async Task should_send_a_create_when_the_form_is_valid()
    {
        var (console, units, _) = await OpenAsync();
        console.BeginCreate();
        console.Form.Echelon = EnumUnitEchelon.Company;
        console.Form.ParentId = 3;
        console.Form.Code = "c07";
        console.Form.Name = "7중대";

        await console.ApplyAsync();

        var sent = Assert.Single(units.Creates);
        Assert.Equal("c07", sent.Code);
        Assert.Equal(EnumUnitEchelon.Company, sent.Echelon);
        Assert.Equal(3, sent.ParentId);
    }

    [Fact]
    public async Task should_warn_before_saving_when_a_new_echelon_conflicts_with_children()
    {
        var (console, _, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 3));      // 대대3 밑에 중대 둘

        console.Form.Echelon = EnumUnitEchelon.Outpost;     // 소초 밑에 중대는 올 수 없다
        console.Form.RefreshChildEchelonWarning(console.Tree);

        Assert.True(console.Form.HasChildEchelonWarning);
        Assert.Contains("하위 부대 2개", console.Form.ChildEchelonWarning);
    }

    [Fact]
    public async Task should_offer_only_higher_echelons_that_are_not_its_own_descendants_as_parents()
    {
        var (console, _, _) = await OpenAsync();

        await console.SelectRowAsync(Row(console, 3));      // 대대3

        var ids = console.Form.ParentOptions.Select(o => o.Id).ToList();
        Assert.Contains(UnitDetailFormViewModel.ROOT_OPTION, ids);   // 최상위(루트)
        Assert.Contains(1, ids);                            // 사단
        Assert.Contains(2, ids);                            // 연대
        Assert.DoesNotContain(3, ids);                      // 자기 자신
        Assert.DoesNotContain(5, ids);                      // 자손
        Assert.DoesNotContain(8, ids);                      // 같은 제대
    }

    [Fact]
    public async Task should_not_call_the_server_when_nothing_changed_in_the_form()
    {
        var (console, units, _) = await OpenAsync();
        await console.SelectRowAsync(Row(console, 6));
        console.Form.Name = "다른 이름";
        console.Form.Name = "c06";                          // 원래대로 되돌림
        units.Patches.Clear();

        await console.ApplyAsync();

        Assert.Empty(units.Patches);
    }
    #endregion

    #region - 투영 -
    [Fact]
    public async Task should_keep_the_selected_row_when_the_list_is_reprojected()
    {
        var (console, _, _) = await OpenAsync();
        var row = Row(console, 6);
        await console.SelectRowAsync(row);

        console.SearchText = "c0";
        console.SearchText = string.Empty;

        Assert.Same(row, console.Rows.First(r => r.Id == 6));   // 같은 인스턴스가 살아 있다 = 선택이 죽지 않는다
        Assert.Same(row, console.SelectedRow);
    }

    [Fact]
    public async Task should_filter_by_echelon_chip_when_one_is_chosen()
    {
        var (console, _, _) = await OpenAsync();

        console.SelectEchelon(console.EchelonFilters.First(f => f.Echelon == EnumUnitEchelon.Company));

        Assert.Equal(new[] { 5, 6 }, console.Rows.Select(r => r.Id));
        Assert.True(console.EchelonFilters.Single(f => f.IsSelected).Echelon == EnumUnitEchelon.Company);
    }

    [Fact]
    public async Task should_search_by_name_or_code()
    {
        var (console, _, _) = await OpenAsync();

        console.SearchText = "b0";

        Assert.Equal(new[] { "b08", "b01" }, console.Rows.Select(r => r.Code));
    }

    [Fact]
    public async Task should_hide_children_when_a_node_is_collapsed()
    {
        var (console, _, _) = await OpenAsync();

        console.ToggleExpand(Row(console, 3));

        Assert.DoesNotContain(console.Rows, r => r.Id is 5 or 6);
        Assert.Contains(console.Rows, r => r.Id == 3);
    }

    [Fact]
    public async Task should_count_devices_per_unit_on_the_rows()
    {
        var (console, _, _) = await OpenAsync(devices: new[] { Device(1, 6), Device(2, 6), Device(3, 5) });

        await console.ReloadAsync();

        Assert.Equal(2, Row(console, 6).DeviceCount);
        Assert.Equal(1, Row(console, 5).DeviceCount);
        Assert.Equal(0, Row(console, 1).DeviceCount);
    }
    #endregion

    #region - 권한 기본값 — 서버와 같은 모듈(units:view · units:edit · units:delete / 배치는 devices:edit) -
    // 서버(app/security/permission_map.py): GET /api/units* = units:view · POST/PATCH/PUT = units:edit ·
    // DELETE = units:delete. 장비를 부대에 두는 것은 PATCH /api/devices/… 라 devices:edit 다.
    // 이 절은 게이트를 주입하지 않고 **기본값**(호스트 launcher 가 쓰는 그대로)을 진짜 PermissionService 로 검증한다.

    private sealed class PermissionScope : IDisposable
    {
        private readonly Func<Type, string, object> _getInstance = IoC.GetInstance;
        private readonly Func<Type, IEnumerable<object>> _getAllInstances = IoC.GetAllInstances;
        private readonly Action<object> _buildUp = IoC.BuildUp;

        public PermissionScope(string modulesJson)
        {
            var permission = new Ironwall.Dotnet.Libraries.Accounts.Api.Services.PermissionService();
            permission.Apply(new Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.AuthUserDto
            {
                Role = "OPERATOR",
                Permissions = JObject.Parse(modulesJson),
            });

            IoC.GetInstance = (type, key) =>
                type == typeof(Ironwall.Dotnet.Libraries.Accounts.Api.Services.IPermissionService) ? permission
                : type == typeof(IEventAggregator) ? new EventAggregator()
                : null!;
            IoC.GetAllInstances = type => Enumerable.Empty<object>();
            IoC.BuildUp = obj => { };
        }

        public void Dispose()
        {
            IoC.GetInstance = _getInstance;
            IoC.GetAllInstances = _getAllInstances;
            IoC.BuildUp = _buildUp;
        }
    }

    private static string Modules(string devices, string units)
        => $@"{{""modules"":{{""devices"":{{{devices}}},""units"":{{{units}}}}}}}";

    private const string VIEW = @"""view"":true";
    private const string VIEW_EDIT = @"""view"":true,""edit"":true";
    private const string ALL = @"""view"":true,""edit"":true,""delete"":true";

    /// <summary>게이트를 넘기지 않는다 — 기본값이 무엇을 보는지가 시험 대상이다.</summary>
    private static async Task<(UnitConsoleViewModel Console, FakeUnitApi Units, FakeDeviceApi Devices)> OpenWithDefaultGatesAsync(
        IEnumerable<UnitDeviceItem>? devices = null)
    {
        var units = new FakeUnitApi() { Graph = Sample() };
        var deviceApi = new FakeDeviceApi();
        if (devices is not null) deviceApi.Items.AddRange(devices);

        var console = new UnitConsoleViewModel(units, deviceApi, log: null, myUnitCode: () => "c06");
        await ((IActivate)console).ActivateAsync();
        return (console, units, deviceApi);
    }

    [Fact]
    public async Task should_block_unit_writes_when_the_account_holds_devices_edit_but_not_units_edit()
    {
        using var scope = new PermissionScope(Modules(devices: ALL, units: VIEW));
        var (console, units, _) = await OpenWithDefaultGatesAsync();

        await console.SelectByIdAsync(6);
        var moved = await console.MoveAsync(6, 8);

        Assert.False(console.CanEditUnits);
        Assert.False(console.CanAdd);
        Assert.False(console.CanDeleteUnit);
        Assert.False(moved);
        Assert.Empty(units.Patches);                           // 서버가 403 을 낼 요청을 보내지 않는다
    }

    [Fact]
    public async Task should_allow_unit_writes_when_the_account_holds_units_edit_without_devices_edit()
    {
        using var scope = new PermissionScope(Modules(devices: VIEW, units: ALL));
        var (console, units, _) = await OpenWithDefaultGatesAsync();

        await console.SelectByIdAsync(6);
        Assert.True(console.CanEditUnits);
        Assert.True(console.CanAdd);
        Assert.True(console.CanDeleteUnit);

        var moved = await console.MoveAsync(6, 8);

        Assert.True(moved);
        Assert.Single(units.Patches);
    }

    [Fact]
    public async Task should_not_read_the_graph_when_the_account_lacks_units_view()
    {
        using var scope = new PermissionScope(Modules(devices: ALL, units: ""));
        var (console, units, devices) = await OpenWithDefaultGatesAsync();

        Assert.Equal(0, units.GraphReads);                     // GET /api/units/graph 는 units:view — 403 왕복을 만들지 않는다
        Assert.Equal(0, devices.Loads);
        Assert.False(console.CanReload);
        Assert.Equal("부대 편제를 볼 권한이 없습니다.", console.StatusText);
    }

    [Fact]
    public async Task should_keep_device_placement_on_devices_edit_when_the_account_lacks_units_edit()
    {
        using var scope = new PermissionScope(Modules(devices: VIEW_EDIT, units: VIEW));
        var (console, _, _) = await OpenWithDefaultGatesAsync(devices: new[] { Device(1, null) });
        await console.ReloadAsync();
        console.SetSelectedDevices(console.DeviceRows.ToList());
        console.AssignTargetUnitId = 6;

        var verdict = console.Drop.Verdict(PayloadOf(console.DeviceRows[0]), new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 6), -1));

        Assert.False(console.CanEditUnits);
        Assert.True(console.CanAssignSelectedDevices);          // 배치는 장비 쓰기다 — units:edit 가 없어도 된다
        Assert.True(verdict.IsAllowed, verdict.Reason);
    }

    [Fact]
    public async Task should_block_device_placement_when_the_account_lacks_devices_edit()
    {
        using var scope = new PermissionScope(Modules(devices: VIEW, units: ALL));
        var (console, _, _) = await OpenWithDefaultGatesAsync(devices: new[] { Device(1, null) });
        await console.ReloadAsync();
        console.SetSelectedDevices(console.DeviceRows.ToList());
        console.AssignTargetUnitId = 6;

        var verdict = console.Drop.Verdict(PayloadOf(console.DeviceRows[0]), new DropTarget(UnitDropRules.ZONE_PARENT, Row(console, 6), -1));

        Assert.True(console.CanEditUnits);
        Assert.False(console.CanAssignSelectedDevices);
        Assert.False(verdict.IsAllowed);
        Assert.Equal(UnitDropHandler.PlaceDeniedReason, verdict.Reason);
    }
    #endregion
}
