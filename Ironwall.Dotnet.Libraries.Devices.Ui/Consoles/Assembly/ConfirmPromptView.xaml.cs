using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>예 · 아니오. ESC · Enter · 첫 포커스는 틀(<c>ConsoleDialogFrame</c>)이 정한다.</summary>
public partial class ConfirmPromptView : UserControl
{
    public ConfirmPromptView()
    {
        InitializeComponent();
    }

    private ConfirmPromptViewModel? ViewModel => DataContext as ConfirmPromptViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.AcceptAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }
}
