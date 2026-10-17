using Caliburn.Micro;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dialogs;

/// <summary>함체 임계값 창. 닫기 · ESC 는 틀(<c>ConsoleDialogFrame</c>)이 한 길로 모은다.</summary>
public partial class EnclosureThresholdDialogView : UserControl
{
    public EnclosureThresholdDialogView()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (DataContext is IScreen screen) _ = screen.TryCloseAsync(false);
    }
}
