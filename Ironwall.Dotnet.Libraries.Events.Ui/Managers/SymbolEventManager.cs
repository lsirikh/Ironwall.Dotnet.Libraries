using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/28/2025 5:38:46 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SymbolEventManager : IDisposable
{
    // 개별 마커: (Device.Id, DeviceType) → GMapPidsMarker/PidsSymbolModel
    // 복합 키를 사용하여 같은 ID라도 DeviceType이 다르면 별도 등록
    private readonly Dictionary<(int Id, EnumDeviceType Type), DeviceSymbolLookupModel> _deviceSymbolLookup;

    // 그룹 마커: DeviceGroup → GMapPidsGroupMarker/PidsGroupSymbolModel
    private readonly Dictionary<int, DeviceSymbolLookupModel> _groupSymbolLookup;

    private readonly IEventAggregator _ea;
    private readonly ILogService _log;
    private readonly EventSetupModel _eventSetupModel;

    public SymbolEventManager(IEventAggregator eventAggregator,
                             ILogService log,
                             EventSetupModel eventSetupModel)
    {
        _ea = eventAggregator;
        _log = log;
        _eventSetupModel = eventSetupModel;

        _deviceSymbolLookup = new Dictionary<(int, EnumDeviceType), DeviceSymbolLookupModel>();
        _groupSymbolLookup = new Dictionary<int, DeviceSymbolLookupModel>();
    }

    // 센서 장비-심볼 매핑 등록 (개별 마커용)
    // 복합 키 (Id, DeviceType)를 사용하여 같은 ID라도 타입이 다르면 별도 등록
    public void RegisterDeviceSymbol(IBaseDeviceModel deviceModel, IPidsEventCapable symbolModel)
    {
        var lookup = new DeviceSymbolLookupModel(_log, _ea, _eventSetupModel)
        {
            Id = deviceModel.Id,
            DeviceModel = deviceModel,
            SymbolModel = symbolModel
        };

        var key = (deviceModel.Id, deviceModel.DeviceType);
        _deviceSymbolLookup[key] = lookup;
        _log?.Info($"개별 심볼 등록: Device({deviceModel.Id}, {deviceModel.DeviceType}) → {symbolModel.GetType().Name}");
    }

    // 그룹 심볼 매핑 등록 (그룹 마커용)
    public void RegisterGroupSymbol(int deviceGroup, IBaseDeviceModel deviceModel, IPidsEventCapable symbolModel)
    {
        var lookup = new DeviceSymbolLookupModel(_log, _ea, _eventSetupModel)
        {
            Id = deviceGroup,
            DeviceModel = deviceModel,
            SymbolModel = symbolModel
        };

        _groupSymbolLookup[deviceGroup] = lookup;
    }

    // 센서 이벤트 처리 (deviceId + deviceType: 개별 마커, deviceGroup: 그룹 마커)
    public void ProcessDeviceEvent(int deviceId, EnumDeviceType deviceType, int deviceGroup, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
    {
        // 1. 개별 심볼 처리 - 복합 키 (Id, DeviceType) 사용
        var key = (deviceId, deviceType);
        if (_deviceSymbolLookup.TryGetValue(key, out var deviceLookup))
        {
            deviceLookup.ProcessEvent(eventType, severity);
            _log?.Info($"센서 이벤트 처리: Device({deviceId}, {deviceType}) -> {eventType}");
        }
        else
        {
            _log?.Warning($"매핑되지 않은 장비: Device({deviceId}, {deviceType})");
        }

        // 2. 그룹 심볼 처리 (Intrusion 이벤트만)
        if (ShouldProcessGroupSymbol(eventType) && _groupSymbolLookup.TryGetValue(deviceGroup, out var groupLookup))
        {
            groupLookup.ProcessEvent(eventType, severity);
            _log?.Info($"그룹 이벤트 처리: DeviceGroup({deviceGroup}) -> {eventType}");
        }
    }

    // 컨트롤러 이벤트 처리 (복합 키 사용)
    public void ProcessControllerEvent(int controllerId, int deviceGroup, EnumDeviceType deviceType, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
    {
        // 1. 개별 심볼 처리 - 복합 키 (Id, DeviceType) 사용
        // 컨트롤러는 DeviceType.Controller로 조회
        var key = (controllerId, EnumDeviceType.Controller);
        if (_deviceSymbolLookup.TryGetValue(key, out var deviceLookup))
        {
            deviceLookup.ProcessEvent(eventType, severity);
            _log?.Info($"컨트롤러 이벤트 처리: Controller({controllerId}) -> {eventType}");
        }
        else
        {
            _log?.Warning($"매핑되지 않은 컨트롤러: Controller({controllerId})");
        }

        // 2. 그룹 심볼 처리 (Fence 타입만)
        if (IsFenceType(deviceType) && _groupSymbolLookup.TryGetValue(deviceGroup, out var groupLookup))
        {
            groupLookup.ProcessEvent(eventType, severity);
            _log?.Info($"그룹 이벤트 처리 (Fence): DeviceGroup({deviceGroup}) -> {eventType}");
        }
    }

    public void ProcessEventReport(int deviceId, EnumDeviceType deviceType, int deviceGroup)
    {
        // 1. 개별 심볼 복원 - 복합 키 (Id, DeviceType) 사용
        var key = (deviceId, deviceType);
        if (_deviceSymbolLookup.TryGetValue(key, out var deviceLookup))
        {
            deviceLookup.ProcessEventReport();
            _log?.Info($"조치보고 처리: Device({deviceId}, {deviceType})");
        }
        else
        {
            _log?.Warning($"조치보고 실패 - 매핑되지 않은 장비: Device({deviceId}, {deviceType})");
        }

        // 2. 그룹 심볼 복원
        if (_groupSymbolLookup.TryGetValue(deviceGroup, out var groupLookup))
        {
            groupLookup.ProcessEventReport();
            _log?.Info($"조치보고 처리 (그룹): DeviceGroup({deviceGroup})");
        }
    }

    /// <summary>
    /// 카메라 PTZ 데이터로 FOV 업데이트
    /// </summary>
    /// <param name="cameraId">카메라 장비 ID</param>
    /// <param name="pan">Pan 각도 (0.0 ~ 360.0)</param>
    /// <param name="tilt">Tilt 각도</param>
    /// <param name="zoom">줌 백분율 (100 = 1x)</param>
    public void ProcessCameraPtz(int cameraId, float pan, float tilt, float zoom)
    {
        // 카메라는 항상 IpCamera 타입으로 조회
        var key = (cameraId, EnumDeviceType.IpCamera);
        if (_deviceSymbolLookup.TryGetValue(key, out var lookup))
        {
            lookup.ProcessPtz(pan, tilt, zoom);
            _log?.Info($"PTZ → FOV 업데이트: Camera({cameraId}), Pan={pan}, Tilt={tilt}, Zoom={zoom}");
        }
        else
        {
            _log?.Warning($"PTZ 업데이트 실패 - 매핑되지 않은 카메라: Camera({cameraId})");
        }
    }

    public void Dispose()
    {
        _deviceSymbolLookup.Clear();
        _groupSymbolLookup.Clear();
    }

    /// <summary>
    /// 그룹 심볼 처리 대상 이벤트인지 확인 (Intrusion 이벤트만)
    /// </summary>
    private bool ShouldProcessGroupSymbol(EnumEventType eventType)
    {
        return eventType == EnumEventType.Intrusion;
    }

    /// <summary>
    /// Fence 타입 장비인지 확인 (Fence, Underground)
    /// </summary>
    private bool IsFenceType(EnumDeviceType deviceType)
    {
        return deviceType == EnumDeviceType.Fence || deviceType == EnumDeviceType.Underground;
    }

    // 테스트용 접근자 - 복합 키 (Id, DeviceType) 사용
    internal bool HasDeviceSymbol(int deviceId, EnumDeviceType deviceType) =>
        _deviceSymbolLookup.ContainsKey((deviceId, deviceType));
    internal bool HasGroupSymbol(int deviceGroup) => _groupSymbolLookup.ContainsKey(deviceGroup);
    internal DeviceSymbolLookupModel? GetDeviceSymbol(int deviceId, EnumDeviceType deviceType) =>
        _deviceSymbolLookup.TryGetValue((deviceId, deviceType), out var lookup) ? lookup : null;
    internal DeviceSymbolLookupModel? GetGroupSymbol(int deviceGroup) =>
        _groupSymbolLookup.TryGetValue(deviceGroup, out var lookup) ? lookup : null;
}