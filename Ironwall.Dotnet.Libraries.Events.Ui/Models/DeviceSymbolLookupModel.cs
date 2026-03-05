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
    public void ProcessEvent(EnumEventType eventType, EnumSeverityLevel severity)
    {
        try
        {

            // 1. 기존 비즈니스 상태 업데이트 (기존 로직 유지)
            UpdateDeviceAndSymbolState(eventType, severity);

            // 2. 애니메이션 상태 처리 (신규)
            _animationManager.ProcessNewEvent(eventType);

            // 3. 심볼 업데이트 알림
            SymbolModel.SetUpdate();


            switch (eventType)
            {
                case EnumEventType.Intrusion:
                    DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                    SymbolModel.EventStatus = EnumEventStatus.Detecting;
                    SymbolModel.OperationState = EnumOperationState.ACTIVATED;
                    _log?.Info($"DeviceModel의 Status({DeviceModel.Status}),SymbolModel의 EventStatus({SymbolModel.EventStatus}) ");
                    SymbolModel.SetUpdate();
                    break;
                case EnumEventType.Fault:
                    DeviceModel.Status = EnumDeviceStatus.ERROR;
                    SymbolModel.EventStatus = EnumEventStatus.Fault;
                    SymbolModel.OperationState = EnumOperationState.ERROR;
                    _log?.Info($"DeviceModel의 Status({DeviceModel.Status}),SymbolModel의 EventStatus({SymbolModel.EventStatus}) ");
                    SymbolModel.SetUpdate();
                    break;
                case EnumEventType.Connection:
                    DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                    SymbolModel.EventStatus = EnumEventStatus.Connection;
                    SymbolModel.OperationState = EnumOperationState.ACTIVATED;
                    _log?.Info($"DeviceModel의 Status({DeviceModel.Status}),SymbolModel의 EventStatus({SymbolModel.EventStatus}) ");
                    SymbolModel.SetUpdate();
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
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
        // 정상 상태로 복원
        if (DeviceModel != null && SymbolModel != null)
        {
            DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
            SymbolModel.EventStatus = EnumEventStatus.Normal;
            SymbolModel.OperationState = EnumOperationState.ACTIVATED;
            SymbolModel.SetUpdate();

            _log?.Info($"상태 복원: {SymbolModel.Title}");
        }
    }

    private void UpdateDeviceAndSymbolState(EnumEventType eventType, EnumSeverityLevel severity)
    {
        if(DeviceModel == null || SymbolModel == null) return;

        // 기존 switch 문 로직 유지
        switch (eventType)
        {
            case EnumEventType.Intrusion:
                DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                SymbolModel.EventStatus = EnumEventStatus.Detecting;
                SymbolModel.OperationState = EnumOperationState.ACTIVATED;
                break;
            case EnumEventType.Fault:
                DeviceModel.Status = EnumDeviceStatus.ERROR;
                SymbolModel.EventStatus = EnumEventStatus.Fault;
                SymbolModel.OperationState = EnumOperationState.ERROR;
                break;
            case EnumEventType.Connection:
                DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                SymbolModel.EventStatus = EnumEventStatus.Connection;
                SymbolModel.OperationState = EnumOperationState.ACTIVATED;
                break;
        }
    }

    #region - PTZ → FOV Conversion Methods -
    // FOV 변환 상수
    private const double BaseDetectionAngle = 80.0;   // 기본 검출 각도 (1x 줌)
    private const double MinDetectionAngle = 5.0;     // 최소 검출 각도
    private const double MaxDetectionAngle = 120.0;   // 최대 검출 각도
    private const double BaseDetectionRange = 30.0;  // 기본 검출 거리 (미터, 1x 줌)
    private const double MinDetectionRange = 10.0;    // 최소 검출 거리
    private const double MaxDetectionRange = 2000.0;  // 최대 검출 거리

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
    /// <param name="zoom">줌 백분율 (100 = 1x, 200 = 2x, ...)</param>
    /// <returns>검출 각도</returns>
    protected double ConvertZoomToAngle(float zoom)
    {
        // 줌 배율 계산 (100% = 1x)
        var zoomMultiplier = zoom / 100.0;
        if (zoomMultiplier < 1.0) zoomMultiplier = 1.0;

        // 줌이 높을수록 각도는 좁아짐 (역비례)
        var angle = BaseDetectionAngle / zoomMultiplier;

        // 최소/최대 제한
        return Math.Max(MinDetectionAngle, Math.Min(MaxDetectionAngle, angle));
    }

    /// <summary>
    /// Zoom 값을 FOV Range로 변환 (줌 ↑ → 거리 ↑)
    /// </summary>
    /// <param name="zoom">줌 백분율 (100 = 1x, 200 = 2x, ...)</param>
    /// <returns>검출 거리 (미터)</returns>
    protected double ConvertZoomToRange(float zoom)
    {
        // 줌 배율 계산 (100% = 1x)
        var zoomMultiplier = zoom / 100.0;
        if (zoomMultiplier < 1.0) zoomMultiplier = 1.0;

        // 줌이 높을수록 거리는 늘어남 (정비례)
        var range = BaseDetectionRange * zoomMultiplier;

        // 최소/최대 제한
        return Math.Max(MinDetectionRange, Math.Min(MaxDetectionRange, range));
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