using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 지도 겹창 머리 띠 끌기(공용) — 머리 띠(Y ≤ headerHeight)를 누르면 부모 ContentPresenter 를
                  Canvas 안에서 옮긴다(OverlayHeaderClamp 로 머리가 지도 안에 남게). 본문 폼은 통과.
                  사이트 고정 창(MapAnchorPanelControl) · 묶음 회전 판(GroupRotationPanelControl)이 함께 쓴다.
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/
internal sealed class OverlayHeaderDrag
{
    private readonly FrameworkElement _panel;
    private readonly double _headerHeight;
    private bool _isDragging;
    private Point _lastMousePosition;

    private OverlayHeaderDrag(FrameworkElement panel, double headerHeight)
    {
        _panel = panel;
        _headerHeight = headerHeight;
    }

    /// <summary>겹창에 머리 띠 끌기를 붙인다.</summary>
    public static void Attach(FrameworkElement panel, double headerHeight)
    {
        var d = new OverlayHeaderDrag(panel, headerHeight);
        panel.MouseLeftButtonDown += d.OnMouseLeftButtonDown;
        panel.MouseMove += d.OnMouseMove;
        panel.MouseLeftButtonUp += d.OnMouseLeftButtonUp;
        panel.LostMouseCapture += (_, _) => d._isDragging = false;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(_panel).Y > _headerHeight) return;    // 머리 띠만 끌기(본문 폼은 통과)
        var cp = FindParent<ContentPresenter>(_panel);
        if (cp?.Parent is not Canvas canvas) return;
        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        _panel.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cp = FindParent<ContentPresenter>(_panel);
        if (cp?.Parent is not Canvas canvas) return;
        var cur = e.GetPosition(canvas);
        double left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
        double top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
        // 머리가 지도 안에 남도록 자른다(AUX-D7 · MAP-191)
        var (newLeft, newTop) = OverlayHeaderClamp.Clamp(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, _panel, canvas, _headerHeight);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
        _panel.InvalidateMeasure();   // 자리 아래 남은 높이로 판을 자르는 겹창(HeightPanelControlBase)이 다시 재게 — Canvas.Top 은 자식을 다시 재지 않는다
        _lastMousePosition = cur;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _panel.ReleaseMouseCapture();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }
}
