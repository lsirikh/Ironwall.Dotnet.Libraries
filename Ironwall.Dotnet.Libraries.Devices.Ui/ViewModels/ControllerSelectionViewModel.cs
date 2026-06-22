using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/10/2025 3:33:28 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ControllerSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public ControllerSelectionViewModel(IList<ControllerDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<ControllerDevicePanelViewModel>();
            _selection = selection;
            RefreshAll();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
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
                item.IpAddress = IpAddress ?? item.IpAddress;
                item.Port = Port ?? item.Port;
                item.Location = Location ?? item.Location;
                if (Latitude.HasValue) item.Latitude = Math.Clamp(Latitude.Value, -90.0, 90.0);
                if (Longitude.HasValue) item.Longitude = Math.Clamp(Longitude.Value, -180.0, 180.0);
                if (Bearing.HasValue) item.Bearing = Bearing.Value;     // mod360 정규화는 VM setter가 처리
                if (Altitude.HasValue) item.Altitude = Altitude.Value;
                if (IsEnable.HasValue) item.IsEnable = IsEnable.Value;
            }
            ApplyGroups();
        }


        /* 공통값 계산 헬퍼 */
        private static T? CommonOrNullValue<T>(IEnumerable<ControllerDeviceViewModel> list, Func<IControllerDeviceModel, T> selector) where T : struct 
        {
            try
            {
                if (list == null || !list.Any()) return null;

                var firstModel = list.FirstOrDefault()?.Model as IControllerDeviceModel;
                if (firstModel == null) return null;

                T firstValue = selector(firstModel);

                bool allSame = list
                    .Select(vm => vm.Model as IControllerDeviceModel)
                    .Where(m => m != null)
                    .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

                return allSame ? firstValue : (T?)null;
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        private static T? CommonOrNullString<T>(IEnumerable<ControllerDeviceViewModel> list, Func<IControllerDeviceModel, T> selector) where T : class?
        {
            try
            {
                if (!list.Any()) return null;

                var models = list.Select(x => x.Model as IControllerDeviceModel).ToList();
                var firstModel = list.FirstOrDefault()?.Model as IControllerDeviceModel;
                if (firstModel == null) return null;
                T firstValue = selector(firstModel);

                return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
            }
            catch (Exception)
            {
                throw;
            }

        }

        /* 공통값 계산 헬퍼 — 소스가 Nullable<T>(Heading/Altitude). 전부 동일하면 그 값, 혼재/일부 null이면 null */
        private static T? CommonOrNullNullable<T>(IEnumerable<ControllerDeviceViewModel> list, Func<IControllerDeviceModel, T?> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var models = list.Select(vm => vm.Model as IControllerDeviceModel).Where(m => m != null).ToList();
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
            IpAddress = CommonOrNullString(_selection, m => m.IpAddress);
            Port = CommonOrNullValue(_selection, m => m.Port);
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
        #region - IHanldes -
        #endregion
        #region - Properties -
        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public EnumDeviceStatus? Status { get; set; }
        public string? IpAddress { get; set; }
        public int? Port { get; set; }
        public string? Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Bearing { get; set; }
        public double? Altitude { get; set; }
        public bool? IsEnable { get; set; }
        public ObservableCollection<DeviceGroupItemViewModel> GroupItems { get; set; } = new();
        public ControllerDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<ControllerDeviceViewModel> _selection;
        #endregion
    }
}