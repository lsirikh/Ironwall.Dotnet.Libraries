using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class EnclosureSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public EnclosureSelectionViewModel(IList<EnclosureDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<EnclosureDevicePanelViewModel>();
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
                item.DoorStatus = DoorStatus ?? item.DoorStatus;
                if (HeaterEnabled.HasValue) item.HeaterEnabled = HeaterEnabled.Value;
                if (FanEnabled.HasValue) item.FanEnabled = FanEnabled.Value;
                item.Location = Location ?? item.Location;
                if (Latitude.HasValue) item.Latitude = Math.Clamp(Latitude.Value, -90.0, 90.0);
                if (Longitude.HasValue) item.Longitude = Math.Clamp(Longitude.Value, -180.0, 180.0);
                if (Bearing.HasValue) item.Bearing = Bearing.Value;     // mod360 정규화는 VM setter가 처리
                if (Altitude.HasValue) item.Altitude = Altitude.Value;
                if (IsEnable.HasValue) item.IsEnable = IsEnable.Value;
            }
            ApplyGroups();
        }

        /// <summary>단일 선택 시에만 임계값 설정 가능(카메라 상세 패턴).</summary>
        public bool IsSingleSelected => _selection != null && _selection.Count == 1;

        /// <summary>임계값 설정 다이얼로그 열기 — model 직접 편집, 영속은 그리드 적용(Save).</summary>
        public void ThresholdButton()
        {
            try
            {
                if (!IsSingleSelected) return;
                var model = _selection.FirstOrDefault()?.Model as IEnclosureDeviceModel;
                if (model == null) return;
                var dialog = new EnclosureThresholdDialogViewModel(model);
                _eventAggregator?.PublishOnUIThreadAsync(new OpenEnclosureThresholdDialogMessageModel { Dialog = dialog });
            }
            catch (Exception ex) { _log?.Error(ex.Message); }
        }

        private static T? CommonOrNullValue<T>(IEnumerable<EnclosureDeviceViewModel> list, Func<IEnclosureDeviceModel, T> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as IEnclosureDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            bool allSame = list
                .Select(vm => vm.Model as IEnclosureDeviceModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));
            return allSame ? firstValue : (T?)null;
        }

        private static T? CommonOrNullString<T>(IEnumerable<EnclosureDeviceViewModel> list, Func<IEnclosureDeviceModel, T> selector) where T : class?
        {
            if (!list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as IEnclosureDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            return list
                .Select(x => x.Model as IEnclosureDeviceModel)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }

        /* 공통값 계산 헬퍼 — 소스가 Nullable<T>(Heading/Altitude). 전부 동일하면 그 값, 혼재/일부 null이면 null */
        private static T? CommonOrNullNullable<T>(IEnumerable<EnclosureDeviceViewModel> list, Func<IEnclosureDeviceModel, T?> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var models = list.Select(vm => vm.Model as IEnclosureDeviceModel).Where(m => m != null).ToList();
            if (models.Count == 0) return null;
            T? first = selector(models[0]!);
            return models.All(m => Nullable.Equals(selector(m!), first)) ? first : (T?)null;
        }

        public void RefreshAll()
        {
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            Status = CommonOrNullValue(_selection, m => m.Status);
            DoorStatus = CommonOrNullString(_selection, m => m.DoorStatus);
            HeaterEnabled = CommonOrNullValue(_selection, m => m.HeaterEnabled);
            FanEnabled = CommonOrNullValue(_selection, m => m.FanEnabled);
            Location = CommonOrNullString(_selection, m => m.Location);
            Latitude = CommonOrNullValue(_selection, m => m.Latitude);
            Longitude = CommonOrNullValue(_selection, m => m.Longitude);
            Bearing = CommonOrNullNullable(_selection, m => m.Heading);
            Altitude = CommonOrNullNullable(_selection, m => m.Altitude);
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
        public string? DoorStatus { get; set; }
        public bool? HeaterEnabled { get; set; }
        public bool? FanEnabled { get; set; }
        public string? Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Bearing { get; set; }
        public double? Altitude { get; set; }
        public bool? IsEnable { get; set; }
        public ObservableCollection<DeviceGroupItemViewModel> GroupItems { get; set; } = new();
        public EnclosureDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<EnclosureDeviceViewModel> _selection;
        #endregion
    }
}
