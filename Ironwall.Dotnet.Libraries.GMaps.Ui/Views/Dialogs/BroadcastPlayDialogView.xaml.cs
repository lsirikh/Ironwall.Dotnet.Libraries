using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Dialogs;

public partial class BroadcastPlayDialogView : Window
{
    public BroadcastPlayDialogView()
    {
        InitializeComponent();
    }

    private void OnSend_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
