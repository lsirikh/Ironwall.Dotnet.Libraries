using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.ObjectModel;
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
            leaf.OnMoveUpAction = RaiseLayerMoveUpRequested;
            leaf.OnMoveDownAction = RaiseLayerMoveDownRequested;
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
    public event EventHandler<LayerChangedEventArgs>? LayerMoveUpRequested;
    public event EventHandler<LayerChangedEventArgs>? LayerMoveDownRequested;
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

    internal void RaiseLayerMoveUpRequested(LayerTreeNode node)
    {
        if (node.Model != null)
            LayerMoveUpRequested?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked == true));
    }

    internal void RaiseLayerMoveDownRequested(LayerTreeNode node)
    {
        if (node.Model != null)
            LayerMoveDownRequested?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked == true));
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

    #region Constructor

    public LayerPanelControl()
    {
        CloseCommand = new RelayCommand(_ => OnCloseRequested());
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
        Canvas.SetLeft(cp, left + cur.X - _lastMousePosition.X);
        Canvas.SetTop(cp, top + cur.Y - _lastMousePosition.Y);
        _lastMousePosition = cur;
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
        // 좌상단 고정 위치 (툴바 아래, 좌표 표시 옆)
        Canvas.SetLeft(cp, 50);
        Canvas.SetTop(cp, 60);
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
