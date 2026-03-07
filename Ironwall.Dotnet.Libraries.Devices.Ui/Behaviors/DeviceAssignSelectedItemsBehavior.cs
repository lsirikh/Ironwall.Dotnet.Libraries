using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Utils.Behaviors;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Behaviors;

public class DeviceAssignSelectedItemsBehavior : DataGridSelectedItemsBehavior<DeviceAssignItemViewModel>
{
    protected override void OnSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        var target = SelectedItems;
        if (target == null) return;

        foreach (var removed in e.RemovedItems.OfType<DeviceAssignItemViewModel>())
            target.Remove(removed);

        foreach (var added in e.AddedItems.OfType<DeviceAssignItemViewModel>())
        {
            if (!target.Contains(added))
                target.Add(added);
        }
    }
}
