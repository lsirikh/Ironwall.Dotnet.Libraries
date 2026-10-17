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

    // ───────────────── Digital Zoom (0.5 래더 — zoom-float-halfstep PRD v1.1) ─────────────────
    // 슬라이더는 Zoom DP가 아니라 SliderValue(실효줌 = min(Zoom,Max) + 0.5×dzl)에 바인딩한다.
    // (Zoom DP는 OnCoerceZoom 클램프로 MaxZoom 초과를 표현 못 하므로 — CM-8)
    // 합성/라우팅/라벨 산술은 Helpers.ZoomLadder(순수 함수, ZoomLadderTests 검증) 단일 소스.
    private bool _isSyncing;

    /// <summary>슬라이더 좌측 라벨("17" / "17.5" / 최상단 "19.5+" / "19.5++"). 읽기전용 DP. (FR-01)</summary>
    public string ZoomLabel
    {
        get => (string)GetValue(ZoomLabelProperty);
        private set => SetValue(ZoomLabelKey, value);
    }
    private static readonly DependencyPropertyKey ZoomLabelKey =
        DependencyProperty.RegisterReadOnly(nameof(ZoomLabel), typeof(string), typeof(MapZoomControl),
            new PropertyMetadata("15"));   // Zoom default=15·Level default=0 일치 → 최초 렌더 stale 방지
    public static readonly DependencyProperty ZoomLabelProperty = ZoomLabelKey.DependencyProperty;

    /// <summary>디지털 줌 활성 여부(라벨 색 트리거용). 읽기전용 DP.</summary>
    public bool IsDigitalZoom
    {
        get => (bool)GetValue(IsDigitalZoomProperty);
        private set => SetValue(IsDigitalZoomKey, value);
    }
    private static readonly DependencyPropertyKey IsDigitalZoomKey =
        DependencyProperty.RegisterReadOnly(nameof(IsDigitalZoom), typeof(bool), typeof(MapZoomControl),
            new PropertyMetadata(false));
    public static readonly DependencyProperty IsDigitalZoomProperty = IsDigitalZoomKey.DependencyProperty;

    /// <summary>최상단 소프트 밴드(dzl≥2 — "19.5+"/"19.5++") 여부. 주황 라벨 트리거(FR-18, G-6=B).
    /// 하프스텝(x.5)은 정식 줌이므로 여기 포함되지 않는다.</summary>
    public bool IsSoftZoom
    {
        get => (bool)GetValue(IsSoftZoomProperty);
        private set => SetValue(IsSoftZoomKey, value);
    }
    private static readonly DependencyPropertyKey IsSoftZoomKey =
        DependencyProperty.RegisterReadOnly(nameof(IsSoftZoom), typeof(bool), typeof(MapZoomControl),
            new PropertyMetadata(false));
    public static readonly DependencyProperty IsSoftZoomProperty = IsSoftZoomKey.DependencyProperty;

    private static void UpdateZoomLabel(MapZoomControl c)
    {
        // zoom-float-halfstep FR-01: 소수 라벨(InvariantCulture — NFR-01). '+' 생성은 최상단 소프트 밴드 전용.
        c.ZoomLabel = Helpers.ZoomLadder.Label(c.Zoom, c.MaxZoom, c.DigitalZoomLevel);
        c.IsDigitalZoom = c.DigitalZoomLevel > 0;   // 레거시 의미 유지(외부 바인딩 호환)
        c.IsSoftZoom = Helpers.ZoomLadder.IsSoftBand(c.Zoom, c.MaxZoom, c.DigitalZoomLevel);
    }

    /// <summary>디지털 줌 레벨(0~Steps). MainMap.DigitalZoomLevel과 TwoWay.</summary>
    public int DigitalZoomLevel
    {
        get => (int)GetValue(DigitalZoomLevelProperty);
        set => SetValue(DigitalZoomLevelProperty, value);
    }
    public static readonly DependencyProperty DigitalZoomLevelProperty =
        DependencyProperty.Register(nameof(DigitalZoomLevel), typeof(int), typeof(MapZoomControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomStateChanged));

    /// <summary>최상단 디지털 스텝 수(TopSteps, 기본 3 = "Max.5"/"Max.5+"/"Max.5++").
    /// GMapCustomControl.DIGITAL_ZOOM_MAX(3)와 동일해야 한다(FR-04 — 구 기본값 2는 잠재 상한 충돌 SIM-D007).</summary>
    public int DigitalZoomSteps
    {
        get => (int)GetValue(DigitalZoomStepsProperty);
        set => SetValue(DigitalZoomStepsProperty, value);
    }
    public static readonly DependencyProperty DigitalZoomStepsProperty =
        DependencyProperty.Register(nameof(DigitalZoomSteps), typeof(int), typeof(MapZoomControl),
            new PropertyMetadata(3, OnExtendedMaxChanged));

    /// <summary>슬라이더 Maximum = MaxZoom + 0.5×DigitalZoomSteps (읽기 전용, FR-04). 기본 19+1.5=20.5.</summary>
    public double ExtendedMaxZoom
    {
        get => (double)GetValue(ExtendedMaxZoomProperty);
        private set => SetValue(ExtendedMaxZoomKey, value);
    }
    private static readonly DependencyPropertyKey ExtendedMaxZoomKey =
        DependencyProperty.RegisterReadOnly(nameof(ExtendedMaxZoom), typeof(double), typeof(MapZoomControl),
            new PropertyMetadata(20.5));
    public static readonly DependencyProperty ExtendedMaxZoomProperty = ExtendedMaxZoomKey.DependencyProperty;

    /// <summary>슬라이더 전용 합성 값 = 실효줌(min(Zoom,Max) + 0.5×dzl). 슬라이더는 이것에 TwoWay 바인딩.</summary>
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
        // zoom-float-halfstep FR-02: 0.5×dzl 합성 — (17,H)=17.5 vs (18,0)=18 항상 구분(SIM-D003 해소).
        try { c.SliderValue = Helpers.ZoomLadder.Compose(c.Zoom, c.MaxZoom, c.DigitalZoomLevel); }
        finally { c._isSyncing = false; }
        UpdateZoomLabel(c);   // ★ C1: 맵→슬라이더/MaxZoom 변경 경로 라벨 갱신
    }

    /// <summary>MaxZoom 또는 DigitalZoomSteps 변경 → ExtendedMaxZoom 갱신 + 합성값 재동기화.</summary>
    private static void OnExtendedMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (MapZoomControl)d;
        c.ExtendedMaxZoom = c.MaxZoom + 0.5 * c.DigitalZoomSteps;   // FR-04: 0.5 그리드 상한(기본 19+1.5=20.5)
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
            // zoom-float-halfstep FR-02: 0.5 그리드 분해(AwayFromZero — SIM-D002) —
            // 타일줌에는 정수만 대입(NFR-04, 소수 직대입은 벤더 floor 무성 절단 SIM-D001).
            var (tile, dzl) = Helpers.ZoomLadder.Route((double)e.NewValue, c.MinZoom, c.MaxZoom, c.DigitalZoomSteps);
            c.Zoom = tile;
            c.DigitalZoomLevel = dzl;
        }
        finally { c._isSyncing = false; }
        UpdateZoomLabel(c);   // ★ C1: 슬라이더 드래그 경로 라벨 갱신 (두 콜백 모두 가드로 억압되므로 여기서 직접)
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateZoomLabel(this);   // 템플릿 적용 시점 현재 Zoom/DigitalZoomLevel로 라벨 1회 동기화 (최초 렌더 stale 방어)
    }
}
