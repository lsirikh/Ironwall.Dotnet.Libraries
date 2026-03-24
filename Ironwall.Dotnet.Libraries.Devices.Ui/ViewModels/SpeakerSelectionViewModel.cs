using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class SpeakerSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public SpeakerSelectionViewModel(IList<SpeakerDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<SpeakerDevicePanelViewModel>();
            _selection = selection;
            RefreshAll();
        }
        #endregion
        #region - Processes -
        public void ApplyButton()
        {
            foreach (var item in _selection)
            {
                item.DeviceNumber = DeviceNumber ?? item.DeviceNumber;
                item.DeviceName = DeviceName ?? item.DeviceName;
                item.DeviceType = DeviceType ?? item.DeviceType;
                item.Version = Version ?? item.Version;
                item.Status = Status ?? item.Status;
                item.SpeakerType = SpeakerType ?? item.SpeakerType;
                item.Description = Description ?? item.Description;
                item.Location = Location ?? item.Location;
                if (Latitude.HasValue) item.Latitude = Math.Clamp(Latitude.Value, -90.0, 90.0);
                if (Longitude.HasValue) item.Longitude = Math.Clamp(Longitude.Value, -180.0, 180.0);
                if (IsEnable.HasValue) item.IsEnable = IsEnable.Value;
            }
            ApplyGroups();
        }

        private static T? CommonOrNullValue<T>(IEnumerable<SpeakerDeviceViewModel> list, Func<ISpeakerDeviceModel, T> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as ISpeakerDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            bool allSame = list
                .Select(vm => vm.Model as ISpeakerDeviceModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));
            return allSame ? firstValue : (T?)null;
        }

        private static T? CommonOrNullString<T>(IEnumerable<SpeakerDeviceViewModel> list, Func<ISpeakerDeviceModel, T> selector) where T : class?
        {
            if (!list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as ISpeakerDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            return list
                .Select(x => x.Model as ISpeakerDeviceModel)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }

        public void RefreshAll()
        {
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            Status = CommonOrNullValue(_selection, m => m.Status);
            SpeakerType = CommonOrNullString(_selection, m => m.SpeakerType);
            Description = CommonOrNullString(_selection, m => m.Description);
            Location = CommonOrNullString(_selection, m => m.Location);
            Latitude = CommonOrNullValue(_selection, m => m.Latitude);
            Longitude = CommonOrNullValue(_selection, m => m.Longitude);
            IsEnable = CommonOrNullValue(_selection, m => m.IsEnable);
            RefreshGroupItems();
        }

        private void RefreshGroupItems()
        {
            var provider = IoC.Get<DeviceGroupProvider>();
            GroupItems = new ObservableCollection<DeviceGroupItemViewModel>(
                provider.OfType<IDeviceGroupModel>().Select(g =>
                {
                    var state = ComputeGroupCheckState(g.Id);
                    return new DeviceGroupItemViewModel
                    {
                        GroupId = g.Id,
                        GroupName = g.Name,
                        IsChecked = state,
                        OriginalState = state
                    };
                }));
            NotifyOfPropertyChange(nameof(GroupItems));
        }

        private bool? ComputeGroupCheckState(int groupId)
        {
            var count = _selection.Count(item => item.DeviceGroups?.Contains(groupId) == true);
            if (count == 0) return false;
            if (count == _selection.Count) return true;
            return null;
        }

        private void ApplyGroups()
        {
            var checkedIds = GroupItems.Where(g => g.IsChecked == true).Select(g => g.GroupId).ToList();
            foreach (var item in _selection)
                item.DeviceGroups = new List<int>(checkedIds);
        }
        #endregion
        #region - Properties -
        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public EnumDeviceStatus? Status { get; set; }
        public string? SpeakerType { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool? IsEnable { get; set; }
        public ObservableCollection<DeviceGroupItemViewModel> GroupItems { get; set; } = new();
        public SpeakerDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<SpeakerDeviceViewModel> _selection;
        #endregion
    }
}
