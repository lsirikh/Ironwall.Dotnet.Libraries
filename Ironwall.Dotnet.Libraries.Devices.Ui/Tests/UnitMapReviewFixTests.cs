using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 부대 관계도 적대 검토 REVIEW-01 — 관계도 뷰모델 몫의 결함을 먼저 빨간 시험으로 잠근다.
/// HIGH-1(최상위 부대 되돌리기) · MEDIUM-2(412 뒤 대기 쓰기) · MEDIUM-3(정리의 대기열 우회) · MEDIUM-4(콘솔 바쁨) ·
/// MEDIUM-7(다시 읽기 실패) · L-1(연기 알림) · L-2 · L-3(막대 문구) · L-6(예외 원문) · L-7(닫기).
/// </summary>
public class UnitMapReviewFixTests
{
    private static UnitMapDropRequest Drop(MapKit kit, string moving, string? hover, double dx = 40, double dy = 0)
        => new(kit.Id(moving), dx, dy, hover is null ? null : kit.Id(hover), false);

    /// <summary>최상위 중대 둘이 있는 편제 — 콘솔처럼 성공한 이동 · 재조회 뒤 새 편제를 관계도에 넘긴다.</summary>
    private static async Task<MapKit> OpenWithRootsAsync()
    {
        var kit = await MapKit.OpenAsync(fixture: UnitMapTestData.Standard200WithRoots());
        kit.Commands.OnMoveSucceeded = () => kit.Vm.SetData(kit.Commands.CurrentTree(), kit.Devices);
        kit.Commands.OnReload = () => kit.Vm.SetData(kit.Commands.CurrentTree(), kit.Devices);
        return kit;
    }

    private static async Task ConfirmMoveAsync(MapKit kit, string moving, string target)
    {
        kit.Vm.CompleteDrag(Drop(kit, moving, target));
        Assert.Equal(UnitMapConfirmKind.Reparent, kit.Vm.PendingConfirm?.Kind);
        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();
    }

    private static async Task UndoOnceAsync(MapKit kit)
    {
        await kit.Vm.UndoAsync();
        await kit.Vm.WhenIdleAsync();
    }

    #region - HIGH-1 — 최상위 부대의 상위 되돌리기 -
    [Fact]
    public async Task should_return_root_unit_to_root_and_leave_other_unit_when_root_move_is_undone_after_another_move()
    {
        // 시나리오 A — 최상위 이동 → 다른 이동 → 되돌리기 두 번. 공유 _lastMove 에 기대면 두 번째 되돌리기가 엉뚱한 부대를 옮긴다.
        var kit = await OpenWithRootsAsync();
        var root = kit.Id("기본중대");
        var eight = kit.Id("8중대");
        await ConfirmMoveAsync(kit, "기본중대", "3대대");
        await ConfirmMoveAsync(kit, "8중대", "3대대");

        await UndoOnceAsync(kit);
        await UndoOnceAsync(kit);

        Assert.Null(kit.Commands.ParentOf(root));                    // 최상위로 돌아왔다
        Assert.Equal(kit.Id("2대대"), kit.Commands.ParentOf(eight)); // 다른 부대는 되돌린 자리 그대로
        Assert.Equal($"Move:{root}->root", kit.Commands.Writes.Last());
        Assert.False(kit.Vm.CanUndo);
        Assert.False(kit.Vm.BarIsError);
    }

    [Fact]
    public async Task should_return_both_units_to_root_when_two_root_moves_are_undone()
    {
        // 시나리오 B — 최상위 둘을 옮기고 되돌리기 두 번. 공유 _lastMove 는 하나뿐이라 두 번째가 "성공"만 하고 옮기지 않는다.
        var kit = await OpenWithRootsAsync();
        var first = kit.Id("기본중대");
        var second = kit.Id("예비중대");
        await ConfirmMoveAsync(kit, "기본중대", "3대대");
        await ConfirmMoveAsync(kit, "예비중대", "4대대");

        await UndoOnceAsync(kit);
        await UndoOnceAsync(kit);

        Assert.Null(kit.Commands.ParentOf(second));
        Assert.Null(kit.Commands.ParentOf(first));
        Assert.Equal(new[] { $"Move:{second}->root", $"Move:{first}->root" }, kit.Commands.Writes.Skip(2));
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_drop_parent_undo_and_say_why_when_unit_vanished()
    {
        // SIM-F119 — 사라진 부대의 되돌리기는 되돌리려는 순간 판정해 까닭과 함께 뺀다(종전 문서의 Prune 대신)
        var kit = await OpenWithRootsAsync();
        var root = kit.Id("기본중대");
        await ConfirmMoveAsync(kit, "기본중대", "3대대");
        var gone = kit.F.Graph.Nodes.Where(n => n.Id != root).ToList();
        kit.Commands.OnReload = () => kit.Vm.SetData(UnitMapTestData.Tree(UnitMapTestData.Graph(gone, adjacency: kit.F.Graph.Edges.AdjacencyPairs.ToList())), kit.Devices);

        await UndoOnceAsync(kit);

        Assert.Single(kit.Commands.Writes);                         // 되돌리기 쓰기 0
        Assert.Equal(UnitMapText.UndoUnitGoneBar("기본중대"), kit.Vm.BarText);
        Assert.False(kit.Vm.CanUndo);
    }
    #endregion

    #region - MEDIUM-2 — 같은 부대의 앞선 쓰기가 412 · 실패면 뒤의 쓰기를 보내지 않는다 -
    [Theory]
    [InlineData("conflict")]
    [InlineData("failed")]
    public async Task should_discard_queued_position_write_for_same_unit_when_earlier_write_did_not_land(string how)
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var six = kit.Id("6중대");
        kit.Gate.HoldWrites = true;
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));      // 첫 쓰기 — 응답 대기
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 5, 0));       // 둘째 — 첫 쓰기의 자리를 보고 놓았다
        kit.Gate.HoldWrites = false;
        if (how == "conflict") api.SimulateOtherWrite(six, 99, 99);   // 그 사이 다른 운영자가 같은 부대를 옮겼다 → 첫 쓰기 412
        else api.FailNextWrite(new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Server, "500"));
        var barAfterFirst = default(string);
        kit.Vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(UnitMapViewModel.BarText) && barAfterFirst is null) barAfterFirst = kit.Vm.BarText; };

        kit.Gate.ReleaseNextWrite();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(api.Writes);                                  // 둘째는 서버에 가지 않았다
        if (how == "conflict") Assert.Equal(new Vector(99, 99), api.Current.Deltas[six]);   // 남의 변경을 덮지 않았다
        else Assert.False(api.Current.Deltas.ContainsKey(six));
        Assert.True(kit.Vm.BarIsError);
        Assert.Equal(barAfterFirst, kit.Vm.BarText);                 // 첫 쓰기의 충돌 · 실패 막대가 남는다
        Assert.False(kit.Vm.CanUndo);                                // 되돌릴 항목도 생기지 않았다
    }
    #endregion

    #region - TEST-43 r11b (H-10 · D-08 · SIM-C013) — 미룬 알림 뒤 확정은 운영자가 본 판으로 보내고 412 가 지킨다 -
    // 헤디드 r11b(2026-09-29 12:01) 실패 3건의 헤드리스 재현. PRD H-10 "막대 충돌 문구 · 최신 배치로 그려짐 · 재전송 0" ·
    // SIM-C013 "끄는 동안 그림 불변 · 놓은 뒤 반영, 내 PATCH 는 412 → FR-52". 결함: 손을 놓는 순간(M 모드 끝 · 놓기) 미룬 알림의
    // 재조회가 먼저 돌아 최신 문서를 읽고, 뒤따른 쓰기가 그 최신 버전을 If-Match 로 실어 다른 운영자의 Δ 를 말없이 덮었다.

    /// <summary>다른 운영자(가짜 서버)가 그 부대를 옮기고 알림이 온다 — 손이 바빠 미룬다.</summary>
    private static async Task<long> OtherOperatorMovesAsync(MapKit kit, FakeUnitLayoutApi api, int unitId, double dx, double dy)
    {
        var reads = api.ReadCount;
        var version = api.SimulateOtherWrite(unitId, dx, dy);
        await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(version), default);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(reads, api.ReadCount);                          // D-08 — 손이 바쁜 동안 읽지 않는다
        return version;
    }

    [Fact]
    public async Task should_send_seen_version_and_show_other_operators_delta_when_same_unit_changed_during_move_mode()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var seven = kit.Id("7중대");
        var p0 = kit.Vm.Scene.Positions[seven];
        kit.Vm.RequestSelect(seven);
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        for (var i = 0; i < 3; i++) kit.Vm.HandleKey(UnitMapKeyCommand.Right, false);
        var seen = api.Current.Version;
        await OtherOperatorMovesAsync(kit, api, seven, 60, 20);
        Assert.True(kit.Vm.IsMoveMode);

        kit.Vm.HandleKey(UnitMapKeyCommand.Enter, false);
        await kit.Vm.WhenIdleAsync();
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(api.Writes);                        // 재전송 0
        Assert.Equal(seen, write.IfMatch);                            // 운영자가 본 판으로 — 최신 판으로 덮지 않는다
        Assert.IsType<UnitLayoutWrite.Conflict>(write.Result);
        Assert.Equal(new Vector(60, 20), api.Current.Deltas[seven]);  // 서버는 다른 운영자 값 그대로
        Assert.Equal(p0 + new Vector(60, 20), kit.Vm.Scene.Positions[seven]);   // 최신 배치로 그려짐(SIM-C013)
        Assert.Equal(UnitMapText.LayoutConflictBar("7중대"), kit.Vm.BarText);
        Assert.False(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_send_seen_version_and_show_latest_when_same_unit_changed_during_drag()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var seven = kit.Id("7중대");
        var p0 = kit.Vm.Scene.Positions[seven];
        var seen = api.Current.Version;
        kit.Vm.BeginDrag(seven);
        await OtherOperatorMovesAsync(kit, api, seven, 60, 20);
        Assert.Equal(p0, kit.Vm.Scene.Positions[seven]);             // 끄는 동안 그림 불변

        kit.Vm.CompleteDrag(new UnitMapDropRequest(seven, 40, 40, null, false));
        await kit.Vm.WhenIdleAsync();
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        var write = Assert.Single(api.Writes);
        Assert.Equal(seen, write.IfMatch);
        Assert.Equal(new Vector(40, 40), write.Change.Set[seven]);   // 놓은 자리 = 본 그림 기준(최신 Δ + 끈 양이 아니다)
        Assert.IsType<UnitLayoutWrite.Conflict>(write.Result);
        Assert.Equal(p0 + new Vector(60, 20), kit.Vm.Scene.Positions[seven]);
        Assert.Equal(UnitMapText.LayoutConflictBar("7중대"), kit.Vm.BarText);
    }

    [Fact]
    public async Task should_still_save_my_move_when_other_operator_changed_a_different_unit_during_move_mode()
    {
        // 건드린 부대(끈 부대 + 조상) 밖의 변경은 충돌이 아니다(FR-52) — 막지 않는다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var seven = kit.Id("7중대");
        var thirty = kit.Id("30중대");
        kit.Vm.RequestSelect(seven);
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        kit.Vm.HandleKey(UnitMapKeyCommand.Right, false);
        await OtherOperatorMovesAsync(kit, api, thirty, 5, 5);

        kit.Vm.HandleKey(UnitMapKeyCommand.Enter, false);
        await kit.Vm.WhenIdleAsync();
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.True(api.Writes.Last().Succeeded);
        Assert.Equal(new Vector(UnitMapViewModel.MoveStep, 0), api.Current.Deltas[seven]);
        Assert.Equal(new Vector(5, 5), api.Current.Deltas[thirty]);
        Assert.False(kit.Vm.BarIsError);
        Assert.True(kit.Vm.CanUndo);
    }

    [Fact]
    public async Task should_reject_stale_write_and_show_b_latest_when_two_operators_move_same_unit()
    {
        // H-10 두 운영자판 — 같은 가짜 서버 · A(main) · B 두 창구. ① B 가 옮기면 A 는 알림으로 다시 읽는다 ② A 가 M 모드인 동안 B 가 같은 부대 저장 → A Enter
        var server = new FakeUnitLayoutApi();
        var a = await MapKit.OpenAsync(server);
        var b = await MapKit.OpenAsync(server, port: server.ForClient("B"));
        server.Published = v =>
        {
            _ = a.Vm.HandleAsync(new UnitLayoutChangedMessage(v), default);
            _ = b.Vm.HandleAsync(new UnitLayoutChangedMessage(v), default);
        };
        var seven = a.Id("7중대");
        var p0 = a.Vm.Scene.Positions[seven];

        b.Vm.CompleteDrag(new UnitMapDropRequest(seven, 0, 40, null, false));   // ①
        await b.Vm.WhenIdleAsync();
        a.Delay.ElapseAll(); b.Delay.ElapseAll();
        await a.Vm.WhenIdleAsync();
        Assert.Equal(p0 + new Vector(0, 40), a.Vm.Scene.Positions[seven]);     // FR-53 — A 가 B 의 변경을 그린다

        a.Vm.RequestSelect(seven);                                               // ②
        a.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        for (var i = 0; i < 3; i++) a.Vm.HandleKey(UnitMapKeyCommand.Right, false);
        var aReads = server.ReadCount;
        b.Vm.CompleteDrag(new UnitMapDropRequest(seven, 60, 0, null, false));   // B 의 Δ (60, 40)
        await b.Vm.WhenIdleAsync();
        a.Delay.ElapseAll();
        await a.Vm.WhenIdleAsync();
        Assert.Equal(new Vector(60, 40), server.Current.Deltas[seven]);
        Assert.Equal(aReads, server.ReadCount);                                 // A 는 M 모드라 미뤘고, B 는 자기 메아리를 건너뛰었다

        var aWritesBefore = server.Writes.Count(w => w.Client == "main");
        a.Vm.HandleKey(UnitMapKeyCommand.Enter, false);
        await a.Vm.WhenIdleAsync();
        a.Delay.ElapseAll(); b.Delay.ElapseAll();
        await a.Vm.WhenIdleAsync();
        await b.Vm.WhenIdleAsync();

        var aw = server.Writes.Where(w => w.Client == "main").Skip(aWritesBefore).ToList();
        var write = Assert.Single(aw);                                           // 재전송 0
        Assert.IsType<UnitLayoutWrite.Conflict>(write.Result);
        Assert.Equal(new Vector(60, 40), server.Current.Deltas[seven]);         // B 의 값 그대로
        Assert.Equal(p0 + new Vector(60, 40), a.Vm.Scene.Positions[seven]);     // A 는 B 의 최신을 그린다
        Assert.Equal(UnitMapText.LayoutConflictBar("7중대"), a.Vm.BarText);
    }
    #endregion

    #region - MEDIUM-3 — 트리 이동 뒤 정리도 한 줄 대기열을 탄다 -
    [Fact]
    public async Task should_queue_parent_change_cleanup_behind_in_flight_write()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Gate.HoldWrites = true;
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));      // 쓰기 응답 대기 중
        var reads = api.ReadCount;

        var cleanup = kit.Vm.CleanupAfterParentChangeAsync(kit.Id("8중대"));

        Assert.Equal(reads, api.ReadCount);                          // 앞선 쓰기가 끝나기 전에는 읽지도 않는다
        Assert.False(cleanup.IsCompleted);

        kit.Gate.ReleaseNextWrite();
        await cleanup;
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(reads + 1, api.ReadCount);
    }
    #endregion

    #region - MEDIUM-4 — 확정이 대기열에서 차례를 받았을 때 콘솔이 바쁘면 기다린다 -
    /// <summary>앞선 위치 쓰기 뒤에 상위 확정을 대기열에 싣고, 그 쓰기가 끝나는 순간 콘솔이 바빠지게 한다.</summary>
    private static async Task<Task> QueueMoveBehindWriteThenBusyAsync(MapKit kit, FakeUnitLayoutApi api)
    {
        kit.Commands.IsConsoleBusy = () => kit.Bridge.IsBusy;
        kit.Gate.HoldWrites = true;
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));      // 앞선 쓰기
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));
        var confirmed = kit.Vm.ConfirmAsync();                        // 대기열에서 앞선 쓰기 뒤
        api.Published = _ => kit.Bridge.IsBusy = true;                // 앞선 쓰기가 닿는 순간 콘솔이 바빠졌다(재조회 · 트리 작업)

        kit.Gate.ReleaseNextWrite();
        // 확정이 차례를 받아 "보냈거나(결함)" "기다리기 시작했거나(수정)" — 시간 대기 없이 둘 중 먼저 온 것.
        await Task.WhenAny(kit.Commands.FirstWrite.Task, kit.Bridge.ExtraBusySubscriber.Task).WaitAsync(TimeSpan.FromSeconds(10));
        return confirmed;
    }

    [Fact]
    public async Task should_wait_for_console_to_become_idle_before_sending_confirmed_move()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var confirmed = await QueueMoveBehindWriteThenBusyAsync(kit, api);

        Assert.Equal(0, kit.Commands.WriteCalls);                     // 바쁜 콘솔에 보내 버리지 않는다
        kit.Bridge.IsBusy = false;
        await confirmed;
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(new[] { $"Move:{kit.Id("8중대")}->{kit.Id("3대대")}" }, kit.Commands.Writes);
        Assert.Equal(0, kit.Commands.WritesWhileBusy);
        Assert.False(kit.Vm.BarIsError);
    }

    [Fact]
    public async Task should_not_send_and_say_why_when_console_stays_busy_past_the_wait()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var confirmed = await QueueMoveBehindWriteThenBusyAsync(kit, api);

        kit.Delay.ElapseAll();                                        // 기다림이 끝났는데도 바쁘다
        await confirmed;
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(0, kit.Commands.WriteCalls);
        Assert.True(kit.Vm.BarIsError);
        Assert.Contains("앞선 작업", kit.Vm.BarText);
        Assert.Contains("8중대", kit.Vm.BarText);
    }
    #endregion

    #region - MEDIUM-7 — 열린 뒤의 다시 읽기 실패는 마지막으로 읽은 배치를 지킨다 -
    [Fact]
    public async Task should_keep_last_known_layout_and_offer_retry_when_reread_fails_after_open()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var six = kit.Id("6중대");
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 40, 0));
        await kit.Vm.WhenIdleAsync();
        var moved = kit.Vm.Scene.Positions[six];
        var version = api.SimulateOtherWrite(kit.Id("30중대"), 5, 0);
        api.Mode = FakeLayoutServerMode.Failing;
        api.FailureKind = UnitLayoutFailureKind.Server;

        await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(version), default);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(moved, kit.Vm.Scene.Positions[six]);            // 자동 배치로 무너지지 않는다
        Assert.True(kit.Vm.Scene.FactsOf(six).IsMoved);
        Assert.True(kit.Vm.CanRetryLayout);                          // [다시 시도] 띠
        Assert.StartsWith("배치를 다시 불러오지 못했습니다", kit.Vm.LayoutStatusText);

        api.Mode = FakeLayoutServerMode.Supported;
        await kit.Vm.RetryLayoutAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.False(kit.Vm.CanRetryLayout);
        Assert.True(kit.Vm.Scene.FactsOf(kit.Id("30중대")).IsMoved);
        Assert.StartsWith("배치: 모든 운영자 공유", kit.Vm.LayoutStatusText);
    }
    #endregion

    #region - L-1 · MEDIUM-T2 — 합침 창 끝에 미룬 알림도 30초 상한을 센다 -
    [Fact]
    public async Task should_say_once_that_change_waits_when_notice_is_deferred_at_settle_time()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        var reads = api.ReadCount;
        await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(api.SimulateOtherWrite(kit.Id("30중대"), 1, 0)), default);   // 한가 — 합침 창 열림
        kit.Vm.RequestSelect(kit.Id("6중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);                                                            // 창이 닫히기 전에 손이 바빠졌다

        kit.Delay.ElapseAll();                   // 합침 창 끝 — 이 순간 미룬다
        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        kit.Delay.ElapseAll();                   // 연기 상한

        Assert.Equal(UnitMapText.LayoutNoticeDeferredStatus, kit.Vm.StatusText);
        Assert.Equal(reads, api.ReadCount);      // 강제 반영 없음

        kit.Vm.HandleKey(UnitMapKeyCommand.Escape, false);
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(reads + 1, api.ReadCount);  // 손을 놓자 한 번 읽었다
    }
    #endregion

    #region - L-2 · L-3 — 막대가 한 일을 그대로 말한다 -
    [Fact]
    public async Task should_say_adjacency_was_not_changed_when_adjoin_write_fails()
    {
        var kit = await MapKit.OpenAsync();
        kit.Commands.AdjacencyResult = false;
        kit.Vm.CompleteDrag(Drop(kit, "6중대", "9중대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.True(kit.Vm.BarIsError);
        Assert.DoesNotContain("옮기지", kit.Vm.BarText);
        Assert.Contains("인접", kit.Vm.BarText);
        Assert.Contains("9중대", kit.Vm.BarText);
    }

    [Fact]
    public async Task should_not_call_layout_a_unit_when_reset_write_fails()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        api.FailNextWrite(new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Server, "500"));
        kit.Vm.RequestResetLayout();

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.True(kit.Vm.BarIsError);
        Assert.DoesNotContain("‘배치’", kit.Vm.BarText);
        Assert.Contains("초기화", kit.Vm.BarText);
    }
    #endregion

    #region - L-6 — 예외 원문은 막대에 싣지 않는다 -
    [Fact]
    public async Task should_show_operator_text_not_exception_message_when_queued_work_throws()
    {
        var kit = await MapKit.OpenAsync();
        kit.Commands.ThrowOnWrite = new InvalidOperationException("내부 스택 원문 0xDEAD");
        kit.Vm.CompleteDrag(Drop(kit, "8중대", "3대대"));

        await kit.Vm.ConfirmAsync();
        await kit.Vm.WhenIdleAsync();

        Assert.True(kit.Vm.BarIsError);
        Assert.DoesNotContain("0xDEAD", kit.Vm.BarText);
        Assert.Equal(UnitMapText.UnexpectedFailureBar("8중대"), kit.Vm.BarText);
        Assert.Contains(kit.Log.Errors, e => e.Contains("0xDEAD", StringComparison.Ordinal));   // 원문은 기록으로
    }
    #endregion

    #region - L-7 — 닫으면 시작하지 않은 쓰기 · 연기 감시를 거둔다 -
    [Fact]
    public async Task should_not_send_queued_writes_after_dispose()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Gate.HoldWrites = true;
        kit.Vm.CompleteDrag(Drop(kit, "6중대", null, 10, 0));
        kit.Vm.CompleteDrag(Drop(kit, "7중대", null, 10, 0));      // 아직 시작하지 않았다
        kit.Gate.HoldWrites = false;

        kit.Vm.Dispose();
        kit.Gate.ReleaseNextWrite();
        await kit.Vm.WhenIdleAsync();

        Assert.Single(api.Writes);
    }

    [Fact]
    public async Task should_stop_watching_deferred_notice_when_disposed()
    {
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.RequestSelect(kit.Id("6중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.MoveMode, false);
        await kit.Vm.HandleAsync(new UnitLayoutChangedMessage(api.SimulateOtherWrite(kit.Id("30중대"), 1, 0)), default);
        Assert.True(kit.Delay.Pending > 0);                          // 30초 연기 감시

        kit.Vm.Dispose();

        Assert.Equal(0, kit.Delay.Pending);
    }
    #endregion
}
