using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

public partial class PasteReportView : UserControl
{
    public PasteReportView()
    {
        InitializeComponent();
    }

    private PasteReportViewModel? ViewModel => DataContext as PasteReportViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyAsync();
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
}
