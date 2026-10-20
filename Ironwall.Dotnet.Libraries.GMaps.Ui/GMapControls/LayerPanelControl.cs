using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class LayerPanelControl : Control
{
    #region Static Constructor
    static LayerPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LayerPanelControl),
            new FrameworkPropertyMetadata(typeof(LayerPanelControl)));
    }
    #endregion

    #region Dependency Properties

    /// <summary>
    /// 트리 노드 컬렉션 (3-Tier: Section → Group → Leaf)
    /// </summary>
    public ObservableCollection<LayerTreeNode> TreeNodes
    {
        get { return (ObservableCollection<LayerTreeNode>)GetValue(TreeNodesProperty); }
        set { SetValue(TreeNodesProperty, value); }
    }

    public static readonly DependencyProperty TreeNodesProperty =
        DependencyProperty.Register("TreeNodes", typeof(ObservableCollection<LayerTreeNode>),
            typeof(LayerPanelControl), new PropertyMetadata(null, OnTreeNodesChanged));

    private static void OnTreeNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LayerPanelControl ctrl && e.NewValue is ObservableCollection<LayerTreeNode> nodes)
        {
            ctrl.SubscribeLeafCheckChanged(nodes);
            ctrl._selection.Remap(nodes);   // 재조회로 트리가 바뀌어도 Shift 기준 · 초점 행은 같은 항목으로
        }
    }

    private void SubscribeLeafCheckChanged(IEnumerable<LayerTreeNode> nodes)
    {
        foreach (var leaf in LayerTreeBuilder.Flatten(nodes))
        {
            leaf.CheckChanged -= OnLeafCheckChanged;
            leaf.CheckChanged += OnLeafCheckChanged;
            leaf.OpacityChanged -= OnLeafOpacityChanged;
            leaf.OpacityChanged += OnLeafOpacityChanged;
            leaf.LockChanged -= OnLeafLockChanged;
            leaf.LockChanged += OnLeafLockChanged;

            // ContextMenu Command → 이벤트 라우팅
            leaf.OnDeleteAction = RaiseLayerDeleteRequested;
            // 우클릭 '위로/아래로' 도 끌기 · Alt+↑↓ 와 같은 한 경로(LayerReorderRequested)로 보낸다(D-36).
            leaf.OnMoveUpAction = n => RaiseLayerStepRequested(n, -1);
            leaf.OnMoveDownAction = n => RaiseLayerStepRequested(n, +1);
            // 개별 심볼 리프는 심볼 전용 rename/navigate, 그 외(오버레이 Leaf)는 기존 레이어 경로 (FR-01/04)
            leaf.OnRenameAction = leaf.IsSymbolLeaf ? RaiseSymbolRenameRequested : RaiseLayerRenameRequested;
            leaf.OnNavigateAction = leaf.IsSymbolLeaf ? RaiseSymbolNavigateRequested : RaiseLayerNavigateRequested;
        }
    }

    /// <summary>패널 닫힘/트리 교체 시 leaf 구독·델리게이트 해제 — 닫힌 컨트롤이 트리 노드에 잡혀 누수되는 것 방지.</summary>
    internal void UnsubscribeLeaves()
    {
        if (TreeNodes == null) return;
        foreach (var leaf in LayerTreeBuilder.Flatten(TreeNodes))
        {
            leaf.CheckChanged -= OnLeafCheckChanged;
            leaf.OpacityChanged -= OnLeafOpacityChanged;
            leaf.LockChanged -= OnLeafLockChanged;
            leaf.OnDeleteAction = null;
            leaf.OnMoveUpAction = null;
            leaf.OnMoveDownAction = null;
            leaf.OnRenameAction = null;
            leaf.OnNavigateAction = null;
        }
        _opacityDebounceTimer.Stop();
    }

    private void OnLeafCheckChanged(object? sender, EventArgs e)
    {
        if (sender is not LayerTreeNode node) return;
        // 개별 심볼 리프(Model=null, Symbol≠null)는 심볼 전용 이벤트로 라우팅 — Model 게이팅에 막히지 않게(FR-01)
        if (node.IsSymbolLeaf && node.Symbol != null)
            SymbolVisibilityChanged?.Invoke(this, new SymbolVisibilityChangedEventArgs(node.Symbol, node.IsChecked ?? true));
        else if (node.Model != null)
            LayerVisibilityChanged?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked ?? true));
    }

    private void OnLeafOpacityChanged(object? sender, EventArgs e)
    {
        if (sender is LayerTreeNode node && node.Model != null)
        {
            NotifyOpacityChanged(node.Model, node.Opacity);
        }
    }

    public string PanelTitle
    {
        get { return (string)GetValue(PanelTitleProperty); }
        set { SetValue(PanelTitleProperty, value); }
    }

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register("PanelTitle", typeof(string),
            typeof(LayerPanelControl), new PropertyMetadata("레이어"));

    #endregion

    #region Commands

    public ICommand CloseCommand
    {
        get { return (ICommand)GetValue(CloseCommandProperty); }
        set { SetValue(CloseCommandProperty, value); }
    }

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register("CloseCommand", typeof(ICommand),
            typeof(LayerPanelControl));

    #endregion

    #region Events

    public event EventHandler<LayerChangedEventArgs>? LayerVisibilityChanged;
    public event EventHandler<LayerOpacityChangedEventArgs>? LayerOpacityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler<LayerChangedEventArgs>? LayerDeleteRequested;
    /// <summary>
    /// 오버레이 레이어 순서 바꾸기(D-36) — 끌기 · Alt+↑↓ · 우클릭 '위로/아래로' 의 단일 경로.
    /// 예전 LayerMoveUp/DownRequested(두 행 스왑 · 두 번의 개별 UPDATE)를 대체한다.
    /// </summary>
    public event EventHandler<LayerReorderRequestedEventArgs>? LayerReorderRequested;
    public event EventHandler<LayerRenameEventArgs>? LayerRenameRequested;
    public event EventHandler<LayerChangedEventArgs>? LayerNavigateRequested;
    public event EventHandler<SymbolVisibilityChangedEventArgs>? SymbolVisibilityChanged;
    public event EventHandler<SymbolNavigateRequestedEventArgs>? SymbolNavigateRequested;
    public event EventHandler<SymbolRenameRequestedEventArgs>? SymbolRenameRequested;
    public event EventHandler<SymbolLockChangedEventArgs>? SymbolLockChanged;
    public event EventHandler<LayerLockChangedEventArgs>? LayerLockChanged;
    /// <summary>리사이즈 드래그 완료 시 최종 크기(영속용, FR-06).</summary>
    public event EventHandler<Size>? PanelSizeCommitted;

    internal void RaiseLayerDeleteRequested(LayerTreeNode node)
    {
        if (node.Model != null)
            LayerDeleteRequested?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked == true));
    }

    /// <summary>
    /// 오버레이 목록(ListBox)의 드롭 담당 — 템플릿이 <c>drag:DropZone.Handler</c> 로 물린다.
    /// 끌기(<c>CaptureDragBehavior</c>)와 Alt+↑↓(<c>ReorderKeyboardBehavior</c>)가 같은 이 담당을 부른다.
    /// </summary>
    public IDragDropHandler ReorderHandler { get; }

    /// <summary>우클릭 '위로'(-1) · '아래로'(+1) — 한 칸 삽입으로 바꿔 끌기와 같은 판정 · 같은 이벤트로 보낸다.</summary>
    internal void RaiseLayerStepRequested(LayerTreeNode node, int direction)
    {
        var section = node.Parent;
        if (section == null) return;
        var insertion = LayerReorderRules.StepInsertion(section.Children.IndexOf(node), direction, section.Children.Count);
        if (insertion < 0) return;
        if (LayerReorderRules.TryPlan(section, new[] { node }, insertion, out var newOrder))
            RaiseLayerReorderRequested(section, newOrder, "menu");
    }

    private void RaiseLayerReorderRequested(LayerTreeNode section, IReadOnlyList<LayerTreeNode> newOrder, string source)
        => LayerReorderRequested?.Invoke(this, new LayerReorderRequestedEventArgs(section, newOrder, source));

    /// <summary>
    /// 이 패널 안에서 레이어 행을 <b>끄는 중</b>인가(데드존을 넘긴 뒤). 지도 모드(조준 · 배치 · 측정 · 드로잉)의
    /// 창 전역 Esc 후킹이 이 값을 보고 양보한다 — 그 후킹들이 커널보다 먼저 창에 구독돼 있어, 양보하지 않으면
    /// Esc 가 지도 모드만 끄고(Handled) 끌기 취소는 커널에 닿지 않는다.
    /// </summary>
    public bool IsReorderDragging
    {
        get
        {
            // 손잡이(Thumb)가 눌린 동안 마우스를 쥐고 있다. 그 손잡이를 품은 목록의 커널이 끄는 중인지 묻는다.
            if (Mouse.Captured is not DragHandle handle || !handle.IsDescendantOf(this)) return false;
            for (DependencyObject? d = handle; d != null && !ReferenceEquals(d, this); d = VisualTreeHelper.GetParent(d))
            {
                if (d is ItemsControl list
                    && Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(list).OfType<CaptureDragBehavior>().Any(b => b.IsDragging))
                    return true;
            }
            return false;
        }
    }

    internal void RaiseLayerRenameRequested(LayerTreeNode node, string newName)
    {
        if (node.Model != null)
            LayerRenameRequested?.Invoke(this, new LayerRenameEventArgs(node.Model, newName));
    }

    internal void RaiseLayerNavigateRequested(LayerTreeNode node)
    {
        if (node.Model != null)
            LayerNavigateRequested?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked == true));
    }

    internal void RaiseSymbolNavigateRequested(LayerTreeNode node)
    {
        if (node.Symbol != null)
            SymbolNavigateRequested?.Invoke(this, new SymbolNavigateRequestedEventArgs(node.Symbol));
    }

    internal void RaiseSymbolRenameRequested(LayerTreeNode node, string newName)
    {
        if (node.Symbol != null)
            SymbolRenameRequested?.Invoke(this, new SymbolRenameRequestedEventArgs(node.Symbol, newName));
    }

    /// <summary>잠금 토글 라우팅 — 심볼 리프는 SymbolLockChanged, Overlay 이미지 리프는 LayerLockChanged(FR-03).</summary>
    private void OnLeafLockChanged(object? sender, EventArgs e)
    {
        if (sender is not LayerTreeNode node) return;
        if (node.IsSymbolLeaf && node.Symbol != null)
            SymbolLockChanged?.Invoke(this, new SymbolLockChangedEventArgs(node.Symbol, node.IsLocked));
        else if (node.Model != null)
            LayerLockChanged?.Invoke(this, new LayerLockChangedEventArgs(node.Model, node.IsLocked));
    }

    #endregion

    #region Selection (클릭 · Ctrl · Shift · 키보드 다중 선택)

    private readonly LayerSelection _selection = new();

    /// <summary>
    /// 사용자가 패널에서 선택을 바꿨다. 편집 모드면 VM 이 지도 마커 선택(단일 · 묶음)으로 옮긴다.
    /// 프로그램이 노드 IsSelected 를 바꿀 때(지도 → 패널)는 나지 않는다.
    /// </summary>
    public event EventHandler<LayerSelectionChangedEventArgs>? LayerSelectionChanged;

    /// <summary>지금 선택된 잎(트리 순서).</summary>
    public IReadOnlyList<LayerTreeNode> SelectedNodes => TreeNodes == null ? Array.Empty<LayerTreeNode>() : LayerSelection.SelectedLeaves(TreeNodes);

    /// <summary>잎 행 클릭(마우스 처리기 · 시험이 같은 길을 탄다). 반환 = 선택이 바뀌었는가.</summary>
    public bool SelectNode(LayerTreeNode node, ModifierKeys modifiers)
        => TreeNodes != null && RaiseSelectionIf(_selection.Click(TreeNodes, node, modifiers));

    /// <summary>Ctrl+묶음 머리 클릭 — 그 묶음 잎 전부를 더한다. 반환 = 선택이 바뀌었는가.</summary>
    public bool SelectGroup(LayerTreeNode header)
        => TreeNodes != null && RaiseSelectionIf(_selection.AddGroup(TreeNodes, header));

    /// <summary>선택 키보드 폴백(↑/↓ · Shift · Ctrl · Space · Ctrl+A · Esc). 반환 = 이 키를 받았는가.</summary>
    public bool HandleSelectionKey(Key key, ModifierKeys modifiers)
    {
        if (TreeNodes == null) return false;
        bool handled = _selection.HandleKey(TreeNodes, key, modifiers, out var changed);
        RaiseSelectionIf(changed);
        if (handled && _selection.Current != null) FocusRow(_selection.Current);
        return handled;
    }

    /// <summary>
    /// 패널에 초점이 있을 때 패널이 가져가는 선택 키인가 — 지도 창 후킹(방향키 이동 · Ctrl+A 전체 선택)이 이 키를 양보한다.
    /// Alt+↑/↓ 는 순서 바꾸기라 넣지 않는다.
    /// </summary>
    public static bool IsSelectionKey(Key key, ModifierKeys modifiers)
    {
        if ((modifiers & ModifierKeys.Alt) != 0) return false;
        return key is Key.Up or Key.Down or Key.Space or Key.Escape
            || (key == Key.A && (modifiers & ModifierKeys.Control) != 0);
    }

    /// <summary>그 행이 보이게 패널을 굴린다(레이아웃 뒤 — 방금 펼친 묶음의 행이 생긴 다음).</summary>
    public void BringNodeIntoView(LayerTreeNode node)
        => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => FindRow(node)?.BringIntoView()));

    private bool RaiseSelectionIf(bool changed)
    {
        if (changed) LayerSelectionChanged?.Invoke(this, new LayerSelectionChangedEventArgs(SelectedNodes));
        return changed;
    }

    /// <summary>
    /// 행 클릭 = 선택. 체크박스 · 자물쇠 · 펼치기 단추 · 슬라이더 · 끌기 손잡이 · 이름 편집 칸은 제 일을 하고 선택은 그대로다.
    /// 묶음 머리는 Ctrl+클릭만 받는다(그냥 클릭은 예전 그대로 — 펼치기/접기는 셰브런).
    /// </summary>
    private void OnRowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _handleGesture = false;
        if (!TryHitRow(e.OriginalSource as DependencyObject, out var node, out var blocker))
        {
            _handleGesture = blocker is DragHandle;   // 손잡이 클릭(데드존 미만)은 커널이 목록 선택으로 그 행을 고른다 → OnListSelectionChanged
            return;
        }
        var modifiers = Keyboard.Modifiers;
        if (node.NodeType == LayerNodeType.Leaf)
        {
            SelectNode(node, modifiers);
            e.Handled = true;   // 목록(ListBox)이 제 규칙으로 다시 고르지 않게
            FocusRow(node);
        }
        else if ((modifiers & ModifierKeys.Control) != 0 && SelectGroup(node))
        {
            e.Handled = true;
            Focus();
        }
    }

    private void OnSelectionPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 글 칸 · 체크박스 · 단추 · 슬라이더가 초점이면 그 키는 그쪽 몫(Space = 체크, ←/→ = 슬라이더, Esc = "?" 닫기)
        if (IsOwnInput(Keyboard.FocusedElement as DependencyObject) || IsOwnInput(e.OriginalSource as DependencyObject)) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandleSelectionKey(key, Keyboard.Modifiers)) e.Handled = true;
    }

    /// <summary>우클릭 — 고르지 않은 잎이면 그 행만 고른다(탐색기 관례 · 목록 행의 기본 동작과 같게). 메뉴는 그대로 뜬다.</summary>
    private void OnRowPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryHitRow(e.OriginalSource as DependencyObject, out var node, out _) && node.NodeType == LayerNodeType.Leaf && !node.IsSelected)
            SelectNode(node, ModifierKeys.None);
    }

    private bool _handleGesture;

    /// <summary>끌기 손잡이 클릭으로 커널이 목록 선택을 그 행 하나로 바꿨다 — 패널 전체에서도 그 행만 고른 것으로 맞춘다.</summary>
    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_handleGesture || LayerSelection.IsApplying || e.OriginalSource is not ListBox) return;
        _handleGesture = false;
        if (e.AddedItems.Count > 0 && e.AddedItems[e.AddedItems.Count - 1] is LayerTreeNode added) SelectNode(added, ModifierKeys.None);
    }

    private static bool IsOwnInput(DependencyObject? d)
        => d is ButtonBase or Thumb or Slider or TextBoxBase or ComboBox or ScrollBar;

    private bool TryHitRow(DependencyObject? source, out LayerTreeNode node, out DependencyObject? blocker)
    {
        node = null!;
        blocker = null;
        for (var d = source; d != null && !ReferenceEquals(d, this); d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (IsOwnInput(d)) { blocker = d; return false; }
            if (d is FrameworkElement { DataContext: LayerTreeNode hit } && node == null) node = hit;
        }
        return node != null && node.NodeType != LayerNodeType.Section;
    }

    /// <summary>행의 컨테이너(목록 행 · 카테고리 잎 칸) — 데이터로 찾는다(가상화 · 인덱스 산술 없음).</summary>
    private FrameworkElement? FindRow(LayerTreeNode node)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is FrameworkElement { DataContext: LayerTreeNode n } fe && ReferenceEquals(n, node)) return fe;
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        return null;
    }

    /// <summary>초점을 그 행으로 — 목록 행이면 그 ListBoxItem(Alt+↑/↓ 순서 바꾸기가 이어서 된다), 아니면 패널.</summary>
    private void FocusRow(LayerTreeNode node)
    {
        var row = FindRow(node);
        for (DependencyObject? d = row; d != null && !ReferenceEquals(d, this); d = VisualTreeHelper.GetParent(d))
            if (d is ListBoxItem item) { item.Focus(); row!.BringIntoView(); return; }
        Focus();
        row?.BringIntoView();
    }

    #endregion

    #region Constructor

    public LayerPanelControl()
    {
        CloseCommand = new RelayCommand(_ => OnCloseRequested());
        PreviewMouseLeftButtonDown += OnRowPreviewMouseLeftButtonDown;
        PreviewMouseRightButtonDown += OnRowPreviewMouseRightButtonDown;
        PreviewKeyDown += OnSelectionPreviewKeyDown;
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnListSelectionChanged));
        ReorderHandler = new LayerReorderDropHandler((section, newOrder) => RaiseLayerReorderRequested(section, newOrder, "list"));
        InitializeDragSupport();
        _opacityDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _opacityDebounceTimer.Tick += OnOpacityDebounce;

        // 리사이즈 경계(FR-05). 폭=고정 250 기본, 높이=초기 Auto + MaxHeight 캡(off-canvas·무한성장 차단).
        Width = MinPanelWidth;
        MinWidth = MinPanelWidth; MaxWidth = MaxPanelWidth;
        MaxHeight = MaxPanelHeight;

        // CheckBox 이벤트는 LayerTreeNode.CheckChanged로 직접 구독 (OnTreeNodesChanged에서)
    }

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) => OnCloseRequested();

        WireResizeGrip(GetTemplateChild("PART_ResizeRight") as System.Windows.Controls.Primitives.Thumb, resizeW: true, resizeH: false);
        WireResizeGrip(GetTemplateChild("PART_ResizeBottom") as System.Windows.Controls.Primitives.Thumb, resizeW: false, resizeH: true);
        WireResizeGrip(GetTemplateChild("PART_ResizeCorner") as System.Windows.Controls.Primitives.Thumb, resizeW: true, resizeH: true);
    }

    #endregion

    #region Layer Event Handlers

    public void NotifyOpacityChanged(IMapLayerModel layer, double opacity)
    {
        _pendingOpacityLayer = layer;
        _pendingOpacity = opacity;
        _opacityDebounceTimer.Stop();
        _opacityDebounceTimer.Start();
    }

    private void OnOpacityDebounce(object? sender, EventArgs e)
    {
        _opacityDebounceTimer.Stop();
        if (_pendingOpacityLayer != null)
        {
            _pendingOpacityLayer.Opacity = _pendingOpacity;
            LayerOpacityChanged?.Invoke(this, new LayerOpacityChangedEventArgs(_pendingOpacityLayer, _pendingOpacity));
            _pendingOpacityLayer = null;
        }
    }

    private void OnCloseRequested()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Drag + Center

    private bool _isDragging;
    private Point _lastMousePosition;
    private readonly DispatcherTimer _opacityDebounceTimer;
    private IMapLayerModel? _pendingOpacityLayer;
    private double _pendingOpacity;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing) return;                 // 리사이즈 중에는 이동 비활성(영역 분리 보강)
        var position = e.GetPosition(this);
        if (position.Y > 36) return;

        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null) return;

        var cur = e.GetPosition(canvas);
        var left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
        var top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
        var (newLeft, newTop) = ClampHeaderInside(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, canvas);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
        _lastMousePosition = cur;
    }

    /// <summary>
    /// 머리 끌기 위치를 지도(Canvas) 안으로 자른다(LAY-D3) — 패널 폭 전체와 머리(36px)가 지도 안에 남아 다시 잡을 수 있게.
    /// 크기 조절(OnResizeDelta)의 Canvas 경계 클램프 · 팔레트 머리 끌기(SymbolPaletteView.ConstrainToCanvas)와 같은 규칙.
    /// 지도 크기를 아직 모르면(ActualWidth 0) 자르지 않는다.
    /// </summary>
    private (double Left, double Top) ClampHeaderInside(double left, double top, Canvas canvas)
    {
        const double HeaderHeight = 36;
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return (left, top);
        double width = double.IsNaN(Width) ? ActualWidth : Width;
        return (Math.Clamp(left, 0, Math.Max(0, canvas.ActualWidth - width)),
                Math.Clamp(top, 0, Math.Max(0, canvas.ActualHeight - HeaderHeight)));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    public void CenterInCanvas()
    {
        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null || cp == null) return;
        // 좌상단 — 왼쪽 위 묶음(좌표 · 3D 단추, GMaps.Map.TopLeftStack) 바로 아래 같은 왼쪽 줄(2026-10-20: 3D 단추가 좌표 아래로 내려가 덮지 않게).
        //   묶음을 못 찾으면 예전 자리(툴바 아래 50, 60).
        double left = 50, top = 60;
        DependencyObject root = canvas;
        while (VisualTreeHelper.GetParent(root) is { } up) root = up;
        if (FindById(root, "GMaps.Map.TopLeftStack") is { ActualWidth: > 0, IsVisible: true } stack)
        {
            try
            {
                var p = stack.TransformToVisual(canvas).Transform(new Point(0, stack.ActualHeight));
                left = p.X; top = Math.Max(top, p.Y + 6);
                // 그 아래 같은 줄에 붙은 바람 배지도 피한다
                if (FindVisible<GMapWindyIndicatorControl>(root) is { } windy)
                {
                    var w = windy.TransformToVisual(canvas).TransformBounds(new Rect(windy.RenderSize));
                    if (Math.Abs(w.Left - left) < 24 && w.Top < top + 24) top = Math.Max(top, w.Bottom + 6);
                }
            }
            catch (InvalidOperationException) { }   // 공통 조상 없음
        }
        var (l, t) = ClampHeaderInside(left, top, canvas);
        Canvas.SetLeft(cp, l);
        Canvas.SetTop(cp, t);
    }

    private static T? FindVisible<T>(DependencyObject root) where T : FrameworkElement
    {
        for (int i = 0, n = VisualTreeHelper.GetChildrenCount(root); i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t && t.IsVisible && t.ActualWidth > 0) return t;
            if (FindVisible<T>(c) is { } hit) return hit;
        }
        return null;
    }

    private static FrameworkElement? FindById(DependencyObject root, string id)
    {
        for (int i = 0, n = VisualTreeHelper.GetChildrenCount(root); i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is FrameworkElement fe && System.Windows.Automation.AutomationProperties.GetAutomationId(fe) == id) return fe;
            if (FindById(c, id) is { } hit) return hit;
        }
        return null;
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    #endregion

    #region Resize (E/S/SE 그립 — 좌상단 앵커 고정, FR-05/06)

    private const double MinPanelWidth = 250, MaxPanelWidth = 375;
    private const double MinPanelHeight = 420, MaxPanelHeight = 630;
    private bool _isResizing;

    private void WireResizeGrip(System.Windows.Controls.Primitives.Thumb? thumb, bool resizeW, bool resizeH)
    {
        if (thumb == null) return;
        thumb.DragStarted += (s, e) => _isResizing = true;
        thumb.DragDelta += (s, e) => OnResizeDelta(e, resizeW, resizeH);
        thumb.DragCompleted += (s, e) =>
        {
            _isResizing = false;
            PanelSizeCommitted?.Invoke(this, new Size(ActualWidth, ActualHeight));
        };
    }

    private void OnResizeDelta(System.Windows.Controls.Primitives.DragDeltaEventArgs e, bool resizeW, bool resizeH)
    {
        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;

        if (resizeW)
        {
            double curW = double.IsNaN(Width) ? ActualWidth : Width;   // 백킹값 사용(빠른 드래그 시 ActualWidth 지연 방지)
            double newW = Math.Min(Math.Max(curW + e.HorizontalChange, MinPanelWidth), MaxPanelWidth);
            if (canvas != null && cp != null)
            {
                double left = Canvas.GetLeft(cp); if (double.IsNaN(left)) left = 0;
                newW = Math.Min(newW, Math.Max(MinPanelWidth, canvas.ActualWidth - left - 8));   // Canvas 경계 2차 클램프
            }
            Width = newW;
        }
        if (resizeH)
        {
            if (double.IsNaN(Height)) Height = ActualHeight;     // 초기 Auto → 드래그 시점에 명시 높이로 전환
            if (MinHeight < MinPanelHeight) MinHeight = MinPanelHeight;
            double newH = Math.Min(Math.Max(Height + e.VerticalChange, MinPanelHeight), MaxPanelHeight);
            if (canvas != null && cp != null)
            {
                double top = Canvas.GetTop(cp); if (double.IsNaN(top)) top = 0;
                newH = Math.Min(newH, Math.Max(MinPanelHeight, canvas.ActualHeight - top - 8));
            }
            Height = newH;
        }
    }

    /// <summary>영속된 크기 복원(FR-06). 범위 밖이면 해당 축 무시.</summary>
    public void SetPanelSize(double width, double height)
    {
        if (width >= MinPanelWidth && width <= MaxPanelWidth) Width = width;
        if (height >= MinPanelHeight && height <= MaxPanelHeight)
        {
            MinHeight = MinPanelHeight;
            Height = height;
        }
    }

    #endregion
}
