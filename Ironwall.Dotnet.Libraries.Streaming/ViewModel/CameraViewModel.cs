using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;

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
    #region Attribute(Fields)
    private string? _contextId;
    private RtspConnectionInfo? _connectionInfo;
    private bool _isActive;
    private int _gridRow;
    private int _gridColumn;
    private int _slotIndex = -1;
    private bool _isSelected;
    private PlaybackState _playbackState = PlaybackState.None;
    private string _statusMessage = "Ready";
    #endregion

    #region Methods
    /// <summary>
    /// 연결 상태 문자열
    /// </summary>
    public string ConnectionStatus
    {
        get
        {
            if (!IsActive) return "Inactive";
            return PlaybackState switch
            {
                PlaybackState.None => "Not Connected",
                PlaybackState.Connecting => "Connecting...",
                PlaybackState.Playing => "Playing",
                PlaybackState.Paused => "Paused",
                PlaybackState.Buffering => "Buffering...",
                PlaybackState.Error => "Error",
                PlaybackState.Reconnecting => "Reconnecting...",
                PlaybackState.Disconnected => "Disconnected",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// 카메라 정보 요약
    /// </summary>
    public override string ToString()
    {
        return $"{ContextId} [{GridPosition}] - {ConnectionStatus}";
    }

    /// <summary>
    /// 상태 초기화
    /// </summary>
    public void Reset()
    {
        IsActive = false;
        IsSelected = false;
        ConnectionInfo = null;
        PlaybackState = PlaybackState.None;
        StatusMessage = "Ready";
    }

    /// <summary>
    /// 연결 정보 업데이트
    /// </summary>
    public void UpdateConnection(RtspConnectionInfo info)
    {
        ConnectionInfo = info;
        IsActive = info != null && info.IsValid();
        NotifyOfPropertyChange(nameof(ConnectionStatus));
    }

    /// <summary>
    /// 상태 업데이트
    /// </summary>
    public void UpdateState(PlaybackState state, string message = null)
    {
        PlaybackState = state;
        if (!string.IsNullOrEmpty(message))
        {
            StatusMessage = message;
        }
        NotifyOfPropertyChange(nameof(ConnectionStatus));
    }
    #endregion

    #region Properties
    public string? ContextId
    {
        get => _contextId;
        set => Set(ref _contextId, value);
    }

    public RtspConnectionInfo? ConnectionInfo
    {
        get => _connectionInfo;
        set => Set(ref _connectionInfo, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    public int GridRow
    {
        get => _gridRow;
        set => Set(ref _gridRow, value);
    }

    public int GridColumn
    {
        get => _gridColumn;
        set => Set(ref _gridColumn, value);
    }

    public int SlotIndex
    {
        get => _slotIndex;
        set => Set(ref _slotIndex, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
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
    /// 스트리밍 옵션
    /// </summary>
    public StreamingOptions? StreamingOptions { get; set; }

    /// <summary>
    /// 자동 재생 여부
    /// </summary>
    public bool AutoPlay { get; set; } = false;

    /// <summary>
    /// 컨트롤 표시 여부
    /// </summary>
    public bool ShowControls { get; set; } = true;

    /// <summary>
    /// 그리드 위치 문자열
    /// </summary>
    public string GridPosition => $"Row {GridRow + 1}, Col {GridColumn + 1}";
    #endregion
}