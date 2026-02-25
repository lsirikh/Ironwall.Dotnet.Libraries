using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

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
            }
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
        public SpeakerDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<SpeakerDeviceViewModel> _selection;
        #endregion
    }
}
