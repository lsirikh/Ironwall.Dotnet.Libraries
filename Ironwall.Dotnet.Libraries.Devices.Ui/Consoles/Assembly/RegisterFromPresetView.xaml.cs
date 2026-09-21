using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

public partial class RegisterFromPresetView : UserControl
{
    public RegisterFromPresetView()
    {
        InitializeComponent();
    }

    private RegisterFromPresetViewModel? ViewModel => DataContext as RegisterFromPresetViewModel;

    private async void OnRegister(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RegisterAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }

}
