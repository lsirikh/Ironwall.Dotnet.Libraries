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
                    SymbolModel.OperationState = EnumOperationState.ACTIVE;
                    _log?.Info($"DeviceModel의 Status({DeviceModel.Status}),SymbolModel의 EventStatus({SymbolModel.EventStatus}) ");
                    SymbolModel.SetUpdate();
                    break;
                case EnumEventType.Fault:
                    DeviceModel.Status = EnumDeviceStatus.ERROR;
                    SymbolModel.EventStatus = EnumEventStatus.Fault;
                    SymbolModel.OperationState = EnumOperationState.FAULT;
                    _log?.Info($"DeviceModel의 Status({DeviceModel.Status}),SymbolModel의 EventStatus({SymbolModel.EventStatus}) ");
                    SymbolModel.SetUpdate();
                    break;
                case EnumEventType.Connection:
                    DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                    SymbolModel.EventStatus = EnumEventStatus.Connection;
                    SymbolModel.OperationState = EnumOperationState.ACTIVE;
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

    private void OnEventRestored()
    {
        // 정상 상태로 복원
        if (DeviceModel != null && SymbolModel != null)
        {
            DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
            SymbolModel.EventStatus = EnumEventStatus.Normal;
            SymbolModel.OperationState = EnumOperationState.ACTIVE;
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
                SymbolModel.OperationState = EnumOperationState.ACTIVE;
                break;
            case EnumEventType.Fault:
                DeviceModel.Status = EnumDeviceStatus.ERROR;
                SymbolModel.EventStatus = EnumEventStatus.Fault;
                SymbolModel.OperationState = EnumOperationState.FAULT;
                break;
            case EnumEventType.Connection:
                DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
                SymbolModel.EventStatus = EnumEventStatus.Connection;
                SymbolModel.OperationState = EnumOperationState.ACTIVE;
                break;
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public IBaseDeviceModel? DeviceModel { get; set; }
    public IPidsSymbolModel? SymbolModel { get; set; }
    #endregion
    #region - Attributes -
    private ILogService _log;
    private EventAnimationManager _animationManager;

    #endregion
}