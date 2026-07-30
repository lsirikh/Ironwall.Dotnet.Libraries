using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 방위각 심볼(나침반) CustomControl (GMap_Compass_Control PRD v1.0)
                  — 지도 회전과 싱크 회전(−θ 로즈) + 드래그 이동 + 외곽 링 회전 조작 +
                  우클릭 설정 메뉴 + 더블클릭 정북. 디자인 정본=와이어프레임 v2.2.
   Layering     : PropertyPanelCanvas 자식 ZIndex 6 "계기층"(D-03) — 팝업/패널/허브 아래.
   Created On   : 2026-07-30 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 맵 회전 상태를 항상 보여주는 <b>드래그 이동 가능·위치 기억</b> 계기 컨트롤.
/// <para>
/// - 회전 싱크: <see cref="TargetMap"/>의 ViewportSnapshot 구독(FR-02, replay+코얼레싱 계약) →
///   PART_Rose를 −Bearing 회전. 구독 수명=Loaded/Unloaded 대칭(재열림 사망 함정 방지).
/// - 표시 게이트: <see cref="RotationFeature.IsEnabled"/> OFF=Collapsed(FR-03, 비회전 회귀 0).
/// - 본체(내부 원) 드래그=이동(허브 8px 데드존·클램프 패턴), 외곽 링(r≥80%) 드래그=지도 회전(FR-06,
///   앵커 A모드 잠금 시 무시), 더블클릭=정북(FR-05), 우클릭=설정 메뉴(FR-07, e.Handled).
/// - 위치/설정 영속은 호스트(MapViewModel) 몫 — <see cref="DragCompletedCommand"/>/<see cref="SettingsChangedCommand"/>.
/// </para>
/// </summary>
public class GMapCompassControl : Control
{
    private const double DragDeadZone = 8.0;               // 허브 패턴 답습
    private const double DialLogicalSize = 120.0;          // 템플릿 논리 좌표(Viewbox 스케일)
    private const double DialLogicalRadius = 54.0;         // 베젤 반지름(논리)

    private FrameworkElement? _dial;
    private FrameworkElement? _rose;
    private RotateTransform? _roseRotate;
    private Canvas? _tickHost;
    private Canvas? _parentCanvas;
    private ContextMenu? _menu;

    private bool _pressed;
    private bool _dragging;
    private bool _ringMode;
    private bool _northPending;          // 더블클릭 정북은 Up 시점(드래그 미승격일 때만) 실행 — 리뷰 #3
    private Point _startOnCanvas;
    private double _startX, _startY;
    private Canvas? _hookedCanvas;       // 부모 캔버스 SizeChanged 구독 대상(리사이즈 재클램프) — 리뷰 #4

    static GMapCompassControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(typeof(GMapCompassControl)));
    }

    public GMapCompassControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Focusable = false;
    }

    #region - Dependency Properties -

    /// <summary>싱크 대상 맵(ElementName 바인딩). 스냅샷 구독·SetMapRotation 호출 대상.</summary>
    public GMapCustomControl? TargetMap { get => (GMapCustomControl?)GetValue(TargetMapProperty); set => SetValue(TargetMapProperty, value); }
    public static readonly DependencyProperty TargetMapProperty =
        DependencyProperty.Register(nameof(TargetMap), typeof(GMapCustomControl), typeof(GMapCompassControl),
            new PropertyMetadata(null, OnTargetMapChanged));

    /// <summary>현재 지도 회전각(canonical, 스냅샷 수신값). 로즈는 −Bearing 회전.</summary>
    public double Bearing { get => (double)GetValue(BearingProperty); private set => SetValue(BearingPropertyKey, value); }
    private static readonly DependencyPropertyKey BearingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(Bearing), typeof(double), typeof(GMapCompassControl),
            new PropertyMetadata(0d, OnBearingChanged));
    public static readonly DependencyProperty BearingProperty = BearingPropertyKey.DependencyProperty;

    /// <summary>리드아웃 문자열(+035.0° — canonical, FR-10). Bearing 파생 읽기전용.</summary>
    public string ReadoutText { get => (string)GetValue(ReadoutTextProperty); private set => SetValue(ReadoutTextPropertyKey, value); }
    private static readonly DependencyPropertyKey ReadoutTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ReadoutText), typeof(string), typeof(GMapCompassControl),
            new PropertyMetadata("+000.0°"));
    public static readonly DependencyProperty ReadoutTextProperty = ReadoutTextPropertyKey.DependencyProperty;

    /// <summary>회전 활성 상태(|θ|&gt;0.1°) — 베젤/필 Primary 강조 트리거용 읽기전용.</summary>
    public bool IsBearingActive { get => (bool)GetValue(IsBearingActiveProperty); private set => SetValue(IsBearingActivePropertyKey, value); }
    private static readonly DependencyPropertyKey IsBearingActivePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsBearingActive), typeof(bool), typeof(GMapCompassControl),
            new PropertyMetadata(false));
    public static readonly DependencyProperty IsBearingActiveProperty = IsBearingActivePropertyKey.DependencyProperty;

    /// <summary>Canvas 화면 X(TwoWay — VM 영속과 동기). NaN=기본 우상단 도킹(D-04).
    /// ⚠영속 원본(desired) — 화면 클램프는 표시(Canvas.Left/Top)에만 적용하고 이 값은 훼손하지 않는다
    /// (리뷰 #4: 작은 창 부팅 시 저장 좌표 소급 파괴 방지). 표시 갱신=UpdateDisplayPosition.</summary>
    public double CompassX { get => (double)GetValue(CompassXProperty); set => SetValue(CompassXProperty, value); }
    public static readonly DependencyProperty CompassXProperty =
        DependencyProperty.Register(nameof(CompassX), typeof(double), typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCompassPosChanged));

    public double CompassY { get => (double)GetValue(CompassYProperty); set => SetValue(CompassYProperty, value); }
    public static readonly DependencyProperty CompassYProperty =
        DependencyProperty.Register(nameof(CompassY), typeof(double), typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCompassPosChanged));

    private static void OnCompassPosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapCompassControl)d).UpdateDisplayPosition();

    /// <summary>다이얼 크기(S64/M96/L128, D-04 기본 M). 우클릭 메뉴로 변경.</summary>
    public CompassDialSize DialSize { get => (CompassDialSize)GetValue(DialSizeProperty); set => SetValue(DialSizeProperty, value); }
    public static readonly DependencyProperty DialSizeProperty =
        DependencyProperty.Register(nameof(DialSize), typeof(CompassDialSize), typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(CompassDialSize.M, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDialSizeChanged));

    /// <summary>다이얼 실제 픽셀(DialSize 파생) — 템플릿 Viewbox 바인딩용 읽기전용.</summary>
    public double DialPixels { get => (double)GetValue(DialPixelsProperty); private set => SetValue(DialPixelsPropertyKey, value); }
    private static readonly DependencyPropertyKey DialPixelsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DialPixels), typeof(double), typeof(GMapCompassControl),
            new PropertyMetadata(96d));
    public static readonly DependencyProperty DialPixelsProperty = DialPixelsPropertyKey.DependencyProperty;

    /// <summary>스타일 변형(Rose/Ring, D-01). 우클릭 메뉴로 변경 — 템플릿 트리거가 요소 전환.</summary>
    public CompassVariant Variant { get => (CompassVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(nameof(Variant), typeof(CompassVariant), typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(CompassVariant.Rose, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>외곽 링 드래그 회전 허용(D-06, 기본 true). 앵커 잠금은 런타임 별도 판정.</summary>
    public bool IsRingRotateEnabled { get => (bool)GetValue(IsRingRotateEnabledProperty); set => SetValue(IsRingRotateEnabledProperty, value); }
    public static readonly DependencyProperty IsRingRotateEnabledProperty =
        DependencyProperty.Register(nameof(IsRingRotateEnabled), typeof(bool), typeof(GMapCompassControl), new PropertyMetadata(true));

    /// <summary>각도 리드아웃 필 표시(우클릭 메뉴 토글).</summary>
    public bool IsReadoutVisible { get => (bool)GetValue(IsReadoutVisibleProperty); set => SetValue(IsReadoutVisibleProperty, value); }
    public static readonly DependencyProperty IsReadoutVisibleProperty =
        DependencyProperty.Register(nameof(IsReadoutVisible), typeof(bool), typeof(GMapCompassControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>드래그 종료 → 호스트가 CompassX/Y 영속(FR-08).</summary>
    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }
    public static readonly DependencyProperty DragCompletedCommandProperty =
        DependencyProperty.Register(nameof(DragCompletedCommand), typeof(ICommand), typeof(GMapCompassControl), new PropertyMetadata(null));

    /// <summary>우클릭 메뉴 설정 변경 → 호스트가 Size/Variant/ShowReadout 영속(FR-08).</summary>
    public ICommand? SettingsChangedCommand { get => (ICommand?)GetValue(SettingsChangedCommandProperty); set => SetValue(SettingsChangedCommandProperty, value); }
    public static readonly DependencyProperty SettingsChangedCommandProperty =
        DependencyProperty.Register(nameof(SettingsChangedCommand), typeof(ICommand), typeof(GMapCompassControl), new PropertyMetadata(null));

    private static void OnBearingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapCompassControl)d;
        double canonical = RotationMath.NormalizeDeg((double)e.NewValue);
        c.ReadoutText = CompassMath.FormatReadout(canonical);
        c.IsBearingActive = Math.Abs(canonical) > 0.1;
        if (c._roseRotate != null) c._roseRotate.Angle = -canonical;
    }

    private static void OnDialSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapCompassControl)d;
        c.DialPixels = CompassMath.SizeToPixels((CompassDialSize)e.NewValue);
        // 크기 변경 후에도 화면 밖으로 나가지 않게 다음 레이아웃에서 재클램프(SizeChanged 훅이 처리).
    }

    private static void OnTargetMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapCompassControl)d;
        if (!c.IsLoaded) return;   // Loaded에서 일괄 구독 — 미로드 시점 구독 방지
        if (e.OldValue is GMapCustomControl oldMap) oldMap.UnsubscribeViewport(c.OnViewportSnapshot);
        if (e.NewValue is GMapCustomControl newMap) newMap.SubscribeViewport(c.OnViewportSnapshot);
    }

    #endregion

    #region - Lifecycle (구독 대칭 — FR-02/03) -

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _parentCanvas = FindParentCanvas();
        TargetMap?.SubscribeViewport(OnViewportSnapshot);          // replay로 현재각 즉시 수신
        RotationFeature.EnabledChanged += OnRotationFeatureChanged;
        ApplyFeatureVisibility(RotationFeature.IsEnabled);
        SizeChanged -= OnControlSizeChanged;
        SizeChanged += OnControlSizeChanged;
        TryPosition();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        TargetMap?.UnsubscribeViewport(OnViewportSnapshot);
        RotationFeature.EnabledChanged -= OnRotationFeatureChanged;
        SizeChanged -= OnControlSizeChanged;
        if (_hookedCanvas != null) { _hookedCanvas.SizeChanged -= OnCanvasSizeChanged; _hookedCanvas = null; }
        ResetInteraction();
    }

    /// <summary>드래그/링/정북 대기 상태 전면 리셋 — 캡처 유실(Alt+Tab·메뉴 팝업·Collapsed 전환·RDP)
    /// 시 hover가 이동/회전으로 오동작하는 상태머신 잔존 차단(리뷰 #1, CameraStreamPopupControl 선례).</summary>
    private void ResetInteraction()
    {
        _pressed = false;
        _dragging = false;
        _ringMode = false;
        _northPending = false;
    }

    private void OnViewportSnapshot(MapViewportSnapshot snap) => Bearing = snap.CanonicalBearing;

    private void OnRotationFeatureChanged(bool enabled)
    {
        // 정적 이벤트는 임의 스레드 가능성 방어 — UI 스레드로 정렬(현재는 UI에서만 발화).
        if (Dispatcher.CheckAccess()) ApplyFeatureVisibility(enabled);
        else Dispatcher.InvokeAsync(() => ApplyFeatureVisibility(enabled));
    }

    private void ApplyFeatureVisibility(bool enabled)
        => Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

    private void OnControlSizeChanged(object? sender, SizeChangedEventArgs e) => TryPosition();

    /// <summary>저장값 없음(NaN)=우상단 도킹(D-04) 커밋, 있으면 표시만 갱신(영속값 보존 — 리뷰 #4).
    /// 허브 TryPosition 패턴 — 미측정(0×0)이면 no-op 후 다음 SizeChanged 재시도.</summary>
    private void TryPosition()
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null) return;
        if (!ReferenceEquals(_hookedCanvas, _parentCanvas))
        {
            // 캔버스 리사이즈(창 최대화/복원) 시 표시 재클램프 — 영속 좌표는 그대로(리뷰 #4).
            if (_hookedCanvas != null) _hookedCanvas.SizeChanged -= OnCanvasSizeChanged;
            _hookedCanvas = _parentCanvas;
            _hookedCanvas.SizeChanged += OnCanvasSizeChanged;
        }

        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight;
        double w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;

        if (double.IsNaN(CompassX) || double.IsNaN(CompassY))
        {
            var (x, y) = CompassMath.DefaultDockTopRight(cw, w);
            CompassX = x; CompassY = y;   // 최초 도킹만 desired로 커밋(이후 사용자 드래그가 갱신)
        }
        else
        {
            UpdateDisplayPosition();
        }
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateDisplayPosition();

    /// <summary>표시 위치 갱신 — desired(CompassX/Y)를 현재 캔버스 경계로 클램프해 Canvas.Left/Top에만
    /// 반영(영속값 미훼손). 창이 작아 화면 밖인 좌표도 표시상 안으로 들어오고, 창이 다시 커지면
    /// 원래 자리로 복귀(리뷰 #4·#7 동시 해소).</summary>
    private void UpdateDisplayPosition()
    {
        if (_parentCanvas == null) return;
        double cw = _parentCanvas.ActualWidth, ch = _parentCanvas.ActualHeight;
        double w = ActualWidth, h = ActualHeight;
        if (cw <= 0 || ch <= 0 || w <= 0 || h <= 0) return;
        if (double.IsNaN(CompassX) || double.IsNaN(CompassY)) return;

        var (cx, cy) = CameraPopupHubMath.ClampToBounds(CompassX, CompassY, w, h, cw, ch);
        Canvas.SetLeft(this, cx);
        Canvas.SetTop(this, cy);
    }

    #endregion

    #region - Template / Ticks -

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_dial != null)
        {
            _dial.MouseLeftButtonDown -= OnDialDown;
            _dial.MouseMove -= OnDialMove;
            _dial.MouseLeftButtonUp -= OnDialUp;
            _dial.LostMouseCapture -= OnDialLostCapture;
        }

        _dial = GetTemplateChild("PART_Dial") as FrameworkElement;
        _rose = GetTemplateChild("PART_Rose") as FrameworkElement;
        _tickHost = GetTemplateChild("PART_TickHost") as Canvas;

        if (_rose != null)
        {
            _roseRotate = new RotateTransform(-RotationMath.NormalizeDeg(Bearing));
            _rose.RenderTransformOrigin = new Point(0.5, 0.5);
            _rose.RenderTransform = _roseRotate;
        }

        if (_dial != null)
        {
            _dial.MouseLeftButtonDown += OnDialDown;
            _dial.MouseMove += OnDialMove;
            _dial.MouseLeftButtonUp += OnDialUp;
            // 캡처 비자발 상실(Alt+Tab·팝업·Collapsed·RDP) 시 상태머신 리셋 — 리뷰 #1,
            // CameraStreamPopupControl FR-DRAG-05 선례 답습.
            _dial.LostMouseCapture += OnDialLostCapture;
        }

        BuildTicks();
        ReadoutText = CompassMath.FormatReadout(Bearing);
    }

    /// <summary>로즈 눈금+기수문자 코드 생성(변형 A 전용 — B는 템플릿 트리거로 TickHost 숨김).
    /// 색상은 SetResourceReference = DynamicResource 등가 → 라이트/다크 라이브 스왑(FR-09).</summary>
    private void BuildTicks()
    {
        if (_tickHost == null) return;
        _tickHost.Children.Clear();
        const double cx = DialLogicalSize / 2.0, cy = DialLogicalSize / 2.0;

        for (int a = 0; a < 360; a += 10)
        {
            bool major = a % 30 == 0;
            if (a % 90 == 0) continue;                     // 기수 방위는 문자로
            double r1 = 50, r2 = major ? 43 : 46;
            double rad = (a - 90) * Math.PI / 180.0;
            var line = new Line
            {
                X1 = cx + Math.Cos(rad) * r1, Y1 = cy + Math.Sin(rad) * r1,
                X2 = cx + Math.Cos(rad) * r2, Y2 = cy + Math.Sin(rad) * r2,
                StrokeThickness = major ? 1.5 : 1.0,
            };
            line.SetResourceReference(Shape.StrokeProperty, major ? "PrimaryBrush" : "DividerBrush");
            _tickHost.Children.Add(line);
        }

        (string Text, int Angle, string BrushKey, double FontSize, FontWeight Weight)[] cardinals =
        {
            ("N", 0, "StatusCriticalBrush", 13, FontWeights.Bold),
            ("E", 90, "TextMutedBrush", 10, FontWeights.SemiBold),
            ("S", 180, "TextMutedBrush", 10, FontWeights.SemiBold),
            ("W", 270, "TextMutedBrush", 10, FontWeights.SemiBold),
        };
        foreach (var (text, angle, brushKey, fontSize, weight) in cardinals)
        {
            double rad = (angle - 90) * Math.PI / 180.0;
            const double r = 36;
            var tb = new TextBlock
            {
                Text = text, FontSize = fontSize, FontWeight = weight,
                Width = 20, TextAlignment = TextAlignment.Center,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            Canvas.SetLeft(tb, cx + Math.Cos(rad) * r - 10);
            Canvas.SetTop(tb, cy + Math.Sin(rad) * r - fontSize * 0.66);
            _tickHost.Children.Add(tb);
        }
    }

    #endregion

    #region - Drag(이동) / Ring(회전) / 정북 / 메뉴 -

    private void OnDialDown(object sender, MouseButtonEventArgs e)
    {
        _parentCanvas ??= FindParentCanvas();
        if (_parentCanvas == null || _dial == null) return;

        var local = e.GetPosition(_dial);
        double half = _dial.ActualWidth / 2.0;
        double dx = local.X - half, dy = local.Y - half;
        double dialRadius = half * (DialLogicalRadius / (DialLogicalSize / 2.0));   // 논리 54/60 비율

        bool ringHit = CompassMath.IsRingHit(dx, dy, dialRadius);
        bool ringAllowed = IsRingRotateEnabled && RotationFeature.IsEnabled
                           && TargetMap != null && !TargetMap.IsRotationLockedByAnchor;
        _ringMode = ringHit && ringAllowed;

        _pressed = true;
        _dragging = false;
        _startOnCanvas = e.GetPosition(_parentCanvas);
        _startX = double.IsNaN(CompassX) ? 0 : CompassX;
        _startY = double.IsNaN(CompassY) ? 0 : CompassY;
        _dial.CaptureMouse();
        e.Handled = true;   // 맵 팬/셀렉트 충돌 차단(FR-04)
    }

    private void OnDialMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _parentCanvas == null || _dial == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            // 캡처 유실 후 잔존 상태로 hover가 이동/회전하는 오동작 이중 방어(리뷰 #1).
            ResetInteraction();
            return;
        }

        var cur = e.GetPosition(_parentCanvas);
        double mx = cur.X - _startOnCanvas.X, my = cur.Y - _startOnCanvas.Y;
        // 링/이동 공통 8px 데드존 — 링 위 클릭+1px 지터가 절대각 점프로 번지는 오발 차단(리뷰 #2).
        if (!_dragging && !CameraPopupHubMath.IsDrag(mx, my, DragDeadZone)) return;
        _dragging = true;

        if (_ringMode)
        {
            var local = e.GetPosition(_dial);
            double half = _dial.ActualWidth / 2.0;
            double bearing = CompassMath.RingBearingFromPointer(local.X - half, local.Y - half);
            TargetMap?.SetMapRotation(bearing);   // SSOT 게이트(Decide)가 최종 판정 — 컨트롤은 요청만(FR-06)
            return;
        }

        var (cx, cy) = CameraPopupHubMath.ClampToBounds(
            _startX + mx, _startY + my, ActualWidth, ActualHeight,
            _parentCanvas.ActualWidth, _parentCanvas.ActualHeight);
        CompassX = cx; CompassY = cy;
    }

    private void OnDialUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        bool wasDragging = _dragging, wasRing = _ringMode, northPending = _northPending;
        ResetInteraction();
        _dial?.ReleaseMouseCapture();

        if (wasDragging && !wasRing)
        {
            // 이동 종료 → 위치 영속(FR-08). 링 회전각 영속은 기존 회전 debounce가 담당.
            if (DragCompletedCommand?.CanExecute(null) == true) DragCompletedCommand.Execute(null);
        }
        else if (northPending && !wasDragging && !wasRing)
        {
            // 더블클릭 정북은 '드래그로 승격되지 않은' Up에서만 실행 — 두 번째 다운 즉시 발동 시
            // 이동 드래그에 무음 회전 리셋이 편승하는 문제 차단(리뷰 #3).
            TargetMap?.SetMapRotation(0);   // 0은 게이트 무관 항상 허용 = 안전 경로(FR-05)
        }
        e.Handled = true;
    }

    private void OnDialLostCapture(object sender, MouseEventArgs e) => ResetInteraction();

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.ChangedButton != MouseButton.Left || _ringMode) return;
        _northPending = true;   // 실행은 OnDialUp에서(리뷰 #3)
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        // FR-07: 맵/심볼 우클릭으로 버블링 차단 + 자체 설정 메뉴.
        e.Handled = true;
        // 좌드래그 진행 중 우클릭 → 메뉴 팝업이 마우스 캡처를 강탈해 Up 미도달(상태 잔존) — 무시(리뷰 #1a).
        if (_pressed) return;
        ShowSettingsMenu();
    }

    private void ShowSettingsMenu()
    {
        _menu ??= BuildSettingsMenu();
        SyncMenuChecks();
        _menu.PlacementTarget = this;
        _menu.IsOpen = true;
    }

    private ContextMenu BuildSettingsMenu()
    {
        var menu = new ContextMenu();

        void AddRadio(string header, string group, System.Action apply)
        {
            var mi = new MenuItem { Header = header, Tag = group };
            mi.Click += (_, __) => { apply(); RaiseSettingsChanged(); SyncMenuChecks(); };
            menu.Items.Add(mi);
        }

        AddRadio("크기 — 소 (64px)", "size:S", () => DialSize = CompassDialSize.S);
        AddRadio("크기 — 중 (96px)", "size:M", () => DialSize = CompassDialSize.M);
        AddRadio("크기 — 대 (128px)", "size:L", () => DialSize = CompassDialSize.L);
        menu.Items.Add(new Separator());
        AddRadio("스타일 — 로즈", "var:Rose", () => Variant = CompassVariant.Rose);
        AddRadio("스타일 — 미니멀 링", "var:Ring", () => Variant = CompassVariant.Ring);
        menu.Items.Add(new Separator());
        AddRadio("각도 표시", "readout", () => IsReadoutVisible = !IsReadoutVisible);
        menu.Items.Add(new Separator());

        var north = new MenuItem { Header = "정북 복귀" };
        north.Click += (_, __) => TargetMap?.SetMapRotation(0);
        menu.Items.Add(north);

        return menu;
    }

    private void SyncMenuChecks()
    {
        if (_menu == null) return;
        foreach (var item in _menu.Items)
        {
            if (item is not MenuItem mi || mi.Tag is not string tag) continue;
            mi.IsChecked = tag switch
            {
                "size:S" => DialSize == CompassDialSize.S,
                "size:M" => DialSize == CompassDialSize.M,
                "size:L" => DialSize == CompassDialSize.L,
                "var:Rose" => Variant == CompassVariant.Rose,
                "var:Ring" => Variant == CompassVariant.Ring,
                "readout" => IsReadoutVisible,
                _ => false,
            };
        }
    }

    private void RaiseSettingsChanged()
    {
        if (SettingsChangedCommand?.CanExecute(null) == true) SettingsChangedCommand.Execute(null);
    }

    /// <summary>가장 가까운 크기 있는 부모 Canvas — 허브 FindParentCanvas 패턴 답습.</summary>
    private Canvas? FindParentCanvas()
    {
        DependencyObject? p = this;
        Canvas? first = null;
        while (p != null)
        {
            p = VisualTreeHelper.GetParent(p);
            if (p is Canvas c)
            {
                first ??= c;
                if (c.ActualWidth > 0 || c.ActualHeight > 0) return c;
            }
        }
        return first;
    }

    #endregion
}
