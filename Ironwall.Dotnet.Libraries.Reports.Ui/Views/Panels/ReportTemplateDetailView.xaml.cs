using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;

/// <summary>
/// 템플릿 상세 칸의 화면 쪽 배선.
/// </summary>
/// <remarks>
/// ▲/▼ 단추는 드래그 · Alt+↑↓ 와 <b>같은 담당</b>을 부른다 — 세 경로의 결과가 같아야 회귀 단언이 성립한다.
/// </remarks>
public partial class ReportTemplateDetailView : UserControl
{
    public ReportTemplateDetailView() => InitializeComponent();

    private ReportConsoleViewModel? ViewModel => DataContext as ReportConsoleViewModel;

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int direction)
    {
        if (ViewModel is not { } vm) return;

        // 고른 줄이 없으면 아무 일도 하지 않는다 — 조용히 첫 줄을 옮기지 않는다.
        var list = FindComponentList(this);
        var item = list?.SelectedItem as TemplateComponentItem
                   ?? list?.SelectedItems.OfType<TemplateComponentItem>().FirstOrDefault();
        if (item is null) return;

        vm.EditViewModel.MoveComponent(item, direction);
        list!.SelectedItem = item;   // 연달아 누를 수 있게 같은 줄을 쥔 채로 둔다
    }

    private static ListBox? FindComponentList(DependencyObject parent)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is ListBox box
                && System.Windows.Automation.AutomationProperties.GetAutomationId(box) == "Reports.Detail.ComponentList")
                return box;
            if (FindComponentList(child) is { } found) return found;
        }
        return null;
    }
}
