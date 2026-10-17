using System;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>설정 창 레일 "센서 모델" 블록 — 보일 때 요약을 읽고, 단추는 뷰모델 메서드로.</summary>
public partial class SensorModelsSettingsBlockView : UserControl
{
    public SensorModelsSettingsBlockView()
    {
        InitializeComponent();
        Loaded += (_, _) => Run(() => ViewModel?.LoadAsync() ?? System.Threading.Tasks.Task.CompletedTask);
    }

    private SensorModelsSettingsBlockViewModel? ViewModel => DataContext as SensorModelsSettingsBlockViewModel;

    private static async void Run(Func<System.Threading.Tasks.Task> work)
    {
        try { await work(); } catch { /* 안내는 뷰모델 상태줄 */ }
    }

    private void OnOpen(object sender, RoutedEventArgs e) => Run(() => ViewModel?.OpenAsync() ?? System.Threading.Tasks.Task.CompletedTask);
}
