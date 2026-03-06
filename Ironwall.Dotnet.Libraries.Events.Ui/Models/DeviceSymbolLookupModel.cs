using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

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
    public DeviceSymbolLookupModel(ILogService log
                                , IEventAggregator ea
                                , EventSetupModel eventSetupModel)
    {
        _log = log;
        _animationManager = new EventAnimationManager(log, ea, eventSetupModel);
        _animationManager.OnEventRestored += OnEventRestored;
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
    /// Device.Status → Symbol.OperationState 동기화
    /// </summary>
    public void SyncFromDevice(EnumDeviceStatus status)
    {
        if (SymbolModel == null)
        {
            _log?.Warning($"[SYNC→Symbol] SyncFromDevice: SymbolModel null (Id={Id})");
            return;
        }

        var prevState     = SymbolModel.OperationState;
        var prevEvent     = SymbolModel.EventStatus;

        // OperationState 동기화
        SymbolModel.OperationState = status switch
        {
            EnumDeviceStatus.ACTIVATED   => EnumOperationState.ACTIVATED,
            EnumDeviceStatus.DEACTIVATED => EnumOperationState.DEACTIVATED,
            EnumDeviceStatus.ERROR       => EnumOperationState.ERROR,
            _                            => EnumOperationState.NONE,
        };

        // EventStatus도 명시적으로 설정 (GMapBaseMarker.OperationState 세터를 우회하기 때문)
        // OnStatusChanged는 세터에서만 호출되므로 여기서 직접 설정
        SymbolModel.EventStatus = status switch
        {
            EnumDeviceStatus.ACTIVATED   => EnumEventStatus.Normal,
            EnumDeviceStatus.DEACTIVATED => EnumEventStatus.Normal,  // 깜빡임 없음
            EnumDeviceStatus.ERROR       => EnumEventStatus.Fault,   // 깜빡임 유지
            _                            => EnumEventStatus.Normal,
        };

        // 애니메이션 상태 관리: ERROR → 깜빡임 시작, 그 외 → 기존 애니메이션 정지
        if (status == EnumDeviceStatus.ERROR)
        {
            _animationManager.ProcessNewEvent(EnumEventType.Fault);
        }
        else
        {
            _animationManager.ReportCurrentEvent();
        }

        _log?.Info($"[SYNC→Symbol] SyncFromDevice: '{SymbolModel.Title}' DeviceStatus={status} → OperationState: {prevState}→{SymbolModel.OperationState}, EventStatus: {prevEvent}→{SymbolModel.EventStatus}");
        SymbolModel.SetUpdate();
    }

    public void ProcessEvent(EnumEventType eventType, EnumSeverityLevel severity)
    {
        if (SymbolModel == null) return;

        // 탐지(Intrusion) 이벤트만 ProcessEvent 경로에서 심볼 비주얼 처리
        // 장애(Fault)는 Device.Status=ERROR → SyncFromDevice 경로에서 처리
        if (eventType == EnumEventType.Intrusion)
        {
            SymbolModel.EventStatus = EnumEventStatus.Detecting;
            SymbolModel.SetUpdate();
            _animationManager.ProcessNewEvent(eventType);
            _log?.Info($"탐지 이벤트 → Detecting: {SymbolModel.Title}");
        }
    }

    public void ProcessEventReport()
    {
        _animationManager.ReportCurrentEvent();
    }

    /// <summary>
    /// PTZ 데이터로 FOV 업데이트 (public API)
    /// </summary>
    /// <param name="pan">Pan 각도 (0.0 ~ 360.0)</param>
    /// <param name="tilt">Tilt 각도 (사용 안 함, 향후 확장용)</param>
    /// <param name="zoom">줌 백분율 (100 = 1x)</param>
    public void ProcessPtz(float pan, float tilt, float zoom)
    {
        UpdateFOV(pan, tilt, zoom);
    }

    private void OnEventRestored()
    {
        if (SymbolModel == null) return;

        SymbolModel.EventStatus = EnumEventStatus.Normal;
        SymbolModel.SetUpdate();
        _log?.Info($"OnEventRestored → Normal 복원: {SymbolModel.Title}");
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
    /// <param name="zoom">줌 백분율 (100 = 1x)</param>
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

        // 심볼 업데이트 알림
        pidsSymbol.SetUpdate();

        _log?.Info($"[UpdateFOV] SetUpdate 완료");
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
    private EventAnimationManager _animationManager;
    #endregion
}