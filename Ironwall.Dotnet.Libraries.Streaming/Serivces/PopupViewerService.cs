using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;
using Ironwall.Dotnet.Libraries.Streaming.Views;
using System;
using System.Collections.Concurrent;
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

        private PopupWindowViewModel? _currentViewModel;
        private Window? _currentWindow;
        private readonly object _lock = new object();

        private EnumDisplayPosition _displayPosition = EnumDisplayPosition.TopRight;
        private int _marginOffset = 20; // 화면 가장자리 여백

        private List<string> _listedCameraRowIds = new List<string>();

        public bool IsPopupOpen => _currentWindow?.IsVisible ?? false;
        public List<string> ListedCameraRowIds => _listedCameraRowIds;
        
        public event EventHandler? RequestClose;
        public event EventHandler<string>? RemoveCameraRow;


        public PopupViewerService()
        {
            _log = IoC.Get<ILogService>();
        }

        public void ShowPopup(string eventId, params ICameraModel[] cameras)
        {
            if (cameras == null || cameras.Length == 0)
                return;

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
                    var rowId = _currentViewModel.AddCameras(eventId, cameras);
                    if (!string.IsNullOrEmpty(rowId))
                    {
                        _listedCameraRowIds.Add(rowId);
                        _log?.Info($"[PopupViewerService] Row added: {rowId}. Total rows tracked: {_listedCameraRowIds.Count}");
                    }

                    // 윈도우 표시
                    if (_currentWindow != null && !_currentWindow.IsVisible)
                    {
                        _currentWindow.Show();
                        SetDisplayPosition();
                    }
                }
            });
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
            // ViewModel 생성
            _currentViewModel = new PopupWindowViewModel();
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

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                _log?.Info("[PopupViewerService] Popup window closed");

                if (_currentViewModel != null)
                {
                    _currentViewModel.RequestClose -= OnViewModelRequestClose;
                    _currentViewModel.RemoveCameraRow -= OnViewModelRemoveCameraRow;
                    _currentViewModel.Dispose();
                    _currentViewModel = null;
                }

                if (_currentWindow != null)
                {
                    _currentWindow.Closed -= OnWindowClosed;
                    _currentWindow = null;
                }

                _listedCameraRowIds.Clear();
            }
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

        public void SetDisplayPosition(EnumDisplayPosition? position = null)
        {
            _displayPosition = position ?? EnumDisplayPosition.TopRight;
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

        private void PositionWindow()
        {

            if (_currentWindow == null) return;

            var workArea = SystemParameters.WorkArea;
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