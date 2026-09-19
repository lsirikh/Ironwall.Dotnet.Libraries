using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

public partial class PresetManagerView : UserControl
{
    private const string FileFilter = "Preset file (*.json)|*.json";

    public PresetManagerView()
    {
        InitializeComponent();
    }

    private PresetManagerViewModel? ViewModel => DataContext as PresetManagerViewModel;

    private void OnOpenInAssembly(object sender, RoutedEventArgs e) => ViewModel?.OpenInAssembly();
    private void OnDuplicate(object sender, RoutedEventArgs e) => ViewModel?.Duplicate();

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.RenameAsync();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.DeleteAsync();
    }

    private async void OnClose(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.CloseAsync();
    }

    // The file pickers are view concerns: the view model only receives a path.
    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = FileFilter, FileName = "device-assembly-presets.json", OverwritePrompt = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) ViewModel?.Export(dialog.FileName);
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = FileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) ViewModel?.Import(dialog.FileName);
    }

    // Esc = 취소. 단추의 IsCancel 에 맡기지 않는다 — IsCancel 은 창을 닫고, 클릭 처리기도 닫아 두 번 닫게 된다.
    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || ViewModel is not { } vm) return;
        e.Handled = true;
        await vm.CloseAsync();
    }
}
