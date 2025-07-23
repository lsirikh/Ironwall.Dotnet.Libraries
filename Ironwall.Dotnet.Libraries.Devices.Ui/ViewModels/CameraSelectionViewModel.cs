using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

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
        #endregion
        #region - Processes -
        private void CheckCondition()
        {
            try
            {
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
                _log.Error($"{ex.Message}");
                IsOnvifButtonEnable = false;
            }
        }
        public void OnvifButton()
        {
            try
            {
                if (!IsOnvifButtonEnable) return;

                var onvifDialog = IoC.Get<OnvifDialogViewModel>();
                if (_selection.FirstOrDefault() == null) throw new NullReferenceException("OnvifProperty를 확인하기 위한 인스턴스 설정에 문제가 있습니다.");
                onvifDialog.UpdateModel(model: (ICameraDeviceModel)_selection.FirstOrDefault()!.Model);
                _eventAggregator?.PublishOnUIThreadAsync(new OpenOnvifPropertyDialogMessageModel());
            }
            catch (Exception ex)  
            {
                _log?.Error(ex.Message);
            }
        }

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
                item.Username = Username ?? item.Username;
                item.Password = Password ?? item.Password;
                item.RtspUri = RtspUri ?? item.RtspUri;
                item.RtspPort = RtspPort ?? item.RtspPort;
                item.Mode = Mode ?? item.Mode;
                item.Category = Category ?? item.Category;
            }
        }

        public void RefreshAll()
        {
            DeviceGroup = CommonOrNullValue(_selection, m => m.DeviceGroup);
            DeviceNumber = CommonOrNullValue(_selection, m => m.DeviceNumber);
            DeviceName = CommonOrNullString(_selection, m => m.DeviceName);
            DeviceType = CommonOrNullValue(_selection, m => m.DeviceType);
            Version = CommonOrNullString(_selection, m => m.Version);
            IpAddress = CommonOrNullString(_selection, m => m.IpAddress);
            Port = CommonOrNullValue(_selection, m => m.Port);
            Username = CommonOrNullString(_selection, m => m.Username);
            Password = CommonOrNullString(_selection, m => m.Password);
            RtspUri = CommonOrNullString(_selection, m => m.RtspUri);
            RtspPort = CommonOrNullValue(_selection, m => m.RtspPort);
            Mode = CommonOrNullValue(_selection, m => m.Mode);
            Category = CommonOrNullValue(_selection, m => m.Category);
            Status = CommonOrNullValue(_selection, m => m.Status);
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
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public bool IsOnvifButtonEnable
        {
            get { return _isOnvifButtonEnable; }
            set { _isOnvifButtonEnable = value; NotifyOfPropertyChange(() => IsOnvifButtonEnable); }
        }

        public int? DeviceGroup { get; set; }
        public int? DeviceNumber { get; set; }
        public string? DeviceName { get; set; }
        public EnumDeviceType? DeviceType { get; set; }
        public string? Version { get; set; }
        public string? IpAddress { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? RtspUri { get; set; }
        public int? RtspPort { get; set; }
        public EnumCameraMode? Mode { get; set; }
        public EnumCameraType? Category { get; set; }

        public EnumDeviceStatus? Status
        {
            get { return _status; }
            set { 
                _status = value; 
                NotifyOfPropertyChange(() => Status);
                CheckCondition();
            }
        }

        public CameraDevicePanelViewModel DevicePanelViewModel { get; }
        #endregion
        private IList<CameraDeviceViewModel> _selection;
        #region - Attributes -
        private bool _isOnvifButtonEnable;
        private EnumDeviceStatus? _status;
        #endregion
    }
}