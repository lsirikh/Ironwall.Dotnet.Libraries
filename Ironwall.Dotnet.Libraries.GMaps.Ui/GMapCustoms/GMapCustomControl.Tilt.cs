using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

/****************************************************************************
   Purpose      : 지도 카드 틸트(2.5D) 배선 — map-tilt-25d PRD v1.1 FR-01(뷰 변환 단일 빌더) · FR-03(게이트 재평가 코얼레싱) ·
                  FR-04(φ_layout 정착·가시 사각형 헬퍼) · FR-05(형제 캔버스 좌표) · FR-09(스냅 게이트 φ) · FR-13(Tier 0 강등).
                  판정은 TiltMath.Decide(순수) + TiltGateCoalescer(프레임당 1회) 결과만 적용한다.
   Note         : ★ 불변식 1~4(DigitalZoom PRD: ScaleMode·Zoom·_core 불변, 수동 히트 보정 금지) 승계.
                  ★ 불변식 5(신설): 뷰 변환은 항상 새 ScaleTransform 객체를 재대입한다 — AdornerLayer 는 in-place ScaleY
                    변경을 추종하지 않는다(헤드리스 실증). ScaleYProperty 애니메이션 금지(G8 스냅).
                  ★ 판정 원자성(FR-03): 휠/스텝/SetEffectiveZoom 은 정수 줌 → 디지털 줌 순으로 두 번 발화하므로
                    ReevaluateTilt 는 Dispatcher Render 우선순위로 프레임당 1회만 커밋한다(QueueViewportSnapshot 동형).
                    게이트 여부는 decision.Active 로 판단하고 WasActive 로 되먹인다(State==Active 비교 금지 — Hold 도 Active 일 수 있다).
                  ★ φ_layout(TiltLayoutDeg)은 MainMap Height/Margin MultiBinding(G 그룹, TiltOverscanConverter)이 소비한다.
                    게이트 전이는 즉시, 사용자 각도 조작은 Active 일 때만 φmax 선확장 → 150 ms 정착(TiltLayoutSettler, G8).
                  ★ x:Name 무변경 · R-40 TransformGroup 무변경 · Marker.Bearing write-back 없음.
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
****************************************************************************/
public partial class GMapCustomControl
{
    #region Tilt — 상수·필드

    /// <summary>틸트 각 입력 스텝(도) — 거친 조정(Ctrl+↑/↓).</summary>
    public const double TILT_STEP_COARSE = 5;
    /// <summary>틸트 각 입력 스텝(도) — 미세 조정(Ctrl+Shift+↑/↓).</summary>
    public const double TILT_STEP_FINE = 1;

    /// <summary>스냅 격자 비활성 판정 임계(도) — R-41 회전 게이트(|θ|&gt;0.1)와 같은 값(FR-09 "|θ|&gt;0.1 ∨ φ&gt;0.1" 단일 식).</summary>
    public const double TILT_SNAP_GATE_EPSILON = 0.1;

    private readonly TiltGateCoalescer _tiltGate = new();
    private TiltLayoutSettler? _tiltSettler;
    private DispatcherTimer? _tiltSettleTimer;
    private bool _tiltReevalQueued;
    private bool _tiltTierSubscribed;
    private bool _tiltTierProbeWarned;
    private MapTiltModel _tiltSettings = new();

    #endregion

    #region Tilt — 공개 API (G 그룹 계약)

    private static readonly DependencyPropertyKey TiltDegPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(TiltDeg), typeof(double), typeof(GMapCustomControl), new PropertyMetadata(0.0));

    /// <summary>적용된 틸트 각 φ(도, 읽기 전용) — 판정(TiltMath.Decide) 결과. 0 = 탑뷰.</summary>
    public static readonly DependencyProperty TiltDegProperty = TiltDegPropertyKey.DependencyProperty;

    /// <summary>적용된 틸트 각 φ(도). RenderTransform ScaleY = s·cosφ. 배지/스냅 게이트/스냅샷 TiltCos 의 원천.</summary>
    public double TiltDeg => (double)GetValue(TiltDegProperty);

    private static readonly DependencyPropertyKey TiltLayoutDegPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(TiltLayoutDeg), typeof(double), typeof(GMapCustomControl), new PropertyMetadata(0.0));

    /// <summary>레이아웃 각 φ_layout(도, 읽기 전용) — MainMap Height/Margin MultiBinding(TiltOverscanConverter) 입력.</summary>
    public static readonly DependencyProperty TiltLayoutDegProperty = TiltLayoutDegPropertyKey.DependencyProperty;

    /// <summary>레이아웃 각 φ_layout(도). 각도 조작 중엔 φmax 로 선확장, 마지막 변경 150 ms 뒤 φ 로 정착(G8). 게이트 전이는 즉시.</summary>
    public double TiltLayoutDeg => (double)GetValue(TiltLayoutDegProperty);

    /// <summary>사용자 요청 각(도, 0~35 하드 클램프). 게이트 통과 시 φ = clamp(값, 0, TiltSettings.MaxAngleDeg). 슬라이더 TwoWay 바인딩 대상.</summary>
    public static readonly DependencyProperty RequestedTiltDegProperty = DependencyProperty.Register(
        nameof(RequestedTiltDeg), typeof(double), typeof(GMapCustomControl),
        new FrameworkPropertyMetadata(MapTiltModel.DefaultAngleDeg, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnRequestedTiltDegChanged, CoerceRequestedTiltDeg));

    /// <summary>사용자 요청 각(도). 범위 밖은 [0, <see cref="MapTiltModel.HardMaxAngleDeg"/>] 로 코어스. 변경 시 재평가(코얼레싱).</summary>
    public double RequestedTiltDeg
    {
        get => (double)GetValue(RequestedTiltDegProperty);
        set => SetValue(RequestedTiltDegProperty, value);
    }

    /// <summary>틸트 kill-switch(G5, 기본 OFF) — 인스턴스 플래그(회전의 정적 RotationFeature 와 달리 컨트롤별). OFF 는 즉시 φ=0(G10).</summary>
    public static readonly DependencyProperty IsTiltFeatureEnabledProperty = DependencyProperty.Register(
        nameof(IsTiltFeatureEnabled), typeof(bool), typeof(GMapCustomControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsTiltFeatureEnabledChanged));

    /// <summary>틸트 kill-switch. 변경 시 재평가(코얼레싱). 토글은 <see cref="ToggleTiltFeature"/> 단일 진입점.</summary>
    public bool IsTiltFeatureEnabled
    {
        get => (bool)GetValue(IsTiltFeatureEnabledProperty);
        set => SetValue(IsTiltFeatureEnabledProperty, value);
    }

    /// <summary>게이트 설정(MinZoom·HysteresisSteps·MaxAngleDeg) — <see cref="MapTiltModel.Normalize"/> 는 호출자 책임(VM 복원 시).
    /// null 대입은 기본값 모델로 대체. 대입 시 정착기(φmax)를 재생성하고 재평가한다.</summary>
    public MapTiltModel TiltSettings
    {
        get => _tiltSettings;
        set
        {
            _tiltSettings = value ?? new MapTiltModel();
            StopTiltSettleTimer();
            _tiltSettler = null;                                   // φmax 변경 → 정착기 재생성
            var settler = EnsureTiltSettler();
            settler.GateTransition(TiltDeg);                       // 현재 φ 로 즉시 정착(대기 취소)
            PublishTiltLayout(settler);
            _log?.Info($"[Tilt] settings={_tiltSettings}");
            ReevaluateTilt("settings");
        }
    }

    /// <summary>현재 상태기계 상태(직전 커밋 판정). 게이트 ON 여부는 <see cref="TiltDecision.Active"/>(TiltDecided 인자)로 읽는다.</summary>
    public TiltState TiltState { get; private set; } = TiltState.Off;

    /// <summary>현재 사유 식별자 — TiltMath.ReasonOff/Tier0/AnchorLock/Below/Hold/Active(툴팁·로그 매핑).</summary>
    public string TiltReason { get; private set; } = TiltMath.ReasonOff;

    /// <summary>코얼레싱 판정 후 φ 또는 상태/사유가 바뀔 때 발화(프레임당 최대 1회). 인자 = 적용된 판정.</summary>
    public event EventHandler<TiltDecision>? TiltDecided;

    /// <summary>진단/테스트용 Tier 강제값 — null 이면 <see cref="RenderTierProbe"/> 실측(기본). true = Tier0 강등 강제, false = 하드웨어 가정.
    /// 헤드리스 STA(렌더 타깃 없는 스레드는 Tier 0)에서 게이트 경로를 검증하기 위한 seam. 운영 코드는 대입하지 않는다.</summary>
    public bool? SoftwareTierOverride { get; set; }

    /// <summary>형제 캔버스(팝업) Y 보정용 뷰포트 오프셋 = −Δ(오버스캔 상단 여분). 오버스캔 미적용이면 0.
    /// Δ 는 FR-04 레이아웃(음수 Margin)에서 읽는다 — 컨버터가 Margin=(0,−Δ,0,−Δ) 를 세우므로 <c>−Margin.Top</c> 이 곧 적용된 Δ.</summary>
    public double ViewportOffsetY => -CurrentOverscanDelta();

    /// <summary>틸트 kill-switch 토글(단일 진입점, ToggleRotationFeature 동형) — 키보드/툴바/메뉴 공용. 앵커 A모드 잠금은 판정(Locked)이 처리한다.</summary>
    public void ToggleTiltFeature()
    {
        IsTiltFeatureEnabled = !IsTiltFeatureEnabled;
        _log?.Info($"[Tilt] kill-switch 토글: {(IsTiltFeatureEnabled ? $"ON — Ctrl+↑/↓ {TILT_STEP_COARSE}°, Ctrl+Shift+↑/↓ {TILT_STEP_FINE}° (줌 {TiltSettings.MinZoom:0.0} 이상에서 적용)" : "OFF — 즉시 φ=0")}");
    }

    /// <summary>요청 각을 ±delta 만큼 조정(0~TiltSettings.MaxAngleDeg 클램프). 키보드 Ctrl(+Shift)+↑/↓ 와 VM 커맨드 공용.</summary>
    public void StepTiltAngle(double deltaDeg)
    {
        if (!double.IsFinite(deltaDeg) || deltaDeg == 0) return;
        double max = TiltMath.ClampAngle(TiltSettings.MaxAngleDeg, MapTiltModel.HardMaxAngleDeg);
        double next = TiltMath.ClampAngle(RequestedTiltDeg + deltaDeg, max);
        if (Math.Abs(next - RequestedTiltDeg) < TiltOverscanMath.AngleEpsilon) return;
        RequestedTiltDeg = next;
        _log?.Info($"[Tilt] 각도 스텝 {(deltaDeg > 0 ? "+" : "")}{deltaDeg:0.#}° → 요청 {next:F1}°");
    }

    /// <summary>
    /// inner(컨트롤 로컬) 좌표계에서 실제로 화면에 보이는 사각형(FR-04) = 컨트롤 중심 ± (W/(2s), H_view/(2·s·cosφ)).
    /// 화면 정렬 항목(나침반/회전정보 텍스트·팔레트 드롭 힌트·드로잉 HUD 클램프)이 소비한다.
    /// 디지털줌·틸트가 없고 오버스캔이 없으면 (0,0,W,H) 그대로.
    /// </summary>
    public Rect GetVisibleInnerRect()
    {
        double w = ActualWidth, h = ActualHeight;
        if (!(w > 0) || !(h > 0)) return new Rect(0, 0, Math.Max(0, w), Math.Max(0, h));
        double delta = CurrentOverscanDelta();
        double hView = Math.Max(0, h - 2 * delta);
        double s = DigitalZoomScale, phi = TiltDeg;
        var tl = TiltOverscanMath.OuterToInner((0.0, 0.0), s, phi, w, h, delta);
        var br = TiltOverscanMath.OuterToInner((w, hView), s, phi, w, h, delta);
        return new Rect(new Point(tl.X, tl.Y), new Point(br.X, br.Y));
    }

    /// <summary>재평가 요청(코얼레싱) — 줌 3지점·플래그·각도·설정·앵커·Tier·Loaded 가 호출. 프레임당 1회만 판정·커밋한다.</summary>
    public void ReevaluateTilt(string cause)
    {
        _tiltGate.Request(cause);
        if (_tiltReevalQueued) return;
        _tiltReevalQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _tiltReevalQueued = false;
            if (!_tiltGate.IsPending) return;                     // Flush 가 먼저 커밋했으면 무동작
            try { CommitTiltNow(); }
            catch (Exception ex) { _log?.Error($"[Tilt] 재평가 커밋 실패: {ex.Message}"); }
        }));
    }

    /// <summary>대기 중인 재평가를 지금 커밋(부팅 복원·테스트용). 반환 = 커밋했는가.</summary>
    public bool FlushTiltReevaluation()
    {
        if (!_tiltGate.IsPending) return false;
        CommitTiltNow();
        return true;
    }

    #endregion

    #region Tilt — 판정·적용

    private static object CoerceRequestedTiltDeg(DependencyObject d, object baseValue)
        => TiltMath.ClampAngle((double)baseValue, MapTiltModel.HardMaxAngleDeg);

    private static void OnRequestedTiltDegChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapCustomControl)d).ReevaluateTilt("angle");

    private static void OnIsTiltFeatureEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GMapCustomControl)d).ReevaluateTilt("flag");

    /// <summary>OnInitialized 에서 1회 — Loaded/Unloaded 배선(Tier 구독·정착기 정리).</summary>
    private void InitializeTilt()
    {
        Loaded += OnTiltLoaded;
        Unloaded += OnTiltUnloaded;
    }

    private void OnTiltLoaded(object sender, RoutedEventArgs e)
    {
        if (!_tiltTierSubscribed)
        {
            try
            {
                _log?.Info(RenderTierProbe.CurrentLog);            // VER-04 실기 grep 키 "[Tilt] tier="
                RenderTierProbe.TierChanged += OnRenderTierChanged;
                _tiltTierSubscribed = true;
            }
            catch (Exception ex) { _log?.Warning($"[Tilt] Tier 프로브 구독 실패(강등 없음으로 진행): {ex.Message}"); }
        }
        ReevaluateTilt("loaded");
    }

    private void OnTiltUnloaded(object sender, RoutedEventArgs e)
    {
        if (_tiltTierSubscribed)
        {
            RenderTierProbe.TierChanged -= OnRenderTierChanged;
            _tiltTierSubscribed = false;
        }
        StopTiltSettleTimer();
        if (_tiltSettler != null && _tiltSettler.Flush()) PublishTiltLayout(_tiltSettler);
    }

    private void OnRenderTierChanged(int tier)
    {
        _log?.Info(RenderTierProbe.FormatLog(tier));
        ReevaluateTilt("tier");
    }

    /// <summary>앵커 모드(G3): 비활성 None · A모드(회전 잠금) RotationLocked(틸트 차단) · B모드 RotationAllowed.</summary>
    private TiltAnchorMode CurrentTiltAnchorMode
        => _anchorSiteRect == null ? TiltAnchorMode.None
         : _anchorAllowsRotation ? TiltAnchorMode.RotationAllowed
         : TiltAnchorMode.RotationLocked;

    private bool ProbeSoftwareTier()
    {
        if (SoftwareTierOverride is bool forced) return forced;
        try { return RenderTierProbe.IsSoftware; }
        catch (Exception ex)
        {
            if (!_tiltTierProbeWarned) { _tiltTierProbeWarned = true; _log?.Warning($"[Tilt] Tier 읽기 실패 — 강등 없음으로 판정: {ex.Message}"); }
            return false;
        }
    }

    private TiltInput BuildTiltInput()
    {
        var s = TiltSettings;
        return new TiltInput(
            IsTiltFeatureEnabled,
            ProbeSoftwareTier(),
            CurrentTiltAnchorMode,
            EffectiveZoom,
            _tiltGate.WasActive,                                   // 코얼레서가 직전 커밋값으로 덮어쓴다(형식상 전달)
            RequestedTiltDeg,
            s.MinZoom,
            s.HysteresisSteps,
            s.MaxAngleDeg);
    }

    /// <summary>코얼레싱된 최종 상태로 1회 판정·적용(FR-03 후속 순서: ApplyViewTransform → 정착기 → RecomputeAnchorViewportBounds → QueueViewportSnapshot → TiltDecided).</summary>
    private void CommitTiltNow()
    {
        string cause = _tiltGate.LastCause ?? "-";
        int coalesced = _tiltGate.PendingCount;
        bool prevActive = _tiltGate.WasActive;
        var input = BuildTiltInput();
        var decision = _tiltGate.Commit(input);
        ApplyTiltDecision(decision, prevActive, cause, coalesced, input.EffectiveZoom);
    }

    private void ApplyTiltDecision(TiltDecision decision, bool prevActive, string cause, int coalesced, double effectiveZoom)
    {
        double prevPhi = TiltDeg;
        bool phiChanged = Math.Abs(decision.PhiDeg - prevPhi) > TiltOverscanMath.AngleEpsilon;
        bool stateChanged = decision.State != TiltState
                            || !string.Equals(decision.Reason, TiltReason, StringComparison.Ordinal);
        TiltState = decision.State;
        TiltReason = decision.Reason;
        if (!phiChanged && !stateChanged) return;

        if (phiChanged)
        {
            SetValue(TiltDegPropertyKey, decision.PhiDeg);
            ApplyViewTransform();                                  // ① 렌더(즉시 스냅, G8)
            // ② φ_layout: 게이트 전이(ON↔OFF)는 즉시 정착, Active 유지 중 각도 변경만 선확장+150 ms 정착
            bool gateTransition = decision.Active != prevActive || !decision.Active;
            SettleTiltLayout(decision.PhiDeg, gateTransition);
            RecomputeAnchorViewportBounds();                       // ③ 세로 가시 범위 변화 → inset 재계산
            QueueViewportSnapshot();                               // ④ 소비처 통지(TiltCos=cosφ)
        }

        _log?.Info($"[Tilt] cause={cause} coalesced={coalesced} z={effectiveZoom:F1} state={decision.State} reason={decision.Reason} φ={decision.PhiDeg:F1}° active={decision.Active}");
        TiltDecided?.Invoke(this, decision);
    }

    #endregion

    #region Tilt — 뷰 변환(FR-01)·좌표(FR-05)

    /// <summary>
    /// 뷰 변환 단일 빌더(FR-01) — 디지털 배율 s 와 틸트 φ 를 하나의 <c>ScaleTransform(s, s·cosφ, W/2, H/2)</c> 로 컨트롤 RenderTransform 에 적용.
    /// (s≈1 ∧ φ=0) 이면 null(항등 복원). ★ 불변식 5: 항상 새 객체를 재대입한다(in-place 변경 금지 — 어도너 미추종).
    /// 호출: dzl DP 콜백 · SizeChanged(dzl&gt;0 ∨ φ&gt;0) · 틸트 판정 커밋.
    /// </summary>
    private void ApplyViewTransform()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;      // SE-1: 초기화/리사이즈 중 중심 어긋남 방어(NFR-4)
        double s = DigitalZoomScale;
        double phi = TiltDeg;
        bool identity = Math.Abs(s - 1.0) < TiltOverscanMath.IdentityEpsilon && TiltOverscanMath.IsFlat(phi);
        RenderTransform = identity
            ? null
            : new ScaleTransform(s, s * TiltOverscanMath.CosOf(phi), ActualWidth / 2.0, ActualHeight / 2.0);
        _log?.Info($"[ViewTransform] s={s:F2} φ={phi:F1} level={DigitalZoomLevel}");
        InvalidateVisual();
    }

    /// <summary>적용된 오버스캔 Δ(상단 여분) — FR-04 레이아웃의 음수 Margin 에서 읽는다(컨버터 Margin=(0,−Δ,0,−Δ)). 미적용이면 0.</summary>
    private double CurrentOverscanDelta()
    {
        double top = Margin.Top;
        return double.IsFinite(top) && top < 0 ? -top : 0.0;
    }

    /// <summary>
    /// inner(논리/타일) 좌표 → outer(화면/형제 캔버스) 좌표(FR-05): <c>outer = (cx_v + (x−cx)·s, cy_v + (y−cy)·s·cosφ)</c>,
    /// (cx,cy)=컨트롤 중심, (cx_v,cy_v)=뷰포트 중심 (W/2, H/2−Δ). 수식 SSOT = <see cref="TiltOverscanMath.InnerToOuter"/>.
    /// ★ 카메라 팝업 경로(PropertyPanelCanvas — RenderTransform '밖' 형제 캔버스) 전용. 컨트롤 '안'(마커/격자/스냅)에는 절대 적용 금지
    ///   (WPF 가 e.GetPosition(this)에 RenderTransform.Inverse 를 자동 적용 — 이중보정 버그). s=1 ∧ φ=0 ∧ Δ=0 이면 항등.
    ///   ※ tests/GMaps.Ui.Tests/DigitalZoomCoordinateTests 가 같은 순수 수식을 검증한다(L-1).
    /// </summary>
    public Point InnerToOuter(Point p)
    {
        var r = TiltOverscanMath.InnerToOuter((p.X, p.Y), DigitalZoomScale, TiltDeg, ActualWidth, ActualHeight, CurrentOverscanDelta());
        return new Point(r.X, r.Y);
    }

    /// <summary>outer(화면) 좌표 → inner(논리/타일) 좌표 — <see cref="InnerToOuter"/> 의 역함수(팝업 드래그 저장 시 FromLocalToLatLng 입력용). 팝업 경로 전용.</summary>
    public Point OuterToInner(Point p)
    {
        var r = TiltOverscanMath.OuterToInner((p.X, p.Y), DigitalZoomScale, TiltDeg, ActualWidth, ActualHeight, CurrentOverscanDelta());
        return new Point(r.X, r.Y);
    }

    /// <summary>스냅샷에 실을 cos(틸트각 φ)(FR-06). φ=0 이면 <see cref="MapViewportSnapshot.DefaultTiltCos"/>(1.0).</summary>
    private double GetTiltCosForSnapshot() => TiltOverscanMath.CosOf(TiltDeg);

    #endregion

    #region Tilt — φ_layout 정착기(FR-04 · G8)

    private TiltLayoutSettler EnsureTiltSettler()
        => _tiltSettler ??= new TiltLayoutSettler(TiltMath.ClampAngle(TiltSettings.MaxAngleDeg, MapTiltModel.HardMaxAngleDeg));

    /// <summary>게이트 전이면 즉시 φ_layout=φ, Active 유지 중 각도 변경이면 φmax 선확장 후 타이머 정착(마지막 변경 +150 ms).</summary>
    private void SettleTiltLayout(double phiDeg, bool gateTransition)
    {
        var settler = EnsureTiltSettler();
        bool layoutChanged;
        if (gateTransition)
        {
            StopTiltSettleTimer();
            layoutChanged = settler.GateTransition(phiDeg);
        }
        else
        {
            layoutChanged = settler.Change(phiDeg, Environment.TickCount64);   // Pending 아니면 Begin(φmax 선확장)
            StartTiltSettleTimer();
        }
        if (layoutChanged) PublishTiltLayout(settler);
    }

    private void StartTiltSettleTimer()
    {
        _tiltSettleTimer ??= new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(EnsureTiltSettler().CommitDelayMs),
        };
        _tiltSettleTimer.Tick -= OnTiltSettleTick;
        _tiltSettleTimer.Tick += OnTiltSettleTick;
        if (!_tiltSettleTimer.IsEnabled) _tiltSettleTimer.Start();
    }

    private void StopTiltSettleTimer()
    {
        if (_tiltSettleTimer == null) return;
        _tiltSettleTimer.Stop();
        _tiltSettleTimer.Tick -= OnTiltSettleTick;
    }

    private void OnTiltSettleTick(object? sender, EventArgs e)
    {
        var settler = _tiltSettler;
        if (settler == null) { StopTiltSettleTimer(); return; }
        if (!settler.Tick(Environment.TickCount64)) return;        // 마지막 변경 후 150 ms 미경과 — 다음 틱
        StopTiltSettleTimer();
        PublishTiltLayout(settler);
    }

    /// <summary>φ_layout DP 갱신(+NFR-01 계측 로그 — LayoutChangeCount = Height 재레이아웃 횟수, 조작당 ≤2).</summary>
    private void PublishTiltLayout(TiltLayoutSettler settler)
    {
        double prev = TiltLayoutDeg;
        SetValue(TiltLayoutDegPropertyKey, settler.PhiLayoutDeg);
        if (Math.Abs(prev - settler.PhiLayoutDeg) > TiltOverscanMath.AngleEpsilon)
            _log?.Info($"[Tilt] layout φ_layout={settler.PhiLayoutDeg:F1}° (φ={settler.PhiDeg:F1}°) pending={settler.IsPending} commits={settler.CommitCount} layoutChanges={settler.LayoutChangeCount}");
    }

    #endregion
}
