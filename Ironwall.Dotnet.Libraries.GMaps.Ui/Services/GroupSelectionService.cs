using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : Shift+드래그 러버밴드 그룹 선택 상태·GroupSelectionAdorner 생명주기 소유.
                  LineDrawingService 피어. AdornerManagerService(5분 정리타이머·per-marker 딕셔너리) 미접촉.
   Created On   : 2026-07-02 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class GroupSelectionService : IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;
    private GroupSelectionAdorner? _adorner;
    private AdornerLayer? _layer;
    private List<IEditableMarker> _selection = new();
    private bool _disposed;

    public GroupSelectionService(GMapCustomControl map, ILogService? log = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
    }

    /// <summary>현재 그룹 선택집합.</summary>
    public IReadOnlyList<IEditableMarker> Selection => _selection;
    public bool HasSelection => _selection.Count > 0;

    /// <summary>그룹 이동 드래그 완료 — VM이 멤버별 DB 영속(FR-MS-05).</summary>
    public event System.Action<IReadOnlyList<(IEditableMarker marker, PointLatLng before)>>? GroupMoveCompleted;
    /// <summary>그룹 삭제 요청(핸들 메뉴) — VM 처리(FR-MS-05).</summary>
    public event System.Action? GroupDeleteRequested;
    /// <summary>그룹 잠금/해제 요청(true=잠금) — VM 처리(FR-MS-06).</summary>
    public event System.Action<bool>? GroupLockRequested;
    /// <summary>그룹 표시/숨김 요청(true=표시) — VM 처리(런타임 가시성).</summary>
    public event System.Action<bool>? GroupVisibilityRequested;
    /// <summary>그룹 Z순서 요청(true=맨위로) — VM 처리(BatchUpdateZOrderAsync).</summary>
    public event System.Action<bool>? GroupZOrderRequested;
    /// <summary>묶음 회전 확정 — 바뀐 멤버의 회전 전 상태 목록 + 회전량(도). VM 이 멤버별 DB 영속 + Undo 1단위.</summary>
    public event System.Action<IReadOnlyList<GroupRotationSession.Original>, double>? GroupRotateCompleted;

    /// <summary>회전 세션 상태가 바뀜(열림 · 각도 · 중심 · 중심 고르기 · 끝) — 컨트롤러 판이 세션에서 다시 읽는다.</summary>
    public event System.Action? RotationChanged;

    /// <summary>선택 집합이 바뀌었다(설정 · 해제 · 일부 제거) — 레이어 패널 잎 선택 동기용.</summary>
    public event System.Action? SelectionChanged;

    private GroupRotationSession? _rotation;
    /// <summary>열린 회전 세션(드래그 중이거나 키보드로 돌리고 아직 확정 전) — 없으면 null.</summary>
    public GroupRotationSession? Rotation => _rotation;
    public bool IsRotating => _rotation != null;
    /// <summary>「지도에서 고르기」 — 다음 지도 클릭 자리를 회전 중심으로(열린 회전 세션이 있을 때만).</summary>
    public bool IsPickingPivot { get; private set; }

    // 손잡이 끌기 한 번의 시작 상태 — 열린 세션에서 이어 끌면 그 각에서 더하고, 캡처를 잃으면 그 각(또는 세션 전체 취소)으로
    private double _dragBaseAngle;
    private bool _dragOpenedSession;
    private PointLatLng _pivotBeforeDrag;

    /// <summary>회전 세션 열기(이미 열렸으면 그대로). 2개 미만이거나 전원 잠금이면 false.</summary>
    public bool BeginRotation()
    {
        if (_rotation != null) return true;
        if (_selection.Count < 2) return false;
        var s = new GroupRotationSession(_selection);
        if (s.IsEmpty) return false;
        _rotation = s;
        _adorner?.SetRotationState(s.Pivot, 0d);
        RotationChanged?.Invoke();
        return true;
    }

    /// <summary>회전량 미리보기(원본 기준 절대 각도).</summary>
    public void PreviewRotation(double degrees)
    {
        if (_rotation == null) return;
        _rotation.Preview(degrees);
        _adorner?.SetRotationState(_rotation.Pivot, _rotation.Angle);
        RotationChanged?.Invoke();
    }

    /// <summary>회전 중심 바꾸기(미리보기 다시 계산 — 저장 없음).</summary>
    public void SetRotationPivot(PointLatLng pivot)
    {
        if (_rotation == null) return;
        _rotation.SetPivot(pivot);
        _adorner?.SetRotationState(_rotation.Pivot, _rotation.Angle);
        RotationChanged?.Invoke();
    }

    /// <summary>「가운데로」 — 중심을 묶음 경계 상자 중심(기본값)으로.</summary>
    public void ResetRotationPivot()
    {
        if (_rotation != null) SetRotationPivot(_rotation.DefaultPivot);
    }

    /// <summary>「지도에서 고르기」 켜기/끄기 — 켜져 있으면 다음 지도 클릭이 중심이 된다(어도너가 지도 전체를 받음).</summary>
    public void SetPivotPicking(bool on)
    {
        on = on && _rotation != null;
        if (IsPickingPivot == on) return;
        IsPickingPivot = on;
        _adorner?.SetPivotPicking(on);
        RotationChanged?.Invoke();
    }

    /// <summary>키보드 한 칸 — 세션이 없으면 열고 현재 회전량에 더한다. 반환 = 회전했는가.</summary>
    public bool RotateBy(double step)
    {
        if (!BeginRotation()) return false;
        PreviewRotation(_rotation!.Angle + step);
        return true;
    }

    /// <summary>회전 세션 끝 — commit=true 면 확정(GroupRotateCompleted), false 면 원위치(저장 없음).</summary>
    public void EndRotation(bool commit)
    {
        var s = _rotation;
        if (s == null) return;
        _rotation = null;
        IsPickingPivot = false;
        _adorner?.SetPivotPicking(false);
        _adorner?.AbortRotationDrag();
        if (!commit) s.Cancel();
        var changed = commit ? s.Commit() : Array.Empty<GroupRotationSession.Original>();
        _adorner?.SetRotationState(null, 0d);
        RotationChanged?.Invoke();
        if (changed.Count > 0) GroupRotateCompleted?.Invoke(changed, s.Angle);
    }

    /// <summary>러버밴드 결과 설정 — 이전 그룹 clear 후 새 집합에 adorner 부착·IsSelected 표시. 빈 목록=clear.</summary>
    public void SetSelection(IReadOnlyList<IEditableMarker>? markers)
    {
        Clear();
        if (markers == null || markers.Count == 0) return;

        _selection = markers.Where(m => m != null && !m.IsDisposed).ToList();
        if (_selection.Count == 0) return;
        SelectionChanged?.Invoke();
        // IsSelected는 설정하지 않음 — 심볼별 선택표시는 GroupSelectionAdorner 정밀 박스가 균일 담당
        // (템플릿 PART_SelectionHighlight 불일치·부정확 회피). 그룹 이동/삭제/잠금은 _selection 기준.

        // ★ 그룹(하늘색) 어도너는 2개 이상에서만 — 1개는 그룹이 아니다(단일 선택은 MarkerEditAdorner 소관).
        //   1-항목 선택이 여기 오는 경우는 러버밴드 시작 시 단일→그룹 승격(병합용, 드래그 중 일시 상태)뿐이며
        //   릴리스 후 VM funnel(ApplyGroupSelectionByIds)이 1개를 단일 선택으로 강등한다. (Adorner_Box_Mismatch_Fix)
        if (_selection.Count < 2) { _log?.Info($"그룹 선택 {_selection.Count}개 — 어도너 미부착(2개 미만)"); return; }

        _layer = AdornerLayer.GetAdornerLayer(_map);
        if (_layer == null) { _log?.Error("GroupSelectionService: AdornerLayer 없음"); return; }

        _adorner = new GroupSelectionAdorner(_map, _log);
        _adorner.GroupMoveCompleted += OnAdornerMoveCompleted;
        _adorner.GroupDeleteRequested += OnAdornerDeleteRequested;
        _adorner.GroupLockRequested += OnAdornerLockRequested;
        _adorner.GroupVisibilityRequested += OnAdornerVisibilityRequested;
        _adorner.GroupZOrderRequested += OnAdornerZOrderRequested;
        _adorner.RotationDragStarted += OnAdornerRotationDragStarted;
        _adorner.RotationDragDelta += OnAdornerRotationDragDelta;
        _adorner.RotationDragFinished += OnAdornerRotationDragFinished;
        _adorner.PivotDragStarted += OnAdornerPivotDragStarted;
        _adorner.PivotDragDelta += SetRotationPivot;
        _adorner.PivotDragFinished += OnAdornerPivotDragFinished;
        _adorner.PivotPicked += OnAdornerPivotPicked;
        _adorner.MoveDragStarting += OnAdornerMoveDragStarting;
        _adorner.SetMarkers(_selection);
        _layer.Add(_adorner);
        _log?.Info($"그룹 선택 {_selection.Count}개");
    }

    private void OnAdornerMoveCompleted(IReadOnlyList<(IEditableMarker marker, PointLatLng before)> moves) => GroupMoveCompleted?.Invoke(moves);
    private void OnAdornerDeleteRequested() => GroupDeleteRequested?.Invoke();
    private void OnAdornerLockRequested(bool locked) => GroupLockRequested?.Invoke(locked);
    private void OnAdornerVisibilityRequested(bool show) => GroupVisibilityRequested?.Invoke(show);
    private void OnAdornerZOrderRequested(bool toFront) => GroupZOrderRequested?.Invoke(toFront);
    private void OnAdornerMoveDragStarting() => EndRotation(commit: true);

    /// <summary>손잡이 끌기 시작 — 세션이 없으면 열고, 있으면 지금 각에서 이어 돌린다.</summary>
    private void OnAdornerRotationDragStarted()
    {
        _dragOpenedSession = _rotation == null;
        BeginRotation();
        _dragBaseAngle = _rotation?.Angle ?? 0d;
    }

    private void OnAdornerRotationDragDelta(double degrees) => PreviewRotation(_dragBaseAngle + degrees);

    /// <summary>손잡이 끌기 끝 — 놓음(commit=true)은 확정하지 않고 세션을 열어 둔다(컨트롤러 판 · Enter 로 확정).
    /// 캡처를 잃음(false) = 이 끌기만 되돌림(이 끌기로 연 세션이면 세션 전체 취소 · 원위치).</summary>
    private void OnAdornerRotationDragFinished(bool commit)
    {
        if (commit) return;
        if (_dragOpenedSession) EndRotation(commit: false);
        else PreviewRotation(_dragBaseAngle);
    }

    private void OnAdornerPivotDragStarted() => _pivotBeforeDrag = _rotation?.Pivot ?? PointLatLng.Empty;

    /// <summary>중심 십자 끌기 끝 — 놓음 = 그 자리 유지, 캡처 잃음 = 끌기 전 중심으로.</summary>
    private void OnAdornerPivotDragFinished(bool commit)
    {
        if (!commit) SetRotationPivot(_pivotBeforeDrag);
    }

    private void OnAdornerPivotPicked(PointLatLng pivot)
    {
        SetPivotPicking(false);
        SetRotationPivot(pivot);
    }

    /// <summary>선택집합에서 일부 제거(그룹 삭제 후) 후 adorner 갱신. 남은 게 없으면 Clear.</summary>
    public void RemoveAndRefresh(IEnumerable<IEditableMarker> removed)
    {
        if (removed != null)
        {
            var set = new HashSet<IEditableMarker>(removed);
            _selection = _selection.Where(m => !set.Contains(m)).ToList();
        }
        if (_selection.Count < 2) { Clear(); return; }   // 1개 잔존 = 그룹 아님 — 어도너 제거(단일 선택은 사용자가 재클릭)
        _adorner?.SetMarkers(_selection);
        SelectionChanged?.Invoke();
    }

    /// <summary>지오/크기 변경 후 바운딩박스 갱신.</summary>
    public void RefreshAdorner() => _adorner?.SetMarkers(_selection);

    /// <summary>그룹 해제 — adorner 제거·Dispose, IsSelected 해제.</summary>
    public void Clear()
    {
        EndRotation(commit: true);   // 열린 회전은 선택이 바뀌기 전에 확정(선택 바꾸기 = 다른 작업으로 넘어감)
        if (_adorner != null)
        {
            _adorner.GroupMoveCompleted -= OnAdornerMoveCompleted;
            _adorner.GroupDeleteRequested -= OnAdornerDeleteRequested;
            _adorner.GroupLockRequested -= OnAdornerLockRequested;
            _adorner.GroupVisibilityRequested -= OnAdornerVisibilityRequested;
            _adorner.GroupZOrderRequested -= OnAdornerZOrderRequested;
            _adorner.RotationDragStarted -= OnAdornerRotationDragStarted;
            _adorner.RotationDragDelta -= OnAdornerRotationDragDelta;
            _adorner.RotationDragFinished -= OnAdornerRotationDragFinished;
            _adorner.PivotDragStarted -= OnAdornerPivotDragStarted;
            _adorner.PivotDragDelta -= SetRotationPivot;
            _adorner.PivotDragFinished -= OnAdornerPivotDragFinished;
            _adorner.PivotPicked -= OnAdornerPivotPicked;
            _adorner.MoveDragStarting -= OnAdornerMoveDragStarting;
            try { _layer?.Remove(_adorner); } catch { /* 레이어 정리 중 */ }
            _adorner.Dispose();
            _adorner = null;
        }
        bool had = _selection.Count > 0;
        foreach (var m in _selection)
            if (m != null && !m.IsDisposed) m.IsSelected = false;
        _selection = new();
        if (had) SelectionChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
