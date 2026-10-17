using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.Serivces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    

    public class PopupWindowViewModel : PropertyChangedBase, IDisposable, IAsyncDisposable
    {
        #region Fields
        private readonly ILogService? _log;
        private readonly IImprovedRtspStreamingService? _streamingService;
        private readonly ISharedCameraStreamHub? _hub;
        private readonly object _lockObject = new object();

        // Row 관리 상수
        private const int MAX_ROWS = 3; // 최대 표시 가능한 Row의 갯수
        private const int MAX_CAMERAS_PER_ROW = 3; // 1개 Row 당 최대 시현 카메라

        // ContextId  조회용 Dictionary
        private readonly Dictionary<string, CameraViewModel> _contextLookup = new Dictionary<string, CameraViewModel>();

        // 대기 큐 - FIFO 방식으로 관리
        private readonly Queue<CameraRowViewModel> _waitingQueue = new Queue<CameraRowViewModel>();

        // RowId로 빠른 조회를 위한 Dictionary
        private readonly Dictionary<string, CameraRowViewModel> _rowLookup = new Dictionary<string, CameraRowViewModel>();

        private bool _isDisposed = false;

        // 큐 항목 최소 표시 대기 ms — PopupViewerService가 주입
        public int QueueMinDisplayMs { get; set; } = 0;

        // 마지막으로 Row가 화면에 표시된 시각 (레이트 리밋 기준)
        private DateTime _lastRowDisplayedAt = DateTime.MinValue;

        // 이미 예약된 Dequeue 작업이 있는지 (중복 스케줄 방지)
        private bool _dequeueScheduled = false;
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

        public PopupWindowViewModel(ISharedCameraStreamHub hub) : this()
        {
            _hub = hub;
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

        private int _maxCameras = MAX_ROWS * MAX_CAMERAS_PER_ROW;
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
        public string? AddCameras(string eventId, string description, params ICameraModel[] cameras)
        {
            lock (_lockObject)
            {
                if (cameras == null || cameras.Length == 0)
                    return null;

                _log?.Info($"[PopupWindowViewModel] Adding {cameras.Length} cameras as new row");

                // 새로운 Row 생성 — Hub가 주입된 경우 Lease 기반 스트림 관리 활성화
                var newRow = _hub != null
                    ? new CameraRowViewModel(eventId, description, _hub)
                    : new CameraRowViewModel(eventId, description);

                // 연결 정보를 CameraItemViewModel로 변환 (최대 2개까지)
                var camerasToAdd = cameras.Take(MAX_CAMERAS_PER_ROW).Select(conn =>
                {
                    return new CameraViewModel(conn, newRow.RowId, newRow);
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

                // Row 개별 종료 구독 — sender 기반, 클로저 캡처 금지
                newRow.CloseRequested += OnRowCloseRequested;

                // Queue-First: 모든 이벤트는 예외 없이 Queue 경유.
                // QueueMinDisplayMs 대기 후 슬롯이 있으면 표시.
                // QueueMinDisplayMs=0이면 Task.Delay(0) → 즉시 표시 (체감 동일).
                // 첫 이벤트도 예외 없음 — 연속성 테스트와 Redis 경로 모두 동일하게 처리.
                _waitingQueue.Enqueue(newRow);
                _log?.Info($"[PopupWindowViewModel] Row({newRow.RowId}) enqueued. Queue: {_waitingQueue.Count}");
                if (CameraRows.Count < MAX_ROWS)
                    ScheduleDequeue();

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
                if (_isDisposed) return false;
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
                    row.CloseRequested -= OnRowCloseRequested;
                    row.Dispose();

                    RemoveCameraRow?.Invoke(this, rowId);

                    // 대기 큐에서 다음 Row를 가져와서 표시
                    ProcessWaitingQueue();

                    // UI 업데이트
                    NotifyOfPropertyChange(nameof(CurrentCount));
                    NotifyOfPropertyChange(nameof(RowCount));
                    NotifyOfPropertyChange(nameof(IsEmpty));
                    NotifyOfPropertyChange(nameof(WaitingQueueCount));

                    _log?.Info($"[PopupWindowViewModel] Row removed. Current: {RowCount}, Waiting: {WaitingQueueCount}");

                    // 비어있으면 닫기 요청 (dequeue 타이머 pending 중이면 닫지 않음)
                    if (IsEmpty && WaitingQueueCount == 0 && !_dequeueScheduled)
                    {
                        RequestClose?.Invoke(this, EventArgs.Empty);
                    }

                    return true;
                }

                // 2. 대기 큐에서 찾아서 삭제
                if (_waitingQueue.Contains(row))
                {
                    _log?.Info($"[PopupWindowViewModel] Removing row from waiting queue: {rowId}");

                    // 취소 마킹 — ProcessWaitingQueue에서 Dequeue 시 자동 스킵됨
                    row.Cancel();

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
                    row.CloseRequested -= OnRowCloseRequested;
                    row.Dispose();

                    RemoveCameraRow?.Invoke(this, rowId);

                    NotifyOfPropertyChange(nameof(WaitingQueueCount));
                    NotifyOfPropertyChange(nameof(IsEmpty));
                    NotifyOfPropertyChange(nameof(CurrentCount));
                    NotifyOfPropertyChange(nameof(RowCount));

                    // 대기 큐에서 제거 후 모두 비었으면 팝업 창 닫기 요청
                    // dequeue 타이머 pending 중이면 닫지 않음 (타이머 콜백에서 최종 판단)
                    if (IsEmpty && WaitingQueueCount == 0 && !_dequeueScheduled)
                        RequestClose?.Invoke(this, EventArgs.Empty);

                    _log?.Info($"[PopupWindowViewModel] Row removed from queue. Waiting: {WaitingQueueCount}");

                    return true;
                }

                _log?.Warning($"[PopupWindowViewModel] Row not found in displayed or waiting: {rowId}");
                return false;
            }
        }

        #endregion
        #region Private Methods

        private void OnRowCloseRequested(object? sender, string rowId)
        {
            // sender 사용 — newRow 클로저 캡처 금지 (멀티 Row 시 wrong row 참조 버그 방지)
            if (sender is CameraRowViewModel row)
                row.CloseRequested -= OnRowCloseRequested;
            RemoveRowById(rowId);
        }

        /// <summary>
        /// 표시 슬롯이 비워졌을 때 대기 큐에서 다음 Row를 가져올지 결정.
        /// QueueMinDisplayMs > 0 이면 남은 대기 시간만큼 지연 후 표시.
        /// </summary>
        private void ProcessWaitingQueue()
        {
            if (_waitingQueue.Count == 0) return;

            if (QueueMinDisplayMs > 0)
                ScheduleDequeue();
            else
                DequeueFromWaitingQueue();
        }

        /// <summary>
        /// 남은 대기 시간을 계산해 단 하나의 Dequeue 작업을 스케줄링.
        /// 이미 예약된 작업이 있으면 중복 스케줄을 건너뜀.
        /// </summary>
        private void ScheduleDequeue()
        {
            if (_dequeueScheduled) return;
            _dequeueScheduled = true;

            int remainingMs;
            if (_lastRowDisplayedAt == DateTime.MinValue)
            {
                remainingMs = 0;
            }
            else
            {
                var elapsed = (DateTime.UtcNow - _lastRowDisplayedAt).TotalMilliseconds;
                remainingMs = (int)Math.Max(0, QueueMinDisplayMs - elapsed);
            }

            _ = Task.Delay(remainingMs).ContinueWith(
                _ => Execute.OnUIThread(() =>
                {
                    if (_isDisposed) return;
                    _dequeueScheduled = false;
                    DequeueFromWaitingQueue();
                    // 타이머 후 dequeue했음에도 여전히 비어있으면 닫기 (OFF로 큐가 비워진 경우)
                    if (IsEmpty && WaitingQueueCount == 0 && !_dequeueScheduled)
                        RequestClose?.Invoke(this, EventArgs.Empty);
                }),
                TaskScheduler.Default);
        }

        private void DequeueFromWaitingQueue()
        {
            lock (_lockObject)
            {
                if (CameraRows.Count >= MAX_ROWS || _waitingQueue.Count == 0) return;

                var nextRow = _waitingQueue.Dequeue();
                while (nextRow != null && nextRow.IsCancelled)
                {
                    _rowLookup.Remove(nextRow.RowId);
                    nextRow.Dispose();
                    _log?.Info($"[PopupWindowViewModel] Skipped cancelled row: {nextRow.RowId}. Remaining: {_waitingQueue.Count}");
                    nextRow = _waitingQueue.Count > 0 ? _waitingQueue.Dequeue() : null;
                }

                if (nextRow == null) return;

                CameraRows.Insert(0, nextRow);
                _lastRowDisplayedAt = DateTime.UtcNow;
                NotifyOfPropertyChange(nameof(CurrentCount));
                NotifyOfPropertyChange(nameof(RowCount));
                NotifyOfPropertyChange(nameof(IsEmpty));
                NotifyOfPropertyChange(nameof(WaitingQueueCount));
                _log?.Info($"[PopupWindowViewModel] Moved row({nextRow.RowId}) from queue to display. Remaining: {_waitingQueue.Count}");

                // 슬롯이 남아 있고 큐에 더 있으면 다음 것도 스케줄
                if (CameraRows.Count < MAX_ROWS && _waitingQueue.Count > 0)
                    ScheduleDequeue();
            }
        }

        // QueueMinDisplayMs == 0 이거나 충분한 시간이 지났으면 즉시 표시 가능
        private bool CanShowNow() =>
            QueueMinDisplayMs == 0 ||
            _lastRowDisplayedAt == DateTime.MinValue ||
            (DateTime.UtcNow - _lastRowDisplayedAt).TotalMilliseconds >= QueueMinDisplayMs;

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

            CameraRowViewModel[] rowsToDispose;
            lock (_lockObject)
            {
                // DisposeAsync()는 CameraRows.ToArray()로 rows를 수집하는데,
                // Clear() 이후 호출되면 빈 배열을 보게 된다. Close() 경로에서는
                // 여기서 직접 fire-and-forget 해제해야 Hub 세션이 제때 종료된다.
                rowsToDispose = CameraRows.Concat(_waitingQueue).ToArray();
                CameraRows.Clear();
                _waitingQueue.Clear();
                _contextLookup.Clear();
                _rowLookup.Clear();
            }

            foreach (var row in rowsToDispose)
            {
                row.CloseRequested -= OnRowCloseRequested;
                _ = row.DisposeAsync().AsTask();
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_streamingService != null)
                _streamingService.StateChanged -= OnStreamingStateChanged;

            CameraRowViewModel[] activeRows;
            CameraRowViewModel[] queuedRows;
            string[] contextIds;
            lock (_lockObject)
            {
                activeRows = CameraRows.ToArray();
                queuedRows = _waitingQueue.ToArray();
                contextIds = _contextLookup.Keys.ToArray();
                CameraRows.Clear();
                _waitingQueue.Clear();
                _contextLookup.Clear();
                _rowLookup.Clear();
            }

            foreach (var row in activeRows.Concat(queuedRows))
                await row.DisposeAsync().ConfigureAwait(false);

            if (_streamingService != null)
            {
                foreach (var contextId in contextIds)
                    await _streamingService.DisconnectAsync(contextId).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        #endregion

        #region Events

        public event EventHandler? RequestClose;
        public event EventHandler<string>? RemoveCameraRow;
        #endregion
    }
}