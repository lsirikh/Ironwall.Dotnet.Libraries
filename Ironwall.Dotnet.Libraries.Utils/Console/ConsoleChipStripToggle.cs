using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 칩 줄의 [⌄ 더 보기 +N] / [⌃ 접기] 칩 — <see cref="ConsoleChipStripPanel"/> 이 직접 만들어 줄 끝에 세운다.
/// </summary>
/// <remarks>
/// <para><see cref="ToggleButton"/> 이라 UIA 토글 패턴(Toggle) · Space · Enter 가 그대로 동작한다. 켜짐(<c>IsChecked</c>)이
/// 칩 줄의 <see cref="ConsoleChipStrip.IsExpanded"/> 와 양방향으로 묶인다 — 누르든 자동화로 토글하든 같은 길이다.</para>
/// <para>보조 기술 이름은 보이는 글과 따로 준다: 보이는 글 "더 보기 +3" → 읽는 이름 "더 보기 3개".</para>
/// </remarks>
public class ConsoleChipStripToggle : ToggleButton
{
    /// <summary>접힌 줄의 보이는 글 — "더 보기 +3".</summary>
    public static string MoreText(int hidden) => $"더 보기 +{hidden}";

    /// <summary>접힌 줄의 보조 기술 이름 — "더 보기 3개".</summary>
    public static string MoreAutomationName(int hidden) => $"더 보기 {hidden}개";

    /// <summary>펼친 줄의 글 · 이름.</summary>
    public const string CollapseText = "접기";

    static ConsoleChipStripToggle()
        => DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleChipStripToggle), new FrameworkPropertyMetadata(typeof(ConsoleChipStripToggle)));

    private static readonly DependencyPropertyKey TextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Text), typeof(string), typeof(ConsoleChipStripToggle), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty TextProperty = TextPropertyKey.DependencyProperty;
    /// <summary>보이는 글("더 보기 +N" 또는 "접기").</summary>
    public string Text => (string)GetValue(TextProperty);

    private static readonly DependencyPropertyKey HiddenCountPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HiddenCount), typeof(int), typeof(ConsoleChipStripToggle), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty HiddenCountProperty = HiddenCountPropertyKey.DependencyProperty;
    /// <summary>접힌 줄에서 가려진 칩 수(펼치면 0).</summary>
    public int HiddenCount => (int)GetValue(HiddenCountProperty);

    /// <summary>글 · 이름 · 가려진 수를 한 번에 맞춘다(같은 값이면 아무 일도 없다).</summary>
    internal void Show(bool expanded, int hidden)
    {
        var text = expanded ? CollapseText : MoreText(hidden);
        var name = expanded ? CollapseText : MoreAutomationName(hidden);
        if (!Equals(Text, text)) SetValue(TextPropertyKey, text);
        if (HiddenCount != (expanded ? 0 : hidden)) SetValue(HiddenCountPropertyKey, expanded ? 0 : hidden);
        if (!Equals(AutomationProperties.GetName(this), name)) AutomationProperties.SetName(this, name);
    }
}
