using Caliburn.Micro;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Streaming.ViewModel{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 10/1/2025 1:20:01 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// Row 단위 카메라 그룹 ViewModel (새로 추가)
    /// </summary>
    public class CameraRowViewModel : PropertyChangedBase
    {
        public string RowId => Guid.NewGuid().ToString();

        private ObservableCollection<CameraViewModel> _cameras = new ObservableCollection<CameraViewModel>();
        public ObservableCollection<CameraViewModel> Cameras
        {
            get => _cameras;
            set => Set(ref _cameras, value);
        }
    }
}