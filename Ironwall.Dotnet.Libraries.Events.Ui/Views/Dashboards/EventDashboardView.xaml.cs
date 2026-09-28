using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Dashboards;

/// <summary>
/// 이벤트 콘솔 — 레일 · 목록 · 상세 3단. 뷰는 <b>배선만</b> 한다(판정은 전부 뷰모델과 순수 함수에 있다).
/// </summary>
public partial class EventDashboardView : UserControl
{
    public EventDashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => BindToolbarParts();
    }

    private EventDashboardViewModel? Model => DataContext as EventDashboardViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EventDashboardViewModel old)
        {
            old.RowFocusRequested -= OnRowFocusRequested;
            old.SelectionRemapped -= OnSelectionRemapped;
        }
        if (e.NewValue is EventDashboardViewModel next)
        {
            next.RowFocusRequested += OnRowFocusRequested;
            next.SelectionRemapped += OnSelectionRemapped;
        }
    }

    /// <summary>
    /// 뷰모델이 고른 행을 같은 이벤트의 새 인스턴스로 옮겼다(목록 다시 읽기) — 그리드 선택을 그 행들로 맞춘다.
    /// 관문을 다시 묻지 않는다(선택이 바뀐 것이 아니라 같은 이벤트를 다시 가리키는 것이다).
    /// </summary>
    private void OnSelectionRemapped(IReadOnlyList<object> rows)
    {
        var grid = Descendants<DataGrid>(this).FirstOrDefault(g => g.Visibility == Visibility.Visible && rows.All(r => g.Items.Contains(r)));
        if (grid is null || Model is null) return;
        _restoringSelection = true;
        using (Model.SuppressSelectionGuard())
        {
            try
            {
                grid.SelectedItems.Clear();
                foreach (var row in rows) grid.SelectedItems.Add(row);
            }
            finally { _restoringSelection = false; }
        }
    }

    /// <summary>
    /// 이 선택 변경은 목록 다시 읽기가 고른 행을 <b>빼 버린</b> 것뿐인가 — 새로 고른 것은 없고, 빠진 행이 이제 목록에 없다.
    /// 그것은 운영자의 선택이 아니다: 뷰모델에 '선택 없음' 을 넘기지 않는다(같은 이벤트의 새 행이 들어오면 뷰모델이 다시 가리킨다 —
    /// 부대 콘솔 UnitTreeSelectionBridge 와 같은 판단). 정말 지워진 행은 뷰모델이 다시 읽기가 끝난 뒤 뺀다.
    /// </summary>
    private static bool IsReloadDrop(DataGrid grid, SelectionChangedEventArgs e)
        => e.AddedItems.Count == 0 && e.RemovedItems.Count > 0
           && e.RemovedItems.Cast<object>().All(removed => !grid.Items.Contains(removed));

    /// <summary>
    /// 뷰모델이 "이 행을 골라 보여 달라" 고 했다([원본 열기] · '조치 내역 보기'). 레일을 막 옮긴 직후일 수 있어
    /// 그리드의 보임이 정해진 뒤(Loaded 우선순위)에 고른다 — 선택은 평소처럼 그리드를 거쳐 뷰모델로 돌아간다.
    /// </summary>
    private void OnRowFocusRequested(object row)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var grid = Descendants<DataGrid>(this).FirstOrDefault(g => g.IsVisible && g.Items.Contains(row));
            if (grid is null) { Model?.SetSelection(new[] { row }); return; }
            grid.SelectedItems.Clear();
            grid.SelectedItem = row;
            grid.ScrollIntoView(row);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 툴바 [추가] · [삭제] 의 보임을 뷰모델에 묶는다 — 커널 툴바에는 아직 보임 스위치가 없어, 템플릿 부품에 직접 건다
    /// (이벤트 목록의 [이벤트 추가] 는 감추고 억제 스케줄의 [새 스케줄] 만 보인다 · 개요에는 [삭제] 가 없다 — 완성도 감사 E-2 #10 · E-3 #7).
    /// 로컬 값이라 커널에 스위치가 생기면 그쪽으로 옮기면 된다.
    /// </summary>
    private void BindToolbarParts()
    {
        foreach (var toolbar in Descendants<Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleToolbar>(this))
        {
            toolbar.ApplyTemplate();
            Bind(toolbar, "PART_Add", nameof(EventDashboardViewModel.ShowAdd));
            Bind(toolbar, "PART_Delete", nameof(EventDashboardViewModel.ShowDelete));
        }

        static void Bind(Control toolbar, string part, string path)
        {
            if (toolbar.Template?.FindName(part, toolbar) is not FrameworkElement element) return;
            if (BindingOperations.GetBindingExpression(element, VisibilityProperty) is not null) return;
            BindingOperations.SetBinding(element, VisibilityProperty,
                new Binding(path) { Converter = new BooleanToVisibilityConverter(), FallbackValue = Visibility.Visible });
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }

    private bool _restoringSelection;

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid || Model is null) return;
        // 보이지 않는 그리드가 목록을 비우며 내는 선택 변경은 무시한다 — 다른 레일의 선택을 지운다.
        if (grid.Visibility != Visibility.Visible) return;
        if (_restoringSelection) return;
        if (IsReloadDrop(grid, e)) return;

        if (Model.SetSelection(grid.SelectedItems.Cast<object>().ToList())) return;

        // (R6) 미적용 변경 때문에 거절됐다 — 그리드를 직전 선택으로 되돌린다.
        // 장비 콘솔과 같은 방식 — 되돌리는 동안의 알림은 가드를 다시 물지 않는다.
        _restoringSelection = true;
        using (Model.SuppressSelectionGuard())
        {
            try
            {
                grid.SelectedItems.Clear();
                foreach (var row in Model.SelectedRows) grid.SelectedItems.Add(row);
            }
            finally { _restoringSelection = false; }
        }
    }

    private void OnFilterChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventFilterChipOption chip }) Model?.SelectFilterChip(chip.Key);
    }

    private void OnPeriodClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventPeriodOption option } && Model is not null) Model.SelectPeriod(option.Name);
    }

    private void OnReload(object sender, RoutedEventArgs e) => Model?.Reload();

    private void OnCancelQuery(object sender, RoutedEventArgs e) => Model?.CancelQuery();

    private void OnAdd(object sender, RoutedEventArgs e) => Model?.Add();

    private void OnDelete(object sender, RoutedEventArgs e) => Model?.Delete();

    private void OnQueueSelection(object sender, RoutedEventArgs e) => Model?.QueueSelection();

    private void OnApply(object sender, RoutedEventArgs e) => Model?.Apply();

    private void OnRevert(object sender, RoutedEventArgs e) => Model?.Revert();

    /// <summary>억제 목록의 상태 칩(정본 SB L2313).</summary>
    private void OnSuppressionFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SuppressionFilterOption option } && Model?.Suppression is { } console)
            console.FilterKey = option.Key;
    }

    /// <summary>억제 목록의 [모두 정리] — 확인 팝업이 먼저 뜬다.</summary>
    private void OnSuppressionCleanup(object sender, RoutedEventArgs e) => Model?.CleanupSuppression();

    /// <summary>상세 적용 막대의 [수정] — 같은 창 안 780 서랍을 연다(서버 호출 없음).</summary>
    private void OnSuppressionEdit(object sender, RoutedEventArgs e) => _ = Model?.Suppression?.EditSelectedAsync();

    /// <summary>상세 적용 막대의 [취소 예약] — 확인 팝업이 먼저 뜬다.</summary>
    private void OnSuppressionCancelBooking(object sender, RoutedEventArgs e) => _ = Model?.Suppression?.CancelSelectedAsync();

    // N-13 mapping workbench
    private void OnOpenMappingWorkbench(object sender, RoutedEventArgs e) => _ = Model?.OpenMappingWorkbenchAsync();
}

/// <summary>
/// 아이콘 <b>이름</b>을 <see cref="PackIconKind"/> 로 바꾼다.
/// </summary>
/// <remarks>
/// XAML 의 문자열-enum 변환에 기대지 않고 여기서 푼다 — 없는 이름이면 조용히 빈 칸이 되는 대신 눈에 띄는 대체 아이콘을 쓴다.
/// </remarks>
public sealed class EventIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Enum.TryParse<PackIconKind>(value as string, out var kind) ? kind : PackIconKind.CircleOutline;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}
