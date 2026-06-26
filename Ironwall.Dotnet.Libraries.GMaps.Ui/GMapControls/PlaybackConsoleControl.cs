using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
/****************************************************************************
   Purpose      : Playback 콘솔 오버레이 윈도우 (표준 Control + 드래그, 스타일=Themes/PlaybackConsoleStyle.xaml)
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 재생 콘솔. 기존 오버레이 윈도우(<see cref="LayerPanelControl"/> 등)와 동일한 패턴 —
/// <c>Control</c> + <c>Themes/PlaybackConsoleStyle.xaml</c> ControlTemplate + <b>헤더 드래그 이동</b>.
/// <para>DataContext = <c>PlaybackViewModel</c>(MapViewModel이 주입). 닫기=헤더 PART_CloseButton→VM CloseCommand.</para>
/// </summary>
public class PlaybackConsoleControl : Control
{
    static PlaybackConsoleControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PlaybackConsoleControl),
            new FrameworkPropertyMetadata(typeof(PlaybackConsoleControl)));
    }

    public PlaybackConsoleControl() => InitializeDragSupport();

    #region - Drag (헤더 Y≤38만 드래그 → 부모 ContentPresenter를 Canvas 이동) -
    private bool _isDragging;
    private Point _lastMousePosition;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnDragDown;
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragUp;
    }

    // 헤더 내 Button(닫기)은 MouseLeftButtonDown을 handled 처리 → 이 핸들러 미실행(드래그 안 걸림)
    private void OnDragDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > 38) return;          // 헤더(38)만 드래그
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
        Canvas.SetLeft(cp, left + cur.X - _lastMousePosition.X);
        Canvas.SetTop(cp, top + cur.Y - _lastMousePosition.Y);
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
