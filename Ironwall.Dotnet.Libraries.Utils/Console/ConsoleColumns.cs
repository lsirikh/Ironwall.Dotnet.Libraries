using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// "열" 메뉴 — 목록은 <b>기본 열</b>만 보이고 나머지는 상세 칸이 맡는다. 창을 넓혔을 때 전체 열을 되살리거나,
/// 열 하나하나를 끄고 켤 수 있다. 선택은 <see cref="ConsolePrefs"/> 가 콘솔 키별로 기억한다.
/// (설계 정본 window-layout-system-storyboard.html L956-976 · L-D2)
/// </summary>
/// <remarks>
/// 열 재정렬 · 정렬은 제공하지 않는다 — 화면 순서와 저장 순서가 어긋나면 의미와 자동화 단언이 같이 무너진다.
/// </remarks>
public static class ConsoleColumns
{
    /// <summary>열의 식별 키. 키가 없는 열(핸들 열 등)은 늘 보이고 개수에도 세지 않는다.</summary>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(ConsoleColumns), new PropertyMetadata(null));
    public static string? GetKey(DependencyObject column) => (string?)column.GetValue(KeyProperty);
    public static void SetKey(DependencyObject column, string? value) => column.SetValue(KeyProperty, value);

    /// <summary>기본 열인가. 거짓이면 "전체 열"을 켰을 때만 보인다.</summary>
    public static readonly DependencyProperty IsDefaultProperty = DependencyProperty.RegisterAttached(
        "IsDefault", typeof(bool), typeof(ConsoleColumns), new PropertyMetadata(true));
    public static bool GetIsDefault(DependencyObject column) => (bool)column.GetValue(IsDefaultProperty);
    public static void SetIsDefault(DependencyObject column, bool value) => column.SetValue(IsDefaultProperty, value);

    /// <summary>
    /// 그 열을 서버 계약이 지원하는가 — 거짓이면 어떤 설정으로도 보이지 않고 개수에도 세지 않는다
    /// (운영 6.3 에 붙었을 때 v7+ 전용 열을 <b>비활성이 아니라 감춘다</b>).
    /// </summary>
    public static readonly DependencyProperty IsSupportedProperty = DependencyProperty.RegisterAttached(
        "IsSupported", typeof(bool), typeof(ConsoleColumns), new PropertyMetadata(true));
    public static bool GetIsSupported(DependencyObject column) => (bool)column.GetValue(IsSupportedProperty);
    public static void SetIsSupported(DependencyObject column, bool value) => column.SetValue(IsSupportedProperty, value);

    /// <summary>
    /// U-17 — 목록 칸이 이 폭(DIU)보다 좁으면 이 열을 접는다(0 = 접지 않는다). 좁아질 때 <b>덜 중요한 열부터</b> 접어,
    /// 사람이 읽는 식별 열(아이디 · 이름)이 줄임표로 잘리거나 오른쪽 열이 가로 스크롤 밖으로 밀려나지 않게 한다.
    /// 폭은 <see cref="ConsoleShell.EffectiveListWidth"/> 로 준다(서랍이 겹치면 셸 폭은 그대로다 — D-03).
    /// 사용자가 "열" 메뉴로 숨긴 것과는 합집합이다 — 넓어지면 돌아온다. (선례: 보고서 콘솔 ReportColumnPriority)
    /// </summary>
    public static readonly DependencyProperty CollapseBelowProperty = DependencyProperty.RegisterAttached(
        "CollapseBelow", typeof(double), typeof(ConsoleColumns), new PropertyMetadata(0.0));
    public static double GetCollapseBelow(DependencyObject column) => (double)column.GetValue(CollapseBelowProperty);
    public static void SetCollapseBelow(DependencyObject column, double value) => column.SetValue(CollapseBelowProperty, value);

    /// <summary>폭 <paramref name="listWidth"/> 에서 문턱 <paramref name="collapseBelow"/> 인 열을 접는가 — 순수 판정. 아직 재지 못한 폭(0 이하)이면 접지 않는다.</summary>
    public static bool ShouldCollapse(double collapseBelow, double listWidth)
        => collapseBelow > 0 && listWidth > 0 && !double.IsNaN(listWidth) && listWidth < collapseBelow;

    /// <summary>목록 폭 <paramref name="listWidth"/> 에서 접어야 할 열 키 — <see cref="Apply"/> 의 숨김 목록에 합쳐 넘긴다.</summary>
    public static IReadOnlyList<string> CollapsedAt(IEnumerable<DataGridColumn> columns, double listWidth)
        => columns
            .Where(c => !string.IsNullOrEmpty(GetKey(c)) && ShouldCollapse(GetCollapseBelow(c), listWidth))
            .Select(c => GetKey(c)!)
            .ToList();

    /// <summary>
    /// U-18 — "열" 메뉴가 없는 목록(이벤트 · 억제 등)용: 참이면 그리드가 자기를 담은 <see cref="ConsoleShell"/> 의
    /// <see cref="ConsoleShell.EffectiveListWidth"/> 를 따라 <see cref="CollapseBelowProperty"/> 가 걸린 열만 스스로 접었다 편다.
    /// "열" 메뉴가 있는 목록은 이것을 쓰지 않는다 — 그쪽은 <see cref="CollapsedAt"/> 를 사용자 숨김과 합쳐 <see cref="Apply"/> 로 넘긴다.
    /// </summary>
    /// <remarks>조상 탐색은 <c>Loaded</c> 에서 한다(템플릿 인플레이션 중에는 부모 사슬이 없다 — drag-first-ux 규칙).</remarks>
    public static readonly DependencyProperty AutoCollapseProperty = DependencyProperty.RegisterAttached(
        "AutoCollapse", typeof(bool), typeof(ConsoleColumns), new PropertyMetadata(false, OnAutoCollapseChanged));
    public static bool GetAutoCollapse(DependencyObject grid) => (bool)grid.GetValue(AutoCollapseProperty);
    public static void SetAutoCollapse(DependencyObject grid, bool value) => grid.SetValue(AutoCollapseProperty, value);

    // 그리드마다 붙인 셸과 처리기 — 떼어 낼 때 필요하다.
    private static readonly DependencyProperty AutoCollapseLinkProperty = DependencyProperty.RegisterAttached(
        "AutoCollapseLink", typeof(Tuple<ConsoleShell, EventHandler<double>>), typeof(ConsoleColumns), new PropertyMetadata(null));

    /// <summary>문턱이 걸린 열만 목록 폭 <paramref name="listWidth"/> 에 맞춰 보이거나 숨긴다(문턱 없는 열은 건드리지 않는다).</summary>
    public static void ApplyCollapse(IEnumerable<DataGridColumn> columns, double listWidth)
    {
        foreach (var column in columns)
        {
            var below = GetCollapseBelow(column);
            if (below <= 0) continue;
            var visibility = ShouldCollapse(below, listWidth) ? Visibility.Collapsed : Visibility.Visible;
            if (column.Visibility != visibility) column.Visibility = visibility;
        }
    }

    private static void OnAutoCollapseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        grid.Loaded -= OnAutoCollapseLoaded;
        grid.Unloaded -= OnAutoCollapseUnloaded;
        Detach(grid);
        if (e.NewValue is not true) return;

        grid.Loaded += OnAutoCollapseLoaded;
        grid.Unloaded += OnAutoCollapseUnloaded;
        if (grid.IsLoaded) Attach(grid);
    }

    private static void OnAutoCollapseLoaded(object sender, RoutedEventArgs e) => Attach((DataGrid)sender);

    private static void OnAutoCollapseUnloaded(object sender, RoutedEventArgs e) => Detach((DataGrid)sender);

    private static void Attach(DataGrid grid)
    {
        Detach(grid);
        ConsoleShell? shell = null;
        for (var d = System.Windows.Media.VisualTreeHelper.GetParent(grid); d is not null && shell is null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            shell = d as ConsoleShell;

        if (shell is null) return;

        EventHandler<double> handler = (_, width) => ApplyCollapse(grid.Columns, width);
        shell.EffectiveListWidthChanged += handler;
        grid.SetValue(AutoCollapseLinkProperty, Tuple.Create(shell, handler));
        ApplyCollapse(grid.Columns, shell.EffectiveListWidth);
    }

    private static void Detach(DataGrid grid)
    {
        if (grid.GetValue(AutoCollapseLinkProperty) is not Tuple<ConsoleShell, EventHandler<double>> link) return;
        link.Item1.EffectiveListWidthChanged -= link.Item2;
        grid.ClearValue(AutoCollapseLinkProperty);
    }

    /// <summary>한 열이 보여야 하는가 — 순수 판정.</summary>
    public static bool ShouldShow(bool isSupported, bool isDefault, bool showAll, bool isHiddenByUser)
        => isSupported && (isDefault || showAll) && !isHiddenByUser;

    /// <summary>설정을 열들에 적용하고 툴바에 찍을 글자("열 6/9")를 돌려준다.</summary>
    public static string Apply(IEnumerable<DataGridColumn> columns, bool showAll, ICollection<string>? hiddenKeys)
    {
        int shown = 0, total = 0;
        foreach (var column in columns)
        {
            var key = GetKey(column);
            if (string.IsNullOrEmpty(key)) continue;

            var supported = GetIsSupported(column);
            var visible = ShouldShow(supported, GetIsDefault(column), showAll, hiddenKeys?.Contains(key!) == true);
            column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (!supported) continue;
            total++;
            if (visible) shown++;
        }
        return $"열 {shown}/{total}";
    }

    /// <summary>"열" 메뉴에 올릴 항목 — (키, 머리글, 지금 보이는가, 기본 열인가). 지원하지 않는 열은 빠진다.</summary>
    public static IReadOnlyList<(string Key, string Header, bool IsVisible, bool IsDefault)> Describe(IEnumerable<DataGridColumn> columns)
        => columns
            .Where(c => !string.IsNullOrEmpty(GetKey(c)) && GetIsSupported(c))
            .Select(c => (GetKey(c)!, c.Header?.ToString() ?? GetKey(c)!, c.Visibility == Visibility.Visible, GetIsDefault(c)))
            .ToList();

    /// <summary>
    /// 사용자가 메뉴에서 한 열을 껐다/켰다. 기본이 아닌 열을 켜려면 "숨김 목록에서 빼는 것"으로는 부족하다 —
    /// 그래서 그때는 전체 열을 켜고 나머지 비기본 열을 숨김 목록에 넣는다.
    /// </summary>
    public static void Toggle(IEnumerable<DataGridColumn> columns, ConsolePrefEntry prefs, string key)
    {
        var all = columns.Where(c => !string.IsNullOrEmpty(GetKey(c)) && GetIsSupported(c)).ToList();
        var column = all.FirstOrDefault(c => GetKey(c) == key);
        if (column == null) return;

        var visible = ShouldShow(true, GetIsDefault(column), prefs.ShowAllColumns, prefs.HiddenColumns.Contains(key));
        if (visible)
        {
            if (!prefs.HiddenColumns.Contains(key)) prefs.HiddenColumns.Add(key);
            return;
        }

        prefs.HiddenColumns.Remove(key);
        if (GetIsDefault(column) || prefs.ShowAllColumns) return;

        // 비기본 열 하나만 켠다: 전체 열을 켜되, 지금 안 보이던 다른 비기본 열은 숨김으로 돌린다.
        prefs.ShowAllColumns = true;
        foreach (var other in all.Where(c => !GetIsDefault(c) && GetKey(c) != key))
            if (!prefs.HiddenColumns.Contains(GetKey(other)!)) prefs.HiddenColumns.Add(GetKey(other)!);
    }
}
