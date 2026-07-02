using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 그룹 선택(러버밴드 결과) 맵레벨 오버레이 — 선택집합 점선 바운딩박스.
                  이동=선택 심볼 아무거나 좌드래그(핸들 없음, 커서=SizeAll), 우클릭=그룹 공통 메뉴
                  (삭제/잠금/해제/표시/숨김/맨위로/맨아래로). LineDrawingAdorner 패턴.
   Note         : AdornerManager 미사용(GroupSelectionService 소유). HitTestCore=선택 마커 영역만
                  (그 외 클릭스루, 불변식#3). 좌표=inner공간(e.GetPosition(_map), InnerToOuter 금지 불변식#10).
                  Dispose서 OnMapZoomChanged/OnMapDrag 구독해제(누수 방지).
   Created On   : 2026-07-02 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class GroupSelectionAdorner : Adorner, IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;
    private IReadOnlyList<IEditableMarker> _markers = Array.Empty<IEditableMarker>();

    private readonly Pen _boxPen;

    private bool _dragging;
    private Point _grabScreen;
    private PointLatLng[] _origPositions = Array.Empty<PointLatLng>();
    private bool _disposed;

    /// <summary>그룹 이동 드래그 완료 — 서비스/VM이 멤버별 DB 영속(FR-MS-05).</summary>
    public event System.Action? GroupMoveCompleted;
    /// <summary>그룹 삭제 요청(우클릭 메뉴/Del) — VM이 멤버 삭제(FR-MS-05).</summary>
    public event System.Action? GroupDeleteRequested;
    /// <summary>그룹 잠금/해제 요청(우클릭 메뉴, true=잠금) — VM이 멤버 IsLocked+영속(FR-MS-06).</summary>
    public event System.Action<bool>? GroupLockRequested;
    /// <summary>그룹 표시/숨김 요청(우클릭 메뉴, true=표시) — VM이 멤버 가시성 토글(런타임).</summary>
    public event System.Action<bool>? GroupVisibilityRequested;
    /// <summary>그룹 Z순서 요청(우클릭 메뉴, true=맨 위로) — VM이 멤버 ZOrder 일괄(BatchUpdateZOrderAsync).</summary>
    public event System.Action<bool>? GroupZOrderRequested;

    public GroupSelectionAdorner(GMapCustomControl map, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
        IsHitTestVisible = true;   // 선택 마커 위 hit 필요(그 외는 HitTestCore null로 투과)
        Cursor = Cursors.SizeAll;  // adorner는 HitTestCore로 선택 마커 위에서만 이벤트 수신 → 이동커서도 거기서만 표시

        _boxPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 170, 255)), 1.5d) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        _boxPen.Freeze();

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌 생존)
        _map.OnMapDrag += OnMapChanged;

        ContextMenu = BuildContextMenu();   // 선택 심볼 우클릭 → 그룹 공통 메뉴 (FR-MS-09)
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        void Add(string header, System.Action onClick)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => onClick();
            menu.Items.Add(mi);
        }
        Add("그룹 삭제", () => GroupDeleteRequested?.Invoke());
        menu.Items.Add(new Separator());
        Add("그룹 잠금", () => GroupLockRequested?.Invoke(true));
        Add("그룹 잠금 해제", () => GroupLockRequested?.Invoke(false));
        menu.Items.Add(new Separator());
        Add("그룹 표시", () => GroupVisibilityRequested?.Invoke(true));
        Add("그룹 숨김", () => GroupVisibilityRequested?.Invoke(false));
        menu.Items.Add(new Separator());
        Add("맨 위로", () => GroupZOrderRequested?.Invoke(true));
        Add("맨 아래로", () => GroupZOrderRequested?.Invoke(false));
        return menu;
    }

    private void OnMapChanged() => InvalidateVisual();

    public void SetMarkers(IReadOnlyList<IEditableMarker> markers)
    {
        _markers = markers ?? Array.Empty<IEditableMarker>();
        InvalidateVisual();
    }

    /// <summary>마커의 화면 사각형 — 실제 렌더 위치·크기(Offset/회전 반영, map 좌표계). 폴백=중심±렌더크기/2(32).</summary>
    private Rect MarkerRect(IEditableMarker m)
    {
        var shape = (m as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
        if (shape != null && shape.ActualWidth > 0 && shape.ActualHeight > 0)
        {
            try
            {
                // 실제 렌더 bounds(Offset·회전 반영) → 심볼 크기에 정확히 맞춘 박스(FR-MS-03)
                var b = shape.TransformToVisual(_map).TransformBounds(new Rect(0, 0, shape.ActualWidth, shape.ActualHeight));
                if (b.Width > 0 && b.Height > 0) return b;
            }
            catch { /* 비주얼트리 분리 등 → 폴백 */ }
        }
        var sp = _map.FromLatLngToLocal(m.Position);
        double w = shape?.ActualWidth is > 0 ? shape.ActualWidth : 32d;
        double h = shape?.ActualHeight is > 0 ? shape.ActualHeight : 32d;
        return new Rect(sp.X - w / 2d, sp.Y - h / 2d, w, h);
    }

    /// <summary>p(adorner 공간)가 선택 마커 중 하나의 화면 사각형 안인가 — 좌드래그 이동/우클릭 메뉴 hit 영역.</summary>
    private bool IsOverSelected(Point p)
    {
        foreach (var m in _markers)
        {
            if (m == null || m.IsDisposed) continue;
            if (MarkerRect(m).Contains(p)) return true;
        }
        return false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
            if (_markers.Count == 0) return;
            // 심볼별 정밀 점선 박스 — 전 심볼에 균일 표시(바운딩박스 1개 아님, 템플릿 하이라이트 불일치 대체, FR-MS-03).
            foreach (var m in _markers)
            {
                if (m == null || m.IsDisposed) continue;
                var r = MarkerRect(m);
                if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) continue;
                r.Inflate(1.5d, 1.5d);   // 심볼 가장자리 살짝 밖(가시성)
                dc.DrawRectangle(null, _boxPen, r);
            }
        }
        catch (Exception ex) { _log?.Error($"GroupSelectionAdorner 렌더 실패: {ex.Message}"); }
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => IsOverSelected(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;   // 선택 마커 외 투과(불변식#3)

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (IsOverSelected(p))   // 선택 심볼 아무거나 좌드래그 = 그룹 이동
        {
            _dragging = true;
            _grabScreen = e.GetPosition(_map);   // 델타는 맵컨트롤 공간(WPF가 디지털줌 RenderTransform.Inverse 자동적용, 불변식#10)
            _origPositions = _markers.Select(m => m.Position).ToArray();
            CaptureMouse();
            e.Handled = true;
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            var cur = e.GetPosition(_map);   // 맵컨트롤 공간(디지털줌 자동 역보정) — _grabScreen과 동일 공간
            var gGrab = _map.FromLocalToLatLng((int)_grabScreen.X, (int)_grabScreen.Y);   // inner공간(디지털줌 역보정 금지 불변식#10)
            var gCur = _map.FromLocalToLatLng((int)cur.X, (int)cur.Y);
            double dLat = gCur.Lat - gGrab.Lat, dLng = gCur.Lng - gGrab.Lng;
            for (int i = 0; i < _markers.Count && i < _origPositions.Length; i++)
            {
                var m = _markers[i];
                if (m == null || m.IsDisposed || m.IsLocked) continue;   // FR-MS-08: 잠긴 멤버 이동 스킵
                m.UpdateLocation(new PointLatLng(_origPositions[i].Lat + dLat, _origPositions[i].Lng + dLng));
            }
            InvalidateVisual();
            e.Handled = true;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
            GroupMoveCompleted?.Invoke();   // lock 밖 발화(멤버별 DB 영속은 서비스/VM)
        }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragging) { _dragging = false; GroupMoveCompleted?.Invoke(); }
        base.OnLostMouseCapture(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsMouseCaptured) ReleaseMouseCapture();
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        GroupMoveCompleted = null;
        GroupDeleteRequested = null;
        GroupLockRequested = null;
        GroupVisibilityRequested = null;
        GroupZOrderRequested = null;
    }
}
