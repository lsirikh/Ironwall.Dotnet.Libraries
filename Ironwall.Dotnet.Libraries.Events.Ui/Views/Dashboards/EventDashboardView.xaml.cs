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
    }

    private EventDashboardViewModel? Model => DataContext as EventDashboardViewModel;

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid || Model is null) return;
        // 보이지 않는 그리드가 목록을 비우며 내는 선택 변경은 무시한다 — 다른 레일의 선택을 지운다.
        if (grid.Visibility != Visibility.Visible) return;

        Model.SetSelection(grid.SelectedItems.Cast<object>().ToList());
    }

    private void OnPeriodClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventPeriodOption option } && Model is not null) Model.Period = option.Name;
    }

    private void OnReload(object sender, RoutedEventArgs e) => Model?.Reload();

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
