using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 조치 트레이 — 드롭존이면서 <b>[적용] · [되돌리기] · [중단]</b> 을 쥔 자리.
/// </summary>
/// <remarks>DataContext 는 콘솔 뷰모델이다(<c>Tray</c> · <c>DropHandler</c> 를 여기서 읽는다).</remarks>
public partial class ActionTrayView : UserControl
{
    public ActionTrayView()
    {
        InitializeComponent();
    }

    private EventDashboardViewModel? Console => DataContext as EventDashboardViewModel;

    private void OnApply(object sender, RoutedEventArgs e) => Console?.ApplyTray();

    private void OnRevert(object sender, RoutedEventArgs e) => Console?.RevertTray();

    private void OnCancel(object sender, RoutedEventArgs e) => Console?.CancelTray();
}
