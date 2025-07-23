using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

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
                item.DeviceGroup = DeviceGroup ?? item.DeviceGroup;
                item.DeviceNumber = DeviceNumber ?? item.DeviceNumber;
                item.DeviceName = DeviceName ?? item.DeviceName;
                item.DeviceType = DeviceType ?? item.DeviceType;
                item.Version = Version ?? item.Version;
                item.Status = Status ?? item.Status;
                item.IpAddress = IpAddress ?? item.IpAddress;
                item.Port = Port ?? item.Port;
            }
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

        public void RefreshAll()
        {
            DeviceGroup = CommonOrNullValue(_selection, m => m.DeviceGroup);
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            Status = CommonOrNullValue(_selection, m => m.Status);
            IpAddress = CommonOrNullString(_selection, m => m.IpAddress);
            Port = CommonOrNullValue(_selection, m => m.Port);
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public int? DeviceGroup { get; set; }
        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public EnumDeviceStatus? Status { get; set; }
        public string? IpAddress { get; set; }
        public int? Port { get; set; }
        public ControllerDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        #region - Attributes -
        private readonly IList<ControllerDeviceViewModel> _selection;
        #endregion
    }
}