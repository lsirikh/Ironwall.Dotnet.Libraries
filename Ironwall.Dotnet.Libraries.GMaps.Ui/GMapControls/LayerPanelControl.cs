using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
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

    public ObservableCollection<IMapLayerModel> Layers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(LayersProperty); }
        set { SetValue(LayersProperty, value); }
    }

    public static readonly DependencyProperty LayersProperty =
        DependencyProperty.Register("Layers", typeof(ObservableCollection<IMapLayerModel>),
            typeof(LayerPanelControl), new PropertyMetadata(null, OnLayersChanged));

    private static void OnLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LayerPanelControl ctrl && e.NewValue is ObservableCollection<IMapLayerModel> layers)
        {
            ctrl.OverlayMapLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => l.LayerType == "OverlayMap"));
            ctrl.OverlayImageLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => l.LayerType == "OverlayImage"));
            ctrl.SymbolLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => l.LayerType == "Symbol"));

            // SYMBOLS 서브 그룹
            var pidsCategories = new[] { "PidsCamera", "PidsSensor", "PidsSpeaker", "PidsController", "PidsLamp", "PidsEnclosure" };
            ctrl.PidsEquipmentLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => pidsCategories.Contains(l.Category)));
            ctrl.PidsGroupLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => l.Category == "PidsGroup"));
            ctrl.MilitaryLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => l.Category == "Military"));
            var geoCategories = new[] { "Geometric", "Line", "Infra" };
            ctrl.GeometricInfraLayers = new ObservableCollection<IMapLayerModel>(layers.Where(l => geoCategories.Contains(l.Category)));
        }
    }

    public ObservableCollection<IMapLayerModel> OverlayMapLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(OverlayMapLayersProperty); }
        set { SetValue(OverlayMapLayersProperty, value); }
    }
    public static readonly DependencyProperty OverlayMapLayersProperty =
        DependencyProperty.Register("OverlayMapLayers", typeof(ObservableCollection<IMapLayerModel>),
            typeof(LayerPanelControl), new PropertyMetadata(null));

    public ObservableCollection<IMapLayerModel> OverlayImageLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(OverlayImageLayersProperty); }
        set { SetValue(OverlayImageLayersProperty, value); }
    }
    public static readonly DependencyProperty OverlayImageLayersProperty =
        DependencyProperty.Register("OverlayImageLayers", typeof(ObservableCollection<IMapLayerModel>),
            typeof(LayerPanelControl), new PropertyMetadata(null));

    public ObservableCollection<IMapLayerModel> SymbolLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(SymbolLayersProperty); }
        set { SetValue(SymbolLayersProperty, value); }
    }
    public static readonly DependencyProperty SymbolLayersProperty =
        DependencyProperty.Register("SymbolLayers", typeof(ObservableCollection<IMapLayerModel>),
            typeof(LayerPanelControl), new PropertyMetadata(null));

    // SYMBOLS 서브 그룹
    public ObservableCollection<IMapLayerModel> PidsEquipmentLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(PidsEquipmentLayersProperty); }
        set { SetValue(PidsEquipmentLayersProperty, value); }
    }
    public static readonly DependencyProperty PidsEquipmentLayersProperty =
        DependencyProperty.Register("PidsEquipmentLayers", typeof(ObservableCollection<IMapLayerModel>), typeof(LayerPanelControl));

    public ObservableCollection<IMapLayerModel> PidsGroupLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(PidsGroupLayersProperty); }
        set { SetValue(PidsGroupLayersProperty, value); }
    }
    public static readonly DependencyProperty PidsGroupLayersProperty =
        DependencyProperty.Register("PidsGroupLayers", typeof(ObservableCollection<IMapLayerModel>), typeof(LayerPanelControl));

    public ObservableCollection<IMapLayerModel> MilitaryLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(MilitaryLayersProperty); }
        set { SetValue(MilitaryLayersProperty, value); }
    }
    public static readonly DependencyProperty MilitaryLayersProperty =
        DependencyProperty.Register("MilitaryLayers", typeof(ObservableCollection<IMapLayerModel>), typeof(LayerPanelControl));

    public ObservableCollection<IMapLayerModel> GeometricInfraLayers
    {
        get { return (ObservableCollection<IMapLayerModel>)GetValue(GeometricInfraLayersProperty); }
        set { SetValue(GeometricInfraLayersProperty, value); }
    }
    public static readonly DependencyProperty GeometricInfraLayersProperty =
        DependencyProperty.Register("GeometricInfraLayers", typeof(ObservableCollection<IMapLayerModel>), typeof(LayerPanelControl));

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

    #endregion

    #region Constructor

    public LayerPanelControl()
    {
        CloseCommand = new RelayCommand(_ => OnCloseRequested());
        InitializeDragSupport();
        _opacityDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _opacityDebounceTimer.Tick += OnOpacityDebounce;
    }

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) => OnCloseRequested();

        if (GetTemplateChild("PART_LayerItemsControl") is ItemsControl itemsControl)
            itemsControl.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(OnLayerCheckChanged));

        if (GetTemplateChild("PART_LayerItemsControl") is ItemsControl ic2)
            ic2.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(OnLayerCheckChanged));
    }

    #endregion

    #region Layer Event Handlers

    private void OnLayerCheckChanged(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is CheckBox cb && cb.DataContext is IMapLayerModel layer)
        {
            layer.IsVisible = cb.IsChecked ?? false;
            LayerVisibilityChanged?.Invoke(this, new LayerChangedEventArgs(layer, layer.IsVisible));
        }
    }

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
        var pw = cp.ActualWidth > 0 ? cp.ActualWidth : 220;
        var ph = cp.ActualHeight > 0 ? cp.ActualHeight : 350;
        Canvas.SetLeft(cp, (canvas.ActualWidth - pw) / 2);
        Canvas.SetTop(cp, (canvas.ActualHeight - ph) / 2);
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
