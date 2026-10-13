using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>위치 프리셋 편집기(W2) — 단추는 뷰모델 메서드로 곧장 잇는다.</summary>
public partial class MountPresetEditorView : UserControl
{
    public MountPresetEditorView()
    {
        InitializeComponent();
    }

    private MountPresetEditorViewModel? ViewModel => DataContext as MountPresetEditorViewModel;

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) { vm.ApplyFields(); await vm.SaveAsync(); } } catch { /* 안내는 상태줄 */ }
    }

    private async void OnSaveAsNew(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) { vm.ApplyFields(); await vm.SaveAsNewAsync(); } } catch { /* 같다 */ }
    }

    private async void OnClose(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CloseAsync(); } catch { /* 같다 */ }
    }

    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.Revert();
    private void OnSuggestName(object sender, RoutedEventArgs e) => ViewModel?.UseSuggestedName();
    private void OnFaceAlong(object sender, RoutedEventArgs e) => ViewModel?.FaceAlong();
    private void OnFaceOutward(object sender, RoutedEventArgs e) => ViewModel?.FaceOutward();
    private void OnFaceInward(object sender, RoutedEventArgs e) => ViewModel?.FaceInward();

    private void OnFieldKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        vm.ApplyFields();
        e.Handled = true;
    }

    private void OnFieldLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.ApplyFields();
}
