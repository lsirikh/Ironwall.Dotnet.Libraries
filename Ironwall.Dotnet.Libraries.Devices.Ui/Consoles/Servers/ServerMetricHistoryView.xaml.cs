using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;

public partial class ServerMetricHistoryView : UserControl
{
    public ServerMetricHistoryView()
    {
        InitializeComponent();
    }

    // IsCancel 에 맡기지 않는다 — IsCancel 이 창을 닫고 처리기도 닫아 두 번 닫는다(레포 교훈).
    private async void OnClose(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerMetricHistoryViewModel vm) await vm.CloseAsync();
    }
}
