using Caliburn.Micro;
using Dotnet.Streaming.UI.Models;
using RtspClientSharp;
using System;
using System.Net;
using System.Windows;

namespace Dotnet.Streaming.UI.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 4/11/2025 11:57:21 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AiStreamingViewModel : Screen
{
    private const string RtspPrefix = "rtsp://";
    private const string HttpPrefix = "http://";

    private readonly IStreamingModel _model;
    private string? _deviceAddress;
    private string? _status;

    public AiStreamingViewModel(IStreamingModel model, string url = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        DeviceAddress = url;
    }

    public IVideoSource VideoSource => _model.VideoSource;

    public string? DeviceAddress
    {
        get => _deviceAddress;
        set
        {
            _deviceAddress = value;
            NotifyOfPropertyChange(() => DeviceAddress);
        }
    }

    public string? Status
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

    public void Start()
    {
        if (DeviceAddress == null) return;
        var address = DeviceAddress;
        if (!address.StartsWith(RtspPrefix) && !address.StartsWith(HttpPrefix))
            address = RtspPrefix + address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri deviceUri))
        {
            MessageBox.Show("Invalid device address");
            return;
        }

        var credential = new NetworkCredential(Login, Password);
        var connectionParameters = !string.IsNullOrEmpty(deviceUri.UserInfo)
            ? new ConnectionParameters(deviceUri)
            : new ConnectionParameters(deviceUri, credential);

        connectionParameters.RtpTransport = RtpTransportProtocol.TCP;
        connectionParameters.CancelTimeout = TimeSpan.FromSeconds(5);

        _model.Start(connectionParameters);
        _model.StatusChanged += OnModelStatusChanged;
    }

    public void Stop()
    {
        _model.Stop();
        _model.StatusChanged -= OnModelStatusChanged;
        Status = string.Empty;
    }

    private void OnModelStatusChanged(object? sender, string s)
    {
        // UI 업데이트용
        Status = s;
    }
}