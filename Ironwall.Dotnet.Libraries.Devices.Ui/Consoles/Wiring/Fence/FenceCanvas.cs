using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 펜스 형상 뷰 캔버스(wiring-fence-view F-3 · fence-wiring-editor FR-02 ~ FR-09 · FR-12). 지도(GMap)와 무관한 별도 컨트롤이다.
/// </summary>
/// <remarks>
/// <para><b>겹</b>(아래 → 위): 바탕 · [세계: 정적 층(땅 · 망 모양 5종 · 기둥 · 케이블 보기) · 망 칩 층(망 한 칸 = <see cref="FenceChip"/> Panel) ·
/// 칩 층(센서 · 묶음 · 함체)] · 덧그림 어도너(목표 막대 · 알약 · 러버밴드) · 고스트 어도너.
/// 세계는 <see cref="GraphViewport"/> 변환 하나로 줌 · 팬한다.</para>
/// <para><b>입력</b>(<see cref="FenceGesture"/> 표 그대로): 터널 <c>PreviewMouseDown</c> 에서 누름을 받아 <b>캔버스가</b> 캡처한다 — 데드존(8 DIU) 안이면 클릭,
/// 넘으면 — 왼쪽: 센서를 잡았으면 옮기기 · 빈 곳 · 망이면 센서 사각형 · Shift 면 망 사각형 / 오른쪽 · 가운데: 화면 이동.
/// 오른쪽을 데드존 안에서 떼면 메뉴. 끝나는 길(뗌 · 캡처 상실 · Esc)은 <see cref="FinishDrag"/> 하나다.
/// 끄는 동안 다시 그리는 것은 <b>목표 칸이 바뀔 때만</b>이다(NFR-02 · 러버밴드는 포인터를 따라간다).</para>
/// <para>판단은 하지 않는다 — 체인 · 자리 편집 · 선택은 <see cref="WiringViewModel"/> 이 한다.</para>
/// </remarks>
public sealed class FenceCanvas : Grid, IFenceDropSurface
{
    public const string AUTOMATION_ID = "Devices.Wiring.Fence.Canvas";

    /// <summary>화살표 팬 한 번(DIU).</summary>
    public const double PAN_STEP = 50;

    /// <summary>전체 보기 여백(DIU).</summary>
    public const double FIT_PADDING = 18;

    private readonly Canvas _world = new() { ClipToBounds = false };
    private readonly MatrixTransform _transform = new();
    private readonly FenceStaticLayer _static = new();
    private readonly Canvas _panels = new() { ClipToBounds = false };
    private readonly Canvas _chips = new() { ClipToBounds = false };
    private readonly Dictionary<int, FenceChip> _sensorChips = new();
    private readonly Dictionary<int, FenceChip> _groupChips = new();
    private readonly Dictionary<int, FenceChip> _panelChips = new();
    private FenceChip? _controllerChip;
    private FenceOverlayAdorner? _overlay;

    private FenceWorld? _scene;
    private FenceProjector _projector = FenceProjector.Tilt;
    private GraphViewport _view = GraphViewport.Identity;
    private bool _autoFit = true;
    private bool _grouped;

    // 끌기 — 누름(_press)과 끌기(_dragging)를 가른다(데드존 미만은 클릭)
    private Press? _press;
    private bool _dragging;
    private FenceGestureAction _action;
    private DragGhostAdorner? _ghost;
    private double? _insertionX;
    private string? _insertionLabel;
    private DropKind _dropKind;
    private double _enclosureX;
    private Point _lastPointer;
    private bool _arming;
    private IReadOnlyList<int> _dragKeys = Array.Empty<int>();
    private double _renderZoom = 1;
    private Rect? _band;

    /// <summary>글자 최소 크기를 다시 맞출 줌 변화(비율) — 이만큼 넘게 바뀔 때만 정적 층 · 칩을 다시 그린다(팬 · 작은 줌은 변환만).</summary>
    public const double REDRAW_ZOOM_STEP = 0.04;
    private int _enclosureGap;
    private readonly DispatcherTimer _paletteHover;

    private sealed record Press(FencePointerButton Button, FenceTargetKind Target, FenceChip? Chip, Point Start, GraphViewport View, bool Ctrl, bool Shift);

    private enum DropKind { None, Chain, Remove }

    /// <summary>
    /// 캔버스 자신의 UIA peer — <c>Grid</c>(패널)는 peer 가 없어 <c>Devices.Wiring.Fence.Canvas</c> 가 트리에 나오지 않고 칩만 보였다
    /// (헤디드 r16 SC-FEN-001). 창 아래 Pane 으로 서고, 자식(칩 peer)은 시각 트리에서 그대로 모은다.
    /// </summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new FenceCanvasAutomationPeer(this);

    public FenceCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        SetResourceReference(BackgroundProperty, "SurfaceBrush");
        AutomationProperties.SetAutomationId(this, AUTOMATION_ID);
        AutomationProperties.SetName(this, "펜스 형상 뷰 — 화살표 키로 화면 이동 · Shift+F10 메뉴");
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        // Tab 은 센서 칩 먼저, 망은 한 번에 들어가 화살표로 옮긴다(망 수백 칸을 Tab 으로 지나지 않게).
        KeyboardNavigation.SetTabIndex(_chips, 0);
        KeyboardNavigation.SetTabIndex(_panels, 1);
        KeyboardNavigation.SetTabNavigation(_panels, KeyboardNavigationMode.Once);

        _world.RenderTransform = _transform;
        _world.Children.Add(_static);
        _world.Children.Add(_panels);
        _world.Children.Add(_chips);
        Children.Add(_world);

        // 테마 전환 신호 — 토큰 참조가 바뀌면 전부 다시 그린다(그릴 때마다 TryFindResource 로 푼다).
        SetResourceReference(ThemeProbeProperty, "PrimaryBrush");
        SetResourceReference(ThemeProbe2Property, "SurfaceSunkenBrush");

        // 팔레트 → 펜스(커널 드롭존) — 포인터 아래 자리를 준다.
        DropZone.SetKey(this, WiringViewModel.FenceZoneKey);
        DropZone.SetData(this, this);
        _paletteHover = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(40) };
        _paletteHover.Tick += (_, _) => UpdatePaletteHover();

        Loaded += (_, _) => { EnsureOverlay(); Rebuild(); };
        Unloaded += (_, _) => { _paletteHover.Stop(); if (_press is not null) FinishDrag(false); };
        SizeChanged += (_, _) => { if (_autoFit) Fit(); else ApplyView(); };
    }

    #region - Dependency properties -
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WiringViewModel), typeof(FenceCanvas), new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>결선 창 뷰모델 — 체인 · 자리 · 선택 · 편집의 주인.</summary>
    public WiringViewModel? ViewModel
    {
        get => (WiringViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (FenceCanvas)d;
        if (e.OldValue is WiringViewModel old) old.FenceChanged -= canvas.OnFenceChanged;
        if (e.NewValue is WiringViewModel now) now.FenceChanged += canvas.OnFenceChanged;
        canvas._autoFit = true;
        canvas.Rebuild();
    }

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(object), typeof(FenceCanvas), new PropertyMetadata(null, OnTheme));
    private static readonly DependencyProperty ThemeProbe2Property = DependencyProperty.Register(
        "ThemeProbe2", typeof(object), typeof(FenceCanvas), new PropertyMetadata(null, OnTheme));

    private static void OnTheme(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FenceCanvas)d).RedrawAll();

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != DropZone.StateProperty) return;
        if ((DropZoneState)e.NewValue == DropZoneState.Hover) _paletteHover.Start();
        else
        {
            _paletteHover.Stop();
            if (_press is null && _insertionX is not null) { _insertionX = null; _insertionLabel = null; UpdateOverlay(); }
        }
    }
    #endregion

    #region - Test · diagnostics -
    internal FenceWorld? Scene => _scene;
    internal double Scale => _view.Scale;
    internal Vector Offset => _view.Offset;
    internal bool IsGrouped => _grouped;
    internal bool IsDragging => _dragging;
    internal FenceProjector Projector => _projector;
    internal IReadOnlyDictionary<int, FenceChip> SensorChips => _sensorChips;
    internal IReadOnlyDictionary<int, FenceChip> GroupChips => _groupChips;
    internal IReadOnlyDictionary<int, FenceChip> PanelChips => _panelChips;
    internal FenceChip? ControllerChip => _controllerChip;
    internal int StaticRenderCount => _static.RenderCount;
    internal int OverlayRenderCount => _overlay?.RenderCount ?? 0;
    internal IReadOnlyList<FenceShape> OverlayShapes => _overlay?.Shapes ?? Array.Empty<FenceShape>();
    internal IReadOnlyList<FenceShape> OverlayScreenShapes => _overlay?.ScreenShapes ?? Array.Empty<FenceShape>();
    internal double? InsertionX => _insertionX;
    internal IReadOnlyList<FenceShape> StaticShapes => _static.Shapes;

    /// <summary>시험 — 메뉴를 띄우지 않고 항목만 남긴다(화면 밖 창에서 팝업이 포커스를 뺏지 않게).</summary>
    internal bool SuppressMenuPopup { get; set; }

    /// <summary>마지막으로 연 메뉴의 항목.</summary>
    internal IReadOnlyList<FenceMenuEntry>? LastMenu { get; private set; }

    /// <summary>덧그림을 새로 얹은 횟수 — 끄는 동안은 후보 자리가 바뀔 때만 는다(NFR-02).</summary>
    internal int OverlayUpdates { get; private set; }

    /// <summary>마지막 다시 세우기에 걸린 시간(ms).</summary>
    internal double LastRebuildMs { get; private set; }

    /// <summary>화면(캔버스) 점 → 세계 점.</summary>
    internal Point ScreenToWorld(Point screen) => _view.ScreenToWorld(screen);

    /// <summary>세계 점 → 화면 점.</summary>
    internal Point WorldToScreen(Point world) => _view.WorldToScreen(world);

    /// <summary>칩의 화면 중심(시험 · 자동화 좌표).</summary>
    internal Point ScreenCenterOf(FenceChip chip)
    {
        return WorldToScreen(new Point(Canvas.GetLeft(chip) + chip.Width / 2, Canvas.GetTop(chip) + chip.Height / 2));
    }
    #endregion

    #region - Build -
    private void OnFenceChanged(object? sender, EventArgs e)
    {
        if (_dragging) return;            // 끄는 동안은 그림을 흔들지 않는다 — 끝나면 다시 세운다
        Rebuild();
    }

    /// <summary>뷰모델에서 장면을 다시 세운다(체인 · 자리 · 선택 · 보기 방식).</summary>
    internal void Rebuild()
    {
        var vm = ViewModel;
        if (vm is null) return;
        var watch = Stopwatch.StartNew();

        var focusedChip = FocusedChip();
        var focusedKey = focusedChip is { Kind: FenceChipKind.Sensor or FenceChipKind.Group } && focusedChip.Keys.FirstOrDefault() is var fk && fk != 0 ? fk : (int?)null;
        var focusedPanel = focusedChip is { Kind: FenceChipKind.Panel } ? focusedChip.Key : (int?)null;
        var controllerFocused = focusedChip is { Kind: FenceChipKind.Controller };

        var flatChanged = _projector.K != (vm.IsFlat ? 0 : 1);
        _projector = vm.IsFlat ? FenceProjector.Flat : FenceProjector.Tilt;
        _scene = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing);
        _enclosureX = _scene.ControllerX;
        _enclosureGap = _scene.Chain.ControllerGap;
        _grouped = _scene.ShouldGroup(_view.Scale);

        DrawStatic();
        SyncPanels();
        SyncChips();

        if (_autoFit && (flatChanged || ActualWidth > 0)) Fit();
        else ApplyView();

        if (focusedKey is { } key) FocusUnitOf(key);
        else if (focusedPanel is { } panel && _panelChips.TryGetValue(panel, out var panelChip)) FocusQuietly(panelChip);
        else if (controllerFocused) _controllerChip?.Focus();

        UpdateOverlay();
        LastRebuildMs = watch.Elapsed.TotalMilliseconds;
    }

    /// <summary>케이블 층(리턴케이블 · 함체 · A/B 번호)을 그리는가 — 펜스 구성 모드에서는 [케이블 보기]를 켰을 때만(FR-12).</summary>
    private bool CablesShown => _scene is not { IsLayout: true } || ViewModel?.ShowCables == true;

    private void DrawStatic()
    {
        if (_scene is null || ViewModel is null) return;
        _renderZoom = _view.Scale;
        var showRange = ViewModel.ShowRange && ViewModel.HasRangeSensors;
        var shapes = _scene.IsLayout
            ? FenceScene.StaticLayout(_scene, _projector, showRange, ViewModel.ShowCables, _enclosureX, _enclosureGap, _view.Scale)
            : FenceScene.Static(_scene, _projector, showRange, _enclosureX, _enclosureGap, _view.Scale);
        var clip = new StreamGeometry();
        var ground = FenceScene.GroundPolygon(_scene, _projector, _enclosureX);
        using (var ctx = clip.Open())
        {
            ctx.BeginFigure(ground[0], true, true);
            ctx.PolyLineTo(ground.Skip(1).ToList(), true, true);
        }
        _static.Show(shapes, clip);
    }

    /// <summary>망 칩 — 망 한 칸마다 하나(peer 있는 <see cref="FenceChip"/> · FR-04). 그림은 선택 표시와 적중 사각형뿐이고 모양은 정적 층이 그린다.</summary>
    private void SyncPanels()
    {
        if (_scene is null || ViewModel is not { } vm) return;
        var geometry = _scene.Geometry;
        var count = geometry?.Panels.Count ?? 0;
        foreach (var key in _panelChips.Keys.Where(k => k >= count).ToList())
        {
            _panels.Children.Remove(_panelChips[key]);
            _panelChips.Remove(key);
        }
        if (geometry is null) return;

        foreach (var panel in geometry.Panels)
        {
            if (!_panelChips.TryGetValue(panel.Index, out var chip))
            {
                chip = new FenceChip(FenceChipKind.Panel, panel.Index, Array.Empty<int>());
                chip.GotFocus += OnChipFocused;
                _panelChips[panel.Index] = chip;
                _panels.Children.Add(chip);
            }
            var selected = vm.IsPanelSelected(panel.Index);
            chip.Picture = FenceScene.PanelChip(panel, _scene, _projector, selected);
            Place(chip, panel.StartM * _scene.Upm);
            AutomationProperties.SetName(chip, $"망 {panel.Index + 1}, {FencePanelSpec.StyleText(panel.Spec.Style)}, 거리 {panel.SpanM:0.##}m, 높이 {panel.Spec.HeightM:0.##}m");
            AutomationProperties.SetItemStatus(chip, selected ? "선택됨" : string.Empty);
        }
        // 자식 순서 = 망 순서(화살표 · 러버밴드가 같은 차례를 본다)
        for (var i = 0; i < geometry.Panels.Count; i++)
        {
            var chip = _panelChips[i];
            var at = _panels.Children.IndexOf(chip);
            if (at == i) continue;
            _panels.Children.RemoveAt(at);
            _panels.Children.Insert(Math.Min(i, _panels.Children.Count), chip);
        }
    }

    private void SyncChips()
    {
        if (_scene is null || ViewModel is null) return;
        var vm = ViewModel;
        var scene = _scene;
        var units = scene.Units(_grouped);
        var liveSensors = new HashSet<int>();
        var liveGroups = new HashSet<int>();
        var shape = scene.Shape;
        var order = new List<FenceChip>(units.Count);

        foreach (var unit in units)
        {
            FenceChip chip;
            if (unit.IsController)
            {
                chip = _controllerChip ??= NewChip(FenceChipKind.Controller, FenceWorld.CONTROLLER_KEY, Array.Empty<int>());
                chip.Picture = FenceScene.Controller(shape, _projector, vm.IsControllerSelected, _view.Scale);
                Place(chip, scene.ControllerX);
                AutomationProperties.SetName(chip, shape == WiringShape.TwoBranch ? "PIDS 제어기 — 위치 고정" : "지중 제어기 — 한 줄의 시작, 위치 고정");
            }
            else if (unit.IsGroup)
            {
                liveGroups.Add(unit.Key);
                if (!_groupChips.TryGetValue(unit.Key, out chip!))
                    _groupChips[unit.Key] = chip = NewChip(FenceChipKind.Group, unit.Key, unit.Keys);
                chip.Keys = unit.Keys;
                var members = unit.Keys.Select(k => scene.Sensors[k]).ToList();
                var selected = unit.Keys.Any(vm.IsFenceSelected);
                chip.Picture = FenceScene.Group(members, shape, _projector, selected, _view.Scale);
                Place(chip, scene.UnitX(unit));
                AutomationProperties.SetName(chip, $"펜스센서 묶음 {members.Count}대, {members[0].Big(shape)}부터 {members[^1].Big(shape)}까지. 확대하면 풀립니다");
            }
            else
            {
                liveSensors.Add(unit.Key);
                if (!_sensorChips.TryGetValue(unit.Key, out chip!))
                    _sensorChips[unit.Key] = chip = NewChip(FenceChipKind.Sensor, unit.Key, unit.Keys);
                var s = scene.Sensors[unit.Key];
                chip.Picture = FenceScene.Sensor(s, shape, _projector, vm.IsFenceSelected(unit.Key), _view.Scale, scene.LiftOf(unit.Key));
                Place(chip, scene.X[unit.Key]);
                var port = s.PortText.Length > 0 ? $", {s.PortText}" : string.Empty;
                // 뒤를 보는 기둥 센서(FR-20)는 칩의 "뒤" 표지와 같은 말을 이름에도 — 그림 표지는 UIA 로 읽을 수 없다.
                var facing = s.IsBackFacing ? ", 뒤(펜스 내부)" : string.Empty;
                var mount = vm.FenceLayout.MountOf(unit.Key) is { } m ? $", {(m.IsPostSpot ? "기둥" : "망")} {m.Panel + 1} {SensorMountSpec.SpotText(m.Spot)}" : string.Empty;
                AutomationProperties.SetName(chip, $"{s.Name}, 장비번호 {s.Number}, {s.Big(shape)}{port}{facing}{mount}");
                AutomationProperties.SetItemStatus(chip, s.HasFacing ? (s.IsBackFacing ? "방향 뒤" : "방향 앞") : string.Empty);
            }
            order.Add(chip);
        }

        // 링의 함체는 단위 목록에 없다 — 케이블 보기일 때만 따로 세운다(끌어서 옮긴다 · 표시만).
        if (shape == WiringShape.Ring && CablesShown)
        {
            var chip = _controllerChip ??= NewChip(FenceChipKind.Controller, FenceWorld.CONTROLLER_KEY, Array.Empty<int>());
            chip.Picture = FenceScene.Controller(shape, _projector, vm.IsControllerSelected, _view.Scale);
            Place(chip, _enclosureX);
            AutomationProperties.SetName(chip, $"함체 — {vm.EnclosureGapText}. Alt+왼쪽/오른쪽 화살표로 옮깁니다(표시만)");
            order.Insert(0, chip);
        }
        else if (shape == WiringShape.Ring && _controllerChip is not null)
        {
            _chips.Children.Remove(_controllerChip);
            _controllerChip = null;
        }

        foreach (var key in _sensorChips.Keys.Where(k => !liveSensors.Contains(k)).ToList())
        {
            _chips.Children.Remove(_sensorChips[key]);
            _sensorChips.Remove(key);
        }
        foreach (var key in _groupChips.Keys.Where(k => !liveGroups.Contains(k)).ToList())
        {
            _chips.Children.Remove(_groupChips[key]);
            _groupChips.Remove(key);
        }

        // 자식 순서 = 화면 순서 — Tab 이 왼쪽에서 오른쪽으로 간다(FR-11).
        for (var i = 0; i < order.Count; i++)
        {
            var at = _chips.Children.IndexOf(order[i]);
            if (at == i) continue;
            if (at >= 0) _chips.Children.RemoveAt(at);
            _chips.Children.Insert(Math.Min(i, _chips.Children.Count), order[i]);
        }
        while (_chips.Children.Count > order.Count) _chips.Children.RemoveAt(_chips.Children.Count - 1);
    }

    /// <summary>
    /// 칩을 세계 x 에 둔다 — 요소의 배치 사각형 = 그림의 적중 사각형(UIA BoundingRectangle · 좌표 클릭이 실제 모양과 맞게).
    /// 그림은 칩 원점(앵커) 기준이라 칩이 <see cref="FenceChip.HitBounds"/> 만큼 옮겨 그린다.
    /// </summary>
    private static void Place(FenceChip chip, double x)
    {
        var hit = chip.HitBounds;
        if (hit.IsEmpty) { Canvas.SetLeft(chip, x); Canvas.SetTop(chip, 0); return; }
        chip.Width = hit.Width;
        chip.Height = hit.Height;
        Canvas.SetLeft(chip, x + hit.X);
        Canvas.SetTop(chip, hit.Y);
    }

    private FenceChip NewChip(FenceChipKind kind, int key, IReadOnlyList<int> keys)
    {
        var chip = new FenceChip(kind, key, keys);
        chip.GotFocus += OnChipFocused;
        _chips.Children.Add(chip);
        return chip;
    }

    private void OnChipFocused(object sender, RoutedEventArgs e)
    {
        if (_press is not null || _arming || sender is not FenceChip chip || ViewModel is not { } vm) return;
        switch (chip.Kind)
        {
            case FenceChipKind.Controller:
                if (!vm.IsControllerSelected) vm.FenceSelectController();
                break;
            case FenceChipKind.Panel:
                if (!vm.IsPanelSelected(chip.Key) || vm.FencePaneKind != FenceSelectionKind.Panels) vm.FenceSelectPanel(chip.Key);
                break;
            default:
                if (vm.FenceSelectedKey is not { } k || !chip.Keys.Contains(k)) vm.FenceSelect(chip.Keys[0]);
                break;
        }
        EnsureVisible(chip);
    }

    /// <summary>지금 포커스를 쥔 칩 — 키보드 포커스, 없으면(비활성 창) 논리 포커스.</summary>
    internal FenceChip? FocusedChip()
        => Keyboard.FocusedElement as FenceChip
           ?? FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) as FenceChip;

    private void RedrawAll()
    {
        _static.InvalidateVisual();
        foreach (var chip in _chips.Children.OfType<FenceChip>().Concat(_panels.Children.OfType<FenceChip>())) chip.InvalidateVisual();
        _overlay?.InvalidateVisual();
    }
    #endregion

    #region - View (FR-06) -
    /// <summary>전체 보기 — 그림 경계를 캔버스에 맞춘다. 이후 크기가 바뀌어도 맞춘다(사람이 줌 · 팬하기 전까지).</summary>
    public void Fit()
    {
        _autoFit = true;
        if (_scene is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        if (GraphViewport.TryFit(_scene.FitBounds(_projector), new Size(ActualWidth, ActualHeight), out var fitted, FIT_PADDING))
            _view = fitted;
        ApplyView();
    }

    /// <summary>커서(캔버스 좌표) 아래를 고정한 채 배율에 <paramref name="factor"/> 를 곱한다.</summary>
    public void ZoomAt(Point cursor, double factor)
    {
        _autoFit = false;
        _view = _view.ZoomAt(cursor, factor);
        ApplyView();
    }

    public void ZoomIn() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), GraphViewport.WheelStep);
    public void ZoomOut() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1 / GraphViewport.WheelStep);

    /// <summary>화면 단위로 그림을 옮긴다.</summary>
    public void PanBy(double dx, double dy)
    {
        _autoFit = false;
        _view = _view.Pan(dx, dy);
        ApplyView();
    }

    /// <summary>배율(시험 · 도구줄 %) — 한계 안으로 자른다.</summary>
    internal void SetView(double scale, Vector offset)
    {
        _autoFit = false;
        _view = new GraphViewport(GraphViewport.ClampScale(scale), offset);
        ApplyView();
    }

    /// <summary>배율 · 오프셋이 바뀌었다(도구줄 % 글자).</summary>
    public event EventHandler? ViewChanged;

    private void ApplyView()
    {
        _transform.Matrix = new Matrix(_view.Scale, 0, 0, _view.Scale, _view.Offset.X, _view.Offset.Y);
        if (_scene is not null)
        {
            var grouped = _scene.ShouldGroup(_view.Scale);
            var rescale = Math.Abs(Math.Log(_view.Scale / _renderZoom)) > REDRAW_ZOOM_STEP;
            if (rescale) DrawStatic();
            if (grouped != _grouped || rescale)
            {
                var changed = grouped != _grouped;
                _grouped = grouped;
                SyncChips();
                if (changed && ViewModel is { } vm)
                    vm.NotifyFenceStatus(grouped ? "축소 — 펜스센서를 묶음으로 접었습니다(확대하면 풀립니다)" : "확대 — 펜스센서 묶음을 풀었습니다");
            }
        }
        UpdateOverlay();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureVisible(FenceChip chip)
    {
        if (ActualWidth <= 0) return;
        var x = ScreenCenterOf(chip).X;
        if (x < 60) PanBy(120 - x, 0);
        else if (x > ActualWidth - 60) PanBy(ActualWidth - 120 - x, 0);
    }
    #endregion

    #region - Overlay -
    private void EnsureOverlay()
    {
        if (_overlay is not null) return;
        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null) return;
        _overlay = new FenceOverlayAdorner(this);
        layer.Add(_overlay);
    }

    private void UpdateOverlay()
    {
        EnsureOverlay();
        if (_overlay is null || _scene is null || ViewModel is null) return;
        var hidden = new HashSet<int>(_grouped ? _groupChips.Values.SelectMany(c => c.Keys) : Enumerable.Empty<int>());
        var named = _dragging ? null : ViewModel.FenceNamedKey;
        var shapes = FenceScene.Overlay(_scene, _projector, hidden, named, _insertionX, _insertionLabel, _view.Scale);
        var screen = _band is { } band ? new[] { FenceScene.RubberBand(band) } : Array.Empty<FenceShape>();
        OverlayUpdates++;
        _overlay.Show(shapes, _transform.Matrix, screen);
    }
    #endregion

    #region - Drop surface (palette → fence) -
    /// <inheritdoc/>
    public (int Line, int Index)? PointerTarget()
    {
        if (_scene is null) return null;
        var p = Mouse.GetPosition(this);
        if (p.X < 0 || p.Y < 0 || p.X > ActualWidth || p.Y > ActualHeight) return null;
        return _scene.DropAt(ScreenToWorld(p).X);
    }

    /// <inheritdoc/>
    public double? PointerMetres()
    {
        if (_scene is not { IsLayout: true }) return null;
        var p = Mouse.GetPosition(this);
        if (p.X < 0 || p.Y < 0 || p.X > ActualWidth || p.Y > ActualHeight) return null;
        return ScreenToWorld(p).X / _scene.Upm;
    }

    private void UpdatePaletteHover()
    {
        if (_scene is null || _press is not null) return;
        var p = Mouse.GetPosition(this);
        var wx = ScreenToWorld(p).X;
        var x = _scene.InsertionX(wx, Array.Empty<int>());
        if (_insertionX == x) return;
        _insertionX = x;
        _insertionLabel = null;
        UpdateOverlay();
    }
    #endregion

    #region - Mouse (FR-04 ~ FR-06) -
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (_press is not null || ViewModel is null || ButtonOf(e.ChangedButton) is not { } button) return;
        // 선점 — Thumb · 목록의 기본 처리보다 먼저 받아 캔버스가 캡처한다(drag-first-ux · Preview 선점).
        e.Handled = true;
        var modifiers = Keyboard.Modifiers;
        OnPointerPressed(e.GetPosition(this), ChipFrom(e.OriginalSource as DependencyObject),
            (modifiers & ModifierKeys.Control) != 0, (modifiers & ModifierKeys.Shift) != 0, button);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_arming) return;
        if (_press is null)
        {
            var hover = ChipFrom(e.OriginalSource as DependencyObject);
            ViewModel?.FenceHover(hover is { Kind: FenceChipKind.Sensor } ? hover.Key : null);
            return;
        }
        OnPointerMoved(e.GetPosition(this));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_press is null || ButtonOf(e.ChangedButton) != _press.Button) return;
        e.Handled = true;
        _lastPointer = e.GetPosition(this);
        FinishDrag(true);
    }

    private static FencePointerButton? ButtonOf(MouseButton button) => button switch
    {
        MouseButton.Left => FencePointerButton.Left,
        MouseButton.Right => FencePointerButton.Right,
        MouseButton.Middle => FencePointerButton.Middle,
        _ => null,
    };

    private static FenceTargetKind TargetOf(FenceChip? chip) => chip?.Kind switch
    {
        FenceChipKind.Sensor or FenceChipKind.Group => FenceTargetKind.Sensor,
        FenceChipKind.Panel => FenceTargetKind.Panel,
        FenceChipKind.Controller => FenceTargetKind.Enclosure,
        _ => FenceTargetKind.Empty,
    };

    /// <summary>누름(캔버스 좌표) — 시험이 이 길로 부른다. 칩이 없으면 빈 곳.</summary>
    internal void OnPointerPressed(Point at, FenceChip? chip, bool ctrl = false, bool shift = false, FencePointerButton button = FencePointerButton.Left)
    {
        if (_press is not null || ViewModel is null) return;

        // 포커스 · 캡처를 먼저 — CaptureMouse 는 그 자리에서 합성 MouseMove(실제 커서 위치)를 올린다.
        // 누름을 먼저 세우면 그 이동이 데드존을 넘은 것으로 읽혀 클릭이 끌기로 바뀐다.
        _arming = true;
        try
        {
            if (button == FencePointerButton.Left && chip is not null) chip.Focus(); else Focus();
            CaptureMouse();
        }
        finally { _arming = false; }

        _press = new Press(button, TargetOf(chip), chip, at, _view, ctrl, shift);
        _dragging = false;
        _action = FenceGestureAction.None;
        _lastPointer = at;
    }

    /// <summary>움직임(캔버스 좌표) — 데드존을 넘으면 끌기를 시작한다.</summary>
    internal void OnPointerMoved(Point now)
    {
        if (_press is null || ViewModel is null || _scene is null) return;
        _lastPointer = now;
        if (!_dragging)
        {
            if (!FenceGesture.IsDrag(_press.Start, now)) return;
            BeginDrag();
        }
        MoveDrag(now);
    }

    /// <summary>뗌(캔버스 좌표) — 확정.</summary>
    internal void OnPointerReleased(Point at)
    {
        if (_press is null) return;
        _lastPointer = at;
        FinishDrag(true);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_press is not null) FinishDrag(false);          // 캡처 상실 = 취소
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.GetPosition(this), e.Delta > 0 ? GraphViewport.WheelStep : 1 / GraphViewport.WheelStep);
        e.Handled = true;
    }

    private void BeginDrag()
    {
        if (_press is null || _scene is null) return;
        _dragging = true;
        _action = FenceGesture.Classify(_press.Button, _press.Ctrl, _press.Shift, _press.Target, isDrag: true);
        if (_action == FenceGestureAction.MoveEnclosure && _scene.Shape != WiringShape.Ring) _action = FenceGestureAction.Pan;   // 가지 · 한 줄 제어기는 고정

        switch (_action)
        {
            case FenceGestureAction.MoveSensors when _press.Chip is { } chip:
                _dragKeys = chip.Kind == FenceChipKind.Group ? chip.Keys.ToList() : ViewModel?.FenceDragKeys(chip.Key) ?? chip.Keys;
                foreach (var dim in _chips.Children.OfType<FenceChip>().Where(c => c.Keys.Any(_dragKeys.Contains))) dim.Opacity = 0.3;
                var layer = AdornerLayer.GetAdornerLayer(this);
                var label = chip.Kind == FenceChipKind.Group
                    ? $"펜스센서 ×{chip.Keys.Count}"
                    : _dragKeys.Count > 1 ? $"센서 {_dragKeys.Count}대"
                    : _scene.Sensors.TryGetValue(chip.Key, out var s) ? $"{s.Number} {s.Name}" : "센서";
                _ghost = new DragGhostAdorner(this, layer, label, _dragKeys.Count);
                layer?.Add(_ghost);
                break;
            case FenceGestureAction.MoveEnclosure:
                Cursor = Cursors.SizeWE;
                break;
            case FenceGestureAction.RubberSensors:
            case FenceGestureAction.RubberPanels:
                Cursor = Cursors.Cross;
                break;
            default:
                Cursor = Cursors.ScrollAll;
                break;
        }
    }

    private void MoveDrag(Point now)
    {
        if (_press is null || _scene is null || ViewModel is not { } vm) return;
        switch (_action)
        {
            case FenceGestureAction.Pan:
                _autoFit = false;
                _view = _press.View.Pan(now.X - _press.Start.X, now.Y - _press.Start.Y);
                ApplyView();
                return;

            case FenceGestureAction.RubberSensors:
            case FenceGestureAction.RubberPanels:
                _band = FenceRubberBand.FromPoints(_press.Start, now);
                UpdateOverlay();
                return;

            case FenceGestureAction.MoveEnclosure:
            {
                var wx = ScreenToWorld(now).X;
                var n = _scene.Chain.Count;
                _enclosureX = Math.Max(_scene.GapMid(0), Math.Min(_scene.GapMid(n), wx));
                if (_controllerChip is not null) Place(_controllerChip, _enclosureX);
                var gap = _scene.NearestGap(_enclosureX);
                if (gap != _enclosureGap)
                {
                    _enclosureGap = gap;
                    DrawStatic();                          // 리턴케이블 · VBUS 는 후보 틈이 바뀔 때만 다시 그린다
                    vm.NotifyFenceStatus($"함체 — {GapText(gap, n)} (놓으면 확정 · Esc 취소 · 체인 순서 · A/B 번호는 그대로)");
                }
                return;
            }

            case FenceGestureAction.MoveSensors when _press.Chip is { } chip:
            {
                _ghost?.MoveTo(now);
                var inside = now.X >= 0 && now.Y >= 0 && now.X <= ActualWidth && now.Y <= ActualHeight;
                if (inside)
                {
                    _dropKind = DropKind.Chain;
                    var wx = ScreenToWorld(now).X;
                    double x;
                    string label;
                    if (_scene.IsLayout)
                    {
                        // 펜스 구성: 목표 = 잡은 센서 자리 종류(기둥 · 망)의 가장 가까운 칸(FR-05)
                        var grabbed = GrabbedKey(chip);
                        var metres = wx / _scene.Upm;
                        x = vm.FenceMoveTargetMetres(grabbed, metres) * _scene.Upm;
                        if (_insertionX == x) return;
                        label = vm.FenceMoveLabel(_dragKeys, grabbed, metres);
                    }
                    else
                    {
                        x = _scene.InsertionX(wx, _dragKeys.ToList());
                        if (_insertionX == x) return;
                        var (line, index) = _scene.DropAt(wx);
                        label = vm.FenceDropLabel(_dragKeys, line, index);
                    }
                    _insertionX = x;
                    _insertionLabel = label;
                    UpdateOverlay();
                }
                else
                {
                    _dropKind = IsRemoveZoneUnder(now) ? DropKind.Remove : DropKind.None;
                    if (_insertionX is not null) { _insertionX = null; _insertionLabel = null; UpdateOverlay(); }
                }
                return;
            }
        }
    }

    private static int GrabbedKey(FenceChip chip) => chip.Kind == FenceChipKind.Group ? chip.Keys[0] : chip.Key;

    /// <summary>
    /// 끝내기는 이것 하나 — 뗌(<paramref name="commit"/>) · 캡처 상실 · Esc(취소). 순서: ① 표지 해제 ② 모습 복원 ③ (구독 없음) ④ 캡처 해제 ⑤ 커밋 통지.
    /// </summary>
    internal void FinishDrag(bool commit)
    {
        var press = _press;
        if (press is null) return;
        var wasDragging = _dragging;
        var action = wasDragging ? _action : FenceGesture.Classify(press.Button, press.Ctrl, press.Shift, press.Target, isDrag: false);
        var drop = _dropKind;
        var pointer = _lastPointer;
        var pointerWorldX = ScreenToWorld(pointer).X;
        var enclosureGap = _enclosureGap;
        var band = _band;

        // ① 표지
        _press = null;
        _dragging = false;
        _action = FenceGestureAction.None;
        _dropKind = DropKind.None;
        var dragKeys = _dragKeys;
        _dragKeys = Array.Empty<int>();
        _band = null;
        // ② 모습
        foreach (var dim in _chips.Children.OfType<FenceChip>()) dim.Opacity = 1;
        if (_ghost is not null) { AdornerLayer.GetAdornerLayer(this)?.Remove(_ghost); _ghost = null; }
        _insertionX = null;
        _insertionLabel = null;
        ClearValue(CursorProperty);
        // ④ 캡처
        if (IsMouseCaptured) ReleaseMouseCapture();

        // ⑤ 통지
        var vm = ViewModel;
        if (vm is null || _scene is null) { UpdateOverlay(); return; }

        if (!commit)
        {
            if (wasDragging && action is FenceGestureAction.MoveSensors) vm.NotifyFenceStatus("취소 — 제자리로 돌렸습니다(서버 호출 없음)");
            else if (wasDragging && action is FenceGestureAction.MoveEnclosure) vm.NotifyFenceStatus("취소 — 함체를 잡기 전 자리로 돌렸습니다");
            else if (wasDragging && action is FenceGestureAction.RubberSensors or FenceGestureAction.RubberPanels) vm.NotifyFenceStatus("취소 — 선택을 바꾸지 않았습니다");
            Rebuild();
            return;
        }

        var chip = press.Chip;
        switch (action)
        {
            case FenceGestureAction.SelectOne when chip is { Kind: FenceChipKind.Controller }:
                vm.FenceSelectController();
                break;
            case FenceGestureAction.SelectOne when chip is { Kind: FenceChipKind.Panel }:
                vm.FenceSelectPanel(chip.Key);
                FocusQuietly(chip);
                break;
            case FenceGestureAction.SelectOne when chip is not null:
                vm.FenceSelect(chip.Keys[0]);
                chip.Focus();
                break;
            case FenceGestureAction.ToggleOne when chip is { Kind: FenceChipKind.Panel }:
                vm.FenceTogglePanel(chip.Key);
                FocusQuietly(chip);
                break;
            case FenceGestureAction.ToggleOne when chip is { Kind: FenceChipKind.Sensor }:     // FR-05 Ctrl 클릭
                vm.FenceToggleSelect(chip.Key);
                chip.Focus();
                break;
            case FenceGestureAction.ToggleOne when chip is { Kind: FenceChipKind.Group }:
                foreach (var key in chip.Keys) vm.FenceToggleSelect(key);
                break;
            case FenceGestureAction.ClearSelection:
                vm.FenceClearSelection();
                Focus();
                break;
            case FenceGestureAction.RubberSensors when band is { } sensorBand:
                vm.FenceSelectSensors(SensorHits(sensorBand), additive: press.Ctrl);
                Focus();
                break;
            case FenceGestureAction.RubberPanels when band is { } panelBand:
                vm.FenceSelectPanels(PanelHits(panelBand), additive: press.Ctrl);
                Focus();
                break;
            case FenceGestureAction.ContextMenu:
                OpenMenu(press.Target, chip, pointer);
                break;
            case FenceGestureAction.MoveEnclosure:
                if (!vm.FenceMoveEnclosure(enclosureGap)) Rebuild();
                break;
            case FenceGestureAction.MoveSensors when chip is not null:
            {
                var keys = dragKeys.Count > 0 ? dragKeys.ToList() : chip.Keys.ToList();
                if (drop == DropKind.Chain)
                {
                    bool moved;
                    if (_scene.IsLayout) moved = vm.FenceMoveSensors(keys, GrabbedKey(chip), pointerWorldX / _scene.Upm);
                    else
                    {
                        var (line, index) = _scene.DropAt(pointerWorldX);
                        moved = vm.FencePlace(keys, line, index);
                    }
                    if (moved && keys.Count == 1) vm.FenceSelect(keys[0]);
                    if (!moved) Rebuild();
                    FocusUnitOf(keys[0]);
                }
                else if (drop == DropKind.Remove) vm.FenceUnplace(keys);
                else
                {
                    vm.NotifyFenceStatus("놓을 곳이 아니어서 제자리로 돌렸습니다");
                    Rebuild();
                }
                break;
            }
            default:
                UpdateOverlay();
                break;
        }
    }

    /// <summary>러버밴드(화면 사각형)에 걸친 센서 — 묶음 칩이면 그 묶음의 센서 전부.</summary>
    private IReadOnlyList<int> SensorHits(Rect screenBand)
    {
        var world = new Rect(ScreenToWorld(screenBand.TopLeft), ScreenToWorld(screenBand.BottomRight));
        var items = _sensorChips.Values.Select(c => (c.Key, WorldRectOf(c)))
            .Concat(_groupChips.Values.SelectMany(g => g.Keys.Select(k => (k, WorldRectOf(g)))));
        var hits = FenceRubberBand.Hits(items, world).ToHashSet();
        return _scene?.Seq.Where(hits.Contains).ToList() ?? (IReadOnlyList<int>)hits.ToList();     // 체인 순서로
    }

    /// <summary>러버밴드에 걸친 망(망 순서).</summary>
    private IReadOnlyList<int> PanelHits(Rect screenBand)
    {
        var world = new Rect(ScreenToWorld(screenBand.TopLeft), ScreenToWorld(screenBand.BottomRight));
        return FenceRubberBand.Hits(_panelChips.Values.OrderBy(c => c.Key).Select(c => (c.Key, WorldRectOf(c))), world);
    }

    private static Rect WorldRectOf(FenceChip chip)
    {
        var left = Canvas.GetLeft(chip);
        var top = Canvas.GetTop(chip);
        if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(chip.Width) || double.IsNaN(chip.Height)) return Rect.Empty;
        return new Rect(left, top, chip.Width, chip.Height);
    }

    /// <summary>포인터 아래가 빼는 곳(<c>wiring-bin</c>) 또는 팔레트인가.</summary>
    private bool IsRemoveZoneUnder(Point canvasPoint)
    {
        var root = Window.GetWindow(this) as Visual ?? PresentationSource.FromVisual(this)?.RootVisual;
        if (root is null) return false;
        var rootPoint = TranslatePoint(canvasPoint, (UIElement)root);
        for (DependencyObject? d = DragHitTest.Top(root, rootPoint); d is not null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
        {
            if (DropZone.GetKey(d) == WiringViewModel.BinZoneKey) return true;
            if (d is FrameworkElement fe && AutomationProperties.GetAutomationId(fe) == "Devices.Wiring.Palette") return true;
            if (ReferenceEquals(d, this)) return false;
        }
        return false;
    }

    private static FenceChip? ChipFrom(DependencyObject? d)
    {
        for (; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is FenceChip chip) return chip;
        return null;
    }

    private static string GapText(int g, int n) => g <= 0 ? "#1 왼쪽" : g >= n ? $"#{n} 오른쪽" : $"#{g} ~ #{g + 1} 사이";
    #endregion

    #region - Context menu (FR-08) -
    /// <summary>
    /// 오른쪽 클릭(데드존 안) · Shift+F10 · 메뉴 키 — 누른 것이 선택 밖이면 그것만 고르고 메뉴를 연다.
    /// </summary>
    private void OpenMenu(FenceTargetKind target, FenceChip? chip, Point at)
    {
        if (ViewModel is not { } vm) return;
        IReadOnlyList<FenceMenuEntry> entries;
        switch (target)
        {
            case FenceTargetKind.Sensor when chip is not null:
                var key = chip.Keys[0];
                if (!chip.Keys.Any(vm.IsFenceSelected)) vm.FenceSelect(key);
                entries = vm.FenceMenu(FenceMenuTargetKind.Sensor, key);
                break;
            case FenceTargetKind.Panel when chip is not null:
                if (!vm.IsPanelSelected(chip.Key)) vm.FenceSelectPanel(chip.Key);
                entries = vm.FenceMenu(FenceMenuTargetKind.Panel, chip.Key);
                break;
            default:
                entries = vm.FenceMenu(FenceMenuTargetKind.Empty, 0);
                break;
        }
        LastMenu = entries;
        if (SuppressMenuPopup) return;
        FenceMenuPresenter.Show(this, entries, at);
    }
    #endregion

    #region - Keyboard (FR-04 · FR-05 · NFR-03) -
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (HandleKeyDown(e.Key, e.SystemKey, Keyboard.Modifiers, Keyboard.FocusedElement as DependencyObject)) e.Handled = true;
    }

    /// <summary>
    /// 키 판정 — 처리했으면 <c>true</c>(시험이 이 길로 부른다). <c>Alt</c>+화살표 · <c>F10</c> 은 <see cref="Key.System"/> + <paramref name="systemKey"/> 로 온다.
    /// Esc 는 끄는 중(누르는 중)이면 취소, 아니면 선택 해제 — 풀 것이 없으면 흘려보낸다(다른 Esc 동작을 깨지 않게).
    /// </summary>
    internal bool HandleKeyDown(Key key, Key systemKey, ModifierKeys modifiers, DependencyObject? focused)
    {
        var vm = ViewModel;
        if (vm is null || _scene is null) return false;

        if (key == Key.Escape)
        {
            if (_press is not null) { FinishDrag(false); return true; }
            return vm.FenceClearSelection();
        }

        if (key == Key.Z && modifiers == ModifierKeys.Control) { vm.Undo(); return true; }

        var alt = key == Key.System;
        var k = alt ? systemKey : key;
        var chip = focused as FenceChip;

        // Shift+F10 · 메뉴 키 — 오른쪽 클릭 메뉴의 키보드 대신(FR-08)
        if (k == Key.Apps || (k == Key.F10 && (modifiers & ModifierKeys.Shift) != 0))
        {
            var at = chip is not null ? ScreenCenterOf(chip) : new Point(ActualWidth / 2, ActualHeight / 2);
            OpenMenu(TargetOf(chip), chip, at);
            return true;
        }

        // Ctrl+A — 센서 모두(망에 포커스가 있으면 망 모두)
        if (!alt && k == Key.A && modifiers == ModifierKeys.Control)
        {
            if (chip is { Kind: FenceChipKind.Panel }) vm.FenceSelectAllPanels(); else vm.FenceSelectAllSensors();
            return true;
        }

        if (chip is { Kind: FenceChipKind.Panel }) return HandlePanelKey(vm, chip, k, alt, modifiers);

        // Ctrl+Space — 포커스 센서를 더하거나 뺀다
        if (!alt && k == Key.Space && modifiers == ModifierKeys.Control && chip is { Kind: FenceChipKind.Sensor or FenceChipKind.Group })
        {
            foreach (var each in chip.Keys) vm.FenceToggleSelect(each);
            return true;
        }

        // F = 보는 쪽 뒤집기(FR-20) — 글자를 치는 중이면 건드리지 않는다.
        if (!alt && k == Key.F && modifiers == ModifierKeys.None && focused is not System.Windows.Controls.Primitives.TextBoxBase)
        {
            var target = chip is { Kind: not FenceChipKind.Controller } ? chip.Keys[0] : ReferenceEquals(focused, this) ? vm.FenceSelectedKey : null;
            if (target is not { } flip) return false;
            vm.FenceFlipFacing(flip);
            if (chip is not null) FocusUnitOf(flip);
            return true;
        }

        if (chip is null)
        {
            if (!ReferenceEquals(focused, this) || alt) return false;
            switch (k)
            {
                case Key.Left: PanBy(PAN_STEP, 0); return true;
                case Key.Right: PanBy(-PAN_STEP, 0); return true;
                case Key.Up: PanBy(0, PAN_STEP); return true;
                case Key.Down: PanBy(0, -PAN_STEP); return true;
                default: return false;
            }
        }

        if (chip.Kind == FenceChipKind.Controller)
        {
            if (alt && k is Key.Left or Key.Right)
            {
                if (_scene.Shape == WiringShape.Ring) { if (k == Key.Left) vm.MoveEnclosureBack(); else vm.MoveEnclosureForward(); }
                else vm.NotifyFenceStatus("제어기 위치는 고정입니다");
                _controllerChip?.Focus();
                return true;
            }
        }
        else
        {
            var first = chip.Keys[0];
            if (alt && k is Key.Left or Key.Right) { MoveUnitVisually(chip, k == Key.Left ? -1 : 1); FocusUnitOf(first); return true; }
            if (alt && k is Key.Home or Key.End) { vm.FenceToEnd(first, k == Key.End); FocusUnitOf(first); return true; }
            if (!alt && k is Key.Delete or Key.Back)
            {
                var units = _scene.Units(_grouped);
                var at = units.ToList().FindIndex(u => u.Key == chip.Key);
                var neighbour = units.Skip(at + 1).FirstOrDefault(u => !u.IsController) ?? units.Take(Math.Max(0, at)).LastOrDefault(u => !u.IsController);
                var removing = chip.Kind == FenceChipKind.Sensor && vm.IsFenceSelected(chip.Key) ? vm.FenceDragKeys(chip.Key) : chip.Keys;
                if (vm.FenceUnplace(removing) && neighbour is not null) FocusUnitOf(neighbour.Keys[0]);
                return true;
            }
            if (!alt && k is Key.Enter or Key.Space && modifiers == ModifierKeys.None) { vm.FenceSelect(first); return true; }

            // Shift+←/→ — 기준 센서에서 이웃까지 범위 선택(FR-05 키보드 대신)
            if (!alt && k is Key.Left or Key.Right && modifiers == ModifierKeys.Shift)
            {
                var units = _scene.Units(_grouped).Where(u => !u.IsController).ToList();
                var at = units.FindIndex(u => u.Key == chip.Key);
                var j = at + (k == Key.Left ? -1 : 1);
                if (at >= 0 && j >= 0 && j < units.Count)
                {
                    vm.FenceExtendSensorSelection(units[j].Keys[0]);
                    FocusUnitOf(units[j].Keys[0], quietly: true);
                }
                return true;
            }
        }

        if (!alt && k is Key.Left or Key.Right or Key.Home or Key.End && modifiers == ModifierKeys.None)
        {
            var units = _scene.Units(_grouped).Where(u => !u.IsController || _scene.Shape != WiringShape.Ring).ToList();
            var at = units.FindIndex(u => chip.Kind == FenceChipKind.Controller ? u.IsController : u.Key == chip.Key);
            var target = k switch
            {
                Key.Home => 0,
                Key.End => units.Count - 1,
                Key.Left => at - 1,
                _ => at + 1,
            };
            if (target >= 0 && target < units.Count)
            {
                if (units[target].IsController) _controllerChip?.Focus();
                else FocusUnitOf(units[target].Keys[0]);
            }
            return true;
        }
        return false;
    }

    /// <summary>망에 포커스가 있을 때의 키 — ←/→/Home/End 이동 · Shift+←/→ 범위 · Enter/Space 선택 · Ctrl+Space 더함/뺌.</summary>
    private bool HandlePanelKey(WiringViewModel vm, FenceChip chip, Key k, bool alt, ModifierKeys modifiers)
    {
        if (alt) return false;
        var count = _panelChips.Count;
        if (k == Key.Space && modifiers == ModifierKeys.Control) { vm.FenceTogglePanel(chip.Key); return true; }
        if (k is Key.Enter or Key.Space && modifiers == ModifierKeys.None) { vm.FenceSelectPanel(chip.Key); return true; }
        if (k is Key.Left or Key.Right && modifiers == ModifierKeys.Shift)
        {
            var j = chip.Key + (k == Key.Left ? -1 : 1);
            if (j >= 0 && j < count)
            {
                vm.FenceExtendPanelSelection(j);
                if (_panelChips.TryGetValue(j, out var next)) FocusQuietly(next);
            }
            return true;
        }
        if (k is Key.Left or Key.Right or Key.Home or Key.End && modifiers == ModifierKeys.None)
        {
            var target = k switch { Key.Home => 0, Key.End => count - 1, Key.Left => chip.Key - 1, _ => chip.Key + 1 };
            if (target >= 0 && target < count && _panelChips.TryGetValue(target, out var next)) next.Focus();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 화면에서 한 단위 옆으로(Alt+←/→) — 이웃 단위 너머 세계 x 에 놓는 것과 같다. 양쪽 가지는 제어기를 건너 다른 가지로 간다.
    /// 한 줄은 제어기 앞으로 가지 못한다. 펜스 구성 모드에서는 이웃과 자리를 바꾼다(보드가 자리를 맞춘다).
    /// </summary>
    private void MoveUnitVisually(FenceChip chip, int direction)
    {
        if (_scene is null || ViewModel is not { } vm) return;
        var units = _scene.Units(_grouped).ToList();
        var at = units.FindIndex(u => u.Key == chip.Key);
        var j = at + direction;
        if (at < 0 || j < 0 || j >= units.Count || (_scene.Shape == WiringShape.Line && units[j].IsController))
        {
            vm.NotifyFenceStatus("끝 — 더 갈 자리가 없습니다");
            return;
        }
        var neighbourX = _scene.UnitX(units[j]);
        var (line, index) = _scene.DropAt(neighbourX + direction * 0.01);
        vm.FencePlace(chip.Keys, line, index);
    }

    private void FocusUnitOf(int key, bool quietly = false)
    {
        FenceChip? target = _sensorChips.TryGetValue(key, out var chip) && chip.IsVisible ? chip : _groupChips.Values.FirstOrDefault(g => g.Keys.Contains(key));
        if (target is null) return;
        if (quietly) FocusQuietly(target); else target.Focus();
    }

    /// <summary>선택을 건드리지 않고 포커스만 옮긴다(키보드 범위 선택 · 클릭 선택 뒤).</summary>
    private void FocusQuietly(FenceChip chip)
    {
        _arming = true;
        try { chip.Focus(); }
        finally { _arming = false; }
    }
    #endregion
}

/// <summary>
/// 펜스 캔버스의 UIA peer — Pane · 이름 · AutomationId(<see cref="FenceCanvas.AUTOMATION_ID"/>)를 내고, 자식은 기본 규칙대로
/// 시각 트리의 peer(망 · 센서 · 묶음 · 제어기 칩)를 모은다. 헤디드 시험이 캔버스를 찾고 그 아래에서 칩을 센다(SC-FEN-001).
/// </summary>
public sealed class FenceCanvasAutomationPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer
{
    public FenceCanvasAutomationPeer(FenceCanvas owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(FenceCanvas);

    protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore()
        => System.Windows.Automation.Peers.AutomationControlType.Pane;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => true;
}
