using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Theme.Themes;

/// <summary>
/// 줄임표(…)로 잘린 글자에만 전체 글자를 툴팁으로 보여 준다 — <c>theme:TrimmedToolTip.IsEnabled="True"</c>.
/// </summary>
/// <remarks>
/// <para>U-18 잘림 감사에서 "줄임표는 찍혔는데 전체 글자를 볼 길이 없는" 곳이 여럿 나왔다(상태 띠 안내 · 검색 안내 글 · 목록 칸).
/// 줄임표는 폭이 모자랄 때의 정당한 모양이지만 <b>전체 글자에 닿는 길</b>이 같이 있어야 한다.</para>
/// <para>늘 툴팁을 달면 잘리지 않은 글자에도 같은 글이 한 번 더 뜬다(다크에서 밝은 상자가 튀는 불만 — U-17 ✕ 툴팁).
/// 그래서 툴팁이 뜨려는 순간(<see cref="FrameworkElement.ToolTipOpening"/>) 실제로 잘렸는지 재고, 안 잘렸으면 띄우지 않는다.</para>
/// <para>글자 블록 자신에 걸거나, 글자를 템플릿 안에 가진 컨트롤(검색 상자)에 걸고 <see cref="PartNameProperty"/> 로
/// 그 글자 블록의 이름을 준다(기본 <c>Hint</c>). 창이 이미 단 다른 툴팁(꺼진 사유 등)은 건드리지 않는다.</para>
/// </remarks>
public static class TrimmedToolTip
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TrimmedToolTip), new PropertyMetadata(false, OnIsEnabledChanged));
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>컨트롤에 걸었을 때 글자를 가진 템플릿 부품의 이름(기본 <c>Hint</c>).</summary>
    public static readonly DependencyProperty PartNameProperty = DependencyProperty.RegisterAttached(
        "PartName", typeof(string), typeof(TrimmedToolTip), new PropertyMetadata("Hint"));
    public static string GetPartName(DependencyObject element) => (string)element.GetValue(PartNameProperty);
    public static void SetPartName(DependencyObject element, string value) => element.SetValue(PartNameProperty, value);

    // 이 도우미가 단 툴팁인가 — 창이 단 툴팁을 덮지 않기 위해 마지막으로 쓴 글을 기억한다.
    private static readonly DependencyProperty OwnedTextProperty = DependencyProperty.RegisterAttached(
        "OwnedText", typeof(string), typeof(TrimmedToolTip), new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.ToolTipOpening -= OnToolTipOpening;
        if (e.NewValue is not true) return;

        element.ToolTipOpening += OnToolTipOpening;
        // 툴팁이 null 이면 ToolTipOpening 자체가 오지 않는다 — 빈 글로 자리만 잡아 둔다(여는 순간 채우거나 막는다).
        if (element.ToolTip is null)
        {
            element.SetCurrentValue(FrameworkElement.ToolTipProperty, string.Empty);
            element.SetValue(OwnedTextProperty, string.Empty);
        }
    }

    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        // 창이 단 툴팁(글이 아니거나, 이 도우미가 쓴 글이 아니면)은 그대로 둔다.
        var owned = (string?)element.GetValue(OwnedTextProperty);
        if (owned is null || element.ToolTip is not string current || current != owned) return;

        var block = element as TextBlock
                    ?? (element as Control)?.Template?.FindName(GetPartName(element), (Control)element) as TextBlock;
        if (block is null || !block.IsVisible || !IsTrimmed(block))
        {
            e.Handled = true;          // 잘리지 않았다 — 띄우지 않는다
            return;
        }

        var text = block.Text ?? string.Empty;
        element.SetCurrentValue(FrameworkElement.ToolTipProperty, text);
        element.SetValue(OwnedTextProperty, text);
    }

    /// <summary>글자 블록이 지금 줄임표로 잘렸는가 — 레이아웃을 건드리지 않고 FormattedText 로만 잰다.</summary>
    public static bool IsTrimmed(TextBlock block)
    {
        if (block.TextTrimming == TextTrimming.None || string.IsNullOrEmpty(block.Text)) return false;

        var formatted = new FormattedText(
            block.Text, CultureInfo.CurrentUICulture, block.FlowDirection,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize,
            Brushes.Black, null, TextOptions.GetTextFormattingMode(block), VisualTreeHelper.GetDpi(block).PixelsPerDip);

        var width = block.ActualWidth - block.Padding.Left - block.Padding.Right;
        if (block.TextWrapping == TextWrapping.NoWrap) return formatted.Width > width + 0.5;

        formatted.MaxTextWidth = Math.Max(1, width);
        return formatted.Height > block.ActualHeight - block.Padding.Top - block.Padding.Bottom + 0.5;
    }
}
