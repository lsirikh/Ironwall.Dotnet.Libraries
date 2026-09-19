using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
    #endregion

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Hook("PART_Add", AddClickEvent);
        Hook("PART_Delete", DeleteClickEvent);
        Hook("PART_Refresh", RefreshClickEvent);
        Hook("PART_Columns", ColumnsClickEvent);
    }

    private void Hook(string part, RoutedEvent routed)
    {
        if (GetTemplateChild(part) is ButtonBase button)
            button.Click += (_, e) => { e.Handled = true; RaiseEvent(new RoutedEventArgs(routed, this)); };
    }

    private static RoutedEvent Ev(string name)
        => EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConsoleToolbar));

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(ConsoleToolbar), new PropertyMetadata(defaultValue));
}
