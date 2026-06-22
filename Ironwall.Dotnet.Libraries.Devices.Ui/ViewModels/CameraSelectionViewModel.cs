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

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/13/2025 3:37:15 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraSelectionViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public CameraSelectionViewModel(IList<CameraDeviceViewModel> selection)
        {
            DevicePanelViewModel = IoC.Get<CameraDevicePanelViewModel>();
            _selection = selection;
            RefreshAll();
        }
        #endregion
        #region - Implementation of Interface -
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            CheckCondition();
            return base.OnActivateAsync(cancellationToken);
        }

        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        public void DetailButton()
        {
            try
            {
                if (!IsSingleSelected) return;

                var selectedCam = _selection.FirstOrDefault();
                if (selectedCam == null) return;

                var model = selectedCam.Model as ICameraDeviceModel;
                if (model == null) return;

                var dialog = new CameraDetailDialogViewModel(model);
                _eventAggregator?.PublishOnUIThreadAsync(new OpenCameraDetailDialogMessageModel { Dialog = dialog });
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }
        #endregion
        #region - Processes -
        private void CheckCondition()
        {
            try
            {
                NotifyOfPropertyChange(nameof(IsSingleSelected));
                if (_selection.Count == 1)
                {
                    var selectedCam = _selection.FirstOrDefault();
                    if (selectedCam == null) throw new NullReferenceException("Selected Camera의 인스턴스가 존재하지 않습니다.");

                    if (!(selectedCam.Status == EnumDeviceStatus.ACTIVATED)) 
                        throw new InvalidOperationException("Selected Camera의 상태가 ACTIVATED가 아닙니다.");

                    switch (selectedCam.Mode)
                    {
                        case EnumCameraMode.NONE:
                            IsOnvifButtonEnable = false;
                            break;
                        case EnumCameraMode.ONVIF:
                            IsOnvifButtonEnable = true;
                            break;
                        case EnumCameraMode.INNODEP_API:
                            IsOnvifButtonEnable = false;
                            break;
                        case EnumCameraMode.ETC:
                            IsOnvifButtonEnable = false;
                            break;
                        default:
                            IsOnvifButtonEnable = false;
                            break;
                    }
                }
                else
                {
                    IsOnvifButtonEnable = false;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"{ex.Message}");
                IsOnvifButtonEnable = false;
            }
        }

        //public void OnvifButton()
        //{
        //    try
        //    {
        //        if (!IsOnvifButtonEnable) return;

        //        var onvifDialog = IoC.Get<OnvifDialogViewModel>();
        //        if (_selection.FirstOrDefault() == null) throw new NullReferenceException("OnvifProperty를 확인하기 위한 인스턴스 설정에 문제가 있습니다.");
        //        onvifDialog.UpdateModel(model: (ICameraDeviceModel)_selection.FirstOrDefault()!.Model);
        //        _eventAggregator?.PublishOnUIThreadAsync(new OpenOnvifPropertyDialogMessageModel());
        //    }
        //    catch (Exception ex)  
        //    {
        //        _log?.Error(ex.Message);
        //    }
        //}

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
                item.IpPort = IpPort ?? item.IpPort;
                item.UserName = UserName ?? item.UserName;
                item.UserPassword = UserPassword ?? item.UserPassword;
                item.Mode = Mode ?? item.Mode;
                item.Category = Category ?? item.Category;
                item.Location = Location ?? item.Location;
                if (Latitude.HasValue) item.Latitude = Math.Clamp(Latitude.Value, -90.0, 90.0);
                if (Longitude.HasValue) item.Longitude = Math.Clamp(Longitude.Value, -180.0, 180.0);
                if (Bearing.HasValue) item.Bearing = Bearing.Value;     // mod360 정규화는 VM setter가 처리
                if (Altitude.HasValue) item.Altitude = Altitude.Value;
                if (IsEnable.HasValue) item.IsEnable = IsEnable.Value;
                if (IsRecord.HasValue) item.IsRecord = IsRecord.Value;
            }
            ApplyGroups();
        }

        public void RefreshAll()
        {
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            IpAddress = CommonOrNullString(_selection, m => m.IpAddress);
            IpPort = CommonOrNullValue(_selection, m => m.IpPort);
            UserName = CommonOrNullString(_selection, m => m.UserName);
            UserPassword = CommonOrNullString(_selection, m => m.UserPassword);
            Mode = CommonOrNullValue(_selection, m => m.Mode);
            Category = CommonOrNullValue(_selection, m => m.Category);
            Status = CommonOrNullValue(_selection, m => m.Status);
            Location = CommonOrNullString(_selection, m => m.Location);
            Latitude = CommonOrNullValue(_selection, m => m.Latitude);
            Longitude = CommonOrNullValue(_selection, m => m.Longitude);
            Bearing = CommonOrNullNullable(_selection, m => m.Heading);
            Altitude = CommonOrNullNullable(_selection, m => m.Altitude);
            IsEnable = CommonOrNullValue(_selection, m => m.IsEnable);
            IsRecord = CommonOrNullValue(_selection, m => m.IsRecord ?? false);
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


        /* 공통값 계산 헬퍼 */
        //int 형 및 Enum 타입의 형식 비교
        private static T? CommonOrNullValue<T>(IEnumerable<CameraDeviceViewModel> list, Func<ICameraDeviceModel, T> selector) where T : struct
        {
            try
            {
                if (list == null || !list.Any()) return null;

                var firstModel = list.FirstOrDefault()?.Model as ICameraDeviceModel;
                if (firstModel == null) return null;

                T firstValue = selector(firstModel);

                bool allSame = list
                    .Select(vm => vm.Model as ICameraDeviceModel)
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
        private static T? CommonOrNullString<T>(IEnumerable<CameraDeviceViewModel> list, Func<ICameraDeviceModel, T> selector) where T : class?
        {
            try
            {
                if (!list.Any()) return null;

                var models = list.Select(x => x.Model as ICameraDeviceModel).ToList();
                var firstModel = list.FirstOrDefault()?.Model as ICameraDeviceModel;
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
        private static T? CommonOrNullNullable<T>(IEnumerable<CameraDeviceViewModel> list, Func<ICameraDeviceModel, T?> selector) where T : struct
        {
            if (list == null || !list.Any()) return null;
            var models = list.Select(vm => vm.Model as ICameraDeviceModel).Where(m => m != null).ToList();
            if (models.Count == 0) return null;
            T? first = selector(models[0]!);
            return models.All(m => Nullable.Equals(selector(m!), first)) ? first : (T?)null;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public bool IsOnvifButtonEnable
        {
            get { return _isOnvifButtonEnable; }
            set { _isOnvifButtonEnable = value; NotifyOfPropertyChange(() => IsOnvifButtonEnable); }
        }

        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public string? IpAddress { get; set; }
        public int? IpPort { get; set; }
        public string? UserName { get; set; }
        public string? UserPassword { get; set; }
        public EnumCameraMode? Mode { get; set; }
        public EnumCameraType? Category { get; set; }
        public string? Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Bearing { get; set; }
        public double? Altitude { get; set; }
        public bool? IsEnable { get; set; }
        public bool? IsRecord { get; set; }

        public EnumDeviceStatus? Status
        {
            get { return _status; }
            set { 
                _status = value; 
                NotifyOfPropertyChange(() => Status);
                CheckCondition();
            }
        }

        public bool IsSingleSelected => _selection.Count == 1;

        public ObservableCollection<DeviceGroupItemViewModel> GroupItems { get; set; } = new();

        public CameraDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        private IList<CameraDeviceViewModel> _selection;
        #region - Attributes -
        private bool _isOnvifButtonEnable;
        private EnumDeviceStatus? _status;
        #endregion
    }
}