using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 레일 한 줄 — 아이콘 · 라벨 · 개수 배지. 장애가 있으면 배지가 <c>▲2 · 46</c> 이 되고 위험색으로 바뀐다.
/// </summary>
public class ConsoleRailEntry : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private object? _icon;
    private int _count;
    private int _badCount;
    private bool _showCount = true;

    public ConsoleRailEntry(string key, string label, object? icon = null)
    {
        Key = key;
        _label = label;
        _icon = icon;
    }

    /// <summary>식별 키 — 자동화 식별자(<c>Console.{콘솔}.Rail.{Key}</c>)와 전환 판정에 쓴다.</summary>
    public string Key { get; }

    /// <summary>창이 매달아 두는 것(패널 뷰모델 등).</summary>
    public object? Tag { get; set; }

    /// <summary>이 줄 위에 구분선을 긋는다.</summary>
    public bool HasSeparatorAbove { get; set; }

    public string Label { get => _label; set => Set(ref _label, value); }
    /// <summary>아이콘 요소(예: <c>md:PackIcon</c>). 커널은 아이콘 라이브러리를 모른다.</summary>
    public object? Icon { get => _icon; set => Set(ref _icon, value); }
    public bool ShowCount { get => _showCount; set { if (Set(ref _showCount, value)) Raise(nameof(CountText)); } }

    public int Count { get => _count; set { if (Set(ref _count, value)) Raise(nameof(CountText)); } }

    public int BadCount
    {
        get => _badCount;
        set { if (Set(ref _badCount, value)) { Raise(nameof(CountText)); Raise(nameof(HasBad)); } }
    }

    public bool HasBad => _badCount > 0;

    public string CountText => !_showCount ? string.Empty : _badCount > 0 ? $"▲{_badCount} · {_count}" : _count.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 레일의 SelectedItem 에 묶인 원본(콘솔 뷰모델)이 "받아들였지만 아직 끝나지 않은 전환"을 알리는 약속 — 고르면 곧바로 거절하지 않고
/// await 뒤에 전환 키를 바꾸는 원본만 단다.
/// </summary>
/// <remarks>
/// 레일은 선택 변경 직후 묶인 값이 사용자의 선택과 다르면 <b>거절</b>로 보고 곧바로(그리기 전) 되돌린다. 전환이 await 로 양보하면
/// 그 시점의 묶인 값은 아직 앞 레일이라 받아들인 전환도 거절처럼 보인다(앞 레일로 튀었다가 늦은 알림에 새 레일로 · 초점은 앞 레일).
/// 이 약속을 단 원본이 <see cref="IsRailSwitchPending"/> 을 참으로 보이는 동안 레일은 되돌리지 않고 기다렸다가,
/// 원본이 PropertyChanged 를 울려 끝났음을 알리면 그때의 묶인 값으로 다시 판정한다(받아들였으면 그대로 · 막았으면 되돌림).
/// </remarks>
public interface IConsoleRailSwitchState : INotifyPropertyChanged
{
    /// <summary>받아들인 레일 전환이 아직 진행 중인가. 끝나면(받아들였든 끝내 막았든) PropertyChanged 를 한 번 이상 울려야 한다.</summary>
    bool IsRailSwitchPending { get; }
}

/// <summary>
/// 콘솔 레일 — <b>바인딩된 항목 목록</b>이다(<c>Tag</c> 문자열 스위치가 아니다). 선택 표시는 좌측 3px 바 —
/// 라이트 테마에서 주색 · 선택색 · 포커스색이 같은 색이라 색으로는 구분되지 않는다.
/// </summary>
public class ConsoleRail : ListBox
{
    static ConsoleRail()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleRail), new FrameworkPropertyMetadata(typeof(ConsoleRail)));
    }

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(ConsoleRail), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    /// <summary>아이콘만 남기고 접는다(960 미만).</summary>
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(ConsoleRail));
    /// <summary>아래 요약 블록 — "전체 N대 / 장애 N대". 접히면 숨는다.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    public static readonly DependencyProperty ConsoleKeyProperty = DependencyProperty.Register(
        nameof(ConsoleKey), typeof(string), typeof(ConsoleRail), new FrameworkPropertyMetadata("Console", FrameworkPropertyMetadataOptions.Inherits));
    public string ConsoleKey { get => (string)GetValue(ConsoleKeyProperty); set => SetValue(ConsoleKeyProperty, value); }

    /// <summary>
    /// 선택 변경이 끝난 뒤 — 목록 안의 선택(SelectedItems · 항목 컨테이너)이 <see cref="Selector.SelectedItem"/> 과 갈라졌으면 SelectedItem 쪽으로 맞춘다.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 갈라지는가</b> — 레일은 SelectedItem 을 콘솔 뷰모델에 TwoWay 로 묶고, 뷰모델은 미적용 변경이 있으면 레일 전환을 <b>거절</b>한다.
    /// WPF 는 TwoWay 로 원본에 쓴 직후 원본을 무조건 다시 읽는데, 그 다시 읽기(원래 레일)는 목록의 선택 변경이 아직 진행 중일 때 와서
    /// Selector 가 무시한다 — SelectedItem 만 원래 레일이 되고 SelectedItems · 컨테이너는 누른 레일에 남는다. 화면 · UIA 는 막힌 레일을 고른 채였다
    /// (2026-09-28 헤디드 SC-ACC-006 · SC-DEV-008). 거절을 알리든 안 알리든, 한 박자 뒤 다시 알리든 같다 — 한 박자 뒤에는 SelectedItem 이
    /// 이미 같은 값이라 아무 변경도 일어나지 않는다.</para>
    /// <para><b>왜 여기서 · 이렇게</b> — 이 시점(선택 변경 이벤트 뒤)에는 진행 중인 변경이 끝났다. <see cref="Selector.SelectedIndex"/> 를
    /// SelectedItem 의 자리로 옮기면 평범한 선택 변경 한 번으로 컨테이너까지 되돌아오고, SelectedItem 은 이미 그 값이라 원본에 다시 쓰지 않는다
    /// (거절 → 되쓰기 → 거절의 되풀이가 없다). 같은 입력 처리 안에서 끝나 막힌 레일이 한 번도 그려지지 않는다. 뷰모델마다 게터 속임수 ·
    /// 한 박자 뒤 알림을 따로 짜지 않아도 된다 — 레일을 쓰는 모든 콘솔에 한 번에 걸린다.</para>
    /// <para><b>받아들였지만 아직 끝나지 않은 전환</b> — 원본이 <see cref="IConsoleRailSwitchState"/> 로 진행 중이라고 알리면 되돌리지 않고
    /// 끝날 때 다시 판정한다(적대 검토 M2). 알리지 않는 원본은 이 시점의 묶인 값으로 판정한다(지금 콘솔들은 전환이 양보 없이 끝난다 —
    /// 실제 뷰 시험으로 확인).</para>
    /// </remarks>
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        // 처리기(자동화 이벤트 포함)가 먼저 사용자의 변경을 보고, 그다음 되돌림을 본다 — 마지막으로 보는 것이 실제 상태가 된다.
        base.OnSelectionChanged(e);
        ReconcileSelection(e);
    }

    private bool _reconciling;
    private IConsoleRailSwitchState? _awaitedOwner;
    private bool _awaitedCheckPosted;

    private void ReconcileSelection(SelectionChangedEventArgs e)
    {
        if (_reconciling || SelectionMode != SelectionMode.Single) return;
        if (!IsSplit(out _, out var shown)) return;   // 한 가지 상태 — 평소의 모든 선택 변경

        // 받아들였지만 아직 끝나지 않은 전환(원본이 await 중) — 지금 되돌리면 받아들인 전환이 거절처럼 보인다
        // (앞 레일로 튀었다가 늦은 알림에 새 레일로). 되돌리지 않고, 원본이 끝났다고 알릴 때 다시 본다.
        if (BoundSource is IConsoleRailSwitchState { IsRailSwitchPending: true } owner)
        {
            AwaitOwner(owner);
            return;
        }

        SnapBack(shown);
    }

    /// <summary>SelectedItem(묶인 값)과 목록 안의 선택(SelectedItems)이 갈라졌는가.</summary>
    private bool IsSplit(out object? bound, out object? shown)
    {
        bound = SelectedItem;
        shown = SelectedItems.Count > 0 ? SelectedItems[0] : null;
        return !Equals(bound, shown);
    }

    /// <summary>SelectedItem 바인딩의 원본(콘솔 뷰모델). 바인딩이 아니면 null.</summary>
    private object? BoundSource => BindingOperations.GetBindingExpression(this, SelectedItemProperty)?.ResolvedSource;

    /// <summary>목록 안의 선택을 SelectedItem 자리로 되돌린다 — 같은 입력 처리 안에서(그리기 전에).</summary>
    private void SnapBack(object? picked)
    {
        var bound = SelectedItem;
        var index = bound is null ? -1 : Items.IndexOf(bound);
        if (bound is not null && index < 0) return;   // 목록에 없는 값 — 맞출 자리가 없다

        _reconciling = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _reconciling = false;
        }

        if (bound is not null && picked is not null) RestoreFocusAfterInput(picked, bound);
    }

    /// <summary>
    /// 누른 항목에 있던 키보드 초점을 원래 항목으로 옮긴다(화살표가 막힌 레일에서 출발하지 않게) — <b>입력 처리가 끝난 뒤에</b>.
    /// </summary>
    /// <remarks>
    /// 키보드 ↓ 는 ListBox 가 초점을 옮기고 → 고르고 → 선택 변경 뒤 초점을 누른 항목에 <b>한 번 더</b> 둔다(실측 2026-09-28:
    /// focus→det · focus→mal · sel+mal · sel+det · focus→det · focus→mal). 선택 변경 안에서 옮기면 곧바로 덮여 초점이 막힌 레일에 남았다.
    /// 그때까지 원본이 늦게 받아들였으면(SelectedItem 이 더는 원래 항목이 아니면) 초점은 사용자의 선택에 그대로 둔다.
    /// 우선순위 Normal — 그리기(Render) 전이라 선택과 초점이 한 화면에 같이 바뀐다.
    /// </remarks>
    private void RestoreFocusAfterInput(object picked, object original)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            if (!Equals(SelectedItem, original)) return;
            if (ItemContainerGenerator.ContainerFromItem(picked) is not UIElement { IsKeyboardFocusWithin: true }) return;
            if (ItemContainerGenerator.ContainerFromItem(original) is UIElement container) container.Focus();
        }));
    }

    private void AwaitOwner(IConsoleRailSwitchState owner)
    {
        if (ReferenceEquals(_awaitedOwner, owner)) return;
        StopAwaitingOwner();
        _awaitedOwner = owner;
        owner.PropertyChanged += OnAwaitedOwnerChanged;
    }

    private void StopAwaitingOwner()
    {
        if (_awaitedOwner is null) return;
        _awaitedOwner.PropertyChanged -= OnAwaitedOwnerChanged;
        _awaitedOwner = null;
    }

    private void OnAwaitedOwnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 같은 알림을 바인딩도 듣는다 — 바인딩이 SelectedItem 을 옮긴 뒤(DataBind, 그리기 전) 본다. 알림이 몰려도 한 번만.
        if (_awaitedCheckPosted) return;
        _awaitedCheckPosted = true;
        Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(CheckAwaitedOwner));
    }

    private void CheckAwaitedOwner()
    {
        _awaitedCheckPosted = false;
        if (_awaitedOwner is null || _awaitedOwner.IsRailSwitchPending) return;
        StopAwaitingOwner();

        // 받아들였으면 바인딩이 이미 새 항목으로 맞췄다(갈라짐 없음). 끝내 막았으면 이제 되돌린다.
        if (!_reconciling && SelectionMode == SelectionMode.Single && IsSplit(out _, out var shown))
            SnapBack(shown);
    }

    protected override DependencyObject GetContainerForItemOverride() => new ConsoleRailItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ConsoleRailItem;

    /// <summary>템플릿의 바닥 띠(요약 · 부대 필터 등). 없으면 null.</summary>
    internal UIElement? FooterElement => GetTemplateChild("FooterHost") as UIElement;

    protected override AutomationPeer OnCreateAutomationPeer() => new ConsoleRailAutomationPeer(this);
}

/// <summary>
/// 레일 자동화 peer — 목록 항목 뒤에 <b>바닥 띠의 내용</b>도 자식으로 내놓는다.
/// </summary>
/// <remarks>
/// <see cref="ListBoxAutomationPeer"/> 는 항목만 자식으로 내놓아, 템플릿 바닥 띠(Console.{K}.Rail.Footer · 서버 부대 필터 ·
/// 예하 포함)가 UIA 트리 · 화면 읽기 프로그램에 한 번도 나오지 않았다(2026-09-27 헤디드 시험 SC-SRV-006/007).
/// </remarks>
internal sealed class ConsoleRailAutomationPeer : ListBoxAutomationPeer
{
    public ConsoleRailAutomationPeer(ConsoleRail owner) : base(owner) { }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var children = base.GetChildrenCore() ?? new List<AutomationPeer>();
        var rail = (ConsoleRail)Owner;
        if (rail.FooterElement is { } footer && IsShownWithin(footer, rail))
            AddPeers(footer, children);
        return children;
    }

    /// <summary>
    /// <paramref name="element"/> 부터 <paramref name="root"/> 까지 모두 Visible 인가 — 템플릿 안의 띠를 감싼 칸이 접혀도 숨은 것으로 본다.
    /// </summary>
    /// <remarks><c>IsVisible</c>(실효 가시성)은 쓰지 않는다 — 창 밖(PresentationSource 없음)에서는 모든 요소가 false 라 전부 사라진다.</remarks>
    private static bool IsShownWithin(DependencyObject element, DependencyObject root)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
            if (ReferenceEquals(node, root)) return true;
        }
        return true;
    }

    /// <summary>
    /// peer 가 있는 요소는 그 peer 를, 없으면 그 아래를 — UIElementAutomationPeer 가 자식을 모으는 방식과 같다.
    /// 단, peer 가 없는 요소(<c>StackPanel</c> · <c>Grid</c> …)에 <b>AutomationId 를 달았으면</b> 그 요소를 묶음(Group)으로 내놓는다.
    /// </summary>
    /// <remarks>
    /// 2026-09-28 헤디드 SC-KRN-001: 이벤트 콘솔 바닥 띠는 두 줄(미조치 · 장애 진행)이라 <c>StackPanel</c> 에
    /// <c>Console.Events.Rail.Footer</c> 를 달았는데, 패널은 peer 가 없어 그 id 가 UIA 에 없었다 — 안의 두 글만 id 없이 흩어져 나왔다.
    /// 뷰마다 "peer 있는 요소로 감싸기" 를 기억하게 하지 않고, 커널이 달린 id 를 존중한다.
    /// <para><b>접힌(Collapsed · Hidden) 요소는 그 아래까지 통째로 건너뛴다</b> — id 가 있든 없든, peer 가 있든 없든 같다. 부모에서 내려오며
    /// 거르므로 조상 하나가 접히면 그 아래는 하나도 나오지 않는다(적대 검토 L1: 서버 콘솔의 id 없는 부대 필터 StackPanel 이 6.3 서버에서
    /// 접혀 있는데도 Console.Servers.UnitFilter · IncludeDescendants 가 레일 자식으로 나왔다). 실효 가시성(IsVisible)은 창 밖에서
    /// 모두 false 라 쓰지 않고, 각 요소의 Visibility 를 레일 안에서 위에서부터 따진다.</para>
    /// </remarks>
    internal static void AddPeers(DependencyObject parent, List<AutomationPeer> into)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { Visibility: not Visibility.Visible }) continue;

            if (child is UIElement ui && UIElementAutomationPeer.CreatePeerForElement(ui) is { } peer)
                into.Add(peer);
            else if (child is FrameworkElement group && ConsoleFooterGroupAutomationPeer.For(group) is { } groupPeer)
                into.Add(groupPeer);
            else
                AddPeers(child, into);
        }
    }
}

/// <summary>
/// 레일 바닥 띠 안에서 제 peer 가 없는 요소(패널)에 AutomationId 가 달렸을 때 그 요소를 대신하는 묶음 peer.
/// 자식은 그 요소 아래의 peer 들이다(<see cref="ConsoleRailAutomationPeer.AddPeers"/> 와 같은 규칙).
/// </summary>
internal sealed class ConsoleFooterGroupAutomationPeer : FrameworkElementAutomationPeer
{
    // 같은 요소에는 같은 peer — UIA 는 peer 의 정체(RuntimeId)로 요소를 알아본다. 매번 새로 만들면 이벤트 · 캐시가 끊긴다.
    private static readonly ConditionalWeakTable<FrameworkElement, ConsoleFooterGroupAutomationPeer> Cache = new();

    private ConsoleFooterGroupAutomationPeer(FrameworkElement owner) : base(owner) { }

    /// <summary>AutomationId 가 달린 요소면 그 묶음 peer, 아니면 null(그대로 아래를 훑는다).</summary>
    internal static ConsoleFooterGroupAutomationPeer? For(FrameworkElement element)
        => string.IsNullOrEmpty(AutomationProperties.GetAutomationId(element))
            ? null
            : Cache.GetValue(element, e => new ConsoleFooterGroupAutomationPeer(e));

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override bool IsControlElementCore() => true;

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var children = new List<AutomationPeer>();
        ConsoleRailAutomationPeer.AddPeers(Owner, children);
        return children.Count == 0 ? null : children;
    }
}

/// <summary>레일 항목 컨테이너.</summary>
public class ConsoleRailItem : ListBoxItem
{
    static ConsoleRailItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleRailItem), new FrameworkPropertyMetadata(typeof(ConsoleRailItem)));
    }
}
