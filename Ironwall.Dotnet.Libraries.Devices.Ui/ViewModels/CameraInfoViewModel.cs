using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 10:08:13 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public sealed class CameraInfoViewModel : BasePanelViewModel
    {
        private readonly ICameraInfoModel _model;

        public CameraInfoViewModel(ICameraInfoModel model)
        {
            _model = model;
        }

        /*── 간단 래퍼 ───────────────────────────────*/
        public string? Name
        {
            get => _model.Name;
            set { _model.Name = value; NotifyOfPropertyChange(() => Name); }
        }

        public string? Location
        {
            get => _model.Location;
            set { _model.Location = value; NotifyOfPropertyChange(() => Location); }
        }

        public string? Manufacturer
        {
            get => _model.Manufacturer;
            set { _model.Manufacturer = value; NotifyOfPropertyChange(() => Manufacturer); }
        }

        public string? Model
        {
            get => _model.Model;
            set { _model.Model = value; NotifyOfPropertyChange(() => Model); }
        }

        public string? Hardware
        {
            get => _model.Hardware;
            set { _model.Hardware = value; NotifyOfPropertyChange(() => Hardware); }
        }

        public string? Firmware
        {
            get => _model.Firmware;
            set { _model.Firmware = value; NotifyOfPropertyChange(() => Firmware); }
        }

        public string? DeviceId
        {
            get => _model.DeviceId;
            set { _model.DeviceId = value; NotifyOfPropertyChange(() => DeviceId); }
        }

        public string? MacAddress
        {
            get => _model.MacAddress;
            set { _model.MacAddress = value; NotifyOfPropertyChange(() => MacAddress); }
        }

        public string? OnvifVersion
        {
            get => _model.OnvifVersion;
            set { _model.OnvifVersion = value; NotifyOfPropertyChange(() => OnvifVersion); }
        }

        public string? Uri
        {
            get => _model.Uri;
            set { _model.Uri = value; NotifyOfPropertyChange(() => Uri); }
        }
    }
}