using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 「이미지 자르기」 판(2026-10-19) — 오버레이 이미지를 다각형으로 자른다. BuildingHeightPanelControl(OverlayWindow 패턴) 답습 —
/// Control + Themes/ImageCropPanelStyle.xaml + 머리 띠 끌기(OverlayHeaderDrag) + DynamicResource 토큰(라이트/다크). DataContext = ImageCropViewModel.
/// 판 안 키는 VM(HandleKey)으로, Space 를 누르고 있는 동안은 그림 캔버스에서 왼쪽 끌기 = 이동.
/// </summary>
public class ImageCropPanelControl : Control
{
    static ImageCropPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageCropPanelControl),
            new FrameworkPropertyMetadata(typeof(ImageCropPanelControl)));
    }

    public ImageCropPanelControl()
    {
        Width = 760;
        Height = 600;
        Focusable = true;
        AutomationProperties.SetAutomationId(this, "Map.ImageCrop.Panel");
        AutomationProperties.SetName(this, "이미지 자르기");
        Helpers.OverlayHeaderDrag.Attach(this, 38);   // 머리 띠(38)만 끌기
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Focus();   // 열리면 판 안 키(Enter · Esc · Ctrl+Z …)를 바로 받게
        PreviewKeyUp += (_, e) => { if (e.Key == Key.Space && FindCanvas() is { } c) { c.IsSpaceHeld = false; e.Handled = true; } };
    }

    #region - Dependency Properties -
    /// <summary>머리 띠 닫기(✕) 커맨드 — 취소와 같다.</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(ImageCropPanelControl), new PropertyMetadata(null));
    #endregion

    /// <summary>커스텀 Control 은 peer 가 없어 AutomationId 가 UIA 트리에 안 나온다 — 판 루트를 찾을 수 있게 peer 를 둔다.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private ImageCropCanvas? FindCanvas() => FindDescendant<ImageCropCanvas>(this);

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    /// <summary>판 안 키 — 끄는 중 Esc = 끌기 취소, Space = 이동 모드, 나머지는 VM(Enter · Backspace · Delete · Esc · Ctrl+Z/Y).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ImageCropViewModel vm) return;
        var canvas = FindCanvas();
        if (e.Key == Key.Space)
        {
            if (canvas != null) canvas.IsSpaceHeld = true;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && canvas?.CancelDrag() == true) { e.Handled = true; return; }
        // 단추에 포커스가 있을 때 Enter/Space 는 단추 몫
        if (e.Key == Key.Enter && e.OriginalSource is Button) return;
        e.Handled = vm.HandleKey(e.Key, Keyboard.Modifiers);
    }
}

/// <summary>
/// 「이미지 자르기」 그림 캔버스(AutomationId Map.ImageCrop.Canvas) — 그림 · 다각형 · 손잡이를 그리고 마우스를 VM 으로 넘긴다.
/// ★ 왼쪽 누르기 → 8 DIU 넘게 움직이면 꼭짓점/가운데 손잡이 끌기(캡처), 아니면 클릭(점 추가 · 첫 점 = 닫기). 두 번 누르기 = 닫기.
/// ★ 오른쪽 = 꼭짓점 지우기, 휠 = 커서 기준 확대/축소, 가운데 단추 끌기 · Space+끌기 = 이동. 마우스 이벤트는 모두 여기서 끝(지도 이동으로 새지 않게).
/// ★ 그리는 중 선 = 점선 {5,3}(앱의 '그리는 중 미리보기' 와 같은 뜻), 닫힌 다각형 = 실선 + 바깥 어둡게(ScrimModalBrush).
/// </summary>
public class ImageCropCanvas : FrameworkElement
{
    private ImageCropViewModel? _vm;
    private bool _needsFit = true;
    private Point? _panFrom;
    private Point? _cursor;

    public ImageCropCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        DataContextChanged += (_, e) =>
        {
            if (_vm != null) _vm.Changed -= OnVmChanged;
            _vm = e.NewValue as ImageCropViewModel;
            if (_vm != null) _vm.Changed += OnVmChanged;
            _needsFit = true;
            FitIfNeeded();
            InvalidateVisual();
        };
    }

    /// <summary>Space 를 누르고 있는가(판이 알려 준다) — 왼쪽 끌기가 이동이 된다.</summary>
    public bool IsSpaceHeld { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private void OnVmChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        FitIfNeeded();
    }

    private void FitIfNeeded()
    {
        if (_vm == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        _vm.ViewportWidth = ActualWidth;
        _vm.ViewportHeight = ActualHeight;
        if (!_needsFit) return;
        _needsFit = false;
        _vm.Fit(ActualWidth, ActualHeight);
    }

    /// <summary>끄는 중이면 끌기 취소 — 처리했으면 true.</summary>
    public bool CancelDrag()
    {
        if (_vm?.IsDragging != true) return false;
        _vm.PointerCancel();
        if (IsMouseCaptured) ReleaseMouseCapture();
        return true;
    }

    private Brush Token(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Token("SurfaceSunkenBrush", Brushes.DimGray), null, new Rect(RenderSize));
        if (_vm is not { } vm) return;

        var imageRect = new Rect(vm.ImageToScreen(new Point(0, 0)), vm.ImageToScreen(new Point(vm.PixelWidth, vm.PixelHeight)));
        dc.DrawImage(vm.Image, imageRect);

        var points = vm.Points.Select(vm.ImageToScreen).ToList();
        var primary = Token("PrimaryBrush", Brushes.DeepSkyBlue);
        var surface = Token("SurfaceBrush", Brushes.White);
        if (points.Count == 0) return;

        var figure = new PathFigure { StartPoint = points[0], IsClosed = vm.IsClosed };
        foreach (var p in points.Skip(1)) figure.Segments.Add(new LineSegment(p, true));
        var outline = new PathGeometry(new[] { figure });

        if (vm.IsClosed)
        {
            // 자를 바깥 = 어둡게(그림 상자 − 다각형), 경계 = 실선(바깥 어두운 띠 + 안쪽 Primary)
            dc.DrawGeometry(Token("ScrimModalBrush", new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0))), null,
                new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(imageRect), outline));
            dc.DrawGeometry(null, new Pen(Brushes.Black, 4) { LineJoin = PenLineJoin.Round }, outline);
            dc.DrawGeometry(null, new Pen(primary, 2) { LineJoin = PenLineJoin.Round }, outline);
        }
        else
        {
            var dashed = new Pen(primary, 2) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) };
            dc.DrawGeometry(null, dashed, outline);
            if (_cursor is { } c && !vm.IsDragging) dc.DrawLine(dashed, points[^1], c);   // 다음 점까지 미리보기
        }

        // 선분 가운데 손잡이(작은 네모, 속 빈) — 끌면 점 추가
        var handlePen = new Pen(primary, 1.5);
        foreach (var (_, mid) in vm.Midpoints())
        {
            var s = vm.ImageToScreen(mid);
            dc.DrawRectangle(surface, handlePen, new Rect(s.X - 3.5, s.Y - 3.5, 7, 7));
        }

        // 꼭짓점(원). 첫 점은 닫을 수 있을 때 크게(닫기 자리), 고른 점은 채움, 끄는 점은 두꺼운 테두리 — 색이 아니라 모양으로
        bool closable = !vm.IsClosed && points.Count >= 3;
        for (int i = 0; i < points.Count; i++)
        {
            double r = i == 0 && closable ? 7 : 5;
            var fill = i == vm.SelectedVertex ? primary : surface;
            var pen = new Pen(primary, i == vm.DraggingVertex ? 3.5 : 2);
            dc.DrawEllipse(fill, pen, points[i], r, r);
        }
    }

    // ── 마우스 ──

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (_vm is not { } vm) return;
        Focus();
        var p = e.GetPosition(this);
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && IsSpaceHeld))
        {
            _panFrom = p;
            CaptureMouse();
        }
        else if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount >= 2) vm.DoubleClick();
            else
            {
                vm.PointerDown(p);
                CaptureMouse();
            }
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_vm is not { } vm) return;
        var p = e.GetPosition(this);
        _cursor = p;
        if (_panFrom is { } from)
        {
            vm.PanBy(p.X - from.X, p.Y - from.Y);
            _panFrom = p;
        }
        else if (IsMouseCaptured) vm.PointerMove(p);
        else InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_vm is not { } vm) return;
        var p = e.GetPosition(this);
        if (_panFrom != null && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
        {
            _panFrom = null;
            ReleaseMouseCapture();
        }
        else if (e.ChangedButton == MouseButton.Left && IsMouseCaptured)
        {
            vm.PointerUp(p);
            ReleaseMouseCapture();
        }
        else if (e.ChangedButton == MouseButton.Right) vm.RightClick(p);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _panFrom = null;
        _vm?.PointerCancel();   // 놓기 전에 캡처를 잃으면 끌기 전 모양으로(놓기에서 이미 끝났으면 아무것도 안 함)
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _cursor = null;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _vm?.ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.2 : 1 / 1.2);
        e.Handled = true;
    }
}
