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
    /// Row 단위 카메라 그룹 ViewModel
    /// RowId를 생성자에서 한 번만 생성하여 고유성 보장
    /// </summary>
    public class CameraRowViewModel : PropertyChangedBase
    {
        // RowId를 필드로 저장 - 생성자에서 한 번만 생성
        private readonly string _rowId = string.Empty;
        private readonly string _eventId = string.Empty;
        private ObservableCollection<CameraViewModel> _cameras = new ObservableCollection<CameraViewModel>();

        public CameraRowViewModel()
        {
            // 생성 시 한 번만 GUID 생성하여 저장
            _rowId = Guid.NewGuid().ToString();
        }

        public CameraRowViewModel(string eventId):this()
        {
            _eventId = eventId;
        }

        // RowId는 읽기 전용 프로퍼티
        public string RowId => _rowId;
        public string EventId => _eventId;
        public ObservableCollection<CameraViewModel> Cameras
        {
            get => _cameras;
            set => Set(ref _cameras, value);
        }
    }
}