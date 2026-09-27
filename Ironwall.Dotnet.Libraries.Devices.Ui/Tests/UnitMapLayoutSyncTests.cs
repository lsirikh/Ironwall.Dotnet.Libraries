using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-14 — 공유 배치 동기화 판정: 지원 판정 · 412 병합 · 알림 재조회 · 되돌리기 · 2클라이언트 전수(NFR-15)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-P001~P030(판정) · Q001~Q045(동시성 · 되돌리기) · N076~N082(배치 알림) · F121 · F122.
                  편제는 200 부대 픽스처 — X = 4중대(1대대 › 1연대 › 사단), 예하 = 41소초, 무관 = 6중대(2대대).
****************************************************************************/
public class UnitMapLayoutSyncTests
{
    private static readonly UnitMapFixture Org = UnitMapTestData.Standard200();
    private static int X => Org.IdOf("4중대");
    private static int Parent => Org.IdOf("1대대");
    private static int Grandparent => Org.IdOf("1연대");
    private static int Child => Org.IdOf("41소초");
    private static int Unrelated => Org.IdOf("6중대");
    private static IReadOnlyList<int> TouchedX => UnitMapLayoutSync.TouchedUnits(Org.Tree, X);

    #region - 지원 판정 (FR-50 · FR-07) -
    [Fact]
    public void should_be_shared_and_writable_when_server_supports_layout()
    {
        var a = UnitMapLayoutSync.Classify(new UnitLayoutRead.Supported(Snap(13, (X, 5, 5))));

        Assert.Equal(UnitMapLayoutState.Shared, a.State);
        Assert.True(a.CanWrite);
        Assert.True(a.AppliesDeltas);
        Assert.Equal(13, a.Snapshot!.Version);
    }

    [Fact]
    public void should_be_session_only_and_writable_in_memory_when_server_lacks_layout()
    {
        var a = UnitMapLayoutSync.Classify(new UnitLayoutRead.Unsupported("422 라우트 가림"));

        Assert.Equal(UnitMapLayoutState.SessionOnly, a.State);
        Assert.True(a.CanWrite);
        Assert.True(a.AppliesDeltas);
    }

    [Theory]
    [InlineData(UnitLayoutFailureKind.Timeout)]
    [InlineData(UnitLayoutFailureKind.Server)]
    [InlineData(UnitLayoutFailureKind.Forbidden)]
    [InlineData(UnitLayoutFailureKind.Unauthorized)]
    [InlineData(UnitLayoutFailureKind.Unreachable)]
    [InlineData(UnitLayoutFailureKind.Parse)]
    public void should_forbid_writes_when_read_failed(UnitLayoutFailureKind kind)
    {
        var a = UnitMapLayoutSync.Classify(new UnitLayoutRead.Failed(kind, "x"));

        Assert.Equal(UnitMapLayoutState.ReadFailed, a.State);
        Assert.Equal(kind, a.Failure);
        Assert.False(a.CanWrite);
        Assert.False(a.AppliesDeltas);
    }

    [Fact]
    public void should_block_writes_and_ignore_deltas_when_layout_version_differs()
    {
        var a = UnitMapLayoutSync.Classify(new UnitLayoutRead.Supported(Snap(13, layoutVersion: 2, (X, 5, 5))));

        Assert.Equal(UnitMapLayoutState.VersionMismatch, a.State);
        Assert.False(a.CanWrite);
        Assert.False(a.AppliesDeltas);
    }

    [Fact]
    public void should_neither_write_nor_apply_while_loading()
    {
        Assert.Equal(UnitMapLayoutState.Loading, UnitMapLayoutAssessment.Loading.State);
        Assert.False(UnitMapLayoutAssessment.Loading.CanWrite);
        Assert.False(UnitMapLayoutAssessment.Loading.AppliesDeltas);
    }
    #endregion

    #region - 건드린 부대 · 쓰기 모양 -
    [Fact]
    public void should_touch_unit_and_all_ancestors_but_not_descendants()
    {
        var touched = UnitMapLayoutSync.TouchedUnits(Org.Tree, X);

        Assert.Equal(new[] { X, Parent, Grandparent, Org.IdOf("제○○사단") }, touched);
        Assert.DoesNotContain(Child, touched);
    }

    [Fact]
    public void should_touch_only_itself_when_unit_is_unknown_or_orphan()
    {
        Assert.Equal(new[] { 999_999 }, UnitMapLayoutSync.TouchedUnits(Org.Tree, 999_999));
        var orphan = UnitMapTestData.OrphanParent();
        Assert.Equal(new[] { orphan.IdOf("9중대") }, UnitMapLayoutSync.TouchedUnits(orphan.Tree, orphan.IdOf("9중대")));
    }

    [Fact]
    public void should_send_one_patch_with_one_item_when_subtree_moved()
    {
        var battalion = Parent;
        Assert.True(Org.Tree.DescendantIds(battalion).Count > 10);   // 예하가 많아도

        var change = UnitMapLayoutSync.MoveChange(battalion, new Vector(-40, 20));

        var item = Assert.Single(change.Set);                       // 항목은 끈 부대 하나
        Assert.Equal((battalion, new Vector(-40, 20)), (item.Key, item.Value));
        Assert.Empty(change.Clear);
        Assert.False(change.ClearAll);
    }

    [Fact]
    public void should_reverse_to_previous_delta_or_clear_when_undoing_a_move()
    {
        var restore = UnitMapLayoutSync.ReverseChange(X, new Vector(3, 4));
        var clear = UnitMapLayoutSync.ReverseChange(X, null);

        Assert.Equal(new Vector(3, 4), Assert.Single(restore.Set).Value);
        Assert.Empty(restore.Clear);
        Assert.Empty(clear.Set);
        Assert.Equal(new[] { X }, clear.Clear);
    }
    #endregion

    #region - 412 병합 (FR-52) -
    [Fact]
    public void should_retry_once_with_new_version_when_conflict_did_not_touch_my_units()
    {
        var before = Snap(13, (X, 5, 5));
        var latest = Snap(14, (X, 5, 5), (Unrelated, 1, 1));

        var r = UnitMapLayoutSync.Resolve(before, new UnitLayoutRead.Supported(latest), TouchedX, isRetry: false);

        Assert.Equal(14, Assert.IsType<UnitMapConflictResolution.Retry>(r).IfMatchVersion);
    }

    [Fact]
    public void should_retry_when_only_a_descendant_changed()
    {
        var r = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Supported(Snap(14, (Child, 9, 9))), TouchedX, isRetry: false);

        Assert.IsType<UnitMapConflictResolution.Retry>(r);             // SIM-Q013 · Q016
    }

    [Fact]
    public void should_retry_when_other_operator_wrote_the_same_delta_value()
    {
        var r = UnitMapLayoutSync.Resolve(Snap(13, (X, 5, 5)), new UnitLayoutRead.Supported(Snap(14, (X, 5, 5))), TouchedX, isRetry: false);

        Assert.IsType<UnitMapConflictResolution.Retry>(r);             // SIM-Q037 · Q040 — 값으로 비교한다
    }

    [Theory]
    [InlineData("self")]
    [InlineData("parent")]
    [InlineData("grandparent")]
    public void should_not_apply_when_conflict_touched_my_unit_or_ancestor(string who)
    {
        var id = who switch { "self" => X, "parent" => Parent, _ => Grandparent };

        var r = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Supported(Snap(14, (id, 9, 9))), TouchedX, isRetry: false);

        var abandon = Assert.IsType<UnitMapConflictResolution.Abandon>(r);
        Assert.Equal(UnitMapConflictReason.TouchedUnitChanged, abandon.Reason);
        Assert.Equal(new[] { id }, abandon.ChangedUnitIds);
        Assert.Equal(14, abandon.Latest!.Version);                    // 최신 배치를 그린다
    }

    [Fact]
    public void should_not_apply_when_my_unit_delta_was_cleared_by_other()
    {
        var r = UnitMapLayoutSync.Resolve(Snap(13, (X, 5, 5)), new UnitLayoutRead.Supported(Snap(14)), TouchedX, isRetry: false);

        Assert.Equal(UnitMapConflictReason.TouchedUnitChanged, Assert.IsType<UnitMapConflictResolution.Abandon>(r).Reason);
    }

    [Fact]
    public void should_not_apply_when_other_operator_cleared_everything()
    {
        // X 와 조상에 Δ 가 없어도 전체 초기화는 "남의 의도" 다 — 되살리지 않는다(SIM-Q025 · Q028).
        var r = UnitMapLayoutSync.Resolve(Snap(13, (Unrelated, 1, 1)), new UnitLayoutRead.Supported(Snap(14)), TouchedX, isRetry: false);

        Assert.Equal(UnitMapConflictReason.ClearedAll, Assert.IsType<UnitMapConflictResolution.Abandon>(r).Reason);
    }

    [Fact]
    public void should_not_apply_when_topology_changed_meanwhile()
    {
        var r = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Supported(Snap(14)), TouchedX, isRetry: false, topologyChanged: true);

        Assert.Equal(UnitMapConflictReason.TopologyChanged, Assert.IsType<UnitMapConflictResolution.Abandon>(r).Reason);   // SIM-Q029
    }

    [Fact]
    public void should_stop_after_the_retry_also_conflicts()
    {
        var r = UnitMapLayoutSync.Resolve(Snap(14), new UnitLayoutRead.Supported(Snap(16, (Unrelated, 2, 2))), TouchedX, isRetry: true);

        var abandon = Assert.IsType<UnitMapConflictResolution.Abandon>(r);   // SIM-Q041 — 무한 재시도 없음
        Assert.Equal(UnitMapConflictReason.RetryAlsoConflicted, abandon.Reason);
        Assert.Equal(16, abandon.Latest!.Version);
    }

    [Fact]
    public void should_not_apply_when_latest_cannot_be_read_or_changed_layout_version()
    {
        var unreadable = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Failed(UnitLayoutFailureKind.Timeout, "t"), TouchedX, false);
        var gone = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Unsupported("x"), TouchedX, false);
        var bumped = UnitMapLayoutSync.Resolve(Snap(13), new UnitLayoutRead.Supported(Snap(14, layoutVersion: 2)), TouchedX, false);

        Assert.Equal(UnitMapConflictReason.Unreadable, Assert.IsType<UnitMapConflictResolution.Abandon>(unreadable).Reason);
        Assert.Equal(UnitMapConflictReason.Unreadable, Assert.IsType<UnitMapConflictResolution.Abandon>(gone).Reason);
        Assert.Equal(UnitMapConflictReason.LayoutVersionChanged, Assert.IsType<UnitMapConflictResolution.Abandon>(bumped).Reason);
    }
    #endregion

    #region - 배치 알림 (FR-53) -
    [Theory]
    [InlineData(13, 13)]
    [InlineData(13, 14)]
    public void should_skip_refetch_when_notice_version_not_newer(long notice, long have)
    {
        Assert.Equal(UnitMapRefetchDecision.Skip, UnitMapLayoutSync.ShouldRefetch(notice, have, UnitMapBusy.None));
        Assert.Equal(UnitMapRefetchDecision.Skip, UnitMapLayoutSync.ShouldRefetch(notice, have, UnitMapBusy.Dragging));
    }

    [Fact]
    public void should_refetch_when_notice_is_newer_and_idle()
    {
        Assert.Equal(UnitMapRefetchDecision.Refetch, UnitMapLayoutSync.ShouldRefetch(14, 13, UnitMapBusy.None));   // SIM-N076
    }

    [Theory]
    [InlineData(UnitMapBusy.Dragging)]
    [InlineData(UnitMapBusy.MoveMode)]
    [InlineData(UnitMapBusy.Confirming)]
    [InlineData(UnitMapBusy.WriteInFlight)]
    [InlineData(UnitMapBusy.Dragging | UnitMapBusy.WriteInFlight)]
    public void should_defer_refetch_when_dragging_or_move_mode_or_confirming(UnitMapBusy busy)
    {
        Assert.Equal(UnitMapRefetchDecision.Defer, UnitMapLayoutSync.ShouldRefetch(20, 13, busy));   // SIM-N080 · N082
    }

    [Fact]
    public void should_defer_own_echo_during_write_then_skip_after_response()
    {
        // 실측(V-11): 자기 쓰기의 알림이 응답보다 ~5 ms 먼저 온다(SIM-N079).
        Assert.Equal(UnitMapRefetchDecision.Defer, UnitMapLayoutSync.ShouldRefetch(14, 13, UnitMapBusy.WriteInFlight));
        Assert.Equal(UnitMapRefetchDecision.Skip, UnitMapLayoutSync.ShouldRefetch(14, 14, UnitMapBusy.None));
    }

    [Fact]
    public void should_refetch_when_nothing_was_read_yet()
    {
        Assert.Equal(UnitMapRefetchDecision.Refetch, UnitMapLayoutSync.ShouldRefetch(3, null, UnitMapBusy.None));
        Assert.Equal(UnitMapRefetchDecision.Defer, UnitMapLayoutSync.ShouldRefetch(3, null, UnitMapBusy.Dragging));
    }
    #endregion

    #region - 되돌리기 허용 (FR-35) -
    [Fact]
    public void should_allow_undo_with_latest_version_when_nobody_touched_my_units()
    {
        var expected = Snap(14, (X, 5, 5));

        var same = UnitMapLayoutSync.CanUndo(expected, expected, TouchedX);
        var unrelated = UnitMapLayoutSync.CanUndo(expected, Snap(15, (X, 5, 5), (Unrelated, 1, 1)), TouchedX);

        Assert.Equal(14, Assert.IsType<UnitMapUndoCheck.Allowed>(same).IfMatchVersion);
        Assert.Equal(15, Assert.IsType<UnitMapUndoCheck.Allowed>(unrelated).IfMatchVersion);    // SIM-Q044
    }

    [Theory]
    [InlineData("self")]
    [InlineData("parent")]
    public void should_refuse_undo_when_unit_changed_by_other_operator(string who)
    {
        var id = who == "self" ? X : Parent;
        var expected = Snap(14, (X, 5, 5));

        var latest = who == "self" ? Snap(15, (X, 7, 7)) : Snap(15, (X, 5, 5), (id, 7, 7));

        var check = UnitMapLayoutSync.CanUndo(expected, latest, TouchedX);

        var refused = Assert.IsType<UnitMapUndoCheck.Refused>(check);   // SIM-Q042 · Q043 · F121 · F122
        Assert.Equal(UnitMapConflictReason.TouchedUnitChanged, refused.Reason);
        Assert.Contains(id, refused.ChangedUnitIds);
    }

    [Fact]
    public void should_refuse_undo_when_other_operator_cleared_everything()
    {
        var check = UnitMapLayoutSync.CanUndo(Snap(14, (X, 5, 5)), Snap(15), TouchedX);

        Assert.IsType<UnitMapUndoCheck.Refused>(check);                 // SIM-Q045
    }

    [Fact]
    public void should_refuse_undo_when_layout_version_changed()
    {
        var check = UnitMapLayoutSync.CanUndo(Snap(14, (X, 5, 5)), Snap(15, layoutVersion: 2, (X, 5, 5)), TouchedX);

        Assert.Equal(UnitMapConflictReason.LayoutVersionChanged, Assert.IsType<UnitMapUndoCheck.Refused>(check).Reason);
    }
    #endregion

    #region - NFR-15: 2클라이언트 엇갈림 전수 -
    public static IEnumerable<object[]> TwoClientCases()
    {
        var relations = new[] { "same", "parent", "grandparent", "child", "unrelated" };
        var ops = new[] { "set", "clear", "clear_all" };
        var initial = new[] { false, true };                  // X 에 처음부터 Δ 가 있었나
        foreach (var order in Interleavings())
            foreach (var relation in relations)
                foreach (var op in ops)
                    foreach (var xHadDelta in initial)
                        yield return new object[] { order, relation, op, xHadDelta };
    }

    /// <summary>A.읽기 → A.쓰기 · B.읽기 → B.쓰기 의 모든 엇갈림(각자 읽기가 먼저) — 6 가지.</summary>
    private static IEnumerable<string> Interleavings()
    {
        var events = new[] { "Ar", "Aw", "Br", "Bw" };
        foreach (var p in Permutations(events))
        {
            var s = string.Join(",", p);
            if (s.IndexOf("Ar", StringComparison.Ordinal) < s.IndexOf("Aw", StringComparison.Ordinal)
                && s.IndexOf("Br", StringComparison.Ordinal) < s.IndexOf("Bw", StringComparison.Ordinal))
                yield return s;
        }
    }

    [Theory]
    [MemberData(nameof(TwoClientCases))]
    public async Task should_never_silently_overwrite_when_two_clients_interleave(string order, string relation, string op, bool xHadDelta)
    {
        var (server, a, b) = await RunTwoClientsAsync(order, relation, op, xHadDelta);

        // Assert ① 보고와 서버 기록이 일치한다 — 성공이라고 한 쓰기는 서버에 있고, 포기는 서버에 없다.
        foreach (var c in new[] { a, b })
        {
            var saved = server.Writes.Where(w => w.Client == c.Name && w.Succeeded).ToList();
            Assert.True(c.Succeeded ? saved.Count == 1 : saved.Count == 0, $"{c.Name}: 보고={c.Succeeded} 서버 성공={saved.Count}");
            Assert.True(c.Succeeded || c.Abandoned is not null, $"{c.Name}: 성공도 포기도 아닌 채로 끝났다");
        }

        // Assert ② 말없는 덮어쓰기 0 — 어떤 성공 쓰기 W 도, W 가 읽은 뒤 남이 성공시킨 쓰기가 바꾼 부대를 건드리지 않는다.
        var history = server.Writes.Where(w => w.Succeeded).OrderBy(w => w.VersionAfter).ToList();
        foreach (var w in history)
        {
            var mine = w.Client == "A" ? a : b;
            var others = history.Where(o => o.Client != w.Client
                                            && o.VersionAfter > mine.BaseVersion
                                            && o.VersionAfter < w.VersionAfter);
            foreach (var other in others)
            {
                var changed = ChangedBy(other);
                var overlap = mine.Touched is null ? changed.ToList() : changed.Intersect(mine.Touched).ToList();
                Assert.True(overlap.Count == 0,
                    $"{order} {relation} {op} x={xHadDelta}: {w.Client} 의 v{w.VersionAfter} 가 {other.Client} 의 v{other.VersionAfter} 변경({string.Join(",", overlap)})을 말없이 덮었다");
            }
        }

        // Assert ③ 재시도는 한 번뿐.
        Assert.True(server.Writes.Count(w => w.Client == "A") <= 2);
        Assert.True(server.Writes.Count(w => w.Client == "B") <= 2);
    }

    [Fact]
    public async Task should_exercise_retry_and_abandon_paths_when_the_exhaustive_matrix_runs()
    {
        // 전수 시험이 공허하게 통과하지 않는지 — 재전송 성공 · 포기 · 첫 쓰기 성공이 모두 실제로 일어나야 한다.
        int retried = 0, abandoned = 0, firstTry = 0, cases = 0;
        foreach (var row in TwoClientCases())
        {
            cases++;
            var (server, a, b) = await RunTwoClientsAsync((string)row[0], (string)row[1], (string)row[2], (bool)row[3]);
            foreach (var c in new[] { a, b })
            {
                var writes = server.Writes.Where(w => w.Client == c.Name).ToList();
                if (c.Succeeded && writes.Count == 2) retried++;
                if (c.Succeeded && writes.Count == 1) firstTry++;
                if (c.Abandoned is not null) abandoned++;
            }
        }

        Assert.Equal(6 * 5 * 3 * 2, cases);
        Assert.True(retried > 0, "재전송 성공 경로가 한 번도 돌지 않았다");
        Assert.True(abandoned > 0, "포기 경로가 한 번도 돌지 않았다");
        Assert.True(firstTry > 0, "첫 쓰기 성공 경로가 한 번도 돌지 않았다");
    }

    private static async Task<(FakeUnitLayoutApi Server, SimClient A, SimClient B)> RunTwoClientsAsync(string order, string relation, string op, bool xHadDelta)
    {
        // Arrange — 가짜 서버 하나에 클라이언트 둘. A 는 X 를 옮기고, B 는 관계(relation)에 있는 부대에 op 를 한다.
        var server = new FakeUnitLayoutApi(version: 10);
        if (xHadDelta) server.SimulateOtherWrite(X, 3, 3, "시드");
        server.SimulateOtherWrite(Org.IdOf("9중대"), 1, 1, "시드");          // 문서가 비지 않게(전체 초기화가 의미를 갖게)
        var target = relation switch
        {
            "same" => X,
            "parent" => Parent,
            "grandparent" => Grandparent,
            "child" => Child,
            _ => Unrelated,
        };
        var bChange = op switch
        {
            "set" => UnitLayoutChange.SetOne(target, new Vector(50, -50)),
            "clear" => UnitLayoutChange.ClearOne(target),
            _ => UnitLayoutChange.ClearEverything(),
        };
        var a = new SimClient("A", server, UnitLayoutChange.SetOne(X, new Vector(-20, 40)), TouchedX);
        // 전체 초기화의 "건드린 부대" 는 문서 전체다(null) — 그 사이 무엇이든 바뀌었으면 되살리지 않는다.
        var b = new SimClient("B", server, bChange, bChange.ClearAll
            ? null
            : UnitMapLayoutSync.TouchedUnits(Org.Tree, target));

        // Act
        foreach (var step in order.Split(','))
        {
            var client = step[0] == 'A' ? a : b;
            if (step[1] == 'r') await client.ReadAsync();
            else await client.WriteAsync();
        }
        return (server, a, b);
    }

    /// <summary>그 쓰기가 <b>실제로</b> 바꾼 부대 — 적용 전 · 뒤 문서의 Δ 를 값으로 견준다(의도가 아니라 결과).</summary>
    private static IEnumerable<int> ChangedBy(FakeLayoutWrite write)
    {
        var before = write.SnapshotBefore.Deltas;
        var after = write.SnapshotAfter!.Deltas;
        return before.Keys.Union(after.Keys)
                     .Where(id => before.TryGetValue(id, out var b) != after.TryGetValue(id, out var a) || b != a);
    }

    /// <summary>시험용 클라이언트 — 읽기 1회 · 쓰기(412 면 최신 받아 <see cref="UnitMapLayoutSync.Resolve"/>, 재시도 최대 1회).</summary>
    private sealed class SimClient
    {
        private readonly IUnitLayoutApi _api;
        private readonly UnitLayoutChange _change;
        private UnitLayoutSnapshot? _base;

        public SimClient(string name, FakeUnitLayoutApi server, UnitLayoutChange change, IReadOnlyList<int>? touched)
        {
            Name = name;
            _api = server.ForClient(name);
            _change = change;
            Touched = touched;
        }

        public string Name { get; }
        public IReadOnlyList<int>? Touched { get; }
        public long BaseVersion => _base?.Version ?? -1;
        public bool Succeeded { get; private set; }
        public UnitMapConflictReason? Abandoned { get; private set; }

        public async Task ReadAsync()
            => _base = Assert.IsType<UnitLayoutRead.Supported>(await _api.ReadAsync()).Snapshot;

        public async Task WriteAsync()
        {
            var before = _base!;
            var ifMatch = before.Version;
            for (var attempt = 0; ; attempt++)
            {
                var result = await _api.WriteAsync(ifMatch, _change);
                if (result is UnitLayoutWrite.Saved) { Succeeded = true; return; }
                Assert.IsType<UnitLayoutWrite.Conflict>(result);

                var latest = await _api.ReadAsync();
                var decision = UnitMapLayoutSync.Resolve(before, latest, Touched, isRetry: attempt > 0);
                if (decision is UnitMapConflictResolution.Abandon abandon) { Abandoned = abandon.Reason; return; }

                var retry = Assert.IsType<UnitMapConflictResolution.Retry>(decision);
                // 재시도의 기준은 방금 읽은 최신 — 두 번째 충돌 판정은 그 문서와 비교한다.
                before = retry.Latest;
                ifMatch = retry.IfMatchVersion;
            }
        }
    }
    #endregion

    #region - Helpers -
    private static UnitLayoutSnapshot Snap(long version, params (int Id, double Dx, double Dy)[] items)
        => Snap(version, 1, items);

    private static UnitLayoutSnapshot Snap(long version, int layoutVersion, params (int Id, double Dx, double Dy)[] items)
        => new(version, layoutVersion, null, null, items.ToDictionary(i => i.Id, i => new Vector(i.Dx, i.Dy)));

    private static IEnumerable<T[]> Permutations<T>(T[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (var i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, j) => j != i).ToArray();
            foreach (var tail in Permutations(rest))
                yield return new[] { items[i] }.Concat(tail).ToArray();
        }
    }
    #endregion
}
