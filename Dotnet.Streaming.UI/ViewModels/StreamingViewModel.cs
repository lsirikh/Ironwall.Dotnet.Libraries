using Caliburn.Micro;
using Dotnet.Streaming.UI.Models;
using RtspClientSharp;
using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Dotnet.Streaming.UI.ViewModels
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 5/3/2024 4:22:12 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class StreamingViewModel : Screen
    {
        #region - Ctors -
        public StreamingViewModel(IStreamingModel model, string url = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            DeviceAddress = url;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        public void StartClick(object obj, EventArgs e)
        {
            Start();
        }

        public void StopClick(object obj, EventArgs e)
        {
            Close();
        }

        private void MainWindowModelOnStatusChanged(object? sender, string s)
        {
            Application.Current.Dispatcher.Invoke(() => Status = s);
        }
        #endregion
        #region - Processes -
        public void Start()
        {
            var address = DeviceAddress;

            if (!address.StartsWith(RtspPrefix) && !address.StartsWith(HttpPrefix))
                address = RtspPrefix + address;

            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri deviceUri))
            {
                MessageBox.Show("Invalid device address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var credential = new NetworkCredential(Login, Password);

            var connectionParameters = !string.IsNullOrEmpty(deviceUri.UserInfo) ? new ConnectionParameters(deviceUri) :
                new ConnectionParameters(deviceUri, credential);

            connectionParameters.RtpTransport = RtpTransportProtocol.TCP;
            connectionParameters.CancelTimeout = TimeSpan.FromSeconds(5);

            _model.Start(connectionParameters);
            _model.StatusChanged += MainWindowModelOnStatusChanged;
        }

        public void Close()
        {
            _model.Stop();
            _model.StatusChanged -= MainWindowModelOnStatusChanged;

            Status = string.Empty;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public IVideoSource VideoSource => _model.VideoSource;
        public string DeviceAddress
        {
            get { return _deviceAddress; }
            set
            {
                _deviceAddress = value;
                NotifyOfPropertyChange(() => DeviceAddress);
            }
        }
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                NotifyOfPropertyChange(() => Status);
            }
        }
        public string Login { get; set; } = "admin";
        public string Password { get; set; } = "sensorway1";
        #endregion
        #region - Attributes -
        private const string RtspPrefix = "rtsp://";
        private const string HttpPrefix = "http://";

        private readonly IStreamingModel _model;
        private string _status = string.Empty;
        private bool _startButtonEnabled = true;
        private bool _stopButtonEnabled;
        private string _deviceAddress = string.Empty;
        #endregion
    }
}