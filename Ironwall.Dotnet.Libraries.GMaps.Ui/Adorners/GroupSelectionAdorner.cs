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
   Purpose      : 그룹 선택(러버밴드 결과) 맵레벨 오버레이 — 선택집합 바운딩박스 + 그룹 이동 핸들.
                  이동 핸들 드래그로 선택 심볼 일괄 이동(잠긴 멤버 스킵). LineDrawingAdorner 패턴.
   Note         : AdornerManager 미사용(GroupSelectionService 소유). HitTestCore=핸들만(그 외 클릭스루,
                  불변식#3). 좌표=inner공간(FromLatLngToLocal/FromLocalToLatLng, InnerToOuter 금지 불변식#10).
                  Dispose서 OnMapZoomChanged/OnMapDrag 구독해제(누수 방지).
   Created On   : 2026-07-02 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class GroupSelectionAdorner : Adorner, IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;
    private IReadOnlyList<IEditableMarker> _markers = Array.Empty<IEditableMarker>();

    private readonly Pen _boxPen;
    private readonly Brush _handleFill;
    private readonly Pen _handlePen;
    private const double HandleSize = 14d;

    private bool _dragging;
    private Point _grabScreen;
    private PointLatLng[] _origPositions = Array.Empty<PointLatLng>();
    private Point _handleCenter;
    private bool _disposed;

    /// <summary>그룹 이동 드래그 완료 — 서비스/VM이 멤버별 DB 영속(FR-MS-05).</summary>
    public event System.Action? GroupMoveCompleted;
    /// <summary>그룹 삭제 요청(핸들 우클릭 메뉴/Del) — VM이 멤버 삭제(FR-MS-05).</summary>
    public event System.Action? GroupDeleteRequested;
    /// <summary>그룹 잠금/해제 요청(핸들 우클릭 메뉴, true=잠금) — VM이 멤버 IsLocked+영속(FR-MS-06).</summary>
    public event System.Action<bool>? GroupLockRequested;

    public GroupSelectionAdorner(GMapCustomControl map, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
        IsHitTestVisible = true;   // 이동 핸들 hit 필요(본체는 HitTestCore null로 투과)

        _boxPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 170, 255)), 1.5d) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        _boxPen.Freeze();
        _handleFill = Freeze(new SolidColorBrush(Color.FromArgb(230, 0, 170, 255)));
        _handlePen = new Pen(Brushes.White, 1.5d); _handlePen.Freeze();

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌 생존)
        _map.OnMapDrag += OnMapChanged;

        ContextMenu = BuildContextMenu();   // 이동핸들 우클릭 → 그룹 삭제/잠금/해제 (FR-MS-09)
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var del = new MenuItem { Header = "그룹 삭제" };
        del.Click += (_, _) => GroupDeleteRequested?.Invoke();
        var lockIt = new MenuItem { Header = "그룹 잠금" };
        lockIt.Click += (_, _) => GroupLockRequested?.Invoke(true);
        var unlock = new MenuItem { Header = "그룹 잠금 해제" };
        unlock.Click += (_, _) => GroupLockRequested?.Invoke(false);
        menu.Items.Add(del);
        menu.Items.Add(new Separator());
        menu.Items.Add(lockIt);
        menu.Items.Add(unlock);
        return menu;
    }

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
    private void OnMapChanged() => InvalidateVisual();

    public void SetMarkers(IReadOnlyList<IEditableMarker> markers)
    {
        _markers = markers ?? Array.Empty<IEditableMarker>();
        InvalidateVisual();
    }

    private Rect ComputeBox()
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var m in _markers)
        {
            if (m == null || m.IsDisposed) continue;
            var sp = _map.FromLatLngToLocal(m.Position);
            var shape = (m as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
            double w = shape?.ActualWidth is > 0 ? shape.ActualWidth : 32d;
            double h = shape?.ActualHeight is > 0 ? shape.ActualHeight : 32d;
            minX = Math.Min(minX, sp.X - w / 2d); minY = Math.Min(minY, sp.Y - h / 2d);
            maxX = Math.Max(maxX, sp.X + w / 2d); maxY = Math.Max(maxY, sp.Y + h / 2d);
            any = true;
        }
        if (!any) return Rect.Empty;
        return new Rect(minX - 4d, minY - 4d, (maxX - minX) + 8d, (maxY - minY) + 8d);
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
            if (_markers.Count == 0) return;
            var box = ComputeBox();
            if (box.IsEmpty || box.Width <= 0 || box.Height <= 0) return;
            dc.DrawRectangle(null, _boxPen, box);
            _handleCenter = new Point(box.X, box.Y);   // 좌상단 그룹 이동핸들
            dc.DrawRectangle(_handleFill, _handlePen,
                new Rect(_handleCenter.X - HandleSize / 2d, _handleCenter.Y - HandleSize / 2d, HandleSize, HandleSize));
        }
        catch (Exception ex) { _log?.Error($"GroupSelectionAdorner 렌더 실패: {ex.Message}"); }
    }

    private bool IsOnHandle(Point p)
        => _markers.Count > 0 && Math.Abs(p.X - _handleCenter.X) <= HandleSize && Math.Abs(p.Y - _handleCenter.Y) <= HandleSize;

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => IsOnHandle(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;   // 핸들 외 투과(불변식#3)

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (IsOnHandle(p))
        {
            _dragging = true;
            _grabScreen = p;
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
            var cur = e.GetPosition(this);
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
    }
}
