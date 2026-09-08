using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      : PIDS 그룹 3D 철망 컨트롤(FR-06/07) — 2D 3-레이어 폴리라인 위에 FenceRunVisual(PART_Fence3D) + Posts LOD 캔버스(PART_PostCanvas).
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 3중 게이트 = Symbol3DFeature.IsEnabled ∧ 그룹 Render3D ∧ LOD Full3D. 기하 재계산은 베이스 <c>OnLineGeometryUpdated</c> 훅에서만 일어나며
/// (점·간격·형태·높이·닫힘) 해시가 같으면 메시를 재생성하지 않는다(R-01). 히트 테스트는 2D 폴리라인 스트로크 계약 그대로(PART_HitBackplate 없음).
/// 재생성 게이트(C2/C4/C16): 해시는 첫 정점 기준 상대좌표(1 px)라 평행이동에 불변이고, 표시 단계(show3D/showPosts) 전이는 해시가 같아도 재할당하며,
/// 정수 투영 위상 흔들림(±1 px)은 메시를 재사용한 뒤 idle 에서 정확 레이아웃을 1회만 다시 만든다(<see cref="DecideFrame"/>, 순수 함수).
/// </summary>
public sealed class GMapMarkerPidsGroup3DControl : GMapMarkerPidsGroupControl
{
    /// <summary>
    /// 렌더 크기 역기록 OFF — 3DHousing/Infra3D 선례(<c>GMapMarker3DHousingControl:15</c>). 근거(C2/C4 리뷰 확정):
    /// 라인 컨트롤의 Width/Height 는 정점 bbox 에서 매 줌·이동마다 다시 계산되는 파생값이라 Marker.Width/Height 로 되돌려 쓸 의미가 없고, 되돌려 쓰면
    /// ① <c>GMapBaseMarker.Height</c> 세터가 <c>UpdateOffset()</c> 으로 Offset 을 (−W/2,−H/2) 로 되돌렸다가 재보정하는 왕복,
    /// ② 속성창 TwoWay 바인딩 → MapViewModel 이 프레임마다 '속성 변경 미영속: Height' WARN(Move 드래그 중 62회/1.05 s 실측) 또는
    ///    편집모드+비편집중(타일 줌 변경)이면 DbUpdateProcess+Undo 기록(IsReplayableProperty 에 Width/Height 포함)이 따라온다.
    /// 히트 테스트(<c>GetMarkerAtScreen</c>)는 Shape.ActualWidth/Height 를 쓰고 GMapPidsGroupMarker 는 Width/Height 를 소비하지 않으므로 영향 없음.
    /// 베이스 UpdateLineGeometry 의 <c>Width = …</c> 로컬 대입은 OneWay 바인딩을 걷어내지만 라인 크기는 이 컨트롤이 항상 직접 세팅하므로 무해.
    /// </summary>
    protected override bool WritesBackRenderSize => false;

    /// <summary>지터 허용 오차(px) — 정수 GPoint 투영은 정점별 반올림 위상이 갈리면(DB 8dp 잔차 부호 혼재 + 디지털 줌 2.0× 0.5 px 스텝) 상대좌표가 정확히 ±1 px 흔들린다(C2/C4 리뷰 실측).</summary>
    public const double JitterTolerancePx = 1.0;

    static GMapMarkerPidsGroup3DControl() => DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerPidsGroup3DControl), new FrameworkPropertyMetadata(typeof(GMapMarkerPidsGroup3DControl)));

    private static DependencyProperty Register(string name, Type type, object? value) =>
        DependencyProperty.Register(name, type, typeof(GMapMarkerPidsGroup3DControl), new PropertyMetadata(value, (d, _) => ((GMapMarkerPidsGroup3DControl)d).OnFenceInputChanged()));
    public static readonly DependencyProperty PostSpacingMProperty = Register(nameof(PostSpacingM), typeof(double?), null);
    public static readonly DependencyProperty FenceHeightMProperty = Register(nameof(FenceHeightM), typeof(double?), null);
    public static readonly DependencyProperty FenceModeProperty = Register(nameof(FenceMode), typeof(EnumFenceMode), EnumFenceMode.Posts);
    public static readonly DependencyProperty Render3DProperty = Register(nameof(Render3D), typeof(bool), true);
    public static readonly DependencyProperty ReverseSensorOrderProperty = Register(nameof(ReverseSensorOrder), typeof(bool), false);
    public static readonly DependencyProperty ActiveNodesProperty = DependencyProperty.Register(nameof(ActiveNodes), typeof(IReadOnlySet<int>), typeof(GMapMarkerPidsGroup3DControl),
        new PropertyMetadata(null, (d, e) => { var c = (GMapMarkerPidsGroup3DControl)d; if (c._fence != null) c._fence.ActiveNodes = c.MapActiveNodes((IReadOnlySet<int>?)e.NewValue); }));
    public static readonly DependencyProperty FenceLodProperty = DependencyProperty.Register(nameof(FenceLod), typeof(FenceLodLevel), typeof(GMapMarkerPidsGroup3DControl), new PropertyMetadata(FenceLodLevel.Line2D));

    public double? PostSpacingM { get => (double?)GetValue(PostSpacingMProperty); set => SetValue(PostSpacingMProperty, value); }
    public double? FenceHeightM { get => (double?)GetValue(FenceHeightMProperty); set => SetValue(FenceHeightMProperty, value); }
    public EnumFenceMode FenceMode { get => (EnumFenceMode)GetValue(FenceModeProperty); set => SetValue(FenceModeProperty, value); }
    public bool Render3D { get => (bool)GetValue(Render3DProperty); set => SetValue(Render3DProperty, value); }
    public bool ReverseSensorOrder { get => (bool)GetValue(ReverseSensorOrderProperty); set => SetValue(ReverseSensorOrderProperty, value); }
    public IReadOnlySet<int>? ActiveNodes { get => (IReadOnlySet<int>?)GetValue(ActiveNodesProperty); set => SetValue(ActiveNodesProperty, value); }
    /// <summary>마지막 기하 갱신에서 판정한 LOD(진단·자동화용).</summary>
    public FenceLodLevel FenceLod { get => (FenceLodLevel)GetValue(FenceLodProperty); private set => SetValue(FenceLodProperty, value); }

    private FenceRunVisual? _fence;
    private Canvas? _postCanvas;
    private int _rebuilds, _skips;
    private FenceFrame? _last;                       // 마지막으로 '적용'한 프레임(스킵/재사용 프레임 아님)
    private bool _lastShow3D, _lastShowPosts;        // C16: 표시 단계 전이 감지
    private bool _settling;                          // C2/C4: 디바운스 정착(정확 재계산) 중
    private System.Windows.Threading.DispatcherTimer? _settleTimer;   // 지터 재사용 뒤 150 ms 디바운스(리뷰: ContextIdle 은 스로틀이 아님)
    private int _settleRequests, _settles;
    /// <summary>마지막 프레임 계산 결과(테스트·진단).</summary>
    public FenceLayoutResult? CurrentLayout => _last?.Layout;
    /// <summary>진단: 재생성/스킵 횟수.</summary>
    public (int Rebuilds, int Skips) FrameCounters => (_rebuilds, _skips);
    /// <summary>진단·테스트: 정착 요청 수(지터 재사용 횟수)와 실제 정착(정확 재계산) 수 — 연속 Reuse N회 → 정착 1회.</summary>
    internal (int Requests, int Fired) SettleCounters => (_settleRequests, _settles);
    internal bool IsSettlePending => _settleTimer?.IsEnabled == true;

    public GMapMarkerPidsGroup3DControl() { }
    public GMapMarkerPidsGroup3DControl(GMapPidsGroupMarker marker) : base(marker) { }

    protected override void SetupSpecificBindings()
    {
        base.SetupSpecificBindings();
        if (Marker == null) return;
        SetupPropertyBinding(PostSpacingMProperty, nameof(Marker.PostSpacingM), BindingMode.OneWay);
        SetupPropertyBinding(FenceHeightMProperty, nameof(Marker.FenceHeightM), BindingMode.OneWay);
        SetupPropertyBinding(FenceModeProperty, nameof(Marker.FenceMode), BindingMode.OneWay);
        SetupPropertyBinding(Render3DProperty, nameof(Marker.Render3D), BindingMode.OneWay);
        SetupPropertyBinding(ReverseSensorOrderProperty, nameof(Marker.ReverseSensorOrder), BindingMode.OneWay);
        SetupPropertyBinding(ActiveNodesProperty, nameof(Marker.ActiveSensorDeviceIds), BindingMode.OneWay);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _fence = GetTemplateChild("PART_Fence3D") as FenceRunVisual;
        _postCanvas = GetTemplateChild("PART_PostCanvas") as Canvas;
        _last = null; _lastShow3D = _lastShowPosts = false;
        RefreshLineGeometry();
    }

    private void OnFenceInputChanged() => RefreshLineGeometry();

    /// <summary>첫 정점 기준 상대 로컬 px 를 1 px 로 반올림 — 평행이동 불변 해시 입력(C2/C4 ①). 빈 입력이면 빈 배열.</summary>
    public static IReadOnlyList<Point> RelativePoints(IReadOnlyList<Point> localPoints)
    {
        if (localPoints.Count == 0) return Array.Empty<Point>();
        var origin = localPoints[0];
        var result = new Point[localPoints.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Point(Math.Round(localPoints[i].X - origin.X), Math.Round(localPoints[i].Y - origin.Y));
        return result;
    }

    /// <summary>두 상대 점열이 점마다 ±<paramref name="tolerancePx"/>(체비쇼프) 이내인가 — 정수 투영 위상 흔들림 동치(C2/C4). 점 수가 다르면 false.</summary>
    public static bool IsJitterEquivalent(IReadOnlyList<Point> a, IReadOnlyList<Point> b, double tolerancePx = JitterTolerancePx)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (Math.Abs(a[i].X - b[i].X) > tolerancePx || Math.Abs(a[i].Y - b[i].Y) > tolerancePx) return false;
        return true;
    }

    /// <summary>
    /// 재생성 게이트 순수 판정. <paramref name="last"/> 는 마지막으로 <b>적용</b>한 프레임(스킵/재사용 프레임이 아님).
    /// <list type="bullet">
    /// <item>이전 프레임 없음 · show3D/showPosts 전이 → <see cref="FenceFrameAction.Rebuild"/>(C16: 디지털 줌은 기하를 안 바꿔 해시가 같아도 Layout/캔버스를 다시 채워야 한다)</item>
    /// <item>해시 동일 → <see cref="FenceFrameAction.Skip"/></item>
    /// <item>설정 동일 + 상대 기하 ±1 px 이내(그리고 정확 재계산 중이 아님) → <see cref="FenceFrameAction.Reuse"/></item>
    /// <item>그 외 → Rebuild</item>
    /// </list>
    /// </summary>
    public static FenceFrameAction DecideFrame(FenceFrame frame, bool show3D, bool showPosts, FenceFrame? last, bool lastShow3D, bool lastShowPosts, bool exact = false)
    {
        if (last is null || show3D != lastShow3D || showPosts != lastShowPosts) return FenceFrameAction.Rebuild;
        if (frame.Hash == last.Hash) return FenceFrameAction.Skip;
        if (!exact && frame.SettingsHash == last.SettingsHash && IsJitterEquivalent(last.RelativePoints, frame.RelativePoints)) return FenceFrameAction.Reuse;
        return FenceFrameAction.Rebuild;
    }

    /// <summary>
    /// 순수 프레임 계산(FR-06): 로컬 px 점 + 설정 → 레이아웃(px 단위: FenceLayout 을 px 프레임에서 그대로 실행해 2D 선과 정확히 정합)·LOD·높이 px·해시.
    /// 해시는 <see cref="RelativePoints"/>(첫 정점 기준, 1 px) 기반이라 평행이동에 불변이다(C2/C4). 지도 의존이 없어 헤드리스 테스트 가능.
    /// </summary>
    public static FenceFrame ComputeFrame(
        IReadOnlyList<Point> localPoints, double spacingM, double heightM, EnumFenceMode mode, bool isClosed, double metersPerPixel, double digitalZoomScale)
    {
        var lod = Helpers.Fence.FenceLod.Select(Helpers.Fence.FenceLod.PxPerSpacing(spacingM, digitalZoomScale, metersPerPixel));
        if (localPoints.Count < 2 || !(metersPerPixel > 0)) return new FenceFrame(FenceLayoutResult.Empty, lod, 0, 0);
        double spacingPx = spacingM / metersPerPixel;
        // 가독 과장(2.5D): 기준 높이가 MinVisualHeightPx 이상으로 보이는 배율을 모든 높이에 똑같이 곱한다 → 높이 px ∝ 실제 높이(m).
        //   종전 Math.Max(실척, 하한) 은 1.5~4.0 m 실척이 전부 하한 미만이라 높이를 평탄화했다(높이 조절 무반응의 원인).
        double heightPx = FenceMath.VisualHeightPx(heightM, metersPerPixel, FenceDefaults.FenceHeightM, FenceDefaults.MinVisualHeightPx);
        var relative = RelativePoints(localPoints);
        int settings = HashCode.Combine(Math.Round(spacingPx, 2), mode, isClosed, Math.Round(heightPx, 1));
        var hash = new HashCode();
        hash.Add(settings);
        foreach (var p in relative) { hash.Add(p.X); hash.Add(p.Y); }
        var pts = localPoints.Select(p => new FencePoint(p.X, -p.Y)).ToList();   // Z = −y_px (북 = 화면 위)
        var layout = FenceLayout.Compute(pts, spacingPx, mode, isClosed);
        return new FenceFrame(layout, lod, heightPx, hash.ToHashCode()) { RelativePoints = relative, SettingsHash = settings };
    }

    protected override void OnLineGeometryUpdated(IReadOnlyList<Point> localPoints, Point origin)
    {
        if (_fence == null) return;
        var map = MapControl;
        if (map == null || Marker == null || localPoints.Count < 2)
        {
            _fence.Visibility = Visibility.Collapsed; _postCanvas?.Children.Clear(); _last = null; _lastShow3D = _lastShowPosts = false;
            return;
        }
        double spacingM = PostSpacingM ?? Utils.Symbol3DFeature.FencePostSpacingM;
        double heightM = FenceHeightM ?? Utils.Symbol3DFeature.FenceHeightM;
        double mpp = Helpers.Fence.FenceLod.MetersPerPixel(Marker.Position.Lat, map.Zoom);
        var frame = ComputeFrame(localPoints, spacingM, heightM, FenceMode, IsClosedPath, mpp, map.DigitalZoomScale);
        FenceLod = frame.Lod;
        bool show3D = Helpers.Fence.FenceLod.Show3D(Utils.Symbol3DFeature.IsEnabled, Render3D, frame.Lod);
        bool showPosts = Utils.Symbol3DFeature.IsEnabled && Render3D && frame.Lod == FenceLodLevel.PostsAndThickLine;
        _fence.Visibility = show3D ? Visibility.Visible : Visibility.Collapsed;
        if (_postCanvas != null) _postCanvas.Visibility = showPosts ? Visibility.Visible : Visibility.Collapsed;
        if (!show3D && !showPosts) return;

        ApplyFrame(frame, show3D, showPosts, mpp);
    }

    /// <summary>
    /// 게이트(표시 여부) 이후 본문 — 판정(<see cref="DecideFrame"/>) → 스킵/재사용(+정착 예약)/재생성 → Layout·Posts 캔버스 갱신.
    /// 훅과 정착 타이머가 공유하며, 지도 없이 템플릿만 적용한 컨트롤에서 직접 호출해 배선을 검증할 수 있다(C16/C2 테스트).
    /// </summary>
    internal void ApplyFrame(FenceFrame frame, bool show3D, bool showPosts, double mpp)
    {
        if (_fence == null) return;
        FenceLod = frame.Lod;
        switch (DecideFrame(frame, show3D, showPosts, _last, _lastShow3D, _lastShowPosts, _settling))
        {
            case FenceFrameAction.Skip: _skips++; return;
            case FenceFrameAction.Reuse: _skips++; ScheduleSettle(); return;   // 메시 재사용(≤1 px 오차) — Move 드래그 중 프레임마다 전량 재생성 방지(C2/C4)
        }
        _settleTimer?.Stop();   // 실제 재생성이 일어나면 보류 중인 정착은 불필요
        _last = frame; _lastShow3D = show3D; _lastShowPosts = showPosts; _rebuilds++;
        var layout = frame.Layout;
        if (show3D)
        {
            _fence.Mode = FenceMode; _fence.HeightPx = frame.HeightPx; _fence.MetersPerPixel = mpp; _fence.ActiveNodes = MapActiveNodes(ActiveNodes);
            _fence.Layout = layout;   // C16: Posts→Full3D 전이에서도 반드시 도달 — 이전 빌드가 Posts 였으면 Layout 이 null 이거나 옛 타일줌의 스테일 px 레이아웃
        }
        if (_postCanvas != null) RenderPosts2D(layout, showPosts);   // Full3D→Posts 전이도 캔버스를 다시 채운다(C16)
        _log?.Info($"[Fence] rebuild={_rebuilds} skip={_skips} posts={layout.Posts.Count} panels={layout.Panels.Count} nodes={layout.Nodes.Count} lod={frame.Lod} hPx={frame.HeightPx:F1} '{Marker?.Title}'");
    }

    /// <summary>
    /// 지터 재사용 뒤 정확 레이아웃을 1회만 다시 만든다 — <b>디바운스</b>(마지막 재사용 후 <see cref="FenceDefaults.SliderCommitDelayMs"/> ms).
    /// ContextIdle 우선순위는 입력 사이 큐가 비는 순간마다 실행돼 스로틀이 되지 못한다(리뷰 지적) — 타이머는 Reuse 마다 재시작되므로
    /// 드래그 중엔 계속 미뤄지고 손을 뗀 뒤 150 ms 에 한 번 실행된다. 재진입은 <see cref="_settling"/> 으로 exact 판정.
    /// </summary>
    private void ScheduleSettle()
    {
        _settleRequests++;
        if (_settleTimer == null)
        {
            _settleTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
            { Interval = TimeSpan.FromMilliseconds(FenceDefaults.SliderCommitDelayMs) };
            _settleTimer.Tick += (_, _) =>
            {
                _settleTimer.Stop(); _settles++;
                if (_fence == null) return;
                _settling = true;
                try { RefreshLineGeometry(); }
                finally { _settling = false; }
            };
        }
        _settleTimer.Stop(); _settleTimer.Start();
    }

    /// <summary>센서 장착 모드에서 그룹 센서 순번 k ↔ 노드 k(역순 옵션). ActiveSensorDeviceIds 는 장비 Id 집합이라 Phase 2 매핑 전까지 순번 그대로 쓴다.</summary>
    private IReadOnlySet<int>? MapActiveNodes(IReadOnlySet<int>? active)
    {
        if (active is null || _last is null || !ReverseSensorOrder) return active;
        int n = _last.Layout.Nodes.Count;
        return active.Select(i => n - 1 - i).Where(i => i >= 0).ToHashSet();
    }

    private void RenderPosts2D(FenceLayoutResult layout, bool show)
    {
        _postCanvas!.Children.Clear();
        if (!show) return;
        foreach (var post in layout.Posts)
        {
            double r = post.IsThick ? 3.2 : 2.4;
            var dot = new Ellipse { Width = r * 2, Height = r * 2, Fill = _fence!.MetalBrush, Stroke = Brushes.White, StrokeThickness = .6, IsHitTestVisible = false };
            Canvas.SetLeft(dot, post.Position.X - r); Canvas.SetTop(dot, -post.Position.Z - r);
            _postCanvas.Children.Add(dot);
        }
    }
}
