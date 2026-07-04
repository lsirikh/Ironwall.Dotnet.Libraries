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
        _layer.Add(a);
        _labels[marker] = a;
    }

    public void Detach(IEditableMarker marker)
    {
        if (marker == null || !_labels.TryGetValue(marker, out var a)) return;
        a.LabelOffsetChanged -= OnLabelMoved;
        try { (_layer ?? AdornerLayer.GetAdornerLayer(_map))?.Remove(a); } catch { /* 레이어 정리 중 */ }
        a.Dispose();
        _labels.Remove(marker);
    }

    /// <summary>맵의 편집가능 마커와 라벨 동기화 — 신규는 Attach, 사라진 건 Detach. (Markers.CollectionChanged/로드 후 호출)</summary>
    public void Sync(IEnumerable? markers)
    {
        if (_disposed || markers == null) return;
        var current = new HashSet<IEditableMarker>(markers.OfType<IEditableMarker>().Where(m => !m.IsDisposed));
        foreach (var m in _labels.Keys.ToList())
            if (!current.Contains(m)) Detach(m);
        foreach (var m in current)
            if (!_labels.ContainsKey(m)) Attach(m);
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
