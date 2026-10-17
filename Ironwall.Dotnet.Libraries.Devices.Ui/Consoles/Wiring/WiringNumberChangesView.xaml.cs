using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>저장 전 바뀌는 번호 표. ESC · Enter · 첫 포커스는 틀(<c>ConsoleDialogFrame</c>)이 정한다.</summary>
public partial class WiringNumberChangesView : UserControl
{
    public WiringNumberChangesView()
    {
        InitializeComponent();
    }

    private WiringNumberChangesViewModel? ViewModel => DataContext as WiringNumberChangesViewModel;

    private async void OnConfirm(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.ConfirmAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }
}
