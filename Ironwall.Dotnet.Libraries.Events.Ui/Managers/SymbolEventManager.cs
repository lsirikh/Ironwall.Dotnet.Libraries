using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
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
public class SymbolEventManager : ISymbolEventManager, IDisposable
{
    // 개별 마커: (Device.Id, DeviceType) → GMapPidsMarker/PidsSymbolModel
    // 복합 키를 사용하여 같은 ID라도 DeviceType이 다르면 별도 등록
    private readonly ConcurrentDictionary<(int Id, EnumDeviceType Type), DeviceSymbolLookupModel> _deviceSymbolLookup;

    // 그룹 마커: DeviceGroup → GMapPidsGroupMarker/PidsGroupSymbolModel
    private readonly ConcurrentDictionary<int, DeviceSymbolLookupModel> _groupSymbolLookup;

    // 보조 인덱스: DeviceId → DeviceSymbolLookupModel (DeviceType 불일치 fallback용 O(1) 검색)
    private readonly ConcurrentDictionary<int, DeviceSymbolLookupModel> _deviceLookupById;

    // 미등록 장비 경고를 이미 한 (Id, 종류) — 장비당 한 번만 알린다(WarnUnresolvedOnce). 다시 등록되면 지운다.
    private readonly ConcurrentDictionary<(int Id, EnumDeviceType Type), byte> _unresolvedWarned = new();

    private readonly IEventAggregator _ea;
    private readonly ILogService _log;
    private readonly EventSetupModel _eventSetupModel;
    // FR-03 재계산 복원용 SSOT(선택 주입) — 미주입(테스트/DB모드)이면 Refresh* 는 no-op. EQM→SEM 참조 없음이라 순환 없음.
    private readonly IEventQueueManager? _eventQueueManager;

    public SymbolEventManager(IEventAggregator eventAggregator,
                             ILogService log,
                             EventSetupModel eventSetupModel,
                             IEventQueueManager? eventQueueManager = null)
    {
        _ea = eventAggregator;
        _log = log;
        _eventSetupModel = eventSetupModel;
        _eventQueueManager = eventQueueManager;

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
        _unresolvedWarned.TryRemove(key, out _);   // 다시 등록됐다 — 다음 미등록은 다시 알린다
        //_log?.Info($"개별 심볼 등록: Device({deviceModel.Id}, {deviceModel.DeviceType}) → {symbolModel.GetType().Name}");

        // FR-08c: 등록 즉시 Device.Status → Symbol.OperationState 동기화
        lookup.SyncFromDevice(deviceModel.Status);

        // (stale 상태 자가치유) 심볼 EventStatus는 PidsSymbols.EventStatus 컬럼에 영속된다.
        //   과거 장애로 Blackout/Fault가 저장된 뒤 복구가 OperationState만 갱신하면 그 값이 영구히 남아
        //   앱을 재시작해도 심볼이 검은색/장애색으로 표시된다(실측: 제어기2(1352)).
        //   장비가 정상(ACTIVATED)으로 로드되는데 저장된 이벤트상태가 남아 있으면 EQM 기준으로 재계산한다.
        //   부팅 시 EQM은 비어 있으므로 Normal로 정리되고, 서버가 여전히 장애면 Status=ERROR라 여기 걸리지 않는다.
        if (deviceModel.Status == EnumDeviceStatus.ACTIVATED)
            RefreshDeviceSymbol(deviceModel.Id, deviceModel.DeviceType);

        // FR-13 부팅 초기화: 함체는 장비정보 door_status 로 개폐 형태를 시작한다(통문 gate_status 는 클라 장비모델 미수용 → Unknown=닫힘 표시).
        if (deviceModel is IEnclosureDeviceModel enclosure && symbolModel is IPidsSymbolModel doorSymbol)
        {
            var initial = DoorStateMachine.FromServer(enclosure.DoorStatus);
            if (initial != EnumDoorState.Unknown) lookup.ApplyDoorState(initial);
        }

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

    /// <summary>
    /// 장비 심볼 등록 해제 — 장비가 지워졌을 때(지도 <c>SymbolLifecycleCoordinator.UnregisterDevice</c> 가 부른다, WP-1 ㉒).
    /// 심볼 색을 Normal 로 돌리고(시각 상태 정리) 조회표 · Id 보조 색인에서 뺀다 — 이후 이 키로 오는 늦은 큐 전이 · 문 상태는
    /// 어떤 심볼도 칠하지 않는다(장비당 한 번 경고). 종전엔 해제가 없어 지도가 '흡수용 빈 모델'을 같은 키로 등록해 끊었다.
    /// </summary>
    /// <remarks>인터페이스(<see cref="ISymbolEventManager"/>)에는 넣지 않는다 — 멤버를 늘리면 목 · 페이크가 전부 깨진다(<see cref="UnregisterGroupSymbol"/> 와 같은 규칙).</remarks>
    /// <returns>등록돼 있어 내렸으면 true.</returns>
    public bool UnregisterDeviceSymbol(int deviceId, EnumDeviceType deviceType)
    {
        if (!_deviceSymbolLookup.TryRemove((deviceId, deviceType), out var removed)) return false;

        // Id 보조 색인이 이 조회를 가리키면 — 같은 Id 의 다른 종류가 남았으면 그것으로 잇고, 없으면 뺀다.
        if (_deviceLookupById.TryGetValue(deviceId, out var byId) && ReferenceEquals(byId, removed))
        {
            var sibling = _deviceSymbolLookup.FirstOrDefault(kv => kv.Key.Id == deviceId).Value;
            if (sibling is not null) _deviceLookupById[deviceId] = sibling;
            else _deviceLookupById.TryRemove(deviceId, out _);
        }

        removed.ApplyCompositeStatus(EnumCompositeEventStatus.Normal);   // 지워진 장비의 마지막 색이 굳지 않게
        _log?.Info($"장비 심볼 해제: Device({deviceId},{deviceType}) — 색 Normal 복원 · 조회표에서 분리");
        return true;
    }

    /// <summary>
    /// 그룹 심볼(구역선) 등록 해제 — 그룹의 마지막 장비가 빠졌거나 그 그룹을 가리키는 구역선이 없어졌을 때.
    /// </summary>
    /// <remarks>
    /// <para>해제한 뒤에는 누구도 이 선을 복원하지 않으므로(큐의 복원 신호는 미등록 no-op) 색을 <b>Normal 로 되돌린다</b>.
    /// 단 같은 선이 다른 그룹으로 아직 등록돼 있으면(속성창에서 연결 그룹을 바꾼 직후) 그 그룹의 색이므로 건드리지 않는다.</para>
    /// <para>인터페이스(<see cref="ISymbolEventManager"/>)에는 넣지 않는다 — 멤버를 늘리면 목 · 페이크가 전부 깨진다.</para>
    /// </remarks>
    /// <returns>등록돼 있어 내렸으면 true.</returns>
    public bool UnregisterGroupSymbol(int deviceGroup)
    {
        if (!_groupSymbolLookup.TryRemove(deviceGroup, out var removed)) return false;

        var stillLinked = removed.SymbolModel is not null
            && _groupSymbolLookup.Values.Any(l => ReferenceEquals(l.SymbolModel, removed.SymbolModel));
        if (!stillLinked) removed.ApplyCompositeStatus(EnumCompositeEventStatus.Normal);

        _log?.Info($"그룹 심볼 해제: DeviceGroup({deviceGroup}) (선 색 {(stillLinked ? "유지 — 다른 그룹으로 등록됨" : "Normal 복원")})");
        return true;
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

            // (stale 상태 고착 방지) SyncFromDevice는 OperationState만 갱신한다.
            //   장비가 정상 복귀(ACTIVATED)했는데 EventStatus(Blackout/Fault)가 남으면
            //   그 값이 심볼 DB(PidsSymbols.EventStatus)에 영속되어 재시작 후에도 검은색이 유지된다.
            //   (실측: 제어기2(1352) — ACTION_REPORT가 '카드종결 no-op(부재)'로 dequeue되지 않은 뒤
            //    SYNC_DEVICE ACTIVATED가 와도 EventStatus=Blackout 잔존 → DB 저장 → 재기동 시 재현)
            //   → EQM 실제 상태로 재계산해 반영한다. 큐에 살아있는 이벤트가 있으면 그 상태가 유지되고,
            //     없으면 Normal로 복귀한다(권위=EventQueueManager).
            if (status == EnumDeviceStatus.ACTIVATED)
                RefreshDeviceSymbol(deviceId, deviceType);
        }
        else
        {
            _log?.Warning($"[SYNC_DEVICE] 상태 동기화 실패 - 미등록 Device({deviceId}, {deviceType}) — 재등록(Id 변경)/미배치 심볼 가능성");
        }
    }

    // (WP-1 ㉕) AllDevicesLoadedMessage · DeviceStatusChangedMessage 처리기는 뺐다 — 이 관리자를 이벤트 버스에 구독하는 곳이
    //   한 군데도 없어(발행도 DeviceStatusChangedMessage 는 0건) 시험에서만 불리던 죽은 길이었다. 상태 동기화의 정본은
    //   SYNC_DEVICE → SyncDeviceStatus, 등록 시 RegisterDeviceSymbol 의 즉시 동기화다.

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
        else
        {
            _log?.Warning($"그룹 심볼 미등록: DeviceGroup({groupId}) → Detecting 반영 no-op(LinkedDeviceGroup 미연결/미배치 가능성)");
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
        else
        {
            // FR-02 관측성: 미등록 그룹은 조용히 무산되던 것을 경고로 표면화(LinkedDeviceGroup=0/미배치 진단).
            _log?.Warning($"그룹 심볼 미등록: DeviceGroup({groupId}) — 복원 no-op(LinkedDeviceGroup 미연결/미배치 가능성)");
        }
    }

    /// <summary>
    /// 그룹 심볼을 EQM 실제 상태로 **재계산 복원**(FR-03). 조치보고/제어기 복구 시 사용.
    /// RestoreGroupSymbol(ProcessEventReport 휴리스틱)과 달리 **잔여 활성 이벤트를 반영**(맹목 Normal 금지 — 공존 센서 장애 색 보존).
    /// EQM 미주입(테스트/DB모드) 시 no-op.
    /// </summary>
    public void RefreshGroupSymbol(int groupId)
    {
        if (_eventQueueManager == null) return;
        var state = _eventQueueManager.GetGroupState(groupId);
        SetGroupCompositeStatus(groupId, state);   // 직접 세팅 — 재계산값 그대로(미등록이면 SetGroupCompositeStatus가 경고)
        _log?.Info($"그룹 심볼 재계산 복원: DeviceGroup({groupId}) → {state}");
    }

    /// <summary>개별 디바이스 심볼을 EQM 실제 상태로 재계산 반영(FR-03). EQM 미주입 시 no-op.</summary>
    public void RefreshDeviceSymbol(int deviceId, EnumDeviceType deviceType)
    {
        if (_eventQueueManager == null) return;
        if (!TryResolveDevice(deviceId, deviceType, out var lookup)) return;
        var state = _eventQueueManager.GetDeviceState(deviceId, deviceType);
        lookup.ApplyCompositeStatus(state);
        _log?.Info($"개별 심볼 재계산: Device({deviceId},{deviceType}) → {state}");
    }

    /// <summary>
    /// 개별 디바이스 복합 상태 전이 처리 — EventQueueManager의 OnDeviceStateChanged에서 호출
    /// </summary>
    public void HandleDeviceStateChanged(int deviceId, EnumDeviceType deviceType, EnumCompositeEventStatus prev, EnumCompositeEventStatus next)
    {
        if (!TryResolveDevice(deviceId, deviceType, out var deviceLookup))
        {
            WarnUnresolvedOnce(deviceId, deviceType, $"상태 전이 {prev}→{next}");
            return;
        }
        deviceLookup.ApplyCompositeStatus(next);
        _log?.Info($"개별 심볼 상태 전이: Device({deviceId},{deviceType}) {prev}→{next}");
    }

    /// <summary>
    /// 그룹 복합 상태 전이 처리 — EventQueueManager의 OnGroupStateChanged에서 호출
    /// </summary>
    public void SetDoorState(int deviceId, EnumDeviceType deviceType, EnumDoorState state)
    {
        if (!TryResolveDevice(deviceId, deviceType, out var lookup))
        {
            WarnUnresolvedOnce(deviceId, deviceType, $"개폐 {state}");
            return;
        }
        lookup.ApplyDoorState(state);
    }

    public void ApplyDoorEvent(int deviceId, EnumDeviceType deviceType, EnumEventType eventType)
    {
        if (!DoorStateMachine.IsContactEvent(eventType)) return;
        if (!TryResolveDevice(deviceId, deviceType, out var lookup))
        {
            WarnUnresolvedOnce(deviceId, deviceType, $"접점 {eventType}");
            return;
        }
        lookup.ApplyDoorEvent(eventType);
    }

    /// <summary>
    /// 미등록 장비로 온 전이 · 개폐를 <b>장비당 한 번</b> 경고한다(WP-1 ㉓) — 종전엔 조용히 버려 "지도에 안 뜬다" 를 진단할 길이 없었다.
    /// 한 번만인 까닭: 큐 전이는 이벤트마다 오므로 매번 쓰면 로그가 넘친다. 다시 등록되면 기억을 지운다.
    /// </summary>
    private void WarnUnresolvedOnce(int deviceId, EnumDeviceType deviceType, string what)
    {
        if (!_unresolvedWarned.TryAdd((deviceId, deviceType), 0)) return;
        _log?.Warning($"[심볼 미등록] Device({deviceId},{deviceType}) — {what} 반영 no-op(지도에 심볼 없음 · 삭제된 장비 · 미배치). 이 장비는 이후 다시 알리지 않습니다.");
    }

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
            case EnumCompositeEventStatus.Blackout:   // 제어기 무통신 → 검은색(GMap_Controller_Blackout)
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
        else
        {
            // FR-02 관측성: 미등록 그룹은 제어기 Blackout 등 상태가 조용히 유실되던 지점(근본원인 A) — 경고로 표면화.
            _log?.Warning($"그룹 심볼 미등록: DeviceGroup({groupId}) → {status} 반영 no-op(LinkedDeviceGroup 미연결/미배치 가능성)");
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