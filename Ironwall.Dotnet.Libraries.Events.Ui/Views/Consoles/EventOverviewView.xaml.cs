using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 개요(T3) — 추이 차트 위를 <b>좌우로 끌어 기간을 고른다</b>(정본 all-windows-drag-wireframe.html L299 · L427).
/// </summary>
/// <remarks>
/// <para><b>캡처 드래그</b>다 — OLE <c>DoDragDrop</c> 은 쓰지 않는다. 데드존은 <see cref="DragMath.DeadZone"/>(8.0 DIU)
/// 을 그대로 쓰고(새 상수를 만들지 않는다), 그 미만은 드래그가 아니라 클릭이다.</para>
/// <para>종료는 <see cref="FinishRangeDrag"/> 하나로 모으고 <b>놓음 · 캡처 상실</b> 양쪽에서 부른다.
/// 순서는 ①플래그 ②시각 복원 ③캡처 해제 ④커밋 — 캡처를 먼저 풀면 재진입한다.</para>
/// <para>Esc 는 터널(<c>PreviewKeyDown</c>)에서 <b>끄는 중일 때만</b> 소비한다 — 무조건 소비하면 다른 Esc 동작이 깨진다.</para>
/// </remarks>
public partial class EventOverviewView : UserControl
{
    private bool _pressed;
    private bool _dragging;
    private Point _pressPoint;
    private FrameworkElement? _plot;

    public EventOverviewView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private EventOverviewViewModel? Model => DataContext as EventOverviewViewModel;

    private void OnTrendLoaded(object sender, RoutedEventArgs e)
    {
        // 배선 탐색은 Loaded 에서 — OnAttached 시점에는 부모 체인이 없다.
        _plot = sender as FrameworkElement;
        PushSize();
    }

    private void OnTrendSizeChanged(object sender, SizeChangedEventArgs e) => PushSize();

    private void PushSize()
    {
        if (_plot is null || Model is null) return;
        Model.Resize(_plot.ActualWidth, _plot.ActualHeight);
    }

    private void OnTrendPressed(object sender, MouseButtonEventArgs e)
    {
        if (_plot is null || Model is null) return;

        _pressed = true;
        _dragging = false;
        _pressPoint = e.GetPosition(_plot);
        _plot.CaptureMouse();
    }

    private void OnTrendMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _plot is null || Model is null) return;

        var now = e.GetPosition(_plot);
        if (!_dragging)
        {
            // 데드존을 넘기 전에는 클릭이다 — 띠를 그리지 않는다.
            if (!DragMath.IsDrag(now.X - _pressPoint.X, now.Y - _pressPoint.Y)) return;
            _dragging = true;
        }

        Model.UpdateBand(_pressPoint.X, now.X);
    }

    private void OnTrendReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed || _plot is null) return;
        var release = e.GetPosition(_plot).X;
        FinishRangeDrag(commit: true, release);
    }

    private void OnTrendLostCapture(object sender, MouseEventArgs e)
    {
        // 캡처를 잃으면 취소다 — 놓음과 같은 종료 경로를 탄다.
        if (_pressed) FinishRangeDrag(commit: false, 0);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_dragging) return;
        FinishRangeDrag(commit: false, 0);
        e.Handled = true;       // 끄는 중일 때만 소비한다
    }

    /// <summary>드래그 종료의 단일 경로 — 놓음 · 캡처 상실 · Esc 가 전부 여기로 온다.</summary>
    private void FinishRangeDrag(bool commit, double releaseX)
    {
        var wasDragging = _dragging;

        // ① 플래그
        _pressed = false;
        _dragging = false;

        // ② 시각 복원
        if (!commit || !wasDragging) Model?.ClearBand();

        // ③ 캡처 해제
        if (_plot?.IsMouseCaptured == true) _plot.ReleaseMouseCapture();

        // ④ 커밋 통지 — 데드존을 넘지 못했으면 아무 일도 없다(서버 호출 0)
        if (commit && wasDragging) Model?.CommitBand(_pressPoint.X, releaseX);
    }

    private void OnControllerGroup(object sender, RoutedEventArgs e)
    {
        if (Model is not null) Model.DeviceGroup = OverviewDeviceGroup.Controller;
    }

    private void OnCameraGroup(object sender, RoutedEventArgs e)
    {
        if (Model is not null) Model.DeviceGroup = OverviewDeviceGroup.Camera;
    }

    private void OnBarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventDeviceBarViewModel bar }) Model?.Drill(bar);
    }
}
