using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 10:08:39 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public sealed class CameraOpticsViewModel : BasePanelViewModel
    {
        private readonly ICameraOpticsModel _model;

        public CameraOpticsViewModel(ICameraOpticsModel model) 
        {
            _model = model;
        }

        public float ZoomLevel
        {
            get => _model.ZoomLevel;
            set { _model.ZoomLevel = value; NotifyOfPropertyChange(() => ZoomLevel); NotifyOfPropertyChange(() => ViewDistance); }
        }

        public float FocalLength
        {
            get => _model.FocalLength;
            set { _model.FocalLength = value; NotifyOfPropertyChange(() => FocalLength); NotifyOfPropertyChange(() => HorizontalFOV); }
        }

        public float SensorWidth
        {
            get => _model.SensorWidth;
            set { _model.SensorWidth = value; NotifyOfPropertyChange(() => SensorWidth); NotifyOfPropertyChange(() => HorizontalFOV); }
        }

        public float SensorHeight
        {
            get => _model.SensorHeight;
            set { _model.SensorHeight = value; NotifyOfPropertyChange(() => SensorHeight); }
        }

        public float HorizontalFOV => _model.HorizontalFOV;
        public float ViewDistance => _model.ViewDistance;
    }
}