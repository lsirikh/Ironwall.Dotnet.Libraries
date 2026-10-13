using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>[위치 프리셋 적용…] 창.</summary>
public partial class MountPresetApplyView : UserControl
{
    public MountPresetApplyView()
    {
        InitializeComponent();
    }

    private MountPresetApplyViewModel? ViewModel => DataContext as MountPresetApplyViewModel;

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.ApplyAsync(); } catch { /* 안내는 결선 창 상태줄 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }
}
