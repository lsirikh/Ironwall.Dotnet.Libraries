using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.ViewModel{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/30/2025 8:43:17 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraItemViewModel : PropertyChangedBase
    {
        private string _id;
        public string Id
        {
            get => _id;
            set => Set(ref _id, value);
        }

        private RtspConnectionInfo _connectionInfo;
        public RtspConnectionInfo ConnectionInfo
        {
            get => _connectionInfo;
            set => Set(ref _connectionInfo, value);
        }

        private DateTime _startTime;
        public DateTime StartTime
        {
            get => _startTime;
            set => Set(ref _startTime, value);
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set => Set(ref _displayName, value);
        }

        private int _remainingSeconds;
        public int RemainingSeconds
        {
            get => _remainingSeconds;
            set => Set(ref _remainingSeconds, value);
        }

        // 타임아웃 표시용
        public string TimeoutDisplay =>
            RemainingSeconds > 0 ? $"{DisplayName} ({RemainingSeconds}s)" : DisplayName;
    }
}