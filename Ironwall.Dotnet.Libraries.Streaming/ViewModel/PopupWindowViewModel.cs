using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
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
        #region Fields
        private readonly ILogService? _log;
        private readonly IImprovedRtspStreamingService? _streamingService;
        private readonly object _lockObject = new object();

        // Row 관리 상수
        private const int MAX_ROWS = 5; // 최대 표시 가능한 Row의 갯수
        private const int MAX_CAMERAS_PER_ROW = 3; // 1개 Row 당 최대 시현 카메라

        // ContextId  조회용 Dictionary
        private readonly Dictionary<string, CameraViewModel> _contextLookup = new Dictionary<string, CameraViewModel>();

        // 대기 큐 - FIFO 방식으로 관리
        private readonly Queue<CameraRowViewModel> _waitingQueue = new Queue<CameraRowViewModel>();

        // RowId로 빠른 조회를 위한 Dictionary
        private readonly Dictionary<string, CameraRowViewModel> _rowLookup = new Dictionary<string, CameraRowViewModel>();

        private bool _isDisposed = false;
        #endregion

        #region Constructor
        public PopupWindowViewModel()
        {
            _log = IoC.Get<ILogService>();
            _streamingService = IoC.Get<IImprovedRtspStreamingService>();

            CloseCommand = new RelayCommand(Close);
            ClearCameraCommand = new RelayCommand<CameraViewModel>(ClearCamera);

            _streamingService.StateChanged += OnStreamingStateChanged;
        }
        #endregion

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

        // 표시 중인 Row 개수
        public int RowCount => CameraRows?.Count ?? 0;

        // 비어있는지 여부
        public bool IsEmpty => CurrentCount == 0;

        // 대기 큐의 Row 개수
        public int WaitingQueueCount => _waitingQueue.Count;
        #endregion

        #region Commands
        public ICommand? CloseCommand { get; }
        public ICommand? ClearCameraCommand { get; }
        #endregion

        #region Public Methods
        /// <summary>
        /// 카메라들을 새로운 Row로 추가
        /// MAX_ROWS를 넘으면 대기 큐에 추가됨
        /// </summary>
        /// <param name="cameras">추가할 카메라 배열</param>
        /// <returns>생성된 RowId</returns>
        public string? AddCameras(string eventId, params ICameraModel[] cameras)
        {
            lock (_lockObject)
            {
                if (cameras == null || cameras.Length == 0)
                    return null;

                _log?.Info($"[PopupWindowViewModel] Adding {cameras.Length} cameras as new row");

                // 새로운 Row 생성
                var newRow = new CameraRowViewModel(eventId);

                // 연결 정보를 CameraItemViewModel로 변환 (최대 2개까지)
                var camerasToAdd = cameras.Take(MAX_CAMERAS_PER_ROW).Select(conn =>
                {
                    return new CameraViewModel(conn, newRow.RowId);
                }).ToList();

                // Row에 카메라 추가
                foreach (var camera in camerasToAdd)
                {
                    newRow.Cameras.Add(camera);

                    //Dictionary에 추가
                    _contextLookup[camera.ContextId] = camera;
                }

                // RowId로 조회 가능하도록 Dictionary에 추가
                _rowLookup[newRow.RowId] = newRow;

                // FIFO 처리 - 3줄을 넘으면 맨 아래 줄 제거
                if (CameraRows.Count >= MAX_ROWS)
                {
                    _waitingQueue.Enqueue(newRow);
                    _log?.Info($"[PopupWindowViewModel] Row({newRow.RowId}) added to waiting queue. Queue count: {_waitingQueue.Count}");
                    //RemoveBottomRow();
                }else
                {
                    // 맨 위에 새 Row 추가 (FIFO - 위에서 추가)
                    CameraRows.Insert(0, newRow);
                    _log?.Info($"[PopupWindowViewModel] Added new row({newRow.RowId}) with {camerasToAdd.Count} cameras.");
                }

                // UI 업데이트
                NotifyOfPropertyChange(nameof(CurrentCount));
                NotifyOfPropertyChange(nameof(RowCount));
                NotifyOfPropertyChange(nameof(IsEmpty));
                NotifyOfPropertyChange(nameof(WaitingQueueCount));

                _log?.Info($"[PopupWindowViewModel] Current count: {CurrentCount}, Total rows: {RowCount}, Waiting: {WaitingQueueCount}");

                return newRow.RowId;
            }
        }

        /// <summary>
        /// RowId로 Row 삭제 (표시 중 또는 대기 중 모두 가능)
        /// </summary>
        /// <param name="rowId">삭제할 Row의 ID</param>
        /// <returns>삭제 성공 여부</returns>
        public bool RemoveRowById(string rowId)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(rowId))
                    return false;

                _log?.Info($"[PopupWindowViewModel] Attempting to remove row by ID: {rowId}");

                // RowId로 Row 조회
                if (!_rowLookup.TryGetValue(rowId, out var row))
                {
                    _log?.Warning($"[PopupWindowViewModel] Row not found: {rowId}");
                    return false;
                }

                // 1. 표시 중인 Row에서 찾아서 삭제
                if (CameraRows.Contains(row))
                {
                    _log?.Info($"[PopupWindowViewModel] Removing displayed row: {rowId}");

                    // Dictionary에서 카메라들 제거
                    foreach (var camera in row.Cameras)
                    {
                        _contextLookup.Remove(camera.ContextId);
                    }

                    // Row 제거
                    CameraRows.Remove(row);
                    _rowLookup.Remove(rowId);

                    RemoveCameraRow?.Invoke(this, rowId);

                    // 대기 큐에서 다음 Row를 가져와서 표시
                    ProcessWaitingQueue();

                    // UI 업데이트
                    NotifyOfPropertyChange(nameof(CurrentCount));
                    NotifyOfPropertyChange(nameof(RowCount));
                    NotifyOfPropertyChange(nameof(IsEmpty));
                    NotifyOfPropertyChange(nameof(WaitingQueueCount));

                    _log?.Info($"[PopupWindowViewModel] Row removed. Current: {RowCount}, Waiting: {WaitingQueueCount}");

                    // 비어있으면 닫기 요청
                    if (IsEmpty && WaitingQueueCount == 0)
                    {
                        RequestClose?.Invoke(this, EventArgs.Empty);
                    }

                    return true;
                }

                // 2. 대기 큐에서 찾아서 삭제
                if (_waitingQueue.Contains(row))
                {
                    _log?.Info($"[PopupWindowViewModel] Removing row from waiting queue: {rowId}");

                    // Queue는 Remove를 직접 지원하지 않으므로, 재구성 필요
                    var tempList = _waitingQueue.ToList();
                    tempList.Remove(row);
                    _waitingQueue.Clear();
                    foreach (var item in tempList)
                    {
                        _waitingQueue.Enqueue(item);
                    }

                    // Dictionary에서 카메라들 제거
                    foreach (var camera in row.Cameras)
                    {
                        _contextLookup.Remove(camera.ContextId);
                    }

                    _rowLookup.Remove(rowId);

                    RemoveCameraRow?.Invoke(this, rowId);

                    NotifyOfPropertyChange(nameof(WaitingQueueCount));

                    _log?.Info($"[PopupWindowViewModel] Row removed from queue. Waiting: {WaitingQueueCount}");

                    return true;
                }

                _log?.Warning($"[PopupWindowViewModel] Row not found in displayed or waiting: {rowId}");
                return false;
            }
        }

        #endregion
        #region Private Methods
        /// <summary>
        /// 대기 큐에서 다음 Row를 가져와서 표시
        /// </summary>
        private void ProcessWaitingQueue()
        {
            // 표시 공간이 있고, 대기 큐에 Row가 있으면
            while (CameraRows.Count < MAX_ROWS && _waitingQueue.Count > 0)
            {
                var nextRow = _waitingQueue.Dequeue();
                CameraRows.Insert(0, nextRow);

                _log?.Info($"[PopupWindowViewModel] Moved row({nextRow.RowId}) from queue to display. Remaining in queue: {_waitingQueue.Count}");
            }
        }

        /// <summary>
        /// 카메라 단위로 삭제 (기존 메서드 유지)
        /// </summary>
        private void RemoveBottomRow()
        {
            if (CameraRows.Count == 0)
                return;

            var bottomRow = CameraRows[CameraRows.Count - 1];

            _log?.Info($"[PopupWindowViewModel] Removing bottom row with {bottomRow.Cameras.Count} cameras");

            //Dictionary에서도 제거
            foreach (var camera in bottomRow.Cameras)
            {
                _contextLookup.Remove(camera.ContextId);
            }


            // Row 제거
            CameraRows.RemoveAt(CameraRows.Count - 1);

            RemoveCameraRow?.Invoke(this, bottomRow.RowId);
        }

        /// <summary>
        /// 스트리밍 상태 변화 이벤트 메소드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                        _log?.Info($"[PopupWindowViewModel] Camera disconnected/stopped, removing: {e.ContextId}");
                        RemoveCamera(camera);
                    }
                });
            }
        }

        /// <summary>
        /// 카메라 단위로 삭제 (기존 메서드 유지)
        /// </summary>
        private void RemoveCamera(CameraViewModel camera)
        {
            if (camera == null) return;

            lock (_lockObject)
            {
                _log?.Info($"[PopupWindowViewModel] Removing camera: {camera.ContextId}");

                // Dictionary에서도 제거
                _contextLookup.Remove(camera.ContextId);

                // Row에서 카메라 찾아서 제거
                var row = CameraRows.FirstOrDefault(r => r.Cameras.Contains(camera));
                if (row != null)
                {
                    row.Cameras.Remove(camera);

                    // Row가 비었으면 Row도 제거
                    if (row.Cameras.Count == 0)
                    {
                        _log?.Info($"[PopupWindowViewModel] Row is empty, removing row: {row.RowId}");
                        RemoveRowById(row.RowId);
                        return; // RemoveRowById에서 UI 업데이트를 처리하므로 여기서 종료
                    }
                }

                // 카운트 업데이트
                NotifyOfPropertyChange(nameof(CurrentCount));
                NotifyOfPropertyChange(nameof(IsEmpty));
                NotifyOfPropertyChange(nameof(RowCount));

                // 비어있으면 닫기 요청
                if (IsEmpty && WaitingQueueCount == 0)
                {
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        


        private CameraViewModel? FindCameraById(string contextId)
        {
            _contextLookup.TryGetValue(contextId, out var camera);
            return camera;
        }

        private void ClearCamera(CameraViewModel camera)
        {
            RemoveCamera(camera);
        }

        private void Close()
        {
            if (_streamingService != null)
                _streamingService.StateChanged -= OnStreamingStateChanged;

            lock (_lockObject)
            {
                CameraRows.Clear();
                _waitingQueue.Clear();
                _contextLookup.Clear();
                _rowLookup.Clear();
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                if (_streamingService != null)
                    _streamingService.StateChanged -= OnStreamingStateChanged;

                lock (_lockObject)
                {
                    CameraRows.Clear();
                    _waitingQueue.Clear();
                    _contextLookup.Clear();
                    _rowLookup.Clear();
                }
            }
        }
        #endregion

        #region Events

        public event EventHandler? RequestClose;
        public event EventHandler<string>? RemoveCameraRow;
        #endregion
    }
}