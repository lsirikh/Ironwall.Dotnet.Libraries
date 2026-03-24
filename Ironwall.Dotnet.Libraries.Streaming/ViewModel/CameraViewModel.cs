using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Streaming.ViewModel;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/25/2025 5:14:03 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraViewModel : PropertyChangedBase
{

    public CameraViewModel()
    {
    }

    public CameraViewModel(ICameraModel model)
    {
        Model = model;
    }

    public CameraViewModel(ICameraModel model, string rowId) : this(model)
    {
        _contextId = $"{rowId}_{Guid}";
    }

    #region Properties
    public string Guid
    {
        get { return _model.Guid; }
        set { _model.Guid = value; NotifyOfPropertyChange(nameof(Guid)); }
    }

    public string? DisplayName
    {
        get { return _model.Title; }
        set { _model.Title = value; NotifyOfPropertyChange(nameof(DisplayName)); }
    }

    public RtspConnectionInfo ConnectionInfo
    {
        get { return _model.ConnectionInfo; }
        set { _model.ConnectionInfo = value; NotifyOfPropertyChange(nameof(ConnectionInfo)); }
    }

    /// <summary>
    /// 스트리밍 옵션
    /// </summary>
    public StreamingOptions? StreamingOptions
    {
        get { return _model.StreamingOptions; }
        set { _model.StreamingOptions = value; NotifyOfPropertyChange(nameof(StreamingOptions)); }
    }

    /// <summary>
    /// 재생 상태
    /// </summary>
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        set => Set(ref _playbackState, value);
    }

    /// <summary>
    /// 상태 메시지
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    /// <summary>
    /// 자동 재생 여부
    /// </summary>
    public bool AutoPlay
    {
        get { return _model.AutoPlay; }
        set { _model.AutoPlay = value; NotifyOfPropertyChange(nameof(AutoPlay)); }
    }


    /// <summary>
    /// 컨트롤 표시 여부
    /// </summary>
    public bool ShowControls
    {
        get { return _model.ShowControls; }
        set { _model.ShowControls = value; NotifyOfPropertyChange(nameof(ShowControls)); }
    }

    public DateTime StartTime
    {
        get => _startTime;
        set => Set(ref _startTime, value);
    }
    
    public string ContextId => _contextId ?? _model.Guid;

    /// <summary>
    /// Wrapper Model
    /// </summary>
    public ICameraModel Model
    {
        get => _model;
        set => Set(ref _model, value);
    }

    #endregion

    #region Attribute(Fields)
    private ICameraModel _model = new CameraModel();
    private PlaybackState _playbackState = PlaybackState.None;
    private string _statusMessage = "Ready";
    private DateTime _startTime;
    private string? _contextId;
    #endregion
}