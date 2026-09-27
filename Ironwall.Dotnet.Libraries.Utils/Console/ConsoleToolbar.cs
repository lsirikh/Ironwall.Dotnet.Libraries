using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 콘솔 공용 툴바 — 두 줄. 첫째 줄(동작): [추가] · [삭제] · [갱신] · Extra · 검색 · [열 n/m] · [⋯]. 둘째 줄(필터): 필터 칩(있을 때만).
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

    public static readonly DependencyProperty FiltersProperty = DependencyProperty.Register(
        nameof(Filters), typeof(object), typeof(ConsoleToolbar), new PropertyMetadata(null));
    /// <summary>
    /// 필터 칩 자리 — <b>둘째 줄(필터 줄)</b>에 왼쪽부터 선다. 첫째 줄(동작 줄)의 폭 예산에는 들어가지 않는다(D-2026-09-27-71c353).
    /// 내용이 없거나 전부 숨으면 둘째 줄은 높이 0 이다(<see cref="HasFilterRow"/>). 좁은 폭에서 칩이 한 줄에 다 안 들어가면
    /// 창의 필터 패널이 <c>WrapPanel</c> 로 다음 줄로 넘긴다 — 칩을 누르거나 글자 중간에서 자르지 않는다.
    /// </summary>
    public object? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }

    private static readonly DependencyPropertyKey HasFilterRowPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasFilterRow), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty HasFilterRowProperty = HasFilterRowPropertyKey.DependencyProperty;
    /// <summary>
    /// 둘째 줄(필터 줄)이 보이는가(읽기 전용) — <see cref="Filters"/> 에 보이는 내용이 있을 때만 참이다.
    /// 거짓이면 그 줄은 여백 · 최소 높이 · 아래 선 모두 0 이고, 첫째 줄이 아래 선을 긋는다.
    /// </summary>
    public bool HasFilterRow { get => (bool)GetValue(HasFilterRowProperty); private set => SetValue(HasFilterRowPropertyKey, value); }

    private static readonly DependencyPropertyKey IsColumnsOverflowPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsColumnsOverflow), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsColumnsOverflowProperty = IsColumnsOverflowPropertyKey.DependencyProperty;
    /// <summary>U-18 — [열 n/m] 이 [⋯] 뒤로 접혔는가(읽기 전용). 접힌 동안에는 팝업 안의 같은 글자 버튼이 <see cref="ColumnsClick"/> 를 낸다.</summary>
    public bool IsColumnsOverflow { get => (bool)GetValue(IsColumnsOverflowProperty); private set => SetValue(IsColumnsOverflowPropertyKey, value); }

    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra), typeof(object), typeof(ConsoleToolbar), new PropertyMetadata(null, (d, _) => ((ConsoleToolbar)d).OnExtraChanged()));
    /// <summary>창 고유 동작 자리 — 첫째 줄에서 [추가][삭제][갱신] 바로 뒤. 폭이 정말 모자라면 [⋯] 팝업으로 옮겨진다(<see cref="IsExtraOverflow"/>).</summary>
    public object? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    private static readonly DependencyPropertyKey IsExtraOverflowPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsExtraOverflow), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsExtraOverflowProperty = IsExtraOverflowPropertyKey.DependencyProperty;
    /// <summary>
    /// U-17 — <see cref="Extra"/> 가 [⋯] 뒤로 접혔는가(읽기 전용, <see cref="ConsoleLayoutMath.ResolveToolbarFit"/> 의 결과).
    /// 검색이 전체 폭(180) 을 받을 수 없을 때만 참이 된다.
    /// </summary>
    public bool IsExtraOverflow { get => (bool)GetValue(IsExtraOverflowProperty); private set => SetValue(IsExtraOverflowPropertyKey, value); }

    private static readonly DependencyPropertyKey IsSearchCompactPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsSearchCompact), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsSearchCompactProperty = IsSearchCompactPropertyKey.DependencyProperty;
    /// <summary>
    /// 검색창이 아이콘 트리거로 접힌 상태인가(읽기 전용, <see cref="ConsoleLayoutMath.ResolveToolbarFit"/> 의 결과) —
    /// Extra · [열 n/m] 을 다 [⋯] 로 옮겨도 검색 전체 폭(180)이 안 들어갈 만큼 좁을 때만 참이다. 소비자가 직접 쓰지 않는다.
    /// </summary>
    public bool IsSearchCompact { get => (bool)GetValue(IsSearchCompactProperty); private set => SetValue(IsSearchCompactPropertyKey, value); }

    /// <summary>지금 검색창에 건 폭(DIU) — 전체 폭이면 180~240, 접혔거나 숨었으면 0. 진단 · 시험용(읽기 전용).</summary>
    public double SearchBoxWidth => _fit.IsSearchCompact || !ShowSearch ? 0 : _fit.SearchWidth;
    #endregion

    // 템플릿 뿌리는 이름 없는 StackPanel 이다(x:Name 신설 금지) — 첫 자식 = 첫째 줄 Border(안에 Grid), 둘째 자식 = 필터 줄 Border.
    // 첫째 줄 Grid 의 0번 칸 = [추가][삭제][갱신] + Extra, 1번 칸 = 검색, 2번 칸 = [열 n/m] + [⋯](코드가 붙인다).
    private Grid? _grid;
    private TextBox? _search;
    private FrameworkElement? _leftCluster;
    private FrameworkElement? _rightCluster;
    private ContentPresenter? _filterPresenter;

    // U-13 — 압축 모드(검색이 아이콘 하나로 접힌 상태)의 트리거 · 오버레이 · 확장 입력칸.
    private ButtonBase? _searchCompactTrigger;
    private Popup? _searchPopup;
    private TextBox? _searchExpanded;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();
        Hook("PART_Add", AddClickEvent);
        Hook("PART_Delete", DeleteClickEvent);
        Hook("PART_Refresh", RefreshClickEvent);
        Hook("PART_Columns", ColumnsClickEvent);

        LayoutUpdated -= OnToolbarLayoutUpdated;

        var root = VisualTreeHelper.GetChildrenCount(this) > 0 ? VisualTreeHelper.GetChild(this, 0) as Panel : null;
        var rows = root?.Children.OfType<Border>().ToList() ?? new List<Border>();
        _grid = rows.ElementAtOrDefault(0)?.Child as Grid;
        _filterPresenter = rows.ElementAtOrDefault(1)?.Child as ContentPresenter;
        _search = GetTemplateChild("Search") as TextBox;
        _leftCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
        _rightCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 2);
        _columnsButton = GetTemplateChild("PART_Columns") as FrameworkElement;
        _fit = new ConsoleToolbarFit(ConsoleToolbarOverflow.None, ConsoleLayoutMath.ToolbarSearchPreferredWidth, false);
        BuildOverflowParts();

        if (_searchCompactTrigger is not null) _searchCompactTrigger.Click -= OnSearchCompactTriggerClick;
        _searchCompactTrigger = GetTemplateChild("PART_SearchCompact") as ButtonBase;
        if (_searchCompactTrigger is not null) _searchCompactTrigger.Click += OnSearchCompactTriggerClick;

        if (_searchExpanded is not null) _searchExpanded.PreviewKeyDown -= OnSearchExpandedPreviewKeyDown;
        _searchPopup = GetTemplateChild("PART_SearchPopup") as Popup;
        _searchExpanded = GetTemplateChild("PART_SearchExpanded") as TextBox;
        if (_searchExpanded is not null) _searchExpanded.PreviewKeyDown += OnSearchExpandedPreviewKeyDown;

        LayoutUpdated += OnToolbarLayoutUpdated;

        ApplySearchGeometry();
    }

    // U-13 — 압축 아이콘을 누르면 전체 폭(320) 오버레이가 뜬다. Popup 은 별도 레이어라 툴바 폭에 잘리지 않는다.
    private void OnSearchCompactTriggerClick(object sender, RoutedEventArgs e)
    {
        if (_searchPopup is null || _searchExpanded is null) return;
        _searchPopup.IsOpen = true;
        _searchExpanded.Dispatcher.BeginInvoke(new Action(() =>
        {
            _searchExpanded.Focus();
            _searchExpanded.CaretIndex = _searchExpanded.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnSearchExpandedPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _searchPopup is null) return;
        _searchPopup.IsOpen = false;
        e.Handled = true;
    }

    // 필터 · Extra 내용은 이 컨트롤의 SizeChanged 밖에서도 바뀐다(레일 전환 · "직접" 선택 시 날짜 범위 필드).
    // LayoutUpdated 로 레이아웃이 정착할 때마다 다시 잰다 — 판정이 바뀔 때만 값을 쓰므로 무한 루프가 없다.
    private void OnToolbarLayoutUpdated(object? sender, EventArgs e) => UpdateLayoutState();

    private ConsoleToolbarFit _fit = new(ConsoleToolbarOverflow.None, ConsoleLayoutMath.ToolbarSearchPreferredWidth, false);

    /// <summary>
    /// 두 줄의 모양을 정한다 — ① 둘째 줄(필터)을 보일지, ② 첫째 줄에서 무엇을 [⋯] 로 옮기고 검색에 얼마를 줄지.
    /// 판정은 <see cref="ConsoleLayoutMath.ResolveToolbarFit"/>(순수 함수)가 하고 여기서는 적용만 한다.
    /// </summary>
    private void UpdateLayoutState()
    {
        UpdateFilterRow();

        if (_leftCluster is null || _rightCluster is null) return;

        // 입력은 모두 "제자리일 때의 폭" 이다 — 접힘을 바꾼 뒤의 실제 폭을 넣으면 접었다 폈다 진동한다.
        // 빈 Extra 자리(내용이 없거나, 있어도 안의 단추가 전부 숨어 폭 0 인 채 제 여백 8 만 차지하는 ContentPresenter)는
        // 옮길 것이 없는 고정 폭으로 센다 — 0 으로 세면 [열 n/m] 이 정확히 그 8 만큼 잘린다(잘림 감사: 장비 6.3 · 900 접힘).
        var extra = ExtraNaturalWidth();
        var fixedLeft = FixedLeftWidth() + (extra > 0 ? 0 : VisibleMargin(_extraPresenter));
        var fit = ConsoleLayoutMath.ResolveToolbarFit(ActualWidth, fixedLeft, extra, ColumnsButtonWidth(), ShowSearch);
        ApplyOverflow(fit.Overflow);

        if (fit.IsSearchCompact != IsSearchCompact) IsSearchCompact = fit.IsSearchCompact;
        if (fit.IsSearchCompact == _fit.IsSearchCompact && Math.Abs(fit.SearchWidth - _fit.SearchWidth) < 0.5) { _fit = fit; return; }
        _fit = fit;
        ApplySearchGeometry();
    }

    /// <summary>필터 줄은 보이는 필터가 있을 때만 선다 — 늘 트리에 있는 ContentPresenter 의 원하는 크기로 판정한다.</summary>
    private void UpdateFilterRow()
    {
        var has = Filters is not null
                  && _filterPresenter is not null
                  && _filterPresenter.DesiredSize.Width > 0.5
                  && _filterPresenter.DesiredSize.Height > 0.5;
        if (has != HasFilterRow) HasFilterRow = has;
    }

    // 판정(ConsoleLayoutMath, 순수 함수)과 적용(여기)을 분리한다. 가운데 칸의 MinWidth 도 검색과 함께 건다 —
    // Auto 칸(왼쪽 · 오른쪽 묶음)이 검색 자리를 눌러 먹지 못하게(판정이 이미 들어가는 것을 보장했다).
    private void ApplySearchGeometry()
    {
        if (_search is null || _grid is null || _grid.ColumnDefinitions.Count < 2) return;

        if (_fit.IsSearchCompact || !ShowSearch)
        {
            _grid.ColumnDefinitions[1].MinWidth = _fit.IsSearchCompact && ShowSearch
                ? ConsoleLayoutMath.ToolbarSearchCompactMinWidth + ConsoleLayoutMath.ToolbarSearchLeftMargin
                : 0;
            return;
        }

        _search.Width = _fit.SearchWidth;
        _grid.ColumnDefinitions[1].MinWidth = _fit.SearchWidth + ConsoleLayoutMath.ToolbarSearchLeftMargin;
    }

    #region - U-17/U-18 넘침([⋯]) — Extra → [열 n/m] 순으로 옮긴다 -
    // 템플릿을 늘리지 않는다(x:Name 신설 금지) — 오른쪽 묶음(StackPanel, Grid.Column 2)에 코드로 [⋯] 버튼과 팝업을 붙이고,
    // Extra 를 담는 ContentPresenter 는 왼쪽 묶음(Grid.Column 0)의 마지막 ContentPresenter 로 찾는다.
    private FrameworkElement? _columnsButton;
    private ContentPresenter? _extraPresenter;
    private ButtonBase? _overflowButton;
    private Popup? _overflowPopup;
    private ContentPresenter? _overflowHost;
    private ButtonBase? _overflowColumnsButton;
    private ConsoleToolbarOverflow _overflow;
    private double _columnsNaturalWidth;

    private void BuildOverflowParts()
    {
        if (_overflowButton is not null) _overflowButton.Click -= OnOverflowClick;
        if (_overflowColumnsButton is not null) _overflowColumnsButton.Click -= OnOverflowColumnsClick;
        _overflowButton = null;
        _overflowPopup = null;
        _overflowHost = null;
        _overflowColumnsButton = null;
        _extraPresenter = null;
        _overflow = ConsoleToolbarOverflow.None;
        _columnsNaturalWidth = 0;
        IsExtraOverflow = false;
        IsColumnsOverflow = false;

        if (_rightCluster is not Panel panel) return;
        _extraPresenter = (_leftCluster as Panel)?.Children.OfType<ContentPresenter>().LastOrDefault();
        if (_extraPresenter is null) return;

        var more = new Button
        {
            Margin = new Thickness(ConsoleLayoutMath.ToolbarOverflowButtonWidth - ConsoleLayoutMath.ToolbarSearchCompactMinWidth, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Content = new TextBlock { Text = "⋯", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
            ToolTip = "이 창의 다른 동작 — 폭이 좁아 여기에 모았습니다",
        };
        more.SetResourceReference(StyleProperty, "Console.Button.Icon");
        AutomationProperties.SetName(more, "더 보기");
        BindingOperations.SetBinding(more, AutomationProperties.AutomationIdProperty,
            new Binding(nameof(ConsoleKey)) { Source = this, StringFormat = "Console.{0}.Toolbar.More" });
        more.Click += OnOverflowClick;

        // 팝업 안: 창 고유 동작 · [열 n/m] — 옮겨진 것만 보인다.
        _overflowHost = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        var columns = new Button { HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed, ToolTip = "기본 열 / 전체 열" };
        columns.SetResourceReference(StyleProperty, "Console.Button.Ghost");
        columns.SetResourceReference(FontFamilyProperty, "MonoFont");
        BindingOperations.SetBinding(columns, ContentControl.ContentProperty, new Binding(nameof(ColumnsText)) { Source = this });
        BindingOperations.SetBinding(columns, AutomationProperties.AutomationIdProperty,
            new Binding(nameof(ConsoleKey)) { Source = this, StringFormat = "Console.{0}.Toolbar.ColumnsOverflow" });
        columns.Click += OnOverflowColumnsClick;
        _overflowColumnsButton = columns;

        var stack = new StackPanel();
        stack.Children.Add(_overflowHost);
        stack.Children.Add(columns);

        var plate = new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = stack,
        };
        plate.SetResourceReference(Border.BackgroundProperty, "SurfaceAltBrush");
        plate.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        plate.SetResourceReference(TextElement.ForegroundProperty, "TextPrimaryBrush");
        // 안의 동작을 누르면 닫는다(처리기가 Handled 를 세워도).
        plate.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => CloseOverflow(false)), handledEventsToo: true);
        plate.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            CloseOverflow(true);
        };

        // 팝업을 묶음 안에 둔다 — 그래야 옮겨 간 Extra 가 창의 DataContext(바인딩)를 그대로 물려받는다.
        var popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = false,
            PlacementTarget = more,
            Placement = PlacementMode.Custom,
            // 오른쪽 끝을 [⋯] 의 오른쪽 끝에 맞춘다 — 툴바 오른쪽 끝의 버튼이라 왼쪽 정렬이면 콘솔 밖으로 나간다.
            CustomPopupPlacementCallback = (popupSize, targetSize, _) => new[]
            {
                new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height + 4), PopupPrimaryAxis.Horizontal),
            },
            Child = plate,
        };

        panel.Children.Add(more);
        panel.Children.Add(popup);
        _overflowButton = more;
        _overflowPopup = popup;
    }

    private void OnOverflowClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_overflowPopup is null) return;
        _overflowPopup.IsOpen = true;
        _overflowPopup.Child?.Dispatcher.BeginInvoke(new Action(() =>
            _overflowPopup.Child?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>팝업 안의 [열 n/m] — 제자리 버튼과 같은 이벤트를 낸다(창은 차이를 모른다).</summary>
    private void OnOverflowColumnsClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseOverflow(false);
        RaiseEvent(new RoutedEventArgs(ColumnsClickEvent, this));
    }

    private void CloseOverflow(bool refocus)
    {
        if (_overflowPopup is null || !_overflowPopup.IsOpen) return;
        _overflowPopup.IsOpen = false;
        if (refocus) _overflowButton?.Focus();
    }

    private void OnExtraChanged()
    {
        if (IsExtraOverflow && _overflowHost is not null)
        {
            _overflowHost.Content = null;
            _overflowHost.Content = Extra;
            _overflowHost.ApplyTemplate();
        }
    }

    /// <summary>[추가] · [삭제] · [갱신] 등 왼쪽 묶음에서 옮기지 않는 부분의 폭(간격 포함).</summary>
    private double FixedLeftWidth()
    {
        if (_leftCluster is not Panel panel) return _leftCluster?.ActualWidth ?? 0;
        double width = 0;
        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            if (ReferenceEquals(child, _extraPresenter) || child.Visibility == Visibility.Collapsed) continue;
            width += child.ActualWidth + child.Margin.Left + child.Margin.Right;
        }
        return width;
    }

    /// <summary>제자리 ContentPresenter 가 보이면 그 좌우 여백(접혀 숨었으면 0).</summary>
    private static double VisibleMargin(FrameworkElement? home)
        => home is { Visibility: Visibility.Visible } ? home.Margin.Left + home.Margin.Right : 0;

    /// <summary>슬롯에 내용이 없는데 제자리 ContentPresenter 가 보이면 그 여백만 차지한다(옮길 것이 없다).</summary>
    private static double EmptySlotMargin(ContentPresenter? home, object? content)
        => home is { Visibility: Visibility.Visible } && (content is null || content is UIElement { Visibility: Visibility.Collapsed })
            ? home.Margin.Left + home.Margin.Right
            : 0;

    /// <summary>열 버튼이 차지하는 폭(간격 포함). 글자가 없어 숨었으면 0 — 접혀 있으면 마지막으로 잰 제자리 폭.</summary>
    private double ColumnsButtonWidth()
    {
        if (_columnsButton is null || string.IsNullOrEmpty(ColumnsText)) return 0;
        if (!IsColumnsOverflow && _columnsButton.Visibility == Visibility.Visible && _columnsButton.ActualWidth > 0)
            _columnsNaturalWidth = _columnsButton.ActualWidth + _columnsButton.Margin.Left + _columnsButton.Margin.Right;
        return _columnsNaturalWidth;
    }

    /// <summary>
    /// Extra 가 제자리에 있을 때의 폭(간격 포함) — 접혀 있는 동안에도 같은 값을 내야 판정이 진동하지 않는다.
    /// 제자리면 실제 폭, 팝업에 가 있으면 그 요소를 무한 폭으로 재서 얻는다. 내용이 없으면 0(그때의 여백은 <see cref="VisibleMargin"/> 로 고정 폭에 센다).
    /// </summary>
    private double ExtraNaturalWidth()
    {
        if (_extraPresenter is null || EmptySlotMargin(_extraPresenter, Extra) > 0) return 0;
        var gap = _extraPresenter.Margin.Left + _extraPresenter.Margin.Right;

        if (!IsExtraOverflow) return _extraPresenter.Visibility == Visibility.Visible && _extraPresenter.ActualWidth > 0 ? _extraPresenter.ActualWidth + gap : 0;

        if (Extra is not UIElement element || element.Visibility == Visibility.Collapsed) return 0;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width > 0 ? element.DesiredSize.Width + gap : 0;
    }

    private void ApplyOverflow(ConsoleToolbarOverflow overflow)
    {
        if (_extraPresenter is null || _overflowButton is null || _overflowHost is null) return;
        if (overflow == _overflow) return;
        if (overflow == ConsoleToolbarOverflow.None) CloseOverflow(false);

        // 한 요소는 부모를 하나만 가진다 — 제자리에서 먼저 떼고 팝업에 싣는다(돌아올 때는 거꾸로).
        // 닫힌 팝업 안의 ContentPresenter 는 레이아웃을 타지 않아 내용을 제 자식으로 붙이지 않는다 — 그러면 옮긴 요소가
        // 부모 없이 떠서 DataContext 를 못 물려받고(바인딩이 풀려 숨었던 버튼이 다 보인다: 실측 폭 368 → 457) 폭 판정도 틀린다.
        Move(overflow.HasFlag(ConsoleToolbarOverflow.Extra), _extraPresenter, _overflowHost, Extra);

        if (_columnsButton is not null && _overflowColumnsButton is not null)
        {
            if (overflow.HasFlag(ConsoleToolbarOverflow.Columns))
            {
                _columnsButton.Visibility = Visibility.Collapsed;
                _overflowColumnsButton.Visibility = Visibility.Visible;
            }
            else
            {
                _columnsButton.ClearValue(VisibilityProperty);                 // 템플릿 트리거(글자 없으면 숨김)로 돌아간다
                _overflowColumnsButton.Visibility = Visibility.Collapsed;
            }
            // 팝업 안에서 둘 다 보이면 사이를 띄운다.
            _overflowColumnsButton.Margin = new Thickness(0, overflow.HasFlag(ConsoleToolbarOverflow.Extra) ? 8 : 0, 0, 0);
        }

        _overflowButton.Visibility = overflow == ConsoleToolbarOverflow.None ? Visibility.Collapsed : Visibility.Visible;
        _overflow = overflow;
        IsExtraOverflow = overflow.HasFlag(ConsoleToolbarOverflow.Extra);
        IsColumnsOverflow = overflow.HasFlag(ConsoleToolbarOverflow.Columns);
    }

    private static void Move(bool toOverflow, ContentPresenter home, ContentPresenter host, object? content)
    {
        var isOut = host.Content is not null && ReferenceEquals(host.Content, content);
        if (toOverflow == isOut) return;

        if (toOverflow)
        {
            home.Content = null;
            home.Visibility = Visibility.Collapsed;
            host.Content = content;
            host.Visibility = Visibility.Visible;
            host.ApplyTemplate();
        }
        else
        {
            host.Content = null;
            home.ClearValue(ContentPresenter.ContentProperty);   // 템플릿의 TemplateBinding 으로 돌아간다
            home.ClearValue(VisibilityProperty);
        }
    }
    #endregion

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
