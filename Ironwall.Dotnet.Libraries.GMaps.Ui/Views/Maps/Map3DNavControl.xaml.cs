using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

/// <summary>
/// 3D 방향 링 · 이동 단추 — DataContext = <see cref="MapViewModel"/>. 끌기 판정은 <see cref="Map3DNavDrag"/>(순수), 보내기는 VM(MapViewModel.Map3DNav).
/// 링 · 지구본은 Thumb 가 마우스를 잡고, 뗌과 캡처 잃음은 모두 DragCompleted(Canceled) 하나로 끝난다.
/// 이동 단추는 누르면 계속 · 떼거나 캡처를 잃으면 멈춤, 키보드 · UIA 로 누르면(Click) 한 걸음.
/// </summary>
public partial class Map3DNavControl : UserControl
{
    private readonly Map3DNavDrag _ring = new(ring: true, DragMath.DeadZone);
    private readonly Map3DNavDrag _tilt = new(ring: false, DragMath.DeadZone);
    private Map3DNavMove? _mouseMove;
    /// <summary>키로 누르고 있는 이동 — 마우스와 같은 방향을 함께 누르면 한쪽을 떼도 다른 쪽이 누르고 있는 동안 계속 간다.</summary>
    private readonly HashSet<Map3DNavMove> _keyMoves = new();

    public Map3DNavControl()
    {
        InitializeComponent();
    }

    private MapViewModel? Vm => DataContext as MapViewModel;

    // ── 링(방위) ──

    private void OnRingDragStarted(object sender, DragStartedEventArgs e)
    {
        var thumb = (FrameworkElement)sender;
        thumb.Focus();   // 키보드 폴백(←/→ · ↑/↓ · N)
        var p = Mouse.GetPosition(thumb);
        _ring.Press(thumb.ActualWidth / 2, thumb.ActualHeight / 2, p.X, p.Y);
    }

    private void OnRingDragDelta(object sender, DragDeltaEventArgs e)
    {
        var p = Mouse.GetPosition((IInputElement)sender);
        if (_ring.Move(p.X, p.Y) is { } n) Vm?.NudgeMap3D(n);
    }

    private void OnRingDragCompleted(object sender, DragCompletedEventArgs e) => _ring.Finish(commit: !e.Canceled);

    // ── 지구본(기울기) ──

    private void OnTiltDragStarted(object sender, DragStartedEventArgs e)
    {
        var p = Mouse.GetPosition(this);
        _tilt.Press(0, 0, p.X, p.Y);
    }

    private void OnTiltDragDelta(object sender, DragDeltaEventArgs e)
    {
        var p = Mouse.GetPosition(this);
        if (_tilt.Move(p.X, p.Y) is { } n) Vm?.NudgeMap3D(n);
    }

    private void OnTiltDragCompleted(object sender, DragCompletedEventArgs e) => _tilt.Finish(commit: !e.Canceled);

    // ── 이동 단추 ──

    private void OnMoveDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Map3DNavMove move }) return;
        EndMouseMove();
        _mouseMove = move;
        Vm?.StartMap3DMove(move);
    }

    private void OnMoveUp(object sender, MouseButtonEventArgs e) => EndMouseMove();

    private void OnMoveLostCapture(object sender, MouseEventArgs e) => EndMouseMove();

    private void EndMouseMove()
    {
        if (_mouseMove is not { } move) return;
        _mouseMove = null;
        if (!_keyMoves.Contains(move)) Vm?.StopMap3DMove(move);
    }

    /// <summary>마우스 클릭은 누름 · 뗌이 이미 처리했다 — 키보드 · UIA(Invoke)로 누른 것만 한 걸음.</summary>
    private void OnMoveClick(object sender, RoutedEventArgs e)
    {
        if (InputManager.Current.MostRecentInputDevice is MouseDevice) return;
        if (sender is not FrameworkElement { Tag: Map3DNavMove move } || Vm is not { } vm) return;
        vm.StartMap3DMove(move);
        vm.StopMap3DMove(move);
    }

    // ── 키보드 폴백(이 판에 초점이 있을 때) ──

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;
        var key = e.Key.ToString();
        if (Map3DNavMath.KeyNudge(key) is { } n) vm.NudgeMap3D(n);
        else if (Map3DNavMath.KeyPreset(key) is { } preset) vm.PresetMap3D(preset);
        else if (Map3DNavMath.KeyMove(key) is { } move) { _keyMoves.Add(move); vm.StartMap3DMove(move); }   // 키 반복은 이미 누름이라 무시된다
        else return;
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Map3DNavMath.KeyMove(e.Key.ToString()) is not { } move) return;
        _keyMoves.Remove(move);
        if (_mouseMove != move) Vm?.StopMap3DMove(move);   // 마우스가 같은 방향을 아직 누르고 있으면 계속
        e.Handled = true;
    }

    private void OnKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false) { _keyMoves.Clear(); Vm?.StopAllMap3DMoves(); }   // 키를 뗀 것을 못 받는다 — 계속 가지 않게
    }
}
