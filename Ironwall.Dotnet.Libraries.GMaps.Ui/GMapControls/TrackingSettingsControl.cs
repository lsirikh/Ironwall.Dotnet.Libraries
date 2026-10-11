using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
/****************************************************************************
   Purpose      : 추적 설정 오버레이 윈도우 (표준 Control + 드래그, 스타일=Themes/TrackingSettingsStyle.xaml)
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 설정 패널. 기존 오버레이 윈도우 패턴 — <c>Control</c> + <c>Themes/TrackingSettingsStyle.xaml</c> + 헤더 드래그.
/// <para>DataContext = <c>TrackingSetupViewModel</c>(MapViewModel 주입). 닫기=PART_CloseButton→VM CloseCommand.</para>
/// </summary>
public class TrackingSettingsControl : Control
{
    static TrackingSettingsControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TrackingSettingsControl),
            new FrameworkPropertyMetadata(typeof(TrackingSettingsControl)));
    }

    public TrackingSettingsControl()
    {
        InitializeDragSupport();
        KeyDown += OnEscapeKeyDown;
    }

    /// <summary>
    /// 겹창 안에서 Esc = ✕ 와 같은 길(CloseCommand) — 결함 AUX-D6(MAP-085 「✕/Esc → 닫힘」).
    /// 버블 KeyDown 이고 아직 처리되지 않은 Esc 만 받는다(열린 "?" 말풍선이 창 단에서 먼저 먹으면 겹창은 남는다).
    /// </summary>
    private void OnEscapeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        if (DataContext is not ViewModels.Maps.TrackingSetupViewModel vm || !vm.CloseCommand.CanExecute(null)) return;
        vm.CloseCommand.Execute(null);
        e.Handled = true;
    }

    #region - Drag (헤더 Y≤38만 드래그 → 부모 ContentPresenter를 Canvas 이동) -
    private bool _isDragging;
    private Point _lastMousePosition;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnDragDown;
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragUp;
    }

    private void OnDragDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > 38) return;
        if (FindParent<ContentPresenter>(this) is not { Parent: Canvas canvas } cp) return;
        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        if (FindParent<ContentPresenter>(this) is not { Parent: Canvas canvas } cp) return;
        var cur = e.GetPosition(canvas);
        var left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
        var top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
        // 머리(38)가 지도 안에 남도록 자른다(AUX-D7 · MAP-191)
        var (newLeft, newTop) = Helpers.OverlayHeaderClamp.Clamp(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, this, canvas, 38);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
        _lastMousePosition = cur;
    }

    private void OnDragUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var p = VisualTreeHelper.GetParent(child);
        while (p != null && p is not T) p = VisualTreeHelper.GetParent(p);
        return p as T;
    }
    #endregion
}
