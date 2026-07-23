using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 심볼별 LabelAdorner 생명주기 소유(Symbol_Label_Decouple). 맵 마커와 라벨 동기화.
                  LineDrawingService/GroupSelectionService 피어. AdornerManagerService(정리타이머·딕셔너리) 미접촉.
   Created On   : 2026-07-02 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class LabelAdornerService : IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;
    private readonly Dictionary<IEditableMarker, LabelAdorner> _labels = new();
    private AdornerLayer? _layer;
    private bool _disposed;

    /// <summary>라벨 오프셋 드래그 완료 — VM이 DB 영속(FR-LB-05).</summary>
    public event System.Action<IEditableMarker, double, double>? LabelOffsetChanged;
    private void OnLabelMoved(IEditableMarker m, double bx, double by) => LabelOffsetChanged?.Invoke(m, bx, by);

    /// <summary>라벨 폭 조절 완료 — (before오프셋A/B, before폭) 원자 전달, VM이 DB 영속+undo(FR-13).</summary>
    public event System.Action<IEditableMarker, double, double, double>? LabelWidthChanged;
    private void OnLabelWidthResized(IEditableMarker m, double ba, double bb, double bw) => LabelWidthChanged?.Invoke(m, ba, bb, bw);

    public LabelAdornerService(GMapCustomControl map, ILogService? log = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
    }

    public void Attach(IEditableMarker marker)
    {
        if (_disposed || marker == null || marker.IsDisposed || _labels.ContainsKey(marker)) return;
        _layer ??= AdornerLayer.GetAdornerLayer(_map);
        if (_layer == null) { _log?.Error("LabelAdornerService: AdornerLayer 없음"); return; }
        var a = new LabelAdorner(_map, marker, _log);
        a.LabelOffsetChanged += OnLabelMoved;   // 오프셋 드래그 완료 → VM 영속 포워딩
        a.LabelWidthChanged += OnLabelWidthResized;   // 폭 조절 완료 → VM 영속+원자 undo 포워딩(FR-13)
        _layer.Add(a);
        _labels[marker] = a;
    }

    public void Detach(IEditableMarker marker)
    {
        if (marker == null || !_labels.TryGetValue(marker, out var a)) return;
        a.LabelOffsetChanged -= OnLabelMoved;
        a.LabelWidthChanged -= OnLabelWidthResized;
        try { (_layer ?? AdornerLayer.GetAdornerLayer(_map))?.Remove(a); } catch { /* 레이어 정리 중 */ }
        a.Dispose();
        _labels.Remove(marker);
    }

    private bool _syncing;   // Detach/Attach의 레이어 조작이 CollectionChanged를 재발화해도 재진입 차단(P2-03)

    /// <summary>맵의 편집가능 마커와 라벨 동기화 — 신규는 Attach, 사라진 건 Detach. (로드/Reset 후 호출)</summary>
    public void Sync(IEnumerable? markers)
    {
        if (_disposed || markers == null || _syncing) return;
        try
        {
            _syncing = true;
            var current = new HashSet<IEditableMarker>(markers.OfType<IEditableMarker>().Where(m => !m.IsDisposed));
            foreach (var m in _labels.Keys.ToList())
                if (!current.Contains(m)) Detach(m);
            foreach (var m in current)
                if (!_labels.ContainsKey(m)) Attach(m);
        }
        finally { _syncing = false; }
    }

    /// <summary>CollectionChanged Action 기반 증분 처리 — Add/Remove는 해당 항목만 O(1), Reset/기타만 전체 Sync.
    /// 초기 대량 로드의 O(N²) 전체 재스캔 제거(Overlay_Title FR-12/P1-06).</summary>
    public void ApplyCollectionChange(System.Collections.Specialized.NotifyCollectionChangedEventArgs e, IEnumerable? allMarkers)
    {
        if (_disposed || e == null || _syncing) return;
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (var m in e.NewItems.OfType<IEditableMarker>()) Attach(m);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (var m in e.OldItems.OfType<IEditableMarker>()) Detach(m);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (var m in e.OldItems.OfType<IEditableMarker>()) Detach(m);
                if (e.NewItems != null)
                    foreach (var m in e.NewItems.OfType<IEditableMarker>()) Attach(m);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                break;   // 순서 변경은 라벨 무관
            default:
                Sync(allMarkers);   // Reset 등 — 전체 동기화
                break;
        }
    }

    public void Clear()
    {
        foreach (var m in _labels.Keys.ToList()) Detach(m);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
