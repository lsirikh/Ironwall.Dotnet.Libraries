using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;
using Ironwall.Dotnet.Libraries.Streaming.Views;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/30/2025 8:47:13 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class PopupViewerService : IPopupViewerService
    {
        private readonly ILogService? _log;
        private readonly ISharedCameraStreamHub? _hub;
        private readonly IStreamingSetupModel? _setupModel;

        private PopupWindowViewModel? _currentViewModel;
        private Window? _currentWindow;
        private readonly object _lock = new object();

        private EnumDisplayPosition _displayPosition = EnumDisplayPosition.TopRight;
        private int _marginOffset = 20; // 화면 가장자리 여백
        private Rect? _targetWorkArea = null;

        private List<string> _listedCameraRowIds = new List<string>();

        public bool IsPopupOpen => _currentWindow?.IsVisible ?? false;
        public List<string> ListedCameraRowIds => _listedCameraRowIds;
        
        public event EventHandler? RequestClose;
        public event EventHandler<string>? RemoveCameraRow;


        public PopupViewerService()
        {
            _log = IoC.Get<ILogService>();
        }

        public PopupViewerService(IStreamingSetupModel setupModel) : this()
        {
            _setupModel = setupModel;
        }

        public PopupViewerService(ISharedCameraStreamHub hub, IStreamingSetupModel setupModel) : this(setupModel)
        {
            _hub = hub;
        }

        public string? ShowPopup(string? eventId = null, string? description = null, params ICameraModel[] cameras)
        {
            if (cameras == null || cameras.Length == 0)
                return null;

            string? rowId = null;
            Execute.OnUIThread(() =>
            {
                lock (_lock)
                {
                    // ViewModel이 없거나 Window가 닫혀있으면 새로 생성
                    if (_currentViewModel == null || _currentWindow?.IsLoaded != true)
                    {
                        CreateNewPopup();
                    }

                    if (_currentViewModel == null) return;

                    // 카메라 추가
                    rowId = _currentViewModel.AddCameras(eventId, description, cameras);
                    if (!string.IsNullOrEmpty(rowId))
                    {
                        _listedCameraRowIds.Add(rowId);
                        _log?.Info($"[PopupViewerService] Row added: {rowId}. Total rows tracked: {_listedCameraRowIds.Count}");
                    }

                    // 윈도우 표시
                    if (_currentWindow != null && !_currentWindow.IsVisible)
                    {
                        _currentWindow.Show();
                        PositionWindow();
                    }
                }
            });
            return rowId;
        }

        public async Task<string?> ShowPopupAsync(string? eventId = null, string? description = null, params ICameraModel[] cameras)
        {
            var delayMs = _setupModel?.PopupStartupDelayMs ?? 0;
            if (delayMs > 0)
            {
                _log?.Info($"[PopupViewerService] Startup delay {delayMs}ms before showing popup");
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
            return ShowPopup(eventId, description, cameras);
        }

        /// <summary>
        /// RowId로 Row 제거 (EventService에서 호출)
        /// </summary>
        public bool RemoveRowById(string rowId)
        {
            if (string.IsNullOrEmpty(rowId))
                return false;

            bool result = false;

            Execute.OnUIThread(() =>
            {
                lock (_lock)
                {
                    if (_currentViewModel == null)
                    {
                        _log?.Warning($"[PopupViewerService] Cannot remove row, ViewModel is null");
                        return;
                    }

                    _log?.Info($"[PopupViewerService] Removing row: {rowId}");
                    result = _currentViewModel.RemoveRowById(rowId);

                    if (result)
                    {
                        _listedCameraRowIds.Remove(rowId);
                        _log?.Info($"[PopupViewerService] Row removed: {rowId}. Remaining rows: {_listedCameraRowIds.Count}");
                    }
                    else
                    {
                        _log?.Warning($"[PopupViewerService] Failed to remove row: {rowId}");
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// 여러 RowId를 한 번에 제거
        /// </summary>
        public void RemoveRowsByIds(params string[] rowIds)
        {
            if (rowIds == null || rowIds.Length == 0)
                return;

            Execute.OnUIThread(() =>
            {
                lock (_lock)
                {
                    foreach (var rowId in rowIds)
                    {
                        RemoveRowById(rowId);
                    }
                }
            });
        }

        private void CreateNewPopup()
        {
            // ViewModel 생성 — Hub가 있으면 Lease 기반 스트림 관리 활성화
            _currentViewModel = _hub != null
                ? new PopupWindowViewModel(_hub)
                : new PopupWindowViewModel();

            // 큐 최소 대기 시간 주입
            _currentViewModel.QueueMinDisplayMs = _setupModel?.QueueMinDisplayMs ?? 0;

            _currentViewModel.RequestClose += OnViewModelRequestClose;
            _currentViewModel.RemoveCameraRow += OnViewModelRemoveCameraRow;

            // PopupWindowView 생성 (일반 Window 대신)
            _currentWindow = new PopupWindowView();
            _currentWindow.DataContext = _currentViewModel;

            _currentWindow.Closed += OnWindowClosed;
            _log?.Info("[PopupViewerService] New popup created");
        }

        private void OnViewModelRemoveCameraRow(object? sender, string rowId)
        {
            // ViewModel에서 Row가 제거될 때 호출
            if (_listedCameraRowIds.Contains(rowId))
            {
                RemoveCameraRow?.Invoke(this, rowId);
                _listedCameraRowIds.Remove(rowId);
                _log?.Info($"[PopupViewerService] Row removed event: {rowId}");
            }
        }

        private void OnViewModelRequestClose(object? sender, EventArgs e)
        {
            RequestClose?.Invoke(this, e);
            ClosePopup();
        }

        private async void OnWindowClosed(object? sender, EventArgs e)
        {
            _log?.Info("[PopupViewerService] Popup window closed");

            PopupWindowViewModel? vmToDispose = null;
            lock (_lock)
            {
                if (_currentViewModel != null)
                {
                    _currentViewModel.RequestClose -= OnViewModelRequestClose;
                    _currentViewModel.RemoveCameraRow -= OnViewModelRemoveCameraRow;
                    vmToDispose = _currentViewModel;
                    _currentViewModel = null;
                }

                if (_currentWindow != null)
                {
                    _currentWindow.Closed -= OnWindowClosed;
                    _currentWindow = null;
                }

                _listedCameraRowIds.Clear();
            }

            // lock 외부에서 await — Lease.DisposeAsync → Hub.ReleaseAsync 체인 완전 실행 보장
            if (vmToDispose != null)
                await vmToDispose.DisposeAsync().ConfigureAwait(false);
        }

        public void ClosePopup()
        {
            Execute.OnUIThread(() =>
            {
                lock (_lock)
                {
                    if (_currentWindow != null)
                    {
                        _log?.Info("[PopupViewerService] Closing popup");
                        _currentWindow.Close();
                    }
                }
            });
        }

        public void SetDisplayPosition(EnumDisplayPosition position)
        {
            _displayPosition = position;
            _log?.Info($"[PopupViewerService] DisplayPosition was updated to {position}");
            if (_currentWindow?.IsVisible == true)
            {
                PositionWindow();
            }
        }

        public void SetMarginOffset(int offset)
        {
            _marginOffset = Math.Max(0, offset);
            if (_currentWindow?.IsVisible == true)
            {
                PositionWindow();
            }
        }

        public void SetTargetWorkArea(Rect workArea)
        {
            _targetWorkArea = workArea;
            _log?.Info($"[PopupViewerService] Target work area set to {workArea}");
            if (_currentWindow?.IsVisible == true)
            {
                PositionWindow();
            }
        }

        private void PositionWindow()
        {
            if (_currentWindow == null) return;

            var workArea = _targetWorkArea ?? SystemParameters.WorkArea;
            double windowWidth = _currentWindow.ActualWidth > 0 ? _currentWindow.ActualWidth : 800;
            double windowHeight = _currentWindow.ActualHeight > 0 ? _currentWindow.ActualHeight : 600;

            double left = 0;
            double top = 0;

            switch (_displayPosition)
            {
                case EnumDisplayPosition.TopLeft:
                    left = workArea.Left + _marginOffset;
                    top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.TopCenter:
                    left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.TopRight:
                    left = workArea.Right - windowWidth - _marginOffset;
                    top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.MiddleLeft:
                    left = workArea.Left + _marginOffset;
                    top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.Center:
                    left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.MiddleRight:
                    left = workArea.Right - windowWidth - _marginOffset;
                    top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.BottomLeft:
                    left = workArea.Left + _marginOffset;
                    top = workArea.Bottom - windowHeight - _marginOffset;
                    break;

                case EnumDisplayPosition.BottomCenter:
                    left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    top = workArea.Bottom - windowHeight - _marginOffset;
                    break;

                case EnumDisplayPosition.BottomRight:
                    left = workArea.Right - windowWidth - _marginOffset;
                    top = workArea.Bottom - windowHeight - _marginOffset;
                    break;
            }

            _currentWindow.Left = left;
            _currentWindow.Top = top;

            _log?.Info($"[PopupViewerService] Window positioned at {_displayPosition}: ({_currentWindow.Left}, {_currentWindow.Top})");
        }
    }

    /// <summary>
    /// 팝업 윈도우 표시 위치
    /// </summary>
    public enum EnumDisplayPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}