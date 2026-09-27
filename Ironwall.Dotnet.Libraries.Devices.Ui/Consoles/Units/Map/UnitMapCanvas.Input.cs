using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 캔버스 입력 — 휠 줌(합침) · 팬 · 더블클릭 · 키 해석 (IMPL-21 · FR-12 · FR-13 · FR-14 · FR-38)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>키 하나를 캔버스가 어떻게 읽었나.</summary>
public enum UnitMapKeyActionKind
{
    None = 0,
    ZoomIn,
    ZoomOut,
    Fit,
    Pan,
    Command,
}

/// <summary>키 해석 결과. 줌 · 팬은 캔버스가 스스로, <see cref="UnitMapKeyActionKind.Command"/> 는 뷰모델로.</summary>
public readonly record struct UnitMapKeyAction(UnitMapKeyActionKind Kind, UnitMapKeyCommand Command = default, bool Shift = false, int PanX = 0, int PanY = 0)
{
    public static UnitMapKeyAction None => default;
    public static UnitMapKeyAction For(UnitMapKeyCommand command, bool shift = false) => new(UnitMapKeyActionKind.Command, command, shift);
    public static UnitMapKeyAction PanStep(int x, int y) => new(UnitMapKeyActionKind.Pan, PanX: x, PanY: y);
}

/// <summary>
/// 키 → 뜻 — 순수 함수(NFR-11). 뜻의 판정(권한 · 모드 · 확인)은 뷰모델이 한다(IMPL-27).
/// </summary>
/// <remarks>
/// <para><c>Alt+↑/↓</c> 는 WPF 에서 <c>Key.System</c> 으로 오고 실제 키는 <c>SystemKey</c> 에 있다(DF 실측) — <c>e.Key == Key.Up</c> 으로는 영원히 안 뜬다.</para>
/// <para><c>Alt+Shift</c>+화살표는 한국어 Windows 입력 언어 전환 단축키라 쓰지 않는다(R-16).</para>
/// <para>한글 IME 가 켜져 있으면 <c>M</c> · <c>L</c> 은 <c>Key.ImeProcessed</c> 로 오고 실제 키는 <c>ImeProcessedKey</c> 다
/// (시나리오 ISSUE-33) — 위치 이동 모드는 위치 끌기의 유일한 키보드 짝이다.</para>
/// </remarks>
public static class UnitMapKeyMap
{
    public static UnitMapKeyAction Interpret(Key key, Key systemKey, Key imeProcessedKey, ModifierKeys modifiers)
    {
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        var ctrl = modifiers.HasFlag(ModifierKeys.Control);

        if (key == Key.System)
        {
            if (shift || ctrl) return UnitMapKeyAction.None;
            return systemKey switch
            {
                Key.Up => UnitMapKeyAction.For(UnitMapKeyCommand.ParentUp),
                Key.Down => UnitMapKeyAction.For(UnitMapKeyCommand.ParentDown),
                _ => UnitMapKeyAction.None,
            };
        }

        if (modifiers.HasFlag(ModifierKeys.Alt)) return UnitMapKeyAction.None;
        var k = key == Key.ImeProcessed ? imeProcessedKey : key;

        if (ctrl)
        {
            return k switch
            {
                Key.Left => UnitMapKeyAction.PanStep(-1, 0),
                Key.Right => UnitMapKeyAction.PanStep(1, 0),
                Key.Up => UnitMapKeyAction.PanStep(0, -1),
                Key.Down => UnitMapKeyAction.PanStep(0, 1),
                Key.Z => UnitMapKeyAction.For(UnitMapKeyCommand.Undo),
                _ => UnitMapKeyAction.None,
            };
        }

        return k switch
        {
            Key.Add or Key.OemPlus => new UnitMapKeyAction(UnitMapKeyActionKind.ZoomIn),
            Key.Subtract or Key.OemMinus => new UnitMapKeyAction(UnitMapKeyActionKind.ZoomOut),
            Key.D0 or Key.NumPad0 => new UnitMapKeyAction(UnitMapKeyActionKind.Fit),
            Key.Up => UnitMapKeyAction.For(UnitMapKeyCommand.Up, shift),
            Key.Down => UnitMapKeyAction.For(UnitMapKeyCommand.Down, shift),
            Key.Left => UnitMapKeyAction.For(UnitMapKeyCommand.Left, shift),
            Key.Right => UnitMapKeyAction.For(UnitMapKeyCommand.Right, shift),
            Key.Home => UnitMapKeyAction.For(UnitMapKeyCommand.Home),
            Key.Enter => UnitMapKeyAction.For(UnitMapKeyCommand.Enter),
            Key.Escape => UnitMapKeyAction.For(UnitMapKeyCommand.Escape),
            Key.M => UnitMapKeyAction.For(UnitMapKeyCommand.MoveMode),
            Key.L => UnitMapKeyAction.For(UnitMapKeyCommand.LocateOnMap),
            _ => UnitMapKeyAction.None,
        };
    }
}

public partial class UnitMapCanvas
{
    private GraphWheelAccumulator? _wheel;
    private DispatcherTimer? _wheelTimer;
    private bool _spaceHeld;
    private (Point At, UnitMapNode? Node)? _rightPress;

    /// <summary>시계 — 휠 합침 · 자동 팬 · 끌리는 층 30Hz 가 쓴다(시험은 가짜 시계, IClock 규칙 I-02).</summary>
    public IClock Clock { get; set; } = new SystemClock();

    /// <summary>수식 키 읽기 — 시험이 바꿔 끼운다.</summary>
    internal Func<ModifierKeys> ReadModifiers { get; set; } = () => Keyboard.Modifiers;

    #region - 마우스 -
    // 누름은 터널(PreviewMouseDown)에서 선점한다 — 노드(Thumb)가 먼저 캡처하지 못하게(DF · 시나리오 ISSUE-19).
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (e.Handled) return;

        var source = e.OriginalSource as DependencyObject;
        if (IsInOverlay(source)) return;                                  // HUD · 막대 · 확인 단추는 제 일을 한다
        if (IsConfirmOpen) { e.Handled = true; return; }                  // 확인 중에는 캔버스 입력을 막는다

        var at = e.GetPosition(this);
        var node = NodeFrom(source);
        Focus();

        switch (e.ChangedButton)
        {
            case MouseButton.Left when e.ClickCount >= 2 && node is null && !IsGestureActive:
                ZoomToNextLevel(at);
                e.Handled = true;
                return;
            case MouseButton.Left:
                OnPointerPressed(at, node, GraphPointerButton.Left, _spaceHeld);
                break;
            case MouseButton.Middle:
                OnPointerPressed(at, node, GraphPointerButton.Middle, spaceHeld: false);
                break;
            case MouseButton.Right:
                // 오른쪽 = 선택만(원장 D-2026-09-27-6615ba — 노드 메뉴는 v2).
                _rightPress = (at, node);
                e.Handled = true;
                return;
            default:
                return;
        }

        CaptureMouse();                                                    // 뗌 · 캡처 상실이 반드시 이 캔버스로 — 토큰이 새지 않는다
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (IsGestureActive) OnPointerMoved(e.GetPosition(this));
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);

        if (e.ChangedButton == MouseButton.Right && _rightPress is { } right)
        {
            _rightPress = null;
            var at = e.GetPosition(this);
            OnRightClick(right.At, at, right.Node);
            e.Handled = true;
            return;
        }

        if (IsGestureActive && e.ChangedButton == ToMouseButton(_gestureButton))
        {
            OnPointerReleased(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (IsGestureActive) FinishGesture(commit: false);
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled || IsInOverlay(e.OriginalSource as DependencyObject)) return;
        e.Handled = true;                                                  // 휠 = 줌(지도 관용구 — R-17), 바깥 스크롤로 새지 않는다
        var notches = Math.Sign(e.Delta) * Math.Max(1, Math.Abs(e.Delta) / Mouse.MouseWheelDeltaForOneLine);
        OnWheel(notches, e.GetPosition(this));
    }

    /// <summary>오른쪽 클릭 — 데드존 안이면 그 노드 선택만. 빈 곳은 아무것도 하지 않는다.</summary>
    internal void OnRightClick(Point pressed, Point released, UnitMapNode? node)
    {
        if (node is null || IsConfirmOpen) return;
        if (DragMath.IsDrag(released.X - pressed.X, released.Y - pressed.Y)) return;
        Interaction?.RequestSelect(node.UnitId);
    }
    #endregion

    #region - 휠 · 더블클릭 -
    /// <summary>
    /// 휠 칸을 모은다 — 33ms 창에 모인 칸은 <b>한 번</b> 적용한다(최대 30Hz — FR-12 · NFR-03). 끄는 중 · 확인 중에는 무시
    /// (끄는 동안 단계가 바뀌면 끌리는 노드 템플릿이 갈려 끌기가 죽는다 — 시나리오 ISSUE-12 ⓐ).
    /// </summary>
    internal void OnWheel(int notches, Point cursor)
    {
        if (notches == 0 || IsDragging || IsConfirmOpen || ActualWidth <= 0 || ActualHeight <= 0) return;
        _wheel ??= new GraphWheelAccumulator(() => Clock.UtcNow);
        _wheel.Add(notches, cursor);
        ScheduleWheelFlush();
    }

    /// <summary>창이 지났으면 모인 칸을 적용한다. 적용했으면 <c>true</c>.</summary>
    internal bool FlushWheel()
    {
        if (_wheel is null || !_wheel.TryFlush(out var factor, out var cursor)) return false;
        if (!IsDragging && !IsConfirmOpen) ZoomAt(cursor, factor);
        return true;
    }

    private void ScheduleWheelFlush()
    {
        if (_wheel is null) return;
        _wheelTimer ??= new DispatcherTimer(DispatcherPriority.Input, Dispatcher);
        _wheelTimer.Stop();
        _wheelTimer.Interval = _wheel.DueIn() > TimeSpan.Zero ? _wheel.DueIn() : TimeSpan.FromMilliseconds(1);
        _wheelTimer.Tick -= OnWheelTimer;
        _wheelTimer.Tick += OnWheelTimer;
        _wheelTimer.Start();
    }

    private void OnWheelTimer(object? sender, EventArgs e)
    {
        _wheelTimer?.Stop();
        if (!FlushWheel() && _wheel?.IsPending == true) ScheduleWheelFlush();
    }

    /// <summary>빈 곳 더블클릭 — 그 점을 고정한 채 다음 단계의 들어가는 배율로(FR-14). L2 에서는 무동작.</summary>
    internal void ZoomToNextLevel(Point at)
    {
        if (IsDragging || IsConfirmOpen) return;
        double? target = _level switch
        {
            UnitMapLevel.L0 => UnitMapLod.EntryScale(UnitMapLevel.L1),
            UnitMapLevel.L1 => UnitMapLod.EntryScale(UnitMapLevel.L2),
            _ => null,
        };
        if (target is double scale) ApplyView(_view.ZoomTo(at, scale));
    }
    #endregion

    #region - 키 -
    // 키는 터널(PreviewKeyDown)에서만 본다 — DataGrid 류가 버블 KeyDown 을 먹는 함정(DF). 캔버스(또는 그 자손)가
    // 포커스일 때만 이 처리기가 불리므로 Space · M · L 이 캔버스 밖 단추 · 검색 칸으로 새지 않는다.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;
        e.Handled = HandleKeyDown(e.Key, e.SystemKey, e.ImeProcessedKey, ReadModifiers(), e.OriginalSource as DependencyObject);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    /// <summary>
    /// 키 하나 — 처리했으면 <c>true</c>. <c>Esc</c> 순서: 확인 오버레이 &gt; 끌기 &gt; 뷰모델(M 모드 등) &gt; 통과
    /// (통과하면 기존 <c>ClearSelectionOnEscBehavior</c> 가 받는다 — 시나리오 ISSUE-18).
    /// </summary>
    internal bool HandleKeyDown(Key key, Key systemKey, Key imeProcessedKey, ModifierKeys modifiers, DependencyObject? source)
    {
        // ① 확인 오버레이 — Enter = 확정 · Esc = 취소. [취소] 단추에 포커스가 있으면 Enter 는 그 단추가 받는다.
        if (IsConfirmOpen)
        {
            if (key == Key.Escape) { ConfirmChoice(false); return true; }
            if (key == Key.Enter && !ReferenceEquals(source, _confirmCancel)) { ConfirmChoice(true); return true; }
            return false;
        }

        // ② 끄는 중 — Esc 는 취소(서버 0)하고 소비한다. 다른 키는 끌기를 흔들지 않게 먹는다.
        //    누르기만 한 중(데드존 안)의 Esc 는 누름만 버리고 통과시킨다 — 선택 해제 등 기존 동작(SIM-C015 · C030 · C046).
        if (IsGestureActive)
        {
            if (IsDragging)
            {
                if (key == Key.Escape) FinishGesture(commit: false);
                return true;
            }
            if (key == Key.Escape) FinishGesture(commit: false);
        }

        // ③ 글 칸 · 오버레이 단추의 Enter/Space 는 그쪽 몫.
        if (source is TextBoxBase or PasswordBox) return false;
        if (IsInOverlay(source) && key is Key.Enter or Key.Space) return false;

        // ④ Space — 누른 채 좌 = 팬(노드 위에서도). 캔버스 포커스에서만 소비한다.
        if (key == Key.Space) { _spaceHeld = true; return true; }

        var action = UnitMapKeyMap.Interpret(key, systemKey, imeProcessedKey, modifiers);
        switch (action.Kind)
        {
            case UnitMapKeyActionKind.ZoomIn: ZoomStep(+1); return true;
            case UnitMapKeyActionKind.ZoomOut: ZoomStep(-1); return true;
            case UnitMapKeyActionKind.Fit: Fit(); return true;
            case UnitMapKeyActionKind.Pan:
                if (ViewportSize.Width > 0) ApplyView(_view.PanStep(ViewportSize, action.PanX, action.PanY));
                return true;
            case UnitMapKeyActionKind.Command:
                return Interaction?.HandleKey(action.Command, action.Shift) ?? false;
            default:
                return false;
        }
    }
    #endregion

    private static MouseButton ToMouseButton(GraphPointerButton button) => button switch
    {
        GraphPointerButton.Middle => MouseButton.Middle,
        GraphPointerButton.Right => MouseButton.Right,
        _ => MouseButton.Left,
    };
}
