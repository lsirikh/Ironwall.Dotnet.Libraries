using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 5:52:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceSymbolLookupModel : BaseModel
{
    private ILogService _log;

    public DeviceSymbolLookupModel(ILogService log)
    {
         _log = log;
    }

    public IBaseDeviceModel? DeviceModel { get; set; }
    public IPidsSymbolModel? SymbolModel { get; set; }


    // 이벤트 처리 메서드들
    public void ProcessEvent(EnumEventType eventType, EnumSeverityLevel severity)
    {
        try
        {
            if (DeviceModel == null || SymbolModel == null) throw new NullReferenceException($"{nameof(DeviceSymbolLookupModel)}(Id:{Id})에 비정상적인 참조된 Model이 있습니다.");

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

    public void ResetToNormal()
    {
        try
        {
            if (DeviceModel == null || SymbolModel == null) throw new NullReferenceException($"{nameof(DeviceSymbolLookupModel)}(Id:{Id})에 비정상적인 참조된 Model이 있습니다.");

            SymbolModel.EventStatus = EnumEventStatus.Normal;
            DeviceModel.Status = EnumDeviceStatus.ACTIVATED;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
        
    }

}