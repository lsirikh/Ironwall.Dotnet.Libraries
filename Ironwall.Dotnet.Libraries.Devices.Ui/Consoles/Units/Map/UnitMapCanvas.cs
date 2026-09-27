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
    private bool _pendingFit;

    private GraphGesture? _gesture;
    private UnitMapNode? _pressedNode;
    private IDisposable? _dragSession;

    public UnitMapCanvas()
    {
        Focusable = true;
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

        RebuildGrid();
        ApplyPan();
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
    }

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(
        nameof(Interaction), typeof(IUnitMapInteraction), typeof(UnitMapCanvas), new PropertyMetadata(null, OnInteractionChanged));

    /// <summary>입력을 받을 쪽(관계도 뷰모델). 붙으면 이 캔버스를 표면으로 건넨다.</summary>
    public IUnitMapInteraction? Interaction { get => (IUnitMapInteraction?)GetValue(InteractionProperty); set => SetValue(InteractionProperty, value); }

    private static void OnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (UnitMapCanvas)d;
        (e.OldValue as IUnitMapInteraction)?.AttachSurface(null);
        (e.NewValue as IUnitMapInteraction)?.AttachSurface(canvas);
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

    /// <summary>진행 중인 제스처가 <see cref="DragSession"/> 토큰을 쥐고 있다.</summary>
    internal bool HoldsDragSession => _dragSession is not null;
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
        if (!Scene.Positions.TryGetValue(unitId, out var world)) return;
        var view = scale is double s ? new GraphViewport(GraphViewport.ClampScale(s), _view.Offset) : _view;
        ApplyView(view.CenterOn(world, ViewportSize));
    }

    public void SetView(double scale, Point centerWorld)
        => ApplyView(new GraphViewport(GraphViewport.ClampScale(scale), _view.Offset).CenterOn(centerWorld, ViewportSize));

    public void Fit()
    {
        var size = ViewportSize;
        if (size.Width <= 0 || size.Height <= 0) { _pendingFit = true; return; }
        _pendingFit = false;

        var bounds = Scene.WorldBounds;
        if (!GraphViewport.TryFit(bounds, size, out var first)) return;

        // 도형은 배율로 커지지 않는다(FR-21) — 맞춘 배율의 단계 도형 크기만큼 한 번 더 맞춘다.
        var level = UnitMapLod.ForScale(first.Scale);
        var box = NodeBoxAround(level);
        ApplyView(GraphViewport.TryFit(bounds, size, out var fitted, GraphViewport.FitPadding, box) ? fitted : first);
    }

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
        ViewChanged?.Invoke(this, EventArgs.Empty);
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

    #region - 입력 골격 -
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        OnPointerPressed(e.GetPosition(this), NodeFrom(e.OriginalSource as DependencyObject), Keyboard.IsKeyDown(Key.Space));
        Focus();
        CaptureMouse();                         // 뗌 · 캡처 상실이 반드시 이 캔버스로 돌아오게 — 제스처 토큰이 새지 않는다
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_gesture is null) return;
        OnPointerReleased(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_gesture is not null) FinishGesture();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        // Esc 는 제스처 중일 때만 소비한다 — 아니면 통과(기존 ClearSelectionOnEscBehavior 보존, DF).
        if (e.Key == Key.Escape && _gesture is not null)
        {
            FinishGesture();
            e.Handled = true;
        }
    }

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
        FinishGesture();
    }

    private void OnHostDeactivated(object? sender, EventArgs e) => FinishGesture();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_pendingFit) Fit();
    }

    /// <summary>눌렀다(<paramref name="at"/> 는 이 캔버스 기준). 뗌 없이 다시 눌리면 앞 제스처를 닫고 하나만 쥔다.</summary>
    internal void OnPointerPressed(Point at, UnitMapNode? node, bool spaceHeld)
    {
        if (_gesture is not null) FinishGesture();

        var target = node is null ? GraphPressTarget.Empty : GraphPressTarget.Node;
        _gesture = GraphGesture.Begin(new GraphPress(target, GraphPointerButton.Left, spaceHeld, CanDragNode(node)), at);
        _pressedNode = node;
        _dragSession = DragSession.Begin();
    }

    /// <summary>뗐다. 데드존 안이면 선택 · 선택 해제를 올린다. 넘었으면 팬 · 끌기의 끝(Phase 3).</summary>
    internal void OnPointerReleased(Point at)
    {
        if (_gesture is null) return;

        var kind = _gesture.Release(at);
        var node = _pressedNode;
        FinishGesture();

        switch (kind)
        {
            case GraphGestureKind.Select when node is not null:
                Interaction?.RequestSelect(node.UnitId);
                break;
            case GraphGestureKind.ClearSelection:
                Interaction?.RequestClearSelection();
                break;
        }
    }

    /// <summary>
    /// 제스처 끝 — 뗌 · 캡처 상실 · <c>Esc</c> · 언로드가 모두 여기로. 순서(DF · <c>LabelAdorner</c> 계약):
    /// ① 플래그 해제 ② 시각 복원 ③ 구독 해제 ④ 캡처 해제 ⑤ 통지(<see cref="DragSession"/> 토큰 반납 포함).
    /// 캡처를 먼저 풀면 <c>LostMouseCapture</c> 로 재진입하므로 플래그를 가장 먼저 내린다.
    /// </summary>
    private void FinishGesture()
    {
        var session = _dragSession;
        _gesture = null;                        // ①
        _pressedNode = null;
        _dragSession = null;
        // ② ③ — Phase 3(끌리는 사본 · 잔상 · 자동 팬 타이머)이 여기에 더한다.
        if (IsMouseCaptured) ReleaseMouseCapture();   // ④
        session?.Dispose();                     // ⑤ 한 번만 — 토큰은 두 번째 반납을 무시한다
    }

    private bool CanDragNode(UnitMapNode? node)
        => node is not null && _level != UnitMapLevel.L0 && node.Echelon is not null;   // L0 · 모르는 제대는 끌지 않는다(FR-13 · 부모 FR-19)

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

    private static DependencyObject? ParentOf(DependencyObject child)
        => child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer()
        => new UnitMapCanvasAutomationPeer(this, () => _ordered, () => _overlay.Children.OfType<UIElement>());

    #region - 내부 -
    private Size ViewportSize
    {
        get
        {
            var width = ActualWidth > 0 ? ActualWidth : double.IsNaN(Width) ? 0 : Width;
            var height = ActualHeight > 0 ? ActualHeight : double.IsNaN(Height) ? 0 : Height;
            return new Size(width, height);
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
