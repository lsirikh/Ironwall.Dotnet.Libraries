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
    private string? _contextId;
    private RtspConnectionInfo? _connectionInfo;
    private bool _isActive;
    private int _gridRow;
    private int _gridColumn;

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

    public StreamingOptions? StreamingOptions { get; set; }
    public bool AutoPlay { get; set; } = false;
    public bool ShowControls { get; set; } = true;
    private int _slotIndex = -1;
}