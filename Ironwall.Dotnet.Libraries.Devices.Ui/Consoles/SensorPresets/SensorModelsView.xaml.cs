using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>센서 모델 창(W1 · W3) — 단추는 뷰모델 메서드로, 파일 고르기만 여기서.</summary>
public partial class SensorModelsView : UserControl
{
    public SensorModelsView()
    {
        InitializeComponent();
    }

    private SensorModelsViewModel? ViewModel => DataContext as SensorModelsViewModel;

    private static async void Run(Func<System.Threading.Tasks.Task> work)
    {
        try { await work(); } catch { /* 안내는 뷰모델 상태줄 */ }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Run(() => ViewModel?.CloseAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    /// <summary>[+ 모델] — 이름은 "새 모델 n", 종류 · 모양은 스마트로 시작. 만든 뒤 이름 칸에 포커스(바로 고쳐 쓴다 · MNT-R28-102).</summary>
    private void OnAddModel(object sender, RoutedEventArgs e)
        => Run(async () =>
        {
            if (ViewModel is not { } vm) return;
            if (await vm.AddModelAsync(vm.NextNewModelName(), EnumDeviceType.SmartSensor, SensorShape.Smart) is null) return;
            if (FindById(this, "Devices.SensorPresets.Models.Name") is TextBox box)
            {
                box.Focus();
                box.SelectAll();
            }
        });

    /// <summary>목록에서 고른 줄이 보이게 — 새 줄 · 다시 읽은 뒤의 선택이 화면 밖에 있지 않도록.</summary>
    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: { } item } list) list.ScrollIntoView(item);
    }

    /// <summary>이름 칸 Enter — 저장(창 기본 단추로 새지 않게 소비).</summary>
    private void OnNameKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        e.Handled = true;
        Run(async () => await vm.RenameModelAsync());
    }

    private void OnNameLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.RenameModelAsync(); });

    private static FrameworkElement? FindById(DependencyObject root, string id)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && System.Windows.Automation.AutomationProperties.GetAutomationId(fe) == id) return fe;
            if (FindById(child, id) is { } deeper) return deeper;
        }
        return null;
    }

    private void OnToggleHidden(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.ToggleHiddenAsync(); });
    private void OnDeleteModel(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.DeleteModelAsync(); });
    private void OnEditMount(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.EditMountPresetAsync(); });
    private void OnMountDoubleClick(object sender, MouseButtonEventArgs e) => OnEditMount(sender, e);
    private void OnNewMount(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.NewMountPresetAsync(); });
    private void OnDuplicateMount(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.DuplicateMountPresetAsync(); });
    private void OnDefaultMount(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.MakeMountDefaultAsync(); });
    private void OnDeleteMount(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.DeleteMountPresetAsync(); });
    private void OnAddAttrRow(object sender, RoutedEventArgs e) => ViewModel?.AddAttributeRow();
    private void OnSaveAttr(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.SaveAttributePresetAsync(); });
    private void OnNewAttr(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.NewAttributePresetAsync(); });
    private void OnDuplicateAttr(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.DuplicateAttributePresetAsync(); });
    private void OnDefaultAttr(object sender, RoutedEventArgs e) => Run(async () => { if (ViewModel is { } vm) await vm.MakeAttributeDefaultAsync(); });

    /// <summary>[내보내기] — JSON 파일로.</summary>
    private void OnExport(object sender, RoutedEventArgs e) => Run(async () =>
    {
        if (ViewModel is not { } vm) return;
        var dialog = new SaveFileDialog { Filter = "센서 프리셋 (*.json)|*.json", FileName = $"sensor-presets-{DateTime.Now:yyyyMMdd}.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        if (await vm.ExportAsync() is { } json) await File.WriteAllTextAsync(dialog.FileName, json, new UTF8Encoding(true));
    });

    /// <summary>[가져오기] — JSON 파일을 합친다.</summary>
    private void OnImport(object sender, RoutedEventArgs e) => Run(async () =>
    {
        if (ViewModel is not { } vm) return;
        var dialog = new OpenFileDialog { Filter = "센서 프리셋 (*.json)|*.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        await vm.ImportAsync(await File.ReadAllTextAsync(dialog.FileName));
    });
}
