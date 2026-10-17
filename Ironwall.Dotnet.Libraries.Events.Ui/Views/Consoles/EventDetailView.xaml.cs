using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>이벤트 상세 칸 — 읽기가 기본, 판정만 상자로 그린다. 뷰는 배선만 한다(일은 뷰모델 · 대시보드가 한다).</summary>
public partial class EventDetailView : UserControl
{
    public EventDetailView()
    {
        InitializeComponent();
    }

    private EventDetailViewModel? Model => DataContext as EventDetailViewModel;

    private void OnReport(object sender, RoutedEventArgs e) => Model?.Request(EventDetailAction.Report);

    private void OnQueueToTray(object sender, RoutedEventArgs e) => Model?.Request(EventDetailAction.QueueToTray);

    private void OnDetectionHistory(object sender, RoutedEventArgs e) => Model?.Request(EventDetailAction.DetectionHistory);

    private void OnOpenOrigin(object sender, RoutedEventArgs e) => Model?.Request(EventDetailAction.OpenOrigin);

    private void OnReloadActions(object sender, RoutedEventArgs e) => _ = Model?.Actions.ReloadAsync();

    /// <summary>[조치 내역 n건] — 같은 칸 아래의 조치 내역 절로 내려간다(서버 호출 없음).</summary>
    private void OnJumpToActions(object sender, RoutedEventArgs e)
    {
        if (FindByAutomationId(this, "Console.Events.Detail.ActionsTitle") is FrameworkElement title)
            title.BringIntoView(new Rect(0, 0, title.ActualWidth, Math.Max(title.ActualHeight, 120)));
        Model?.Request(EventDetailAction.JumpToActions);
    }

    private static DependencyObject? FindByAutomationId(DependencyObject root, string id)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (AutomationProperties.GetAutomationId(child) == id) return child;
            if (FindByAutomationId(child, id) is { } found) return found;
        }
        return null;
    }
}
