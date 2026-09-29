using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 뷰모델 — 공유 배치 수명(지원 판정 · 세션 전용 · 412 병합 · 알림 · 초기화) (FR-07 · 09~11 · 50~53)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public sealed partial class UnitMapViewModel
{
    private readonly List<UnitLayoutChange> _optimistic = new();
    private readonly CoalescingTrigger _noticeTrigger;
    private UnitMapLayoutAssessment _assessment = UnitMapLayoutAssessment.Loading;
    private UnitLayoutSnapshot _snapshot = UnitLayoutSnapshot.Empty();
    private SessionOnlyUnitLayoutStore? _session;

    /// <summary>
    /// 열린 뒤 다시 읽기가 실패했다 — 마지막으로 읽은 문서(<see cref="_snapshot"/>)를 그대로 그리고 [다시 시도] 를 띄운다(REVIEW-01 MEDIUM-7).
    /// 상태는 <see cref="UnitMapLayoutState.Shared"/> 그대로라 쓰기는 <c>If-Match</c>(마지막 버전)로 나가고, 그 사이 바뀌었으면 412 가 지킨다.
    /// </summary>
    private bool _layoutStale;
    private UnitLayoutFailureKind? _staleFailure;

    /// <summary>한 번의 배치 쓰기가 끝난 모양.</summary>
    private enum CommitKind { Saved, SessionSaved, Conflicted, Failed, Unknown }

    private sealed record LayoutCommit(CommitKind Kind, UnitLayoutSnapshot? Snapshot = null, string? Reason = null);

    #region - 상태 · 문구 (FR-11) -
    /// <summary>공유 배치의 지금 상태.</summary>
    public UnitMapLayoutState LayoutState => _assessment.State;

    /// <summary>캔버스 위 가운데 배치 문구(<c>Units.Map.LayoutStatus</c>).</summary>
    public string LayoutStatusText
    {
        get
        {
            var byMe = _options.CurrentOperatorName is { Length: > 0 } me && string.Equals(me, _snapshot.UpdatedByName, StringComparison.Ordinal);
            return UnitMapText.LayoutStatus(LayoutState, _commands.CanEdit, _snapshot.UpdatedByName, _snapshot.UpdatedAt?.LocalDateTime, byMe,
                                            liveOff: _options.IsLiveOff?.Invoke() == true,
                                            sessionExpired: (_layoutStale ? _staleFailure : _assessment.Failure) == UnitLayoutFailureKind.Unauthorized,
                                            stale: _layoutStale);
        }
    }

    /// <summary>[다시 시도](<c>Units.Map.LayoutRetry</c>) — 읽기 실패일 때만(처음부터 못 읽었거나, 열린 뒤 다시 읽기가 실패해 옛 배치를 보이는 중).</summary>
    public bool CanRetryLayout => LayoutState == UnitMapLayoutState.ReadFailed || _layoutStale;

    private long? HaveVersion => LayoutState is UnitMapLayoutState.Shared or UnitMapLayoutState.VersionMismatch ? _snapshot.Version : null;

    private void NotifyLayout()
    {
        NotifyOfPropertyChange(nameof(LayoutState));
        NotifyOfPropertyChange(nameof(LayoutStatusText));
        NotifyOfPropertyChange(nameof(CanRetryLayout));
        NotifyResetState();
    }
    #endregion

    #region - 열기 · 다시 읽기 (FR-50 · NFR-01) -
    /// <summary>
    /// 레일을 열었다 — 자동 배치로 <b>먼저</b> 그리고(GET 을 기다리지 않는다 — NFR-01) 배치 문서를 1회 읽는다(지원 판정 겸).
    /// </summary>
    public Task OpenAsync(CancellationToken token = default)
    {
        _assessment = UnitMapLayoutAssessment.Loading;
        RebuildScene();
        NotifyLayout();
        return Serialize(() => ReloadLayoutAsync(token));
    }

    /// <summary>[다시 시도] — GET 1회.</summary>
    public Task RetryLayoutAsync() => Serialize(() => ReloadLayoutAsync(CancellationToken.None));

    private async Task ReloadLayoutAsync(CancellationToken token)
    {
        var read = await _api.ReadAsync(token).ConfigureAwait(true);
        ApplyRead(read);
    }

    /// <summary>
    /// 읽은 결과를 상태로 옮긴다. 세션 전용은 한번 정해지면 이 창 동안 유지한다(FR-51 — 세션 위치를 서버로 올리지 않는다).
    /// 읽기 실패로 자동 배치에 떨어지는 것은 <b>아직 한 번도 읽지 못했을 때</b>(창을 열 때)뿐이다 — 이미 읽은 문서가 있으면 그것을 지키고
    /// 낡았다고 표시한다(REVIEW-01 MEDIUM-7 — 종전엔 알림 뒤 GET 한 번 실패로 모든 운영자의 배치가 화면에서 사라졌다).
    /// </summary>
    private void ApplyRead(UnitLayoutRead read)
    {
        if (LayoutState == UnitMapLayoutState.SessionOnly) return;

        var assessment = UnitMapLayoutSync.Classify(read);
        switch (assessment.State)
        {
            case UnitMapLayoutState.Shared:
            case UnitMapLayoutState.VersionMismatch:
                _snapshot = assessment.Snapshot!;
                _assessment = assessment;
                _layoutStale = false;
                _staleFailure = null;
                break;
            case UnitMapLayoutState.SessionOnly:
                SwitchToSessionOnly();
                _layoutStale = false;
                break;
            case UnitMapLayoutState.ReadFailed when LayoutState == UnitMapLayoutState.Shared:
                _layoutStale = true;                     // 마지막으로 읽은 문서를 그대로 — 상태 · Δ 유지
                _staleFailure = assessment.Failure;
                break;
            default:
                _assessment = assessment;
                break;
        }
        RebuildScene();
        NotifyLayout();
    }

    private void ApplySnapshot(UnitLayoutSnapshot snapshot) => ApplyRead(new UnitLayoutRead.Supported(snapshot));

    private void SwitchToSessionOnly()
    {
        _session ??= new SessionOnlyUnitLayoutStore();
        _snapshot = _session.Snapshot;
        _assessment = new UnitMapLayoutAssessment(UnitMapLayoutState.SessionOnly, null);
    }
    #endregion

    #region - 위치 쓰기 (FR-31 · FR-34 · FR-52) -
    /// <summary>위치 한 번 — 먼저 그리고 대기열에서 PATCH 1회(끈 부대 한 항목). 세션 전용이면 메모리만.</summary>
    /// <remarks>
    /// <b>놓을 때 본 자리</b>(끈 부대 + 조상의 Δ — 앞선 대기 쓰기의 낙관 변경 포함)를 적어 둔다. 차례가 왔을 때 문서가 그 자리와 다르면
    /// (같은 부대의 앞선 쓰기가 412 · 실패로 닿지 못했거나, 그 사이 다른 운영자가 바꿨다) 보내지 않는다 — 운영자가 보지 못한 문서 위에
    /// 새 버전으로 덮어쓰는 길을 막는다(REVIEW-01 MEDIUM-2). 되돌리기 항목도 만들지 않고, 앞선 충돌 · 실패 막대를 그대로 둔다.
    /// </remarks>
    private Task WritePositionAsync(int unitId, Vector newDelta)
    {
        newDelta = ClampDelta(newDelta);
        var shown = DisplayDeltas();
        var before = shown.TryGetValue(unitId, out var own) ? own : (Vector?)null;
        var change = UnitMapLayoutSync.MoveChange(unitId, newDelta);
        var name = NameOf(unitId);
        var touched = UnitMapLayoutSync.TouchedUnits(_tree, unitId);
        var seen = touched.ToDictionary(id => id, id => shown.TryGetValue(id, out var d) ? d : (Vector?)null);

        _optimistic.Add(change);
        RebuildScene();

        return Serialize(async () =>
        {
            try
            {
                if (!StillAsSeen(seen))
                {
                    if (!_barIsError) ShowBar(UnitMapText.LayoutConflictBar(name), isError: true);   // 앞선 충돌 · 실패 막대가 있으면 그것을 남긴다
                    return;
                }
                var commit = await CommitAsync(change, touched).ConfigureAwait(true);
                switch (commit.Kind)
                {
                    case CommitKind.Saved:
                        _undo.Push(new UnitMapPositionUndo(unitId, before, newDelta, commit.Snapshot, touched, SessionOnly: false));
                        ShowBar(UnitMapText.PositionMovedBar(name, sessionOnly: false), isError: false);
                        break;
                    case CommitKind.SessionSaved:
                        _undo.Push(new UnitMapPositionUndo(unitId, before, newDelta, null, touched, SessionOnly: true));
                        ShowBar(UnitMapText.PositionMovedBar(name, sessionOnly: true), isError: false);
                        break;
                    case CommitKind.Conflicted:
                        ShowBar(UnitMapText.LayoutConflictBar(name), isError: true);
                        break;
                    case CommitKind.Unknown:
                        ShowBar(UnitMapText.LayoutWriteUnknownBar(name), isError: true);
                        break;
                    default:
                        ShowBar(UnitMapText.LayoutWriteFailedBar(name, commit.Reason ?? string.Empty), isError: true);
                        break;
                }
            }
            finally
            {
                _optimistic.Remove(change);
                RebuildScene();
            }
        }, name);
    }

    /// <summary>
    /// 지금 문서(세션 전용이면 세션 문서)가 놓을 때 본 자리와 같은가 — 끈 부대 + 조상의 Δ 값 비교(REVIEW-01 MEDIUM-2).
    /// 앞선 내 쓰기가 성공했으면 그 결과가 곧 본 자리였으므로 같다.
    /// </summary>
    private bool StillAsSeen(IReadOnlyDictionary<int, Vector?> seen)
    {
        foreach (var (id, expected) in seen)
        {
            var now = _snapshot.DeltaOf(id);
            var same = (expected, now) switch
            {
                (null, null) => true,
                (Vector a, Vector b) => Math.Abs(a.X - b.X) <= UnitMapLayoutSync.DeltaTolerance && Math.Abs(a.Y - b.Y) <= UnitMapLayoutSync.DeltaTolerance,
                _ => false,
            };
            if (!same) return false;
        }
        return true;
    }

    /// <summary>
    /// 배치 쓰기 한 건 — <c>If-Match</c> 를 싣고, 412 면 최신을 읽어 <see cref="UnitMapLayoutSync.Resolve"/> 로 한 번만 재전송하거나 멈춘다.
    /// 말없이 덮어쓰는 경로가 없다(NFR-15). 실패하면 서버 배치를 다시 읽어 그대로 그린다(FR-34). 대기열 안에서만 부른다.
    /// </summary>
    /// <param name="touched">건드린 부대(끈 부대 + 조상). <c>null</c> = 문서 전체(전체 초기화).</param>
    /// <param name="ifMatch">되돌리기처럼 판정 버전을 정해 두었으면 그 값, 아니면 지금 가진 버전.</param>
    private async Task<LayoutCommit> CommitAsync(UnitLayoutChange change, IReadOnlyCollection<int>? touched, long? ifMatch = null)
    {
        if (LayoutState == UnitMapLayoutState.SessionOnly)
        {
            _session!.Apply(change);
            _snapshot = _session.Snapshot;
            return new LayoutCommit(CommitKind.SessionSaved, _snapshot);
        }
        if (LayoutState != UnitMapLayoutState.Shared)
            return new LayoutCommit(CommitKind.Failed, Reason: UnitMapText.LayoutReadFailedBlocked);

        var basis = _snapshot;
        var version = ifMatch ?? basis.Version;
        var generation = _treeGeneration;
        var isRetry = false;
        while (true)
        {
            var result = await _api.WriteAsync(version, change).ConfigureAwait(true);
            switch (result)
            {
                case UnitLayoutWrite.Saved saved:
                    ApplySnapshot(saved.Snapshot);
                    return new LayoutCommit(CommitKind.Saved, saved.Snapshot);

                case UnitLayoutWrite.Conflict:
                    var latest = await _api.ReadAsync().ConfigureAwait(true);
                    var resolution = UnitMapLayoutSync.Resolve(basis, latest, touched, isRetry, generation != _treeGeneration);
                    if (resolution is UnitMapConflictResolution.Retry retry)
                    {
                        ApplySnapshot(retry.Latest);
                        basis = retry.Latest;
                        version = retry.IfMatchVersion;
                        isRetry = true;
                        continue;
                    }
                    if (resolution is UnitMapConflictResolution.Abandon { Latest: { } now }) ApplySnapshot(now);
                    else ApplyRead(latest);
                    return new LayoutCommit(CommitKind.Conflicted);

                case UnitLayoutWrite.Unsupported:
                    // 지원 중 경로가 사라졌다(SIM-F059) — 세션 전용으로 바꾸고 이 변경은 메모리에.
                    SwitchToSessionOnly();
                    _session!.Apply(change);
                    _snapshot = _session.Snapshot;
                    NotifyLayout();
                    return new LayoutCommit(CommitKind.SessionSaved, _snapshot);

                case UnitLayoutWrite.Rejected:
                    await ReloadLayoutAsync(CancellationToken.None).ConfigureAwait(true);
                    return new LayoutCommit(CommitKind.Failed, Reason: UnitMapText.LayoutRejectedReason);

                case UnitLayoutWrite.Failed failed:
                    // 시간 초과는 반영됐을 수 있다 — 다시 읽은 서버 상태가 진실이다(ISSUE-6 · SIM-F065).
                    await ReloadLayoutAsync(CancellationToken.None).ConfigureAwait(true);
                    return failed.Kind == UnitLayoutFailureKind.Timeout
                        ? new LayoutCommit(CommitKind.Unknown)
                        : new LayoutCommit(CommitKind.Failed, Reason: UnitMapText.LayoutFailureReason(failed.Kind));

                default:
                    await ReloadLayoutAsync(CancellationToken.None).ConfigureAwait(true);
                    return new LayoutCommit(CommitKind.Failed, Reason: UnitMapText.LayoutFailureReason(UnitLayoutFailureKind.Other));
            }
        }
    }

    /// <summary>위치 되돌리기 — 남이 그 부대(+조상)를 바꿨으면 되돌리지 않는다(FR-35). 반대 Δ PATCH 1회.</summary>
    private async Task UndoPositionAsync(UnitMapPositionUndo entry)
    {
        var name = NameOf(entry.UnitId);
        var reverse = UnitMapLayoutSync.ReverseChange(entry.UnitId, entry.Before);

        if (entry.IsSessionOnly || LayoutState == UnitMapLayoutState.SessionOnly)
        {
            if (LayoutState != UnitMapLayoutState.SessionOnly) { Drop(entry, UnitMapText.UndoRefusedBar(name)); return; }
            _session!.Apply(reverse);
            _snapshot = _session.Snapshot;
            _undo.CompleteUndo(entry, succeeded: true);
            RebuildScene();
            ShowBar(UnitMapText.PositionUndoneBar(name), isError: false);
            return;
        }

        if (entry.Saved is null || UnitMapLayoutSync.CanUndo(entry.Saved, _snapshot, entry.Touched) is not UnitMapUndoCheck.Allowed allowed)
        {
            Drop(entry, UnitMapText.UndoRefusedBar(name));
            return;
        }

        var commit = await CommitAsync(reverse, entry.Touched, allowed.IfMatchVersion).ConfigureAwait(true);
        switch (commit.Kind)
        {
            case CommitKind.Saved:
            case CommitKind.SessionSaved:
                _undo.CompleteUndo(entry, succeeded: true);
                ShowBar(UnitMapText.PositionUndoneBar(name), isError: false);
                break;
            case CommitKind.Conflicted:
                Drop(entry, UnitMapText.UndoRefusedBar(name));
                break;
            case CommitKind.Unknown:
                ShowBar(UnitMapText.LayoutWriteUnknownBar(name), isError: true);
                break;
            default:
                ShowBar(UnitMapText.LayoutWriteFailedBar(name, commit.Reason ?? string.Empty), isError: true);
                break;
        }
        RebuildScene();
    }
    #endregion

    #region - 상위 변경 뒤 정리 (FR-08 · ISSUE-4) -
    /// <summary>
    /// 상위 바꾸기가 성공한 뒤 그 부대의 Δ 를 정리한다 — 먼저 읽고(서버가 같은 트랜잭션에서 이미 지웠으면 무동작 · 충돌 아님),
    /// 행이 남았으면 <b>방금 읽은 버전</b>으로 <c>clear</c> 1회. 그 <c>clear</c> 가 412 면 다시 읽어 한 번 더 — 충돌 막대는 띄우지 않는다.
    /// 세션 전용이면 메모리에서만(레인 B <see cref="UnitMapLayoutSync.PlanReparentCleanup"/>).
    /// </summary>
    private async Task CleanupAfterReparentAsync(int unitId)
    {
        if (LayoutState == UnitMapLayoutState.SessionOnly)
        {
            if (UnitMapLayoutSync.PlanReparentCleanup(LayoutState, null, _session?.Snapshot, unitId) is UnitMapReparentCleanup.ClearInMemory)
            {
                _session!.Apply(UnitLayoutChange.ClearOne(unitId));
                _snapshot = _session.Snapshot;
                RebuildScene();
            }
            return;
        }
        if (LayoutState != UnitMapLayoutState.Shared) return;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var latest = await _api.ReadAsync().ConfigureAwait(true);
            ApplyRead(latest);
            if (UnitMapLayoutSync.PlanReparentCleanup(LayoutState, latest, null, unitId) is not UnitMapReparentCleanup.Clear clear) return;

            var result = await _api.WriteAsync(clear.IfMatchVersion, UnitLayoutChange.ClearOne(unitId)).ConfigureAwait(true);
            if (result is UnitLayoutWrite.Saved saved) { ApplySnapshot(saved.Snapshot); return; }
            if (result is not UnitLayoutWrite.Conflict) return;      // 정리는 다음 재조회가 맞춘다 — 막대를 흔들지 않는다
        }
    }
    #endregion

    #region - 초기화 (FR-09) -
    /// <summary>
    /// 툴바 [배치 초기화] — 확인 오버레이를 연다. 공유 배치는 <c>units:edit</c>, 세션 전용은 누구 화면도 바꾸지 않으므로 <c>units:view</c>(ISSUE-7).
    /// </summary>
    public void RequestResetLayout()
    {
        if (_pending is not null || _moveMode) return;
        var blocked = ResetBlockedReason();
        if (blocked is not null) { StatusText = blocked; return; }
        OpenPending(UnitMapConfirmKind.ResetLayout, null, null,
                    UnitMapText.ConfirmResetLayout(LayoutState == UnitMapLayoutState.SessionOnly, DisplayDeltas().Count));
    }

    /// <summary>상세 [이 부대 배치 초기화] — 확인 없이 <c>clear</c> 1회(되돌리기 가능).</summary>
    public Task ResetNodeLayoutAsync(int unitId)
    {
        var blocked = ResetBlockedReason();
        if (blocked is not null) { StatusText = blocked; return Task.CompletedTask; }

        var name = NameOf(unitId);
        if (DisplayDeltaOf(unitId) is not Vector before) { StatusText = UnitMapText.NodeNotMovedStatus(name); return Task.CompletedTask; }

        var change = UnitLayoutChange.ClearOne(unitId);
        var touched = UnitMapLayoutSync.TouchedUnits(_tree, unitId);
        return Serialize(async () =>
        {
            var commit = await CommitAsync(change, touched).ConfigureAwait(true);
            if (commit.Kind is CommitKind.Saved or CommitKind.SessionSaved)
            {
                _undo.Push(new UnitMapLayoutResetUndo(new Dictionary<int, Vector> { [unitId] = before }, All: false, commit.Snapshot, commit.Kind == CommitKind.SessionSaved));
                ShowBar(UnitMapText.NodeLayoutResetBar(name), isError: false);
            }
            else ShowBar(commit.Kind == CommitKind.Conflicted ? UnitMapText.LayoutConflictBar(name) : UnitMapText.LayoutWriteFailedBar(name, commit.Reason ?? string.Empty), isError: true);
            RebuildScene();
        });
    }

    private Task ResetLayoutConfirmedAsync() => Serialize(async () =>
    {
        var before = new Dictionary<int, Vector>(_snapshot.Deltas);
        var commit = await CommitAsync(UnitLayoutChange.ClearEverything(), touched: null).ConfigureAwait(true);
        if (commit.Kind is CommitKind.Saved or CommitKind.SessionSaved)
        {
            var sessionOnly = commit.Kind == CommitKind.SessionSaved;
            _undo.Push(new UnitMapLayoutResetUndo(before, All: true, commit.Snapshot, sessionOnly));
            ShowBar(UnitMapText.LayoutResetBar(sessionOnly), isError: false);
        }
        else ShowBar(commit.Kind switch
        {
            // 초기화 자체가 412 — 그 사이 누가 배치를 바꿨다(종전엔 "초기화를 되돌리지 않았습니다" 라고 엉뚱하게 말했다 — REVIEW-01 L-3).
            CommitKind.Conflicted => UnitMapText.LayoutResetConflictBar,
            CommitKind.Unknown => UnitMapText.LayoutResetUnknownBar,
            _ => UnitMapText.LayoutResetFailedBar(commit.Reason ?? string.Empty),
        }, isError: true);
        RebuildScene();
    }, "배치 초기화");

    private string? ResetBlockedReason() => LayoutState switch
    {
        UnitMapLayoutState.SessionOnly => _commands.CanView ? null : UnitMapText.NoPermission,
        UnitMapLayoutState.Shared => _commands.CanEdit ? null : UnitMapText.NoPermission,
        UnitMapLayoutState.VersionMismatch => UnitMapText.LayoutVersionBlocked,
        UnitMapLayoutState.ReadFailed => UnitMapText.LayoutReadFailedBlocked,
        _ => UnitMapText.LayoutLoadingBlocked,
    };

    /// <summary>
    /// 초기화 되돌리기 — 먼저 읽고, 초기화 뒤 <b>바뀌지 않았고 아직 편제에 있는</b> 부대만 되살린다(조정자 결정 · 필수 항목 4 ·
    /// 레인 B <see cref="UnitMapLayoutSync.PlanResetUndo"/>). 모르는 id 를 보내면 서버가 묶음 전체를 422 로 거절하므로 삭제된 부대는 싣지 않는다.
    /// </summary>
    private async Task UndoResetAsync(UnitMapLayoutResetUndo entry)
    {
        var sessionOnly = LayoutState == UnitMapLayoutState.SessionOnly;
        if (entry.IsSessionOnly != sessionOnly) { Drop(entry, UnitMapText.LayoutResetUndoRefusedBar); return; }
        if (!sessionOnly)
        {
            await ReloadLayoutAsync(CancellationToken.None).ConfigureAwait(true);
            if (LayoutState != UnitMapLayoutState.Shared || _layoutStale)      // 다시 읽기 실패면 모르는 채로 보내지 않는다(MEDIUM-7)
            {
                ShowBar(UnitMapText.LayoutResetUndoUnreadableBar, isError: true);
                return;
            }
        }

        var plan = UnitMapLayoutSync.PlanResetUndo(entry, _snapshot, _tree);
        if (plan.LayoutVersionChanged) { Drop(entry, UnitMapText.LayoutVersionBlocked); return; }
        if (plan.IsTooLarge) { Drop(entry, UnitMapText.LayoutResetTooLargeBar); return; }
        if (plan.Change is null) { Drop(entry, UnitMapText.LayoutResetNothingToRestoreBar); return; }

        var commit = await CommitAsync(plan.Change, plan.Restored, sessionOnly ? null : plan.IfMatchVersion).ConfigureAwait(true);
        switch (commit.Kind)
        {
            case CommitKind.Saved:
            case CommitKind.SessionSaved:
                _undo.CompleteUndo(entry, succeeded: true);
                ShowBar(UnitMapText.LayoutResetUndoneBar(plan.SkippedCount), isError: false);
                break;
            case CommitKind.Conflicted:
                Drop(entry, UnitMapText.LayoutResetUndoRefusedBar);
                break;
            default:
                ShowBar(UnitMapText.LayoutResetUndoFailedBar(commit.Reason ?? string.Empty), isError: true);
                break;
        }
        RebuildScene();
    }
    #endregion

    #region - 알림 (FR-53) -
    /// <summary>
    /// <c>SYNC_UNIT_LAYOUT {version}</c>(호스트가 <see cref="UnitLayoutChangedMessage"/> 로 발행 — UI 스레드로 받는다).
    /// 가진 것보다 새로울 때만 500ms 합쳐 GET 1회, 손이 바쁘면 끝난 뒤로 미룬다. 발화 때 다시 비교한다(ISSUE-9).
    /// </summary>
    public Task HandleAsync(UnitLayoutChangedMessage message, CancellationToken cancellationToken)
    {
        if (message is not null) OnLayoutNotice(message.Version);
        return Task.CompletedTask;
    }

    /// <summary>합침 창이 계속 다시 열려도 첫 알림에서 이만큼 지나면 곧바로 한 번 읽는다(ISSUE-30 — 뒤끝 디바운스의 굶주림 방지).</summary>
    public static readonly TimeSpan NoticeMaxWait = TimeSpan.FromSeconds(2);

    /// <summary>알림이 이만큼 넘게 미뤄지면 한 번 알린다(ISSUE-9 — 강제 반영은 하지 않는다: 손 아래 노드가 튀지 않게).</summary>
    public static readonly TimeSpan NoticeDeferCap = TimeSpan.FromSeconds(30);

    private DateTime? _firstNoticeAt;

    /// <summary>
    /// 합침 · 발화 시점 재비교 · 연기 상한의 <b>시험된 판정</b>(<see cref="UnitMapLayoutNoticeGate"/> — TEST-66)을 그대로 쓴다
    /// (REVIEW-01 L-1 · MEDIUM-T2: 종전엔 뷰모델이 같은 규칙을 따로 들고 있어 합침 창 끝에 미룬 알림은 연기 시각이 적히지 않았다).
    /// </summary>
    private UnitMapLayoutNoticeGate? _noticeGateInstance;
    private UnitMapLayoutNoticeGate NoticeGate => _noticeGateInstance ??= new UnitMapLayoutNoticeGate(_clock, NoticeDeferCap);

    /// <summary>알림을 미루는 중이다(손이 비면 다시 판정한다).</summary>
    private bool _noticeDeferred;

    /// <summary>30초 연기 감시 — 닫히면(<see cref="_closing"/>) 함께 멈춘다(REVIEW-01 L-7).</summary>
    private CancellationTokenSource? _deferWatch;

    private void OnLayoutNotice(long version)
    {
        if (_disposed || LayoutState == UnitMapLayoutState.SessionOnly) return;
        if (UnitMapLayoutSync.ShouldRefetch(version, HaveVersion, UnitMapBusy.None) == UnitMapRefetchDecision.Skip) return;   // 자기 메아리 · 옛 알림
        NoticeGate.Notice(version);

        if (Busy != UnitMapBusy.None)
        {
            _ = ActOnNoticeAsync(NoticeGate.Evaluate(HaveVersion, Busy), CancellationToken.None);   // 미룬다(처음이면 연기 시각이 적힌다)
            return;
        }

        var now = _clock.UtcNow;
        _firstNoticeAt ??= now;
        if (now - _firstNoticeAt.Value >= NoticeMaxWait)
        {
            // 최대 대기 — 창을 끊고 지금 한 번(ISSUE-30).
            _noticeTrigger.Cancel();
            _ = OnNoticeSettledAsync(CancellationToken.None);
            return;
        }
        _ = _noticeTrigger.Pulse();
    }

    /// <summary>합침 창이 끝났다 — 그 순간의 버전 · 손 상태로 판정한다(그 사이 바빠졌으면 여기서 미룬다 — 연기 상한도 여기서부터 센다).</summary>
    private Task OnNoticeSettledAsync(CancellationToken token)
    {
        _firstNoticeAt = null;
        return ActOnNoticeAsync(NoticeGate.Evaluate(HaveVersion, Busy), token);
    }

    private Task ActOnNoticeAsync(UnitMapNoticeAction action, CancellationToken token)
    {
        switch (action)
        {
            case UnitMapNoticeAction.Refetch:
                _noticeDeferred = false;
                CancelDeferWatch();
                return Serialize(() => ReloadLayoutAsync(token));
            case UnitMapNoticeAction.Defer:
                _noticeDeferred = true;
                ArmDeferWatch();
                return Task.CompletedTask;
            case UnitMapNoticeAction.NotifyDeferred:
                _noticeDeferred = true;
                StatusText = UnitMapText.LayoutNoticeDeferredStatus;      // 한 번만(판정기가 센다) — 강제 반영은 하지 않는다
                return Task.CompletedTask;
            default:
                _noticeDeferred = false;
                CancelDeferWatch();
                return Task.CompletedTask;
        }
    }

    private void ArmDeferWatch()
    {
        if (_deferWatch is not null || _disposed) return;
        var watch = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        _deferWatch = watch;
        _ = WatchDeferCapAsync(watch);
    }

    private void CancelDeferWatch()
    {
        var watch = _deferWatch;
        _deferWatch = null;
        watch?.Cancel();
    }

    /// <summary>연기가 <see cref="NoticeDeferCap"/> 을 넘기면 판정기가 "한 번 알림" 을 낸다(ISSUE-9). 아직이면 다시 감시한다.</summary>
    private async Task WatchDeferCapAsync(CancellationTokenSource watch)
    {
        try
        {
            var stopped = false;
            try { await (_options.Delay ?? Task.Delay)(NoticeDeferCap, watch.Token).ConfigureAwait(true); }
            catch (OperationCanceledException) { stopped = true; }
            if (!ReferenceEquals(_deferWatch, watch)) return;
            _deferWatch = null;                                   // 이 감시가 끝났다 — 다시 멈추려 하지 않게(곧 버린다)
            if (stopped) return;
            if (_disposed || !_noticeDeferred || NoticeGate.PendingVersion is null) return;

            var action = NoticeGate.Evaluate(HaveVersion, Busy);
            if (action == UnitMapNoticeAction.Defer) { ArmDeferWatch(); return; }     // 상한 전(시계) — 다시 감시
            await ActOnNoticeAsync(action, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            watch.Dispose();
        }
    }

    /// <summary>손이 비었다 — 미뤄 둔 알림을 다시 판정한다(그 사이 내 쓰기 응답으로 버전이 올랐으면 건너뛴다).</summary>
    private void ReplayDeferredNotice()
    {
        if (!_noticeDeferred || _disposed) return;
        if (StatusText == UnitMapText.LayoutNoticeDeferredStatus) StatusText = null;
        _ = ActOnNoticeAsync(NoticeGate.Evaluate(HaveVersion, Busy), CancellationToken.None);
    }
    #endregion
}
