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
/// <param name="ServerLayoutNewer">
/// 판 불일치 가운데 <b>서버 판이 이 클라보다 높다</b> — 새 자동 배치 규칙으로 올라갔다 → "클라이언트 갱신 필요"(읽기 전용).
/// <c>false</c> 인 판 불일치는 서버가 옛 판이다(편집 권한이 있으면 여는 순간 한 번 올린다 — <see cref="UnitMapLayoutSync.ShouldBumpLayoutVersion"/>).
/// </param>
public sealed record UnitMapLayoutAssessment(UnitMapLayoutState State, UnitLayoutSnapshot? Snapshot, UnitLayoutFailureKind? Failure = null, bool ServerLayoutNewer = false)
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

/// <summary>배치 알림 한 묶음을 발화 순간에 판정한 결과(TEST-66).</summary>
public enum UnitMapNoticeAction
{
    /// <summary>할 일 없음 — 기다리는 알림이 없거나, 가진 것보다 새롭지 않다(자기 메아리 · 늦게 온 옛 알림).</summary>
    None,

    /// <summary>지금 다시 읽는다(한 번).</summary>
    Refetch,

    /// <summary>손이 바쁘다 — 끝난 뒤 다시 판정한다.</summary>
    Defer,

    /// <summary>30초 넘게 미뤘다 — "다른 운영자가 배치를 바꿨습니다" 를 <b>한 번</b> 알린다(모드는 유지, 계속 미룸).</summary>
    NotifyDeferred,
}

/// <summary>배치 알림 합침 · 발화 시점 재비교 · 연기 상한(FR-53 · ISSUE-9). UI 스레드 전용 상태, 시간은 <c>IClock</c>.</summary>
public sealed class UnitMapLayoutNoticeGate
{
    public static readonly TimeSpan DefaultDeferCap = TimeSpan.FromSeconds(30);

    private readonly Ironwall.Dotnet.Libraries.Base.Services.IClock _clock;
    private readonly TimeSpan _deferCap;
    private DateTime? _deferredSince;
    private bool _notified;

    /// <param name="clock">시각(시험은 가짜 시계 — sleep 없음).</param>
    /// <param name="deferCap">이만큼 넘게 미루면 한 번 알린다. 생략하면 <see cref="DefaultDeferCap"/>(30초).</param>
    public UnitMapLayoutNoticeGate(Ironwall.Dotnet.Libraries.Base.Services.IClock clock, TimeSpan? deferCap = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _deferCap = deferCap ?? DefaultDeferCap;
    }

    /// <summary>받았지만 아직 처리하지 않은 가장 큰 알림 버전(없으면 <c>null</c>).</summary>
    public long? PendingVersion { get; private set; }

    /// <summary>알림 한 건 — 가장 큰 버전만 남긴다(합침 · 버전 건너뜀 허용). 판정은 하지 않는다.</summary>
    public void Notice(long version)
    {
        if (version <= 0) return;
        if (PendingVersion is not long pending || version > pending) PendingVersion = version;
    }

    /// <summary>
    /// <b>발화 순간</b>(합침 창이 끝났을 때 · 손을 놓았을 때 · 쓰기 응답을 반영한 뒤)의 판정 — 그 순간 가진 버전과 견준다.
    /// </summary>
    /// <remarks>
    /// 알림이 도착한 순간이 아니라 이 순간에 견주므로, 내 PATCH 응답보다 먼저 온 자기 메아리도 응답 반영 뒤에는 "새롭지 않음" 이 된다(SIM-N082).
    /// 미루는 동안은 <see cref="PendingVersion"/> 을 쥐고 있고, 처음 미룬 뒤 상한을 넘기면 <see cref="UnitMapNoticeAction.NotifyDeferred"/> 를 한 번만 낸다.
    /// </remarks>
    public UnitMapNoticeAction Evaluate(long? haveVersion, UnitMapBusy busy)
    {
        if (PendingVersion is not long pending) return UnitMapNoticeAction.None;

        if (UnitMapLayoutSync.ShouldRefetch(pending, haveVersion, busy) is var decision && decision != UnitMapRefetchDecision.Defer)
        {
            PendingVersion = null;
            _deferredSince = null;
            _notified = false;
            return decision == UnitMapRefetchDecision.Refetch ? UnitMapNoticeAction.Refetch : UnitMapNoticeAction.None;
        }

        var now = _clock.UtcNow;
        _deferredSince ??= now;
        if (!_notified && now - _deferredSince.Value >= _deferCap)
        {
            _notified = true;
            return UnitMapNoticeAction.NotifyDeferred;
        }
        return UnitMapNoticeAction.Defer;
    }
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

/// <summary>상위 바꾸기가 성공한 뒤 그 부대의 배치를 정리할지(FR-08 · 분석 ISSUE-4).</summary>
public abstract record UnitMapReparentCleanup
{
    private UnitMapReparentCleanup() { }

    /// <summary>할 일 없음 — <paramref name="Reason"/> 은 로그용.</summary>
    public sealed record Skip(UnitMapReparentCleanupReason Reason) : UnitMapReparentCleanup;

    /// <summary>서버 배치에 행이 남아 있다 — 방금 읽은 버전으로 <c>clear</c> 1회.</summary>
    public sealed record Clear(long IfMatchVersion) : UnitMapReparentCleanup;

    /// <summary>세션 전용 — 메모리에서 지운다(서버 · 디스크 0).</summary>
    public sealed record ClearInMemory : UnitMapReparentCleanup;
}

/// <summary>정리를 건너뛴 까닭.</summary>
public enum UnitMapReparentCleanupReason
{
    /// <summary>행이 없다 — 서버가 상위 변경과 같은 트랜잭션에서 이미 지웠거나(S-1 ⑦) 원래 없었다. <b>충돌이 아니다</b>.</summary>
    NoRow,

    /// <summary>쓰기 금지 상태(읽는 중 · 읽기 실패 · 판 불일치) — 모르는 채로 쓰지 않는다.</summary>
    WritesBlocked,

    /// <summary>방금 읽기가 실패했다 — 다음 재조회가 맞춘다.</summary>
    Unreadable,

    /// <summary>경로가 사라졌다 — 세션 전용으로 넘어간다.</summary>
    Unsupported,
}

/// <summary>[배치 초기화] 되돌리기 계획(FR-35 · 분석 ISSUE-50 · 결정 D-2026-09-27-215b6d).</summary>
/// <param name="Change">보낼 변경 — 되살릴 부대가 없거나 막혔으면 <c>null</c>.</param>
/// <param name="IfMatchVersion">방금 읽은 문서 버전.</param>
/// <param name="Restored">되살릴 부대.</param>
/// <param name="SkippedDeleted">그 사이 편제에서 사라진 부대 — 보내면 서버가 422 로 <b>전체</b>를 되돌린다.</param>
/// <param name="SkippedChanged">그 사이 다른 운영자가 배치를 준 부대 — 남의 변경을 덮지 않는다.</param>
/// <param name="IsTooLarge">항목이 일괄 쓰기 상한을 넘는다 — 나눠 보내지 않고 되돌리기를 막는다.</param>
/// <param name="LayoutVersionChanged">문서의 판이 이 클라와 다르다 — 되돌리지 않는다.</param>
public sealed record UnitMapResetUndoPlan(
    UnitLayoutChange? Change,
    long IfMatchVersion,
    IReadOnlyList<int> Restored,
    IReadOnlyList<int> SkippedDeleted,
    IReadOnlyList<int> SkippedChanged,
    bool IsTooLarge,
    bool LayoutVersionChanged)
{
    /// <summary>막대 문구용 — 되살리지 않은 부대 수.</summary>
    public int SkippedCount => SkippedDeleted.Count + SkippedChanged.Count;
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
                => new UnitMapLayoutAssessment(UnitMapLayoutState.VersionMismatch, s.Snapshot,
                                               ServerLayoutNewer: s.Snapshot.LayoutVersion > clientLayoutVersion),
            UnitLayoutRead.Supported s => new UnitMapLayoutAssessment(UnitMapLayoutState.Shared, s.Snapshot),
            UnitLayoutRead.Unsupported => new UnitMapLayoutAssessment(UnitMapLayoutState.SessionOnly, null),
            UnitLayoutRead.Failed f => new UnitMapLayoutAssessment(UnitMapLayoutState.ReadFailed, null, f.Kind),
            _ => new UnitMapLayoutAssessment(UnitMapLayoutState.ReadFailed, null, UnitLayoutFailureKind.Other),
        };
    }
    #endregion

    #region - 판 올림 (서버 v8.0.4 · 회신 2026-09-29 §2 · §4.5) -
    /// <summary>
    /// 읽은 문서를 이 클라의 판으로 <b>한 번</b> 올려야 하는가 — 서버 문서가 옛 판이고(<c>layout_version</c> &lt; 클라) 편집 권한이 있을 때만.
    /// </summary>
    /// <remarks>
    /// 올림 = <c>clear_all:true</c> + 더 큰 <c>layout_version</c> + <c>If-Match</c>(<see cref="UnitLayoutChange.LayoutVersionBump"/>). 옛 판의 Δ 는
    /// 새 자동 배치 규칙에서 의미가 없어 지운다. 서버 판이 더 높으면 올리지 않는다(내리기는 없다 — 클라이언트 갱신 필요).
    /// 보기 권한만 있으면 올리지 않고 읽기 전용으로 둔다(권한 있는 운영자가 열면 올라간다).
    /// </remarks>
    public static bool ShouldBumpLayoutVersion(UnitLayoutRead read, bool canEdit, int clientLayoutVersion = UnitMapLayout.LayoutVersion)
        => canEdit
        && read is UnitLayoutRead.Supported { Snapshot: var doc }
        && doc.LayoutVersion < clientLayoutVersion;
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

    #region - 상위 변경 뒤 정리 (FR-08) -
    /// <summary>
    /// 상위 바꾸기가 성공한 뒤 — 그 부대의 Δ 를 지울지 정한다(FR-08 · ISSUE-4).
    /// </summary>
    /// <param name="mode">지금 배치 상태.</param>
    /// <param name="latest">상위 변경 <b>뒤에</b> 새로 읽은 문서(공유 모드). 세션 전용이면 무시.</param>
    /// <param name="session">세션 전용 저장소의 지금 문서(세션 전용일 때만).</param>
    /// <param name="unitId">상위가 바뀐 부대.</param>
    /// <remarks>
    /// <para>옛 버전으로 <c>clear</c> 를 보내지 않는다 — 서버가 S-1 ⑦ 로 같은 트랜잭션에서 행을 지우고 버전을 올렸다면 옛 If-Match 는 412 가 되고,
    /// FR-52 가 그것을 "다른 운영자가 바꿨습니다" 로 <b>오보</b>한다. 그래서 먼저 읽고: 행이 없으면 무동작(충돌 아님), 있으면 <b>방금 읽은 버전</b>으로 지운다.</para>
    /// <para>그 <c>clear</c> 가 또 412 면 부르는 쪽이 다시 읽어 이 함수를 한 번 더 부른다(그래도 412 면 멈춘다) — 충돌 막대를 띄우지 않는다.</para>
    /// </remarks>
    public static UnitMapReparentCleanup PlanReparentCleanup(UnitMapLayoutState mode, UnitLayoutRead? latest, UnitLayoutSnapshot? session, int unitId)
    {
        switch (mode)
        {
            case UnitMapLayoutState.SessionOnly:
                return session?.DeltaOf(unitId) is null
                    ? new UnitMapReparentCleanup.Skip(UnitMapReparentCleanupReason.NoRow)
                    : new UnitMapReparentCleanup.ClearInMemory();

            case UnitMapLayoutState.Shared:
                return latest switch
                {
                    UnitLayoutRead.Supported { Snapshot: var now } when now.DeltaOf(unitId) is null
                        => new UnitMapReparentCleanup.Skip(UnitMapReparentCleanupReason.NoRow),     // 서버가 이미 지웠다 — 충돌 아님
                    UnitLayoutRead.Supported { Snapshot: var now }
                        => new UnitMapReparentCleanup.Clear(now.Version),                           // 방금 읽은 버전으로
                    UnitLayoutRead.Unsupported => new UnitMapReparentCleanup.Skip(UnitMapReparentCleanupReason.Unsupported),
                    _ => new UnitMapReparentCleanup.Skip(UnitMapReparentCleanupReason.Unreadable),
                };

            default:
                return new UnitMapReparentCleanup.Skip(UnitMapReparentCleanupReason.WritesBlocked);
        }
    }

    /// <summary>
    /// <see cref="PlanReparentCleanup"/> 의 공유 모드 전용 짧은 이름(plan v1.3 TEST-64 ④) — 상위 변경 <b>뒤에</b> 읽은 문서만 본다.
    /// </summary>
    public static UnitMapReparentCleanup PlanParentChangeClear(UnitLayoutSnapshot latestDoc, int unitId)
    {
        ArgumentNullException.ThrowIfNull(latestDoc);
        return PlanReparentCleanup(UnitMapLayoutState.Shared, new UnitLayoutRead.Supported(latestDoc), null, unitId);
    }
    #endregion

    #region - 배치 초기화 되돌리기 (FR-35 · ISSUE-50) -
    /// <summary>한 번의 일괄 쓰기 상한(S-1 ③).</summary>
    public const int MaxBatchItems = 1000;

    /// <summary>
    /// [배치 초기화] 를 되돌린다 — <b>지금 편제에 있고</b>, 초기화 뒤로 <b>아무도 손대지 않은</b> 부대만 되살린다(결정 D-2026-09-27-215b6d = Q-11 ⓐ).
    /// </summary>
    /// <param name="entry">되돌리기 표의 초기화 항목(초기화 전 Δ · 초기화 직후 문서).</param>
    /// <param name="latest">지금 문서(공유 = 방금 읽은 것, 세션 전용 = 저장소).</param>
    /// <param name="tree">지금 편제.</param>
    public static UnitMapResetUndoPlan PlanResetUndo(UnitMapLayoutResetUndo entry, UnitLayoutSnapshot latest, UnitTreeModel tree,
                                                     int clientLayoutVersion = UnitMapLayout.LayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(tree);

        var none = Array.Empty<int>();
        if (latest.LayoutVersion != clientLayoutVersion)
            return new UnitMapResetUndoPlan(null, latest.Version, none, none, none, IsTooLarge: false, LayoutVersionChanged: true);
        if (entry.Before.Count > MaxBatchItems)
            return new UnitMapResetUndoPlan(null, latest.Version, none, none, none, IsTooLarge: true, LayoutVersionChanged: false);

        var restored = new List<int>();
        var deleted = new List<int>();
        var changed = new List<int>();
        var set = new Dictionary<int, Vector>();
        foreach (var (unitId, delta) in entry.Before.OrderBy(kv => kv.Key))
        {
            if (tree.Find(unitId) is null) { deleted.Add(unitId); continue; }          // 없는 unit_id 하나면 서버가 422 로 전체를 되돌린다

            // 초기화 직후의 값(공유 = 저장 직후 문서, 세션 전용 = 비었다)과 지금 값이 다르면 그 사이 누가 손댔다 — 덮지 않는다.
            var afterReset = entry.Saved?.DeltaOf(unitId);
            if (!SameDelta(afterReset, latest.DeltaOf(unitId))) { changed.Add(unitId); continue; }

            restored.Add(unitId);
            set[unitId] = delta;
        }

        var change = set.Count == 0 ? null : new UnitLayoutChange(set, Array.Empty<int>(), ClearAll: false);
        return new UnitMapResetUndoPlan(change, latest.Version, restored, deleted, changed, IsTooLarge: false, LayoutVersionChanged: false);
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
