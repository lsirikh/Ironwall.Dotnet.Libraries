using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// <see cref="ConsoleChipStrip"/> 의 칩 칸 — 접힌 한 줄(+ [더 보기]) 또는 펼친 여러 줄로 칩을 세운다.
/// </summary>
/// <remarks>
/// <para>[더 보기] 칩(<see cref="ConsoleChipStripToggle"/>)은 이 패널의 <b>시각 자식 맨 끝</b>에 따로 둔다 — 항목이 아니므로
/// 항목 생성기가 건드리지 않고, 시각 순서가 곧 Tab 순서라 칩 다음에 닿는다.</para>
/// <para>판정은 <see cref="ConsoleChipStripMath"/> 한 곳이다. 이 패널은 잰 폭을 넘기고 결과대로 세울 뿐이다.</para>
/// <para>가려진 칩은 <b>크기 0 으로 배치</b>하고 칸을 0 배율로 접는다(그려지지 않고 눌리지 않고 UIA 경계도 0).
/// Visibility 는 건드리지 않는다 — 가려진 칩도 UIA 트리(기본 보기)에 남아 자동화가 id 로 찾아 고를 수 있다.
/// 칸(ContentPresenter)에 쓰는 로컬 값은 이 0 배율 하나뿐이고, 보일 때 참조로 확인해 걷는다(이 목록은 가상화 · 재활용이 없다).</para>
/// </remarks>
public class ConsoleChipStripPanel : Panel
{
    /// <summary>가려진 칩 칸을 접는 0 배율 — 이 패널이 건 값인지 참조로 가린다(칸의 다른 RenderTransform 은 건드리지 않는다).</summary>
    private static readonly Transform Folded = CreateFolded();

    private ConsoleChipStripToggle? _toggle;
    private Rect[] _rects = Array.Empty<Rect>();
    private bool[] _hidden = Array.Empty<bool>();
    private Rect _toggleRect;
    private double _layoutWidth = double.NaN;

    /// <summary>[더 보기] 칩 — 줄에 속한 패널이면 첫 측정 때 만든다(시험 · 진단).</summary>
    public ConsoleChipStripToggle? MoreToggle => _toggle;

    protected override int VisualChildrenCount => base.VisualChildrenCount + (_toggle is null ? 0 : 1);

    protected override Visual GetVisualChild(int index)
        => _toggle is not null && index == base.VisualChildrenCount ? _toggle : base.GetVisualChild(index);

    /// <summary><paramref name="element"/> 가 지금 가려진 칩(또는 그 안)인가.</summary>
    internal bool IsInHiddenChip(DependencyObject? element)
    {
        var index = ChildIndexOf(element);
        return index >= 0 && index < _hidden.Length && _hidden[index];
    }

    /// <summary>고른 칩(켜진 칩)을 둘러싼 ScrollViewer 의 보이는 자리로 — 펼친 줄이 최대 높이에 걸려 굴릴 때.</summary>
    internal void BringSelectedIntoView()
    {
        foreach (UIElement? child in InternalChildren)
        {
            if (child is FrameworkElement element && IsChecked(element))
            {
                element.BringIntoView();
                return;
            }
        }
    }

    /// <summary>칩 <paramref name="index"/> 가 지금 가려졌는가(시험 · 진단).</summary>
    public bool IsChipHidden(int index) => index >= 0 && index < _hidden.Length && _hidden[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ResolveOwner();
        var children = InternalChildren;
        var sizes = new Size[children.Count];
        var selected = -1;
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child is null) continue;
            child.Measure(unbounded);
            sizes[i] = child.DesiredSize;
            if (selected < 0 && IsChecked(child)) selected = i;
        }
        return Layout(owner, availableSize.Width, sizes, selected);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren;
        // 측정 때와 폭이 다르면(부모가 다른 폭을 주었다) 그 폭으로 다시 판정한다
        if (_rects.Length != children.Count || Math.Abs(finalSize.Width - _layoutWidth) > ConsoleChipStripMath.Epsilon)
        {
            var sizes = new Size[children.Count];
            var selected = -1;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not { } child) continue;
                sizes[i] = child.DesiredSize;
                if (selected < 0 && IsChecked(child)) selected = i;
            }
            Layout(ResolveOwner(), finalSize.Width, sizes, selected);
        }

        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is not { } child) continue;
            var rect = i < _rects.Length ? _rects[i] : new Rect();
            if (_hidden.Length > i && _hidden[i])
            {
                // 크기 0 배치(레이아웃 클립)만으로는 칩의 UIA 경계가 제 크기 그대로 [더 보기] 자리에 겹쳐 보고된다 —
                // 좌표로 누르는 자동화가 [더 보기]를 대신 누른다. 0 배율로 접어 경계도 0 으로 낸다(칩 peer · 패턴은 그대로).
                if (!ReferenceEquals(child.RenderTransform, Folded)) child.RenderTransform = Folded;
                child.Arrange(new Rect(rect.X, rect.Y, 0, 0));
            }
            else
            {
                if (ReferenceEquals(child.RenderTransform, Folded)) child.ClearValue(RenderTransformProperty);   // 우리가 건 것만 걷는다
                child.Arrange(rect);
            }
        }
        if (_toggle is { Visibility: Visibility.Visible }) _toggle.Arrange(_toggleRect);
        return finalSize;
    }

    /// <summary>판정 + 칸마다의 자리 계산. 반환값은 이 패널이 원하는 크기.</summary>
    private Size Layout(ConsoleChipStrip? owner, double width, Size[] sizes, int selected)
    {
        _layoutWidth = width;
        var count = sizes.Length;
        var widths = new double[count];
        for (var i = 0; i < count; i++) widths[i] = sizes[i].Width;
        var minLine = owner?.FirstLineHeight ?? 0;

        _rects = new Rect[count];
        _hidden = new bool[count];

        var fitsOneLine = !ConsoleChipStripMath.Fit(width, widths, 0, selected).ShowsToggle;
        var expanded = owner?.IsExpanded ?? true;

        if (owner is null || fitsOneLine)
        {
            SetToggleVisible(false);
            owner?.SetHiddenCount(0);
            return Flow(width, sizes, Enumerable.Range(0, count).ToArray(), toggleSize: null, minLine);
        }

        var toggle = EnsureToggle(owner);
        SetToggleVisible(true);
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);

        if (expanded)
        {
            toggle.Show(expanded: true, hidden: 0);
            toggle.Measure(unbounded);
            owner.SetHiddenCount(0);
            return Flow(width, sizes, Enumerable.Range(0, count).ToArray(), toggle.DesiredSize, minLine);
        }

        // 접힘 — [더 보기] 폭은 가장 긴 글(+전체 수)로 먼저 재어 판정하고, 실제 수로 글을 맞춘다(글이 짧아질 뿐 넘치지 않는다)
        toggle.Show(expanded: false, hidden: count);
        toggle.Measure(unbounded);
        var fit = ConsoleChipStripMath.Fit(width, widths, toggle.DesiredSize.Width, selected);
        toggle.Show(expanded: false, hidden: fit.HiddenCount);
        toggle.Measure(unbounded);
        owner.SetHiddenCount(fit.HiddenCount);

        var lineHeight = Math.Max(minLine, toggle.DesiredSize.Height);
        foreach (var i in fit.Visible) lineHeight = Math.Max(lineHeight, sizes[i].Height);

        var x = 0.0;
        for (var i = 0; i < count; i++) _hidden[i] = true;
        foreach (var i in fit.Visible)
        {
            _hidden[i] = false;
            _rects[i] = new Rect(x, (lineHeight - sizes[i].Height) / 2, sizes[i].Width, sizes[i].Height);
            x += sizes[i].Width;
        }
        _toggleRect = new Rect(x, (lineHeight - toggle.DesiredSize.Height) / 2, toggle.DesiredSize.Width, toggle.DesiredSize.Height);
        for (var i = 0; i < count; i++)
            if (_hidden[i]) _rects[i] = new Rect(x, 0, 0, 0);
        x += toggle.DesiredSize.Width;

        return new Size(double.IsInfinity(width) ? x : Math.Min(x, width), lineHeight);
    }

    /// <summary>칸을 순서대로 여러 줄로 흘린다(펼침 · 한 줄에 다 들어갈 때). [더 보기]가 있으면 맨 끝 칸. <paramref name="minLine"/> 은 첫 줄에만.</summary>
    private Size Flow(double width, Size[] sizes, int[] order, Size? toggleSize, double minLine)
    {
        var cells = new List<Size>(order.Length + 1);
        foreach (var i in order) cells.Add(sizes[i]);
        if (toggleSize is { } t) cells.Add(t);

        var lines = ConsoleChipStripMath.Wrap(width, cells.Select(c => c.Width).ToArray());
        var lineCount = lines.Length == 0 ? 0 : lines[^1] + 1;
        var heights = new double[lineCount];
        var lineWidths = new double[lineCount];
        for (var k = 0; k < cells.Count; k++)
        {
            heights[lines[k]] = Math.Max(Math.Max(heights[lines[k]], lines[k] == 0 ? minLine : 0), cells[k].Height);
            lineWidths[lines[k]] += cells[k].Width;
        }

        var tops = new double[lineCount];
        for (var l = 1; l < lineCount; l++) tops[l] = tops[l - 1] + heights[l - 1];

        var xs = new double[lineCount];
        for (var k = 0; k < cells.Count; k++)
        {
            var l = lines[k];
            var rect = new Rect(xs[l], tops[l] + (heights[l] - cells[k].Height) / 2, cells[k].Width, cells[k].Height);
            xs[l] += cells[k].Width;
            if (k < order.Length) _rects[order[k]] = rect;
            else _toggleRect = rect;
        }

        var used = lineWidths.Length == 0 ? 0 : lineWidths.Max();
        var height = lineCount == 0 ? minLine : tops[^1] + heights[^1];
        return new Size(double.IsInfinity(width) ? used : Math.Min(used, width), height);
    }

    private ConsoleChipStrip? ResolveOwner()
    {
        var owner = IsItemsHost ? ItemsControl.GetItemsOwner(this) as ConsoleChipStrip : null;
        if (owner is not null && !ReferenceEquals(owner.Panel, this)) owner.Panel = this;
        if (owner is not null && _toggle is not null)
        {
            var id = owner.MoreAutomationId;
            if (!Equals(AutomationProperties.GetAutomationId(_toggle), id)) AutomationProperties.SetAutomationId(_toggle, id);
        }
        return owner;
    }

    private ConsoleChipStripToggle EnsureToggle(ConsoleChipStrip owner)
    {
        if (_toggle is not null) return _toggle;
        _toggle = new ConsoleChipStripToggle();
        _toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ConsoleChipStrip.IsExpanded)) { Source = owner, Mode = BindingMode.TwoWay });
        AutomationProperties.SetAutomationId(_toggle, owner.MoreAutomationId);
        AddVisualChild(_toggle);
        return _toggle;
    }

    /// <summary>필요 없을 때는 접어 둔다 — 크기 0 으로만 두면 Tab 이 보이지 않는 칩에 닿는다.</summary>
    private void SetToggleVisible(bool visible)
    {
        if (_toggle is null) return;
        var value = visible ? Visibility.Visible : Visibility.Collapsed;
        if (_toggle.Visibility != value) _toggle.Visibility = value;
    }

    private int ChildIndexOf(DependencyObject? element)
    {
        while (element is not null)
        {
            // 초점이 ContentElement(Hyperlink 등)에 있을 수 있다 — VisualTreeHelper 는 Visual 이 아니면 던진다
            var parent = (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);
            if (ReferenceEquals(parent, this)) return element is UIElement ui ? InternalChildren.IndexOf(ui) : -1;
            element = parent;
        }
        return -1;
    }

    private static Transform CreateFolded()
    {
        var folded = new ScaleTransform(0, 0);
        folded.Freeze();
        return folded;
    }

    /// <summary>칸 안의 칩(ToggleButton · RadioButton)이 켜져 있는가 — 칸(ContentPresenter) 아래 몇 층만 본다.</summary>
    private static bool IsChecked(DependencyObject element, int depth = 0)
    {
        if (element is ToggleButton toggle) return toggle.IsChecked == true;
        if (depth >= 3) return false;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (IsChecked(VisualTreeHelper.GetChild(element, i), depth + 1)) return true;
        return false;
    }
}
