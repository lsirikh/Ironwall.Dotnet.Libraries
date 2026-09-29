using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/28/2025 5:40:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceSymbolLookupModel : BaseModel
{
    #region - Ctors -
    public DeviceSymbolLookupModel(ILogService log)
    {
        _log = log;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    // 이벤트 처리 메서드들

    /// <summary>
    /// 고빈도 경로(ProcessEvent, ProcessEventReport, UpdateFOV) 전용 dirty flag + Dispatcher 마샬링.
    /// 이미 큐잉된 콜백 있으면 추가 큐잉 금지 (coalescing). UI 스레드에서만 SetUpdate 발화.
    /// Application.Current null 또는 HasShutdownStarted 시 동기 fallback.
    /// </summary>
    private void MarshalUpdate()
    {
        if (Interlocked.Exchange(ref _isFlushPending, 1) == 1) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            Interlocked.Exchange(ref _isFlushPending, 0);
            SymbolModel?.SetUpdate();
            return;
        }

        dispatcher.InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _isFlushPending, 0);
            SymbolModel?.SetUpdate();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Device.Status → Symbol.OperationState 동기화
    /// </summary>
    public void SyncFromDevice(EnumDeviceStatus status)
    {
        if (SymbolModel == null)
        {
            _log?.Warning($"[SYNC→Symbol] SyncFromDevice: SymbolModel null (Id={Id})");
            return;
        }

        var prevState = SymbolModel.OperationState;

        // OperationState 동기화 — EventStatus는 CompositeStatus setter를 통해서만 설정 (SSOT)
        SymbolModel.OperationState = status switch
        {
            EnumDeviceStatus.ACTIVATED   => EnumOperationState.ACTIVATED,
            EnumDeviceStatus.DEACTIVATED => EnumOperationState.DEACTIVATED,
            EnumDeviceStatus.ERROR       => EnumOperationState.ERROR,
            _                            => EnumOperationState.NONE,
        };

        _log?.Info($"[SYNC→Symbol] SyncFromDevice: '{SymbolModel.Title}' DeviceStatus={status} → OperationState: {prevState}→{SymbolModel.OperationState}");
        // SYNC_DEVICE(NATS 스레드)에서 불린다 — UI 스레드로 넘겨 합친다(WP-1 ㉔). 종전 즉시 SetUpdate 는 교차 스레드로 속성창 · 마커를 건드렸다.
        MarshalUpdate();
    }

    /// <summary>복합 상태를 직접 세팅 — EventQueueManager 전이 콜백에서 호출되므로 MarshalUpdate 경유</summary>
    public void ApplyCompositeStatus(EnumCompositeEventStatus status)
    {
        if (SymbolModel == null) return;
        SymbolModel.CompositeStatus = status;
        MarshalUpdate();
    }

    public void ProcessEvent(EnumEventType eventType, EnumSeverityLevel severity)
    {
        if (SymbolModel == null) return;

        SymbolModel.CompositeStatus = eventType switch
        {
            EnumEventType.Intrusion => SymbolModel.CompositeStatus switch
            {
                EnumCompositeEventStatus.Faulted          => EnumCompositeEventStatus.FaultedDetecting,
                EnumCompositeEventStatus.FaultedDetecting => EnumCompositeEventStatus.FaultedDetecting, // 다운그레이드 방지
                _                                         => EnumCompositeEventStatus.Detecting,
            },
            EnumEventType.Fault => SymbolModel.CompositeStatus switch
            {
                EnumCompositeEventStatus.Detecting        => EnumCompositeEventStatus.FaultedDetecting,
                EnumCompositeEventStatus.FaultedDetecting => EnumCompositeEventStatus.FaultedDetecting, // Detecting 차원 보존
                _                                         => EnumCompositeEventStatus.Faulted,
            },
            _ => SymbolModel.CompositeStatus,
        };
        MarshalUpdate(); // 고빈도 경로 — dirty flag + Dispatcher 마샬링
        _log?.Info($"이벤트 처리 → {SymbolModel.CompositeStatus}: {SymbolModel.Title}");
    }

    /// <summary>
    /// 개폐 형태 축(FR-12) — 색 축과 독립. 같은 상태면 통지하지 않는다. 큐 콜백(ApplyCompositeStatus/ProcessEvent/ProcessEventReport)은 이 값을 읽거나 쓰지 않는다.
    /// </summary>
    public void ApplyDoorState(EnumDoorState next)
    {
        if (SymbolModel is not IPidsSymbolModel door) return;   // 그룹 심볼 등 문이 없는 모델은 무시
        var prev = door.DoorState;
        if (prev == next) return;
        door.DoorState = next;
        MarshalUpdate();
        _log?.Info($"[DoorState] Device({Id}) {prev}→{next}: {door.Title}");
    }

    /// <summary>접점 이벤트(ContactOn/Off)로 개폐 유도 — 현장 배선 반전(OpenOnContactOn=false)을 심볼 속성으로 흡수한다.</summary>
    public void ApplyDoorEvent(EnumEventType eventType)
    {
        if (SymbolModel is not IPidsSymbolModel door) return;
        ApplyDoorState(DoorStateMachine.Next(door.DoorState, eventType, door.OpenOnContactOn));
    }

    public void ProcessEventReport()
    {
        if (SymbolModel == null) return;

        SymbolModel.CompositeStatus = SymbolModel.CompositeStatus switch
        {
            EnumCompositeEventStatus.FaultedDetecting => EnumCompositeEventStatus.Faulted,
            _                                         => EnumCompositeEventStatus.Normal,
        };
        MarshalUpdate(); // 고빈도 경로 — dirty flag + Dispatcher 마샬링
        _log?.Info($"ProcessEventReport → {SymbolModel.CompositeStatus} 복원: {SymbolModel.Title}");
    }

    /// <summary>
    /// PTZ 데이터로 FOV 업데이트 (public API)
    /// </summary>
    /// <param name="pan">Pan 각도 (0.0 ~ 360.0)</param>
    /// <param name="tilt">Tilt 각도 (사용 안 함, 향후 확장용)</param>
    /// <param name="zoom">정규화된 줌 (0~100, 100=NVR 최대줌) — 범위 밖은 양 끝으로 고정</param>
    public void ProcessPtz(float pan, float tilt, float zoom)
    {
        UpdateFOV(pan, tilt, zoom);
    }

    #region - PTZ → FOV Conversion Methods -
    // FOV 변환 상수
    // zoom 입력: 0~100 (normalized, 100 = NVR 최대 줌 = 3km)
    private const double MaxDetectionAngle = 80.0;    // zoom=0 일 때 최대 검출 각도
    private const double MinDetectionAngle = 5.0;     // zoom=100 일 때 최소 검출 각도
    private const double MinDetectionRange = 10.0;    // zoom=0 일 때 최소 검출 거리 (m)
    private const double MaxDetectionRange = 3000.0;  // zoom=100 일 때 최대 검출 거리 (3km)

    /// <summary>
    /// Pan 각도를 FOV Bearing으로 변환
    /// </summary>
    /// <param name="pan">Pan 각도 (0.0 ~ 360.0)</param>
    /// <returns>Bearing 각도 (-180 ~ 180)</returns>
    protected double ConvertPanToBearing(float pan)
    {
        // Pan: 0~360, Bearing: -180~180
        // 0~180 → 그대로, 181~360 → -179~0
        var bearing = pan <= 180 ? pan : pan - 360;
        return bearing;
    }

    /// <summary>
    /// Zoom 값을 FOV Angle으로 변환 (줌 ↑ → 각도 ↓)
    /// </summary>
    /// <param name="zoom">정규화된 줌 (0~100, 100=NVR 최대줌)</param>
    /// <returns>검출 각도 (°): zoom=0 → 80°, zoom=100 → 5°</returns>
    protected double ConvertZoomToAngle(float zoom)
    {
        // 선형 보간: zoom 0→MaxAngle(80°), zoom 100→MinAngle(5°)
        var ratio = Math.Clamp(zoom / 100.0, 0.0, 1.0);
        return MaxDetectionAngle - ratio * (MaxDetectionAngle - MinDetectionAngle);
    }

    /// <summary>
    /// Zoom 값을 FOV Range로 변환 (줌 ↑ → 거리 ↑)
    /// </summary>
    /// <param name="zoom">정규화된 줌 (0~100, 100=NVR 최대줌=3km)</param>
    /// <returns>검출 거리 (m): zoom=0 → 10m, zoom=100 → 3000m</returns>
    protected double ConvertZoomToRange(float zoom)
    {
        // 선형 보간: zoom 0→MinRange(10m), zoom 100→MaxRange(3000m)
        var ratio = Math.Clamp(zoom / 100.0, 0.0, 1.0);
        return MinDetectionRange + ratio * (MaxDetectionRange - MinDetectionRange);
    }

    /// <summary>
    /// PTZ 값으로 FOV 업데이트
    /// </summary>
    /// <param name="pan">Pan 각도 (0.0 ~ 360.0)</param>
    /// <param name="tilt">Tilt 각도 (사용 안 함, 향후 확장용)</param>
    /// <param name="zoom">정규화된 줌 (0~100, 100=NVR 최대줌) — 범위 밖은 양 끝으로 고정</param>
    protected void UpdateFOV(float pan, float tilt, float zoom)
    {
        _log?.Info($"[UpdateFOV] 진입: SymbolModel={SymbolModel?.GetType().Name ?? "null"}, Title={SymbolModel?.Title ?? "null"}");

        // IPidsSymbolModel인 경우에만 FOV 업데이트 적용
        if (SymbolModel is not IPidsSymbolModel pidsSymbol)
        {
            _log?.Warning($"[UpdateFOV] SymbolModel이 IPidsSymbolModel이 아님 - 무시됨");
            return;  // Non-Pids 심볼은 무시
        }

        var bearing = ConvertPanToBearing(pan);
        var angle = ConvertZoomToAngle(zoom);
        var range = ConvertZoomToRange(zoom);

        _log?.Info($"[UpdateFOV] 변환값: Bearing={bearing:F2}, Angle={angle:F2}, Range={range:F2}");

        // PTZ → FOV 변환 적용
        pidsSymbol.DetectionBearing = pidsSymbol.BaseBearing + bearing;
        pidsSymbol.DetectionAngle = angle;
        pidsSymbol.DetectionRange = range;

        _log?.Info($"[UpdateFOV] SetUpdate 호출 전: {pidsSymbol.Title}");

        // 심볼 업데이트 알림 — 고빈도 PTZ 경로: dirty flag + Dispatcher 마샬링
        MarshalUpdate();

        _log?.Info($"[UpdateFOV] MarshalUpdate 큐잉");
    }
    #endregion
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public IBaseDeviceModel? DeviceModel { get; set; }
    public IPidsEventCapable? SymbolModel { get; set; }
    #endregion
    #region - Attributes -
    private ILogService _log;
    private int _isFlushPending; // 0=없음, 1=대기 중 (Interlocked, MarshalUpdate 전용)
    #endregion
}