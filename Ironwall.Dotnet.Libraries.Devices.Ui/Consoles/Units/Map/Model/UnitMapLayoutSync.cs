using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 공유 배치 동기화 판정 — 지원 판정 · 412 병합 · 알림 재조회 · 되돌리기 허용 (FR-35 · FR-50 · FR-52 · FR-53 · NFR-15)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>창을 열 때의 읽기 결과를 관계도가 쓸 상태로 옮긴 것(FR-50 · FR-07 · FR-51).</summary>
/// <param name="State">배치 상태(레인 A 의 <see cref="UnitMapLayoutState"/> — 문구 FR-11 과 드롭 판정이 함께 쓴다).</param>
/// <param name="Snapshot">서버 문서(<see cref="UnitMapLayoutState.Shared"/> · <see cref="UnitMapLayoutState.VersionMismatch"/> 일 때만).</param>
/// <param name="Failure">읽기 실패의 종류(<see cref="UnitMapLayoutState.ReadFailed"/> 일 때만).</param>
public sealed record UnitMapLayoutAssessment(UnitMapLayoutState State, UnitLayoutSnapshot? Snapshot, UnitLayoutFailureKind? Failure = null)
{
    /// <summary>창을 막 열었다 — 자동 배치로 먼저 그리고(NFR-01) 응답을 기다린다.</summary>
    public static UnitMapLayoutAssessment Loading { get; } = new(UnitMapLayoutState.Loading, null);

    /// <summary>위치 쓰기(서버 또는 메모리)를 받을 수 있는가 — 권한(<c>units:edit</c>/<c>units:view</c>)은 따로 본다(FR-05).</summary>
    public bool CanWrite => State is UnitMapLayoutState.Shared or UnitMapLayoutState.SessionOnly;

    /// <summary>Δ 를 입혀 그리는가 — 판이 다르거나 읽지 못했으면 자동 배치 그대로(FR-07).</summary>
    public bool AppliesDeltas => State is UnitMapLayoutState.Shared or UnitMapLayoutState.SessionOnly;
}

/// <summary>알림 반영을 미뤄야 하는 손 · 화면 상태(FR-53 · SIM-N079~N082).</summary>
[Flags]
public enum UnitMapBusy
{
    None = 0,

    /// <summary>노드를 끄는 중(데드존을 넘은 뒤).</summary>
    Dragging = 1,

    /// <summary><c>M</c> 위치 이동 모드.</summary>
    MoveMode = 2,

    /// <summary>상위 · 인접 확인 오버레이가 떠 있다.</summary>
    Confirming = 4,

    /// <summary>배치 쓰기 응답을 기다린다 — 자기 메아리가 응답보다 먼저 온다(V-11 실측 ~5 ms).</summary>
    WriteInFlight = 8,
}

/// <summary>배치 알림 한 건에 대한 판정.</summary>
public enum UnitMapRefetchDecision
{
    /// <summary>가진 것보다 새롭지 않다(자기 메아리 · 늦게 온 옛 알림) — 버린다.</summary>
    Skip,

    /// <summary>지금 다시 읽는다(합침 창 500 ms 는 부르는 쪽).</summary>
    Refetch,

    /// <summary>손이 바쁘다 — 끝난 뒤 이 판정을 다시 한다(그때 가진 버전과 견준다).</summary>
    Defer,
}

/// <summary>충돌 · 되돌리기 거절의 까닭(막대 문구 · 로그).</summary>
public enum UnitMapConflictReason
{
    /// <summary>내가 건드린 부대(또는 그 조상)의 Δ 가 그 사이 바뀌었다.</summary>
    TouchedUnitChanged,

    /// <summary>그 사이 누가 전체 배치를 초기화했다.</summary>
    ClearedAll,

    /// <summary>그 사이 편제가 바뀌었다(상위 변경 · 삭제 — Δ 의 기준이 달라졌다).</summary>
    TopologyChanged,

    /// <summary>서버 문서의 <c>layout_version</c> 이 이 클라와 다르다.</summary>
    LayoutVersionChanged,

    /// <summary>재전송도 412 — 더 시도하지 않는다(무한 루프 금지).</summary>
    RetryAlsoConflicted,

    /// <summary>최신 문서를 읽지 못했다.</summary>
    Unreadable,
}

/// <summary>412 뒤 판정(FR-52).</summary>
public abstract record UnitMapConflictResolution
{
    private UnitMapConflictResolution() { }

    /// <summary>내 부대는 그대로다 — <paramref name="IfMatchVersion"/> 로 <b>한 번</b> 다시 보낸다. 다음 판정의 기준은 <paramref name="Latest"/>.</summary>
    public sealed record Retry(long IfMatchVersion, UnitLayoutSnapshot Latest) : UnitMapConflictResolution;

    /// <summary>적용하지 않는다 — <paramref name="Latest"/>(있으면)를 그대로 그리고 막대에 까닭을 적는다.</summary>
    public sealed record Abandon(UnitMapConflictReason Reason, IReadOnlyList<int> ChangedUnitIds, UnitLayoutSnapshot? Latest) : UnitMapConflictResolution;
}

/// <summary>되돌리기 허용 판정(FR-35).</summary>
public abstract record UnitMapUndoCheck
{
    private UnitMapUndoCheck() { }

    /// <summary>되돌려도 된다 — 반대 쓰기의 <c>If-Match</c> 는 <paramref name="IfMatchVersion"/>.</summary>
    public sealed record Allowed(long IfMatchVersion) : UnitMapUndoCheck;

    /// <summary>남의 변경을 되돌리기로 지우게 된다 — 되돌리지 않고 알린다.</summary>
    public sealed record Refused(UnitMapConflictReason Reason, IReadOnlyList<int> ChangedUnitIds) : UnitMapUndoCheck;
}

/// <summary>
/// 공유 배치 동기화의 <b>순수 판정</b> — I/O 없음 · 스레드 무관(NFR-11).
/// </summary>
/// <remarks>
/// <para><b>"건드린 부대"</b> = 끈 부대 + 그 조상(<see cref="TouchedUnits"/>). Δ 는 예하로 전파되므로 조상의 Δ 가 바뀌면
/// 내 부대의 화면 위치도 바뀐다 — 조상 변경은 충돌이다. 예하의 변경은 내 위치에 영향이 없어 충돌이 아니다(SIM-Q013).</para>
/// <para>전체 초기화처럼 문서 전체를 건드리는 쓰기는 <c>touched = null</c>(= 문서 전체)로 판정한다.</para>
/// <para>값 비교는 Δ 의 <b>값</b>이다(같은 값을 다시 쓴 것은 변경이 아니다 — SIM-Q037). 버전만 오른 문서(부대 삭제 CASCADE 등)는
/// 건드린 부대가 그대로면 재전송한다.</para>
/// </remarks>
public static class UnitMapLayoutSync
{
    /// <summary>Δ 값이 같다고 볼 오차(월드 단위). 서버 왕복의 부동소수 표현 차이만 흡수한다.</summary>
    public const double DeltaTolerance = 1e-6;

    #region - 지원 판정 -
    /// <summary>창을 열 때의 읽기 → 배치 상태(FR-50). 판본 번호를 비교하지 않는다 — 경로의 실존으로 판정된 결과만 본다.</summary>
    public static UnitMapLayoutAssessment Classify(UnitLayoutRead read, int clientLayoutVersion = UnitMapLayout.LayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(read);
        return read switch
        {
            UnitLayoutRead.Supported s when s.Snapshot.LayoutVersion != clientLayoutVersion
                => new UnitMapLayoutAssessment(UnitMapLayoutState.VersionMismatch, s.Snapshot),
            UnitLayoutRead.Supported s => new UnitMapLayoutAssessment(UnitMapLayoutState.Shared, s.Snapshot),
            UnitLayoutRead.Unsupported => new UnitMapLayoutAssessment(UnitMapLayoutState.SessionOnly, null),
            UnitLayoutRead.Failed f => new UnitMapLayoutAssessment(UnitMapLayoutState.ReadFailed, null, f.Kind),
            _ => new UnitMapLayoutAssessment(UnitMapLayoutState.ReadFailed, null, UnitLayoutFailureKind.Other),
        };
    }
    #endregion

    #region - 쓰기 모양 -
    /// <summary>끈 부대 + 조상(가까운 순). 편제에 없는 id 면 자신만. 순환 방어(편제 크기만큼만 오른다).</summary>
    public static IReadOnlyList<int> TouchedUnits(UnitTreeModel tree, int unitId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var result = new List<int> { unitId };
        var cursor = tree.Find(unitId);
        var guard = 0;
        while (cursor?.ParentId is int parentId && guard++ < tree.Count)
        {
            if (result.Contains(parentId)) break;
            result.Add(parentId);
            cursor = tree.Find(parentId);
        }
        return result;
    }

    /// <summary>위치 한 번 = 항목 하나(끈 부대). 예하는 Δ 전파로 따라온다 — 예하를 싣지 않는다(FR-10).</summary>
    public static UnitLayoutChange MoveChange(int unitId, Vector newDelta) => UnitLayoutChange.SetOne(unitId, newDelta);

    /// <summary>위치 되돌리기 = 옮기기 전 Δ 를 다시 <c>set</c>, 원래 없었으면 <c>clear</c>(FR-35).</summary>
    public static UnitLayoutChange ReverseChange(int unitId, Vector? previousDelta)
        => previousDelta is Vector before ? UnitLayoutChange.SetOne(unitId, before) : UnitLayoutChange.ClearOne(unitId);
    #endregion

    #region - 412 병합 -
    /// <summary>
    /// 412 뒤 최신 문서를 받고 판정한다(FR-52) — 건드린 부대가 그대로면 <b>한 번</b> 재전송, 아니면 적용하지 않는다.
    /// </summary>
    /// <param name="before">내 조작이 기준으로 삼은 문서(처음 읽은 것 · 재전송이면 직전 최신).</param>
    /// <param name="latest">412 뒤 다시 읽은 결과.</param>
    /// <param name="touched">건드린 부대(<see cref="TouchedUnits"/>). <c>null</c> = 문서 전체(전체 초기화 · 되돌리기).</param>
    /// <param name="isRetry">이미 한 번 재전송했다 — 또 412 면 멈춘다(SIM-Q041).</param>
    /// <param name="topologyChanged">그 사이 편제 알림(<c>SYNC_UNIT</c>)을 받았다 — Δ 의 기준이 흔들렸을 수 있다(SIM-Q029).</param>
    /// <param name="clientLayoutVersion">이 클라의 자동 배치 판.</param>
    public static UnitMapConflictResolution Resolve(
        UnitLayoutSnapshot before,
        UnitLayoutRead latest,
        IReadOnlyCollection<int>? touched,
        bool isRetry,
        bool topologyChanged = false,
        int clientLayoutVersion = UnitMapLayout.LayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(latest);

        if (latest is not UnitLayoutRead.Supported { Snapshot: var now })
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.Unreadable, Array.Empty<int>(), null);

        if (now.LayoutVersion != clientLayoutVersion)
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.LayoutVersionChanged, Array.Empty<int>(), now);

        if (isRetry)
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.RetryAlsoConflicted, ChangedUnits(before, now, touched), now);

        if (topologyChanged)
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.TopologyChanged, ChangedUnits(before, now, touched), now);

        var changed = ChangedUnits(before, now, touched);
        if (changed.Count > 0)
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.TouchedUnitChanged, changed, now);

        if (WasClearedAll(before, now))
            return new UnitMapConflictResolution.Abandon(UnitMapConflictReason.ClearedAll, ChangedUnits(before, now, null), now);

        return new UnitMapConflictResolution.Retry(now.Version, now);
    }
    #endregion

    #region - 배치 알림 -
    /// <summary>
    /// <c>SYNC_UNIT_LAYOUT {version}</c> 한 건(FR-53). 가진 것보다 새롭지 않으면 버리고, 손이 바쁘면 미룬다.
    /// </summary>
    /// <param name="noticeVersion">알림이 실은 문서 버전.</param>
    /// <param name="haveVersion">가진 문서 버전(아직 못 읽었으면 <c>null</c> — 새 알림이면 읽는다).</param>
    /// <param name="busy">지금 손 · 화면 상태.</param>
    /// <remarks>
    /// 자기 쓰기의 메아리가 응답보다 먼저 온다(V-11) — 그래서 <see cref="UnitMapBusy.WriteInFlight"/> 도 미룸이다.
    /// 응답을 반영한 뒤 다시 판정하면 버전이 같아 <see cref="UnitMapRefetchDecision.Skip"/> 이 된다(SIM-N079).
    /// </remarks>
    public static UnitMapRefetchDecision ShouldRefetch(long noticeVersion, long? haveVersion, UnitMapBusy busy)
    {
        if (haveVersion is long have && noticeVersion <= have) return UnitMapRefetchDecision.Skip;
        return busy == UnitMapBusy.None ? UnitMapRefetchDecision.Refetch : UnitMapRefetchDecision.Defer;
    }
    #endregion

    #region - 되돌리기 -
    /// <summary>
    /// 되돌려도 되는가(FR-35) — 내 쓰기 직후 문서(<paramref name="expected"/>)와 지금 문서를 건드린 부대에서 견준다.
    /// 규칙은 <see cref="Resolve"/> 와 같다(조상 포함 · 전체 초기화 = 거절 — SIM-Q043 · Q045).
    /// </summary>
    public static UnitMapUndoCheck CanUndo(
        UnitLayoutSnapshot expected,
        UnitLayoutSnapshot latest,
        IReadOnlyCollection<int>? touched,
        int clientLayoutVersion = UnitMapLayout.LayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(latest);

        if (latest.LayoutVersion != clientLayoutVersion)
            return new UnitMapUndoCheck.Refused(UnitMapConflictReason.LayoutVersionChanged, Array.Empty<int>());

        var changed = ChangedUnits(expected, latest, touched);
        if (changed.Count > 0)
            return new UnitMapUndoCheck.Refused(UnitMapConflictReason.TouchedUnitChanged, changed);

        if (WasClearedAll(expected, latest))
            return new UnitMapUndoCheck.Refused(UnitMapConflictReason.ClearedAll, ChangedUnits(expected, latest, null));

        return new UnitMapUndoCheck.Allowed(latest.Version);
    }
    #endregion

    #region - 비교 -
    /// <summary>두 문서 사이에 Δ 가 바뀐 부대(값 비교, 있음/없음 포함). <paramref name="touched"/> 가 <c>null</c> 이면 문서 전체.</summary>
    internal static IReadOnlyList<int> ChangedUnits(UnitLayoutSnapshot before, UnitLayoutSnapshot after, IReadOnlyCollection<int>? touched)
    {
        IEnumerable<int> ids = touched ?? (IEnumerable<int>)before.Deltas.Keys.Union(after.Deltas.Keys);
        var changed = new List<int>();
        foreach (var id in ids.Distinct().OrderBy(i => i))
        {
            if (!SameDelta(before.DeltaOf(id), after.DeltaOf(id))) changed.Add(id);
        }
        return changed;
    }

    /// <summary>Δ 가 있던 문서가 통째로 비었다 — 전체 초기화로 본다(남의 초기화를 되살리지 않는다).</summary>
    private static bool WasClearedAll(UnitLayoutSnapshot before, UnitLayoutSnapshot after)
        => before.Deltas.Count > 0 && after.Deltas.Count == 0;

    private static bool SameDelta(Vector? a, Vector? b)
        => (a, b) switch
        {
            (null, null) => true,
            (Vector x, Vector y) => Math.Abs(x.X - y.X) <= DeltaTolerance && Math.Abs(x.Y - y.Y) <= DeltaTolerance,
            _ => false,
        };
    #endregion
}
