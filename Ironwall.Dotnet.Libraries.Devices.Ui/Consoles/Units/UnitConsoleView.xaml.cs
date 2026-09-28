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
    private readonly UnitTreeSelectionBridge _treeSelection = new();
    private readonly UnitDetailFocusBridge _detailFocus;

    public UnitConsoleView()
    {
        InitializeComponent();
        // 관계도 Enter = 상세 첫 칸(FR-36) — 선택이 막 바뀐 직후면 상세 폼이 아직 그려지지 않았을 수 있어 입력 차례로 미룬다.
        _detailFocus = new UnitDetailFocusBridge(() => Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input, new Action(() => UnitDetailFocusBridge.FocusFirstField(this))));
        DataContextChanged += (_, _) => _detailFocus.Bind(IsLoaded ? Vm?.Map : null);
        Loaded += (_, _) => _detailFocus.Bind(Vm?.Map);
        Unloaded += (_, _) => _detailFocus.Bind(null);
    }

    private UnitConsoleViewModel? Vm => DataContext as UnitConsoleViewModel;

    #region - Window -
    private bool _chromeApplied;

    /// <summary>
    /// V-38 — 이 콘솔이 <b>제 창의 뿌리</b>일 때(부대 편제 OS 창) 한 번: 테두리에 먹힌 도킹 폭을 되돌리고 제목 줄을 테마 토큰으로 그린다.
    /// 겉은 커널 <see cref="Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleWindowChrome"/> 한 곳이 입힌다(B2 — 모든 콘솔 OS 창 공용, 여러 번 불러도 한 번).
    /// 미리보기 · 다른 창 안에 얹힌 경우(뿌리가 아닐 때)는 남의 창을 건드리지 않는다.
    /// </summary>
    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        if (_chromeApplied || sender is not FrameworkElement shell) return;
        var window = Window.GetWindow(this);
        if (window is null || !ReferenceEquals(window.Content, this)) return;
        _chromeApplied = true;
        Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleWindowChrome.Apply(window, shell);
    }
    #endregion

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
    /// <summary>
    /// 행을 고른다. 몸통은 <see cref="UnitTreeSelectionBridge"/> 한 곳이다(시험이 같은 몸통을 실제 ListBox 에 붙여 검증한다).
    /// </summary>
    private async void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list) return;
        await Guard(() => _treeSelection.OnSelectionChangedAsync(list, e, Vm));
    }

    /// <summary>끌기의 버튼 경로 — 고른 장비를 배치 바가 가리키는 부대에 쌓는다.</summary>
    private void OnAssignSelected(object sender, RoutedEventArgs e) => Vm?.QueueAssignSelected();

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

    #region - Map (unit-relationship-map IMPL-32) -
    /// <summary>
    /// 둘째 툴바 줄 · 상세 칸의 [지도에서 보기] — 둘 다 관계도의 같은 길(선택 부대 + 예하 포함 토글). 장비 0 이면 단추가 꺼져 있다.
    /// </summary>
    private async void OnLocateOnMap(object sender, RoutedEventArgs e) => await Guard(() => Vm?.Map.LocateOnMapAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    /// <summary>[배치 초기화] — 확인 오버레이를 연다(서버는 [확정]에서만).</summary>
    private void OnResetLayout(object sender, RoutedEventArgs e) => Vm?.Map.RequestResetLayout();

    /// <summary>상세 [이 부대 배치 초기화] — 확인 없이 1회, 되돌리기 막대가 뜬다.</summary>
    private async void OnResetNodeLayout(object sender, RoutedEventArgs e) => await Guard(() => Vm?.Map.ResetSelectedNodeLayoutAsync() ?? System.Threading.Tasks.Task.CompletedTask);

    /// <summary>상태 띠 [다시 읽기] — 편제만 다시 읽는다. 상세의 미적용 편집은 그대로(FR-48 ⑤).</summary>
    private async void OnReadExternalChange(object sender, RoutedEventArgs e) => await Guard(() => Vm?.ReadExternalChangeAsync() ?? System.Threading.Tasks.Task.CompletedTask);
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
