using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Collections.Concurrent;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/28/2025 5:38:46 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SymbolEventManager : ISymbolEventManager, IDisposable,
    IHandle<AllDevicesLoadedMessage>,
    IHandle<DeviceStatusChangedMessage>
{
    // 개별 마커: (Device.Id, DeviceType) → GMapPidsMarker/PidsSymbolModel
    // 복합 키를 사용하여 같은 ID라도 DeviceType이 다르면 별도 등록
    private readonly ConcurrentDictionary<(int Id, EnumDeviceType Type), DeviceSymbolLookupModel> _deviceSymbolLookup;

    // 그룹 마커: DeviceGroup → GMapPidsGroupMarker/PidsGroupSymbolModel
    private readonly ConcurrentDictionary<int, DeviceSymbolLookupModel> _groupSymbolLookup;

    // 보조 인덱스: DeviceId → DeviceSymbolLookupModel (DeviceType 불일치 fallback용 O(1) 검색)
    private readonly ConcurrentDictionary<int, DeviceSymbolLookupModel> _deviceLookupById;

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

        _deviceSymbolLookup = new ConcurrentDictionary<(int, EnumDeviceType), DeviceSymbolLookupModel>();
        _groupSymbolLookup = new ConcurrentDictionary<int, DeviceSymbolLookupModel>();
        _deviceLookupById = new ConcurrentDictionary<int, DeviceSymbolLookupModel>();
    }

    // 센서 장비-심볼 매핑 등록 (개별 마커용)
    // 복합 키 (Id, DeviceType)를 사용하여 같은 ID라도 타입이 다르면 별도 등록
    public void RegisterDeviceSymbol(IBaseDeviceModel deviceModel, IPidsEventCapable symbolModel)
    {
        var lookup = new DeviceSymbolLookupModel(_log)
        {
            Id = deviceModel.Id,
            DeviceModel = deviceModel,
            SymbolModel = symbolModel
        };

        var key = (deviceModel.Id, deviceModel.DeviceType);
        _deviceSymbolLookup[key] = lookup;
        _deviceLookupById[deviceModel.Id] = lookup;
        //_log?.Info($"개별 심볼 등록: Device({deviceModel.Id}, {deviceModel.DeviceType}) → {symbolModel.GetType().Name}");

        // FR-08c: 등록 즉시 Device.Status → Symbol.OperationState 동기화
        lookup.SyncFromDevice(deviceModel.Status);

        // FR-13 ④: 장비정보(API geolocation.heading) → 심볼 BaseBearing 메모리 반영(로컬 DB 미저장).
        // SaveMarker 호출하지 않음 — SoT=서버, 설치방향 변경은 서버 장비 API로. heading=null이면 미변경.
        if (deviceModel.Heading.HasValue && symbolModel is IPidsSymbolModel pids)
        {
            pids.BaseBearing = deviceModel.Heading.Value;
            pids.DetectionBearing = pids.BaseBearing;   // 초기 FOV 방향 = 설치방향
            _log?.Info($"[SymbolEventManager] BaseBearing 메모리 반영: Device({deviceModel.Id}) heading={deviceModel.Heading.Value:F1}°");
            symbolModel.SetUpdate();   // BaseBearing/DetectionBearing 변경 → FOV 부채꼴 재렌더링 트리거(UI notify, DB 미저장)
        }
    }

    // 그룹 심볼 매핑 등록 (그룹 마커용)
    public void RegisterGroupSymbol(int deviceGroup, IBaseDeviceModel deviceModel, IPidsEventCapable symbolModel)
    {
        var lookup = new DeviceSymbolLookupModel(_log)
        {
            Id = deviceGroup,
            DeviceModel = deviceModel,
            SymbolModel = symbolModel
        };

        _groupSymbolLookup[deviceGroup] = lookup;

        //_log?.Info($"그룹 심볼 등록: Group({deviceGroup}) → {symbolModel.GetType().Name} ");
    }

    // 센서 이벤트 처리 (deviceId + deviceType: 개별 마커, deviceGroups: 그룹 마커)
    public void ProcessDeviceEvent(int deviceId, EnumDeviceType deviceType, List<int>? deviceGroups, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
    {
        // 1. 개별 심볼 처리 - 복합 키 (Id, DeviceType) 사용
        var key = (deviceId, deviceType);
        if (_deviceSymbolLookup.TryGetValue(key, out var deviceLookup))
        {
            deviceLookup.ProcessEvent(eventType, severity);
            //_log?.Info($"센서 이벤트 처리: Device({deviceId}, {deviceType}) -> {eventType}");
        }
        else
        {
            _log?.Warning($"매핑되지 않은 장비: Device({deviceId}, {deviceType})");
        }

        // 2. 그룹 심볼 처리 (Intrusion 이벤트만) - 복수 그룹 지원
        if (ShouldProcessGroupSymbol(eventType) && deviceGroups != null)
        {
            foreach (var groupId in deviceGroups)
            {
                if (_groupSymbolLookup.TryGetValue(groupId, out var groupLookup))
                {
                    groupLookup.ProcessEvent(eventType, severity);
                    _log?.Info($"그룹 이벤트 처리: DeviceGroup({groupId}) -> {eventType}");
                }
            }
        }
    }

    // 탐지 이벤트 처리 (deviceId로 검색 — NATS DETECTION 메시지용)
    // NOTE: 심볼 Detecting은 EventQueueManager 전이 이벤트로 일원화됨
    //   - 개별 심볼: OnDeviceFirstEvent → SetDeviceDetecting()
    //   - 그룹 심볼: OnGroupFirstEvent → SetGroupDetecting()
    public void ProcessDetectionById(int deviceId, List<int>? deviceGroups, EnumEventType eventType)
    {
        _log?.Info($"ProcessDetectionById 호출됨 (no-op): Device({deviceId}), EventType={eventType} — 심볼 상태는 EventQueueManager 전이로 처리");
    }

    // 컨트롤러 이벤트 처리 (복합 키 사용)
    public void ProcessControllerEvent(int controllerId, List<int>? deviceGroups, EnumDeviceType deviceType, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
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

        // 2. 그룹 심볼 처리 (Fence 타입만) - 복수 그룹 지원
        if (IsFenceType(deviceType) && deviceGroups != null)
        {
            foreach (var groupId in deviceGroups)
            {
                if (_groupSymbolLookup.TryGetValue(groupId, out var groupLookup))
                {
                    groupLookup.ProcessEvent(eventType, severity);
                    _log?.Info($"그룹 이벤트 처리 (Fence): DeviceGroup({groupId}) -> {eventType}");
                }
            }
        }
    }

    // NOTE: 심볼 복원은 EventQueueManager 전이 이벤트로 일원화됨
    //   - 개별 심볼: OnDeviceEmpty → RestoreDeviceSymbol()
    //   - 그룹 심볼: OnGroupEmpty → RestoreGroupSymbol()
    public void ProcessEventReport(int deviceId, EnumDeviceType deviceType, List<int>? deviceGroups)
    {
        _log?.Info($"ProcessEventReport 호출됨 (no-op): Device({deviceId}, {deviceType}) — 심볼 복원은 EventQueueManager Dequeue 전이로 처리");
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

    /// <summary>
    /// NatsSync: 단일 Device Status 변경 → Symbol OperationState 갱신 (FR-09)
    /// </summary>
    public void SyncDeviceStatus(int deviceId, EnumDeviceType deviceType, EnumDeviceStatus status)
    {
        // (FR-B2) 재등록 desync 완화 — 복합 키(Id,Type) 실패 시 Id 단독 보조 인덱스로 폴백(TryResolveDevice).
        //   장비 재등록으로 DeviceType이 바뀐 경우에도 Id로 심볼을 찾아 OperationState 동기화를 지속한다.
        //   (Id 자체가 바뀐 재등록은 lookup 미스 → 아래 진단 경고. 심볼 재바인딩은 등록 경로에서 처리.)
        if (TryResolveDevice(deviceId, deviceType, out var lookup))
        {
            lookup.SyncFromDevice(status);
            _log?.Info($"Device 상태 동기화: Device({deviceId}, {deviceType}) → {status}");
        }
        else
        {
            _log?.Warning($"[SYNC_DEVICE] 상태 동기화 실패 - 미등록 Device({deviceId}, {deviceType}) — 재등록(Id 변경)/미배치 심볼 가능성");
        }
    }

    /// <summary>
    /// AllDevicesLoadedMessage 수신 시 등록된 전체 Symbol 일괄 동기화 (FR-08b)
    /// </summary>
    public Task HandleAsync(AllDevicesLoadedMessage message, CancellationToken cancellationToken)
    {
        foreach (var lookup in _deviceSymbolLookup.Values)
        {
            if (lookup.DeviceModel != null)
                lookup.SyncFromDevice(lookup.DeviceModel.Status);
        }
        _log?.Info($"전체 Symbol OperationState 동기화 완료: {_deviceSymbolLookup.Count}개");
        return Task.CompletedTask;
    }

    /// <summary>
    /// DeviceStatusChangedMessage 수신 시 해당 Symbol 갱신 (FR-09)
    /// </summary>
    public Task HandleAsync(DeviceStatusChangedMessage message, CancellationToken cancellationToken)
    {
        _log?.Info($"[SYNC→Symbol] DeviceStatusChangedMessage 수신: id={message.DeviceId}, type={message.DeviceType}, status={message.Status} (등록 심볼 수={_deviceSymbolLookup.Count})");
        SyncDeviceStatus(message.DeviceId, message.DeviceType, message.Status);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 개별 심볼 Detecting 상태 설정 — EventQueueManager의 OnDeviceFirstEvent에서 호출
    /// </summary>
    public void SetDeviceDetecting(int deviceId, EnumDeviceType deviceType, EnumEventType eventType)
    {
        if (!TryResolveDevice(deviceId, deviceType, out var deviceLookup))
            return;
        deviceLookup.ProcessEvent(eventType, EnumSeverityLevel.WARNING);
        _log?.Info($"개별 심볼 Detecting 설정: Device({deviceId}, {deviceType}) -> {eventType}");
    }

    /// <summary>
    /// 개별 심볼 Normal 복원 — EventQueueManager의 OnDeviceEmpty에서 호출
    /// </summary>
    public void RestoreDeviceSymbol(int deviceId, EnumDeviceType deviceType)
    {
        if (!TryResolveDevice(deviceId, deviceType, out var deviceLookup)) return;
        deviceLookup.ProcessEventReport();
        _log?.Info($"개별 심볼 복원: Device({deviceId}, {deviceType}) → Normal");
    }

    /// <summary>
    /// 그룹 심볼 Detecting 상태 설정 — EventQueueManager의 OnGroupFirstEvent에서 호출
    /// </summary>
    public void SetGroupDetecting(int groupId, EnumEventType eventType)
    {
        if (_groupSymbolLookup.TryGetValue(groupId, out var groupLookup))
        {
            groupLookup.ProcessEvent(eventType, EnumSeverityLevel.WARNING);
            _log?.Info($"그룹 Detecting 설정: DeviceGroup({groupId}) -> {eventType}");
        }
    }

    /// <summary>
    /// 그룹 심볼 Normal 복원 — EventQueueManager의 OnGroupEmpty에서 호출
    /// </summary>
    public void RestoreGroupSymbol(int groupId)
    {
        if (_groupSymbolLookup.TryGetValue(groupId, out var groupLookup))
        {
            groupLookup.ProcessEventReport();
            _log?.Info($"그룹 심볼 복원: DeviceGroup({groupId}) → Normal");
        }
    }

    /// <summary>
    /// 개별 디바이스 복합 상태 전이 처리 — EventQueueManager의 OnDeviceStateChanged에서 호출
    /// </summary>
    public void HandleDeviceStateChanged(int deviceId, EnumDeviceType deviceType, EnumCompositeEventStatus prev, EnumCompositeEventStatus next)
    {
        if (!TryResolveDevice(deviceId, deviceType, out var deviceLookup))
            return;
        deviceLookup.ApplyCompositeStatus(next);
        _log?.Info($"개별 심볼 상태 전이: Device({deviceId},{deviceType}) {prev}→{next}");
    }

    /// <summary>
    /// 그룹 복합 상태 전이 처리 — EventQueueManager의 OnGroupStateChanged에서 호출
    /// </summary>
    public void HandleGroupStateChanged(int groupId, EnumCompositeEventStatus prev, EnumCompositeEventStatus next)
    {
        switch (next)
        {
            case EnumCompositeEventStatus.Normal:
                RestoreGroupSymbol(groupId);
                break;
            case EnumCompositeEventStatus.Detecting:
            case EnumCompositeEventStatus.Faulted:
            case EnumCompositeEventStatus.FaultedDetecting:
                SetGroupCompositeStatus(groupId, next);
                break;
            case EnumCompositeEventStatus.Connection:
            default:
                break;
        }
    }

    public void Dispose()
    {
        _ea.Unsubscribe(this);
        _deviceSymbolLookup.Clear();
        _groupSymbolLookup.Clear();
        _deviceLookupById.Clear();
    }

    /// <summary>
    /// 복합 키(Id,Type) → 실패 시 보조 인덱스(Id 단독) 순서로 O(1) 검색.
    /// </summary>
    private bool TryResolveDevice(int deviceId, EnumDeviceType deviceType,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeviceSymbolLookupModel? lookup)
    {
        if (_deviceSymbolLookup.TryGetValue((deviceId, deviceType), out lookup)) return true;
        if (_deviceLookupById.TryGetValue(deviceId, out lookup))
        {
            _log?.Warning($"TryResolveDevice: DeviceType 불일치 Device({deviceId},{deviceType}) → ID 재검색으로 대체");
            return true;
        }
        lookup = null;
        return false;
    }

    /// <summary>
    /// 그룹 심볼 처리 대상 이벤트인지 확인 (Intrusion + Fault 이벤트)
    /// </summary>
    private bool ShouldProcessGroupSymbol(EnumEventType eventType)
    {
        return eventType == EnumEventType.Intrusion || eventType == EnumEventType.Fault;
    }

    /// <summary>
    /// Fence 타입 장비인지 확인 (Fence, Underground)
    /// </summary>
    private bool IsFenceType(EnumDeviceType deviceType)
    {
        return deviceType == EnumDeviceType.Fence || deviceType == EnumDeviceType.Underground;
    }

    private void SetGroupCompositeStatus(int groupId, EnumCompositeEventStatus status)
    {
        if (_groupSymbolLookup.TryGetValue(groupId, out var groupLookup))
        {
            groupLookup.ApplyCompositeStatus(status);
            _log?.Info($"그룹 복합 상태 설정: DeviceGroup({groupId}) → {status}");
        }
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