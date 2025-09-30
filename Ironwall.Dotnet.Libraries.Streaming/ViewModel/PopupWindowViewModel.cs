using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Streaming.ViewModel{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/30/2025 8:26:55 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class PopupWindowViewModel : PropertyChangedBase
    {
        private readonly ILogService? _log;
        private readonly IImprovedRtspStreamingService _streamingService;
        private readonly object _lockObject = new object();
        private Timer? _autoDiscardTimer;

        public PopupWindowViewModel()
        {
            _log = IoC.Get<ILogService>();
            _streamingService = IoC.Get<IImprovedRtspStreamingService>();

            Cameras = new ObservableCollection<CameraItemViewModel>();
            CloseCommand = new RelayCommand(Close);
            ClearCameraCommand = new RelayCommand<CameraItemViewModel>(ClearCamera);
        }

        #region Properties

        private ObservableCollection<CameraItemViewModel>? _cameras;
        public ObservableCollection<CameraItemViewModel>? Cameras
        {
            get => _cameras;
            set => Set(ref _cameras, value);
        }

        private string _title = "이벤트 영상 팝업";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private int _maxCameras = 6;
        public int MaxCameras
        {
            get => _maxCameras;
            set => Set(ref _maxCameras, value);
        }

        public int CurrentCount => Cameras?.Count ?? 0;

        public bool IsEmpty => CurrentCount == 0;

        #endregion

        #region Commands

        public ICommand CloseCommand { get; }
        public ICommand ClearCameraCommand { get; }

        #endregion

        #region Methods

        public void AddCameras(params RtspConnectionInfo[] connections)
        {
            lock (_lockObject)
            {
                // FIFO 로직: 최대 개수 초과시 오래된 것부터 제거
                while (Cameras.Count + connections.Length > MaxCameras)
                {
                    var oldest = Cameras.First();
                    RemoveCamera(oldest);
                }

                // 새 카메라 추가 (상단에 추가)
                foreach (var conn in connections)
                {
                    var cameraVm = new CameraItemViewModel
                    {
                        Id = $"popup_cam_{Guid.NewGuid():N}",
                        ConnectionInfo = conn,
                        StartTime = DateTime.Now,
                        DisplayName = conn.Description ?? conn.CameraName
                    };

                    Cameras.Insert(0, cameraVm);  // 최신을 맨 위로

                    // 스트리밍 서비스 상태 변경 이벤트 구독
                    SubscribeToStreamingEvents(cameraVm.Id);
                }

                NotifyOfPropertyChange(nameof(CurrentCount));
                NotifyOfPropertyChange(nameof(IsEmpty));
            }
        }

        private void RemoveCamera(CameraItemViewModel camera)
        {
            if (camera == null) return;

            // 이벤트 구독 해제
            UnsubscribeFromStreamingEvents(camera.Id);

            // 목록에서 제거
            Cameras.Remove(camera);

            // 카운트 업데이트
            NotifyOfPropertyChange(nameof(CurrentCount));
            NotifyOfPropertyChange(nameof(IsEmpty));

            // 비어있으면 닫기 요청
            if (IsEmpty)
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ClearCamera(CameraItemViewModel camera)
        {
            RemoveCamera(camera);
        }

        private void Close()
        {
            // 모든 카메라 정리
            var camerasToRemove = Cameras.ToList();
            foreach (var camera in camerasToRemove)
            {
                RemoveCamera(camera);
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void SubscribeToStreamingEvents(string contextId)
        {
            // IsAutoDiscard 처리를 위한 이벤트 구독
            if (_streamingService != null)
            {
                // StateChanged 이벤트로 Disconnected 감지
                _streamingService.StateChanged += OnStreamingStateChanged;
            }
        }

        private void UnsubscribeFromStreamingEvents(string contextId)
        {
            if (_streamingService != null)
            {
                _streamingService.StateChanged -= OnStreamingStateChanged;
            }
        }

        private void OnStreamingStateChanged(object sender, StreamingStateChangedEventArgs e)
        {
            // Disconnected 상태면 해당 카메라 제거
            if (e.NewState == PlaybackState.Disconnected)
            {
                var camera = Cameras.FirstOrDefault(c => c.Id == e.ContextId);
                if (camera != null)
                {
                    Execute.OnUIThread(() => RemoveCamera(camera));
                }
            }
        }

        #endregion

        #region Events

        public event EventHandler? RequestClose;

        #endregion
    }
}