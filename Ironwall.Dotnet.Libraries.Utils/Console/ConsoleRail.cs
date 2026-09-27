using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
    /// </remarks>
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        // 처리기(자동화 이벤트 포함)가 먼저 사용자의 변경을 보고, 그다음 되돌림을 본다 — 마지막으로 보는 것이 실제 상태가 된다.
        base.OnSelectionChanged(e);
        ReconcileSelection(e);
    }

    private bool _reconciling;

    private void ReconcileSelection(SelectionChangedEventArgs e)
    {
        if (_reconciling || SelectionMode != SelectionMode.Single) return;

        var bound = SelectedItem;
        var shown = SelectedItems.Count > 0 ? SelectedItems[0] : null;
        if (Equals(bound, shown)) return;       // 한 가지 상태 — 평소의 모든 선택 변경

        var index = bound is null ? -1 : Items.IndexOf(bound);
        if (bound is not null && index < 0) return;   // 목록에 없는 값 — 맞출 자리가 없다

        // 누른 항목에 있던 키보드 초점도 원래 항목으로 옮긴다(화살표가 막힌 레일에서 출발하지 않게).
        var focusWasOnPick = e.AddedItems.Count > 0
                             && ItemContainerGenerator.ContainerFromItem(e.AddedItems[0]) is UIElement { IsKeyboardFocusWithin: true };

        _reconciling = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _reconciling = false;
        }

        if (focusWasOnPick && bound is not null && ItemContainerGenerator.ContainerFromItem(bound) is UIElement original)
            original.Focus();
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
        if (((ConsoleRail)Owner).FooterElement is { Visibility: Visibility.Visible } footer)
            AddPeers(footer, children);
        return children;
    }

    /// <summary>peer 가 있는 요소는 그 peer 를, 없으면 그 아래를 — UIElementAutomationPeer 가 자식을 모으는 방식과 같다.</summary>
    private static void AddPeers(DependencyObject parent, List<AutomationPeer> into)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement ui && UIElementAutomationPeer.CreatePeerForElement(ui) is { } peer)
                into.Add(peer);
            else
                AddPeers(child, into);
        }
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
