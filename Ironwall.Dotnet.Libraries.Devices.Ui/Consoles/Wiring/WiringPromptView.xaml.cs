using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

public partial class WiringPromptView : UserControl
{
    public WiringPromptView()
    {
        InitializeComponent();
    }

    private WiringPromptViewModel? ViewModel => DataContext as WiringPromptViewModel;

    private async void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ConfirmAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CancelAsync();
    }

    // Esc = 취소. 단추의 IsCancel 에 맡기지 않는다 — IsCancel 이 창을 닫고 처리기도 닫아 두 번 닫는다.
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || ViewModel is not { } vm) return;
        e.Handled = true;
        await vm.CancelAsync();
    }
}
