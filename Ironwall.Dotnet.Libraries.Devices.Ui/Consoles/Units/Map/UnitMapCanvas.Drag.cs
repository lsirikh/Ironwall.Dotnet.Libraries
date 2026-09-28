using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 캔버스 — 팬 · 노드 캡처 드래그 · 자동 팬 · 대상 표시 · 끌리는 사본 · 취소 (IMPL-22 · IMPL-39 · FR-17 · FR-28~30 · FR-33)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 끌기 기계. <b>캡처 드래그</b>(누름 → 데드존 뒤 진행 → 뗌 · 캡처 상실 · 비활성 · Esc · 언로드 → 단일 <see cref="FinishGesture"/>) —
/// OLE 끌어 놓기 없음, 전역 커서 덮어쓰기 없음(로컬 <c>Cursor</c> + <c>ClearValue</c> — DF).
/// </summary>
/// <remarks>
/// <para><b>좌표</b>: 포인터는 움직이지 않는 이 캔버스(루트) 기준으로 받고(VER-01), 이동량은 <b>누른 순간의 월드 점과 지금 포인터의
/// 월드 점(지금 뷰포트로 환산)</b>의 차이다 — 자동 팬이 뷰를 옮겨도 놓은 자리와 저장 자리가 어긋나지 않는다(시나리오 ISSUE-11).</para>
/// <para><b>판정</b>: 포인터 아래 노드는 <see cref="UnitMapHitTest"/>(월드 격자 색인, 끌리는 부대와 예하는 뺀다)로, 포인터가 캔버스 위인지는
/// 커널 <see cref="DragHitTest.Top"/> 으로 본다 — 원시 시각 트리 히트 테스트는 셸의 숨은 덮개(<c>PART_OverlayBox</c>)를
/// 집는다(4584f130). 캔버스 밖에서 놓으면 <b>취소</b>(원장 D-2026-09-27-6615ba).</para>
/// <para><b>대상 표시</b>(FR-30): 끌기 시작 때 <b>한 번</b> 모든 노드를 판정해 <c>DropState</c> 를 칠하고, 머문 노드가 바뀔 때만 강조 · 칩을 바꾼다.
/// 끌리는 층(사본 · 칩 · 미리보기 선)만 ≤ 30Hz 로 움직이고 정적 층은 다시 그리지 않는다(NFR-04).</para>
/// <para><b>오버레이</b>: 끄는 동안 HUD · 막대 · 문구는 입력에 투명하다 — 아래 32 DIU 자동 팬 띠 위에 앉아 있어도 놓기를 가로채지 않는다(must-cover 1).</para>
/// <para><see cref="DragSession"/> 토큰은 누른 순간부터 끝날 때까지 <b>하나</b> — 부대 콘솔은 그동안 편제 재조회를 미룬다.</para>
/// </remarks>
public partial class UnitMapCanvas
{
    /// <summary>캔버스 밖 칩 — 놓으면 취소된다.</summary>
    public const string OUTSIDE_CHIP_TEXT = "캔버스 밖 — 놓으면 취소됩니다";

    /// <summary>끌리는 층 갱신 간격(≤ 30Hz — NFR-04).</summary>
    public static readonly TimeSpan DragFrameInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>자동 팬 틱.</summary>
    public static readonly TimeSpan AutoPanTickInterval = TimeSpan.FromMilliseconds(16);

    private GraphGesture? _gesture;
    private GraphPointerButton _gestureButton;
    private UnitMapNode? _pressedNode;
    private IDisposable? _dragSession;
    private Point _lastPoint;

    private bool _dragging;
    private int _dragUnitId;
    private Point _dragUnitWorld;
    private Point _dragStartWorld;
    private Vector _dragDelta;
    private UnitMapHitTest? _hitIndex;
    private IReadOnlySet<int>? _exclusion;
    private Dictionary<int, UnitMapDropDecision>? _decisions;
    private UnitMapDropDecision? _emptyDecision;
    private int? _hoverId;
    private bool _pointerOutside;          // 캔버스 밖 — 자동 팬 멈춤 · 놓으면 취소
    private bool _overOverlay;             // HUD · 막대 · 문구 위 — 자동 팬은 계속(띠가 가려지지 않게) · 놓으면 취소
    private bool _ctrlHeld;
    private DateTime _lastFrame;

    private Grid? _ghost;
    private Border? _subtreeChip;
    private Border? _hoverChip;
    private ConsoleText? _hoverChipText;
    private Border? _hoverChipBlockedMark;
    private Path? _preview;

    private DispatcherTimer? _autoPanTimer;
    private DateTime _autoPanLast;

    #region - 상태(시험 · 뷰모델) -
    /// <summary>누름부터 뗌까지(데드존 안 포함).</summary>
    internal bool IsGestureActive => _gesture is not null;

    /// <summary>노드 끌기가 데드존을 넘어 진행 중이다(<c>_pressed</c> 와 다르다).</summary>
    public bool IsDragging => _dragging;

    /// <summary>진행 중인 제스처가 <see cref="DragSession"/> 토큰을 쥐고 있다.</summary>
    internal bool HoldsDragSession => _dragSession is not null;

    /// <summary>지금 포인터 아래 부대(끄는 중).</summary>
    internal int? HoverUnitId => _hoverId;

    /// <summary>지금 포인터가 캔버스 밖이다(끄는 중).</summary>
    internal bool IsPointerOutside => _pointerOutside;

    /// <summary>지금 포인터가 오버레이(HUD · 막대 · 문구) 위다(끄는 중) — 놓으면 취소.</summary>
    internal bool IsPointerOverOverlay => _overOverlay;

    /// <summary>지금 이동량(월드 단위).</summary>
    internal Vector DragDelta => _dragDelta;

    internal string? HoverChipText => _hoverChip?.Visibility == Visibility.Visible ? _hoverChipText?.Text : null;
    internal bool HoverChipIsBlocked => _hoverChipBlockedMark?.Visibility == Visibility.Visible;
    internal string? SubtreeChipText => ((_subtreeChip?.Child as Panel)?.Children.OfType<ConsoleText>().FirstOrDefault())?.Text;
    internal bool IsPreviewVisible => _preview?.Visibility == Visibility.Visible && _preview.Data is not null;
    internal bool IsPreviewDotted => _preview?.StrokeDashArray is { Count: > 0 };
    internal FrameworkElement? Ghost => _ghost;
    /// <summary>마지막으로 끈 부대(확인 오버레이가 닫히면 포커스가 돌아간다 — SIM-K157 · K163).</summary>
    internal int? LastDraggedUnitId { get; private set; }

    /// <summary>끝냄 순서 기록기 — ①flags ②visuals ③unsubscribe ④capture ⑤notify(시험).</summary>
    internal Action<string>? FinishTrace { get; set; }
    #endregion

    #region - 누름 · 이동 · 뗌 -
    /// <summary>(Phase 2 서명) 왼쪽 누름.</summary>
    internal void OnPointerPressed(Point at, UnitMapNode? node, bool spaceHeld)
        => OnPointerPressed(at, node, GraphPointerButton.Left, spaceHeld);

    /// <summary>눌렀다(<paramref name="at"/> 는 이 캔버스 기준). 뗌 없이 다시 눌리면 앞 제스처를 닫고(취소) 하나만 쥔다.</summary>
    internal void OnPointerPressed(Point at, UnitMapNode? node, GraphPointerButton button, bool spaceHeld)
    {
        if (_gesture is not null) FinishGesture(commit: false);
        if (IsConfirmOpen) return;

        var target = node is null ? GraphPressTarget.Empty : GraphPressTarget.Node;
        var canDrag = node is not null && UnitMapDropClassifier.CanDrag(Scene.Tree, node.UnitId, _level);
        _gesture = GraphGesture.Begin(new GraphPress(target, button, spaceHeld, canDrag), at);
        _gestureButton = button;
        _pressedNode = node;
        _lastPoint = at;
        _dragSession = DragSession.Begin();
    }

    /// <summary>움직였다. 팬이면 그만큼 옮기고, 노드 끌기면 데드존을 넘은 순간 시작한다.</summary>
    internal void OnPointerMoved(Point at)
    {
        if (_gesture is null) return;

        switch (_gesture.Move(at))
        {
            case GraphGestureKind.Pan:
                var dx = at.X - _lastPoint.X;
                var dy = at.Y - _lastPoint.Y;
                _lastPoint = at;
                PanBy(dx, dy);
                return;
            case GraphGestureKind.NodeDrag:
                if (!_dragging) BeginNodeDrag();
                UpdateDrag(at);
                return;
            default:
                return;                      // 데드존 안 — 누른 점을 지킨다(팬은 누른 곳부터 따라온다)
        }
    }

    /// <summary>뗐다. 데드존 안이면 선택 · 선택 해제, 끄는 중이면 놓기(캔버스 밖이면 취소).</summary>
    internal void OnPointerReleased(Point at)
    {
        if (_gesture is null) return;

        var kind = _gesture.Release(at);
        var node = _pressedNode;

        if (_dragging)
        {
            UpdateDrag(at, force: true);
            FinishGesture(commit: true);
            return;
        }

        FinishGesture(commit: false);
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
    #endregion

    #region - 끌기 시작 · 진행 -
    private void BeginNodeDrag()
    {
        var node = _pressedNode!;
        _dragging = true;
        _dragUnitId = node.UnitId;
        _dragUnitWorld = Scene.Positions.TryGetValue(node.UnitId, out var world) ? world : default;
        _dragStartWorld = _view.ScreenToWorld(_gesture!.Origin);
        _dragDelta = default;
        _hoverId = null;
        _pointerOutside = false;
        _overOverlay = false;
        _ctrlHeld = false;

        RebuildHitIndex();
        PaintCandidates();
        CreateGhost(node);

        _overlay.IsHitTestVisible = false;                  // must-cover 1 — 오버레이는 끄는 동안 입력에 투명
        Cursor = Cursors.SizeAll;                           // 로컬 커서(전역 덮어쓰기 금지 — 해제 경로가 빈약해 남는다)
        _lastFrame = DateTime.MinValue;

        Interaction?.BeginDrag(_dragUnitId);
        StartAutoPan();
    }

    /// <summary>포인터 아래 판정 색인 — 그림(z) 순서대로, 나중 것이 위(선택 · 마지막 옮긴 부대가 위 — must-cover 2).</summary>
    private void RebuildHitIndex()
    {
        var positions = Scene.Positions;
        _exclusion = UnitMapHitTest.DragExclusion(Scene.Tree, _dragUnitId);
        _hitIndex = UnitMapHitTest.Build(
            NodesInDrawOrder().Where(n => n.Visibility == Visibility.Visible && positions.ContainsKey(n.UnitId))
                              .Select(n => new UnitMapHitItem(n.UnitId, positions[n.UnitId], n.Echelon)),
            _level, _view.Scale);
    }

    /// <summary>후보 칠하기 — 끌기 시작 때 <b>한 번</b>(판정은 뷰모델의 순수 판정기).</summary>
    private void PaintCandidates()
    {
        _decisions = new Dictionary<int, UnitMapDropDecision>();
        _emptyDecision = Interaction?.Classify(_dragUnitId, null, false) ?? UnitMapDropDecision.Position();
        foreach (var node in _ordered)
        {
            if (_exclusion!.Contains(node.UnitId))
            {
                node.DropState = UnitMapNodeDropState.Origin;       // 원래 자리 편제 35%
                continue;
            }

            var decision = Interaction?.Classify(_dragUnitId, node.UnitId, false) ?? UnitMapDropDecision.Blocked(string.Empty, node.UnitId);
            _decisions[node.UnitId] = decision;
            node.DropState = StateOf(decision);
        }
    }

    private static UnitMapNodeDropState StateOf(UnitMapDropDecision decision) => decision.Kind switch
    {
        UnitMapDropKind.Reparent => UnitMapNodeDropState.ParentCandidate,
        UnitMapDropKind.Adjoin => UnitMapNodeDropState.AdjoinCandidate,
        UnitMapDropKind.Blocked => UnitMapNodeDropState.Blocked,
        _ => UnitMapNodeDropState.None,
    };

    /// <summary>포인터가 <paramref name="at"/> 에 있다 — 이동량 · 머묾 · 사본 · 자동 팬 속도.</summary>
    private void UpdateDrag(Point at, bool force = false)
    {
        if (!_dragging) return;

        _lastPoint = at;
        var zone = ZoneAt(at);
        var outside = zone == PointerZone.Outside;
        var overOverlay = zone == PointerZone.Overlay;
        var world = _view.ScreenToWorld(at);
        _dragDelta = world - _dragStartWorld;           // 월드 점의 차 — 자동 팬 몫이 들어간다(ISSUE-11)

        var ctrl = ReadModifiers().HasFlag(ModifierKeys.Control);
        int? hover = zone != PointerZone.Content || ctrl ? null : _hitIndex?.HitTest(world, _exclusion);
        var hoverChanged = hover != _hoverId || outside != _pointerOutside || overOverlay != _overOverlay || ctrl != _ctrlHeld;
        if (hoverChanged) SetHover(hover, outside, overOverlay, ctrl);

        var now = Clock.UtcNow;
        if (force || hoverChanged || now - _lastFrame >= DragFrameInterval)
        {
            _lastFrame = now;
            PositionDragLayer();
        }
    }

    /// <summary>뷰가 움직였다(자동 팬 · 크기 변화) — 같은 포인터라도 월드 점이 바뀌므로 다시 잰다.</summary>
    private void OnViewMovedDuringDrag()
    {
        if (_dragging) UpdateDrag(_lastPoint, force: true);
    }

    /// <summary>장면이 바뀌었다(레이어 토글 · 끄는 중 반영) — 끌기를 유지하고 판정 색인만 다시. 끌던 부대가 사라지면 취소.</summary>
    private void OnSceneChangedDuringDrag()
    {
        if (!_dragging) return;
        if (!Scene.Positions.ContainsKey(_dragUnitId)) { FinishGesture(commit: false); return; }
        RebuildHitIndex();
        UpdateDrag(_lastPoint, force: true);
    }

    private void SetHover(int? hover, bool outside, bool overOverlay, bool ctrl)
    {
        if (_hoverId is int old && _nodes.TryGetValue(old, out var oldNode) && _decisions!.TryGetValue(old, out var oldDecision))
            oldNode.DropState = StateOf(oldDecision);

        _hoverId = hover;
        _pointerOutside = outside;
        _overOverlay = overOverlay;
        _ctrlHeld = ctrl;

        if (hover is int id && _nodes.TryGetValue(id, out var node)) node.DropState = UnitMapNodeDropState.Hover;

        // 칩 — 할 일을 말한다(FR-30). 캔버스 밖이면 "놓으면 취소".
        string? text;
        var blocked = false;
        if (outside || overOverlay)
        {
            text = OUTSIDE_CHIP_TEXT;
            blocked = true;
        }
        else
        {
            var decision = hover is int h && _decisions!.TryGetValue(h, out var d) ? d : _emptyDecision;
            text = decision is null ? null : UnitMapText.DropChip(decision, Scene.Tree);
            blocked = decision?.Kind == UnitMapDropKind.Blocked;
        }

        _hoverChipText!.Text = text ?? string.Empty;
        _hoverChip!.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        _hoverChipBlockedMark!.Visibility = blocked ? Visibility.Visible : Visibility.Collapsed;
        ApplyChipStyle(_hoverChip, _hoverChipText, blocked);
    }

    private enum PointerZone { Content, Overlay, Outside }

    /// <summary>
    /// 포인터가 어디인가 — 캔버스 콘텐츠 · 오버레이 위 · 캔버스 밖(시나리오 ISSUE-13 — 오버레이 위와 밖은 놓으면 취소).
    /// 캔버스 위인지는 커널 <see cref="DragHitTest.Top"/> 으로(숨은 · 입력 투명 요소를 거른다 — 끄는 동안 오버레이는 투명이라
    /// 그 아래 자동 팬 띠가 막히지 않는다), 오버레이 위인지는 보이는 오버레이 틀의 사각형으로 가른다.
    /// </summary>
    private PointerZone ZoneAt(Point at)
    {
        if (at.X < 0 || at.Y < 0 || at.X > ActualWidth || at.Y > ActualHeight) return PointerZone.Outside;

        if (PresentationSource.FromVisual(this)?.RootVisual is UIElement root
            && DragHitTest.Top(root, TranslatePoint(at, root)) is { } top)
        {
            var mine = false;
            for (var current = top; current is not null; current = ParentOf(current))
                if (ReferenceEquals(current, this)) { mine = true; break; }
            if (!mine) return PointerZone.Outside;             // 캔버스 위에 떠 있는 다른 요소(팝업 등)
        }

        foreach (var box in OverlayBoxes())
        {
            if (box.Visibility != Visibility.Visible || box.ActualWidth <= 0) continue;
            var bounds = box.TransformToAncestor(this).TransformBounds(new Rect(box.RenderSize));
            if (bounds.Contains(at)) return PointerZone.Overlay;
        }
        return PointerZone.Content;
    }
    #endregion

    #region - 끌리는 층(사본 · 칩 · 미리보기 선) -
    private void CreateGhost(UnitMapNode source)
    {
        // 끌리는 사본 — 부대 하나(예하 사본 없음 — R-15) + 2px Primary 윤곽 + 그림자(이 요소 하나만 — NFR-05).
        var copy = new UnitMapNode
        {
            UnitId = 0,
            Level = source.Level,
            Echelon = source.Echelon,
            UnitName = source.UnitName,
            ShortName = source.ShortName,
            Code = source.Code,
            IsSuspended = source.IsSuspended,
            IsMine = source.IsMine,
            IsMoved = source.IsMoved,
            ErrorCount = source.ErrorCount,
            DeviceCount = source.DeviceCount,
            ShowDeviceBadges = source.ShowDeviceBadges,
            LabelWidth = source.LabelWidth,
            IsHitTestVisible = false,
        };
        AutomationProperties_Clear(copy);

        var outline = new Rectangle { Margin = new Thickness(-2), RadiusX = 6, RadiusY = 6, StrokeThickness = 2, IsHitTestVisible = false };
        outline.SetResourceReference(Shape.StrokeProperty, "PrimaryBrush");

        _ghost = new Grid { IsHitTestVisible = false };
        _ghost.Children.Add(copy);
        _ghost.Children.Add(outline);
        _ghost.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 4, Direction = 270, Opacity = 0.28, Color = ShadowColor() };

        var descendants = Scene.Tree.DescendantIds(_dragUnitId).Count;
        _subtreeChip = descendants > 0 ? Chip(UnitMapText.SubtreeChip(descendants)!, out _, out _) : null;
        _hoverChip = Chip(string.Empty, out _hoverChipText, out _hoverChipBlockedMark);
        _hoverChip.Visibility = Visibility.Collapsed;

        _preview = new Path { StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _preview.SetResourceReference(Shape.StrokeProperty, "PrimaryBrush");

        _dragLayer.Children.Add(_preview);
        _dragLayer.Children.Add(_ghost);
        if (_subtreeChip is not null) _dragLayer.Children.Add(_subtreeChip);
        _dragLayer.Children.Add(_hoverChip);
    }

    // 사본은 자동화 트리에 나오지 않는다(끌리는 층은 캔버스 peer 자식이 아니다) — 원본과 같은 id 를 달지 않는다.
    private static void AutomationProperties_Clear(UnitMapNode copy) => copy.ClearValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty);

    /// <summary>끌리는 층 위치 — 사본 · 예하 칩 · 머묾 칩 · 미리보기 선(월드 × 배율 좌표 — 팬은 컨테이너가).</summary>
    private void PositionDragLayer()
    {
        if (_ghost is null) return;
        var scale = _view.Scale;
        var copy = (UnitMapNode)_ghost.Children[0];
        var ghostWorld = _dragUnitWorld + _dragDelta;
        var ghostScreen = new Point(ghostWorld.X * scale, ghostWorld.Y * scale);
        Canvas.SetLeft(_ghost, ghostScreen.X - copy.CenterOffset.X);
        Canvas.SetTop(_ghost, ghostScreen.Y - copy.CenterOffset.Y);

        var belowGhost = ghostScreen.Y - copy.CenterOffset.Y + copy.Height + 8;
        if (_subtreeChip is not null)
        {
            Canvas.SetLeft(_subtreeChip, ghostScreen.X - copy.CenterOffset.X);
            Canvas.SetTop(_subtreeChip, belowGhost);
            belowGhost += 30;
        }

        // 머묾 칩 — 머문 노드 위(없으면 사본 아래).
        if (_hoverId is int hover && Scene.Positions.TryGetValue(hover, out var hoverWorld) && _nodes.TryGetValue(hover, out var hoverNode))
        {
            Canvas.SetLeft(_hoverChip!, hoverWorld.X * scale - hoverNode.CenterOffset.X);
            Canvas.SetTop(_hoverChip!, hoverWorld.Y * scale - hoverNode.CenterOffset.Y - 32);
        }
        else
        {
            Canvas.SetLeft(_hoverChip!, ghostScreen.X - copy.CenterOffset.X);
            Canvas.SetTop(_hoverChip!, belowGhost);
        }

        UpdatePreview(ghostScreen);
    }

    /// <summary>
    /// 미리보기 선 — 상위 후보면 꺾은 실선(대상 아래 → 사본 위), 인접 후보면 둥근 점선 호. 레이어 토글과 무관하게 그린다
    /// (인접선 레이어를 숨겨도 인접 판정 · 미리보기는 산다 — must-cover 4).
    /// </summary>
    private void UpdatePreview(Point ghostScreen)
    {
        if (_preview is null) return;
        var decision = _hoverId is int id && _decisions!.TryGetValue(id, out var d) ? d : null;
        if (decision is null || _pointerOutside || decision.Kind is not (UnitMapDropKind.Reparent or UnitMapDropKind.Adjoin)
            || !Scene.Positions.TryGetValue(decision.TargetId ?? -1, out var targetWorld))
        {
            _preview.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = _view.Scale;
        var target = new Point(targetWorld.X * scale, targetWorld.Y * scale);
        var targetUnit = Scene.Tree.Find(decision.TargetId!.Value);
        var movingUnit = Scene.Tree.Find(_dragUnitId);
        var ta = UnitMapEdges.AnchorOf(_level, targetUnit?.Echelon);
        var ga = UnitMapEdges.AnchorOf(_level, movingUnit?.Echelon);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (decision.Kind == UnitMapDropKind.Reparent)
            {
                // 계층선과 같은 경로(대상 아래 가운데 → 사본 위 가운데) — 사본이 대상보다 위 · 옆이면 두 노드 사이로 돈다.
                var route = UnitMapEdges.Route(target, ta, ghostScreen, ga);
                ctx.BeginFigure(route[0], false, false);
                for (var i = 1; i < route.Count; i++) ctx.LineTo(route[i], true, false);
            }
            else
            {
                var chord = target - ghostScreen;
                var normal = chord.Length > 0 ? new Vector(-chord.Y, chord.X) : new Vector(0, -1);
                normal.Normalize();
                if (normal.Y > 0) normal = -normal;
                var mid = new Point((ghostScreen.X + target.X) / 2, (ghostScreen.Y + target.Y) / 2);
                ctx.BeginFigure(ghostScreen, false, false);
                ctx.QuadraticBezierTo(mid + normal * UnitMapEdges.AdjacencyBow(chord.Length), target, true, true);
            }
        }

        _preview.Data = geometry;
        if (decision.Kind == UnitMapDropKind.Adjoin)
        {
            _preview.StrokeDashArray = new DoubleCollection { 0, 2 };
            _preview.StrokeDashCap = PenLineCap.Round;
            _preview.StrokeThickness = UnitMapLineLayer.ADJACENCY_THICKNESS;
        }
        else
        {
            _preview.StrokeDashArray = null;
            _preview.StrokeThickness = 2;
        }
        _preview.Visibility = Visibility.Visible;
    }

    /// <summary>칩 — 뜻은 Primary 알약, 막힘은 중립 바탕 + 왼쪽 3px 세로 막대(색이 아니라 형태 — NFR-06).</summary>
    private Border Chip(string text, out ConsoleText label, out Border blockedMark)
    {
        label = new ConsoleText { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        blockedMark = new Border { Width = 3, Margin = new Thickness(-10, -4, 7, -4), Visibility = Visibility.Collapsed };
        blockedMark.SetResourceReference(Border.BackgroundProperty, "StatusCriticalBrush");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(blockedMark);
        row.Children.Add(label);
        var chip = new Border { Child = row, Padding = new Thickness(10, 4, 10, 4), IsHitTestVisible = false, BorderThickness = new Thickness(1) };
        if (text.Length > 0) ApplyChipStyle(chip, label, blocked: false);
        return chip;
    }

    private static void ApplyChipStyle(Border chip, ConsoleText label, bool blocked)
    {
        chip.CornerRadius = new CornerRadius(blocked ? 4 : 12);
        chip.SetResourceReference(Border.BackgroundProperty, blocked ? "SurfaceSunkenBrush" : "PrimaryBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, blocked ? "RowLineBrush" : "PrimaryBrush");
        label.SetResourceReference(TextBlock.ForegroundProperty, blocked ? "TextPrimaryBrush" : "OnPrimaryBrush");
    }

    /// <summary>그림자 색 — 매번 토큰에서(테마 전환에 고착되지 않게).</summary>
    private Color ShadowColor()
        => (TryFindResource("ScrimModalBrush") as SolidColorBrush)?.Color is Color c ? Color.FromRgb(c.R, c.G, c.B) : Colors.Black;
    #endregion

    #region - 자동 팬(FR-17 — px/sec, 시간 기반) -
    private void StartAutoPan()
    {
        _autoPanLast = Clock.UtcNow;
        _autoPanTimer ??= new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = AutoPanTickInterval };
        _autoPanTimer.Tick -= OnAutoPanTick;
        _autoPanTimer.Tick += OnAutoPanTick;
        _autoPanTimer.Start();
    }

    private void OnAutoPanTick(object? sender, EventArgs e)
    {
        var now = Clock.UtcNow;
        var elapsed = now - _autoPanLast;
        _autoPanLast = now;
        AutoPanTick(elapsed);
    }

    /// <summary>
    /// 가장자리 32 DIU 띠 안이면 <paramref name="elapsed"/> 동안 <see cref="DragMath.AutoScrollVelocity"/>(최대 480 px/s)만큼 팬.
    /// 캔버스 밖이면 멈춘다(밖에서 놓으면 취소 — 멀리 밀어 둘 까닭이 없다).
    /// </summary>
    internal void AutoPanTick(TimeSpan elapsed)
    {
        if (!_dragging || _pointerOutside || elapsed <= TimeSpan.Zero) return;
        var vx = ActualWidth > 0 ? DragMath.AutoScrollVelocity(_lastPoint.X, ActualWidth, AutoPanBand(ActualWidth)) : 0;
        var vy = ActualHeight > 0 ? DragMath.AutoScrollVelocity(_lastPoint.Y, ActualHeight, AutoPanBand(ActualHeight)) : 0;
        if (vx == 0 && vy == 0) return;
        var seconds = elapsed.TotalSeconds;
        PanBy(-vx * seconds, -vy * seconds);          // 아래 띠 = 아래를 보러 간다 → 그림은 위로
    }
    #endregion

    #region - 끝(단일 출구) -
    /// <summary>
    /// 제스처 끝 — 뗌 · 캡처 상실 · <c>Esc</c> · 창 비활성화 · 언로드 · 확인 오버레이 뜸이 모두 여기로(LabelAdorner 계약).
    /// ① 플래그 해제 ② 시각 복원 ③ 구독 해제 ④ 캡처 해제 ⑤ 통지(<see cref="DragSession"/> 반납 · 놓기/취소).
    /// 캡처를 먼저 풀면 <c>LostMouseCapture</c> 로 재진입하므로 플래그를 가장 먼저 내린다.
    /// <paramref name="commit"/> 이어도 포인터가 캔버스 밖이면 취소한다.
    /// </summary>
    internal void FinishGesture(bool commit)
    {
        var wasDragging = _dragging;
        var unitId = _dragUnitId;
        var droppedOutside = wasDragging && commit && (_pointerOutside || _overOverlay);
        var session = _dragSession;
        UnitMapDropRequest? request = wasDragging && commit && !_pointerOutside && !_overOverlay
            ? new UnitMapDropRequest(unitId, _dragDelta.X, _dragDelta.Y, _hoverId ?? (_ctrlHeld ? _hitIndex?.HitTest(_view.ScreenToWorld(_lastPoint), _exclusion) : null), _ctrlHeld)
            : null;

        // ① 플래그
        _gesture = null;
        _pressedNode = null;
        _dragging = false;
        _dragSession = null;
        FinishTrace?.Invoke("flags");

        // ② 시각 복원 — 즉시 원위치(애니메이션 없음)
        if (wasDragging)
        {
            foreach (var node in _ordered) node.DropState = UnitMapNodeDropState.None;
            _dragLayer.Children.Clear();
            _ghost = null;
            _subtreeChip = null;
            _hoverChip = null;
            _hoverChipText = null;
            _hoverChipBlockedMark = null;
            _preview = null;
            _overlay.IsHitTestVisible = true;
            ClearValue(CursorProperty);
        }
        FinishTrace?.Invoke("visuals");

        // ③ 구독 해제
        _autoPanTimer?.Stop();
        _hitIndex = null;
        _exclusion = null;
        _decisions = null;
        _emptyDecision = null;
        _hoverId = null;
        _pointerOutside = false;
        _overOverlay = false;
        FinishTrace?.Invoke("unsubscribe");

        // ④ 캡처
        if (IsMouseCaptured) ReleaseMouseCapture();
        FinishTrace?.Invoke("capture");

        // ⑤ 통지
        session?.Dispose();
        if (wasDragging)
        {
            LastDraggedUnitId = unitId;
            if (request is not null)
            {
                Interaction?.CompleteDrag(request);
            }
            else
            {
                Interaction?.CancelDrag(unitId);
                if (droppedOutside) ShowLocalNotice(DROP_OUTSIDE_CANCELLED);   // FR-29 v1.3 ② — 원위치 · 서버 0 · 알림
            }
        }
        FinishTrace?.Invoke("notify");
    }
    #endregion
}
