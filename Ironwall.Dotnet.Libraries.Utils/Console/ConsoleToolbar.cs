using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 콘솔 공용 툴바 — [추가] · [삭제] · [갱신] · 검색 · 필터 슬롯 · [열 n/m] · 추가 슬롯.
/// </summary>
/// <remarks>
/// <para>버튼은 라우티드 이벤트로 알린다 — Caliburn 의 <c>cal:Message.Attach="[Event AddClick] = [Action …]"</c> 로 받는다.</para>
/// <para><b>아무 일도 안 일어나는 버튼은 두지 않는다</b>: 꺼진 버튼은 늘 사유 툴팁을 갖는다(권한 없음 · 선택 없음).</para>
/// </remarks>
[TemplatePart(Name = "PART_Add", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Delete", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Refresh", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Columns", Type = typeof(ButtonBase))]
public class ConsoleToolbar : Control
{
    static ConsoleToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleToolbar), new FrameworkPropertyMetadata(typeof(ConsoleToolbar)));
        FocusableProperty.OverrideMetadata(typeof(ConsoleToolbar), new FrameworkPropertyMetadata(false));
    }

    #region - Events -
    public static readonly RoutedEvent AddClickEvent = Ev(nameof(AddClick));
    public event RoutedEventHandler AddClick { add => AddHandler(AddClickEvent, value); remove => RemoveHandler(AddClickEvent, value); }

    public static readonly RoutedEvent DeleteClickEvent = Ev(nameof(DeleteClick));
    public event RoutedEventHandler DeleteClick { add => AddHandler(DeleteClickEvent, value); remove => RemoveHandler(DeleteClickEvent, value); }

    public static readonly RoutedEvent RefreshClickEvent = Ev(nameof(RefreshClick));
    public event RoutedEventHandler RefreshClick { add => AddHandler(RefreshClickEvent, value); remove => RemoveHandler(RefreshClickEvent, value); }

    public static readonly RoutedEvent ColumnsClickEvent = Ev(nameof(ColumnsClick));
    public event RoutedEventHandler ColumnsClick { add => AddHandler(ColumnsClickEvent, value); remove => RemoveHandler(ColumnsClickEvent, value); }
    #endregion

    #region - Properties -
    public static readonly DependencyProperty ConsoleKeyProperty = Reg(nameof(ConsoleKey), "Console");
    public string ConsoleKey { get => (string)GetValue(ConsoleKeyProperty); set => SetValue(ConsoleKeyProperty, value); }

    public static readonly DependencyProperty AddTextProperty = Reg(nameof(AddText), "추가");
    public string AddText { get => (string)GetValue(AddTextProperty); set => SetValue(AddTextProperty, value); }

    public static readonly DependencyProperty CanAddProperty = Reg(nameof(CanAdd), true);
    public bool CanAdd { get => (bool)GetValue(CanAddProperty); set => SetValue(CanAddProperty, value); }

    public static readonly DependencyProperty CanDeleteProperty = Reg(nameof(CanDelete), false);
    public bool CanDelete { get => (bool)GetValue(CanDeleteProperty); set => SetValue(CanDeleteProperty, value); }

    public static readonly DependencyProperty CanRefreshProperty = Reg(nameof(CanRefresh), true);
    public bool CanRefresh { get => (bool)GetValue(CanRefreshProperty); set => SetValue(CanRefreshProperty, value); }

    public static readonly DependencyProperty AddDisabledReasonProperty = Reg(nameof(AddDisabledReason), "권한이 없습니다.");
    /// <summary>[추가] 가 꺼져 있을 때의 사유.</summary>
    public string AddDisabledReason { get => (string)GetValue(AddDisabledReasonProperty); set => SetValue(AddDisabledReasonProperty, value); }

    public static readonly DependencyProperty DeleteDisabledReasonProperty = Reg(nameof(DeleteDisabledReason), "지울 행을 먼저 고르세요.");
    /// <summary>[삭제] 가 꺼져 있을 때의 사유 — 권한 없음이면 창이 "권한이 없습니다." 로 바꿔 준다.</summary>
    public string DeleteDisabledReason { get => (string)GetValue(DeleteDisabledReasonProperty); set => SetValue(DeleteDisabledReasonProperty, value); }

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText), typeof(string), typeof(ConsoleToolbar),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    public static readonly DependencyProperty SearchPlaceholderProperty = Reg(nameof(SearchPlaceholder), "검색");
    public string SearchPlaceholder { get => (string)GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    public static readonly DependencyProperty ShowSearchProperty = Reg(nameof(ShowSearch), true);
    public bool ShowSearch { get => (bool)GetValue(ShowSearchProperty); set => SetValue(ShowSearchProperty, value); }

    public static readonly DependencyProperty ColumnsTextProperty = Reg(nameof(ColumnsText), string.Empty);
    /// <summary>"열 6/12". 비우면 버튼을 숨긴다.</summary>
    public string ColumnsText { get => (string)GetValue(ColumnsTextProperty); set => SetValue(ColumnsTextProperty, value); }

    public static readonly DependencyProperty FiltersProperty = Reg<object?>(nameof(Filters), null);
    /// <summary>필터 칩 자리(검색 앞).</summary>
    public object? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }

    public static readonly DependencyProperty ExtraProperty = Reg<object?>(nameof(Extra), null);
    /// <summary>맨 오른쪽 자리(⋯ · 창 고유 버튼).</summary>
    public object? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    private static readonly DependencyPropertyKey IsSearchCompactPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsSearchCompact), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsSearchCompactProperty = IsSearchCompactPropertyKey.DependencyProperty;
    /// <summary>
    /// 검색창이 아이콘 트리거로 접힌 상태인가(D-23) — 폭 부족을 커널이 스스로 판정한다(읽기 전용,
    /// <see cref="ConsoleLayoutMath.ResolveToolbarSearchMode"/> 의 결과). 소비자가 직접 쓰지 않는다.
    /// </summary>
    public bool IsSearchCompact { get => (bool)GetValue(IsSearchCompactProperty); private set => SetValue(IsSearchCompactPropertyKey, value); }
    #endregion

    // D-23 — 왼쪽(추가·삭제·갱신·필터) · 오른쪽(열 버튼·Extra) 클러스터, 가운데 Grid 는 이름 없는 템플릿
    // 요소다(x:Name 신설 금지 — 시각 트리에서 Grid.Column 번호로 찾는다). 폭을 알아야 오버플로를 판정하고,
    // Grid 도 있어야 가운데 칸의 최소폭을 검색 상태에 맞춰 줄일 수 있다(칸 자체의 고정 MinWidth 는
    // 폭이 모자라도 줄지 않아 옛 결함의 원인이었다).
    private Grid? _grid;
    private TextBox? _search;
    private FrameworkElement? _leftCluster;
    private FrameworkElement? _rightCluster;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();
        Hook("PART_Add", AddClickEvent);
        Hook("PART_Delete", DeleteClickEvent);
        Hook("PART_Refresh", RefreshClickEvent);
        Hook("PART_Columns", ColumnsClickEvent);

        LayoutUpdated -= OnToolbarLayoutUpdated;

        _grid = (VisualTreeHelper.GetChild(this, 0) as Border)?.Child as Grid;
        _search = GetTemplateChild("Search") as TextBox;
        _leftCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
        _rightCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 2);

        LayoutUpdated += OnToolbarLayoutUpdated;

        ApplySearchGeometry();
    }

    // D-23 — 필터 · Extra 내용은 이 컨트롤의 SizeChanged 밖에서도 바뀐다(예: "직접" 선택 시 날짜 범위
    // 필드가 나타난다). LayoutUpdated 로 레이아웃이 정착할 때마다 다시 재는 것이 안전하다 — 판정이
    // 바뀔 때만 DependencyProperty 를 쓰므로(UpdateSearchMode 내부 조기 반환) 무한 루프가 없다.
    private void OnToolbarLayoutUpdated(object? sender, EventArgs e) => UpdateSearchMode();

    /// <summary>
    /// 마지막으로 판정한 안전한 검색 최소폭(D-23) — <see cref="ConsoleLayoutMath.ResolveToolbarSearchMinWidth"/>
    /// 는 이미 "예산을 넘지 않는 최대치"를 돌려주므로, 포커스가 와도 이 값을 더 넓힐 필요가 없다(넓힐 여지가
    /// 있었다면 애초에 이 값 자체가 더 컸을 것이다) — 그래서 포커스 이벤트를 따로 듣지 않는다.
    /// </summary>
    private double _searchMinWidth = ConsoleLayoutMath.ToolbarSearchFullMinWidth;

    private void UpdateSearchMode()
    {
        if (_leftCluster is null || _rightCluster is null) return;

        var resolved = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(ActualWidth, _leftCluster.ActualWidth, _rightCluster.ActualWidth);
        if (Math.Abs(resolved - _searchMinWidth) < 0.5) return; // 거의 그대로면 다시 쓰지 않는다 — 레이아웃 진동 방지
        _searchMinWidth = resolved;

        var compact = resolved < ConsoleLayoutMath.ToolbarSearchFullMinWidth;
        if (compact != IsSearchCompact) IsSearchCompact = compact;

        ApplySearchGeometry();
    }

    // 판정(ConsoleLayoutMath, 순수 함수)과 적용(여기)을 분리한다. 가운데 칸의 MinWidth 도 검색과 함께
    // 줄인다 — 칸 자체의 고정폭이 검색의 접힘을 무력화하지 않도록(옛 결함의 직접 원인이었다).
    private void ApplySearchGeometry()
    {
        if (_search is null || _grid is null || _grid.ColumnDefinitions.Count < 2) return;

        var full = _searchMinWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth;
        _search.MinWidth = _searchMinWidth;
        _search.MaxWidth = full ? ConsoleLayoutMath.ToolbarSearchFullMaxWidth : _searchMinWidth;
        _grid.ColumnDefinitions[1].MinWidth = _searchMinWidth + ConsoleLayoutMath.ToolbarSearchLeftMargin;
    }

    // 템플릿은 다시 적용될 수 있다(스타일 · 테마 교체). 옛 부품의 구독을 풀지 않고 람다를 또 얹으면 한 번 눌러 N번 울린다.
    private readonly List<(ButtonBase Button, RoutedEventHandler Handler)> _hooks = new();

    private void Unhook()
    {
        foreach (var (button, handler) in _hooks) button.Click -= handler;
        _hooks.Clear();
    }

    private void Hook(string part, RoutedEvent routed)
        => Hook(part, (_, e) => { e.Handled = true; RaiseEvent(new RoutedEventArgs(routed, this)); });

    private void Hook(string part, RoutedEventHandler handler)
    {
        if (GetTemplateChild(part) is not ButtonBase button) return;
        button.Click += handler;
        _hooks.Add((button, handler));
    }

    private static RoutedEvent Ev(string name)
        => EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConsoleToolbar));

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleToolbar), new PropertyMetadata(defaultValue));
}
