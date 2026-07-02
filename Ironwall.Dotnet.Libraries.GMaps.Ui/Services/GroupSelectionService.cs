using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
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
    public event System.Action? GroupMoveCompleted;
    /// <summary>그룹 삭제 요청(핸들 메뉴) — VM 처리(FR-MS-05).</summary>
    public event System.Action? GroupDeleteRequested;
    /// <summary>그룹 잠금/해제 요청(true=잠금) — VM 처리(FR-MS-06).</summary>
    public event System.Action<bool>? GroupLockRequested;

    /// <summary>러버밴드 결과 설정 — 이전 그룹 clear 후 새 집합에 adorner 부착·IsSelected 표시. 빈 목록=clear.</summary>
    public void SetSelection(IReadOnlyList<IEditableMarker>? markers)
    {
        Clear();
        if (markers == null || markers.Count == 0) return;

        _selection = markers.Where(m => m != null && !m.IsDisposed).ToList();
        if (_selection.Count == 0) return;
        foreach (var m in _selection) m.IsSelected = true;

        _layer = AdornerLayer.GetAdornerLayer(_map);
        if (_layer == null) { _log?.Error("GroupSelectionService: AdornerLayer 없음"); return; }

        _adorner = new GroupSelectionAdorner(_map, _log);
        _adorner.GroupMoveCompleted += OnAdornerMoveCompleted;
        _adorner.GroupDeleteRequested += OnAdornerDeleteRequested;
        _adorner.GroupLockRequested += OnAdornerLockRequested;
        _adorner.SetMarkers(_selection);
        _layer.Add(_adorner);
        _log?.Info($"그룹 선택 {_selection.Count}개");
    }

    private void OnAdornerMoveCompleted() => GroupMoveCompleted?.Invoke();
    private void OnAdornerDeleteRequested() => GroupDeleteRequested?.Invoke();
    private void OnAdornerLockRequested(bool locked) => GroupLockRequested?.Invoke(locked);

    /// <summary>선택집합에서 일부 제거(그룹 삭제 후) 후 adorner 갱신. 남은 게 없으면 Clear.</summary>
    public void RemoveAndRefresh(IEnumerable<IEditableMarker> removed)
    {
        if (removed != null)
        {
            var set = new HashSet<IEditableMarker>(removed);
            _selection = _selection.Where(m => !set.Contains(m)).ToList();
        }
        if (_selection.Count == 0) { Clear(); return; }
        _adorner?.SetMarkers(_selection);
    }

    /// <summary>지오/크기 변경 후 바운딩박스 갱신.</summary>
    public void RefreshAdorner() => _adorner?.SetMarkers(_selection);

    /// <summary>그룹 해제 — adorner 제거·Dispose, IsSelected 해제.</summary>
    public void Clear()
    {
        if (_adorner != null)
        {
            _adorner.GroupMoveCompleted -= OnAdornerMoveCompleted;
            _adorner.GroupDeleteRequested -= OnAdornerDeleteRequested;
            _adorner.GroupLockRequested -= OnAdornerLockRequested;
            try { _layer?.Remove(_adorner); } catch { /* 레이어 정리 중 */ }
            _adorner.Dispose();
            _adorner = null;
        }
        foreach (var m in _selection)
            if (m != null && !m.IsDisposed) m.IsSelected = false;
        _selection = new();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
