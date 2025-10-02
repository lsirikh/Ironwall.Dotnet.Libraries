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
    

    public class PopupWindowViewModel : PropertyChangedBase, IDisposable
    {
        private readonly ILogService? _log;
        private readonly IImprovedRtspStreamingService? _streamingService;
        private readonly object _lockObject = new object();

        // Row 관리
        private const int MAX_ROWS = 5; // 최대 Row의 갯수

        private const int MAX_CAMERAS_PER_ROW = 3; // 1개 Row 당 최대 시현 카메라

        private readonly Dictionary<string, CameraViewModel> _cameraLookup
        = new Dictionary<string, CameraViewModel>();

        public PopupWindowViewModel()
        {
            _log = IoC.Get<ILogService>();
            _streamingService = IoC.Get<IImprovedRtspStreamingService>();

            CloseCommand = new RelayCommand(Close);
            ClearCameraCommand = new RelayCommand<CameraViewModel>(ClearCamera);

            _streamingService.StateChanged += OnStreamingStateChanged;
        }

        #region Properties
        private ObservableCollection<CameraRowViewModel> _cameraRows = new ObservableCollection<CameraRowViewModel>();
        public ObservableCollection<CameraRowViewModel> CameraRows
        {
            get => _cameraRows;
            set
            {
                if (Set(ref _cameraRows, value))
                {
                    NotifyOfPropertyChange(nameof(CurrentCount));
                    NotifyOfPropertyChange(nameof(RowCount));
                    NotifyOfPropertyChange(nameof(IsEmpty));
                }
            }
        }

        private string _title = "이벤트 영상 팝업";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private int _maxCameras = 15;
        public int MaxCameras
        {
            get => _maxCameras;
            set => Set(ref _maxCameras, value);
        }

        // Row들의 모든 카메라 수 합계
        public int CurrentCount => CameraRows?.Sum(row => row.Cameras?.Count ?? 0) ?? 0;
        public int RowCount => CameraRows?.Count ?? 0;
        public bool IsEmpty => CurrentCount == 0;

        #endregion

        #region Commands

        public ICommand? CloseCommand { get; }
        public ICommand? ClearCameraCommand { get; }

        #endregion

        #region Methods

        public string? AddCameras(params ICameraModel[] cameras)
        {
            lock (_lockObject)
            {
                if (cameras == null || cameras.Length == 0)
                    return null;

                _log?.Info($"[PopupWindowViewModel] Adding {cameras.Length} cameras as new row");

                // 새로운 Row 생성
                var newRow = new CameraRowViewModel();

                // 연결 정보를 CameraItemViewModel로 변환 (최대 2개까지)
                var camerasToAdd = cameras.Take(MAX_CAMERAS_PER_ROW).Select(conn =>
                {
                    return new CameraViewModel(conn);
                }).ToList();

                // Row에 카메라 추가
                foreach (var camera in camerasToAdd)
                {
                    newRow.Cameras.Add(camera);

                    //Dictionary에 추가
                    _cameraLookup[camera.Guid] = camera;
                }

                // FIFO 처리 - 3줄을 넘으면 맨 아래 줄 제거
                if (CameraRows.Count >= MAX_ROWS)
                {
                    RemoveBottomRow();
                }

                // 맨 위에 새 Row 추가
                CameraRows.Insert(0, newRow);

                // UI 업데이트
                NotifyOfPropertyChange(nameof(CurrentCount));
                NotifyOfPropertyChange(nameof(RowCount));
                NotifyOfPropertyChange(nameof(IsEmpty));

                _log?.Info($"[PopupWindowViewModel] Added new row({newRow.RowId}) with {camerasToAdd.Count} cameras. Total rows: {RowCount}");

                return newRow.RowId;
            }
        }

        private void RemoveBottomRow()
        {
            if (CameraRows.Count == 0)
                return;

            var bottomRow = CameraRows[CameraRows.Count - 1];

            _log?.Info($"[PopupWindowViewModel] Removing bottom row with {bottomRow.Cameras.Count} cameras");

            //Dictionary에서도 제거
            foreach (var camera in bottomRow.Cameras)
            {
                _cameraLookup.Remove(camera.Guid);
            }


            // Row 제거
            CameraRows.RemoveAt(CameraRows.Count - 1);

            RemoveCameraRow?.Invoke(this, bottomRow.RowId);
        }

        private void RemoveCamera(CameraViewModel camera)
        {
            if (camera == null) return;

            //Dictionary에서도 제거
            _cameraLookup.Remove(camera.Guid);

            // Row에서 카메라 찾아서 제거
            var row = CameraRows.FirstOrDefault(r => r.Cameras.Contains(camera));
            if (row != null)
            {
                row.Cameras.Remove(camera);

                // Row가 비었으면 Row도 제거
                if (row.Cameras.Count == 0)
                {
                    CameraRows.Remove(row);
                }
            }

            // 카운트 업데이트
            NotifyOfPropertyChange(nameof(CurrentCount));
            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(RowCount));

            // 비어있으면 닫기 요청
            if (IsEmpty)
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }


        private CameraViewModel? FindCameraById(string contextId)
        {
            _cameraLookup.TryGetValue(contextId, out var camera);
            return camera;
        }

        private void ClearCamera(CameraViewModel camera)
        {
            RemoveCamera(camera);
        }

        private void Close()
        {
            if(_streamingService != null)
                _streamingService.StateChanged -= OnStreamingStateChanged;

            CameraRows.Clear();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }


        private void OnStreamingStateChanged(object? sender, StreamingStateChangedEventArgs e)
        {
            // Disconnected 상태면 해당 카메라 제거
            if (e.NewState == PlaybackState.Disconnected || e.NewState == PlaybackState.Stopped)
            {
                Execute.OnUIThread(() =>
                {
                    var camera = FindCameraById(e.ContextId);
                    if (camera != null)
                    {
                        RemoveCamera(camera);
                    }
                });
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {

                _isDisposed = true;
                CameraRows.Clear();
            }
        }

        #endregion

        #region Events

        public event EventHandler? RequestClose;
        public event EventHandler<string>? RemoveCameraRow;
        private bool _isDisposed = false;
        #endregion
    }
}