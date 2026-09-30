using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
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
/// 링 개념도(fence-wiring-editor FR-12 · FR-13 · FR-14) — 제어기 · Ch1(A) → #1 … #N → Ch2(B) · 흐름 화살표 · 리턴케이블 · 센서 번호 · IP 표지 · 신호등.
/// 펜스 뷰 · 표 보기와 <b>같은 선택</b>을 본다. 노드를 끌어 순서를 바꾸면 펜스 위 자리도 따라간다(보드가 맞춘다).
/// </summary>
/// <remarks>
/// <para><b>입력</b> — 펜스 캔버스와 같은 규칙(<see cref="FenceGesture"/>): 노드 클릭 = 고름(Ctrl = 더함/뺌) · 노드 끌기 = 순서 옮기기(캡처 드래그 · 8 DIU) ·
/// 빈 곳 클릭 = 선택 해제 · 오른쪽 클릭(데드존 안) = 메뉴 · 오른쪽 · 가운데 끌기 = 가로 이동 · 휠 = 가로 이동.
/// 끝나는 길은 <see cref="FinishDrag"/> 하나(뗌 · 캡처 상실 · Esc).</para>
/// <para><b>키보드</b> — 노드 ←/→ 이동 · Alt+←/→ 한 칸 옮기기 · Delete 빼기 · Shift+F10/메뉴 키 · Ctrl+A · Ctrl+Space · Enter/Space · Esc.</para>
/// <para>노드 · 제어기는 peer 있는 <see cref="FenceChip"/>(<c>Devices.Wiring.Fence.Concept.Node.{id}</c>), 신호등은 <see cref="SignalLamp"/>
/// (<c>Devices.Wiring.Fence.Signal.{id}</c> · 제어기 <c>Devices.Wiring.Fence.Signal.Controller</c>).</para>
/// </remarks>
public sealed class FenceConceptView : Grid
{
    public const string AUTOMATION_ID = "Devices.Wiring.Fence.Concept";
    public const string SIGNAL_ID_PREFIX = "Devices.Wiring.Fence.Signal.";
    public const string CONTROLLER_SIGNAL_ID = "Devices.Wiring.Fence.Signal.Controller";

    private readonly Canvas _content = new() { ClipToBounds = false };
    private readonly TranslateTransform _scrollTransform = new();
    private readonly FenceStaticLayer _back = new();
    private readonly FenceStaticLayer _overlay = new();
    private readonly Canvas _nodes = new() { ClipToBounds = false };
    private readonly Dictionary<int, FenceChip> _nodeChips = new();
    private readonly Dictionary<int, SignalLamp> _lamps = new();
    private FenceChip? _controllerChip;
    private SignalLamp? _controllerLamp;
    private ConceptGeometry? _geometry;
    private double _scroll;

    private Press? _press;
    private bool _dragging;
    private FenceGestureAction _action;
    private int? _gap;
    private bool _arming;
    private IReadOnlyList<int> _dragKeys = Array.Empty<int>();

    private sealed record Press(FencePointerButton Button, FenceTargetKind Target, FenceChip? Chip, Point Start, double Scroll, bool Ctrl);

    public FenceConceptView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        MinHeight = 120;
        SetResourceReference(BackgroundProperty, "SurfaceBrush");
        AutomationProperties.SetAutomationId(this, AUTOMATION_ID);
        AutomationProperties.SetName(this, "링 개념도 — 노드를 끌어 순서를 바꿉니다 · Alt+←/→ 한 칸");
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);

        _overlay.IsHitTestVisible = false;
        _content.RenderTransform = _scrollTransform;
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
    internal IReadOnlyList<FenceShape> BackgroundShapes => _back.Shapes;
    internal double ScrollOffset => _scroll;
    internal bool IsDragging => _dragging;

    /// <summary>노드의 화면(이 컨트롤) 가운데.</summary>
    internal Point ScreenCenterOf(int key)
        => _geometry?.Nodes.FirstOrDefault(n => n.Key == key) is { } node ? new Point(node.Center.X - _scroll, node.Center.Y) : default;
    #endregion

    #region - Build -
    private void OnFenceChanged(object? sender, EventArgs e)
    {
        if (_dragging) return;
        Rebuild();
    }

    internal void Rebuild()
    {
        if (ViewModel is not { } vm) return;
        var nodes = vm.ConceptNodes();
        var size = new Size(Math.Max(ActualWidth, 200), Math.Max(ActualHeight, MinHeight));
        _geometry = ConceptLayout.Build(vm.IsConceptRing ? ConceptShape.Ring : ConceptShape.Strip, nodes.Select(n => n.Key).ToList(), size);
        _back.Show(ConceptScene.Background(_geometry, nodes), null);
        _overlay.Show(Array.Empty<FenceShape>(), null);
        SyncNodes(vm, nodes);
        SetScroll(_scroll);
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
        AutomationProperties.SetName(_controllerChip, $"제어기 {vm.Controller.Name} — {WiringValidation.PORT_1} · {WiringValidation.PORT_2}");
        order.Add(_controllerChip);

        _controllerLamp ??= NewLamp(CONTROLLER_SIGNAL_ID, "제어기 통신", large: true);
        _controllerLamp.Level = vm.ControllerSignal;
        Canvas.SetLeft(_controllerLamp, geometry.Controller.Right + 58);
        Canvas.SetTop(_controllerLamp, geometry.Controller.Top + (geometry.Controller.Height - _controllerLamp.Height) / 2);
        ToolTipService.SetToolTip(_controllerLamp, vm.ControllerSignalText);

        foreach (var node in nodes)
        {
            var point = geometry.Nodes.First(n => n.Key == node.Key).Center;
            if (!_nodeChips.TryGetValue(node.Key, out var chip))
            {
                chip = NewChip(FenceChipKind.ConceptNode, node.Key);
                _nodeChips[node.Key] = chip;
            }
            chip.Picture = ConceptScene.Node(node, node.IsSelected);
            Place(chip, point);
            var ip = node.IsIp ? $", IP {node.Address}" : $", 노드 주소 {node.Address}";
            AutomationProperties.SetName(chip, $"#{node.Position} {node.Name}, 번호 {node.Number}{ip}, 통신 {SignalMath.Text(node.Signal)}");
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
            Canvas.SetLeft(lamp, point.X - lamp.Width / 2);
            Canvas.SetTop(lamp, point.Y + ConceptLayout.NODE_R + 5);
        }

        // 자식 순서 = 체인 순서(Tab · 자동화가 Ch1 쪽부터)
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
        if (!large) { lamp.Width = 28; lamp.Height = 11; }
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

    private void SetScroll(double value)
    {
        var max = Math.Max(0, (_geometry?.Extent.Width ?? 0) - ActualWidth);
        _scroll = Math.Clamp(value, 0, max);
        _scrollTransform.X = -_scroll;
    }

    private void OnChipFocused(object sender, RoutedEventArgs e)
    {
        if (_press is not null || _arming || sender is not FenceChip chip || ViewModel is not { } vm) return;
        if (chip.Kind == FenceChipKind.ConceptController) { if (!vm.IsControllerSelected) vm.FenceSelectController(); }
        else if (!vm.IsFenceSelected(chip.Key)) vm.FenceSelect(chip.Key);
        EnsureVisible(chip.Key);
    }

    private void EnsureVisible(int key)
    {
        if (ActualWidth <= 0 || _geometry is null) return;
        var x = ScreenCenterOf(key).X;
        if (x < 30) SetScroll(_scroll + x - 60);
        else if (x > ActualWidth - 30) SetScroll(_scroll + x - ActualWidth + 60);
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

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        SetScroll(_scroll - e.Delta * 0.5);
        e.Handled = true;
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
            FenceChipKind.ConceptController => FenceTargetKind.Enclosure,
            _ => FenceTargetKind.Empty,
        };
        _press = new Press(button, target, chip, at, _scroll, ctrl);
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
            // 개념도에서 왼쪽 끌기: 노드면 순서 옮기기 · 그 밖(빈 곳 · 제어기)은 가로 이동
            if (_press.Button == FencePointerButton.Left && _action != FenceGestureAction.MoveSensors) _action = FenceGestureAction.Pan;
            if (_action == FenceGestureAction.MoveSensors && _press.Chip is { } chip)
            {
                _dragKeys = vm.FenceDragKeys(chip.Key);
                foreach (var key in _dragKeys) if (_nodeChips.TryGetValue(key, out var dim)) dim.Opacity = 0.35;
            }
            Cursor = _action == FenceGestureAction.MoveSensors ? Cursors.SizeWE : Cursors.ScrollAll;
        }

        if (_action == FenceGestureAction.Pan)
        {
            SetScroll(_press.Scroll - (now.X - _press.Start.X));
            return;
        }
        if (_action != FenceGestureAction.MoveSensors) return;
        var gap = ConceptLayout.GapAt(_geometry, new Point(now.X + _scroll, now.Y));
        if (gap == _gap) return;                                  // 후보 틈이 바뀔 때만 다시 그린다(NFR-02)
        _gap = gap;
        var at = ConceptLayout.InsertionPoint(_geometry, gap);
        _overlay.Show(ConceptScene.Insertion(at), null);
        vm.NotifyFenceStatus($"개념도 — 여기 놓으면 {vm.ConceptDropLabel(_dragKeys, gap)} · Esc 취소");
    }

    internal void OnPointerReleased(Point at) => FinishDrag(true, at);

    /// <summary>끝내기는 이것 하나 — 뗌 · 캡처 상실 · Esc. 순서: ① 표지 ② 모습 ④ 캡처 ⑤ 통지.</summary>
    internal void FinishDrag(bool commit, Point? at = null)
    {
        var press = _press;
        if (press is null) return;
        var wasDragging = _dragging;
        var action = wasDragging ? _action : FenceGesture.Classify(press.Button, press.Ctrl, false, press.Target, isDrag: false);
        var gap = _gap;
        var keys = _dragKeys;

        // ① 표지
        _press = null;
        _dragging = false;
        _action = FenceGestureAction.None;
        _gap = null;
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
            if (wasDragging && action == FenceGestureAction.MoveSensors) vm.NotifyFenceStatus("취소 — 개념도 순서를 그대로 두었습니다(서버 호출 없음)");
            Rebuild();
            return;
        }

        switch (action)
        {
            case FenceGestureAction.MoveSensors when gap is { } g && keys.Count > 0:
                if (!vm.ConceptMove(keys, g)) Rebuild();
                else if (keys.Count == 1) vm.FenceSelect(keys[0]);
                break;
            case FenceGestureAction.SelectOne when press.Chip is { Kind: FenceChipKind.ConceptController }:
                vm.FenceSelectController();
                break;
            case FenceGestureAction.SelectOne when press.Chip is { } node:
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
        if (!SuppressMenuPopup) FenceMenuPresenter.Show(this, entries, at);
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
        if (HandleKeyDown(e.Key, e.SystemKey, Keyboard.Modifiers, Keyboard.FocusedElement as DependencyObject)) e.Handled = true;
    }

    /// <summary>키 판정 — 처리했으면 <c>true</c>(시험이 이 길로 부른다).</summary>
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

        var chain = vm.FenceChain.Keys.ToList();
        var at = chain.IndexOf(node.Key);
        if (alt && k is Key.Left or Key.Right) { vm.FenceStep(node.Key, k == Key.Left ? -1 : 1); FocusNode(node.Key); return true; }
        if (!alt && k is Key.Delete or Key.Back) { vm.FenceUnplace(vm.FenceDragKeys(node.Key)); return true; }
        if (!alt && k == Key.Space && modifiers == ModifierKeys.Control) { vm.FenceToggleSelect(node.Key); return true; }
        if (!alt && k is Key.Enter or Key.Space && modifiers == ModifierKeys.None) { vm.FenceSelect(node.Key); return true; }
        if (!alt && k is Key.Left or Key.Right && modifiers == ModifierKeys.Shift)
        {
            var j = at + (k == Key.Left ? -1 : 1);
            if (j >= 0 && j < chain.Count) { vm.FenceExtendSensorSelection(chain[j]); FocusNode(chain[j], quietly: true); }
            return true;
        }
        if (!alt && k is Key.Left or Key.Right or Key.Home or Key.End && modifiers == ModifierKeys.None)
        {
            var j = k switch { Key.Home => 0, Key.End => chain.Count - 1, Key.Left => at - 1, _ => at + 1 };
            if (j >= 0 && j < chain.Count) FocusNode(chain[j]);
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

/// <summary>개념도 peer — Pane · 자식은 노드 · 제어기 · 신호등 peer.</summary>
public sealed class FenceConceptViewAutomationPeer : FrameworkElementAutomationPeer
{
    public FenceConceptViewAutomationPeer(FenceConceptView owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(FenceConceptView);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => true;
}
