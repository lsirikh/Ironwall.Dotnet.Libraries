using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-26 (FR-07 · FR-09 · FR-10 · FR-11 · FR-50~53 · NFR-01 · NFR-14 · NFR-15) — 배치 수명.
/// 시나리오: SIM-P001~P012 · P023~P028 · P036~P044 · SIM-Q001~Q048(412 병합) · SIM-N001~N082(알림 합침 · 연기) · SIM-F078 · F083 · F089 · SIM-L053.
/// 조정자 필수 항목 4 · 결정 "초기화 되돌리기는 그 사이 바뀌지 않았고 아직 있는 부대만 되살린다".
/// </summary>
public class UnitMapViewModelLayoutTests
{
    private static UnitMapDropRequest Drop(MapKit kit, string moving, double dx = 40, double dy = 0)
        => new(kit.Id(moving), dx, dy, null, false);

    private static async Task NoticeAsync(MapKit kit, long version)
        => await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(version), default);

    /// <summary>한 부대를 뺀 편제(다른 곳에서 삭제됨).</summary>
    private static UnitTreeModel Without(UnitMapFixture f, params int[] removed)
    {
        var gone = new HashSet<int>(removed);
        var nodes = f.Graph.Nodes.Where(n => !gone.Contains(n.Id) && !(n.ParentId is int p && gone.Contains(p))).ToList();
        return UnitMapTestData.Tree(UnitMapTestData.Graph(nodes, adjacency: f.Graph.Edges.AdjacencyPairs.Where(p => !gone.Contains(p.Low) && !gone.Contains(p.High)).ToList()));
    }

    #region - 열기 · 지원 판정 (FR-50 · FR-11) -
    [Fact]
    public async Task should_draw_auto_layout_before_layout_get_returns()
    {
        // NFR-01 · SIM-P041 — 첫 그림은 GET 을 기다리지 않는다
        var api = new FakeUnitLayoutApi();
        api.SimulateOtherWrite(UnitMapTestData.Standard200().IdOf("6중대"), 50, 0);
        var kit = MapKit.Create(api);
        kit.Gate.HoldReads = true;

        var open = kit.Vm.OpenAsync();

        Assert.Equal(200, kit.Vm.Scene.Positions.Count);
        Assert.Equal(UnitMapLayoutState.Loading, kit.Vm.LayoutState);
        Assert.Equal("배치를 불러오는 중 — 자동 배치로 보입니다", kit.Vm.LayoutStatusText);
        var auto = kit.Vm.Scene.Positions[kit.Id("6중대")];

        kit.Gate.ReleaseRead();
        await open;
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(1, api.ReadCount);
        Assert.Equal(UnitMapLayoutState.Shared, kit.Vm.LayoutState);
        Assert.Equal(auto + new Vector(50, 0), kit.Vm.Scene.Positions[kit.Id("6중대")]);   // 도착하면 Δ 를 입힌다
    }

    [Fact]
    public async Task should_describe_shared_layout_with_last_editor_when_supported()
    {
        var api = new FakeUnitLayoutApi();
        api.SimulateOtherWrite(UnitMapTestData.Standard200().IdOf("6중대"), 50, 0, by: "김○○");

        var kit = await MapKit.OpenAsync(api);

        Assert.StartsWith("배치: 모든 운영자 공유 · 마지막 변경 김○○", kit.Vm.LayoutStatusText);
        Assert.False(kit.Vm.CanRetryLayout);
    }

    [Fact]
    public async Task should_enter_session_only_without_retry_when_layout_unsupported()
    {
        var kit = await MapKit.OpenAsync(new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported });

        Assert.Equal(UnitMapLayoutState.SessionOnly, kit.Vm.LayoutState);
        Assert.StartsWith("이 서버는 배치 저장을 지원하지 않습니다", kit.Vm.LayoutStatusText);
        Assert.Equal(UnitMapDropKind.Position, kit.Vm.Classify(kit.Id("6중대"), null, false).Kind);
    }

    [Fact]
    public async Task should_block_writes_and_offer_retry_when_layout_read_fails()
    {
        // SIM-P023~P028 — 읽기 실패: 자동 배치 · 쓰기 금지 · [다시 시도]
        var api = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Failing };
        var kit = await MapKit.OpenAsync(api);

        Assert.Equal(UnitMapLayoutState.ReadFailed, kit.Vm.LayoutState);
        Assert.True(kit.Vm.CanRetryLayout);
        Assert.Equal(UnitMapText.LayoutReadFailedBlocked, kit.Vm.Classify(kit.Id("6중대"), null, false).Reason);

        api.Mode = FakeLayoutServerMode.Supported;
        await kit.Vm.RetryLayoutAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(2, api.ReadCount);
        Assert.Equal(UnitMapLayoutState.Shared, kit.Vm.LayoutState);
        Assert.False(kit.Vm.CanRetryLayout);
    }

    [Fact]
    public async Task should_draw_auto_layout_and_block_writes_when_layout_version_differs()
    {
        // SIM-L053 · P005
        var api = new FakeUnitLayoutApi(layoutVersion: 2);
        api.SimulateOtherWrite(UnitMapTestData.Standard200().IdOf("6중대"), 50, 0);
        var kit = await MapKit.OpenAsync(api);
        var auto = UnitMapLayout.Compute(kit.F.Tree).Positions;

        Assert.Equal(UnitMapLayoutState.VersionMismatch, kit.Vm.LayoutState);
        Assert.Equal(auto[kit.Id("6중대")], kit.Vm.Scene.Positions[kit.Id("6중대")]);
        // 서버 판(2)이 이 클라(1)보다 높다 — 클라이언트 갱신 필요(PRD v1.8 · 회신 2026-09-29 §2)
        Assert.Equal(UnitMapText.LayoutStatusClientOutdated, kit.Vm.LayoutStatusText);
        Assert.Equal(UnitMapText.LayoutVersionBlocked, kit.Vm.Classify(kit.Id("6중대"), null, false).Reason);
    }

    [Fact]
    public async Task should_say_view_only_when_shared_without_edit()
    {
        var kit = await MapKit.OpenAsync(canEdit: false);

        Assert.Equal("배치: 모든 운영자 공유 · 아직 아무도 옮기지 않았습니다 · 보기 전용", kit.Vm.LayoutStatusText);
    }
    #endregion

    #region - 412 병합 (FR-52 · NFR-15) -
    [Fact]
    public async Task should_resend_once_with_new_version_when_conflict_did_not_touch_my_units()
    {
        // SIM-Q 계열 — 다른 운영자가 다른 가지를 옮겼다 → 말없이 한 번 재전송
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        api.SimulateOtherWrite(kit.Id("30중대"), 10, 10);    // 내 알림 전(아직 모른다)

        kit.Vm.CompleteDrag(Drop(kit, "6중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(2, api.Writes.Count);
        Assert.IsType<UnitLayoutWrite.Conflict>(api.Writes[0].Result);
        Assert.True(api.Writes[1].Succeeded);
        Assert.Equal(new Vector(40, 0), api.Current.Deltas[kit.Id("6중대")]);
        Assert.Equal(new Vector(10, 10), api.Current.Deltas[kit.Id("30중대")]);
        Assert.False(kit.Vm.BarIsError);
    }

    [Theory]
    [InlineData("6중대")]     // 같은 부대
    [InlineData("2대대")]     // 조상
    public async Task should_draw_latest_and_not_resend_when_conflict_touched_my_unit_or_ancestor(string changed)
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        api.SimulateOtherWrite(kit.Id(changed), 77, 0);

        kit.Vm.CompleteDrag(Drop(kit, "6중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.Single(api.Writes);
        Assert.True(kit.Vm.BarIsError);
        Assert.Equal("다른 운영자가 방금 ‘6중대’ 배치를 바꿨습니다 — 최신 배치를 불러왔습니다.", kit.Vm.BarText);
        var auto = UnitMapLayout.Compute(kit.F.Tree).Positions;
        Assert.Equal(auto[kit.Id(changed)] + new Vector(77, 0), kit.Vm.Scene.Positions[kit.Id(changed)]);
        Assert.NotEqual(new Vector(40, 0), api.Current.DeltaOf(kit.Id("6중대")) ?? default);   // 말없이 덮지 않았다
    }

    [Fact]
    public async Task should_stop_after_one_resend_when_resend_also_conflicts()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        api.SimulateOtherWrite(kit.Id("30중대"), 10, 10);
        api.FailNextWrite(new UnitLayoutWrite.Conflict(null));
        api.FailNextWrite(new UnitLayoutWrite.Conflict(null));

        kit.Vm.CompleteDrag(Drop(kit, "6중대"));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(2, api.Writes.Count);
        Assert.True(kit.Vm.BarIsError);
    }
    #endregion

    #region - 알림 (FR-53) -
    [Fact]
    public async Task should_refetch_once_after_coalescing_when_notices_are_newer()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var reads = api.ReadCount;
        var v1 = api.SimulateOtherWrite(kit.Id("30중대"), 1, 0, by: "이○○");
        var v2 = api.SimulateOtherWrite(kit.Id("31중대"), 2, 0, by: "이○○");

        await NoticeAsync(kit, v1);
        await NoticeAsync(kit, v2);
        Assert.Equal(reads, api.ReadCount);          // 창이 닫히기 전 0
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads + 1, api.ReadCount);
        Assert.StartsWith("배치: 모든 운영자 공유 · 마지막 변경 이○○", kit.Vm.LayoutStatusText);
        Assert.True(kit.Vm.Scene.FactsOf(kit.Id("31중대")).IsMoved);
    }

    [Fact]
    public async Task should_skip_refetch_when_notice_is_my_own_echo_or_older()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.CompleteDrag(Drop(kit, "6중대"));
        await kit.Vm.WhenIdleAsync();
        var reads = api.ReadCount;

        await NoticeAsync(kit, api.Current.Version);      // 자기 메아리
        await NoticeAsync(kit, api.Current.Version - 1);  // 늦게 온 옛 알림
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads, api.ReadCount);
    }

    [Theory]
    [InlineData("drag")]
    [InlineData("move")]
    [InlineData("confirm")]
    public async Task should_defer_refetch_until_hands_are_free(string busy)
    {
        // SIM-N079~N082 — 내 손 아래 노드가 튀지 않게
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var reads = api.ReadCount;
        var eight = kit.Id("8중대");
        switch (busy)
        {
            case "drag": kit.Vm.BeginDrag(eight); break;
            case "move": kit.Vm.RequestSelect(eight); kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false); break;
            default: kit.Vm.CompleteDrag(new UnitMapDropRequest(eight, 0, 0, kit.Id("3대대"), false)); break;
        }

        await NoticeAsync(kit, api.SimulateOtherWrite(kit.Id("30중대"), 1, 0));
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(reads, api.ReadCount);

        switch (busy)
        {
            case "drag": kit.Vm.CancelDrag(eight); break;
            case "move": kit.Vm.HandleKey(UnitMapKeyCommand.Escape, false); break;
            default: kit.Vm.CancelConfirm(); break;
        }
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads + 1, api.ReadCount);
    }

    [Fact]
    public async Task should_ignore_notices_when_session_only()
    {
        var api = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported };
        var kit = await MapKit.OpenAsync(api);
        var reads = api.ReadCount;

        await NoticeAsync(kit, 99);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads, api.ReadCount);   // SIM-P044 — 세션 위치를 서버로 올리지 않는다
    }
    #endregion

    #region - 초기화 (FR-09) -
    [Fact]
    public async Task should_confirm_then_patch_clear_all_once_when_reset_layout()
    {
        var api = new FakeUnitLayoutApi();
        api.SimulateOtherWrite(UnitMapTestData.Standard200().IdOf("6중대"), 50, 0);
        var kit = await MapKit.OpenAsync(api);

        kit.Vm.RequestResetLayout();
        Assert.Equal(UnitMapConfirmKind.ResetLayout, kit.Vm.PendingConfirm!.Kind);
        Assert.Contains("모든 운영자의 배치가 자동 배치로 돌아갑니다.", kit.Vm.PendingConfirm.Text.Lines);
        Assert.Empty(api.Writes);

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(api.Writes);
        Assert.True(write.Change.ClearAll);
        Assert.Empty(api.Current.Deltas);
        Assert.True(kit.Vm.CanUndo);
        Assert.Equal("배치를 초기화했습니다 — 모든 운영자에게 보입니다.", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_clear_one_unit_without_confirm_when_node_reset()
    {
        var api = new FakeUnitLayoutApi();
        var six = UnitMapTestData.Standard200().IdOf("6중대");
        api.SimulateOtherWrite(six, 50, 0);
        var kit = await MapKit.OpenAsync(api);

        await kit.Vm.ResetNodeLayoutAsync(six);
        await kit.Vm.WhenIdleAsync();

        Assert.Null(kit.Vm.PendingConfirm);
        var write = Assert.Single(api.Writes);
        Assert.Equal(new[] { six }, write.Change.Clear);
        Assert.True(kit.Vm.CanUndo);

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(new Vector(50, 0), api.Current.Deltas[six]);
    }

    [Fact]
    public async Task should_restore_only_unchanged_and_existing_units_when_reset_undone()
    {
        // 필수 항목 4 · 결정 — 그 사이 남이 옮긴 부대 · 삭제된 부대는 건너뛰고(422 방지) 나머지만 되살린다
        var api = new FakeUnitLayoutApi();
        var f = UnitMapTestData.Standard200();
        var (a, b, c) = (f.IdOf("6중대"), f.IdOf("7중대"), f.IdOf("30중대"));
        api.SimulateOtherWrite(a, 10, 0);
        api.SimulateOtherWrite(b, 20, 0);
        api.SimulateOtherWrite(c, 30, 0);
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.RequestResetLayout();
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        await NoticeAsync(kit, api.SimulateOtherWrite(b, 99, 0));   // b 는 남이 다시 옮겼다
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();
        kit.Vm.SetData(Without(kit.F, c), kit.Devices);             // c 는 삭제됐다

        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();

        var undo = api.Writes.Last();
        Assert.Equal(new[] { a }, undo.Change.Set.Keys);
        Assert.False(undo.Change.ClearAll);
        Assert.Equal(new Vector(10, 0), api.Current.Deltas[a]);
        Assert.Equal(new Vector(99, 0), api.Current.Deltas[b]);
        Assert.Contains("2곳은 건너뛰었습니다", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_reset_in_memory_only_with_view_permission_when_session_only()
    {
        // ISSUE-7 · SIM-P043 — 세션 전용 초기화는 누구 화면도 바꾸지 않는다: units:view 로 된다 · 문구에 '모든 운영자' 없음
        var api = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported };
        var kit = await MapKit.OpenAsync(api, canEdit: false);
        kit.Vm.CompleteDrag(Drop(kit, "6중대"));
        await kit.Vm.WhenIdleAsync();

        kit.Vm.RequestResetLayout();
        Assert.DoesNotContain(kit.Vm.PendingConfirm!.Text.Lines, l => l.Contains("모든 운영자"));
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(0, kit.Gate.StartedWrites);
        Assert.False(kit.Vm.Scene.FactsOf(kit.Id("6중대")).IsMoved);
    }

    [Fact]
    public async Task should_refuse_reset_with_reason_when_shared_without_edit()
    {
        var kit = await MapKit.OpenAsync(canEdit: false);

        kit.Vm.RequestResetLayout();

        Assert.Null(kit.Vm.PendingConfirm);
        Assert.Equal(UnitMapText.NoPermission, kit.Vm.StatusText);
    }
    #endregion
}
