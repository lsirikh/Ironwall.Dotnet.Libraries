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

        public int Port
        {
            get => (_model as ICameraDeviceModel)!.Port;
            set
            {
                (_model as ICameraDeviceModel)!.Port = value;
                NotifyOfPropertyChange(() => Port);
            }
        }

        public string? Username
        {
            get => (_model as ICameraDeviceModel)!.Username;
            set
            {
                (_model as ICameraDeviceModel)!.Username = value;
                NotifyOfPropertyChange(() => Username);
            }
        }

        public string? Password
        {
            get => (_model as ICameraDeviceModel)!.Password;
            set
            {
                (_model as ICameraDeviceModel)!.Password = value;
                NotifyOfPropertyChange(() => Password);
            }
        }

        public string? RtspUri
        {
            get => (_model as ICameraDeviceModel)!.RtspUri;
            set
            {
                (_model as ICameraDeviceModel)!.RtspUri = value;
                NotifyOfPropertyChange(() => RtspUri);
            }
        }

        public int RtspPort
        {
            get => (_model as ICameraDeviceModel)!.RtspPort;
            set
            {
                (_model as ICameraDeviceModel)!.RtspPort = value;
                NotifyOfPropertyChange(() => RtspPort);
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

        public ICameraInfoModel? Identification
        {
            get => (_model as ICameraDeviceModel)!.Identification;
            set
            {
                (_model as ICameraDeviceModel)!.Identification = value;
                NotifyOfPropertyChange(() => Identification);
            }
        }

        public ICameraPtzCapabilityModel? PtzCapability
        {
            get => (_model as ICameraDeviceModel)!.PtzCapability;
            set
            {
                (_model as ICameraDeviceModel)!.PtzCapability = value;
                NotifyOfPropertyChange(() => PtzCapability);
            }
        }

        public ICameraPositionModel? Position
        {
            get => (_model as ICameraDeviceModel)!.Position;
            set
            {
                (_model as ICameraDeviceModel)!.Position = value;
                NotifyOfPropertyChange(() => Position);
            }
        }

        public List<ICameraPresetModel>? Presets
        {
            get => (_model as ICameraDeviceModel)!.Presets;
            set
            {
                (_model as ICameraDeviceModel)!.Presets = value;
                NotifyOfPropertyChange(() => Presets);
            }
        }

        public ICameraOpticsModel? Optics
        {
            get => (_model as ICameraDeviceModel)!.Optics;
            set
            {
                (_model as ICameraDeviceModel)!.Optics = value;
                NotifyOfPropertyChange(() => Optics);
            }
        }
        #endregion
        #region - Attributes -
        #endregion

    }
}