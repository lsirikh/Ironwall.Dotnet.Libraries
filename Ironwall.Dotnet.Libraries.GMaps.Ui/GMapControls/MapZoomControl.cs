using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class MapZoomControl : Control
{
    static MapZoomControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapZoomControl),
            new FrameworkPropertyMetadata(typeof(MapZoomControl)));
    }

    public double Zoom
    {
        get { return (double)GetValue(ZoomProperty); }
        set { SetValue(ZoomProperty, value); }
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register("Zoom", typeof(double),
            typeof(MapZoomControl),
            new FrameworkPropertyMetadata(15.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomStateChanged));

    public int MinZoom
    {
        get { return (int)GetValue(MinZoomProperty); }
        set { SetValue(MinZoomProperty, value); }
    }

    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register("MinZoom", typeof(int),
            typeof(MapZoomControl), new PropertyMetadata(2));

    public int MaxZoom
    {
        get { return (int)GetValue(MaxZoomProperty); }
        set { SetValue(MaxZoomProperty, value); }
    }

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register("MaxZoom", typeof(int),
            typeof(MapZoomControl), new PropertyMetadata(19, OnExtendedMaxChanged));

    public ICommand ZoomInCommand
    {
        get { return (ICommand)GetValue(ZoomInCommandProperty); }
        set { SetValue(ZoomInCommandProperty, value); }
    }

    public static readonly DependencyProperty ZoomInCommandProperty =
        DependencyProperty.Register("ZoomInCommand", typeof(ICommand),
            typeof(MapZoomControl));

    public ICommand ZoomOutCommand
    {
        get { return (ICommand)GetValue(ZoomOutCommandProperty); }
        set { SetValue(ZoomOutCommandProperty, value); }
    }

    public static readonly DependencyProperty ZoomOutCommandProperty =
        DependencyProperty.Register("ZoomOutCommand", typeof(ICommand),
            typeof(MapZoomControl));

    // ───────────────── Digital Zoom ─────────────────
    // 슬라이더는 Zoom DP가 아니라 SliderValue(=Zoom+DigitalZoomLevel)에 바인딩한다.
    // (Zoom DP는 OnCoerceZoom 클램프로 MaxZoom 초과를 표현 못 하므로 — CM-8)
    private bool _isSyncing;

    /// <summary>디지털 줌 레벨(0~Steps). MainMap.DigitalZoomLevel과 TwoWay.</summary>
    public int DigitalZoomLevel
    {
        get => (int)GetValue(DigitalZoomLevelProperty);
        set => SetValue(DigitalZoomLevelProperty, value);
    }
    public static readonly DependencyProperty DigitalZoomLevelProperty =
        DependencyProperty.Register(nameof(DigitalZoomLevel), typeof(int), typeof(MapZoomControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomStateChanged));

    /// <summary>디지털 줌 단계 수(기본 2). 슬라이더 디지털 구간 길이.</summary>
    public int DigitalZoomSteps
    {
        get => (int)GetValue(DigitalZoomStepsProperty);
        set => SetValue(DigitalZoomStepsProperty, value);
    }
    public static readonly DependencyProperty DigitalZoomStepsProperty =
        DependencyProperty.Register(nameof(DigitalZoomSteps), typeof(int), typeof(MapZoomControl),
            new PropertyMetadata(2, OnExtendedMaxChanged));

    /// <summary>슬라이더 Maximum = MaxZoom + DigitalZoomSteps (읽기 전용).</summary>
    public double ExtendedMaxZoom
    {
        get => (double)GetValue(ExtendedMaxZoomProperty);
        private set => SetValue(ExtendedMaxZoomKey, value);
    }
    private static readonly DependencyPropertyKey ExtendedMaxZoomKey =
        DependencyProperty.RegisterReadOnly(nameof(ExtendedMaxZoom), typeof(double), typeof(MapZoomControl),
            new PropertyMetadata(21.0));
    public static readonly DependencyProperty ExtendedMaxZoomProperty = ExtendedMaxZoomKey.DependencyProperty;

    /// <summary>슬라이더 전용 합성 값(= Zoom + DigitalZoomLevel). 슬라이더는 이것에 TwoWay 바인딩.</summary>
    public double SliderValue
    {
        get => (double)GetValue(SliderValueProperty);
        set => SetValue(SliderValueProperty, value);
    }
    public static readonly DependencyProperty SliderValueProperty =
        DependencyProperty.Register(nameof(SliderValue), typeof(double), typeof(MapZoomControl),
            new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSliderValueChanged));

    /// <summary>Zoom 또는 DigitalZoomLevel 변경(맵→슬라이더) → 합성값 역동기화.</summary>
    private static void OnZoomStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (MapZoomControl)d;
        if (c._isSyncing) return;
        c._isSyncing = true;
        // Zoom은 MaxZoom으로 클램프(초과분은 디지털 줌이 담당). Zoom>MaxZoom 일시 상태가 슬라이더에 누설되지 않게.
        try { c.SliderValue = System.Math.Min(c.Zoom, c.MaxZoom) + c.DigitalZoomLevel; }
        finally { c._isSyncing = false; }
    }

    /// <summary>MaxZoom 또는 DigitalZoomSteps 변경 → ExtendedMaxZoom 갱신 + 합성값 재동기화.</summary>
    private static void OnExtendedMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (MapZoomControl)d;
        c.ExtendedMaxZoom = c.MaxZoom + c.DigitalZoomSteps;
        OnZoomStateChanged(d, e);
    }

    /// <summary>슬라이더 드래그(슬라이더→맵) → Zoom/DigitalZoomLevel 라우팅. 재진입 가드로 무한루프 차단.</summary>
    private static void OnSliderValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (MapZoomControl)d;
        if (c._isSyncing) return;
        c._isSyncing = true;
        try
        {
            double v = (double)e.NewValue;
            int maxZ = c.MaxZoom;
            if (v <= maxZ)
            {
                c.Zoom = v;
                c.DigitalZoomLevel = 0;
            }
            else
            {
                c.Zoom = maxZ;
                c.DigitalZoomLevel = System.Math.Clamp((int)System.Math.Round(v - maxZ), 0, c.DigitalZoomSteps);
            }
        }
        finally { c._isSyncing = false; }
    }
}
