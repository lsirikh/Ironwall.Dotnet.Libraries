using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 맵 위 이동식 RTSP 스트리밍 팝업 CustomControl(관심지역/레이어 창 드래그 패턴 답습).
/// DataContext = <see cref="CameraStreamPopupViewModel"/>.
/// <para>
/// 좌버튼: 헤더(Y≤42) 드래그로 부모 Canvas 내 창 이동(VM.CanvasLeft/Top).
/// 우버튼(영상 영역, CameraPopup_PTZ_Control): 드래그 = PTZ 방향 이동(벡터 오버레이→릴리즈 시 RelativeMove),
/// 8px 미만 짧은클릭 = 탭 패널 토글. IsPtzCapable=false면 차단.
/// </para>
/// </summary>
public class CameraStreamPopupControl : Control
{
    private const double HeaderHeight = 42;     // 헤더(창이동 드래그) 높이 — Style 헤더 Row와 일치
    private const double PtzDragThreshold = 8;   // 드래그/짧은클릭 구분 데드존(DIU, FR-DRAG-01)
    private const double PtzMaxLengthRatio = 0.4; // 화살표 최대 길이 = 영상 단축변 40% 캡

    // 좌버튼 창이동 상태
    private bool _isDragging;
    private Point _lastMousePosition;

    // 우버튼 PTZ 드래그 상태
    private FrameworkElement? _videoRegion;
    private Canvas? _ptzOverlay;
    private Line? _ptzShaft;
    private Polygon? _ptzHead;
    private bool _ptzActive;
    private bool _isPtzDragging;
    private Point _ptzStart;

    static CameraStreamPopupControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CameraStreamPopupControl),
            new FrameworkPropertyMetadata(typeof(CameraStreamPopupControl)));
    }

    /// <summary>헤더 타이틀 — TemplateBinding으로 노출(MapView가 {Binding Title} 주입).</summary>
    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(nameof(PanelTitle), typeof(string),
            typeof(CameraStreamPopupControl), new PropertyMetadata(string.Empty));

    public CameraStreamPopupControl()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        // 우버튼 = PTZ 전용(드래그=이동, 짧은클릭=탭패널). (FR-DRAG-07)
        MouseRightButtonDown += OnPtzButtonDown;
        MouseRightButtonUp += OnPtzButtonUp;
        LostMouseCapture += OnLostMouseCapture;
        Focusable = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) =>
            {
                if (DataContext is CameraStreamPopupViewModel vm)
                    vm.CloseCommand.Execute(null);
            };

        _videoRegion = GetTemplateChild("PART_VideoRegion") as FrameworkElement;
        _ptzOverlay = GetTemplateChild("PART_PtzOverlay") as Canvas;
    }

    /*──────────────── 좌버튼: 창 이동(헤더) ────────────────*/

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 좌클릭(헤더/영상 어디든) = 이 팝업 선택 + 맨앞. (FR-SEL-01)
        (DataContext as CameraStreamPopupViewModel)?.RaiseSelectRequested();

        var position = e.GetPosition(this);
        if (position.Y > HeaderHeight) return;   // 헤더만 창이동 드래그

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // 우버튼 PTZ 드래그 우선 처리
        if (_ptzActive)
        {
            HandlePtzMove(e);
            return;
        }

        if (!_isDragging) return;
        if (DataContext is not CameraStreamPopupViewModel vm) return;

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        var cur = e.GetPosition(canvas);
        var dx = cur.X - _lastMousePosition.X;
        var dy = cur.Y - _lastMousePosition.Y;

        var left = double.IsNaN(vm.CanvasLeft) ? 0 : vm.CanvasLeft;
        var top = double.IsNaN(vm.CanvasTop) ? 0 : vm.CanvasTop;

        var newLeft = left + dx;
        var newTop = top + dy;

        // 경계 clamp — 팝업이 Canvas 밖으로 사라지지 않게
        var maxLeft = canvas.ActualWidth - ActualWidth;
        var maxTop = canvas.ActualHeight - ActualHeight;
        if (maxLeft > 0) newLeft = Math.Min(Math.Max(0, newLeft), maxLeft);
        if (maxTop > 0) newTop = Math.Min(Math.Max(0, newTop), maxTop);

        vm.CanvasLeft = newLeft;
        vm.CanvasTop = newTop;
        _lastMousePosition = cur;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
        (DataContext as CameraStreamPopupViewModel)?.RaiseDragCompleted();
    }

    /*──────────────── 우버튼: PTZ 드래그 / 탭 패널 ────────────────*/

    private void OnPtzButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CameraStreamPopupViewModel vm) return;

        // PTZ 미지원 → 입력 차단(오버레이/메뉴 없음). (FR-GATE-01/03)
        if (!vm.IsPtzCapable) { e.Handled = true; return; }

        // 헤더 영역 우클릭은 무시(영상 영역만 PTZ)
        if (e.GetPosition(this).Y <= HeaderHeight) return;
        if (_videoRegion == null) return;

        _ptzActive = true;
        _isPtzDragging = false;
        _ptzStart = e.GetPosition(_videoRegion);
        CaptureMouse();
        Keyboard.Focus(this);   // ESC 취소 수신용
        e.Handled = true;
    }

    private void HandlePtzMove(MouseEventArgs e)
    {
        if (_videoRegion == null) return;
        var cur = e.GetPosition(_videoRegion);
        var dx = cur.X - _ptzStart.X;
        var dy = cur.Y - _ptzStart.Y;

        if (!_isPtzDragging && (dx * dx + dy * dy) > (PtzDragThreshold * PtzDragThreshold))
            _isPtzDragging = true;

        if (_isPtzDragging)
            DrawPtzVector(_ptzStart, cur);
    }

    private void OnPtzButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_ptzActive) return;
        _ptzActive = false;
        ReleaseMouseCapture();

        var vm = DataContext as CameraStreamPopupViewModel;
        if (_isPtzDragging && _videoRegion != null && vm != null)
        {
            var cur = e.GetPosition(_videoRegion);
            var dx = cur.X - _ptzStart.X;
            var dy = cur.Y - _ptzStart.Y;
            // 릴리즈 시 단 1회 RelativeMove 요청(MapViewModel이 IPtzController 호출). (FR-DRAG-03)
            vm.RaisePtzDrag(dx, dy, _videoRegion.ActualWidth, _videoRegion.ActualHeight);
        }
        else if (!_isPtzDragging && vm != null)
        {
            // 8px 미만 짧은클릭 → 탭 패널 토글. (FR-UI-01)
            vm.TogglePanel();
        }

        _isPtzDragging = false;
        ClearPtzVector();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        // 포커스 이탈/캡처 분실 → 드래그 자동 취소(RelativeMove 미호출). (FR-DRAG-05)
        if (!_ptzActive) return;
        _ptzActive = false;
        _isPtzDragging = false;
        ClearPtzVector();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // 드래그 중 ESC → 취소(창닫기 충돌 방지 위해 가로채기). (FR-DRAG-05)
        if (_ptzActive && e.Key == Key.Escape)
        {
            _ptzActive = false;
            _isPtzDragging = false;
            ReleaseMouseCapture();
            ClearPtzVector();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    /*──────────────── 벡터 오버레이(화살표 + 색상 게이지) ────────────────*/

    private void DrawPtzVector(Point start, Point cur)
    {
        if (_ptzOverlay == null || _videoRegion == null) return;

        var dx = cur.X - start.X;
        var dy = cur.Y - start.Y;
        var mag = Math.Sqrt(dx * dx + dy * dy);
        if (mag < 0.0001) { ClearPtzVector(); return; }

        var maxLen = PtzMaxLengthRatio * Math.Min(_videoRegion.ActualWidth, _videoRegion.ActualHeight);
        if (maxLen <= 0) maxLen = mag;
        var capLen = Math.Min(mag, maxLen);
        var frac = capLen / maxLen;            // 0..1 강도

        var angle = Math.Atan2(dy, dx);
        var end = new Point(start.X + Math.Cos(angle) * capLen, start.Y + Math.Sin(angle) * capLen);

        var brush = MagnitudeBrush(frac);

        // 화살표 몸통
        _ptzShaft ??= AddShaft();
        _ptzShaft.X1 = start.X; _ptzShaft.Y1 = start.Y;
        _ptzShaft.X2 = end.X; _ptzShaft.Y2 = end.Y;
        _ptzShaft.Stroke = brush;
        _ptzShaft.Visibility = Visibility.Visible;

        // 화살촉(삼각형)
        const double headLen = 12, spread = 0.45;
        var p2 = new Point(end.X - Math.Cos(angle - spread) * headLen, end.Y - Math.Sin(angle - spread) * headLen);
        var p3 = new Point(end.X - Math.Cos(angle + spread) * headLen, end.Y - Math.Sin(angle + spread) * headLen);
        _ptzHead ??= AddHead();
        _ptzHead.Points = new PointCollection { end, p2, p3 };
        _ptzHead.Fill = brush;
        _ptzHead.Visibility = Visibility.Visible;
    }

    private Line AddShaft()
    {
        var line = new Line { StrokeThickness = 4, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
        _ptzOverlay!.Children.Add(line);
        return line;
    }

    private Polygon AddHead()
    {
        var poly = new Polygon { IsHitTestVisible = false };
        _ptzOverlay!.Children.Add(poly);
        return poly;
    }

    private void ClearPtzVector()
    {
        if (_ptzShaft != null) _ptzShaft.Visibility = Visibility.Collapsed;
        if (_ptzHead != null) _ptzHead.Visibility = Visibility.Collapsed;
    }

    /// <summary>강도(0=초록 ~ 1=빨강) 색상 브러시. 0.5=노랑(green→yellow→red).</summary>
    private static Brush MagnitudeBrush(double frac)
    {
        frac = frac < 0 ? 0 : (frac > 1 ? 1 : frac);
        byte r = (byte)Math.Min(255, 510 * frac);
        byte g = (byte)Math.Min(255, 510 * (1 - frac));
        var brush = new SolidColorBrush(Color.FromRgb(r, g, 0));
        brush.Freeze();
        return brush;
    }

    /*──────────────── 부모 Canvas 탐색(창이동 clamp용) ────────────────*/

    private Canvas? FindParentCanvas()
    {
        DependencyObject? parent = this;
        Canvas? firstCanvas = null;
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is Canvas canvas)
            {
                firstCanvas ??= canvas;
                if (canvas.ActualWidth > 0 || canvas.ActualHeight > 0)
                    return canvas;
            }
        }
        return firstCanvas;
    }
}
