using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 캔버스 골격 — 겹 순서 · 팬 변환 · 선 층 · 단계 교체 · 입력 골격 (IMPL-20 · FR-04 · FR-24~27 · D-1 · D-2)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 관계도 캔버스. <b>지도(GMap)와 무관한 별도 컨트롤</b>이다(FR-04 · DF "지도 위에 새 드래그 금지").
/// </summary>
/// <remarks>
/// <para><b>겹 순서</b>(D-1, 아래 → 위): 점 격자 · [월드: 선 층(<see cref="DrawingVisual"/> 하나) · 노드 · 원래 자리 잔상 · 끌리는 층] · 오버레이.
/// 잔상 · 끌리는 층 · 오버레이는 Phase 3 의 끌기(<c>UnitMapCanvas.Drag</c>) · 오버레이(<c>UnitMapCanvas.Overlays</c>)가 채운다.</para>
/// <para><b>팬</b>은 월드 컨테이너의 <see cref="TranslateTransform"/> 만 바꾼다 — 선 층 · 노드는 다시 그리지 않는다(D-2 · NFR-02).
/// <b>줌</b>은 노드 <c>Canvas.Left/Top</c> 재계산 + 선 층 다시 그림이다. 의미 줌은 도형을 키우지 않으므로
/// <c>ScaleTransform</c> 으로는 표현할 수 없다(R-3). 단계는 <see cref="UnitMapLod.Resolve"/> 로 정하고 <b>바뀔 때만</b>
/// 노드 템플릿을 갈아 끼운다(NFR-03).</para>
/// <para>뷰모델은 이 컨트롤을 모르고 <see cref="IUnitMapSurface"/> 만 안다. 이 컨트롤은 뷰모델을 모르고
/// <see cref="IUnitMapInteraction"/> 에 입력을 뜻 없이 올린다. <b>UI 스레드 전용</b>(NFR-12).</para>
/// <para><b>입력 골격</b>: 누름 → 뗌을 <see cref="GraphGesture"/>(데드존 <c>DragMath</c> 8.0)로 가른다 — 데드존 안이면 선택 · 선택 해제,
/// 넘으면 팬 · 노드 끌기(Phase 3 이 채운다). 포인터는 <b>움직이지 않는 이 캔버스(루트) 기준</b>으로 잰다(FR-28 · VER-01).
/// 누른 순간부터 제스처가 끝날 때까지 <see cref="DragSession"/> 토큰을 <b>하나</b> 쥔다 — 부대 콘솔은 그동안 <c>SYNC_UNIT</c>
/// 재조회를 미룬다. 끝나는 길(뗌 · 캡처 상실 · <c>Esc</c> · 언로드)은 전부 <see cref="FinishGesture"/> 하나로 모인다.</para>
/// </remarks>
public partial class UnitMapCanvas : Grid, IUnitMapSurface
{
    /// <summary>캔버스 AutomationId(FR-40).</summary>
    public const string AUTOMATION_ID = "Units.Map.Canvas";

    public const string BACKGROUND_TOKEN = "SurfaceBrush";
    public const string GRID_TOKEN = "RowLineBrush";

    /// <summary>점 격자 간격(화면 DIU) — 배율과 무관(격자는 배경 질감이지 좌표계가 아니다).</summary>
    public const double GRID_SPACING = 24.0;

    /// <summary>첫 배율 — 개인 뷰 · 내 부대 중심이 없을 때(FR-15 — 뷰모델이 곧 맞춘다).</summary>
    public const double INITIAL_SCALE = 0.5;

    /// <summary>L1 짧은 이름 여백 — 폭 = 칸 × 배율 − 8(FR-20).</summary>
    public const double LABEL_MARGIN = 8.0;

    private readonly Rectangle _gridLayer = new() { IsHitTestVisible = false };
    private readonly TranslateTransform _gridShift = new();
    private readonly Canvas _world = new();
    private readonly TranslateTransform _pan = new();
    private readonly UnitMapLineLayer _lineLayer = new();
    private readonly Canvas _nodeLayer = new();
    private readonly Canvas _originLayer = new() { IsHitTestVisible = false };
    private readonly Canvas _dragLayer = new() { IsHitTestVisible = false };
    private readonly Grid _overlay = new();

    private readonly Dictionary<int, UnitMapNode> _nodes = new();
    private readonly List<UnitMapNode> _ordered = new();
    private readonly Dictionary<int, UnitMapNodeFacts> _facts = new();

    private GraphViewport _view = new(INITIAL_SCALE, new Vector(0, 0));
    private UnitMapLevel _level = UnitMapLod.ForScale(INITIAL_SCALE);
    private int _gridBuilds;
    private bool _themeRedrawPending;

    // 크기가 생기기 전(첫 레이아웃 전)에 온 뷰 요청 — 마지막 것만 크기가 생길 때 한 번 한다(must-cover 3).
    private Action? _pendingView;

    // 겹침 순서 = 트리 순(Tree.Ordered) — 선택해도 앞으로 올리지 않는다(PRD v1.3 FR-29 ⑥ · ISSUE-56).
    // 가려진 노드는 키보드 선택(화살표 · 검색)으로 닿는다.

    public UnitMapCanvas()
    {
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);     // 노드 200개를 Tab 으로 돌지 않는다(ISSUE-52)
        FocusVisualStyle = null;                                                     // 포커스 표시는 캔버스 자체 링(오버레이)
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, BACKGROUND_TOKEN);
        AutomationProperties.SetAutomationId(this, AUTOMATION_ID);

        // D-1 겹 순서. 월드 컨테이너 하나에 선 · 노드 · 잔상 · 끌리는 층 — 팬은 이 컨테이너 변환만 바꾼다.
        _world.RenderTransform = _pan;
        _world.Children.Add(_lineLayer);
        _world.Children.Add(_nodeLayer);
        _world.Children.Add(_originLayer);
        _world.Children.Add(_dragLayer);
        Children.Add(_gridLayer);
        Children.Add(_world);
        Children.Add(_overlay);

        // 테마 전환 신호 — 토큰 참조가 바뀌면 선 층 · 격자를 한 번 다시 그린다(그릴 때 TryFindResource 로 다시 찾는다, NFR-07).
        SetResourceReference(HierarchyTokenProperty, UnitMapLineLayer.HIERARCHY_TOKEN);
        SetResourceReference(AdjacencyTokenProperty, UnitMapLineLayer.ADJACENCY_TOKEN);
        SetResourceReference(GridTokenProperty, GRID_TOKEN);

        AddHandler(UnitMapNode.SelectRequestedEvent, new RoutedEventHandler(OnNodeSelectRequested));
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        BuildOverlays();
        RebuildGrid();
        ApplyPan();
        UpdateHud();
    }

    #region - 의존 속성 -
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(UnitMapScene), typeof(UnitMapCanvas), new PropertyMetadata(UnitMapScene.Empty, OnSceneChanged));

    /// <summary>그릴 장면(편제 · Δ 입힌 위치 · 노드 사실 · 레이어). 새 인스턴스로 갈아 끼우면 노드 요소를 id 로 다시 쓴다.</summary>
    public UnitMapScene Scene { get => (UnitMapScene)GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (UnitMapCanvas)d;
        canvas.SyncNodes();
        canvas.Relayout();
        canvas.RedrawLines();
        canvas.OnSceneChangedDuringDrag();          // 끄는 중 레이어 토글 · 반영 — 끌기는 이어 간다
    }

    public static readonly DependencyProperty SelectedUnitIdProperty = DependencyProperty.Register(
        nameof(SelectedUnitId), typeof(int?), typeof(UnitMapCanvas), new PropertyMetadata(null, OnSelectedUnitIdChanged));

    /// <summary>선택 부대 — 트리와 같은 선택(FR-02). 장면과 따로 흐른다(자주 바뀐다).</summary>
    public int? SelectedUnitId { get => (int?)GetValue(SelectedUnitIdProperty); set => SetValue(SelectedUnitIdProperty, value); }

    private static void OnSelectedUnitIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (UnitMapCanvas)d;
        if (e.OldValue is int oldId && canvas._nodes.TryGetValue(oldId, out var oldNode)) oldNode.IsSelectedNode = false;
        if (e.NewValue is int newId && canvas._nodes.TryGetValue(newId, out var newNode)) newNode.IsSelectedNode = true;
        canvas.RedrawLines();                       // 선택 부대에 닿는 선 굵기 +1(FR-27)
        if (canvas.HasFocusInside()) canvas.RestoreFocus();   // 포커스는 선택을 따른다(ISSUE-52 — UIA 가 고른 노드를 읽는다)
    }

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(
        nameof(Interaction), typeof(IUnitMapInteraction), typeof(UnitMapCanvas), new PropertyMetadata(null, OnInteractionChanged));

    /// <summary>입력을 받을 쪽(관계도 뷰모델). 붙으면 이 캔버스를 표면으로 건넨다.</summary>
    public IUnitMapInteraction? Interaction { get => (IUnitMapInteraction?)GetValue(InteractionProperty); set => SetValue(InteractionProperty, value); }

    private static void OnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (UnitMapCanvas)d;
        (e.OldValue as IUnitMapInteraction)?.AttachSurface(null);
        if (e.OldValue is UnitMapViewModel oldVm) oldVm.FocusRequested -= canvas.OnFocusRequested;
        (e.NewValue as IUnitMapInteraction)?.AttachSurface(canvas);
        // 뷰모델의 포커스 요청(오버레이 · M 모드 · 막대가 닫힘) → 캔버스(고른 노드). 상세 칸 요청은 콘솔 뷰의 몫.
        if (e.NewValue is UnitMapViewModel newVm) newVm.FocusRequested += canvas.OnFocusRequested;
    }

    // 테마 전환 감지용 — 값은 쓰지 않는다(그릴 때 다시 찾는다). 여러 토큰이 한꺼번에 바뀌어도 한 번만 다시 그린다.
    private static readonly DependencyProperty HierarchyTokenProperty = DependencyProperty.Register(
        "HierarchyToken", typeof(object), typeof(UnitMapCanvas), new PropertyMetadata(null, OnThemeTokenChanged));

    private static readonly DependencyProperty AdjacencyTokenProperty = DependencyProperty.Register(
        "AdjacencyToken", typeof(object), typeof(UnitMapCanvas), new PropertyMetadata(null, OnThemeTokenChanged));

    private static readonly DependencyProperty GridTokenProperty = DependencyProperty.Register(
        "GridToken", typeof(object), typeof(UnitMapCanvas), new PropertyMetadata(null, OnThemeTokenChanged));

    private static void OnThemeTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((UnitMapCanvas)d).ScheduleThemeRedraw();
    #endregion

    #region - 읽기(시험 · Phase 3) -
    /// <summary>노드 요소(그림 순서 = 편제 깊이 우선 · 코드 순).</summary>
    public IReadOnlyList<UnitMapNode> Nodes => _ordered;

    /// <summary>정적 층을 다시 그린 횟수(선 층 + 격자) — 팬에서는 오르지 않아야 한다(NFR-02).</summary>
    public int RenderCount => _lineLayer.RenderCount + _gridBuilds;

    /// <summary>선 층.</summary>
    public UnitMapLineLayer LineLayer => _lineLayer;

    /// <summary>지금 팬(화면 오프셋).</summary>
    public Vector PanOffset => _view.Offset;

    /// <summary>지금 뷰포트(배율 · 오프셋).</summary>
    public GraphViewport View => _view;

    /// <summary>원래 자리 잔상 층 · 끌리는 층 · 오버레이 — Phase 3 이 채운다.</summary>
    internal Canvas OriginLayer => _originLayer;
    internal Canvas DragLayer => _dragLayer;
    internal Grid OverlayLayer => _overlay;

    #endregion

    #region - IUnitMapSurface -
    public double Scale => _view.Scale;

    public UnitMapLevel Level => _level;

    public Point CenterWorld
    {
        get
        {
            var size = ViewportSize;
            return _view.ScreenToWorld(new Point(size.Width / 2, size.Height / 2));
        }
    }

    public event EventHandler? ViewChanged;

    public void CenterOn(int unitId, double? scale = null)
    {
        if (DeferUntilSized(() => CenterOn(unitId, scale))) return;
        if (!Scene.Positions.TryGetValue(unitId, out var world)) return;
        var view = scale is double s ? new GraphViewport(GraphViewport.ClampScale(s), _view.Offset) : _view;
        ApplyView(ClearOfOverlays(view.CenterOn(world, ViewportSize)));
        Remember(() => CenterOn(unitId, scale));
    }

    public void SetView(double scale, Point centerWorld)
    {
        if (DeferUntilSized(() => SetView(scale, centerWorld))) return;
        ApplyView(ClearOfOverlays(new GraphViewport(GraphViewport.ClampScale(scale), _view.Offset).CenterOn(centerWorld, ViewportSize)));
        Remember(() => SetView(scale, centerWorld));
    }

    /// <summary>
    /// 가운데 두기(첫 화면 · 저장된 뷰 · 검색 이동) 뒤 — 그림 전체가 오버레이 띠 사이에 들어갈 수 있는 축이면 가장 적게 옮겨 넣는다
    /// (2026-09-28: 내 부대 가운데 50% 첫 화면에서 뿌리 노드가 위 배치 문구 밑). 그림이 더 크면 그대로(가운데 부대가 가운데).
    /// </summary>
    private GraphViewport ClearOfOverlays(GraphViewport view)
        => view.KeepClearOf(Scene.WorldBounds, ViewportSize, OverlayInsets(), GraphViewport.FitPadding,
                            NodeBoxAround(UnitMapLod.Resolve(view.Scale, _level)));

    /// <summary>방금 만든 뷰를 기억한다 — 오버레이 띠가 바뀌었을 때 그대로라면 같은 요청을 한 번 다시 한다(<c>OnOverlayBandChanged</c>).</summary>
    private void Remember(Action replay) => _autoView = (_view, Scene.WorldBounds, OverlayInsets(), replay);

    public void Fit()
    {
        if (DeferUntilSized(Fit)) return;
        var size = ViewportSize;

        var bounds = Scene.WorldBounds;
        var insets = OverlayInsets();
        if (!GraphViewport.TryFit(bounds, size, insets, out var first)) return;

        // 도형은 배율로 커지지 않는다(FR-21) — 맞춘 배율의 단계 도형 크기만큼 한 번 더 맞춘다.
        // 떠 있는 오버레이(위: 배치 문구 · M 표시 / 아래: HUD · 되돌리기 막대)의 띠는 비워 둔다 — 노드가 그 밑에 깔리지 않게.
        var level = UnitMapLod.ForScale(first.Scale);
        var box = NodeBoxAround(level);
        ApplyView(GraphViewport.TryFit(bounds, size, insets, out var fitted, GraphViewport.FitPadding, box) ? fitted : first);
        Remember(Fit);
    }

    /// <summary>
    /// 마지막 자동 뷰(전체 보기 · 가운데 두기)가 만든 뷰 · 그때의 경계 · 비운 띠 · 다시 할 요청. 뷰도 장면도 그대로인데 오버레이 띠만
    /// 바뀌면(배치 문구가 배치 GET 응답 뒤에 나타남) 같은 요청을 한 번 다시 한다 — 사용자가 옮긴 뷰는 건드리지 않는다.
    /// </summary>
    private (GraphViewport View, Rect Bounds, GraphInsets Insets, Action Replay)? _autoView;

    public bool IsInView(int unitId)
    {
        if (!Scene.Positions.TryGetValue(unitId, out var world) || !_nodes.TryGetValue(unitId, out var node)) return false;
        var center = _view.WorldToScreen(world);
        var rect = new Rect(center.X - node.CenterOffset.X, center.Y - node.CenterOffset.Y, node.Width, node.Height);
        return new Rect(ViewportSize).Contains(rect);
    }
    #endregion

    #region - 뷰 조작(Phase 3 입력이 부른다) -
    /// <summary>그림을 화면 단위로 옮긴다 — 변환만 바꾼다(정적 층 재그림 0).</summary>
    public void PanBy(double dx, double dy) => ApplyView(_view.Pan(dx, dy));

    /// <summary><paramref name="cursor"/>(이 캔버스 기준) 아래 월드 점을 고정한 채 배율에 <paramref name="factor"/> 를 곱한다.</summary>
    public void ZoomAt(Point cursor, double factor) => ApplyView(_view.ZoomAt(cursor, factor));

    private void ApplyView(GraphViewport next)
    {
        // 끄는 동안 배율은 바꾸지 않는다(시나리오 ISSUE-12 ⓐ) — 단계가 바뀌면 노드 템플릿이 갈려 끌기가 죽는다.
        if (IsDragging && next.Scale != _view.Scale) return;
        next = ClampPan(next);
        if (next == _view) return;

        var scaleChanged = next.Scale != _view.Scale;
        _view = next;

        if (scaleChanged)
        {
            var level = UnitMapLod.Resolve(_view.Scale, _level);
            if (level != _level) ApplyLevel(level);
            Relayout();
            RedrawLines();
        }

        ApplyPan();
        UpdateHud();
        OnViewMovedDuringDrag();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 팬 범위 제한 자리 — 조직 경계가 화면에 최소 20% 남게(원장 D-2026-09-27-6615ba). 수식은 레인 A 의 <c>GraphViewport</c>
    /// 가 가진다(<see cref="GraphViewport.ClampPan"/>). 크기가 아직 없으면(첫 레이아웃 전) 그대로 통과한다.
    /// </summary>
    private GraphViewport ClampPan(GraphViewport view)
        => ViewportSize.Width > 0 && ViewportSize.Height > 0 ? view.ClampPan(Scene.WorldBounds, ViewportSize) : view;

    /// <summary>크기가 아직 없으면(첫 레이아웃 전) 요청을 미뤄 두고 <c>true</c>. 마지막 요청만 남는다.</summary>
    private bool DeferUntilSized(Action request)
    {
        if (ActualWidth > 0 && ActualHeight > 0) return false;
        _pendingView = request;
        return true;
    }

    private void ApplyPan()
    {
        _pan.X = _view.Offset.X;
        _pan.Y = _view.Offset.Y;
        _gridShift.X = Mod(_view.Offset.X, GRID_SPACING);
        _gridShift.Y = Mod(_view.Offset.Y, GRID_SPACING);
    }

    /// <summary>단계가 바뀌었다 — 노드마다 한 번 템플릿 교체(스타일 트리거) + peer 상태 문자열의 단계.</summary>
    private void ApplyLevel(UnitMapLevel level)
    {
        _level = level;
        foreach (var node in _ordered)
        {
            node.Level = level;
            node.PeerStatus = StatusOf(node);
        }
    }
    #endregion

    #region - 장면 → 노드 -
    private void SyncNodes()
    {
        var scene = Scene;
        _facts.Clear();

        var alive = new HashSet<int>(scene.Tree.Ordered.Select(n => n.Id));
        foreach (var gone in _nodes.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _nodeLayer.Children.Remove(_nodes[gone]);
            _nodes.Remove(gone);
        }

        var ordered = new List<UnitMapNode>(scene.Tree.Count);
        foreach (var unit in scene.Tree.Ordered)
        {
            if (!_nodes.TryGetValue(unit.Id, out var node))
            {
                node = new UnitMapNode { UnitId = unit.Id, Level = _level };
                _nodes[unit.Id] = node;
            }

            var facts = scene.FactsOf(unit.Id);
            _facts[unit.Id] = facts;
            ApplyNode(node, unit, facts, scene.Layers);
            ordered.Add(node);
        }

        // 그림 순서(= 겹침에서 위에 그린 것 — UnitMapHitTest 와 같은 순서)가 달라졌을 때만 자식을 다시 꽂는다.
        if (!ordered.SequenceEqual(_ordered) || _nodeLayer.Children.Count != ordered.Count)
        {
            _nodeLayer.Children.Clear();
            foreach (var node in ordered) _nodeLayer.Children.Add(node);
        }

        _ordered.Clear();
        _ordered.AddRange(ordered);
    }

    /// <summary>그림(히트) 순서 = 트리 순. 뒤에 있을수록 위 — 입력 히트(라우팅)와 끄는 동안의 판정(<see cref="UnitMapHitTest"/>)이 같다.</summary>
    internal IReadOnlyList<UnitMapNode> NodesInDrawOrder() => _ordered;

    private void ApplyNode(UnitMapNode node, UnitTreeNode unit, UnitMapNodeFacts facts, UnitMapLayers layers)
    {
        node.Level = _level;
        node.Echelon = unit.Echelon;
        node.UnitName = unit.Name;
        node.Code = unit.Code;
        node.ShortName = UnitMapText.ShortName(unit.Name);
        node.IsSuspended = !unit.IsEnable;
        node.IsMine = facts.IsMine;
        node.IsMoved = facts.IsMoved;
        node.IsDimmed = facts.IsDimmed;
        node.ErrorCount = facts.ErrorCount;
        node.DeviceCount = facts.DeviceCount;
        node.ShowDeviceBadges = layers.DeviceBadges;
        node.IsSelectedNode = SelectedUnitId == unit.Id;
        node.PeerName = UnitMapText.PeerName(unit);
        node.ToolTip = UnitMapText.Tooltip(unit);
        node.PeerStatus = UnitMapText.ItemStatus(_level, facts.IsMoved, facts.DeviceCount, facts.ErrorCount, !unit.IsEnable);
    }

    private string StatusOf(UnitMapNode node)
    {
        var facts = _facts.TryGetValue(node.UnitId, out var f) ? f : UnitMapNodeFacts.Default(node.UnitId);
        return UnitMapText.ItemStatus(_level, facts.IsMoved, facts.DeviceCount, facts.ErrorCount, node.IsSuspended);
    }

    /// <summary>줌 · 장면 변경 — 노드 자리(월드 × 배율 − 노드 안 부대 점) · L1 이름 폭.</summary>
    private void Relayout()
    {
        var positions = Scene.Positions;
        var scale = _view.Scale;
        var labelWidth = Math.Max(0, UnitMapLayout.SlotWidth * scale - LABEL_MARGIN);

        foreach (var node in _ordered)
        {
            if (!positions.TryGetValue(node.UnitId, out var world))
            {
                node.Visibility = Visibility.Collapsed;
                continue;
            }

            node.Visibility = Visibility.Visible;
            var center = node.CenterOffset;
            Canvas.SetLeft(node, world.X * scale - center.X);
            Canvas.SetTop(node, world.Y * scale - center.Y);
            if (_level == UnitMapLevel.L1 && node.LabelWidth != labelWidth) node.LabelWidth = labelWidth;
        }
    }

    private void RedrawLines()
    {
        var scene = Scene;
        var edges = UnitMapEdges.Build(scene.Tree, scene.Positions, _view.Scale, _level, SelectedUnitId,
                                       UnitMapLayout.SlotWidth, UnitMapLayout.LayerHeight);
        _lineLayer.Redraw(edges, scene.Layers);
    }

    /// <summary>테마 토큰이 바뀌었다 — 한꺼번에 여러 개가 바뀌어도 한 번만 다시 그린다.</summary>
    private void ScheduleThemeRedraw()
    {
        if (_themeRedrawPending) return;
        _themeRedrawPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _themeRedrawPending = false;
            RebuildGrid();
            RedrawLines();
        }));
    }

    /// <summary>점 격자 — 타일 브러시. 팬은 브러시 변환(<see cref="_gridShift"/>)만 옮긴다.</summary>
    private void RebuildGrid()
    {
        var dot = TryFindResource(GRID_TOKEN) as Brush ?? new SolidColorBrush(SystemColors.GrayTextColor);
        _gridLayer.Fill = new DrawingBrush(new GeometryDrawing(dot, null, new EllipseGeometry(new Point(1, 1), 1, 1)))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, GRID_SPACING, GRID_SPACING),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, GRID_SPACING, GRID_SPACING),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = 0.55,
            Transform = _gridShift,
        };
        _gridBuilds++;
    }
    #endregion

    #region - 수명 -
    private Window? _host;

    // 창 비활성화(Alt+Tab 등)도 제스처 끝이다(FR-33) — 캡처 상실이 먼저 오지 않는 경로를 막는다.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_host is not null) _host.Deactivated -= OnHostDeactivated;
        _host = Window.GetWindow(this);
        if (_host is not null) _host.Deactivated += OnHostDeactivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_host is not null) _host.Deactivated -= OnHostDeactivated;
        _host = null;
        FinishGesture(commit: false);
    }

    internal void OnHostDeactivated(object? sender, EventArgs e) => FinishGesture(commit: false);

    /// <summary>
    /// 크기가 처음 생기면 미뤄 둔 뷰 요청을 한다. 그 뒤의 크기 변화는 <b>가운데를 지킨다</b>(배율 유지) — 창을 넓혀도
    /// 보던 부대가 한쪽으로 밀려나지 않는다(must-cover 3).
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0) return;

        if (_pendingView is { } pending)
        {
            _pendingView = null;
            pending();
            return;
        }

        if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0) return;

        // 끄는 중에는 뷰를 옮기지 않는다 — 포인터 아래 월드 점이 그대로라 월드 Δ 가 유지된다(ISSUE-54).
        if (IsDragging) { OnViewMovedDuringDrag(); return; }

        var center = _view.ScreenToWorld(new Point(e.PreviousSize.Width / 2, e.PreviousSize.Height / 2));
        ApplyView(_view.CenterOn(center, e.NewSize));
    }

    private void OnNodeSelectRequested(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not UnitMapNode node) return;
        Interaction?.RequestSelect(node.UnitId);
        e.Handled = true;
    }

    /// <summary>입력 원천에서 위로 올라가 노드를 찾는다(라우팅 조상 — 히트 테스트가 아니다).</summary>
    private UnitMapNode? NodeFrom(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = ParentOf(current))
            if (current is UnitMapNode node) return node;
        return null;
    }

    /// <summary>입력 원천이 오버레이(HUD · 막대 · 확인) 안인가.</summary>
    private bool IsInOverlay(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = ParentOf(current))
            if (ReferenceEquals(current, _overlay)) return true;
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject child)
        => child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer()
        => new UnitMapCanvasAutomationPeer(this, () => _ordered,
                                           () => _overlay.Children.OfType<UIElement>().Where(c => c.Visibility == Visibility.Visible));

    #region - 내부 -
    private Size ViewportSize
    {
        get
        {
            return new Size(ActualWidth, ActualHeight);   // 레이아웃이 준 실제 크기만 — 없으면 요청을 미룬다
        }
    }

    /// <summary>단계 도형의 화면 사각형(부대 점 기준) — 전체 보기 여백 계산용. L0 는 가장 큰 틀(사단).</summary>
    private static Rect NodeBoxAround(UnitMapLevel level)
    {
        var (box, center) = UnitSymbolGeometry.NodeBox(level, level == UnitMapLevel.L0 ? EnumUnitEchelon.Division : EnumUnitEchelon.Company);
        return new Rect(-center.X, -center.Y, box.Width, box.Height);
    }

    private static double Mod(double value, double modulus)
    {
        var r = value % modulus;
        return r < 0 ? r + modulus : r;
    }
    #endregion
}
