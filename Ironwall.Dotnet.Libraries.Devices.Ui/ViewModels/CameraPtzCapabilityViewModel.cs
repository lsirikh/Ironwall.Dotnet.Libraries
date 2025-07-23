using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 10:09:42 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public sealed class CameraPtzCapabilityViewModel : BasePanelViewModel
    {
        private readonly ICameraPtzCapabilityModel _model;

        public CameraPtzCapabilityViewModel(ICameraPtzCapabilityModel model)
        {
            _model = model;
        }

        public float MinPan
        {
            get => _model.MinPan;
            set { _model.MinPan = value; NotifyOfPropertyChange(() => MinPan); }
        }

        public float MaxPan
        {
            get => _model.MaxPan;
            set { _model.MaxPan = value; NotifyOfPropertyChange(() => MaxPan); }
        }

        public float MinTilt
        {
            get => _model.MinTilt;
            set { _model.MinTilt = value; NotifyOfPropertyChange(() => MinTilt); }
        }

        public float MaxTilt
        {
            get => _model.MaxTilt;
            set { _model.MaxTilt = value; NotifyOfPropertyChange(() => MaxTilt); }
        }

        public float MinZoom
        {
            get => _model.MinZoom;
            set { _model.MinZoom = value; NotifyOfPropertyChange(() => MinZoom); }
        }

        public float MaxZoom
        {
            get => _model.MaxZoom;
            set { _model.MaxZoom = value; NotifyOfPropertyChange(() => MaxZoom); }
        }

        public float HorizontalFov
        {
            get => _model.HorizontalFov;
            set { _model.HorizontalFov = value; NotifyOfPropertyChange(() => HorizontalFov); }
        }

        public float VerticalFov
        {
            get => _model.VerticalFov;
            set { _model.VerticalFov = value; NotifyOfPropertyChange(() => VerticalFov); }
        }

        public float MaxVisibleDistance
        {
            get => _model.MaxVisibleDistance;
            set { _model.MaxVisibleDistance = value; NotifyOfPropertyChange(() => MaxVisibleDistance); }
        }

        public int ZoomLevel
        {
            get => _model.ZoomLevel;
            set { _model.ZoomLevel = value; NotifyOfPropertyChange(() => ZoomLevel); }
        }
    }
}