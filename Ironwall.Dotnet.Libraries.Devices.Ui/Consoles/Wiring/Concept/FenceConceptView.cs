using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;

/// <summary>
/// 두 줄 개념도(fence-wiring-editor v0.3 FR-20 · FR-21 · FR-13 · FR-14) — 펜스 격자 위 아래 줄 · 위 줄 칩 · 제어기 <c>C</c>(Ch1 실선 · Ch2 점선) ·
/// 칩 위 번호 · 아래 위치 눈금 · 신호등 · VBus 표지. 펜스 뷰 · 표 보기와 <b>같은 선택</b>을 본다.
/// </summary>
/// <remarks>
/// <para><b>칸에 맞춘다</b> — 펜스 전체를 보이는 폭에 담는다(가로 이동 없음 · 잘리지 않음). 센서가 많으면 칩만 남기고 번호 · 눈금을 솎는다.</para>
/// <para><b>끌기 1순위</b>(drag-first-ux · 캡처 드래그 · 8 DIU) — 칩을 줄 안에서 끌면 순서, 다른 줄로 끌면 줄이 바뀐다(삽입 표지).
/// <c>C</c> 를 반대쪽 끝으로 끌면 제어기 위치가 바뀐다. VBus 칩은 사슬 틈으로 옮긴다. 끝나는 길은 <see cref="FinishDrag"/> 하나(뗌 · 캡처 상실 · Esc).</para>
/// <para><b>키보드 대신</b> — ←/→ 줄 안 이웃 · ↑/↓ 다른 줄 · Alt+←/→ 줄 안 한 칸 옮기기 · Alt+↑/↓ 위 줄/아래 줄로(<see cref="Key.System"/> + SystemKey) ·
/// Delete 빼기 · Shift+F10/메뉴 키 · Ctrl+A · Ctrl+Space · Enter/Space · Esc · Ctrl+Z. 제어기 위치는 속성 칸 「제어기 위치」 단추.</para>
/// <para>칩은 peer 있는 <see cref="FenceChip"/>(<c>Devices.Wiring.Fence.Concept.Node.{key}</c> · <c>….Controller</c> · <c>….Vbus</c>),
/// 신호등은 <see cref="SignalLamp"/>(<c>Devices.Wiring.Fence.Signal.{id}</c> · 제어기 <c>Devices.Wiring.Fence.Signal.Controller</c>).</para>
/// </remarks>
public sealed class FenceConceptView : Grid
{
    public const string AUTOMATION_ID = "Devices.Wiring.Fence.Concept";
    public const string SIGNAL_ID_PREFIX = "Devices.Wiring.Fence.Signal.";
    public const string CONTROLLER_SIGNAL_ID = "Devices.Wiring.Fence.Signal.Controller";

    private readonly Canvas _content = new() { ClipToBounds = false };
    private readonly FenceStaticLayer _back = new();
    private readonly FenceStaticLayer _overlay = new();
    private readonly Canvas _nodes = new() { ClipToBounds = false };
    private readonly Dictionary<int, FenceChip> _nodeChips = new();
    private readonly Dictionary<int, SignalLamp> _lamps = new();
    private FenceChip? _controllerChip;
    private FenceChip? _vbusChip;
    private SignalLamp? _controllerLamp;
    private ConceptGeometry? _geometry;

    private Press? _press;
    private bool _dragging;
    private FenceGestureAction _action;
    private (FenceLane Lane, int Index)? _target;
    private int? _vbusTarget;
    private bool? _controllerFlip;
    private bool _arming;
    private IReadOnlyList<int> _dragKeys = Array.Empty<int>();

    private sealed record Press(FencePointerButton Button, FenceTargetKind Target, FenceChip? Chip, Point Start, bool Ctrl);

    public FenceConceptView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        MinHeight = ConceptLayout.MIN_HEIGHT;
        SetResourceReference(BackgroundProperty, "SurfaceBrush");
        AutomationProperties.SetAutomationId(this, AUTOMATION_ID);
        AutomationProperties.SetName(this, "두 줄 개념도 — 칩을 끌어 순서 · 줄을 바꿉니다 · Alt+←/→ 줄 안 한 칸 · Alt+↑/↓ 위 줄/아래 줄");
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);

        _overlay.IsHitTestVisible = false;
        _content.Children.Add(_back);
        _content.Children.Add(_nodes);
        _content.Children.Add(_overlay);
        Children.Add(_content);

        SetResourceReference(ThemeProbeProperty, "PrimaryBrush");
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => { if (_press is not null) FinishDrag(false); };
        SizeChanged += (_, _) => Rebuild();
    }

    #region - Properties -
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WiringViewModel), typeof(FenceConceptView), new PropertyMetadata(null, OnViewModelChanged));

    public WiringViewModel? ViewModel
    {
        get => (WiringViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (FenceConceptView)d;
        if (e.OldValue is WiringViewModel old) old.FenceChanged -= view.OnFenceChanged;
        if (e.NewValue is WiringViewModel now) now.FenceChanged += view.OnFenceChanged;
        view.Rebuild();
    }

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(object), typeof(FenceConceptView), new PropertyMetadata(null, (d, _) => ((FenceConceptView)d).Redraw()));

    /// <summary>시험 — 메뉴를 띄우지 않고 항목만 남긴다.</summary>
    internal bool SuppressMenuPopup { get; set; }

    internal IReadOnlyList<FenceMenuEntry>? LastMenu { get; private set; }
    internal ConceptGeometry? Geometry => _geometry;
    internal IReadOnlyDictionary<int, FenceChip> NodeChips => _nodeChips;
    internal IReadOnlyDictionary<int, SignalLamp> Lamps => _lamps;
    internal SignalLamp? ControllerLamp => _controllerLamp;
    internal FenceChip? ControllerChip => _controllerChip;
    internal FenceChip? VbusChip => _vbusChip;
    internal IReadOnlyList<FenceShape> BackgroundShapes => _back.Shapes;
    internal IReadOnlyList<FenceShape> OverlayShapes => _overlay.Shapes;
    internal bool IsDragging => _dragging;

    /// <summary>노드의 화면(이 컨트롤) 가운데.</summary>
    internal Point ScreenCenterOf(int key) => _geometry?.Nodes.FirstOrDefault(n => n.Key == key)?.Center ?? default;
    #endregion

    #region - Build -
    private void OnFenceChanged(object? sender, EventArgs e)
    {
        if (_dragging) return;
        Rebuild();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        base.MeasureOverride(constraint);
        return new Size(0, double.IsInfinity(constraint.Height) ? ConceptLayout.MIN_HEIGHT : Math.Min(ConceptLayout.MIN_HEIGHT, constraint.Height));
    }

    internal void Rebuild()
    {
        if (ViewModel is not { } vm) return;
        var nodes = vm.ConceptNodes();
        var size = new Size(Math.Max(ActualWidth, 240), Math.Max(ActualHeight, MinHeight));
        _geometry = ConceptLayout.Build(vm.ConceptItems(), vm.ConceptPostsM(), vm.ConceptLengthM, vm.FenceControllerEnd, size);
        _back.Show(ConceptScene.Background(_geometry, nodes), null);
        _overlay.Show(Array.Empty<FenceShape>(), null);
        SyncNodes(vm, nodes);
    }

    private void SyncNodes(WiringViewModel vm, IReadOnlyList<ConceptNodeInfo> nodes)
    {
        var geometry = _geometry!;
        var live = new HashSet<int>(nodes.Select(n => n.Key));
        foreach (var key in _nodeChips.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _nodes.Children.Remove(_nodeChips[key]);
            _nodeChips.Remove(key);
            _nodes.Children.Remove(_lamps[key]);
            _lamps.Remove(key);
        }

        var order = new List<UIElement>();
        _controllerChip ??= NewChip(FenceChipKind.ConceptController, 0);
        _controllerChip.Picture = ConceptScene.Controller(geometry, vm.IsControllerSelected);
        Place(_controllerChip, geometry.Controller.TopLeft);
        AutomationProperties.SetName(_controllerChip, $"제어기 {vm.Controller.Name} — {WiringViewModel.ControllerEndText(geometry.End)} · Ch1 아래 줄 · Ch2 위 줄. 반대쪽 끝으로 끌면 위치가 바뀝니다");
        order.Add(_controllerChip);

        _controllerLamp ??= NewLamp(CONTROLLER_SIGNAL_ID, "제어기 통신", large: false);
        _controllerLamp.Level = vm.ControllerSignal;
        Canvas.SetLeft(_controllerLamp, geometry.Controller.X + geometry.Controller.Width / 2 - _controllerLamp.Width / 2);
        Canvas.SetTop(_controllerLamp, geometry.LowerY + 7);
        ToolTipService.SetToolTip(_controllerLamp, vm.ControllerSignalText);

        var showLamps = geometry.Mode != ConceptChipMode.Dot;
        // 자식 순서 = 사슬 순서(Tab · 자동화가 Ch1 쪽부터)
        foreach (var node in nodes)
        {
            var point = geometry.Nodes.FirstOrDefault(n => n.Key == node.Key);
            if (point is null) continue;
            if (!_nodeChips.TryGetValue(node.Key, out var chip))
            {
                chip = NewChip(FenceChipKind.ConceptNode, node.Key);
                _nodeChips[node.Key] = chip;
            }
            chip.Picture = ConceptScene.Node(node, point.Lane, geometry.Mode, geometry.Chip, node.IsSelected);
            Place(chip, point.Center);
            var ip = node.IsIp ? $", IP {node.Address}" : $", 노드 주소 {node.Address}";
            var orientation = node.OrientationText.Length > 0 ? $", {node.OrientationText}" : string.Empty;
            AutomationProperties.SetName(chip, $"#{node.Position} {node.Name}, {WiringViewModel.LaneText(point.Lane)}, 번호 {node.Number}{ip}, 통신 {SignalMath.Text(node.Signal)}{orientation}");
            ToolTipService.SetToolTip(chip, $"{node.Name} · 번호 {node.Number}{(node.OrientationText.Length > 0 ? $" · {node.OrientationText}" : string.Empty)}");
            AutomationProperties.SetItemStatus(chip, node.IsSelected ? "선택됨" : string.Empty);
            order.Add(chip);

            if (!_lamps.TryGetValue(node.Key, out var lamp))
            {
                lamp = NewLamp(SIGNAL_ID_PREFIX + (node.Id > 0 ? node.Id : node.Key), $"센서 #{node.Position} 통신", large: false);
                _lamps[node.Key] = lamp;
            }
            lamp.Subject = $"센서 #{node.Position} 통신";
            lamp.Level = node.Signal;
            lamp.UpdateName();
            lamp.Width = Math.Clamp(geometry.Chip.Width, 10, 26);
            lamp.Visibility = showLamps ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(lamp, point.Center.X - lamp.Width / 2);
            Canvas.SetTop(lamp, point.Center.Y + geometry.Chip.Height / 2 + 2);
        }

        // VBus 표지(FR-21) — 스마트 복합센서2 링만
        if (vm.HasVbus)
        {
            _vbusChip ??= NewChip(FenceChipKind.ConceptVbus, 0);
            _vbusChip.Visibility = Visibility.Visible;
            _vbusChip.Picture = ConceptScene.Vbus(false);
            Place(_vbusChip, ConceptLayout.VbusPoint(geometry, vm.FenceChain.Keys, vm.VbusGap));
            AutomationProperties.SetName(_vbusChip, $"VBus — {vm.VbusGap}번째 센서 뒤 · 표시 전용 · 끌어 옮깁니다");
            order.Add(_vbusChip);
        }
        else if (_vbusChip is not null) _vbusChip.Visibility = Visibility.Collapsed;

        for (var i = 0; i < order.Count; i++)
        {
            var at = _nodes.Children.IndexOf(order[i]);
            if (at == i) continue;
            if (at >= 0) _nodes.Children.RemoveAt(at);
            _nodes.Children.Insert(Math.Min(i, _nodes.Children.Count), order[i]);
        }
    }

    private FenceChip NewChip(FenceChipKind kind, int key)
    {
        var chip = new FenceChip(kind, key, kind == FenceChipKind.ConceptNode ? new[] { key } : Array.Empty<int>());
        chip.GotFocus += OnChipFocused;
        _nodes.Children.Add(chip);
        return chip;
    }

    private SignalLamp NewLamp(string automationId, string subject, bool large)
    {
        var lamp = new SignalLamp { Subject = subject, IsHitTestVisible = true };
        if (!large) { lamp.Width = 26; lamp.Height = 9; }
        AutomationProperties.SetAutomationId(lamp, automationId);
        lamp.UpdateName();
        _nodes.Children.Add(lamp);
        return lamp;
    }

    private static void Place(FenceChip chip, Point origin)
    {
        var hit = chip.HitBounds;
        chip.Width = hit.Width;
        chip.Height = hit.Height;
        Canvas.SetLeft(chip, origin.X + hit.X);
        Canvas.SetTop(chip, origin.Y + hit.Y);
    }

    private void Redraw()
    {
        _back.InvalidateVisual();
        _overlay.InvalidateVisual();
        foreach (var child in _nodes.Children.OfType<UIElement>()) child.InvalidateVisual();
    }

    private void OnChipFocused(object sender, RoutedEventArgs e)
    {
        if (_press is not null || _arming || sender is not FenceChip chip || ViewModel is not { } vm) return;
        if (chip.Kind == FenceChipKind.ConceptController) { if (!vm.IsControllerSelected) vm.FenceSelectController(); }
        else if (chip.Kind == FenceChipKind.ConceptNode && !vm.IsFenceSelected(chip.Key)) vm.FenceSelect(chip.Key);
    }
    #endregion

    #region - Mouse -
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (_press is not null || ViewModel is null) return;
        FencePointerButton? button = e.ChangedButton switch
        {
            MouseButton.Left => FencePointerButton.Left,
            MouseButton.Right => FencePointerButton.Right,
            MouseButton.Middle => FencePointerButton.Middle,
            _ => null,
        };
        if (button is null) return;
        e.Handled = true;
        OnPointerPressed(e.GetPosition(this), ChipFrom(e.OriginalSource as DependencyObject), (Keyboard.Modifiers & ModifierKeys.Control) != 0, button.Value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_press is null || _arming) return;
        OnPointerMoved(e.GetPosition(this));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_press is null) return;
        e.Handled = true;
        OnPointerReleased(e.GetPosition(this));
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_press is not null) FinishDrag(false);
    }

    /// <summary>누름(이 컨트롤 좌표) — 시험이 이 길로 부른다.</summary>
    internal void OnPointerPressed(Point at, FenceChip? chip, bool ctrl = false, FencePointerButton button = FencePointerButton.Left)
    {
        if (_press is not null) return;
        _arming = true;
        try
        {
            if (button == FencePointerButton.Left && chip is not null) chip.Focus(); else Focus();
            CaptureMouse();
        }
        finally { _arming = false; }
        var target = chip?.Kind switch
        {
            FenceChipKind.ConceptNode => FenceTargetKind.Sensor,
            FenceChipKind.ConceptController or FenceChipKind.ConceptVbus => FenceTargetKind.Enclosure,
            _ => FenceTargetKind.Empty,
        };
        _press = new Press(button, target, chip, at, ctrl);
        _dragging = false;
        _action = FenceGestureAction.None;
    }

    internal void OnPointerMoved(Point now)
    {
        if (_press is null || ViewModel is not { } vm || _geometry is null) return;
        if (!_dragging)
        {
            if (!FenceGesture.IsDrag(_press.Start, now)) return;
            _dragging = true;
            _action = FenceGesture.Classify(_press.Button, _press.Ctrl, false, _press.Target, isDrag: true);
            // 개념도는 칸에 맞춰 가로 이동이 없다 — 빈 곳 끌기는 아무 일도 하지 않는다(펜스 뷰가 러버밴드를 맡는다).
            if (_action is not (FenceGestureAction.MoveSensors or FenceGestureAction.MoveEnclosure)) _action = FenceGestureAction.None;
            if (_action == FenceGestureAction.MoveSensors && _press.Chip is { } chip)
            {
                _dragKeys = vm.FenceDragKeys(chip.Key);
                foreach (var key in _dragKeys) if (_nodeChips.TryGetValue(key, out var dim)) dim.Opacity = 0.35;
            }
            if (_action != FenceGestureAction.None) Cursor = Cursors.SizeAll;
        }

        switch (_action)
        {
            case FenceGestureAction.MoveSensors:
            {
                var lane = ConceptLayout.LaneAt(_geometry, now);
                var index = ConceptLayout.IndexAt(_geometry, lane, now, _dragKeys);
                if (_target == (lane, index)) return;                // 후보가 바뀔 때만 다시 그린다(NFR-02)
                _target = (lane, index);
                _overlay.Show(ConceptScene.Insertion(ConceptLayout.InsertionPoint(_geometry, lane, index, _dragKeys)), null);
                vm.NotifyFenceStatus($"개념도 — 여기 놓으면 {vm.ConceptLaneDropLabel(_dragKeys, lane, index)} · Esc 취소");
                break;
            }
            case FenceGestureAction.MoveEnclosure when _press.Chip is { Kind: FenceChipKind.ConceptVbus }:
            {
                var gap = ConceptLayout.VbusGapAt(_geometry, vm.FenceChain.Keys, now);
                if (_vbusTarget == gap) return;
                _vbusTarget = gap;
                _overlay.Show(ConceptScene.Insertion(ConceptLayout.VbusPoint(_geometry, vm.FenceChain.Keys, gap)), null);
                vm.NotifyFenceStatus($"개념도 — VBus 를 {gap}번째 센서 뒤로 · Esc 취소");
                break;
            }
            case FenceGestureAction.MoveEnclosure:
            {
                // 제어기 — 그림 가운데를 넘어 반대쪽으로 가면 뒤집는다
                var flip = SideOf(now.X) != _geometry.End;
                if (_controllerFlip == flip) return;
                _controllerFlip = flip;
                var c = _geometry.Controller;
                var at = flip ? new Rect(_geometry.Extent.Width - c.Right, c.Top, c.Width, c.Height) : c;
                _overlay.Show(ConceptScene.ControllerTarget(at), null);
                vm.NotifyFenceStatus(flip ? $"개념도 — 놓으면 제어기를 {WiringViewModel.ControllerEndText(_geometry.End == FenceControllerEnd.Left ? FenceControllerEnd.Right : FenceControllerEnd.Left)}으로 · Esc 취소"
                                          : "개념도 — 제자리(반대쪽 끝으로 끌면 제어기 위치가 바뀝니다)");
                break;
            }
        }

        FenceControllerEnd SideOf(double x) => x < _geometry!.Extent.Width / 2 ? FenceControllerEnd.Left : FenceControllerEnd.Right;
    }

    internal void OnPointerReleased(Point at) => FinishDrag(true, at);

    /// <summary>끝내기는 이것 하나 — 뗌 · 캡처 상실 · Esc. 순서: ① 표지 ② 모습 ④ 캡처 ⑤ 통지.</summary>
    internal void FinishDrag(bool commit, Point? at = null)
    {
        var press = _press;
        if (press is null) return;
        var wasDragging = _dragging;
        var action = wasDragging ? _action : FenceGesture.Classify(press.Button, press.Ctrl, false, press.Target, isDrag: false);
        var target = _target;
        var vbus = _vbusTarget;
        var flip = _controllerFlip;
        var keys = _dragKeys;

        // ① 표지
        _press = null;
        _dragging = false;
        _action = FenceGestureAction.None;
        _target = null;
        _vbusTarget = null;
        _controllerFlip = null;
        _dragKeys = Array.Empty<int>();
        // ② 모습
        foreach (var chip in _nodeChips.Values) chip.Opacity = 1;
        _overlay.Show(Array.Empty<FenceShape>(), null);
        ClearValue(CursorProperty);
        // ④ 캡처
        if (IsMouseCaptured) ReleaseMouseCapture();

        // ⑤ 통지
        if (ViewModel is not { } vm) return;
        if (!commit)
        {
            if (wasDragging && action != FenceGestureAction.None) vm.NotifyFenceStatus("취소 — 개념도를 그대로 두었습니다(서버 호출 없음)");
            Rebuild();
            return;
        }

        switch (action)
        {
            case FenceGestureAction.MoveSensors when target is { } t && keys.Count > 0:
                if (!vm.ConceptLaneDrop(keys, t.Lane, t.Index)) Rebuild();
                else if (keys.Count == 1) vm.FenceSelect(keys[0]);
                break;
            case FenceGestureAction.MoveEnclosure when press.Chip is { Kind: FenceChipKind.ConceptVbus } && vbus is { } g:
                if (!vm.SetVbusGap(g)) Rebuild();
                break;
            case FenceGestureAction.MoveEnclosure when flip == true:
                if (!vm.FlipControllerEnd()) Rebuild();
                break;
            case FenceGestureAction.SelectOne when press.Chip is { Kind: FenceChipKind.ConceptController }:
                vm.FenceSelectController();
                break;
            case FenceGestureAction.SelectOne when press.Chip is { Kind: FenceChipKind.ConceptNode } node:
                vm.FenceSelect(node.Key);
                FocusQuietly(node);
                break;
            case FenceGestureAction.ToggleOne when press.Chip is { Kind: FenceChipKind.ConceptNode } node:
                vm.FenceToggleSelect(node.Key);
                break;
            case FenceGestureAction.ClearSelection:
                vm.FenceClearSelection();
                Focus();
                break;
            case FenceGestureAction.ContextMenu:
                OpenMenu(press.Chip, at ?? press.Start);
                break;
            default:
                Rebuild();
                break;
        }
    }

    private void OpenMenu(FenceChip? chip, Point at)
    {
        if (ViewModel is not { } vm) return;
        IReadOnlyList<FenceMenuEntry> entries;
        if (chip is { Kind: FenceChipKind.ConceptNode })
        {
            if (!vm.IsFenceSelected(chip.Key)) vm.FenceSelect(chip.Key);
            entries = vm.FenceMenu(FenceMenuTargetKind.Sensor, chip.Key);
        }
        else entries = vm.FenceMenu(FenceMenuTargetKind.Empty, 0);
        LastMenu = entries;
        if (!SuppressMenuPopup) FenceMenuPresenter.Show(this, entries, at, (entry, ex) => vm.ReportFenceMenuFailure(entry.Text, ex));
    }

    private static FenceChip? ChipFrom(DependencyObject? d)
    {
        for (; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is FenceChip chip) return chip;
        return null;
    }
    #endregion

    #region - Keyboard -
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        // 수정키는 누른 순서와 무관하게(Alt 먼저 · Shift 먼저 모두 Alt+Shift) — 헤디드 r21
        if (HandleKeyDown(e.Key, e.SystemKey, FenceKeyModifiers.Of(e), Keyboard.FocusedElement as DependencyObject)) e.Handled = true;
    }

    /// <summary>
    /// 키 판정 — 처리했으면 <c>true</c>(시험이 이 길로 부른다). <c>Alt</c>+화살표는 <see cref="Key.System"/> + <paramref name="systemKey"/> 로 온다.
    /// Esc 는 끄는 중이면 취소, 아니면 선택 해제 — 풀 것이 없으면 흘려보낸다.
    /// </summary>
    internal bool HandleKeyDown(Key key, Key systemKey, ModifierKeys modifiers, DependencyObject? focused)
    {
        if (ViewModel is not { } vm || _geometry is null) return false;
        if (key == Key.Escape)
        {
            if (_press is not null) { FinishDrag(false); return true; }
            return vm.FenceClearSelection();
        }
        var alt = key == Key.System;
        var k = alt ? systemKey : key;
        var chip = focused as FenceChip;
        var node = chip is { Kind: FenceChipKind.ConceptNode } ? chip : null;

        if (k == Key.Apps || (k == Key.F10 && (modifiers & ModifierKeys.Shift) != 0))
        {
            OpenMenu(chip, node is not null ? ScreenCenterOf(node.Key) : new Point(ActualWidth / 2, ActualHeight / 2));
            return true;
        }
        if (!alt && k == Key.A && modifiers == ModifierKeys.Control) { vm.FenceSelectAllSensors(); return true; }
        if (!alt && k == Key.Z && modifiers == ModifierKeys.Control) { vm.Undo(); return true; }
        if (node is null) return false;

        var lane = vm.FenceLayout.LaneOf(node.Key);
        var row = vm.LaneKeysLeftToRight(lane).ToList();
        var at = row.IndexOf(node.Key);
        // Ctrl+←/→(주) · Alt+Shift+←/→ — 옆 망(기둥)으로 한 칸(펜스 보기와 같은 규칙 · 헤디드 r22: Shift 를 보지 않던 Alt+←/→ 가 줄 안 한 칸으로 갔다)
        if (FenceCanvas.IsPanelMoveKey(alt, k, modifiers))
        {
            if (!vm.IsFenceSelected(node.Key)) vm.FenceSelect(node.Key);
            vm.FenceMoveSelectedByPanels(k == Key.Left ? -1 : 1);
            FocusNode(node.Key);
            return true;
        }
        if (alt && k is Key.Left or Key.Right) { vm.ConceptLaneStep(node.Key, k == Key.Left ? -1 : 1); FocusNode(node.Key); return true; }
        if (alt && k is Key.Up or Key.Down) { vm.ConceptLaneChange(node.Key, k == Key.Up ? FenceLane.Upper : FenceLane.Lower); FocusNode(node.Key); return true; }
        if (!alt && k is Key.Delete or Key.Back) { vm.FenceUnplace(vm.FenceDragKeys(node.Key)); return true; }
        if (!alt && k == Key.Space && modifiers == ModifierKeys.Control) { vm.FenceToggleSelect(node.Key); return true; }
        if (!alt && k is Key.Enter or Key.Space && modifiers == ModifierKeys.None) { vm.FenceSelect(node.Key); return true; }
        if (!alt && k is Key.Left or Key.Right or Key.Home or Key.End && modifiers == ModifierKeys.None)
        {
            var j = k switch { Key.Home => 0, Key.End => row.Count - 1, Key.Left => at - 1, _ => at + 1 };
            if (j >= 0 && j < row.Count) FocusNode(row[j]);
            return true;
        }
        if (!alt && k is Key.Up or Key.Down && modifiers == ModifierKeys.None)
        {
            // 다른 줄의 가장 가까운 칩으로
            var other = k == Key.Up ? FenceLane.Upper : FenceLane.Lower;
            if (other == lane) return true;
            var x = ScreenCenterOf(node.Key).X;
            var candidates = vm.LaneKeysLeftToRight(other).OrderBy(o => Math.Abs(ScreenCenterOf(o).X - x)).Take(1).ToList();
            if (candidates.Count > 0) FocusNode(candidates[0]);
            return true;
        }
        return false;
    }

    private void FocusNode(int key, bool quietly = false)
    {
        if (!_nodeChips.TryGetValue(key, out var chip)) return;
        if (quietly) FocusQuietly(chip); else chip.Focus();
    }

    private void FocusQuietly(FenceChip chip)
    {
        _arming = true;
        try { chip.Focus(); }
        finally { _arming = false; }
    }
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new FenceConceptViewAutomationPeer(this);
}

/// <summary>개념도 peer — Pane · 자식은 노드 · 제어기 · VBus · 신호등 peer.</summary>
public sealed class FenceConceptViewAutomationPeer : FrameworkElementAutomationPeer
{
    public FenceConceptViewAutomationPeer(FenceConceptView owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(FenceConceptView);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => true;
}
