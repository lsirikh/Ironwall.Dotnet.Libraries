using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace PreviewTools.Shared;

/// <summary>
/// 잘림 감사 — 미리보기가 한 장을 찍을 때마다(<c>--audit</c> 일 때만) 시각 트리를 걸어
/// "사람 눈에 잘려 보이는 것" 을 기계가 읽을 수 있는 표(TSV)로 남긴다.
/// </summary>
/// <remarks>
/// <para>잡는 것</para>
/// <list type="bullet">
/// <item><c>TEXT_CLIP</c> — 줄임표 없이 글자가 잘린다(제 폭 · 조상의 클립 · 고정 높이). 글자 잉크 사각을 FormattedText 로 재어
/// 실제로 보이는 사각(자신 + 조상들의 클립 교집합)과 비교한다.</item>
/// <item><c>TEXT_TRIM</c> — 줄임표로 잘렸는데 전체 글자를 보여 줄 툴팁이 없다. (툴팁이 있으면 <c>TEXT_TRIM_TIP</c> — 의도된 것, 결함 아님)</item>
/// <item><c>BUTTON_CLIP</c> — 버튼(ButtonBase: 버튼 · 토글 · 칩 · 체크)이 가장 가까운 스크롤 뷰포트 · 클립 컨테이너 · 콘솔 루트 안에 다 들지 않는다.</item>
/// <item><c>BUTTON_OVERLAP</c> — 버튼이 다른 버튼이나 제 밖의 글자와 겹친다.</item>
/// <item><c>HSCROLL</c> — 가로 스크롤막대가 서 있다(목록 · 그리드면 기본 크기에서 결함).</item>
/// <item><c>TYPENAME</c> — 화면에 형식 이름이 그대로 찍혔다(".ViewModels." 등 — 뷰를 못 찾은 ContentControl).</item>
/// </list>
/// <para>세로로 굴릴 수 있는 스크롤 뷰어 안의 잘림은 결함이 아니다 — 스크롤할 수 있는 축은 클립에서 뺀다.
/// 가로로 굴릴 수 있는 축도 여기서는 빼고, 대신 그 스크롤막대를 <c>HSCROLL</c> 한 줄로 적는다.</para>
/// <para>이 도구는 레이아웃을 건드리지 않는다(Measure 를 다시 부르지 않는다) — 읽기만 한다.</para>
/// </remarks>
public static class ClipAudit
{
    private const double HorizontalTolerance = 1.5;
    private const double VerticalTolerance = 3.0;

    private static readonly Regex TypeNamePattern = new(
        @"(\.ViewModels\.|\.Views\.|\.Models\.|\bIronwall\.Dotnet\.|\bSystem\.[A-Z]\w+\.|\bCaliburn\.|\bMaterialDesign\w*\.|DependencyProperty\.UnsetValue|^[A-Z]\w*(ViewModel|Dto)$)",
        RegexOptions.Compiled);

    /// <summary><c>--audit</c> 가 명령줄에 있는가 — 도구마다 따로 설정하지 않아도 된다.</summary>
    public static bool Enabled { get; } = Environment.GetCommandLineArgs().Contains("--audit");

    private sealed record Finding(string Kind, string Path, string AutomationId, string Text, string Detail, string Clipper, int Row);

    /// <summary>한 장(프레임)을 감사하고 <c>clip-audit.tsv</c> · <c>clip-audit-frames.tsv</c> 에 덧붙인다.</summary>
    public static void Frame(string directory, string frame, FrameworkElement root)
    {
        if (!Enabled) return;

        OffscreenStage.GuardOffscreen(directory, frame);

        try
        {
            root.UpdateLayout();
            var findings = Audit(root);
            Write(directory, frame, root, findings);
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(directory, "clip-audit-error.txt"), $"{frame}: {ex}{Environment.NewLine}");
        }
    }

    #region - Audit -
    private static List<Finding> Audit(FrameworkElement root)
    {
        var findings = new List<Finding>();
        var rootRect = new Rect(0, 0, root.ActualWidth, root.ActualHeight);

        var texts = new List<(TextBlock Block, Rect Ink)>();
        var buttons = new List<(ButtonBase Button, Rect Bounds, Rect Visible)>();

        foreach (var element in Walk(root))
        {
            if (element is not FrameworkElement fe || !fe.IsVisible || fe.ActualWidth <= 0 || fe.ActualHeight <= 0) continue;
            if (IsTransparent(fe, root)) continue;

            switch (fe)
            {
                case TextBlock text:
                    AuditText(text, root, rootRect, findings, texts);
                    break;
                // 그리드 오른쪽 빈 머리(PART_FillerColumnHeader)는 남는 폭을 채우는 장식이다 — 버튼으로 세지 않는다.
                case ButtonBase button when !IsInside<ScrollBar>(button, root) && button.Name != "PART_FillerColumnHeader":
                    {
                        var bounds = BoundsIn(button, root);
                        var visible = VisibleRect(button, root, rootRect, out var clipper);
                        // 겹침은 굴려서 닿는 사각이 아니라 지금 실제로 보이는 사각으로 본다.
                        buttons.Add((button, bounds, VisibleRect(button, root, rootRect, out _, freeScroll: false)));
                        if (visible.IsEmpty || visible.Width < bounds.Width - HorizontalTolerance || visible.Height < bounds.Height - HorizontalTolerance)
                        {
                            findings.Add(new Finding("BUTTON_CLIP", PathOf(button, root), AutomationIdOf(button), ButtonText(button),
                                $"bounds={Fmt(bounds)} visible={(visible.IsEmpty ? "empty" : Fmt(visible))}", clipper, RowOf(button)));
                        }
                        break;
                    }
                case ScrollViewer viewer when viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible:
                    {
                        var owner = viewer.TemplatedParent as FrameworkElement ?? viewer;
                        var isList = owner is ItemsControl;
                        findings.Add(new Finding(isList ? "HSCROLL" : "HSCROLL_OTHER", PathOf(owner, root), AutomationIdOf(owner), string.Empty,
                            $"owner={owner.GetType().Name} viewport={viewer.ViewportWidth:0.#} extent={viewer.ExtentWidth:0.#} scrollable={viewer.ScrollableWidth:0.#}",
                            string.Empty, -1));
                        break;
                    }
            }
        }

        // 겹침 — 버튼끼리, 버튼과 제 밖의 글자.
        for (var i = 0; i < buttons.Count; i++)
        {
            var (a, _, va) = buttons[i];
            if (va.IsEmpty) continue;

            for (var j = i + 1; j < buttons.Count; j++)
            {
                var (b, _, vb) = buttons[j];
                if (vb.IsEmpty || IsAncestor(a, b) || IsAncestor(b, a) || SameTemplate(a, b) || IsOccluded(a, b) || IsOccluded(b, a)) continue;
                var overlap = Rect.Intersect(va, vb);
                if (overlap.IsEmpty || overlap.Width <= 2 || overlap.Height <= 2) continue;
                findings.Add(new Finding("BUTTON_OVERLAP", PathOf(a, root), AutomationIdOf(a), ButtonText(a),
                    $"with={PathTail(b, root)} [{AutomationIdOf(b)}] '{ButtonText(b)}' overlap={Fmt(overlap)}", string.Empty, RowOf(a)));
            }

            foreach (var (block, ink) in texts)
            {
                if (IsAncestor(a, block) || SameTemplate(a, block) || IsOccluded(block, a) || IsOccluded(a, block)) continue;
                if (FindAncestor<ButtonBase>(block, root) is { } owner && (IsAncestor(owner, a) || IsAncestor(a, owner))) continue;
                var overlap = Rect.Intersect(va, ink);
                if (overlap.IsEmpty || overlap.Width <= 2 || overlap.Height <= 2) continue;
                findings.Add(new Finding("BUTTON_OVERLAP", PathOf(a, root), AutomationIdOf(a), ButtonText(a),
                    $"text='{Clean(block.Text, 40)}' at {PathTail(block, root)} overlap={Fmt(overlap)}", string.Empty, RowOf(a)));
            }
        }

        return findings;
    }

    private static void AuditText(TextBlock block, FrameworkElement root, Rect rootRect, List<Finding> findings, List<(TextBlock, Rect)> texts)
    {
        var content = TextOf(block);
        if (string.IsNullOrWhiteSpace(content)) return;

        if (TypeNamePattern.IsMatch(content))
            findings.Add(new Finding("TYPENAME", PathOf(block, root), AutomationIdOf(block), Clean(content, 80), string.Empty, string.Empty, RowOf(block)));

        var padding = block.Padding;
        var innerWidth = Math.Max(0, block.ActualWidth - padding.Left - padding.Right);
        var innerHeight = Math.Max(0, block.ActualHeight - padding.Top - padding.Bottom);
        var wraps = block.TextWrapping != TextWrapping.NoWrap;

        var formatted = Format(block, content, wraps ? Math.Max(1, innerWidth) : 0);
        // WPF 는 줄바꿈 없는 글자 블록이 다 못 보일 때 RenderSize 를 "글자 본래 폭" 으로 잡고 레이아웃 클립을 건다 —
        // 그래서 제 폭을 넘는 FormattedText 값은 기호 글꼴 대체(▶ · ●)에서 생기는 재기 오차다. 제 폭으로 상한을 둔다.
        var trimmedBlock = block.TextTrimming != TextTrimming.None;
        var textWidth = trimmedBlock ? formatted.Width : Math.Min(formatted.Width, innerWidth);
        var textHeight = trimmedBlock ? formatted.Height : Math.Min(formatted.Height, Math.Max(innerHeight, 1));

        // 글자 잉크가 차지하는 사각(블록 좌표) — 정렬을 따른다.
        var inkWidth = wraps ? Math.Min(textWidth, innerWidth) : textWidth;
        var x = block.TextAlignment switch
        {
            TextAlignment.Right => block.ActualWidth - padding.Right - inkWidth,
            TextAlignment.Center => padding.Left + (innerWidth - inkWidth) / 2,
            _ => padding.Left,
        };
        var trimmed = block.TextTrimming != TextTrimming.None;
        var inkLocal = new Rect(x, padding.Top, trimmed ? Math.Min(inkWidth, innerWidth) : inkWidth, trimmed ? Math.Min(textHeight, innerHeight) : textHeight);

        var toRoot = block.TransformToAncestor(root);
        var ink = toRoot.TransformBounds(inkLocal);
        var visible = VisibleRect(block, root, rootRect, out var clipper, includeSelfBounds: false);
        var shown = visible.IsEmpty ? Rect.Empty : Rect.Intersect(ink, visible);
        var actual = VisibleRect(block, root, rootRect, out _, includeSelfBounds: false, freeScroll: false);
        var onScreen = actual.IsEmpty ? Rect.Empty : Rect.Intersect(ink, actual);
        texts.Add((block, onScreen.IsEmpty ? Rect.Empty : onScreen));

        // 줄임표 — 전체 글자가 들어갈 자리가 없다.
        if (trimmed)
        {
            var isTrimmed = wraps ? textHeight > innerHeight + 1 : textWidth > innerWidth + 0.5;
            if (isTrimmed)
            {
                var tip = HasToolTip(block, root);
                findings.Add(new Finding(tip ? "TEXT_TRIM_TIP" : "TEXT_TRIM", PathOf(block, root), AutomationIdOf(block), Clean(content, 80),
                    $"need={textWidth:0.#}x{textHeight:0.#} have={innerWidth:0.#}x{innerHeight:0.#}", string.Empty, RowOf(block)));
            }
        }

        // 줄임표 없이 잘림 — 잉크 사각이 보이는 사각 안에 다 들지 않는다.
        var clippedWide = shown.IsEmpty || shown.Width < ink.Width - HorizontalTolerance;
        var clippedTall = shown.IsEmpty || shown.Height < ink.Height - VerticalTolerance;
        if (clippedWide || clippedTall)
        {
            // 자기 폭보다 넓은 글자가 클립 없이 흘러넘쳐도 결함이다(옆 요소를 덮는다) — 그 경우 clipper 는 self.
            findings.Add(new Finding("TEXT_CLIP", PathOf(block, root), AutomationIdOf(block), Clean(content, 80),
                $"{(clippedWide ? "W" : string.Empty)}{(clippedTall ? "H" : string.Empty)} ink={Fmt(ink)} shown={(shown.IsEmpty ? "empty" : Fmt(shown))} font={block.FontSize:0.#}",
                clipper, RowOf(block)));
        }
    }

    /// <summary>
    /// 요소가 실제로 보이는 사각(루트 좌표) — 자신과 조상들의 클립(ClipToBounds · 레이아웃 클립 · Clip · 스크롤 뷰포트)의 교집합.
    /// 굴릴 수 있는 축은 뷰포트 클립에서 뺀다(굴리면 보인다).
    /// </summary>
    internal static Rect VisibleRect(FrameworkElement element, FrameworkElement root, Rect rootRect, out string clipper, bool includeSelfBounds = true, bool freeScroll = true)
    {
        // 글자는 제 사각이 아니라 잉크 사각을 따로 비교한다 — 자신의 레이아웃 클립(아래 고리)만 적용한다.
        var visible = includeSelfBounds ? BoundsIn(element, root) : new Rect(-1e6, -1e6, 2e6, 2e6);
        clipper = string.Empty;

        // 굴릴 수 있는 뷰포트를 한 번 지나면 그 축은 끝까지(바깥 클립 · 루트 포함) 풀어 둔다 — 굴리면 들어온다.
        bool freeV = false, freeH = false;
        Rect Free(Rect r)
        {
            if (freeV) r = new Rect(r.X, -1e6, r.Width, 2e6);
            if (freeH) r = new Rect(-1e6, r.Y, 2e6, r.Height);
            return r;
        }

        for (DependencyObject? current = element; current is not null && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
        {
            if (current is not UIElement ui) continue;

            Rect? clip = null;
            if (current is ScrollContentPresenter presenter)
            {
                var viewer = presenter.TemplatedParent as ScrollViewer ?? FindAncestor<ScrollViewer>(presenter, root);
                if (freeScroll && viewer is not null && viewer.ScrollableHeight > 0.5) freeV = true;
                if (freeScroll && viewer is not null && viewer.ScrollableWidth > 0.5) freeH = true;
                clip = BoundsIn(presenter, root);
            }
            else if (current is DataGridColumnHeadersPresenter headers)
            {
                // 머리 줄은 스크롤 뷰포트 밖에서 가로 오프셋만 따라간다 — 그리드가 가로로 굴러가면 머리도 굴리면 닿는다(HSCROLL 한 줄로 적는다).
                var viewer = FindAncestor<ScrollViewer>(headers, root);
                if (freeScroll && viewer is not null && viewer.ScrollableWidth > 0.5) freeH = true;
                var geometry = LayoutInformation.GetLayoutClip(headers);
                if (geometry is not null && !geometry.Bounds.IsEmpty) clip = headers.TransformToAncestor(root).TransformBounds(geometry.Bounds);
            }
            else
            {
                // FrameworkElement 의 레이아웃 클립은 ClipToBounds 와 "제 자리보다 크게 재어져 잘린 것" 을 둘 다 담는다.
                var geometry = ui is FrameworkElement fe ? LayoutInformation.GetLayoutClip(fe)
                             : ui.ClipToBounds ? new RectangleGeometry(new Rect(ui.RenderSize)) : null;
                if (geometry is not null && !geometry.Bounds.IsEmpty)
                    clip = ui.TransformToAncestor(root).TransformBounds(geometry.Bounds);

                if (ui.Clip is { } explicitClip && !explicitClip.Bounds.IsEmpty)
                {
                    var c = ui.TransformToAncestor(root).TransformBounds(explicitClip.Bounds);
                    clip = clip is null ? c : Rect.Intersect(clip.Value, c);
                }

                // 가로로 굴러간 그리드에서 고정 열 밑으로 미끄러진 칸 · 머리는 칸 패널이 제 폭보다 좁게 자른다 — 굴림 상태다(HSCROLL 로 센다).
                if (freeScroll && clip is { } cellClip && current is DataGridCell or DataGridColumnHeader
                    && cellClip.Width < BoundsIn((FrameworkElement)current, root).Width - 0.5
                    && FindAncestor<ScrollViewer>(current, root) is { ScrollableWidth: > 0.5 })
                    clip = new Rect(-1e6, cellClip.Y, 2e6, cellClip.Height);
            }

            if (clip is null) continue;
            clip = Free(clip.Value);
            var before = visible;
            visible = Rect.Intersect(visible, clip.Value);
            if (visible.IsEmpty) { clipper = $"{Describe(current)} {clip.Value.Width:0.#}x{clip.Value.Height:0.#}"; return Rect.Empty; }
            // 가장 가까운(처음) 클립 조상을 적는다 — 고칠 자리다.
            if (string.IsNullOrEmpty(clipper) && (before.Width - visible.Width > 0.5 || before.Height - visible.Height > 0.5))
                clipper = $"{Describe(current)} {clip.Value.Width:0.#}x{clip.Value.Height:0.#}";
        }

        var final = Rect.Intersect(visible, Free(rootRect));
        if (final.IsEmpty) { if (string.IsNullOrEmpty(clipper)) clipper = "root"; return Rect.Empty; }
        if (string.IsNullOrEmpty(clipper) && (final.Width < visible.Width - 0.5 || final.Height < visible.Height - 0.5)) clipper = "root";
        return final;
    }
    #endregion

    #region - Text -
    private static string TextOf(TextBlock block)
    {
        if (block.Inlines.Count == 0) return block.Text ?? string.Empty;
        var builder = new StringBuilder();
        foreach (var inline in block.Inlines)
        {
            switch (inline)
            {
                case Run run: builder.Append(run.Text); break;
                case LineBreak: builder.Append('\n'); break;
                case Span span: builder.Append(new TextRange(span.ContentStart, span.ContentEnd).Text); break;
            }
        }
        return builder.ToString();
    }

    private static FormattedText Format(TextBlock block, string content, double maxWidth)
    {
        var dpi = VisualTreeHelper.GetDpi(block);
        var typeface = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
        var formatted = new FormattedText(content, CultureInfo.CurrentUICulture, block.FlowDirection, typeface, block.FontSize,
            Brushes.Black, null, TextOptions.GetTextFormattingMode(block), dpi.PixelsPerDip);
        if (maxWidth > 0) formatted.MaxTextWidth = maxWidth;
        if (!double.IsNaN(block.LineHeight) && block.LineHeight > 0) formatted.LineHeight = block.LineHeight;

        // 굵기 · 크기가 다른 Run 은 그 범위만 따로 적용한다(요약 굵은 글자 등).
        if (block.Inlines.Count > 0)
        {
            var at = 0;
            foreach (var inline in block.Inlines)
            {
                var length = inline switch { Run run => run.Text.Length, LineBreak => 1, Span span => new TextRange(span.ContentStart, span.ContentEnd).Text.Length, _ => 0 };
                if (length > 0 && at + length <= content.Length)
                {
                    formatted.SetFontWeight(inline.FontWeight, at, length);
                    formatted.SetFontSize(inline.FontSize, at, length);
                    formatted.SetFontFamily(inline.FontFamily, at, length);
                }
                at += length;
            }
        }
        return formatted;
    }

    private static bool HasToolTip(FrameworkElement element, FrameworkElement root)
    {
        var depth = 0;
        for (DependencyObject? d = element; d is not null && !ReferenceEquals(d, root) && depth < 8; d = VisualTreeHelper.GetParent(d), depth++)
        {
            if (d is FrameworkElement fe && fe.ToolTip is not null) return true;
            if (d is DataGridCell or ListBoxItem or DataGridRow) return d is FrameworkElement { ToolTip: not null };
        }
        return false;
    }

    private static string ButtonText(ButtonBase button)
    {
        if (button.Content is string s) return Clean(s, 40);
        var first = Walk(button).OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
        return first is null ? string.Empty : Clean(first.Text, 40);
    }
    #endregion

    #region - Tree helpers -
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            if (current is UIElement { Visibility: not Visibility.Visible }) continue;
            var count = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(current) : 0;
            for (var i = count - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(current, i));
        }
    }

    private static bool IsTransparent(UIElement element, FrameworkElement root)
    {
        for (DependencyObject? d = element; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UIElement ui && ui.Opacity <= 0.01) return true;
            if (ReferenceEquals(d, root)) break;
        }
        return false;
    }

    private static Rect BoundsIn(FrameworkElement element, Visual root)
        => element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject descendant)
    {
        for (var d = VisualTreeHelper.GetParent(descendant); d is not null; d = VisualTreeHelper.GetParent(d))
            if (ReferenceEquals(d, ancestor)) return true;
        return false;
    }

    private static T? FindAncestor<T>(DependencyObject element, DependencyObject root) where T : DependencyObject
    {
        for (var d = VisualTreeHelper.GetParent(element); d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is T hit) return hit;
        return null;
    }

    /// <summary>
    /// 템플릿 부품끼리인가 — 콤보의 펼침 토글과 그 선택 글자처럼, 한 컨트롤의 템플릿 안에서 일부러 포갠 것은 겹침이 아니다.
    /// </summary>
    private static bool SameTemplate(FrameworkElement button, DependencyObject other)
        => button.TemplatedParent is DependencyObject owner && owner is not DataGrid && IsAncestor(owner, other);

    /// <summary>
    /// <paramref name="lower"/> 가 <paramref name="upper"/> 쪽 층에 가려 보이지 않는가 — 두 요소가 갈라지는 패널에서 위 쪽 가지가 나중에 그려지고
    /// (z 순서) 그 가지의 루트부터 <paramref name="upper"/> 까지 어딘가에 불투명 배경이 있으면 아래 것은 덮인 것이다(서랍 · 떠 있는 판).
    /// </summary>
    private static bool IsOccluded(DependencyObject lower, DependencyObject upper)
    {
        var upperChain = new List<DependencyObject>();
        for (DependencyObject? d = upper; d is not null; d = VisualTreeHelper.GetParent(d)) upperChain.Add(d);

        DependencyObject? lowerChild = null;
        for (DependencyObject? d = lower; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            var at = upperChain.IndexOf(d);
            if (at < 0) { lowerChild = d; continue; }
            if (at == 0 || lowerChild is null || d is not Panel panel) return false;
            var upperChild = upperChain[at - 1];
            var upperZ = PanelIndex(panel, upperChild);
            var lowerZ = PanelIndex(panel, lowerChild);
            if (upperZ <= lowerZ) return false;
            for (var i = at - 1; i >= 0; i--)
            {
                var brush = upperChain[i] switch { Panel p => p.Background, Border b => b.Background, Control c => c.Background, _ => null };
                if (brush is SolidColorBrush { Color.A: > 200 } || brush is GradientBrush or ImageBrush) return true;
            }
            return false;
        }
        return false;
    }

    private static int PanelIndex(Panel panel, DependencyObject child)
    {
        var index = panel.Children.IndexOf(child as UIElement);
        var z = child is UIElement ui ? Panel.GetZIndex(ui) : 0;
        return z * 100000 + (index < 0 ? VisualIndex(panel, child) : index);
    }

    private static int VisualIndex(DependencyObject parent, DependencyObject child)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (ReferenceEquals(VisualTreeHelper.GetChild(parent, i), child)) return i;
        return -1;
    }

    private static bool IsInside<T>(DependencyObject element, DependencyObject root) where T : DependencyObject
        => FindAncestor<T>(element, root) is not null;

    private static string AutomationIdOf(DependencyObject element)
    {
        var own = AutomationProperties.GetAutomationId(element);
        if (!string.IsNullOrEmpty(own)) return own;
        for (var d = VisualTreeHelper.GetParent(element); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            var id = AutomationProperties.GetAutomationId(d);
            if (!string.IsNullOrEmpty(id)) return "^" + id;
        }
        return string.Empty;
    }

    private static int RowOf(DependencyObject element)
    {
        for (var d = VisualTreeHelper.GetParent(element); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is DataGridRow row) return row.GetIndex();
            if (d is ListBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is { } owner)
                return owner.ItemContainerGenerator.IndexFromContainer(item);
        }
        return -1;
    }

    /// <summary>
    /// 요소 경로 — 뷰(UserControl) · 이름 · 자동화 식별자가 있는 조상과 가까운 부모 둘만 남긴다(행 번호는 빼서 같은 결함이 한 서명으로 묶인다).
    /// </summary>
    private static string PathOf(DependencyObject element, DependencyObject root)
    {
        var segments = new List<string>();
        var near = 0;
        for (DependencyObject? d = element; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            var keep = near < 3 || d is UserControl or DataGridCell or ContentControl { Name.Length: > 0 }
                       || d is FrameworkElement { Name.Length: > 0 }
                       || !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(d))
                       || d.GetType().Name is "ConsoleShell" or "ConsoleDetailHost" or "ConsoleToolbar" or "ConsoleSection";
            if (keep) segments.Add(Describe(d));
            near++;
            if (ReferenceEquals(d, root)) break;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string PathTail(DependencyObject element, DependencyObject root)
    {
        var path = PathOf(element, root);
        var parts = path.Split('/');
        return string.Join("/", parts.Skip(Math.Max(0, parts.Length - 3)));
    }

    private static string Describe(DependencyObject d)
    {
        var name = d.GetType().Name;
        if (d is FrameworkElement { Name.Length: > 0 } fe) name += "#" + fe.Name;
        var id = AutomationProperties.GetAutomationId(d);
        if (!string.IsNullOrEmpty(id)) name += "@" + id;
        if (d is DataGridCell cell) name += $"[{Clean(cell.Column?.Header?.ToString() ?? "?", 20)}]";
        return name;
    }
    #endregion

    #region - Output -
    private static void Write(string directory, string frame, FrameworkElement root, List<Finding> findings)
    {
        Directory.CreateDirectory(directory);
        var detailPath = Path.Combine(directory, "clip-audit.tsv");
        var framePath = Path.Combine(directory, "clip-audit-frames.tsv");

        var builder = new StringBuilder();
        if (!File.Exists(detailPath)) builder.AppendLine("frame\tkind\tsurface\tpath\tautomationId\trow\ttext\tdetail\tclipper");
        var surface = $"{root.ActualWidth:0}x{root.ActualHeight:0}";
        foreach (var f in findings)
            builder.Append(frame).Append('\t').Append(f.Kind).Append('\t').Append(surface).Append('\t').Append(Clean(f.Path, 400)).Append('\t')
                   .Append(f.AutomationId).Append('\t').Append(f.Row).Append('\t').Append(f.Text).Append('\t')
                   .Append(Clean(f.Detail, 300)).Append('\t').Append(Clean(f.Clipper, 200)).AppendLine();
        File.AppendAllText(detailPath, builder.ToString(), new UTF8Encoding(true));

        var kinds = new[] { "TEXT_CLIP", "TEXT_TRIM", "BUTTON_CLIP", "BUTTON_OVERLAP", "HSCROLL", "HSCROLL_OTHER", "TYPENAME", "TEXT_TRIM_TIP" };
        var summary = new StringBuilder();
        if (!File.Exists(framePath)) summary.AppendLine("frame\tsurface\t" + string.Join("\t", kinds) + "\tdefects");
        var counts = kinds.Select(k => findings.Count(f => f.Kind == k)).ToArray();
        var defects = counts.Take(kinds.Length - 2).Sum() - counts[5];   // HSCROLL_OTHER · TEXT_TRIM_TIP 는 판단용, 결함 합계에서 뺀다
        summary.Append(frame).Append('\t').Append(surface).Append('\t').Append(string.Join("\t", counts)).Append('\t').Append(defects).AppendLine();
        File.AppendAllText(framePath, summary.ToString(), new UTF8Encoding(true));
    }

    private static string Clean(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var one = text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        return one.Length <= max ? one : one[..max] + "…";
    }

    private static string Fmt(Rect r) => $"{r.X:0.#},{r.Y:0.#},{r.Width:0.#}x{r.Height:0.#}";
    #endregion
}
