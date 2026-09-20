using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 장비 셋업 · 결선맵의 뷰. 뷰모델은 창도 클립보드도 모르고, 여기서는 <b>전달만</b> 한다.
/// </summary>
/// <remarks>
/// <para>키보드 폴백이 여기 있다 — 드래그 전용 UI 를 만들지 않는다(자동화가 좌표 클릭에 묶인다).
/// <c>Alt+←/→</c> 는 WPF 에서 <see cref="Key.System"/> 으로 도착하고 실제 키는 <see cref="KeyEventArgs.SystemKey"/> 에 있다 —
/// <c>e.Key == Key.Left</c> 로 짠 폴백은 영원히 뜨지 않는다.</para>
/// </remarks>
public partial class WiringView : UserControl
{
    public WiringView()
    {
        InitializeComponent();
    }

    private WiringViewModel? ViewModel => DataContext as WiringViewModel;

    #region - Head · steps -
    private void OnGoSensors(object sender, RoutedEventArgs e) => ViewModel?.GoSensors();
    private void OnGoWiring(object sender, RoutedEventArgs e) => ViewModel?.GoWiring();
    private void OnUndo(object sender, RoutedEventArgs e) => ViewModel?.Undo();
    private void OnAutoLayout(object sender, RoutedEventArgs e) => ViewModel?.AutoLayout();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.SaveAsync();
    }
    #endregion

    #region - Sensor table -
    private void OnAddRow(object sender, RoutedEventArgs e) => ViewModel?.AddOneRow();

    private async void OnMakeSensors(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.MakeSensorsAsync();
    }

    private async void OnPaste(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.PasteAsync();
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        ViewModel?.OnSelectionChanged(grid.SelectedItems.OfType<SensorRowViewModel>().ToList());
    }

    private void OnApplyEdit(object sender, RoutedEventArgs e) => ViewModel?.ApplyEdit();
    private void OnCancelEdit(object sender, RoutedEventArgs e) => ViewModel?.CancelEdit();
    private void OnUnifyFirst(object sender, RoutedEventArgs e) => ViewModel?.UnifyWithFirst();

    private void OnToggleGroup(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WiringGroupCheckViewModel group) ViewModel?.ToggleGroup(group);
    }

    private async void OnFillSequential(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.FillSequentialAsync();
    }

    private async void OnNameRule(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyNameRuleAsync();
    }
    #endregion

    #region - Wiring map -
    private void OnAddSlotPrimary(object sender, RoutedEventArgs e) => ViewModel?.AddSlotPrimary();
    private void OnAddSlotSecondary(object sender, RoutedEventArgs e) => ViewModel?.AddSlotSecondary();
    private void OnUnplaceSelected(object sender, RoutedEventArgs e) => ViewModel?.UnplaceSelected();

    private void OnRemoveSlot(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WiringSlotViewModel slot) ViewModel?.Unplace(slot);
    }

    /// <summary>고른 칸은 한 선에서만 — 키보드 명령이 어느 칸을 가리키는지 흔들리지 않게.</summary>
    private void OnSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list || ViewModel is not { } vm) return;

        foreach (var slot in e.RemovedItems.OfType<WiringSlotViewModel>()) slot.IsSelected = false;
        foreach (var slot in e.AddedItems.OfType<WiringSlotViewModel>()) slot.IsSelected = true;

        if (e.AddedItems.Count == 0) return;

        var other = ReferenceEquals(list.ItemsSource, vm.Line1) ? vm.Line2 : vm.Line1;
        foreach (var slot in other) slot.IsSelected = false;
    }

    /// <summary>선 위의 키보드 폴백 — Alt+←/→ 는 <see cref="Key.System"/> 으로 온다.</summary>
    private void OnLinePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (e.Key == Key.System && e.SystemKey is Key.Left)
        {
            vm.MoveSelectedBack();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.System && e.SystemKey is Key.Right)
        {
            vm.MoveSelectedForward();
            e.Handled = true;
            return;
        }

        // Alt+↑ · Alt+↓ = 다른 선으로 옮기기(C6) — 선 안에서만 움직이면 건너갈 길이 없다.
        if (e.Key == Key.System && e.SystemKey is Key.Up or Key.Down)
        {
            vm.MoveSelectedToOtherLine();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            vm.UnplaceSelected();
            e.Handled = true;
        }
    }

    /// <summary>팔레트의 키보드 폴백 — Enter = 첫 빈 칸에 붙이기.</summary>
    private void OnPalettePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        if (sender is not ListBox list) return;

        vm.PlaceManyFromPalette(list.SelectedItems.OfType<SensorRowViewModel>().ToList());
        e.Handled = true;
    }
    #endregion
}

/// <summary>검증 한 줄의 심각도 → 색. 색 <b>하나로</b> 뜻을 전하지 않는다(글자 표지가 함께 간다).</summary>
public sealed class IssueLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as WiringIssueLevel? switch
        {
            WiringIssueLevel.Critical => "StatusCriticalBrush",
            WiringIssueLevel.Warning => "StatusWarningBrush",
            _ => "StatusInfoBrush",
        };

        // 매번 다시 찾는다 — 한 번 찾아 캐시하면 테마를 바꿔도 옛 색으로 굳는다.
        return Application.Current?.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>붙여넣기 열의 뜻 → 사람 말(W7).</summary>
public sealed class PasteColumnTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value as PasteColumn? switch
        {
            PasteColumn.Number => "번호",
            PasteColumn.Name => "이름",
            PasteColumn.Type => "종류",
            PasteColumn.Zone => "구역",
            _ => "읽지 않음",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>심각도 → 글자 표지(형태로 구분).</summary>
public sealed class IssueLevelGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value as WiringIssueLevel? switch
        {
            WiringIssueLevel.Critical => "✕",
            WiringIssueLevel.Warning => "⚠",
            _ => "ℹ",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
