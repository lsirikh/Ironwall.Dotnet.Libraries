using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/13/2025 11:32:27 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraDeviceViewModel : DeviceViewModel, ICameraDeviceViewModel
    {
        #region - Ctors -
        public CameraDeviceViewModel(ICameraDeviceModel model) : base(model)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public string IpAddress
        {
            get => (_model as ICameraDeviceModel)!.IpAddress;
            set
            {
                (_model as ICameraDeviceModel)!.IpAddress = value;
                NotifyOfPropertyChange(() => IpAddress);
            }
        }

        public int IpPort
        {
            get => (_model as ICameraDeviceModel)!.IpPort;
            set
            {
                (_model as ICameraDeviceModel)!.IpPort = value;
                NotifyOfPropertyChange(() => IpPort);
            }
        }

        public string? UserName
        {
            get => (_model as ICameraDeviceModel)!.UserName;
            set
            {
                (_model as ICameraDeviceModel)!.UserName = value;
                NotifyOfPropertyChange(() => UserName);
            }
        }

        public string? UserPassword
        {
            get => (_model as ICameraDeviceModel)!.UserPassword;
            set
            {
                (_model as ICameraDeviceModel)!.UserPassword = value;
                NotifyOfPropertyChange(() => UserPassword);
            }
        }

        public EnumCameraMode Mode
        {
            get => (_model as ICameraDeviceModel)!.Mode;
            set
            {
                (_model as ICameraDeviceModel)!.Mode = value;
                NotifyOfPropertyChange(() => Mode);
            }
        }

        public EnumCameraType Category
        {
            get => (_model as ICameraDeviceModel)!.Category;
            set
            {
                (_model as ICameraDeviceModel)!.Category = value;
                NotifyOfPropertyChange(() => Category);
            }
        }

        public ICameraInfoModel? HardwareSpec
        {
            get => (_model as ICameraDeviceModel)!.HardwareSpec;
            set
            {
                (_model as ICameraDeviceModel)!.HardwareSpec = value;
                NotifyOfPropertyChange(() => HardwareSpec);
            }
        }

        public ICameraUrlsModel? Urls
        {
            get => (_model as ICameraDeviceModel)!.Urls;
            set
            {
                (_model as ICameraDeviceModel)!.Urls = value;
                NotifyOfPropertyChange(() => Urls);
            }
        }

        public ICameraSettingModel? Setting
        {
            get => (_model as ICameraDeviceModel)!.Setting;
            set
            {
                (_model as ICameraDeviceModel)!.Setting = value;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        public bool? IsRecord
        {
            get => (_model as ICameraDeviceModel)!.IsRecord;
            set
            {
                (_model as ICameraDeviceModel)!.IsRecord = value;
                NotifyOfPropertyChange(() => IsRecord);
            }
        }
        #endregion
        #region - Attributes -
        #endregion

    }
}