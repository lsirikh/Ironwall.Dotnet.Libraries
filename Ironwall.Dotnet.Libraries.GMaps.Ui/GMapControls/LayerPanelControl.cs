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

            // ContextMenu Command → 이벤트 라우팅
            leaf.OnDeleteAction = RaiseLayerDeleteRequested;
            leaf.OnMoveUpAction = RaiseLayerMoveUpRequested;
            leaf.OnMoveDownAction = RaiseLayerMoveDownRequested;
            leaf.OnRenameAction = RaiseLayerRenameRequested;
            leaf.OnNavigateAction = RaiseLayerNavigateRequested;
        }
    }

    private void OnLeafCheckChanged(object? sender, EventArgs e)
    {
        if (sender is LayerTreeNode node && node.Model != null)
        {
            LayerVisibilityChanged?.Invoke(this, new LayerChangedEventArgs(node.Model, node.IsChecked ?? true));
        }
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

    #endregion

    #region Constructor

    public LayerPanelControl()
    {
        CloseCommand = new RelayCommand(_ => OnCloseRequested());
        InitializeDragSupport();
        _opacityDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _opacityDebounceTimer.Tick += OnOpacityDebounce;

        // CheckBox 이벤트는 LayerTreeNode.CheckChanged로 직접 구독 (OnTreeNodesChanged에서)
    }

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) => OnCloseRequested();
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
}
