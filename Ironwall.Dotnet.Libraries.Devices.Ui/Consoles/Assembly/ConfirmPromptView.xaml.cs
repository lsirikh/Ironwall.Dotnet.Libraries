using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

public partial class ConfirmPromptView : UserControl
{
    public ConfirmPromptView()
    {
        InitializeComponent();
    }

    private ConfirmPromptViewModel? ViewModel => DataContext as ConfirmPromptViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.AcceptAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }
}
