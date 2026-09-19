using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

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

    protected override DependencyObject GetContainerForItemOverride() => new ConsoleRailItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ConsoleRailItem;
}

/// <summary>레일 항목 컨테이너.</summary>
public class ConsoleRailItem : ListBoxItem
{
    static ConsoleRailItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleRailItem), new FrameworkPropertyMetadata(typeof(ConsoleRailItem)));
    }
}
