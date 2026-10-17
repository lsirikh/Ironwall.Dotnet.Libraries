using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;

/// <summary>진행 창. ESC · 머리의 닫기는 틀이 '보조'로 모아 주고, 보조는 끝나기 전이면 취소다.</summary>
public partial class ProgressDialogView : UserControl
{
    public ProgressDialogView()
    {
        InitializeComponent();
    }

    private ProgressDialogViewModel? ViewModel => DataContext as ProgressDialogViewModel;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (vm.IsDone) { _ = vm.CloseAsync(); return; }
        vm.RequestCancel();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) _ = vm.CloseAsync();
    }
}
