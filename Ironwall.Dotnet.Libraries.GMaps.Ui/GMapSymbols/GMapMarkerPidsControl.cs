using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : PIDS 마커 전용 컨트롤
   Created By   : GHLee                                                
   Created On   : 8/26/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// PIDS 마커 전용 컨트롤
/// - GMapMarkerBaseControl<GMapPidsMarker>를 상속받아 PIDS 특화 기능 제공
/// - FOV 시각화, 장비 타입별 UI 차별화
/// </summary>
public class GMapMarkerPidsControl : GMapMarkerBaseControl<GMapPidsMarker>
{
    #region Static Constructor
    static GMapMarkerPidsControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerPidsControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerPidsControl)));
    }
    #endregion

    #region Private Fields

    /// <summary>
    /// 부모 GMapCustomControl 참조 (이벤트 구독 해제용)
    /// </summary>
    private GMapCustomControl? _mapControl;

    /// <summary>
    /// 각도 기반 애니메이션용 현재 반지름 값 (픽셀)
    /// </summary>
    private double _animatedRadius;

    /// <summary>
    /// 각도 기반 애니메이션용 현재 방향각 (도)
    /// </summary>
    private double _animatedBearing;

    /// <summary>
    /// 각도 기반 애니메이션용 현재 시야각 (도)
    /// </summary>
    private double _animatedAngle;

    /// <summary>
    /// 애니메이션 진행 중 여부
    /// </summary>
    private bool _isAnimating;

    #endregion

    #region Additional Dependency Properties

    /// <summary>
    /// 장비 타입
    /// </summary>
    public EnumDeviceType DeviceType
    {
        get { return (EnumDeviceType)GetValue(DeviceTypeProperty); }
        set { SetValue(DeviceTypeProperty, value); }
    }

    public static readonly DependencyProperty DeviceTypeProperty =
        DependencyProperty.Register("DeviceType", typeof(EnumDeviceType), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(EnumDeviceType.Fence, OnDeviceTypeChanged));

    /// <summary>
    /// 이벤트 상태 (애니메이션 트리거)
    /// </summary>
    public EnumEventStatus EventStatus
    {
        get { return (EnumEventStatus)GetValue(EventStatusProperty); }
        set { SetValue(EventStatusProperty, value); }
    }

    public static readonly DependencyProperty EventStatusProperty =
        DependencyProperty.Register("EventStatus", typeof(EnumEventStatus), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(EnumEventStatus.Normal, OnEventStatusChanged));

    /// <summary>개폐 형태 축(FR-12) — 통문·함체. 템플릿 트리거가 PART_Housing3D.DoorOpen 을 0.4s 애니메이션한다. 색 축(EventStatus)과 독립.</summary>
    public EnumDoorState DoorState
    {
        get { return (EnumDoorState)GetValue(DoorStateProperty); }
        set { SetValue(DoorStateProperty, value); }
    }

    public static readonly DependencyProperty DoorStateProperty =
        DependencyProperty.Register(nameof(DoorState), typeof(EnumDoorState), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(EnumDoorState.Unknown));

    /// <summary>
    /// FOV 표시 여부
    /// </summary>
    public bool ShowFOV
    {
        get { return (bool)GetValue(ShowFOVProperty); }
        set { SetValue(ShowFOVProperty, value); }
    }

    public static readonly DependencyProperty ShowFOVProperty =
        DependencyProperty.Register("ShowFOV", typeof(bool), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(false, OnShowFOVChanged));

    /// <summary>
    /// 감지 범위 (미터)
    /// </summary>
    public Brush FOVColor
    {
        get { return (Brush)GetValue(FOVColorProperty); }
        set { SetValue(FOVColorProperty, value); }
    }

    public static readonly DependencyProperty FOVColorProperty =
        DependencyProperty.Register("FOVColor", typeof(Brush), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(Brushes.Blue));


    /// <summary>
    /// 감지 영역 투명도
    /// </summary>
    public double FOVOpacity
    {
        get { return (double)GetValue(FOVOpacityProperty); }
        set { SetValue(FOVOpacityProperty, value); }
    }
    
    public static readonly DependencyProperty FOVOpacityProperty =
        DependencyProperty.Register("FOVOpacity", typeof(double), typeof(GMapMarkerPidsControl), new PropertyMetadata(0.0, OnFOVOpacityChanged));

   



    /// <summary>
    /// 감지 범위 (미터)
    /// </summary>
    public double DetectionRange
    {
        get { return (double)GetValue(DetectionRangeProperty); }
        set { SetValue(DetectionRangeProperty, value); }
    }

    public static readonly DependencyProperty DetectionRangeProperty =
        DependencyProperty.Register("DetectionRange", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(30.0, OnFOVParameterChanged));

    /// <summary>
    /// 감지 각도 (도)
    /// </summary>
    public double DetectionAngle
    {
        get { return (double)GetValue(DetectionAngleProperty); }
        set { SetValue(DetectionAngleProperty, value); }
    }

    public static readonly DependencyProperty DetectionAngleProperty =
        DependencyProperty.Register("DetectionAngle", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(120.0, OnFOVParameterChanged));

    /// <summary>
    /// 감지 방향 (도, 북쪽 기준)
    /// </summary>
    public double DetectionBearing
    {
        get { return (double)GetValue(DetectionBearingProperty); }
        set { SetValue(DetectionBearingProperty, value); }
    }

    public static readonly DependencyProperty DetectionBearingProperty =
        DependencyProperty.Register("DetectionBearing", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(0.0, OnFOVParameterChanged));

    /// <summary>
    /// 방송(음원/TTS) 동작 중 여부 — Opacity 펄스 애니메이션 트리거
    /// </summary>
    public bool IsBroadcasting
    {
        get => (bool)GetValue(IsBroadcastingProperty);
        set => SetValue(IsBroadcastingProperty, value);
    }

    public static readonly DependencyProperty IsBroadcastingProperty =
        DependencyProperty.Register("IsBroadcasting", typeof(bool), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(false));

    // ── 부품 건강 · 문 표시 · 툴팁(장비 부품 설정을 지도 아이콘에 표현 — 1차 조각) ──
    //   값 DP 몇 개만 둔다(부품마다 요소를 만들지 않는다 — 수백 개 아이콘). 그리는 쪽은 ComponentStatusOverlay 하나.

    /// <summary>마커의 부품 요약(런타임). 바뀌면 파생 DP(<see cref="ComponentHealth"/> · <see cref="ComponentBadgeCount"/> · <see cref="DoorIndicator"/> · <see cref="SymbolToolTip"/>)를 다시 계산한다.</summary>
    public ComponentHealthSummary ComponentSummary
    {
        get => (ComponentHealthSummary)GetValue(ComponentSummaryProperty);
        set => SetValue(ComponentSummaryProperty, value);
    }

    public static readonly DependencyProperty ComponentSummaryProperty =
        DependencyProperty.Register(nameof(ComponentSummary), typeof(ComponentHealthSummary), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(ComponentHealthSummary.None));

    public ComponentHealthLevel ComponentHealth
    {
        get => (ComponentHealthLevel)GetValue(ComponentHealthProperty);
        private set => SetValue(ComponentHealthPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComponentHealthPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComponentHealth), typeof(ComponentHealthLevel), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(ComponentHealthLevel.None));
    public static readonly DependencyProperty ComponentHealthProperty = ComponentHealthPropertyKey.DependencyProperty;

    public int ComponentBadgeCount
    {
        get => (int)GetValue(ComponentBadgeCountProperty);
        private set => SetValue(ComponentBadgeCountPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComponentBadgeCountPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComponentBadgeCount), typeof(int), typeof(GMapMarkerPidsControl), new PropertyMetadata(0));
    public static readonly DependencyProperty ComponentBadgeCountProperty = ComponentBadgeCountPropertyKey.DependencyProperty;

    /// <summary>좌하단 문 표시 모양 — 통문 · 함체만. 3D 는 문짝이 위치를 그리므로 구동 중만.</summary>
    public DoorIndicatorKind DoorIndicator
    {
        get => (DoorIndicatorKind)GetValue(DoorIndicatorProperty);
        private set => SetValue(DoorIndicatorPropertyKey, value);
    }

    private static readonly DependencyPropertyKey DoorIndicatorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DoorIndicator), typeof(DoorIndicatorKind), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(DoorIndicatorKind.None));
    public static readonly DependencyProperty DoorIndicatorProperty = DoorIndicatorPropertyKey.DependencyProperty;

    /// <summary>아이콘 툴팁 — 제목 + (문) + (부품 절). 부품 축도 문도 없으면 제목만(종전과 같다).</summary>
    public string? SymbolToolTip
    {
        get => (string?)GetValue(SymbolToolTipProperty);
        private set => SetValue(SymbolToolTipPropertyKey, value);
    }

    private static readonly DependencyPropertyKey SymbolToolTipPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SymbolToolTip), typeof(string), typeof(GMapMarkerPidsControl), new PropertyMetadata(null));
    public static readonly DependencyProperty SymbolToolTipProperty = SymbolToolTipPropertyKey.DependencyProperty;

    /// <summary>아이콘 아래 부품 칸 줄(L2) — 부품 요약에서 파생. 6.3 · 미수신이면 빈 줄.</summary>
    public ComponentStrip ComponentStrip
    {
        get => (ComponentStrip)GetValue(ComponentStripProperty);
        private set => SetValue(ComponentStripPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComponentStripPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComponentStrip), typeof(ComponentStrip), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(ComponentStrip.Empty));
    public static readonly DependencyProperty ComponentStripProperty = ComponentStripPropertyKey.DependencyProperty;

    /// <summary>조립 카드가 이 아이콘에 열려 있다(런타임 — 마커 <c>IsComponentCardOpen</c> 을 따른다).</summary>
    public bool IsComponentCardTarget
    {
        get => (bool)GetValue(IsComponentCardTargetProperty);
        set => SetValue(IsComponentCardTargetProperty, value);
    }

    public static readonly DependencyProperty IsComponentCardTargetProperty =
        DependencyProperty.Register(nameof(IsComponentCardTarget), typeof(bool), typeof(GMapMarkerPidsControl), new PropertyMetadata(false));

    /// <summary>선택 · 호버 · 조립 카드 대상 — 밀집 중에도 칸 줄을 그린다(FR-04).</summary>
    public bool IsComponentEmphasized
    {
        get => (bool)GetValue(IsComponentEmphasizedProperty);
        private set => SetValue(IsComponentEmphasizedPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsComponentEmphasizedPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsComponentEmphasized), typeof(bool), typeof(GMapMarkerPidsControl), new PropertyMetadata(false));
    public static readonly DependencyProperty IsComponentEmphasizedProperty = IsComponentEmphasizedPropertyKey.DependencyProperty;

    /// <summary>심볼의 화면 크기(px) = 심볼 크기 × 디지털 배율 — 지도 밀도 판정과 같은 함수(<see cref="ComponentBadgeLod.MarkerScreenPixels"/>).</summary>
    public double ComponentScreenPixels
    {
        get => (double)GetValue(ComponentScreenPixelsProperty);
        private set => SetValue(ComponentScreenPixelsPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComponentScreenPixelsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComponentScreenPixels), typeof(double), typeof(GMapMarkerPidsControl), new PropertyMetadata(double.NaN));
    public static readonly DependencyProperty ComponentScreenPixelsProperty = ComponentScreenPixelsPropertyKey.DependencyProperty;

    /// <summary>제목 라벨 상자(아이콘 중심 기준) — 부품 칸 줄이 라벨을 피해 아래로 간다. 라벨이 없으면 <see cref="Rect.Empty"/>.</summary>
    public Rect ComponentLabelBox
    {
        get => (Rect)GetValue(ComponentLabelBoxProperty);
        private set => SetValue(ComponentLabelBoxPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComponentLabelBoxPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComponentLabelBox), typeof(Rect), typeof(GMapMarkerPidsControl), new PropertyMetadata(Rect.Empty));
    public static readonly DependencyProperty ComponentLabelBoxProperty = ComponentLabelBoxPropertyKey.DependencyProperty;

    /// <summary>라벨 상자에 영향을 주는 심볼 속성(LabelAdorner 의 글자 · 오프셋 규칙과 같다).</summary>
    private static readonly HashSet<string> LabelProps = new(StringComparer.Ordinal)
    {
        "Title", "TitleSize", "ShowTitle", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth",
        "LabelOffsetX", "LabelOffsetY", "Width", "Height", "IsLayerEnabled",
    };

    private void OnMarkerPropertyChangedForComponents(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || LabelProps.Contains(e.PropertyName)) RefreshComponentGeometry();
    }

    /// <summary>화면 크기 · 라벨 상자 파생값을 다시 계산한다. 호출 스레드: UI(아니면 넘긴다).</summary>
    private void RefreshComponentGeometry()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefreshComponentGeometry)); return; }
        var marker = Marker;
        double w = marker?.Width is > 0 ? marker.Width : ActualWidth;
        double h = marker?.Height is > 0 ? marker.Height : ActualHeight;
        ComponentScreenPixels = ComponentBadgeLod.MarkerScreenPixels(w, h, MarkerScreenScale);
        ComponentLabelBox = marker is null ? Rect.Empty
            : Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners.LabelAdorner.DefaultPointLabelBox(marker, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    /// <summary>화면 배율(디지털 줌) — 부품 층 LOD 가 "화면에서 몇 px 인가"를 재는 데 쓴다.</summary>
    public double MarkerScreenScale
    {
        get => (double)GetValue(MarkerScreenScaleProperty);
        set => SetValue(MarkerScreenScaleProperty, value);
    }

    public static readonly DependencyProperty MarkerScreenScaleProperty =
        DependencyProperty.Register(nameof(MarkerScreenScale), typeof(double), typeof(GMapMarkerPidsControl), new PropertyMetadata(1.0));

    /// <summary>
    /// (A9) 소프트웨어 렌더(Tier 0 — 원격 데스크톱 등)에서 탐지 펄스 링을 3개 → 1개로 줄인다. 우상단 이벤트 점 · 색은 그대로다.
    /// 탐지 마커 1개가 무한 애니메이션 9개(링 3 × 투명도 · 가로 · 세로)를 돌리므로, 수백 개면 원격 dirty-region 전송이 폭증한다.
    /// </summary>
    public bool ReducedPulse
    {
        get => (bool)GetValue(ReducedPulseProperty);
        set => SetValue(ReducedPulseProperty, value);
    }

    public static readonly DependencyProperty ReducedPulseProperty =
        DependencyProperty.Register(nameof(ReducedPulse), typeof(bool), typeof(GMapMarkerPidsControl), new PropertyMetadata(false));

    private void OnRenderTierChanged(int tier)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => OnRenderTierChanged(tier))); return; }
        ReducedPulse = RenderTierProbe.IsSoftwareTier(tier);
    }

    /// <summary>문짝이 열림/닫힘을 직접 그리는 템플릿인가(3D 하우징) — 그러면 문 표시는 구동 중만 낸다.</summary>
    protected virtual bool DoorLeavesShowPosition => false;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ComponentSummaryProperty || e.Property == DoorStateProperty || e.Property == DeviceTypeProperty
            || e.Property == MarkerTitleProperty)
            RefreshComponentPresentation();
        else if (e.Property == IsMouseOverProperty || e.Property == IsSelectedProperty || e.Property == IsComponentCardTargetProperty)
            IsComponentEmphasized = IsMouseOver || IsSelected || IsComponentCardTarget;
        else if (e.Property == MarkerScreenScaleProperty || e.Property == WidthProperty || e.Property == HeightProperty)
            RefreshComponentGeometry();
    }

    /// <summary>부품 요약 · 문 형태 · 제목 → 배지 · 문 표시 · 툴팁 파생값.</summary>
    private void RefreshComponentPresentation()
    {
        var summary = ComponentSummary ?? ComponentHealthSummary.None;
        ComponentHealth = summary.Health;
        ComponentBadgeCount = summary.BadgeCount;
        ComponentStrip = summary.Strip;
        DoorIndicator = DoorIndicatorRules.Resolve(
            Ironwall.Dotnet.Monitoring.Models.Helpers.DoorStateMachine.HasDoor(DeviceType), DoorState, summary.DoorMotion, DoorLeavesShowPosition);
        SymbolToolTip = SymbolStatusText.ToolTip(MarkerTitle, DoorIndicator, summary);
    }

    /// <summary>
    /// UIA peer(A6) — 지도 심볼이 <c>GMaps.Symbol.{종류}.{장비Id}</c> 로 트리에 나오고, 이름에 이벤트 · 장비 · 문 · 부품 상태가 실린다.
    /// peer 는 UIA 클라이언트가 물을 때만 만들어진다(수백 개 아이콘에 상시 비용 없음).
    /// </summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PidsMarkerAutomationPeer(this);

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerPidsControl()
    {
    }

    /// <summary>
    /// PIDS 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="pidsMarker">연결할 PIDS 마커</param>
    public GMapMarkerPidsControl(GMapPidsMarker pidsMarker) : base(pidsMarker)
    {
        this.Loaded += GMapMarkerPidsControl_Loaded;
        this.Unloaded += GMapMarkerPidsControl_Unloaded;
    }

    private void GMapMarkerPidsControl_Loaded(object sender, RoutedEventArgs e)
    {
        try { ReducedPulse = RenderTierProbe.IsSoftware; } catch (InvalidOperationException) { /* 렌더 능력 조회 불가 — 기본(링 3개) 유지 */ }
        RenderTierProbe.TierChanged -= OnRenderTierChanged;
        RenderTierProbe.TierChanged += OnRenderTierChanged;   // Unloaded 에서 해제

        _mapControl = FindParentMapControl();
        if (_mapControl != null)
        {
            _mapControl.OnMapZoomChanged += OnMapZoomChanged;
            // 회전 통지(R-13) — 회전만 변경 시에도 FOV 재계산 트리거. Unloaded서 해제
            (_mapControl as GMapCustomControl)?.SubscribeViewport(OnViewportSnapshot);
        }

        // 초기추가 보강: 생성자 단계(UpdateFromSpecificMarker/OnApplyTemplate)의 UpdateFOVPath는
        // 맵 미부착(_mapControl==null → early-return)으로 스킵된다. 맵 부착이 완료된 이 시점에
        // ShowFOV면 1회 재트리거해, 줌 조작 없이도 신규 카메라 FOV 부채꼴이 즉시 그려지도록 보장.
        if (ShowFOV)
            UpdateFOVPath();
    }

    private void GMapMarkerPidsControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        RenderTierProbe.TierChanged -= OnRenderTierChanged;   // 정적 이벤트 — 안 풀면 마커가 영구히 붙잡힌다
        if (_mapControl != null)
        {
            _mapControl.OnMapZoomChanged -= OnMapZoomChanged;
            (_mapControl as GMapCustomControl)?.UnsubscribeViewport(OnViewportSnapshot);
            _mapControl = null;
        }
    }

    /// <summary>직전에 처리한 뷰포트 snapshot — TiltCos 단독 변화 필터(map-tilt FR-06/D2)의 비교 기준.</summary>
    private GMapCustoms.MapViewportSnapshot? _lastViewportSnapshot;

    /// <summary>뷰포트(회전) snapshot 수신 — 줌 핸들러와 동일 경로로 FOV 재계산(R-13).
    /// [map-tilt FR-06/D2] TiltCos 만 바뀐 스냅샷(중심·베어링·줌·크기·디지털배율 동일)에는 반응하지 않는다 — FOV 는 컨트롤 RenderTransform 을
    /// 승계하므로 재계산 불필요. 슬라이더 드래그 중 카메라 N개 × 프레임 재계산 방지.</summary>
    private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot snapshot)
    {
        bool tiltOnly = snapshot.IsTiltCosOnlyChangeFrom(_lastViewportSnapshot);
        _lastViewportSnapshot = snapshot;
        if (!MarkerScreenScale.Equals(snapshot.DigitalZoomScale)) MarkerScreenScale = snapshot.DigitalZoomScale;   // 부품 층 LOD
        if (tiltOnly) return;
        OnMapZoomChanged();
    }

    /// <summary>[Rotation FR-12/R-22] 회전 pivot을 FOV apex(카메라 하우징 중심, SVG y=43.6/200)에 고정 —
    /// 중심(0.5,0.5) 회전 시 apex가 지리 앵커에서 이탈하던 드리프트 방지. IpCamera 외에는 중심 유지.
    /// (우상단 EventStatus 배지는 원형이라 회전해도 시각 동일 — upright 보정 불요.)</summary>
    protected override Point RotationPivot
        => DeviceType == EnumDeviceType.IpCamera ? new Point(0.5, 43.6 / 200.0) : base.RotationPivot;

    /// <summary>[map-tilt-25d FR-10 결정③] PIDS 2D 아이콘은 빌보드 — 맵 회전 시 글리프는 정립하고 FOV 만 지면(월드각)을 추종한다.
    /// 3D 하우징(<see cref="GMapMarker3DHousingControl"/>)은 override false(yaw 로 회전을 표현).</summary>
    public override bool IsBillboard => true;

    /// <summary>빌보드: 루트 각은 항상 0(3D 선례 <c>GMapMarker3DHousingControl.ApplyDisplayAngle</c> 동형), FOV 는
    /// <see cref="GetFovBearing"/> 이 <c>CurrentDisplayAngle</c> 을 1회 가산해 월드각 D−θ 를 유지하므로 즉시 재계산한다.</summary>
    protected override void ApplyDisplayAngle(double angle)
    {
        base.ApplyDisplayAngle(IsBillboard ? 0 : angle);
        if (IsBillboard) UpdateFOVPath();
    }

    private void OnMapZoomChanged()
    {
        if (ShowFOV && DeviceType == EnumDeviceType.IpCamera)
        {
            //System.Diagnostics.Debug.WriteLine($"[FOV] OnMapZoomChanged 호출됨 - Zoom: {_mapControl?.Zoom}");
            UpdateFOVPath();
        }
    }

    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// PIDS 마커 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // PIDS 마커 전용 속성 동기화
        DeviceType = Marker.DeviceType;
        EventStatus = Marker.EventStatus;

        ShowFOV = Marker.ShowFOV;
        FOVOpacity = Marker.FOVOpacity;
        FOVColor = ColorHelper.ToBrush(Marker.FOVColor);
        DetectionRange = Marker.Model.DetectionRange;
        DetectionAngle = Marker.Model.DetectionAngle;
        DetectionBearing = Marker.Model.DetectionBearing;

        // FOV 업데이트
        if (ShowFOV)
        {
            UpdateFOVPath(); // 추가
        }

        // 장비 타입별 기본 설정
        ApplyDeviceTypeDefaults();
    }

    /// <summary>
    /// PIDS 마커 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // PIDS 마커 전용 바인딩
        SetupPropertyBinding(DeviceTypeProperty, nameof(Marker.DeviceType));
        SetupPropertyBinding(EventStatusProperty, nameof(Marker.EventStatus));
        SetupPropertyBinding(DoorStateProperty, nameof(Marker.DoorState), BindingMode.OneWay);   // FR-12 형태 축
        SetupPropertyBinding(ShowFOVProperty, nameof(Marker.ShowFOV));
        SetupPropertyBinding(FOVOpacityProperty, nameof(Marker.FOVOpacity));
        SetupPropertyBinding(DetectionRangeProperty, nameof(Marker.DetectionRange));
        SetupPropertyBinding(DetectionAngleProperty, nameof(Marker.DetectionAngle));
        SetupPropertyBinding(DetectionBearingProperty, nameof(Marker.DetectionBearing));
        SetupPropertyBinding(IsBroadcastingProperty, nameof(Marker.IsBroadcasting));
        SetupPropertyBinding(ComponentSummaryProperty, nameof(Marker.ComponentSummary), BindingMode.OneWay);   // 런타임 부품 요약
        SetupPropertyBinding(IsComponentCardTargetProperty, nameof(Marker.IsComponentCardOpen), BindingMode.OneWay);   // 조립 카드 대상(칸 줄 강조)
        // 지도가 센 밀집 값(FR-04) — 심볼 → 이 컨트롤(상속 속성) → 템플릿 안 부품 층
        SetupPropertyBinding(ComponentStatusOverlay.IsStripCrowdedProperty, nameof(Marker.ComponentStripCrowded), BindingMode.OneWay);
        Marker.PropertyChanged -= OnMarkerPropertyChangedForComponents;
        Marker.PropertyChanged += OnMarkerPropertyChangedForComponents;   // 라벨 상자 · 크기(심볼과 수명이 같다)
        RefreshComponentGeometry();

        var colorConverter = new ColorTypeToBrushConverter();
        var visibilityConverter = new System.Windows.Controls.BooleanToVisibilityConverter();

        var fovColorBinding = new Binding(nameof(Marker.FOVColor))
        {
            Source = Marker,
            Mode = BindingMode.OneWay,
            Converter = colorConverter
        };
        SetBinding(FOVColorProperty, fovColorBinding);
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();
        ApplyDeviceTypeDefaults();
        //System.Diagnostics.Debug.WriteLine("GMapMarkerPidsControl 초기화 완료");
    }

    /// <summary>
    /// 마커 모양 업데이트 (장비 타입 고려)
    /// </summary>
    protected override void UpdateMarkerAppearance()
    {
        //// 이벤트 상태에 따른 색상 설정 (기본 상태 색상 무시)
        //MarkerFill = GetEventStatusBrush(EventStatus);

        //// 장비 타입별 테두리 색상
        //MarkerStroke = GetDeviceTypeBorderBrush(DeviceType);

        UpdateFOVPath();
    }

    /// <summary>
    /// 단일 클릭 처리 (PIDS 전용 로직)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);
        OnPidsMarkerClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 (FOV 토글 등)
    /// </summary>
    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);
    }

    // OnMouseRightButtonDown은 GMapMarkerBaseControl에서 공통 처리

    /// <summary>
    /// 클릭으로 선택과 비선택에 따른 이벤트 콜백
    /// </summary>
    /// <param name="isSelected"></param>
    protected override void OnSelectionChanged(bool isSelected)
    {
        base.OnSelectionChanged(isSelected);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        UpdateFOVPath();
    }

    /// <summary>
    /// 컨트롤 템플릿 적용후
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 템플릿 요소들이 로드된 후 FOV 업데이트
        UpdateFOVPath();
    }
    #endregion

    #region PIDS Specific Methods
    /// <summary>
    /// 장비 타입별 기본 설정 적용
    /// </summary>
    private void ApplyDeviceTypeDefaults()
    {
        if (!WritesBackRenderSize) return;
        var size = GetSizeForDeviceType(DeviceType);
        Width = Height = size;
    }

    /// <summary>
    /// DeviceType별 기본 크기 반환 (단위 테스트용 internal static)
    /// </summary>
    internal static double GetSizeForDeviceType(EnumDeviceType deviceType)
    {
        return deviceType switch
        {
            EnumDeviceType.IpCamera           => 40,
            EnumDeviceType.PIR                => 35,
            EnumDeviceType.Fence              => 30,
            EnumDeviceType.IpSpeaker          => 36,
            EnumDeviceType.Multi              => 32,
            EnumDeviceType.SmartMultisensor2  => 32,
            _                                 => 32,
        };
    }

    /// <summary>
    /// PIDS 마커 클릭 처리
    /// </summary>
    protected virtual void OnPidsMarkerClicked(MouseButtonEventArgs e)
    {
        // 장비 타입별 특별한 처리
        switch (DeviceType)
        {
            case EnumDeviceType.IpCamera:
                // 카메라 클릭은 선택만 — FOV 켜기/끄기는 속성창 체크박스(SSOT)가 담당한다.
                // (사용자 정책 2026-07-19: 클릭이 FOV를 바꾸지 않음. 토글값이 DB에 영구 저장·동기화됨.)
                break;

            case EnumDeviceType.PIR:
                // PIR 센서 클릭 시 감지 범위 표시
                // TODO: 감지 범위 시각화
                break;
        }
    }

    /// <summary>
    /// FOV Path 업데이트 (수정된 버전 - Phase 15.7)
    /// - 각도 기반 애니메이션 지원 (PointAnimation 대신 DoubleAnimation 사용)
    /// - 좌표 계산 로직 내장 (WPF 좌표계 호환)
    /// - animate=false: 즉시 업데이트, animate=true: 부드러운 전환
    /// </summary>
    protected void UpdateFOVPath(bool animate = false)
    {
        animate &= AnimateFovChanges;
        // UI 스레드 접근 보장
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateFOVPath(animate), DispatcherPriority.Background);
            return;
        }

        try
        {
            // 템플릿 파트 가져오기
            if (GetTemplateChild("PART_FOVFigure") is PathFigure figure &&
                GetTemplateChild("PART_FOVLine1") is LineSegment line1 &&
                GetTemplateChild("PART_FOVArc") is ArcSegment arc &&
                GetTemplateChild("PART_FOVCenter") is Ellipse centerEllipse &&
                GetTemplateChild("PART_FOVCanvas") is Canvas fovCanvas &&
                GetTemplateChild("PART_FOVTransform") is TranslateTransform transform)
            {
                // 1. 캔버스 및 중심점 설정
                // IpCamera: 하우징 중심(SVG y=43.6/200) 을 FOV 꼭지점으로 사용
                var origin = GetFovOrigin();
                transform.X = origin.X;
                transform.Y = origin.Y;

                fovCanvas.Width = ActualWidth;
                fovCanvas.Height = ActualHeight;

                figure.StartPoint = new Point(0, 0);

                // 2. 거리 계산 (미터 -> 픽셀)
                var mapControl = _mapControl ?? FindParentMapControl();
                if (mapControl == null || Marker?.Position == null) return;

                double targetRadius = ConvertMetersToPixels(DetectionRange, Marker.Position, mapControl);
                if (targetRadius < 1) targetRadius = 1;

                // [Rotation FR-13 정확-1회] 루트가 DisplayAngle(=Bearing−θ)로 돌므로 FOV 지오메트리는
                // θ를 읽지 않고 (Detection−Bearing)만 사용 → 월드각=(Bearing−θ)+(Detection−Bearing)=Detection−θ.
                // 이 식에 θ가 없다는 것이 이중 −θ(F-07/R-36) 방지의 증명. 종전 R-22(Bearing≠0 시 FOV 오프셋)도 해소.
                double targetBearing = GetFovBearing();
                double targetAngle = DetectionAngle;

                // 3. 애니메이션 적용 여부 결정
                if (animate && !_isAnimating && _animatedRadius > 0)
                {
                    // 각도 기반 애니메이션 시작
                    StartAngleBasedAnimation(
                        line1, arc, centerEllipse, transform,
                        targetRadius, targetBearing, targetAngle);
                }
                else
                {
                    // 즉시 업데이트 (애니메이션 없음 또는 초기화)
                    _animatedRadius = targetRadius;
                    _animatedBearing = targetBearing;
                    _animatedAngle = targetAngle;

                    ApplyFOVValues(line1, arc, centerEllipse, transform,
                        targetRadius, targetBearing, targetAngle);
                }

                //System.Diagnostics.Debug.WriteLine($"[FOV] Zoom:{mapControl.Zoom}, Range:{DetectionRange}m, Radius:{targetRadius:F1}px, Bearing:{targetBearing}, Angle:{targetAngle}, Animate:{animate}");
            }
        }
        catch (Exception ex)
        {
            //System.Diagnostics.Debug.WriteLine($"FOV Path 업데이트 실패: {ex.Message}");
        }
    }

    protected virtual Point GetFovOrigin() => new(ActualWidth * 0.5,
        ActualHeight * (DeviceType == EnumDeviceType.IpCamera ? 43.6 / 200.0 : 0.5));

    /// <summary>FOV 컨트롤-로컬 방위(FR-13 정확-1회). 비빌보드(루트=b−θ): (D−b) 만 → 월드각 D−θ.
    /// 빌보드(루트=0, FR-11): (D−b)+CurrentDisplayAngle(b−θ) → 월드각 D−θ (3D <c>GetFovBearing</c> 과 동형). θ 는 어느 경로에서도 정확히 1회.</summary>
    protected virtual double GetFovBearing()
        => Utils.RotationMath.FovControlSpaceBearing(DetectionBearing, RotationAngle) + (IsBillboard ? CurrentDisplayAngle : 0d);
    protected virtual bool AnimateFovChanges => true;

    /// <summary>
    /// 각도 기반 애니메이션 시작 (Phase 15.7)
    /// - DoubleAnimation으로 반지름/방향/각도 값을 애니메이션
    /// - CompositionTarget.Rendering으로 매 프레임마다 좌표 재계산
    /// </summary>
    private void StartAngleBasedAnimation(
        LineSegment line1, ArcSegment arc, Ellipse centerEllipse, TranslateTransform transform,
        double targetRadius, double targetBearing, double targetAngle)
    {
        _isAnimating = true;

        var duration = TimeSpan.FromMilliseconds(200);
        var startRadius = _animatedRadius;
        var startBearing = _animatedBearing;
        var startAngle = _animatedAngle;

        var startTime = DateTime.Now;

        // CompositionTarget.Rendering 이벤트 핸들러
        void OnRendering(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var progress = Math.Min(1.0, elapsed / duration.TotalMilliseconds);

            // EaseOut 함수 적용 (부드러운 감속)
            var easedProgress = 1 - Math.Pow(1 - progress, 3);

            // 현재 값 보간
            _animatedRadius = startRadius + (targetRadius - startRadius) * easedProgress;
            _animatedBearing = startBearing + (targetBearing - startBearing) * easedProgress;
            _animatedAngle = startAngle + (targetAngle - startAngle) * easedProgress;

            // 좌표 재계산 및 적용
            ApplyFOVValues(line1, arc, centerEllipse, transform,
                _animatedRadius, _animatedBearing, _animatedAngle);

            // 애니메이션 완료 체크
            if (progress >= 1.0)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isAnimating = false;

                // 최종 값으로 정확히 설정
                _animatedRadius = targetRadius;
                _animatedBearing = targetBearing;
                _animatedAngle = targetAngle;
                ApplyFOVValues(line1, arc, centerEllipse, transform,
                    targetRadius, targetBearing, targetAngle);
            }
        }

        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// FOV 값을 Path 요소에 적용 (좌표 계산 및 설정)
    /// </summary>
    private void ApplyFOVValues(
        LineSegment line1, ArcSegment arc, Ellipse centerEllipse, TranslateTransform transform,
        double radius, double bearing, double angle)
    {
        // 각도 계산 (Map Bearing -> WPF Coordinates)
        // Map: 0도=북쪽(North), 시계방향(CW) 증가
        // WPF: 0도=동쪽(East), 시계방향(CW) 증가
        // 변환 공식: WPF_Angle = Map_Angle - 90
        double halfAngle = angle / 2.0;
        double startAngleDeg = bearing - halfAngle - 90;
        double endAngleDeg = bearing + halfAngle - 90;

        // 좌표 계산 (삼각함수)
        double startRad = startAngleDeg * Math.PI / 180.0;
        double endRad = endAngleDeg * Math.PI / 180.0;

        double startX = radius * Math.Cos(startRad);
        double startY = radius * Math.Sin(startRad);
        double endX = radius * Math.Cos(endRad);
        double endY = radius * Math.Sin(endRad);

        // Path 요소 업데이트
        line1.Point = new Point(startX, startY);
        arc.Point = new Point(endX, endY);
        arc.Size = new Size(radius, radius);
        arc.IsLargeArc = angle > 180.0;
        arc.SweepDirection = SweepDirection.Clockwise;

        // 중심점 마커 위치
        Canvas.SetLeft(centerEllipse, transform.X - 2);
        Canvas.SetTop(centerEllipse, transform.Y - 2);
    }

    /// <summary>
    /// 미터 단위 거리를 현재 줌 레벨에서의 픽셀로 변환
    /// </summary>
    private double ConvertMetersToPixels(double meters, PointLatLng position, GMapCustomControl mapControl)
    {
        try
        {
            // 현재 위치에서 동쪽으로 meters만큼 떨어진 지점 계산
            var earthRadius = 6371000; // 지구 반지름 (미터)
            var lat1Rad = position.Lat * Math.PI / 180.0;
            var deltaLon = meters / (earthRadius * Math.Cos(lat1Rad)) * 180.0 / Math.PI;

            var targetPoint = new PointLatLng(position.Lat, position.Lng + deltaLon);

            // 두 지점을 화면 좌표로 변환
            var centerPixel = mapControl.FromLatLngToLocal(position);
            var targetPixel = mapControl.FromLatLngToLocal(targetPoint);

            // 픽셀 거리 계산
            var pixelDistance = Math.Sqrt(
                Math.Pow(targetPixel.X - centerPixel.X, 2) +
                Math.Pow(targetPixel.Y - centerPixel.Y, 2));

            return pixelDistance;
        }
        catch (Exception ex)
        {
            //System.Diagnostics.Debug.WriteLine($"거리 변환 실패: {ex.Message}");
            // 폴백: 간단한 스케일 사용
            return meters * 0.1; // 대략적인 스케일
        }
    }
   
    #endregion

    #region Static Property Changed Callbacks

    /// <summary>
    /// 장비 타입 변경 시 호출
    /// </summary>
    protected static void OnDeviceTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            control.ApplyDeviceTypeDefaults();
            control.UpdateMarkerAppearance();

            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.DeviceType != (EnumDeviceType)e.NewValue)
            {
                control.Marker.DeviceType = (EnumDeviceType)e.NewValue;
            }
        }
    }

    /// <summary>
    /// 이벤트 상태 변경 시 호출 (애니메이션 트리거 핵심)
    /// </summary>
    protected static void OnEventStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            // UI 색상 즉시 업데이트
            control.UpdateMarkerAppearance();

            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.EventStatus != (EnumEventStatus)e.NewValue)
            {
                control.Marker.EventStatus = (EnumEventStatus)e.NewValue;
            }

            //System.Diagnostics.Debug.WriteLine($"EventStatus 변경: {e.OldValue} → {e.NewValue}");
        }
    }

    /// <summary>
    /// FOV 표시 여부 변경 시 호출
    /// </summary>
    protected static void OnShowFOVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            bool isVisible = (bool)e.NewValue;
            
            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.ShowFOV != isVisible)
            {
                control.Marker.ShowFOV = isVisible;
            }

            if (isVisible)
            {
                control.UpdateFOVPath(); // 추가
            }

        }
    }

    /// <summary>
    /// Opacity 변경 시 
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    protected static void OnFOVOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            //System.Diagnostics.Debug.WriteLine($"OnFOVOpacityChanged: {e.OldValue} → {e.NewValue}");
            control.Marker.FOVOpacity = (double)e.NewValue;
        }
    }

    /// <summary>
    /// FOV 매개변수 변경 시 호출
    /// </summary>
    protected static void OnFOVParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null && control.ShowFOV)
        {
            // 사용자 상호작용에 의한 변경은 부드러운 애니메이션 적용
            control.UpdateFOVPath(animate: true);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// FOV 매개변수 업데이트
    /// </summary>
    public void UpdateFOVParameters(double range, double angle, double bearing)
    {
        DetectionRange = range;
        DetectionAngle = angle;
        DetectionBearing = bearing;

        if (ShowFOV)
        {
            UpdateFOVPath(); // 추가
        }
    }

    /// <summary>
    /// 장비 상태 초기화
    /// </summary>
    public void ResetDeviceState()
    {
        EventStatus = EnumEventStatus.Normal;
        ShowFOV = false;
        ApplyDeviceTypeDefaults();
    }

    #endregion

}
