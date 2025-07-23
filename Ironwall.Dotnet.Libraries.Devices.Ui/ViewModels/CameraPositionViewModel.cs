using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 10:09:15 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public sealed class CameraPositionViewModel : BasePanelViewModel
    {
        private readonly ICameraPositionModel _model;

        public CameraPositionViewModel(ICameraPositionModel model)
        {
            _model = model;
        }

        public double Latitude
        {
            get => _model.Latitude;
            set { _model.Latitude = value; NotifyOfPropertyChange(() => Latitude); }
        }

        public double Longitude
        {
            get => _model.Longitude;
            set { _model.Longitude = value; NotifyOfPropertyChange(() => Longitude); }
        }

        public double Altitude
        {
            get => _model.Altitude;
            set { _model.Altitude = value; NotifyOfPropertyChange(() => Altitude); }
        }

        public float Heading
        {
            get => _model.Heading;
            set { _model.Heading = value; NotifyOfPropertyChange(() => Heading); }
        }
    }
}