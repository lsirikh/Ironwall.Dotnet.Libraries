using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 묶음 회전 컨트롤러 판(2026-10-18) — 선택 묶음의 회전 중심 · 각도를 보이고 바꾸고 확정/취소한다.
/// MapAnchorPanelControl(OverlayWindow 패턴) 답습 — Control + Themes/GroupRotationPanelStyle.xaml(ControlTemplate)
/// + 머리 띠 끌기(부모 ContentPresenter 를 Canvas 에서 이동, OverlayHeaderClamp) + DynamicResource 토큰(라이트/다크).
/// 폼은 컨트롤 DataContext(=MapViewModel)에 바인딩. 입력칸(UpdateSourceTrigger=LostFocus)은 {Enter} 로도 바로 넣는다.
/// </summary>
public class GroupRotationPanelControl : Control
{
    private const double HeaderHeight = 38;

    static GroupRotationPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupRotationPanelControl),
            new FrameworkPropertyMetadata(typeof(GroupRotationPanelControl)));
    }

    public GroupRotationPanelControl()
    {
        Width = 300;
        AutomationProperties.SetAutomationId(this, "Map.GroupRotation.Panel");
        AutomationProperties.SetName(this, "묶음 회전");
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += (_, _) => _isDragging = false;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    #region - Dependency Properties -
    /// <summary>머리 띠 닫기(✕) 커맨드 — 묶음 회전에서는 취소(원위치).</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(GroupRotationPanelControl), new PropertyMetadata(null));
    #endregion

    /// <summary>커스텀 Control 은 peer 가 없어 AutomationId 가 UIA 트리에 안 나온다 — 판 루트를 찾을 수 있게 peer 를 둔다(ui-automation 규칙).</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>입력칸 {Enter} =그 칸 값을 바로 넣기(칸을 떠나지 않아도). 판 밖 키(]/[ · Enter 확정)는 지도 키 처리기가 받는다.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        box.SelectAll();
        e.Handled = true;
    }

    #region - Header Drag (머리 띠 Y<=38 만, MapAnchorPanelControl 답습) -
    private bool _isDragging;
    private Point _lastMousePosition;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > HeaderHeight) return;     // 머리 띠만 끌기(본문 폼은 통과)
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
        var (newLeft, newTop) = Helpers.OverlayHeaderClamp.Clamp(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, this, canvas, HeaderHeight);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
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
