using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// "?" 말풍선의 몸 — 제목(굵게) + 글머리 항목 + (선택) 소제목 · 단축키 칩, 꼬리가 "?" 를 가리킨다(스토리보드 help-callout §공용 부품).
/// </summary>
/// <remarks>
/// <para>겉(둥근 카드 + 꼬리)은 <see cref="OnRender"/> 가 <b>한 윤곽선</b>으로 그린다 — 꼬리와 카드 사이에 이음매가 없다.
/// 색은 스타일의 <c>DynamicResource</c> 토큰(<see cref="Control.Background"/> · <see cref="Control.BorderBrush"/>)이라 테마를 바꾸면 다시 칠해진다.
/// 안의 글 요소도 토큰을 <see cref="FrameworkElement.SetResourceReference"/> 로 건다(한 번 풀어 캐싱하지 않는다).</para>
/// <para>바깥 크기는 꼬리 자리(<see cref="HelpCalloutPlacement.TailMargin"/>)를 네 변에 늘 포함한다 — 어느 쪽에 뜨든 크기가 같다.</para>
/// <para>자동화: 이름 = 내용 평문(제목 · 항목). AutomationId 는 여는 "?" 가 <c>Help.{키}.Body</c> 로 건다.</para>
/// </remarks>
public class HelpCallout : Control
{
    /// <summary>카드 최대 폭(꼬리 자리 제외) — PRD FR-01.</summary>
    public const double MaxCardWidth = 420;

    private const double CornerRadius = 10;
    private const double TailHalfWidth = 7;

    private StackPanel? _body;

    static HelpCallout()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HelpCallout), new FrameworkPropertyMetadata(typeof(HelpCallout)));
        FocusableProperty.OverrideMetadata(typeof(HelpCallout), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(
        nameof(Entry), typeof(HelpEntry), typeof(HelpCallout),
        new PropertyMetadata(null, (d, _) => ((HelpCallout)d).Rebuild()));
    /// <summary>보일 내용. 비우면 몸이 빈다.</summary>
    public HelpEntry? Entry { get => (HelpEntry?)GetValue(EntryProperty); set => SetValue(EntryProperty, value); }

    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(
        nameof(Side), typeof(HelpCalloutSide), typeof(HelpCallout),
        new FrameworkPropertyMetadata(HelpCalloutSide.Below, FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>"?" 의 어느 쪽에 떴는가 — 꼬리는 반대 모서리.</summary>
    public HelpCalloutSide Side { get => (HelpCalloutSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }

    public static readonly DependencyProperty TailOffsetProperty = DependencyProperty.Register(
        nameof(TailOffset), typeof(double), typeof(HelpCallout),
        new FrameworkPropertyMetadata(HelpCalloutPlacement.TailMargin + HelpCalloutPlacement.PreferredTailInset, FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>꼬리 중심의 자리(바깥 상자 기준) — 위/아래 꼬리면 x, 좌/우 꼬리면 y.</summary>
    public double TailOffset { get => (double)GetValue(TailOffsetProperty); set => SetValue(TailOffsetProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _body = GetTemplateChild("PART_Body") as StackPanel;
        Rebuild();
    }

    #region - Content -
    private void Rebuild()
    {
        if (_body is null) return;
        _body.Children.Clear();
        if (Entry is not { } entry) return;

        // 제목 줄 — 주색 "?" + 굵은 제목
        var title = NewText(13, FontWeights.SemiBold, "TextPrimaryBrush");
        var mark = new Run("? ") { FontWeight = FontWeights.Bold };
        mark.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
        title.Inlines.Add(mark);
        AddRuns(title, entry.Title);
        title.Margin = new Thickness(0, 0, 0, 6);
        _body.Children.Add(title);

        var first = true;
        foreach (var section in entry.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                var heading = NewText(11.5, FontWeights.SemiBold, "TextMutedBrush");
                heading.Margin = new Thickness(0, first ? 0 : 8, 0, 4);
                AddRuns(heading, section.Heading);
                _body.Children.Add(heading);
            }
            foreach (var item in section.Items) _body.Children.Add(Bullet(item));
            first = false;
        }
    }

    private static FrameworkElement Bullet(string item)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 글머리는 색이 아니라 형태(작은 원) — 주색은 덧칠일 뿐이다.
        var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Margin = new Thickness(2, 7, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
        dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "PrimaryBrush");
        grid.Children.Add(dot);

        var text = NewText(12.5, FontWeights.Normal, "TextPrimaryBrush");
        AddRuns(text, item);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static TextBlock NewText(double size, FontWeight weight, string brushKey)
    {
        var text = new TextBlock { FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, LineHeight = Math.Round(size * 1.5) };
        text.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return text;
    }

    /// <summary>표기를 조각으로 — 글은 한글 음절 줄바꿈 커널(<see cref="KoreanWordWrap.Join"/>)을 거친다(인라인이라 전역 설치가 손대지 않는다).</summary>
    private static void AddRuns(TextBlock text, string? source)
    {
        foreach (var run in HelpText.Parse(source))
        {
            switch (run.Kind)
            {
                case HelpRunKind.Key:
                    text.Inlines.Add(new InlineUIContainer(KeyChip(run.Text)) { BaselineAlignment = BaselineAlignment.Center });
                    break;
                case HelpRunKind.Bold:
                    text.Inlines.Add(new Run(KoreanWordWrap.Join(run.Text)) { FontWeight = FontWeights.Bold });
                    break;
                default:
                    text.Inlines.Add(new Run(KoreanWordWrap.Join(run.Text)));
                    break;
            }
        }
    }

    private static FrameworkElement KeyChip(string key)
    {
        var label = new TextBlock { Text = key, FontSize = 11.5 };
        label.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFont");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        var chip = new Border { Padding = new Thickness(4, 0, 4, 0), BorderThickness = new Thickness(1), CornerRadius = new System.Windows.CornerRadius(4), Child = label };
        chip.SetResourceReference(Border.BackgroundProperty, "SurfaceSunkenBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return chip;
    }
    #endregion

    #region - Chrome (card + tail, one outline) -
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size = RenderSize;
        if (size.Width <= 2 * HelpCalloutPlacement.TailMargin || size.Height <= 2 * HelpCalloutPlacement.TailMargin) return;
        var pen = BorderBrush is { } stroke ? new Pen(stroke, 1) : null;
        dc.DrawGeometry(Background, pen, Outline(size, Side, TailOffset));
    }

    /// <summary>카드(둥근 사각형) + 꼬리(삼각형)를 한 윤곽선으로 — 꼬리는 <paramref name="side"/> 의 반대 모서리에서 바깥으로.</summary>
    internal static Geometry Outline(Size size, HelpCalloutSide side, double tailOffset)
    {
        var m = HelpCalloutPlacement.TailMargin;
        var card = new Rect(m + 0.5, m + 0.5, size.Width - 2 * m - 1, size.Height - 2 * m - 1);
        var body = new RectangleGeometry(card, CornerRadius, CornerRadius);

        var tail = new StreamGeometry();
        using (var g = tail.Open())
        {
            switch (side)
            {
                case HelpCalloutSide.Below:      // 꼬리는 위 모서리
                    g.BeginFigure(new Point(tailOffset - TailHalfWidth, card.Top + 1), true, true);
                    g.LineTo(new Point(tailOffset, card.Top - m + 1), true, false);
                    g.LineTo(new Point(tailOffset + TailHalfWidth, card.Top + 1), true, false);
                    break;
                case HelpCalloutSide.Above:      // 꼬리는 아래 모서리
                    g.BeginFigure(new Point(tailOffset - TailHalfWidth, card.Bottom - 1), true, true);
                    g.LineTo(new Point(tailOffset, card.Bottom + m - 1), true, false);
                    g.LineTo(new Point(tailOffset + TailHalfWidth, card.Bottom - 1), true, false);
                    break;
                case HelpCalloutSide.Left:       // 꼬리는 오른쪽 모서리
                    g.BeginFigure(new Point(card.Right - 1, tailOffset - TailHalfWidth), true, true);
                    g.LineTo(new Point(card.Right + m - 1, tailOffset), true, false);
                    g.LineTo(new Point(card.Right - 1, tailOffset + TailHalfWidth), true, false);
                    break;
                default:                          // Right — 꼬리는 왼쪽 모서리
                    g.BeginFigure(new Point(card.Left + 1, tailOffset - TailHalfWidth), true, true);
                    g.LineTo(new Point(card.Left - m + 1, tailOffset), true, false);
                    g.LineTo(new Point(card.Left + 1, tailOffset + TailHalfWidth), true, false);
                    break;
            }
        }
        tail.Freeze();
        return new CombinedGeometry(GeometryCombineMode.Union, body, tail);
    }
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new HelpCalloutAutomationPeer(this);
}

/// <summary>말풍선 몸의 자동화 — 이름은 내용 평문(헤디드 시험이 이름으로 글을 읽는다).</summary>
public sealed class HelpCalloutAutomationPeer : FrameworkElementAutomationPeer
{
    public HelpCalloutAutomationPeer(HelpCallout owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(HelpCallout);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? ((HelpCallout)Owner).Entry?.ToPlainText() ?? string.Empty : name;
    }
}
