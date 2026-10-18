using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 그룹 선택(러버밴드 결과) 맵레벨 오버레이 — 선택집합 점선 바운딩박스.
                  이동=선택 심볼 아무거나 좌드래그(핸들 없음, 커서=SizeAll), 우클릭=그룹 공통 메뉴
                  (삭제/잠금/해제/표시/숨김/맨위로/맨아래로). LineDrawingAdorner 패턴.
                  회전=묶음 위쪽 회전 손잡이(Thumb 자식) 캡처 드래그 → 서비스가 GroupRotationSession 으로 미리보기 · 확정(2026-10-18).
                  회전 세션 중엔 중심 십자 손잡이(Thumb, Map.GroupRotation.CenterHandle)를 끌어 중심을 옮기고,
                  「지도에서 고르기」 중엔 지도 전체 눌림을 받아 그 자리를 중심으로 알린다(컨트롤러 판, 2026-10-18).
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

    /// <summary>그룹 이동 드래그 완료 — 멤버별 (마커,이동전위치) 목록 전달(멤버 DB 영속 + Undo 기록, FR-MS-05).</summary>
    public event System.Action<IReadOnlyList<(IEditableMarker marker, PointLatLng before)>>? GroupMoveCompleted;
    /// <summary>그룹 삭제 요청(우클릭 메뉴/Del) — VM이 멤버 삭제(FR-MS-05).</summary>
    public event System.Action? GroupDeleteRequested;
    /// <summary>그룹 잠금/해제 요청(우클릭 메뉴, true=잠금) — VM이 멤버 IsLocked+영속(FR-MS-06).</summary>
    public event System.Action<bool>? GroupLockRequested;
    /// <summary>그룹 표시/숨김 요청(우클릭 메뉴, true=표시) — VM이 멤버 가시성 토글(런타임).</summary>
    public event System.Action<bool>? GroupVisibilityRequested;
    /// <summary>그룹 Z순서 요청(우클릭 메뉴, true=맨 위로) — VM이 멤버 ZOrder 일괄(BatchUpdateZOrderAsync).</summary>
    public event System.Action<bool>? GroupZOrderRequested;

    // ── 묶음 회전 손잡이(드래그 우선 규칙: Thumb=peer 실재 · 컨테이너가 PreviewMouseDown 선점·캡처 · 데드존 8 · 단일 FinishRotationDrag) ──
    private readonly VisualCollection _children;
    private readonly Thumb _rotateHandle;
    private const double RotateHandleSize = 14d;
    private bool _rotPressed;          // 손잡이 눌림(데드존 전)
    private bool _rotDragging;         // 데드존 통과 — 회전 진행 중
    private Point _rotPressPoint;
    private PointLatLng? _rotationPivot;   // 회전 세션 중심(세션 없으면 null) — 서비스가 설정
    private double _rotationAngle;
    private PointLatLng _rotHandleRestGeo; // 세션 시작 때 손잡이 자리(위경도 — 팬/줌에도 같은 자리)

    /// <summary>회전 손잡이 드래그가 데드존을 넘어 시작됨 — 서비스가 회전 세션을 연다.</summary>
    public event System.Action? RotationDragStarted;
    /// <summary>회전 손잡이 드래그 중 회전량(도, 시계 방향 +, Shift=15° 단위) — 서비스가 미리보기.</summary>
    public event System.Action<double>? RotationDragDelta;
    /// <summary>회전 손잡이 드래그 끝(true=놓아서 확정, false=캡처 잃음 → 취소).</summary>
    public event System.Action<bool>? RotationDragFinished;
    /// <summary>그룹 이동 드래그가 막 시작됨 — 열린 키보드 회전을 먼저 확정하게(Undo 단위 섞임 방지).</summary>
    public event System.Action? MoveDragStarting;

    // ── 회전 중심 십자 손잡이(회전 세션 중에만 보임 · 끌어서 중심 옮기기) + 「지도에서 고르기」 ──
    private readonly Thumb _pivotHandle;
    private const double PivotHandleSize = 18d;
    private bool _pivPressed;
    private bool _pivDragging;
    private Point _pivPressPoint;
    private Vector _pivGrabOffset;     // 누른 점 → 중심 화면 자리(손잡이 가장자리를 잡아도 중심이 튀지 않게)
    private bool _pickingPivot;

    /// <summary>중심 십자 끌기가 데드존을 넘어 시작됨.</summary>
    public event System.Action? PivotDragStarted;
    /// <summary>중심 십자 끌기 중 새 중심(위경도).</summary>
    public event System.Action<PointLatLng>? PivotDragDelta;
    /// <summary>중심 십자 끌기 끝(true=놓음, false=캡처 잃음 → 끌기 전 중심으로).</summary>
    public event System.Action<bool>? PivotDragFinished;
    /// <summary>「지도에서 고르기」 중 지도 클릭 — 그 자리(위경도).</summary>
    public event System.Action<PointLatLng>? PivotPicked;

    public GroupSelectionAdorner(GMapCustomControl map, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
        IsHitTestVisible = true;   // 선택 마커 위 hit 필요(그 외는 HitTestCore null로 투과)
        Cursor = Cursors.SizeAll;  // adorner는 HitTestCore로 선택 마커 위에서만 이벤트 수신 → 이동커서도 거기서만 표시

        _boxPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 170, 255)), 1.5d) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        _boxPen.Freeze();

        _rotateHandle = CreateRotateHandle();
        _pivotHandle = CreatePivotHandle();
        _children = new VisualCollection(this) { _rotateHandle };   // 중심 손잡이는 회전 세션 동안만 자식(SetRotationState)

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌 생존)
        _map.OnMapDrag += OnMapChanged;
        _map.SubscribeViewport(OnViewportSnapshot);   // 회전 통지(R-20) — Dispose서 해제

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

    private void OnMapChanged() => Refresh();
    private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot _) => Refresh();

    /// <summary>다시 그림 + 회전 손잡이 자리 재배치.</summary>
    private void Refresh() { InvalidateVisual(); InvalidateArrange(); }

    public void SetMarkers(IReadOnlyList<IEditableMarker> markers)
    {
        _markers = markers ?? Array.Empty<IEditableMarker>();
        Refresh();
    }

    #region - 묶음 회전 손잡이 -

    /// <summary>회전 손잡이 — 속 빈 원(형태로 구분: 단일 편집의 채운 회전점과 다름). 색은 테마 토큰 동적 참조(테마 전환 즉시 반영).</summary>
    private static Thumb CreateRotateHandle()
    {
        var ring = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        ring.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 2d);
        ring.SetValue(System.Windows.Shapes.Shape.FillProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        ring.SetValue(System.Windows.Shapes.Shape.StrokeProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        var thumb = new Thumb
        {
            Width = RotateHandleSize,
            Height = RotateHandleSize,
            Cursor = Cursors.Hand,
            Template = new ControlTemplate(typeof(Thumb)) { VisualTree = ring },
        };
        thumb.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
        thumb.SetResourceReference(Control.BorderBrushProperty, "PrimaryBrush");
        AutomationProperties.SetAutomationId(thumb, "Map.GroupSelection.RotateHandle");
        AutomationProperties.SetName(thumb, "선택 묶음 회전");
        return thumb;
    }

    /// <summary>회전 중심 손잡이 — 속 빈 원(가운데 "+" 는 OnRender 가 그림). 속은 투명 채움(누르기 받음), 색은 테마 토큰 동적 참조.</summary>
    private static Thumb CreatePivotHandle()
    {
        var ring = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        ring.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.5d);
        ring.SetValue(System.Windows.Shapes.Shape.FillProperty, Brushes.Transparent);
        ring.SetValue(System.Windows.Shapes.Shape.StrokeProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        var thumb = new Thumb
        {
            Width = PivotHandleSize,
            Height = PivotHandleSize,
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed,
            Template = new ControlTemplate(typeof(Thumb)) { VisualTree = ring },
        };
        thumb.SetResourceReference(Control.BorderBrushProperty, "PrimaryBrush");
        AutomationProperties.SetAutomationId(thumb, "Map.GroupRotation.CenterHandle");
        AutomationProperties.SetName(thumb, "회전 중심");
        return thumb;
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        var c = RotateHandleCenter();
        _rotateHandle.Visibility = c.HasValue ? Visibility.Visible : Visibility.Collapsed;
        var p = c ?? new Point();
        _rotateHandle.Arrange(new Rect(p.X - RotateHandleSize / 2d, p.Y - RotateHandleSize / 2d, RotateHandleSize, RotateHandleSize));

        var pc = PivotScreen();
        _pivotHandle.Visibility = pc.HasValue ? Visibility.Visible : Visibility.Collapsed;
        var q = pc ?? new Point();
        _pivotHandle.Arrange(new Rect(q.X - PivotHandleSize / 2d, q.Y - PivotHandleSize / 2d, PivotHandleSize, PivotHandleSize));
        return finalSize;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _rotateHandle.Measure(new Size(RotateHandleSize, RotateHandleSize));
        _pivotHandle.Measure(new Size(PivotHandleSize, PivotHandleSize));
        return base.MeasureOverride(constraint);
    }

    /// <summary>회전 중심의 화면 자리 — 세션이 없으면 null.</summary>
    private Point? PivotScreen()
    {
        if (_rotationPivot is not PointLatLng pivot) return null;
        try { var g = _map.FromLatLngToLocal(pivot); return new Point(g.X, g.Y); }
        catch { return null; }
    }

    /// <summary>회전 대상(잠금 제외) 화면 사각형 합집합 — 없으면 null.</summary>
    private Rect? SelectionBounds()
    {
        Rect? u = null;
        foreach (var m in _markers)
        {
            if (m == null || m.IsDisposed || m.IsLocked) continue;
            var r = MarkerRect(m);
            if (r.IsEmpty) continue;
            if (u is Rect cur) { cur.Union(r); u = cur; } else u = r;
        }
        return u;
    }

    /// <summary>쉬는 자리 = 묶음 위쪽 가운데에서 단일 회전점과 같은 거리(MarkerEditSettings.RotateHandleDistance) 위.</summary>
    private Point? RestHandleCenter()
    {
        if (SelectionBounds() is not Rect b) return null;
        return new Point(b.Left + b.Width / 2d, b.Top - MarkerEditAdorner.PADDING - MarkerEditSettings.RotateHandleDistance);
    }

    /// <summary>현재 손잡이 중심 — 회전 중이면 시작 자리를 중심 기준으로 회전량만큼 돌린 자리.</summary>
    private Point? RotateHandleCenter()
    {
        try
        {
            if (_rotationPivot is not PointLatLng pivot) return RestHandleCenter();
            var ps = _map.FromLatLngToLocal(pivot);
            var rs = _map.FromLatLngToLocal(_rotHandleRestGeo);
            var (x, y) = GroupRotationMath.RotateScreen(ps.X, ps.Y, rs.X, rs.Y, _rotationAngle);
            return new Point(x, y);
        }
        catch { return null; }
    }

    /// <summary>서비스가 회전 세션 상태를 알림 — pivot=null 이면 세션 끝(손잡이 쉬는 자리로).</summary>
    internal void SetRotationState(PointLatLng? pivot, double angle)
    {
        if (pivot.HasValue && !_rotationPivot.HasValue && RestHandleCenter() is Point rest)
            _rotHandleRestGeo = _map.FromLocalToLatLng((int)Math.Round(rest.X), (int)Math.Round(rest.Y));   // 세션 시작 자리 고정
        _rotationPivot = pivot;
        _rotationAngle = pivot.HasValue ? angle : 0d;
        bool showPivot = pivot.HasValue;
        if (showPivot && !_children.Contains(_pivotHandle)) _children.Add(_pivotHandle);
        else if (!showPivot && _children.Contains(_pivotHandle)) _children.Remove(_pivotHandle);
        InvalidateMeasure();
        Refresh();
    }

    /// <summary>「지도에서 고르기」 켜기/끄기 — 켜면 지도 전체 눌림을 받아 그 자리를 중심으로 알린다(십자 커서).</summary>
    internal void SetPivotPicking(bool on)
    {
        _pickingPivot = on;
        Cursor = on ? Cursors.Cross : Cursors.SizeAll;
    }

    /// <summary>회전 드래그 강제 종료(ESC 취소 등) — 이벤트 없이 플래그 · 캡처만 정리.</summary>
    internal void AbortRotationDrag()
    {
        if (!_rotPressed && !_pivPressed) return;
        _rotPressed = false;
        _rotDragging = false;
        _pivPressed = false;
        _pivDragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private bool IsIn(DependencyObject d, Visual target)
    {
        for (DependencyObject? cur = d; cur != null; cur = VisualTreeHelper.GetParent(cur))
            if (ReferenceEquals(cur, target)) return true;
        return false;
    }

    /// <summary>중심 십자 끌기 이동 — 데드존(8 DIU) 통과 뒤 시작, 새 중심 = 누른 자리 오프셋을 뺀 커서 자리.</summary>
    private void ProcessPivotMove(Point cur)
    {
        if (!_pivDragging)
        {
            if (!CameraPopupHubMath.IsDrag(cur.X - _pivPressPoint.X, cur.Y - _pivPressPoint.Y)) return;
            _pivDragging = true;
            PivotDragStarted?.Invoke();
        }
        var p = cur + _pivGrabOffset;
        PivotDragDelta?.Invoke(_map.FromLocalToLatLng((int)Math.Round(p.X), (int)Math.Round(p.Y)));
    }

    /// <summary>중심 십자 끌기 단일 종료점(Up · LostMouseCapture 공용).</summary>
    private void FinishPivotDrag(bool commit)
    {
        if (!_pivPressed) return;
        bool wasDragging = _pivDragging;
        _pivPressed = false;
        _pivDragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) PivotDragFinished?.Invoke(commit);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // 「지도에서 고르기」 — 다음 좌클릭 자리를 중심으로(이동 · 팬 · 선택 바꾸기로 새지 않게 여기서 소비).
        if (_pickingPivot && e.ChangedButton == MouseButton.Left)
        {
            var at = e.GetPosition(_map);
            e.Handled = true;
            PivotPicked?.Invoke(_map.FromLocalToLatLng((int)Math.Round(at.X), (int)Math.Round(at.Y)));
            return;
        }
        // 중심 십자 눌림 — 회전 손잡이와 같은 선점 · 캡처 규칙
        if (e.ChangedButton == MouseButton.Left && !_pivPressed && !_rotPressed && _rotationPivot.HasValue
            && e.OriginalSource is DependencyObject psrc && IsIn(psrc, _pivotHandle) && PivotScreen() is Point pc)
        {
            _pivPressed = true;
            _pivDragging = false;
            _pivPressPoint = e.GetPosition(_map);
            _pivGrabOffset = pc - _pivPressPoint;
            CaptureMouse();
            e.Handled = true;
            return;
        }
        // 손잡이 눌림은 터널에서 선점 — Thumb 자체 캡처 · 아래 그룹 이동 · 지도 팬과 겹치지 않게(drag-first-ux Must Always).
        if (e.ChangedButton == MouseButton.Left && !_rotPressed && !ExclusiveInputModeGate.IsActive(_map)
            && e.OriginalSource is DependencyObject src && IsInRotateHandle(src))
        {
            _rotPressed = true;
            _rotDragging = false;
            _rotPressPoint = e.GetPosition(_map);
            CaptureMouse();
            e.Handled = true;
            return;
        }
        base.OnPreviewMouseDown(e);
    }

    private bool IsInRotateHandle(DependencyObject d)
    {
        for (DependencyObject? cur = d; cur != null; cur = VisualTreeHelper.GetParent(cur))
            if (ReferenceEquals(cur, _rotateHandle)) return true;
        return false;
    }

    /// <summary>회전 드래그 이동 — 데드존(8 DIU) 통과 뒤 시작, 각 = 중심 기준 누른 점 → 현재 점(Shift=15°).</summary>
    private void ProcessRotationMove(Point cur)
    {
        if (!_rotDragging)
        {
            if (!CameraPopupHubMath.IsDrag(cur.X - _rotPressPoint.X, cur.Y - _rotPressPoint.Y)) return;   // 데드존 미만 = 클릭
            _rotDragging = true;
            RotationDragStarted?.Invoke();
            if (_rotationPivot == null) { FinishRotationDrag(commit: false); return; }   // 회전할 멤버 없음(전원 잠금 등)
        }
        if (_rotationPivot is not PointLatLng pivot) return;
        var ps = _map.FromLatLngToLocal(pivot);
        bool snap = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        RotationDragDelta?.Invoke(GroupRotationMath.DragAngle(ps.X, ps.Y, _rotPressPoint.X, _rotPressPoint.Y, cur.X, cur.Y, snap));
    }

    /// <summary>회전 드래그 단일 종료점(Up · LostMouseCapture 공용). 순서: 플래그 clear → 캡처 해제 → 통지.</summary>
    private void FinishRotationDrag(bool commit)
    {
        if (!_rotPressed) return;
        bool wasDragging = _rotDragging;
        _rotPressed = false;
        _rotDragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) RotationDragFinished?.Invoke(commit);   // 데드존 미만(클릭)은 아무 일 없음
    }

    #endregion

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
            if (m == null || m.IsDisposed || m.IsLocked) continue;   // 잠금 멤버는 드래그/우클릭 hit 제외(클릭 투과)
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
                if (m == null || m.IsDisposed || m.IsLocked) continue;   // FR-MS-08: 잠금 멤버는 선택 점선박스 미표시
                RenderMarkerBox(dc, m);
            }
            RenderRotationGuide(dc);
        }
        catch (Exception ex) { _log?.Error($"GroupSelectionAdorner 렌더 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 회전 손잡이 줄기(점선 — 단일 편집 회전점과 같은 어휘) + 회전 중이면 중심 십자 · 중심→손잡이 점선 · 회전량 글.
    /// 색은 매번 테마 토큰 재해석(캐싱 금지 — 테마 전환 고착 방지).
    /// </summary>
    private void RenderRotationGuide(DrawingContext dc)
    {
        if (RotateHandleCenter() is not Point h) return;
        var primary = TryFindResource("PrimaryBrush") as Brush ?? Brushes.DeepSkyBlue;
        var guide = new Pen(primary, 1d) { DashStyle = DashStyles.Dot };

        if (_rotationPivot is not PointLatLng pivot)
        {
            if (SelectionBounds() is Rect b)
                dc.DrawLine(guide, new Point(h.X, h.Y + RotateHandleSize / 2d), new Point(h.X, b.Top - MarkerEditAdorner.PADDING));
            return;
        }

        var g = _map.FromLatLngToLocal(pivot);
        var c = new Point(g.X, g.Y);
        dc.DrawLine(guide, c, h);
        var cross = new Pen(primary, 2d);
        const double arm = 6d;
        dc.DrawLine(cross, new Point(c.X - arm, c.Y), new Point(c.X + arm, c.Y));
        dc.DrawLine(cross, new Point(c.X, c.Y - arm), new Point(c.X, c.Y + arm));

        var text = new FormattedText(GroupRotationMath.FormatAngle(_rotationAngle),
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12d, TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var chip = new Rect(h.X + RotateHandleSize, h.Y - text.Height / 2d - 2d, text.Width + 8d, text.Height + 4d);
        dc.DrawRoundedRectangle(TryFindResource("SurfaceBrush") as Brush ?? Brushes.Black, new Pen(primary, 1d), chip, 3d, 3d);
        dc.DrawText(text, new Point(chip.X + 4d, chip.Y + 2d));
    }

    /// <summary>
    /// 심볼 1개의 선택 박스 — 단일 편집 어도너(<see cref="MarkerEditAdorner"/>)와 **동일 공식**으로 그린다
    /// (Adorner_Box_Mismatch_Fix: 종전 TransformBounds AABB+1.5 인플레이트는 단일 박스와 크기·회전이 안 맞았다).
    /// · 라인/구역 컨트롤 = ActualLineBounds + PADDING (RenderEditArea의 CalculateLineEditBounds와 동일)
    /// · 그 외          = RenderSize(0,0,W,H) + PADDING (CalculateEditBounds와 동일)
    /// 요소→맵 변환을 PushTransform으로 적용해 회전 심볼도 단일 어도너처럼 심볼을 따라 도는 박스가 된다.
    /// 변환을 얻지 못하면 기존 AABB 폴백.
    /// </summary>
    private void RenderMarkerBox(DrawingContext dc, IEditableMarker m)
    {
        var shape = (m as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
        if (shape != null && shape.ActualWidth > 0 && shape.ActualHeight > 0)
        {
            // 단일 어도너와 같은 소스: 라인 계열은 폴리라인 점 bbox, 그 외는 컨트롤 렌더 크기
            Rect local = shape switch
            {
                GMapMarkerLineControl lc       => lc.ActualLineBounds,
                GMapMarkerPidsGroupControl gc  => gc.ActualLineBounds,
                _                              => new Rect(shape.RenderSize),
            };
            local.Inflate(MarkerEditAdorner.PADDING, MarkerEditAdorner.PADDING);

            try
            {
                if (!local.IsEmpty && local.Width > 0 && local.Height > 0
                    && shape.TransformToVisual(_map) is Transform t)
                {
                    dc.PushTransform(t);
                    dc.DrawRectangle(null, _boxPen, local);
                    dc.Pop();
                    return;
                }
            }
            catch { /* 비주얼트리 분리 등 → 아래 AABB 폴백 */ }
        }

        var r = MarkerRect(m);
        if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) return;
        r.Inflate(MarkerEditAdorner.PADDING, MarkerEditAdorner.PADDING);
        dc.DrawRectangle(null, _boxPen, r);
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // C12: 독점 입력 모드(라인드로잉·배치·조준·측정 등) 중엔 선택 마커 영역도 투과 — 눌림이 맵의 base-전 분기로 내려가야 한다(D-19 동일 계열).
        if (ExclusiveInputModeGate.IsActive(_map)) return null!;

        // 「지도에서 고르기」 중엔 지도 전체 눌림을 받는다(OnPreviewMouseDown 이 그 자리를 중심으로).
        if (_pickingPivot) return new PointHitTestResult(this, hitTestParameters.HitPoint);

        // Ctrl/Shift = 멀티셀렉션 토글·러버밴드(그룹 '이동' 아님) → adorner 투과시켜 아래 마커 Shape/GMapCustomControl이
        //   클릭을 받아 MarkerToggleRequested가 발화되게 함. (미투과 시 그룹 내 마커 위 Ctrl+클릭을 adorner가 e.Handled로
        //   삼켜 '제외'가 영구 불가 — 추가만 되고 제외/모든 시나리오 실패의 실제 원인)
        if ((System.Windows.Input.Keyboard.Modifiers
            & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)) != 0)
            return null!;
        return IsOverSelected(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;   // 선택 마커 외 투과(불변식#3)
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // C12: 독점 입력 모드 중엔 그룹 이동을 시작하지 않고 통과(GMapMarkerBaseControl D-19 가드와 같은 목록·같은 게이트).
        if (ExclusiveInputModeGate.IsActive(_map)) { base.OnMouseLeftButtonDown(e); return; }

        var p = e.GetPosition(this);
        if (IsOverSelected(p))   // 선택 심볼 아무거나 좌드래그 = 그룹 이동
        {
            MoveDragStarting?.Invoke();   // 열린 키보드 회전 먼저 확정(위치 스냅샷은 그 뒤)
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
        if (_rotPressed || _pivPressed)
        {
            if (_rotPressed) ProcessRotationMove(e.GetPosition(_map));
            else ProcessPivotMove(e.GetPosition(_map));
            e.Handled = true;
            base.OnMouseMove(e);
            return;
        }
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

    /// <summary>이동한 멤버의 (마커, 이동 전 위치) 목록 — Undo/영속용. 미이동(잠금) 멤버 제외.</summary>
    private IReadOnlyList<(IEditableMarker marker, PointLatLng before)> BuildMoveList()
    {
        var list = new List<(IEditableMarker marker, PointLatLng before)>();
        for (int i = 0; i < _markers.Count && i < _origPositions.Length; i++)
        {
            var m = _markers[i];
            if (m == null || m.IsDisposed) continue;
            var b = _origPositions[i];
            if (m.Position.Lat == b.Lat && m.Position.Lng == b.Lng) continue;   // 미이동(잠금 멤버)
            list.Add((m, b));
        }
        return list;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_rotPressed || _pivPressed)
        {
            if (_rotPressed) FinishRotationDrag(commit: true);   // 놓음 = 그 각에서 멈춤(저장은 컨트롤러 판 확정 · Enter)
            else FinishPivotDrag(commit: true);                  // 놓음 = 그 자리를 중심으로
            e.Handled = true;
            base.OnMouseLeftButtonUp(e);
            return;
        }
        if (_dragging)
        {
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
            GroupMoveCompleted?.Invoke(BuildMoveList());   // lock 밖 발화(멤버별 DB 영속·Undo는 서비스/VM)
        }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_rotPressed) FinishRotationDrag(commit: false);   // 캡처 잃음(창 전환 등) = 취소 · 원위치
        if (_pivPressed) FinishPivotDrag(commit: false);      // 캡처 잃음 = 끌기 전 중심으로
        if (_dragging) { _dragging = false; GroupMoveCompleted?.Invoke(BuildMoveList()); }
        base.OnLostMouseCapture(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsMouseCaptured) ReleaseMouseCapture();
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        _map.UnsubscribeViewport(OnViewportSnapshot);
        GroupMoveCompleted = null;
        GroupDeleteRequested = null;
        GroupLockRequested = null;
        GroupVisibilityRequested = null;
        GroupZOrderRequested = null;
        RotationDragStarted = null;
        RotationDragDelta = null;
        RotationDragFinished = null;
        MoveDragStarting = null;
        PivotDragStarted = null;
        PivotDragDelta = null;
        PivotDragFinished = null;
        PivotPicked = null;
    }
}
