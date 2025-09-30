using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;
using Ironwall.Dotnet.Libraries.Streaming.Views;
using System;
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
        private readonly IWindowManager? _windowManager;

        private PopupWindowViewModel _currentViewModel;
        private Window _currentWindow;
        private readonly object _lock = new object();

        public bool IsPopupOpen => _currentWindow?.IsVisible ?? false;

        public PopupViewerService()
        {
            _log = IoC.Get<ILogService>();
            _windowManager = IoC.Get<IWindowManager>();
        }

        public void ShowPopup(params RtspConnectionInfo[] connections)
        {
            if (connections == null || connections.Length == 0)
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

                    // 카메라 추가
                    _currentViewModel.AddCameras(connections);

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

            // Window 생성 (Caliburn.Micro 방식)
            _currentWindow = new PopupWindowView
            {
                DataContext = _currentViewModel
            };

            _currentWindow.Closed += OnWindowClosed;

            _log?.Info("[PopupViewerService] New popup created");
        }

        private void OnViewModelRequestClose(object sender, EventArgs e)
        {
            ClosePopup();
        }

        private void OnWindowClosed(object sender, EventArgs e)
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

        private void PositionWindow()
        {
            if (_currentWindow == null) return;

            // 화면 우측 상단에 위치
            var workArea = SystemParameters.WorkArea;
            _currentWindow.Left = workArea.Right - _currentWindow.Width - 20;  // 우측
            _currentWindow.Top = workArea.Top + 20;  // 상단 (+ 값이어야 함)


            //// 우측 상단
            //_currentWindow.Left = workArea.Right - _currentWindow.Width - 20;
            //_currentWindow.Top = workArea.Top + 20;

            //// 우측 하단
            //_currentWindow.Left = workArea.Right - _currentWindow.Width - 20;
            //_currentWindow.Top = workArea.Bottom - _currentWindow.Height - 20;

            //// 좌측 상단
            //_currentWindow.Left = workArea.Left + 20;
            //_currentWindow.Top = workArea.Top + 20;

            //// 좌측 하단
            //_currentWindow.Left = workArea.Left + 20;
            //_currentWindow.Top = workArea.Bottom - _currentWindow.Height - 20;

            //// 중앙
            //_currentWindow.Left = (workArea.Width - _currentWindow.Width) / 2;
            //_currentWindow.Top = (workArea.Height - _currentWindow.Height) / 2;
        }
    }
}