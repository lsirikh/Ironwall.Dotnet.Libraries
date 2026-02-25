using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels
{
    public class LampSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public LampSelectionViewModel(IList<LampDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<LampDevicePanelViewModel>();
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
                item.IpAddress = IpAddress ?? item.IpAddress;
                if (IpPort.HasValue) item.IpPort = IpPort.Value;
                item.UserName = UserName ?? item.UserName;
                item.UserPassword = UserPassword ?? item.UserPassword;
                item.Description = Description ?? item.Description;
            }
        }

        private static T? CommonOrNullValue<T>(IEnumerable<LampDeviceViewModel> list, Func<ILampDeviceModel, T> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as ILampDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            bool allSame = list
                .Select(vm => vm.Model as ILampDeviceModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));
            return allSame ? firstValue : (T?)null;
        }

        private static T? CommonOrNullString<T>(IEnumerable<LampDeviceViewModel> list, Func<ILampDeviceModel, T> selector) where T : class?
        {
            if (!list.Any()) return null;
            var firstModel = list.FirstOrDefault()?.Model as ILampDeviceModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);
            return list
                .Select(x => x.Model as ILampDeviceModel)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }

        public void RefreshAll()
        {
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            Status = CommonOrNullValue(_selection, m => m.Status);
            IpAddress = CommonOrNullString(_selection, m => m.IpAddress);
            IpPort = CommonOrNullValue(_selection, m => m.IpPort);
            UserName = CommonOrNullString(_selection, m => m.UserName);
            UserPassword = CommonOrNullString(_selection, m => m.UserPassword);
            Description = CommonOrNullString(_selection, m => m.Description);
        }
        #endregion
        #region - Properties -
        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public EnumDeviceStatus? Status { get; set; }
        public string? IpAddress { get; set; }
        public int? IpPort { get; set; }
        public string? UserName { get; set; }
        public string? UserPassword { get; set; }
        public string? Description { get; set; }
        public LampDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<LampDeviceViewModel> _selection;
        #endregion
    }
}
