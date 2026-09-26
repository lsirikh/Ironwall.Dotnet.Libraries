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

    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra), typeof(object), typeof(ConsoleToolbar), new PropertyMetadata(null, (d, _) => ((ConsoleToolbar)d).OnExtraChanged()));
    /// <summary>맨 오른쪽 자리(창 고유 버튼). 폭이 모자라면 [⋯] 팝업으로 옮겨진다(<see cref="IsExtraOverflow"/>).</summary>
    public object? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    private static readonly DependencyPropertyKey IsExtraOverflowPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsExtraOverflow), typeof(bool), typeof(ConsoleToolbar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsExtraOverflowProperty = IsExtraOverflowPropertyKey.DependencyProperty;
    /// <summary>
    /// U-17 — <see cref="Extra"/> 가 [⋯] 뒤로 접혔는가(읽기 전용, <see cref="ConsoleLayoutMath.ShouldOverflowToolbarExtra"/> 의 결과).
    /// 검색을 0 까지 접어도 오른쪽 끝 버튼이 테두리 밖으로 잘릴 때만 참이 된다 — 마지막 수단이다.
    /// </summary>
    public bool IsExtraOverflow { get => (bool)GetValue(IsExtraOverflowProperty); private set => SetValue(IsExtraOverflowPropertyKey, value); }

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

        _grid = (VisualTreeHelper.GetChild(this, 0) as Border)?.Child as Grid;
        _search = GetTemplateChild("Search") as TextBox;
        _leftCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
        _rightCluster = _grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 2);
        _columnsButton = GetTemplateChild("PART_Columns") as FrameworkElement;
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

    // U-13 — 압축 아이콘을 누르면 전체 폭(320) 오버레이가 뜬다. 예산이 모자라 TextBox 자체를 못 보여줄
    // 때도 이 팝업은 그리드 예산 밖(Popup 은 별도 레이어)이라 절대 잘리지 않는다.
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

        // U-17 — 먼저 오른쪽 묶음을 [⋯] 로 접을지 정한다(상태와 무관한 폭으로 — 진동하지 않는다).
        // 그 결과로 정해지는 오른쪽 폭을 검색 예산에 쓴다: 실제 ActualWidth 는 접힘을 바꾼 다음 레이아웃에서야 바뀐다.
        var columnsWidth = ColumnsButtonWidth();
        var extraWidth = ExtraNaturalWidth();
        var overflow = ConsoleLayoutMath.ShouldOverflowToolbarExtra(ActualWidth, _leftCluster.ActualWidth, columnsWidth, extraWidth, ShowSearch);
        ApplyExtraOverflow(overflow);
        var rightWidth = _extraPresenter is null
            ? _rightCluster.ActualWidth
            : columnsWidth + (overflow ? ConsoleLayoutMath.ToolbarOverflowButtonWidth : extraWidth);

        var resolved = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(ActualWidth, _leftCluster.ActualWidth, rightWidth);
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

    #region - U-17 오른쪽 묶음 넘침([⋯]) -
    // 템플릿을 늘리지 않는다(x:Name 신설 금지) — 오른쪽 묶음(StackPanel, Grid.Column 2)에 코드로 [⋯] 버튼과 팝업을 붙이고,
    // Extra 를 담는 ContentPresenter 는 그 묶음의 마지막 ContentPresenter 로 찾는다.
    private FrameworkElement? _columnsButton;
    private ContentPresenter? _extraPresenter;
    private ButtonBase? _overflowButton;
    private Popup? _overflowPopup;
    private ContentPresenter? _overflowHost;

    private void BuildOverflowParts()
    {
        if (_overflowButton is not null) _overflowButton.Click -= OnOverflowClick;
        _overflowButton = null;
        _overflowPopup = null;
        _overflowHost = null;
        _extraPresenter = null;
        IsExtraOverflow = false;

        if (_rightCluster is not Panel panel) return;
        _extraPresenter = panel.Children.OfType<ContentPresenter>().LastOrDefault();
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

        _overflowHost = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center };
        var plate = new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = _overflowHost,
        };
        plate.SetResourceReference(Border.BackgroundProperty, "SurfaceAltBrush");
        plate.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        plate.SetResourceReference(TextElement.ForegroundProperty, "TextPrimaryBrush");
        // 안의 동작을 누르면 닫는다(처리기가 Handled 를 세워도) · Esc 는 닫고 [⋯] 로 초점을 돌린다.
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
            // 오른쪽 끝을 [⋯] 의 오른쪽 끝에 맞춘다 — 머리 오른쪽 끝의 버튼이라 왼쪽 정렬이면 콘솔 밖으로 나간다.
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

    /// <summary>열 버튼이 차지하는 폭(간격 포함). 숨었으면 0.</summary>
    private double ColumnsButtonWidth()
        => _columnsButton is { Visibility: Visibility.Visible } columns ? columns.ActualWidth + columns.Margin.Left + columns.Margin.Right : 0;

    /// <summary>
    /// Extra 가 제자리에 있을 때의 폭(간격 포함) — 접혀 있는 동안에도 같은 값을 내야 판정이 진동하지 않는다.
    /// 제자리면 실제 폭, 팝업에 가 있으면 그 요소를 무한 폭으로 재서 얻는다.
    /// </summary>
    private double ExtraNaturalWidth()
    {
        if (_extraPresenter is null) return 0;
        var gap = _extraPresenter.Margin.Left + _extraPresenter.Margin.Right;

        if (!IsExtraOverflow) return _extraPresenter.ActualWidth > 0 ? _extraPresenter.ActualWidth + gap : 0;

        if (Extra is not UIElement element || element.Visibility == Visibility.Collapsed) return 0;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width > 0 ? element.DesiredSize.Width + gap : 0;
    }

    private void ApplyExtraOverflow(bool overflow)
    {
        if (_extraPresenter is null || _overflowButton is null || _overflowHost is null) return;
        if (overflow == IsExtraOverflow) return;

        if (overflow)
        {
            // 한 요소는 부모를 하나만 가진다 — 제자리에서 먼저 떼고 팝업에 싣는다.
            _extraPresenter.Content = null;
            _extraPresenter.Visibility = Visibility.Collapsed;
            _overflowHost.Content = Extra;
            // 닫힌 팝업 안의 ContentPresenter 는 레이아웃을 타지 않아 내용을 제 자식으로 붙이지 않는다 — 그러면 Extra 가
            // 부모 없이 떠서 DataContext 를 못 물려받고(바인딩이 풀려 숨었던 버튼이 다 보인다: 실측 폭 368 → 457) 폭 판정도 틀린다.
            _overflowHost.ApplyTemplate();
            _overflowButton.Visibility = Visibility.Visible;
        }
        else
        {
            CloseOverflow(false);
            _overflowHost.Content = null;
            _extraPresenter.ClearValue(ContentPresenter.ContentProperty);   // 템플릿의 TemplateBinding 으로 돌아간다
            _extraPresenter.ClearValue(VisibilityProperty);
            _overflowButton.Visibility = Visibility.Collapsed;
        }
        IsExtraOverflow = overflow;
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
