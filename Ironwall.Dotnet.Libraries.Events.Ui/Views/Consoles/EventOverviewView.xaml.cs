using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 개요(T3) — 추이 차트 위를 <b>좌우로 끌어 기간을 고른다</b>(정본 all-windows-drag-wireframe.html L299 · L427).
/// </summary>
/// <remarks>
/// <para><b>캡처 드래그</b>다 — OLE <c>DoDragDrop</c> 은 쓰지 않는다. 데드존 · 종료 순서 · ESC 판정은
/// <see cref="TrendDragStateMachine"/>(순수)이 쥐고, 뷰는 그 지시를 실행만 한다.</para>
/// <para>★ ESC 구독은 <b>창</b>(<c>Window.PreviewKeyDown</c>)에 건다. 누르는 대상이 포커스를 받지 않는
/// <c>Border</c> 라 <c>CaptureMouse</c> 로는 키보드 포커스가 오지 않고, UserControl 에 건 터널은
/// 그 경로를 지나가지 않아 <b>영원히 안 온다</b>(N-07 적대 검토 R3 — 커널 <c>CaptureDragBehavior</c> 와 같은 방식).</para>
/// <para>구독은 <b>누를 때 걸고</b> 종료 단계 ③에서 뗀다 — 끌지 않는 동안 창의 ESC 를 넘겨다보지 않는다.</para>
/// </remarks>
public partial class EventOverviewView : UserControl
{
    private readonly TrendDragStateMachine _drag = new();
    private FrameworkElement? _plot;
    private Window? _keyHost;

    public EventOverviewView()
    {
        InitializeComponent();
        Unloaded += (_, _) => FinishDrag(_drag.LostCapture());
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

        var at = e.GetPosition(_plot);
        if (!_drag.Press(at.X, at.Y)) return;

        _plot.CaptureMouse();
        Subscribe();
    }

    private void OnTrendMouseMove(object sender, MouseEventArgs e)
    {
        if (_plot is null || Model is null) return;

        var at = e.GetPosition(_plot);
        if (_drag.Move(at.X, at.Y)) Model.UpdateBand(_drag.PressX, at.X);
    }

    private void OnTrendReleased(object sender, MouseButtonEventArgs e)
    {
        if (_plot is null) return;
        FinishDrag(_drag.Release(), e.GetPosition(_plot).X);
    }

    private void OnTrendLostCapture(object sender, MouseEventArgs e) => FinishDrag(_drag.LostCapture());

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        var (handled, finish) = _drag.Escape();
        if (!handled) return;               // 끄는 중이 아닐 때는 소비하지 않는다

        FinishDrag(finish);
        e.Handled = true;
    }

    /// <summary>상태 기계가 내린 지시를 순서대로 실행한다 — ②시각 ③구독 ④캡처 ⑤커밋.</summary>
    private void FinishDrag(TrendDragFinish finish, double releaseX = 0)
    {
        if (finish is { ClearBand: false, ReleaseCapture: false, Unsubscribe: false, Commit: false }) return;

        if (finish.ClearBand) Model?.ClearBand();
        if (finish.Unsubscribe) Unsubscribe();
        if (finish.ReleaseCapture && _plot?.IsMouseCaptured == true) _plot.ReleaseMouseCapture();
        if (finish.Commit) Model?.CommitBand(_drag.PressX, releaseX);
    }

    private void Subscribe()
    {
        Unsubscribe();
        _keyHost = Window.GetWindow(this);
        if (_keyHost is not null) _keyHost.PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void Unsubscribe()
    {
        if (_keyHost is null) return;
        _keyHost.PreviewKeyDown -= OnWindowPreviewKeyDown;
        _keyHost = null;
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
