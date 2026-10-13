using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>프리셋 저장 확인(W5).</summary>
public partial class PresetSaveConfirmView : UserControl
{
    public PresetSaveConfirmView()
    {
        InitializeComponent();
    }

    private PresetSaveConfirmViewModel? ViewModel => DataContext as PresetSaveConfirmViewModel;

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.SaveAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnSaveAsNew(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.SaveAsNewAsync(); } catch { /* 같다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }
}
