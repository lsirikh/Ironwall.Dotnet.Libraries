using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

public partial class MakeSensorsView : UserControl
{
    public MakeSensorsView()
    {
        InitializeComponent();
    }

    private MakeSensorsViewModel? ViewModel => DataContext as MakeSensorsViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.MakeAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }

}
