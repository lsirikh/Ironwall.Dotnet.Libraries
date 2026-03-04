using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class DeviceGroupSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public DeviceGroupSelectionViewModel(IList<DeviceGroupViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<DeviceGroupPanelViewModel>();
            _selection = selection;
            RefreshAll();
        }
        #endregion
        #region - Processes -
        public void ApplyButton()
        {
            foreach (var item in _selection)
            {
                item.Name = Name ?? item.Name;
                item.Description = Description ?? item.Description;
            }
        }

        private static T? CommonOrNullString<T>(IEnumerable<DeviceGroupViewModel> list, Func<IDeviceGroupModel, T> selector) where T : class?
        {
            if (!list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            return list
                .Select(x => x.Model)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }

        public void RefreshAll()
        {
            Name = CommonOrNullString(_selection, m => m.Name);
            Description = CommonOrNullString(_selection, m => m.Description);
        }
        #endregion
        #region - Properties -
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DeviceGroupPanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<DeviceGroupViewModel> _selection;
        #endregion
    }
}
