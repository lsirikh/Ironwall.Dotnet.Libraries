using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 강풍모드(WINDY) 인디케이터 CustomControl (GMap_Map_Instruments FR-01~04b)
                  — 현재 WINDY 모드(wind0~3)를 아이콘/색/라벨로 표시 + 드래그 이동 + 우클릭 설정
                  + 클릭 시 WINDY 패널 오픈. 계기층 z6(D-06). 나침반 드래그 패턴·리뷰 8건 교훈 답습.
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
****************************************************************************/
public class GMapWindyIndicatorControl : Control
{
    private const double DragDeadZone = 8.0;

    private FrameworkElement? _root;
    private Canvas? _parentCanvas;
    private Canvas? _hookedCanvas;
    private ContextMenu? _menu;
    private bool _pressed, _dragging;
    private Point _startOnCanvas;
    private double _startX, _startY;

    static GMapWindyIndicatorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapWindyIndicatorControl),
            new FrameworkPropertyMetadata(typeof(GMapWindyIndicatorControl)));
    }

    public GMapWindyIndicatorControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region - Dependency Properties -

    /// <summary>현재 WINDY 모드(wind0~3). 템플릿 DataTrigger가 아이콘/색 전환.</summary>
    public EnumWindyMode Mode { get => (EnumWindyMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(EnumWindyMode), typeof(GMapWindyIndicatorControl),
            new PropertyMetadata(EnumWindyMode.wind0, OnModeChanged));

    /// <summary>모드명(파생, 읽기전용) — 라벨 바인딩.</summary>
    public string ModeName { get => (string)GetValue(ModeNameProperty); private set => SetValue(ModeNamePropertyKey, value); }
    private static readonly DependencyPropertyKey ModeNamePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ModeName), typeof(string), typeof(GMapWindyIndicatorControl), new PropertyMetadata("보통 바람"));
    public static readonly DependencyProperty ModeNameProperty = ModeNamePropertyKey.DependencyProperty;

    /// <summary>모드 서브텍스트(파생, 읽기전용).</summary>
    public string ModeDesc { get => (string)GetValue(ModeDescProperty); private set => SetValue(ModeDescPropertyKey, value); }
    private static readonly DependencyPropertyKey ModeDescPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ModeDesc), typeof(string), typeof(GMapWindyIndicatorControl), new PropertyMetadata("wind0 · 표준 감도"));
    public static readonly DependencyProperty ModeDescProperty = ModeDescPropertyKey.DependencyProperty;

    /// <summary>표시 형태(아이콘+라벨 / 아이콘만).</summary>
    public WindyDisplayStyle DisplayStyle { get => (WindyDisplayStyle)GetValue(DisplayStyleProperty); set => SetValue(DisplayStyleProperty, value); }
    public static readonly DependencyProperty DisplayStyleProperty =
        DependencyProperty.Register(nameof(DisplayStyle), typeof(WindyDisplayStyle), typeof(GMapWindyIndicatorControl),
            new FrameworkPropertyMetadata(WindyDisplayStyle.IconLabel, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>평상(wind0) 숨김(FR-04).</summary>
    public bool HideOnNormal { get => (bool)GetValue(HideOnNormalProperty); set => SetValue(HideOnNormalProperty, value); }
    public static readonly DependencyProperty HideOnNormalProperty =
        DependencyProperty.Register(nameof(HideOnNormal), typeof(bool), typeof(GMapWindyIndicatorControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisibilityInputChanged));

    /// <summary>보기(View) 메뉴 마스터 가시성(FR-15). 최종 표시 = 이 값 AND !(HideOnNormal &amp;&amp; wind0).</summary>
    public bool IsInstrumentVisible { get => (bool)GetValue(IsInstrumentVisibleProperty); set => SetValue(IsInstrumentVisibleProperty, value); }
    public static readonly DependencyProperty IsInstrumentVisibleProperty =
        DependencyProperty.Register(nameof(IsInstrumentVisible), typeof(bool), typeof(GMapWindyIndicatorControl),
            new PropertyMetadata(true, OnVisibilityInputChanged));

    /// <summary>영속 원본 X(desired, TwoWay). 표시 클램프는 Canvas.SetLeft로 분리(리뷰 #4).</summary>
    public double WindyX { get => (double)GetValue(WindyXProperty); set => SetValue(WindyXProperty, value); }
    public static readonly DependencyProperty WindyXProperty =
        DependencyProperty.Register(nameof(WindyX), typeof(double), typeof(GMapWindyIndicatorControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));

    public double WindyY { get => (double)GetValue(WindyYProperty); set => SetValue(WindyYProperty, value); }
    public static readonly DependencyProperty WindyYProperty =
        DependencyProperty.Register(nameof(WindyY), typeof(double), typeof(GMapWindyIndicatorControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPosChanged));

    /// <summary>드래그 종료 → 호스트가 위치 영속.</summary>
    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }
    public static readonly DependencyProperty DragCompletedCommandProperty =
        DependencyProperty.Register(nameof(DragCompletedCommand), typeof(ICommand), typeof(GMapWindyIndicatorControl), new PropertyMetadata(null));

    /// <summary>우클릭 메뉴 설정 변경 → 호스트가 영속.</summary>
    public ICommand? SettingsChangedCommand { get => (ICommand?)GetValue(SettingsChangedCommandProperty); set => SetValue(SettingsChangedCommandProperty, value); }
    public static readonly DependencyProperty SettingsChangedCommandProperty =
        DependencyProperty.Register(nameof(SettingsChangedCommand), typeof(ICommand), typeof(GMapWindyIndicatorControl), new PropertyMetadata(null));

    /// <summary>클릭(드래그 미승격) → WINDY 패널 오픈(D-10). 호스트가 OpenWindyPanelMessageModel publish.</summary>
    public ICommand? ClickCommand { get => (ICommand?)GetValue(ClickCommandProperty); set => SetValue(ClickCommandProperty, value); }
    public static readonly DependencyProperty ClickCommandProperty =
        DependencyProperty.Register(nameof(ClickCommand), typeof(ICommand), typeof(GMapWindyIndicatorControl), new PropertyMetadata(null));

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapWindyIndicatorControl)d;
        var m = (EnumWindyMode)e.NewValue;
        c.ModeName = InstrumentMath.WindyModeName(m);
        c.ModeDesc = InstrumentMath.WindyModeDesc(m);
        c.ApplyVisibility();
    }
    private static void OnVisibilityInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapWindyIndicatorControl)d).ApplyVisibility();
    private static void OnPosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapWindyIndicatorControl)d).UpdateDisplayPosition();

    #endregion

    #region - Lifecycle -

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _parentCanvas = FindParentCanvas();
        SizeChanged -= OnControlSizeChanged; SizeChanged += OnControlSizeChanged;
        ApplyVisibility();
        TryPosition();
    }
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnControlSizeChanged;
        if (_hookedCanvas != null) { _hookedCanvas.SizeChanged -= OnCanvasSizeChanged; _hookedCanvas = null; }
        ResetInteraction();
    }

    /// <summary>최종 가시성 = 마스터(보기 메뉴) AND !(HideOnNormal &amp;&amp; wind0). FR-04/17.</summary>
    private void ApplyVisibility()
    {
        bool hiddenByNormal = HideOnNormal && InstrumentMath.IsNormalWind(Mode);
        Visibility = (IsInstrumentVisible && !hiddenByNormal) ? Visibility.Visible : Visibility.Collapsed;
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
        if (double.IsNaN(WindyX) || double.IsNaN(WindyY))
        {
            var (x, y) = InstrumentMath.DefaultDockWindy(cw, ch);
            WindyX = x; WindyY = y;
        }
        else UpdateDisplayPosition();
    }

    /// <summary>desired(WindyX/Y)를 클램프해 Canvas.Left/Top에만 반영(영속값 미훼손, 리뷰 #4).</summary>
    private void UpdateDisplayPosition()
    {
        if (_parentCanvas == null) return;
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight, w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;
        if (double.IsNaN(WindyX) || double.IsNaN(WindyY)) return;
        var (cx, cy) = InstrumentMath.ClampWithMargin(WindyX, WindyY, w, h, cw, ch);
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
        ModeName = InstrumentMath.WindyModeName(Mode);
        ModeDesc = InstrumentMath.WindyModeDesc(Mode);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null || _root == null) return;
        _pressed = true; _dragging = false;
        _startOnCanvas = e.GetPosition(_parentCanvas);
        _startX = double.IsNaN(WindyX) ? 0 : WindyX;
        _startY = double.IsNaN(WindyY) ? 0 : WindyY;
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
        WindyX = cx; WindyY = cy;
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
            // 클릭(미승격) → WINDY 패널 오픈(D-10)
            if (ClickCommand?.CanExecute(null) == true) ClickCommand.Execute(null);
        }
        e.Handled = true;
    }
    private void OnLost(object sender, MouseEventArgs e) => ResetInteraction();

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pressed) return;   // 드래그 중 우클릭 무시(리뷰 #1a)
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
        Add("아이콘 + 라벨", "style:IconLabel", () => DisplayStyle = WindyDisplayStyle.IconLabel);
        Add("아이콘만", "style:IconOnly", () => DisplayStyle = WindyDisplayStyle.IconOnly);
        menu.Items.Add(new Separator());
        Add("평상시(보통) 숨김", "hideNormal", () => HideOnNormal = !HideOnNormal);
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
                "style:IconLabel" => DisplayStyle == WindyDisplayStyle.IconLabel,
                "style:IconOnly" => DisplayStyle == WindyDisplayStyle.IconOnly,
                "hideNormal" => HideOnNormal,
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
