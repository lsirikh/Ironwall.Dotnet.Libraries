using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

public partial class WiringTextPromptView : UserControl
{
    public WiringTextPromptView()
    {
        InitializeComponent();
    }

    private WiringTextPromptViewModel? ViewModel => DataContext as WiringTextPromptViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ConfirmAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || ViewModel is not { } vm) return;
        e.Handled = true;
        await vm.CancelAsync();
    }

    private void OnTextLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }
}
