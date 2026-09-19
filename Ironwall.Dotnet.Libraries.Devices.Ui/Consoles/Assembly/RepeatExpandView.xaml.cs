using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

public partial class RepeatExpandView : UserControl
{
    public RepeatExpandView()
    {
        InitializeComponent();
    }

    private RepeatExpandViewModel? ViewModel => DataContext as RepeatExpandViewModel;

    private async void OnExpand(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ExpandAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }
}
