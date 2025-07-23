using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/11/2025 11:32:46 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class SensorSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public SensorSelectionViewModel(IList<SensorDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<SensorDevicePanelViewModel>();
            _controllerProvider = IoC.Get<ControllerDeviceProvider>();
            _selection = selection;

            //foreach (var item in _controllerProvider)
            //{
            //    _log?.Info($"Controller Item Hash : {item.GetHashCode()}");
            //}
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
                item.Controller = Controller ?? item.Controller;
            }
        }


        /* 공통값 계산 헬퍼 */
        //int 형 및 Enum 타입의 형식 비교
        private static T? CommonOrNullValue<T>(IEnumerable<SensorDeviceViewModel> list, Func<ISensorDeviceModel, T> selector) where T : struct
        {
            try
            {
                if (list == null || !list.Any()) return null;

                var firstModel = list.FirstOrDefault()?.Model as ISensorDeviceModel;
                if (firstModel == null) return null;

                T firstValue = selector(firstModel);

                bool allSame = list
                    .Select(vm => vm.Model as ISensorDeviceModel)
                    .Where(m => m != null)
                    .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

                return allSame ? firstValue : (T?)null;
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        //String과 같은 타입 비교
        private static T? CommonOrNullString<T>(IEnumerable<SensorDeviceViewModel> list, Func<ISensorDeviceModel, T> selector) where T : class?
        {
            try
            {
                if (!list.Any()) return null;

                var models = list.Select(x => x.Model as ISensorDeviceModel).ToList();
                var firstModel = list.FirstOrDefault()?.Model as ISensorDeviceModel;
                if (firstModel == null) return null;
                T firstValue = selector(firstModel);

                return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
            }
            catch (Exception)
            {
                throw;
            }

        }


        //Controller와 같은 타입 비교
        private static IControllerDeviceModel? CommonOrNullReference(IEnumerable<SensorDeviceViewModel> list, ControllerDeviceProvider controllers, ILogService log)
        {
            if (!list.Any()) return null;

            var first = list.First()?.Controller;
            if (first == null) return null;

            var ret = list
                .Where(m => m?.Controller != null)
                .All(m => ReferenceEquals(m!.Controller, first))
            ? first
            : null;

            if (ret == null)
                return null;
            else
                return controllers.Where(entity => entity.Id == ret.Id)
                    .Where(entity => entity.DeviceName == ret.DeviceName).FirstOrDefault(); 
        }

      
        public void RefreshAll()
        {
            DeviceGroup = CommonOrNullValue(_selection, m => m.DeviceGroup);
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Controller = CommonOrNullReference(_selection, _controllerProvider, _log);
            Version = CommonOrNullString(_selection, m => m.Version);
            Status = CommonOrNullValue(_selection, m => m.Status);
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
        public IControllerDeviceModel? Controller { get; set; }
        public SensorDevicePanelViewModel DevicePanelViewModel { get; }
        public IEnumerable<IControllerDeviceModel> Controllers => _controllerProvider;
        #endregion
        #region - Attributes -
        private readonly ControllerDeviceProvider _controllerProvider;
        private readonly IList<SensorDeviceViewModel> _selection;
        #endregion
    }
}