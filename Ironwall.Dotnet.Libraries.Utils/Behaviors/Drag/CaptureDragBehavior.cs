using Microsoft.Xaml.Behaviors;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 드래그 핸들 — 행 본체가 아니라 <b>이 손잡이에서만</b> 끌기가 시작된다.
/// </summary>
/// <remarks>
/// <see cref="Thumb"/> 파생이라 (1) 자동화 peer 가 실재하고 (2) 마우스 캡처와 그 상실을 스스로 다루며
/// (3) 종료를 <see cref="Thumb.DragCompleted"/> <b>하나</b>로 알린다(마우스 업 · 캡처 상실 · <see cref="Thumb.CancelDrag"/> 전부).
/// <c>Button</c> · <c>ToggleButton</c> 은 버블 단계에서 캡처를 빼앗아 마우스다운 순간 드래그가 죽는다 — 핸들로 쓰지 않는다.
/// </remarks>
public class DragHandle : Thumb
{
    static DragHandle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DragHandle), new FrameworkPropertyMetadata(typeof(DragHandle)));
        FocusableProperty.OverrideMetadata(typeof(DragHandle), new FrameworkPropertyMetadata(false));
    }
}

/// <summary>
/// 캡처 드래그 — 목록(<see cref="ItemsControl"/>)에 붙인다. 행 안의 <see cref="DragHandle"/> 을 잡아 끌면
/// 고스트가 따라오고, 드롭존 위에서는 형태로 가능 · 불가를 보이며, 순서 드롭존에서는 행 사이에 삽입선을 그린다.
/// OLE <c>DoDragDrop</c> 은 쓰지 않는다.
/// </summary>
/// <remarks>
/// <para>상태: 눌림(<c>_pressed</c>)과 끌기(<c>_dragging</c>)를 나눈다. 데드존(8.0 DIU) 을 넘기 전에는 클릭이다 —
/// 놓으면 그 행을 선택한다.</para>
/// <para>종료는 <see cref="FinishDrag"/> 한 곳: ① 플래그 ② 시각 복원 ③ 구독 해제 ④ 통지. 캡처는 <see cref="Thumb"/> 가 푼다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public class CaptureDragBehavior : Behavior<ItemsControl>
{
    private bool _pressed;
    private bool _dragging;
    private DragHandle? _handle;
    private object? _pressedItem;
    private DragPayload? _payload;
    private FrameworkElement? _root;
    private AdornerLayer? _ghostLayer;
    private DragGhostAdorner? _ghost;
    private AdornerLayer? _lineLayer;
    private InsertionLineAdorner? _line;
    // 끄는 동안 상태를 건드린 드롭존 전부 — 끝날 때 여기 있는 것을 빠짐없이 되돌린다.
    // 시작할 때 한 번 찍어 둔 목록만 믿으면, 끄는 도중에 나타난 드롭존(스크롤 · 펼침)은 Hover 로 굳거나 영영 '불가'가 된다.
    private readonly Dictionary<FrameworkElement, bool> _zoneAccepts = new();
    private Point _pressPoint;
    private FrameworkElement? _hoverZone;
    private int _hoverIndex = -1;
    // 지금 커서 아래 있지만 거절한 드롭존 — 거기서 놓으면 담당에게 알린다(IDropRefusalHandler).
    private FrameworkElement? _refusedZone;
    private int _refusedIndex = -1;
    private readonly EdgeAutoScroller _autoScroller = new();
    private string? _traceLastHit;
    // DragSession 에 올렸는가 — 눌림에서 올리고 FinishDrag(또는 BeginDrag 실패)에서 한 번만 내린다.
    private bool _inSession;

    #region - Properties -
    /// <summary>판정 · 처리 담당(보통 패널 뷰모델). 드롭존에 담당이 따로 없으면 이것을 쓴다.</summary>
    public static readonly DependencyProperty HandlerProperty = DependencyProperty.Register(
        nameof(Handler), typeof(IDragDropHandler), typeof(CaptureDragBehavior));
    public IDragDropHandler? Handler { get => (IDragDropHandler?)GetValue(HandlerProperty); set => SetValue(HandlerProperty, value); }

    /// <summary>고스트 라벨로 쓸 속성 이름. 비우면 <c>ToString()</c>.</summary>
    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(CaptureDragBehavior));
    public string? DisplayMemberPath { get => (string?)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }

    /// <summary>
    /// 같은 일을 <b>키보드 · 버튼으로</b> 하는 경로를 한 줄로 적는다 — 예: "Alt+↑↓", "상세 '그룹' 편집".
    /// 비어 있으면 디버그 빌드에서 예외다. 드래그 전용 UI 는 만들지 않는다 —
    /// 자동화가 좌표 클릭에 묶이고, 좌표 드래그는 파괴 동작 가드를 구조적으로 우회한다.
    /// </summary>
    public static readonly DependencyProperty KeyboardFallbackProperty = DependencyProperty.Register(
        nameof(KeyboardFallback), typeof(string), typeof(CaptureDragBehavior));
    public string? KeyboardFallback { get => (string?)GetValue(KeyboardFallbackProperty); set => SetValue(KeyboardFallbackProperty, value); }

    /// <summary>끌기를 허용할지 — 읽기 전용 · 적용 중이면 끈다.</summary>
    public static readonly DependencyProperty IsDragEnabledProperty = DependencyProperty.Register(
        nameof(IsDragEnabled), typeof(bool), typeof(CaptureDragBehavior), new PropertyMetadata(true));
    public bool IsDragEnabled { get => (bool)GetValue(IsDragEnabledProperty); set => SetValue(IsDragEnabledProperty, value); }

    public bool IsDragging => _dragging;
    #endregion

    #region - Wiring -
    protected override void OnAttached()
    {
        base.OnAttached();
        // 배선은 Loaded 에서 — DataTemplate 인플레이션 시점에는 부모 체인이 없다.
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;
        AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
        AssociatedObject.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted));
        AssociatedObject.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
    }

    protected override void OnDetaching()
    {
        FinishDrag(commit: false);
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
        AssociatedObject.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted));
        AssociatedObject.RemoveHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AssertHasFallback();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DragTrace.IsOn && (_pressed || _dragging)) DragTrace.Write($"[capture] unloaded while pressed list={DragTrace.Chain(AssociatedObject, 2)}");
        FinishDrag(commit: false);
    }

    [Conditional("DEBUG")]
    private void AssertHasFallback()
    {
        if (string.IsNullOrWhiteSpace(KeyboardFallback))
            throw new InvalidOperationException(
                $"CaptureDragBehavior({AssociatedObject?.GetType().Name}) 에 KeyboardFallback 이 비어 있습니다 — 드래그 전용 UI 는 만들지 않습니다. 같은 일을 하는 키보드 · 버튼 경로를 적으세요.");
    }
    #endregion

    #region - Thumb events -
    /// <summary>
    /// DataGrid 행의 손잡이를 누르기 직전(터널) — 이미 골라 둔 행이면 그 칸에 초점을 먼저 준다.
    /// </summary>
    /// <remarks>
    /// <para><c>DataGridCell</c> 은 <c>MouseLeftButtonDown</c> 클래스 처리기를 <b>handledEventsToo</b> 로 건다(WPF 원본).
    /// 손잡이(Thumb)가 눌림을 처리해도 칸은 "초점이 칸 밖"이면 선택을 그 행 하나로 접었다 — 여러 행을 골라 끌면
    /// 잡은 행만 실려 "잡은 행이 선택에 들어 있으면 선택 전부를" 계약이 깨졌다(ListBox 는 괜찮다: ListBoxItem 의 선택은
    /// 가상 메서드라 처리된 눌림에서 불리지 않는다). 초점이 칸 안 · 선택됨 · 처리됨이면 칸은 아무것도 바꾸지 않는다.</para>
    /// <para>고르지 않은 행을 잡으면 DataGrid 가 그 행을 고른다 — 탐색기와 같고, 실리는 것도 그 행 하나라 계약과 결과가 같다.
    /// Ctrl · Shift 가 눌렸으면 DataGrid 의 선택 규칙에 맡긴다.</para>
    /// </remarks>
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !IsDragEnabled || _pressed) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.OriginalSource is not DependencyObject source) return;

        var handle = AncestorOf<DragHandle>(source);
        if (handle == null) return;
        var cell = AncestorOf<DataGridCell>(handle);
        if (cell == null || !cell.IsSelected || cell.IsKeyboardFocusWithin) return;
        cell.Focus();
    }

    private static T? AncestorOf<T>(DependencyObject start) where T : DependencyObject
    {
        for (var d = start; d != null; d = ParentOf(d))
            if (d is T hit) return hit;
        return null;
    }

    private void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (e.OriginalSource is not DragHandle handle) return;          // 열 머리 · 스크롤바의 Thumb 은 남의 것
        if (!IsDragEnabled || _pressed) return;

        var container = ItemsControl.ContainerFromElement(AssociatedObject, handle);
        if (container == null) return;
        var item = AssociatedObject.ItemContainerGenerator.ItemFromContainer(container);
        if (item == null || item == DependencyProperty.UnsetValue) return;

        _pressed = true;
        _handle = handle;
        // 끝남은 목록이 아니라 잡은 손잡이에서 직접 받는다. 끄는 도중 목록이 다시 그려져(서버 재조회 · 되돌리기 · 탭 전환)
        // 잡은 행의 컨테이너가 빠지면 손잡이는 캡처를 잃고 DragCompleted(취소)를 내지만, 그 이벤트는 떨어져 나간 가지에서
        // 목록까지 올라오지 못한다 — 고스트 · 삽입선 · 커서 · DragSession 이 영영 남았다(2026-09-29 이벤트 맵핑 실창 재현).
        handle.DragCompleted += OnDragCompleted;
        _pressedItem = item;
        _pressPoint = DragPointer.GetPosition(AssociatedObject);
        EnterSession();
        e.Handled = true;
        if (DragTrace.IsOn) DragTrace.Write($"[capture] start list={DragTrace.Chain(AssociatedObject, 2)} item={item} captured={DragTrace.Captured()}");
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_pressed || !ReferenceEquals(e.OriginalSource, _handle)) return;
        e.Handled = true;

        if (!_dragging)
        {
            // DragDeltaEventArgs 의 이동량은 '손잡이 기준' 좌표다 — 목록이 굴러 손잡이가 움직이면 누적값이 아니게 된다.
            // 움직이지 않는 목록 기준으로 직접 잰다.
            var now = DragPointer.GetPosition(AssociatedObject);
            if (!DragMath.IsDrag(now.X - _pressPoint.X, now.Y - _pressPoint.Y)) return;
            BeginDrag();
            if (!_dragging) return;
        }
        UpdateDrag();
    }

    private void OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_pressed || !ReferenceEquals(e.OriginalSource, _handle)) return;
        e.Handled = true;
        if (DragTrace.IsOn) DragTrace.Write($"[capture] completed canceled={e.Canceled} dragging={_dragging} hover={(_hoverZone == null ? "(none)" : DropZone.GetKey(_hoverZone))} index={_hoverIndex} captured={DragTrace.Captured()}");
        FinishDrag(commit: !e.Canceled);
    }
    #endregion

    #region - Drag lifecycle -
    private void BeginDrag()
    {
        var items = PayloadItems();
        if (items.Count == 0) { _pressed = false; ReleaseHandle(); ExitSession(); return; }

        _root = Window.GetWindow(AssociatedObject) as FrameworkElement ?? TopVisual(AssociatedObject);
        if (_root == null) { _pressed = false; ReleaseHandle(); ExitSession(); return; }

        _payload = new DragPayload(AssociatedObject, items, LabelOf(items[0]));
        _dragging = true;

        _zoneAccepts.Clear();
        foreach (var zone in DropZone.ZonesUnder(_root)) Probe(zone);
        if (DragTrace.IsOn) DragTrace.Write($"[capture] begin root={DragTrace.Chain(_root, 1)} zones=[{string.Join(", ", _zoneAccepts.Select(z => $"{DropZone.GetKey(z.Key)}:{z.Value}"))}]");

        _ghostLayer = GhostLayerFor(AssociatedObject);
        if (_ghostLayer != null)
        {
            _ghost = new DragGhostAdorner(AssociatedObject, _ghostLayer, _payload.Label, _payload.Count);
            _ghostLayer.Add(_ghost);
        }

        _root.PreviewKeyDown += OnRootPreviewKeyDown;
        _root.Cursor = Cursors.SizeAll;                 // Mouse.OverrideCursor 는 쓰지 않는다 — 해제 경로가 빈약해 커서가 남는다
        _autoScroller.Tick += UpdateDrag;
    }

    private void UpdateDrag()
    {
        if (!_dragging || _root == null || _payload == null) return;
        if (_handle != null && !_handle.IsDescendantOf(AssociatedObject))
        {
            // 잡은 행이 목록에서 빠졌다(다시 그려짐) — 끄던 것은 이제 없다. 오토스크롤 틱이 여기로 올 수 있다.
            if (DragTrace.IsOn) DragTrace.Write("[capture] held handle left the list — cancel");
            FinishDrag(commit: false);
            return;
        }

        _ghost?.MoveTo(DragPointer.GetPosition(AssociatedObject));

        var zone = ZoneAt(DragPointer.GetPosition(_root), out var hit);
        if (DragTrace.IsOn)
        {
            var chain = DragTrace.Chain(hit, 10);
            if (chain != _traceLastHit)
            {
                _traceLastHit = chain;
                DragTrace.Write($"[capture] at root={DragPointer.GetPosition(_root)} list={DragPointer.GetPosition(AssociatedObject)} zone={(zone == null ? "(none)" : DropZone.GetKey(zone))} hit={chain}");
            }
        }
        var index = -1;
        var rawIndex = -1;
        var lineY = double.NaN;

        if (zone != null && DropZone.GetIsReorder(zone) && zone is ItemsControl list)
        {
            (index, lineY) = InsertionAt(list, hit);
            rawIndex = index;
            Probe(zone);        // 끄는 도중에 나타난 목록도 상태 장부에 올린다
            var ok = index >= 0 && SafeCanDrop(zone, DropZone.TargetOf(zone, index));
            if (!ok) { index = -1; lineY = double.NaN; }
            _autoScroller.Track(list);
        }
        else
        {
            _autoScroller.Stop();
        }

        var accepted = zone != null && (DropZone.GetIsReorder(zone) ? index >= 0 : Probe(zone));
        var newHover = accepted ? zone : null;
        _refusedZone = zone != null && !accepted ? zone : null;
        _refusedIndex = rawIndex;

        // 후보가 바뀔 때만 시각을 고친다 — 마우스 이동마다 다시 그리지 않는다(RDP 에서 증폭된다).
        if (!ReferenceEquals(newHover, _hoverZone))
        {
            if (DragTrace.IsOn) DragTrace.Write($"[capture] hover {(newHover == null ? "(none)" : DropZone.GetKey(newHover))} zone={(zone == null ? "(none)" : DropZone.GetKey(zone))} index={index} hit={DragTrace.Chain(hit)}");
            if (_hoverZone != null) DropZone.SetState(_hoverZone, _zoneAccepts.TryGetValue(_hoverZone, out var was) && was ? DropZoneState.Available : DropZoneState.Blocked);
            if (newHover != null) DropZone.SetState(newHover, DropZoneState.Hover);
            MoveLineTo(newHover);
            _hoverZone = newHover;
        }
        _hoverIndex = index;
        _line?.MoveTo(lineY);
    }

    /// <summary>
    /// 종료 — 마우스 업 · 캡처 상실 · Esc · 분리 · 언로드가 전부 이리로 온다.
    /// 순서: ① 플래그 ② 시각 복원 ③ 구독 해제 ④ 통지(드롭). 통지를 먼저 하면 그 안에서 재진입한다.
    /// </summary>
    private void FinishDrag(bool commit)
    {
        ExitSession();      // 드롭 통지(④) 전에 내린다 — 담당이 드롭 처리 중에 목록을 다시 읽을 수 있어야 한다
        if (!_pressed && !_dragging) return;

        // ① 플래그
        var wasDragging = _dragging;
        var payload = _payload;
        var zone = _hoverZone;
        var index = _hoverIndex;
        var refusedZone = _refusedZone;
        var refusedIndex = _refusedIndex;
        var pressedItem = _pressedItem;
        _pressed = false;
        _dragging = false;
        _payload = null;
        _hoverZone = null;
        _hoverIndex = -1;
        _refusedZone = null;
        _refusedIndex = -1;
        ReleaseHandle();
        _pressedItem = null;

        // ② 시각 복원
        foreach (var z in _zoneAccepts.Keys) DropZone.SetState(z, DropZoneState.None);
        _zoneAccepts.Clear();
        if (_ghost != null) { _ghostLayer?.Remove(_ghost); _ghost = null; }
        _ghostLayer = null;
        MoveLineTo(null);

        // ③ 구독 해제
        _autoScroller.Tick -= UpdateDrag;
        _autoScroller.Stop();
        if (_root != null)
        {
            _root.PreviewKeyDown -= OnRootPreviewKeyDown;
            _root.ClearValue(FrameworkElement.CursorProperty);
            _root = null;
        }

        // ④ 통지
        if (!wasDragging)
        {
            if (commit && pressedItem != null) SelectOnly(pressedItem);       // 데드존 미만 = 클릭
            return;
        }
        if (!commit || payload == null) return;
        if (zone == null)
        {
            NotifyRefused(payload, refusedZone, refusedIndex);
            return;
        }

        var target = DropZone.TargetOf(zone, DropZone.GetIsReorder(zone) ? index : -1);
        var handler = HandlerFor(zone);
        // 시각 · 구독은 위에서 이미 다 풀었다 — 담당(창)의 Drop 이 던져도 커널 상태는 깨끗하다. 예외는 삼키지 않는다.
        var canDrop = handler != null && SafeCanDrop(handler, payload, target);
        if (DragTrace.IsOn) DragTrace.Write($"[capture] drop zone={target.ZoneKey} index={index} handler={handler?.GetType().Name ?? "(none)"} canDrop={canDrop}");
        if (canDrop) handler!.Drop(payload, target);
        else NotifyRefused(payload, zone, index);
    }

    /// <summary>
    /// 거절한 드롭존 위에서 놓았다 — 담당이 <see cref="IDropRefusalHandler"/> 면 알려 사유를 말하게 한다.
    /// 출발 목록 자신(또는 그것을 품은 드롭존)에 도로 놓은 것은 그만두기라 알리지 않는다.
    /// </summary>
    private void NotifyRefused(DragPayload payload, FrameworkElement? zone, int index)
    {
        if (zone == null) return;
        if (ReferenceEquals(zone, payload.Source) || payload.Source.IsDescendantOf(zone)) return;
        if (HandlerFor(zone) is not IDropRefusalHandler listener) return;

        var target = DropZone.TargetOf(zone, DropZone.GetIsReorder(zone) ? index : -1);
        if (DragTrace.IsOn) DragTrace.Write($"[capture] refused zone={target.ZoneKey} index={target.InsertionIndex}");
        listener.Refused(payload, target);
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 터널에서, 끄는 중일 때만 소비한다 — 무조건 소비하면 Esc 로 선택을 푸는 기존 동작이 깨진다.
        if (!_dragging || e.Key != Key.Escape) return;
        e.Handled = true;
        _handle?.CancelDrag();          // → DragCompleted(Canceled) → FinishDrag(false)
        // 손잡이가 이미 끌기를 놓친 상태(캡처 상실 뒤)면 CancelDrag 는 아무것도 내지 않는다 — 여기서 직접 끝낸다.
        if (_pressed || _dragging) FinishDrag(commit: false);
    }
    #endregion

    #region - Helpers -
    /// <summary>잡은 손잡이에서 손을 뗀다 — 끝남 구독을 풀고 잊는다(끝나는 모든 길이 여기로 온다).</summary>
    private void ReleaseHandle()
    {
        if (_handle != null) _handle.DragCompleted -= OnDragCompleted;
        _handle = null;
    }

    private void EnterSession()
    {
        if (_inSession) return;
        _inSession = true;
        DragSession.Enter();
    }

    private void ExitSession()
    {
        if (!_inSession) return;
        _inSession = false;
        DragSession.Exit();
    }

    private IReadOnlyList<object> PayloadItems()
    {
        if (_pressedItem == null) return Array.Empty<object>();

        // 잡은 행이 선택에 들어 있으면 선택 전부를, 아니면 그 행만.
        var selected = AssociatedObject switch
        {
            MultiSelector m => m.SelectedItems.Cast<object>().ToList(),
            ListBox l => l.SelectedItems.Cast<object>().ToList(),
            Selector s when s.SelectedItem != null => new List<object> { s.SelectedItem },
            _ => new List<object>(),
        };
        if (!selected.Contains(_pressedItem)) return new[] { _pressedItem };

        var order = AssociatedObject.Items;
        return selected.OrderBy(order.IndexOf).ToList();
    }

    private string LabelOf(object item)
    {
        var path = DisplayMemberPath;
        if (!string.IsNullOrEmpty(path))
        {
            var value = item.GetType().GetProperty(path)?.GetValue(item);
            if (value != null) return value.ToString() ?? string.Empty;
        }
        return item.ToString() ?? string.Empty;
    }

    private void SelectOnly(object item)
    {
        if (AssociatedObject is Selector selector) selector.SelectedItem = item;
    }

    /// <summary>
    /// 드롭존의 담당. 드롭존에 <see cref="DropZone.HandlerProperty"/> 가 없으면 <b>끌기 시작한 목록의 담당</b>을 쓴다.
    /// 한 창에 끌기 출발지가 둘 이상(팔레트 + 보드)이고 담당이 서로 다르면 드롭존마다 담당을 명시해야 한다.
    /// </summary>
    private IDragDropHandler? HandlerFor(FrameworkElement zone) => DropZone.GetHandler(zone) ?? Handler;

    /// <summary>드롭존을 상태 장부에 올리고(처음이면 판정해서) 받는지 돌려준다.</summary>
    private bool Probe(FrameworkElement zone)
    {
        if (_zoneAccepts.TryGetValue(zone, out var known)) return known;

        // 순서 드롭존은 인덱스마다 답이 다를 수 있다 — 여기서는 "어디든 하나"를 묻는다.
        var accepts = SafeCanDrop(zone, DropZone.TargetOf(zone, DropZone.GetIsReorder(zone) ? 0 : -1));
        _zoneAccepts[zone] = accepts;
        DropZone.SetState(zone, accepts ? DropZoneState.Available : DropZoneState.Blocked);
        return accepts;
    }

    private bool SafeCanDrop(FrameworkElement zone, DropTarget target)
        => _payload != null && HandlerFor(zone) is { } handler && SafeCanDrop(handler, _payload, target);

    /// <summary>
    /// 판정은 마우스를 움직일 때마다 불린다 — 창 쪽 구현이 던지면 캡처를 쥔 채 입력 처리 한가운데서 터진다.
    /// 던진 판정은 "놓을 수 없음"으로 읽고 흔적을 남긴다(판정은 부수효과가 없어야 하므로 잃는 것이 없다).
    /// </summary>
    private static bool SafeCanDrop(IDragDropHandler handler, DragPayload payload, DropTarget target)
    {
        try
        {
            return handler.CanDrop(payload, target);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.TraceError($"[CaptureDragBehavior] CanDrop threw for zone '{target.ZoneKey}' — treated as not droppable: {ex}");
            return false;
        }
    }

    private FrameworkElement? ZoneAt(Point pointInRoot, out DependencyObject? hit)
    {
        hit = null;
        if (_root == null) return null;

        // 숨은 덮개(MetroWindow PART_OverlayBox) · 우리 고스트 · 삽입선을 거른다 — DragHitTest 주석.
        hit = DragHitTest.Top(_root, pointInRoot);
        return DragHitTest.ZoneFrom(hit);
    }

    /// <summary>
    /// 삽입 인덱스 — HitTest → ContainerFromElement → ItemFromContainer → IndexOf. y 산술 · ContainerFromIndex 를 쓰지 않는다
    /// (행 높이가 패널마다 다르고, 컨테이너가 재활용된다).
    /// </summary>
    private static (int Index, double LineY) InsertionAt(ItemsControl list, DependencyObject? hit)
    {
        var count = list.Items.Count;
        var pointer = DragPointer.GetPosition(list);

        var container = hit == null ? null : ItemsControl.ContainerFromElement(list, hit) as FrameworkElement;
        if (container != null)
        {
            var item = list.ItemContainerGenerator.ItemFromContainer(container);
            var row = list.Items.IndexOf(item);
            if (row >= 0)
            {
                var rect = new Rect(container.TranslatePoint(new Point(0, 0), list), container.RenderSize);
                var offset = DragMath.InsertionIndex(new[] { rect }, pointer.Y);           // 0 = 이 행 앞, 1 = 뒤
                return (row + offset, offset == 0 ? rect.Top : rect.Bottom);
            }
        }

        // 행이 아닌 빈 자리 — 맨 끝에 놓는다.
        if (count == 0) return (0, 2);
        var last = list.ItemContainerGenerator.ContainerFromItem(list.Items[count - 1]) as FrameworkElement;
        var y = last == null ? double.NaN : last.TranslatePoint(new Point(0, last.RenderSize.Height), list).Y;
        return (count, y);
    }

    /// <summary>
    /// 고스트를 얹을 층 — 가장 가까운 <see cref="AdornerDecorator"/>(콘솔 셸 · 창)의 층.
    /// </summary>
    /// <remarks>
    /// <see cref="AdornerLayer.GetAdornerLayer"/> 는 <see cref="ScrollContentPresenter"/> 의 층도 돌려준다. 출발 목록이
    /// 스크롤 뷰어(상세 칸 · 팔레트) 안에 있으면 고스트가 그 뷰어의 뷰포트로 잘려, 다른 칸 위로 끄는 순간 사라졌다
    /// (2026-09-29 이벤트 맵핑 워크벤치 — 팔레트 → 보드). 그런 층은 건너뛴다.
    /// </remarks>
    private static AdornerLayer? GhostLayerFor(Visual element)
    {
        for (var d = VisualTreeHelper.GetParent(element); d != null; d = VisualTreeHelper.GetParent(d))
            if (d is AdornerDecorator { AdornerLayer: { } layer }) return layer;
        return AdornerLayer.GetAdornerLayer(element);
    }

    private void MoveLineTo(FrameworkElement? zone)
    {
        if (_line != null)
        {
            _lineLayer?.Remove(_line);
            _line = null;
            _lineLayer = null;
        }
        if (zone == null || !DropZone.GetIsReorder(zone)) return;

        _lineLayer = AdornerLayer.GetAdornerLayer(zone);
        if (_lineLayer == null) return;
        _line = new InsertionLineAdorner(zone);
        _lineLayer.Add(_line);
    }

    private static DependencyObject? ParentOf(DependencyObject d)
        => d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    private static FrameworkElement? TopVisual(DependencyObject d)
    {
        FrameworkElement? top = null;
        for (var c = d; c != null; c = ParentOf(c))
            if (c is FrameworkElement fe) top = fe;
        return top;
    }
    #endregion
}

/// <summary>
/// 끄는 동안의 포인터 위치. 평소에는 <see cref="Mouse.GetPosition"/> 그대로다.
/// </summary>
/// <remarks>
/// <see cref="Override"/> 는 <b>시험용</b>이다 — UIA 에 드래그 패턴이 없고 실제 입력 주입은 사용자의 마우스를 빼앗으므로,
/// 갤러리 · 테스트가 드래그 이벤트를 직접 일으킬 때 포인터 자리만 알려 준다. 제품 코드는 건드리지 않는다.
/// </remarks>
public static class DragPointer
{
    public static Func<IInputElement, Point>? Override { get; set; }

    internal static Point GetPosition(IInputElement relativeTo) => Override?.Invoke(relativeTo) ?? Mouse.GetPosition(relativeTo);
}

/// <summary>
/// 경계 오토스크롤 — 끄는 동안 포인터가 목록의 위 · 아래 띠에 들어가면 굴린다. 속도는 <b>px/sec</b>(시간 기반).
/// </summary>
internal sealed class EdgeAutoScroller
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly Stopwatch _clock = new();
    private ScrollViewer? _viewer;
    private ItemsControl? _list;

    public EdgeAutoScroller() => _timer.Tick += OnTick;

    /// <summary>굴린 뒤 — 포인터는 그대로여도 그 아래 행이 바뀌었으니 삽입 위치를 다시 잡으라는 신호.</summary>
    public event System.Action? Tick;

    public void Track(ItemsControl list)
    {
        if (!ReferenceEquals(_list, list))
        {
            _list = list;
            _viewer = FindScrollViewer(list);
        }
        if (_viewer == null || _timer.IsEnabled) return;
        _clock.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Reset();
        _list = null;
        _viewer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_viewer == null) { Stop(); return; }

        var elapsed = _clock.Elapsed;
        _clock.Restart();

        var y = DragPointer.GetPosition(_viewer).Y;
        var delta = DragMath.AutoScrollDelta(y, _viewer.ViewportHeight > 0 ? _viewer.ActualHeight : 0, elapsed);
        if (delta == 0) return;

        // CanContentScroll(항목 단위 스크롤)이면 오프셋 단위가 "항목"이다 — 픽셀 속도를 항목 속도로 낮춘다.
        var unit = _viewer.CanContentScroll ? delta / 38.0 : delta;
        _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset + unit);
        Tick?.Invoke();          // 오프셋은 다음 레이아웃에서 바뀐다 — 비교하지 않고 매번 알린다
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }
}
