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

    // Esc = 취소. 단추의 IsCancel 에 맡기지 않는다 — IsCancel 은 창을 닫고, 클릭 처리기도 닫아 두 번 닫게 된다.
    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || ViewModel is not { } vm) return;
        e.Handled = true;
        await vm.CancelAsync();
    }
}
