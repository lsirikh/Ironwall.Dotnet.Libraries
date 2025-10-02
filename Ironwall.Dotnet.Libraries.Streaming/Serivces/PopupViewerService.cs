using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
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
        //private readonly IWindowManager? _windowManager;

        private PopupWindowViewModel? _currentViewModel;
        private Window? _currentWindow;
        private readonly object _lock = new object();

        private EnumDisplayPosition _displayPosition = EnumDisplayPosition.TopRight;
        private int _marginOffset = 20; // 화면 가장자리 여백

        private List<string> _listedCameraRowIds = new List<string>();

        public bool IsPopupOpen => _currentWindow?.IsVisible ?? false;
        public List<string> ListedCameraRowIds => _listedCameraRowIds;
        public PopupViewerService()
        {
            _log = IoC.Get<ILogService>();
            //_windowManager = IoC.Get<IWindowManager>();
        }

        public void ShowPopup(params ICameraModel[] cameras)
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
                    var row = _currentViewModel.AddCameras(cameras);
                    _listedCameraRowIds.Add(row);

                    // 윈도우 표시
                    if (_currentWindow != null && !_currentWindow.IsVisible)
                    {
                        _currentWindow.Show();
                        PositionWindow();
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

        private void OnViewModelRemoveCameraRow(object? sender, string e)
        {
            _listedCameraRowIds.Add(e);
        }

        private void OnViewModelRequestClose(object? sender, EventArgs e)
        {
            ClosePopup();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                if (_currentViewModel != null)
                {
                    _currentViewModel.RequestClose -= OnViewModelRequestClose;
                    _currentViewModel = null;
                }

                if (_currentWindow != null)
                {
                    _currentWindow.Closed -= OnWindowClosed;
                    _currentWindow = null;
                }

                _log?.Info("[PopupViewerService] Popup closed");
            }
        }

        public void ClosePopup()
        {
            Execute.OnUIThread(() =>
            {
                _currentWindow?.Close();
            });
        }

        public void PositionWindow(EnumDisplayPosition? position = null)
        {
            if (_currentWindow == null) return;

            var workArea = SystemParameters.WorkArea;
            var windowWidth = _currentWindow.Width;
            var windowHeight = _currentWindow.Height;

            if(position != null) 
                _displayPosition = position ?? EnumDisplayPosition.TopRight;

            switch (_displayPosition)
            {
                case EnumDisplayPosition.TopLeft:
                    _currentWindow.Left = workArea.Left + _marginOffset;
                    _currentWindow.Top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.TopCenter:
                    _currentWindow.Left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    _currentWindow.Top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.TopRight:
                    _currentWindow.Left = workArea.Right - windowWidth - _marginOffset;
                    _currentWindow.Top = workArea.Top + _marginOffset;
                    break;

                case EnumDisplayPosition.MiddleLeft:
                    _currentWindow.Left = workArea.Left + _marginOffset;
                    _currentWindow.Top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.Center:
                    _currentWindow.Left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    _currentWindow.Top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.MiddleRight:
                    _currentWindow.Left = workArea.Right - windowWidth - _marginOffset;
                    _currentWindow.Top = workArea.Top + (workArea.Height - windowHeight) / 2;
                    break;

                case EnumDisplayPosition.BottomLeft:
                    _currentWindow.Left = workArea.Left + _marginOffset;
                    _currentWindow.Top = workArea.Bottom - windowHeight - _marginOffset;
                    break;

                case EnumDisplayPosition.BottomCenter:
                    _currentWindow.Left = workArea.Left + (workArea.Width - windowWidth) / 2;
                    _currentWindow.Top = workArea.Bottom - windowHeight - _marginOffset;
                    break;

                case EnumDisplayPosition.BottomRight:
                    _currentWindow.Left = workArea.Right - windowWidth - _marginOffset;
                    _currentWindow.Top = workArea.Bottom - windowHeight - _marginOffset;
                    break;

                default:
                    // 기본값: TopRight
                    _currentWindow.Left = workArea.Right - windowWidth - _marginOffset;
                    _currentWindow.Top = workArea.Top + _marginOffset;
                    break;
            }

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