using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>글 한 줄 묻기. ESC · Enter 는 틀이 정한다 — 여기서는 첫 포커스만 입력칸으로 당긴다.</summary>
public partial class TextPromptView : UserControl
{
    public TextPromptView()
    {
        InitializeComponent();
    }

    private TextPromptViewModel? ViewModel => DataContext as TextPromptViewModel;

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.AcceptAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }

    /// <summary>글을 고치러 온 창이다 — 취소가 아니라 입력칸에 커서를 둔다(틀의 기본 포커스를 덮는 유일한 곳).</summary>
    private void OnTextLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }
}
