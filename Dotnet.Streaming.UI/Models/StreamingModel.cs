using Dotnet.Streaming.UI.RawFramesReceiving;
using RtspClientSharp;
using System;

namespace Dotnet.Streaming.UI.Models
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 5/3/2024 4:17:26 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class StreamingModel : IStreamingModel
    {
        #region - Ctors -
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        public void Start(ConnectionParameters connectionParameters)
        {
            if (_rawFramesSource != null)
                return;

            connectionParameters.RtpTransport = RtpTransportProtocol.TCP;
            _rawFramesSource = new RawFramesSource(connectionParameters);
            _rawFramesSource.ConnectionStatusChanged += ConnectionStatusChanged;

            _realtimeVideoSource.SetRawFramesSource(_rawFramesSource);
            _realtimeAudioSource.SetRawFramesSource(_rawFramesSource);

            _rawFramesSource.Start();
        }

        public void Stop()
        {
            if (_rawFramesSource == null) return;

            _rawFramesSource.Stop();
            _realtimeVideoSource.SetRawFramesSource(null);
            _rawFramesSource = null;
        }

        private void ConnectionStatusChanged(object? sender, string s)
        {
            StatusChanged?.Invoke(this, s);
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public IVideoSource VideoSource => _realtimeVideoSource;
        #endregion
        #region - Attributes -
        private readonly RealtimeVideoSource _realtimeVideoSource = new RealtimeVideoSource();
        private readonly RealtimeAudioSource _realtimeAudioSource = new RealtimeAudioSource();

        private IRawFramesSource? _rawFramesSource;

        public event EventHandler<string>? StatusChanged;
        #endregion
    }
}