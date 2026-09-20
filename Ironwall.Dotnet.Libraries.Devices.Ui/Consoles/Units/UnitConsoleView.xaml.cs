using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 뷰 — 배선만, 판단은 전부 뷰모델 (N-11)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 콘솔 뷰. 코드 비하인드는 <b>이벤트를 뷰모델 호출로 옮기는 일만</b> 한다.
/// </summary>
/// <remarks>
/// <para>키보드 폴백은 <c>PreviewKeyDown</c> 에서 <c>Key.System</c> + <c>e.SystemKey</c> 로 받는다 —
/// <c>Alt+↑</c> 는 WPF 에서 <c>Key.System</c> 으로 도착하고 버블 <c>KeyDown</c> 은 목록이 소비한다
/// (와이어프레임 L239 · L243-244).</para>
/// </remarks>
public partial class UnitConsoleView : UserControl
{
    public UnitConsoleView() => InitializeComponent();

    private UnitConsoleViewModel? Vm => DataContext as UnitConsoleViewModel;

    #region - Toolbar -
    private async void OnAdd(object sender, RoutedEventArgs e) => await Guard(() => { Vm?.BeginCreate(); return System.Threading.Tasks.Task.CompletedTask; });

    private async void OnReload(object sender, RoutedEventArgs e) => await Guard(() => Vm?.ReloadAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private async void OnUndoMove(object sender, RoutedEventArgs e) => await Guard(() => Vm?.UndoMoveAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private void OnEchelonChip(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is UnitEchelonFilterViewModel filter) Vm?.SelectEchelon(filter);
    }
    #endregion

    #region - Tree -
    private async void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list) return;
        await Guard(() => Vm?.SelectRowAsync(list.SelectedItem as UnitNodeRowViewModel) ?? System.Threading.Tasks.Task.CompletedTask);
    }

    private void OnToggleExpand(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is UnitNodeRowViewModel row) Vm?.ToggleExpand(row);
        e.Handled = true;
    }

    /// <summary>Alt+↑ 상위로 · Alt+↓ 바로 위 형제 밑으로 — 드래그의 키보드 폴백.</summary>
    private async void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.System) return;
        if (e.SystemKey is not (Key.Up or Key.Down)) return;

        e.Handled = true;
        var up = e.SystemKey == Key.Up;
        await Guard(() => (up ? Vm?.MoveSelectedUpAsync() : Vm?.MoveSelectedDownAsync()) ?? System.Threading.Tasks.Task.CompletedTask);
    }

    private async void OnMoveToRoot(object sender, RoutedEventArgs e)
    {
        var id = Vm?.SelectedRow?.Id ?? 0;
        if (id <= 0) return;
        await Guard(() => Vm?.MoveAsync(id, null) ?? System.Threading.Tasks.Task.FromResult(false));
    }

    private async void OnMoveToSelectedParent(object sender, RoutedEventArgs e)
    {
        var id = Vm?.SelectedRow?.Id ?? 0;
        if (id <= 0) return;
        await Guard(() => Vm!.MoveAsync(id, Vm.Form.SelectedParentId));
    }
    #endregion

    #region - Detail -
    private async void OnApply(object sender, RoutedEventArgs e) => await Guard(() => Vm?.ApplyAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private void OnRevert(object sender, RoutedEventArgs e) => Vm?.Revert();

    private async void OnDelete(object sender, RoutedEventArgs e) => await Guard(() => Vm?.DeleteAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private async void OnDisable(object sender, RoutedEventArgs e) => await Guard(() => Vm?.DisableAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private void OnDismissDeleteBlock(object sender, RoutedEventArgs e) => Vm?.DismissDeleteBlock();

    private async void OnAddAdjacency(object sender, RoutedEventArgs e)
    {
        var combo = FindCombo("Units.Detail.AdjacencyCombo");
        if ((combo?.SelectedItem as UnitOptionViewModel)?.Id is not int id) return;
        await Guard(() => Vm?.ChangeAdjacencyAsync(id, null) ?? System.Threading.Tasks.Task.CompletedTask);
    }

    private async void OnRemoveAdjacency(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UnitAdjacencyChipViewModel chip) return;
        await Guard(() => Vm?.ChangeAdjacencyAsync(null, chip.Id) ?? System.Threading.Tasks.Task.CompletedTask);
    }
    #endregion

    #region - Devices -
    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list) return;
        Vm?.SetSelectedDevices(list.SelectedItems.OfType<UnitDeviceRowViewModel>());
    }

    private async void OnApplyAssigns(object sender, RoutedEventArgs e) => await Guard(() => Vm?.ApplyAssignsAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    private void OnRevertAssigns(object sender, RoutedEventArgs e) => Vm?.RevertAssigns();
    #endregion

    #region - Helpers -
    /// <summary>
    /// <c>async void</c> 처리기에서 새는 예외는 앱을 죽인다 — 전부 여기서 잡아 상태 띠에 남긴다.
    /// </summary>
    private async System.Threading.Tasks.Task Guard(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError($"[UnitConsoleView] {ex}"); }
    }

    private async System.Threading.Tasks.Task Guard(Func<System.Threading.Tasks.Task<bool>> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError($"[UnitConsoleView] {ex}"); }
    }

    private ComboBox? FindCombo(string automationId) => Find<ComboBox>(this, automationId);

    private static T? Find<T>(DependencyObject root, string automationId) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed && System.Windows.Automation.AutomationProperties.GetAutomationId(child) == automationId) return typed;
            if (Find<T>(child, automationId) is { } found) return found;
        }
        return null;
    }
    #endregion
}

/// <summary>제대 enum → 한글. 목록·콤보가 같은 글자를 쓴다.</summary>
public sealed class EchelonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EnumUnitEchelon echelon ? UnitDropRules.EchelonText(echelon) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary><c>false</c> 일 때 보인다. 토큰과 마찬가지로 매번 다시 평가되므로 테마 전환에도 굳지 않는다.</summary>
public sealed class NotBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
