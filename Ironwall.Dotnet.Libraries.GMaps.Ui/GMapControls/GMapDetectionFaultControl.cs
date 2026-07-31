using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 탐지·장애 상태 인디케이터 CustomControl (GMap_Map_Instruments FR-05~08b)
                  — 활성(미조치) 탐지/장애 건수 집계 pill 2개 + 드래그 이동 + 우클릭 설정
                  + 클릭 시 이벤트 패널 오픈. 소스=EventQueueManager(D-01). 계기층 z6(D-06).
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
****************************************************************************/
public class GMapDetectionFaultControl : Control
{
    private const double DragDeadZone = 8.0;

    private FrameworkElement? _root;
    private Canvas? _parentCanvas;
    private Canvas? _hookedCanvas;
    private ContextMenu? _menu;
    private bool _pressed, _dragging;
    private Point _startOnCanvas;
    private double _startX, _startY;

    static GMapDetectionFaultControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapDetectionFaultControl),
            new FrameworkPropertyMetadata(typeof(GMapDetectionFaultControl)));
    }

    public GMapDetectionFaultControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region - Dependency Properties -

    /// <summary>활성(미조치) 탐지 건수 — EQM 소스.</summary>
    public int DetectionCount { get => (int)GetValue(DetectionCountProperty); set => SetValue(DetectionCountProperty, value); }
    public static readonly DependencyProperty DetectionCountProperty =
        DependencyProperty.Register(nameof(DetectionCount), typeof(int), typeof(GMapDetectionFaultControl),
            new PropertyMetadata(0, OnDetectionChanged));

    /// <summary>활성(미조치) 장애 건수 — EQM 소스.</summary>
    public int FaultCount { get => (int)GetValue(FaultCountProperty); set => SetValue(FaultCountProperty, value); }
    public static readonly DependencyProperty FaultCountProperty =
        DependencyProperty.Register(nameof(FaultCount), typeof(int), typeof(GMapDetectionFaultControl),
            new PropertyMetadata(0, OnFaultChanged));

    // 파생(읽기전용) — 배지 텍스트·활성·pill 표시
    public string DetectionText { get => (string)GetValue(DetectionTextProperty); private set => SetValue(DetectionTextPropertyKey, value); }
    private static readonly DependencyPropertyKey DetectionTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DetectionText), typeof(string), typeof(GMapDetectionFaultControl), new PropertyMetadata("0"));
    public static readonly DependencyProperty DetectionTextProperty = DetectionTextPropertyKey.DependencyProperty;

    public string FaultText { get => (string)GetValue(FaultTextProperty); private set => SetValue(FaultTextPropertyKey, value); }
    private static readonly DependencyPropertyKey FaultTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FaultText), typeof(string), typeof(GMapDetectionFaultControl), new PropertyMetadata("0"));
    public static readonly DependencyProperty FaultTextProperty = FaultTextPropertyKey.DependencyProperty;

    public bool DetectionActive { get => (bool)GetValue(DetectionActiveProperty); private set => SetValue(DetectionActivePropertyKey, value); }
    private static readonly DependencyPropertyKey DetectionActivePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DetectionActive), typeof(bool), typeof(GMapDetectionFaultControl), new PropertyMetadata(false));
    public static readonly DependencyProperty DetectionActiveProperty = DetectionActivePropertyKey.DependencyProperty;

    public bool FaultActive { get => (bool)GetValue(FaultActiveProperty); private set => SetValue(FaultActivePropertyKey, value); }
    private static readonly DependencyPropertyKey FaultActivePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FaultActive), typeof(bool), typeof(GMapDetectionFaultControl), new PropertyMetadata(false));
    public static readonly DependencyProperty FaultActiveProperty = FaultActivePropertyKey.DependencyProperty;

    /// <summary>탐지 pill 표시 여부(HideOnZero+0건이면 숨김) — 읽기전용.</summary>
    public Visibility DetectionPillVisibility { get => (Visibility)GetValue(DetectionPillVisibilityProperty); private set => SetValue(DetectionPillVisibilityPropertyKey, value); }
    private static readonly DependencyPropertyKey DetectionPillVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DetectionPillVisibility), typeof(Visibility), typeof(GMapDetectionFaultControl), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty DetectionPillVisibilityProperty = DetectionPillVisibilityPropertyKey.DependencyProperty;

    public Visibility FaultPillVisibility { get => (Visibility)GetValue(FaultPillVisibilityProperty); private set => SetValue(FaultPillVisibilityPropertyKey, value); }
    private static readonly DependencyPropertyKey FaultPillVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FaultPillVisibility), typeof(Visibility), typeof(GMapDetectionFaultControl), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty FaultPillVisibilityProperty = FaultPillVisibilityPropertyKey.DependencyProperty;

    /// <summary>배치(세로/가로).</summary>
    public InstrumentOrientation Orientation { get => (InstrumentOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(InstrumentOrientation), typeof(GMapDetectionFaultControl),
            new FrameworkPropertyMetadata(InstrumentOrientation.Vertical, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>0건 pill 숨김(FR-08).</summary>
    public bool HideOnZero { get => (bool)GetValue(HideOnZeroProperty); set => SetValue(HideOnZeroProperty, value); }
    public static readonly DependencyProperty HideOnZeroProperty =
        DependencyProperty.Register(nameof(HideOnZero), typeof(bool), typeof(GMapDetectionFaultControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPillVisibilityInputChanged));

    /// <summary>보기(View) 메뉴 마스터 가시성(FR-15).</summary>
    public bool IsInstrumentVisible { get => (bool)GetValue(IsInstrumentVisibleProperty); set => SetValue(IsInstrumentVisibleProperty, value); }
    public static readonly DependencyProperty IsInstrumentVisibleProperty =
        DependencyProperty.Register(nameof(IsInstrumentVisible), typeof(bool), typeof(GMapDetectionFaultControl),
            new PropertyMetadata(true, OnMasterVisibilityChanged));

    public double DetFaultX { get => (double)GetValue(DetFaultXProperty); set => SetValue(DetFaultXProperty, value); }
    public static readonly DependencyProperty DetFaultXProperty =
        DependencyProperty.Register(nameof(DetFaultX), typeof(double), typeof(GMapDetectionFaultControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));

    public double DetFaultY { get => (double)GetValue(DetFaultYProperty); set => SetValue(DetFaultYProperty, value); }
    public static readonly DependencyProperty DetFaultYProperty =
        DependencyProperty.Register(nameof(DetFaultY), typeof(double), typeof(GMapDetectionFaultControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));

    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }
    public static readonly DependencyProperty DragCompletedCommandProperty =
        DependencyProperty.Register(nameof(DragCompletedCommand), typeof(ICommand), typeof(GMapDetectionFaultControl), new PropertyMetadata(null));

    public ICommand? SettingsChangedCommand { get => (ICommand?)GetValue(SettingsChangedCommandProperty); set => SetValue(SettingsChangedCommandProperty, value); }
    public static readonly DependencyProperty SettingsChangedCommandProperty =
        DependencyProperty.Register(nameof(SettingsChangedCommand), typeof(ICommand), typeof(GMapDetectionFaultControl), new PropertyMetadata(null));

    /// <summary>클릭(드래그 미승격) → 이벤트 패널 오픈(D-10). 호스트가 OpenEventPanelMessageModel publish.</summary>
    public ICommand? ClickCommand { get => (ICommand?)GetValue(ClickCommandProperty); set => SetValue(ClickCommandProperty, value); }
    public static readonly DependencyProperty ClickCommandProperty =
        DependencyProperty.Register(nameof(ClickCommand), typeof(ICommand), typeof(GMapDetectionFaultControl), new PropertyMetadata(null));

    private static void OnDetectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapDetectionFaultControl)d; int n = (int)e.NewValue;
        c.DetectionText = InstrumentMath.CountBadgeText(n);
        c.DetectionActive = InstrumentMath.IsActive(n);
        c.UpdatePillVisibility();
    }
    private static void OnFaultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapDetectionFaultControl)d; int n = (int)e.NewValue;
        c.FaultText = InstrumentMath.CountBadgeText(n);
        c.FaultActive = InstrumentMath.IsActive(n);
        c.UpdatePillVisibility();
    }
    private static void OnPillVisibilityInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapDetectionFaultControl)d).UpdatePillVisibility();
    private static void OnMasterVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapDetectionFaultControl)d).ApplyMasterVisibility();
    private static void OnPosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapDetectionFaultControl)d).UpdateDisplayPosition();

    #endregion

    #region - Lifecycle -

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _parentCanvas = FindParentCanvas();
        SizeChanged -= OnControlSizeChanged; SizeChanged += OnControlSizeChanged;
        ApplyMasterVisibility(); UpdatePillVisibility();
        TryPosition();
    }
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnControlSizeChanged;
        if (_hookedCanvas != null) { _hookedCanvas.SizeChanged -= OnCanvasSizeChanged; _hookedCanvas = null; }
        ResetInteraction();
    }

    private void ApplyMasterVisibility()
        => Visibility = IsInstrumentVisible ? Visibility.Visible : Visibility.Collapsed;

    private void UpdatePillVisibility()
    {
        DetectionPillVisibility = (HideOnZero && !DetectionActive) ? Visibility.Collapsed : Visibility.Visible;
        FaultPillVisibility = (HideOnZero && !FaultActive) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResetInteraction() { _pressed = false; _dragging = false; }
    private void OnControlSizeChanged(object? sender, SizeChangedEventArgs e) => TryPosition();
    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateDisplayPosition();

    private void TryPosition()
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null) return;
        if (!ReferenceEquals(_hookedCanvas, _parentCanvas))
        {
            if (_hookedCanvas != null) _hookedCanvas.SizeChanged -= OnCanvasSizeChanged;
            _hookedCanvas = _parentCanvas;
            _hookedCanvas.SizeChanged += OnCanvasSizeChanged;
        }
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight, w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;
        if (double.IsNaN(DetFaultX) || double.IsNaN(DetFaultY))
        {
            var (x, y) = InstrumentMath.DefaultDockDetFault(cw, ch);
            DetFaultX = x; DetFaultY = y;
        }
        else UpdateDisplayPosition();
    }

    private void UpdateDisplayPosition()
    {
        if (_parentCanvas == null) return;
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight, w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;
        if (double.IsNaN(DetFaultX) || double.IsNaN(DetFaultY)) return;
        var (cx, cy) = InstrumentMath.ClampWithMargin(DetFaultX, DetFaultY, w, h, cw, ch);
        Canvas.SetLeft(this, cx); Canvas.SetTop(this, cy);
    }

    #endregion

    #region - Template / Drag / Menu -

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root != null)
        {
            _root.MouseLeftButtonDown -= OnDown; _root.MouseMove -= OnMove;
            _root.MouseLeftButtonUp -= OnUp; _root.LostMouseCapture -= OnLost;
        }
        _root = GetTemplateChild("PART_Root") as FrameworkElement;
        if (_root != null)
        {
            _root.MouseLeftButtonDown += OnDown; _root.MouseMove += OnMove;
            _root.MouseLeftButtonUp += OnUp; _root.LostMouseCapture += OnLost;
        }
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null || _root == null) return;
        _pressed = true; _dragging = false;
        _startOnCanvas = e.GetPosition(_parentCanvas);
        _startX = double.IsNaN(DetFaultX) ? 0 : DetFaultX;
        _startY = double.IsNaN(DetFaultY) ? 0 : DetFaultY;
        _root.CaptureMouse();
        e.Handled = true;
    }
    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _parentCanvas == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { ResetInteraction(); return; }
        var cur = e.GetPosition(_parentCanvas);
        double mx = cur.X - _startOnCanvas.X, my = cur.Y - _startOnCanvas.Y;
        if (!_dragging && !CameraPopupHubMath.IsDrag(mx, my, DragDeadZone)) return;
        _dragging = true;
        var (cx, cy) = InstrumentMath.ClampWithMargin(_startX + mx, _startY + my,
            ActualWidth, ActualHeight, _parentCanvas.ActualWidth, _parentCanvas.ActualHeight);
        DetFaultX = cx; DetFaultY = cy;
    }
    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        bool wasDragging = _dragging;
        ResetInteraction();
        _root?.ReleaseMouseCapture();
        if (wasDragging)
        {
            if (DragCompletedCommand?.CanExecute(null) == true) DragCompletedCommand.Execute(null);
        }
        else
        {
            if (ClickCommand?.CanExecute(null) == true) ClickCommand.Execute(null);
        }
        e.Handled = true;
    }
    private void OnLost(object sender, MouseEventArgs e) => ResetInteraction();

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pressed) return;
        _menu ??= BuildMenu();
        SyncMenuChecks();
        _menu.PlacementTarget = this; _menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        void Add(string header, string tag, Action apply)
        {
            var mi = new MenuItem { Header = header, Tag = tag };
            mi.Click += (_, __) => { apply(); RaiseSettings(); SyncMenuChecks(); };
            menu.Items.Add(mi);
        }
        Add("세로 배치", "orient:Vertical", () => Orientation = InstrumentOrientation.Vertical);
        Add("가로 배치", "orient:Horizontal", () => Orientation = InstrumentOrientation.Horizontal);
        menu.Items.Add(new Separator());
        Add("0건 숨김", "hideZero", () => HideOnZero = !HideOnZero);
        return menu;
    }
    private void SyncMenuChecks()
    {
        if (_menu == null) return;
        foreach (var it in _menu.Items)
        {
            if (it is not MenuItem mi || mi.Tag is not string tag) continue;
            mi.IsChecked = tag switch
            {
                "orient:Vertical" => Orientation == InstrumentOrientation.Vertical,
                "orient:Horizontal" => Orientation == InstrumentOrientation.Horizontal,
                "hideZero" => HideOnZero,
                _ => false,
            };
        }
    }
    private void RaiseSettings()
    {
        if (SettingsChangedCommand?.CanExecute(null) == true) SettingsChangedCommand.Execute(null);
    }

    private Canvas? FindParentCanvas()
    {
        DependencyObject? p = this; Canvas? first = null;
        while (p != null)
        {
            p = VisualTreeHelper.GetParent(p);
            if (p is Canvas c) { first ??= c; if (c.ActualWidth > 0 || c.ActualHeight > 0) return c; }
        }
        return first;
    }

    #endregion
}
