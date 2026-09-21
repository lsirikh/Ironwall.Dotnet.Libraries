using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dialogs;

/// <summary>
/// 장비 배정 창의 배선 — 고른 것을 뷰모델에 알리고, 뷰모델이 옮긴 것을 저쪽 목록에서 다시 고른다.
/// </summary>
/// <remarks>
/// <para>▶ ◀ 와 Enter · Delete 는 드롭과 <b>같은 메서드</b>를 부른다. 폴백이 다른 일을 하면 자동화가 보는 것과 사람이 보는 것이 갈린다.</para>
/// <para>키는 <c>PreviewKeyDown</c>(터널)에서 본다 — 목록이 버블 <c>KeyDown</c> 을 먼저 삼킨다.</para>
/// <para>ESC 와 저장은 틀(<c>ConsoleDialogFrame</c>)이 알려 준다 — 닫는 길이 하나뿐이어야 두 번 닫히지 않는다.</para>
/// </remarks>
public partial class DeviceAssignDialogView : UserControl
{
    private bool _applyingSelection;

    public DeviceAssignDialogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private DeviceAssignDialogViewModel? ViewModel => DataContext as DeviceAssignDialogViewModel;

    #region - Wiring -
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DeviceAssignDialogViewModel old) old.SelectionRequested -= OnSelectionRequested;
        if (e.NewValue is DeviceAssignDialogViewModel fresh) fresh.SelectionRequested += OnSelectionRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.SelectionRequested -= OnSelectionRequested;
    }

    /// <summary>옮긴 행을 저쪽에서 그대로 골라 둔다 — 여러 건을 옮기고 곧바로 되돌릴 수 있게.</summary>
    private void OnSelectionRequested(object? sender, AssignSelectionRequest e)
    {
        var list = FindList(e.Side);
        if (list is null) return;

        _applyingSelection = true;
        try
        {
            list.SelectedItems.Clear();
            foreach (var item in e.Items) list.SelectedItems.Add(item);
            if (e.Items.Count > 0) list.ScrollIntoView(e.Items[^1]);
        }
        finally { _applyingSelection = false; }

        ViewModel?.SetSelection(e.Side, e.Items);
    }

    private ListBox? FindList(AssignSide side)
        => FindByAutomationId(this, side == AssignSide.Available ? "Devices.Assign.Available" : "Devices.Assign.Assigned");

    private static ListBox? FindByAutomationId(DependencyObject root, string id)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is ListBox list && System.Windows.Automation.AutomationProperties.GetAutomationId(list) == id) return list;
            if (FindByAutomationId(child, id) is { } found) return found;
        }
        return null;
    }
    #endregion

    #region - Selection -
    private void OnAvailableSelectionChanged(object sender, SelectionChangedEventArgs e)
        => PushSelection(AssignSide.Available, sender);

    private void OnAssignedSelectionChanged(object sender, SelectionChangedEventArgs e)
        => PushSelection(AssignSide.Assigned, sender);

    private void PushSelection(AssignSide side, object sender)
    {
        if (_applyingSelection || sender is not ListBox list) return;
        var items = list.SelectedItems.OfType<DeviceAssignItemViewModel>().ToList();
        ViewModel?.SetSelection(side, items);
    }
    #endregion

    #region - Keys and buttons (the fallback path) -
    private void OnAvailableKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        ViewModel?.AssignSelected();
    }

    private void OnAssignedKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        e.Handled = true;
        ViewModel?.UnassignSelected();
    }

    private void OnMoveRight(object sender, RoutedEventArgs e) => ViewModel?.AssignSelected();
    private void OnMoveAllRight(object sender, RoutedEventArgs e) => ViewModel?.AssignAll();
    private void OnMoveLeft(object sender, RoutedEventArgs e) => ViewModel?.UnassignSelected();
    private void OnMoveAllLeft(object sender, RoutedEventArgs e) => ViewModel?.UnassignAll();
    private void OnRevert(object sender, RoutedEventArgs e) => ViewModel?.RevertAll();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        // async void 다 — 여기서 새는 예외는 앱을 죽인다. 뷰모델이 안에서 다 잡지만 배선도 막아 둔다.
        try { if (ViewModel is { } vm) await vm.SaveAsync(); }
        catch { /* 뷰모델이 이미 기록하고 버튼 줄에 까닭을 적었다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); }
        catch { /* 창을 닫는 길은 하나뿐이다 — 실패해도 더 할 일이 없다 */ }
    }
    #endregion
}
