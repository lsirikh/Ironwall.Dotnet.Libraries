using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 사이트 고정(맵 앵커) 설정 오버레이 패널.
/// LayerPanelControl 패턴 답습 — Control + Themes/MapAnchorPanelStyle.xaml(ControlTemplate)
/// + 헤더 드래그(부모 ContentPresenter를 Canvas에서 이동) + DynamicResource 토큰(다크/라이트).
/// 폼 필드는 컨트롤 DataContext(=MapViewModel)에 바인딩. 고정 폼이라 리사이즈는 생략.
/// </summary>
public class MapAnchorPanelControl : Control
{
    static MapAnchorPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapAnchorPanelControl),
            new FrameworkPropertyMetadata(typeof(MapAnchorPanelControl)));
    }

    public MapAnchorPanelControl()
    {
        Width = 320;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    #region - Dependency Properties -
    /// <summary>헤더 제목.</summary>
    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }
    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(nameof(PanelTitle), typeof(string),
            typeof(MapAnchorPanelControl), new PropertyMetadata("사이트 고정 (Map Anchor)"));

    /// <summary>헤더 닫기(✕) 커맨드 — 보통 VM의 패널 토글 커맨드.</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(MapAnchorPanelControl), new PropertyMetadata(null));
    #endregion

    #region - Header Drag (헤더 영역 Y<=36 만, LayerPanelControl 답습) -
    private bool _isDragging;
    private Point _lastMousePosition;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > 36) return;                 // 헤더 영역만 드래그(본문 폼은 통과)
        var cp = FindParent<ContentPresenter>(this);
        if (cp?.Parent is not Canvas canvas) return;
        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cp = FindParent<ContentPresenter>(this);
        if (cp?.Parent is not Canvas canvas) return;
        var cur = e.GetPosition(canvas);
        double left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
        double top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
        Canvas.SetLeft(cp, left + cur.X - _lastMousePosition.X);
        Canvas.SetTop(cp, top + cur.Y - _lastMousePosition.Y);
        _lastMousePosition = cur;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }
    #endregion
}
