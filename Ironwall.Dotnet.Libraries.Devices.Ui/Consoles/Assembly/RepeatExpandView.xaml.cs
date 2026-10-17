using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>반복 펼치기. ESC · Enter · 첫 포커스는 틀(<c>ConsoleDialogFrame</c>)이 정한다.</summary>
public partial class RepeatExpandView : UserControl
{
    public RepeatExpandView()
    {
        InitializeComponent();
    }

    private RepeatExpandViewModel? ViewModel => DataContext as RepeatExpandViewModel;

    private async void OnExpand(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.ExpandAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }

}
