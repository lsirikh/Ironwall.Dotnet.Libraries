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
    private readonly Dictionary<int, DeviceSymbolLookupModel> _deviceSymbolLookup;
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

        _deviceSymbolLookup = new Dictionary<int, DeviceSymbolLookupModel>();
    }

    // 센서 장비-심볼 매핑 등록
    public void RegisterDeviceSymbol(IBaseDeviceModel deviceModel, ISymbolModel symbolModel)
    {
        var lookup = new DeviceSymbolLookupModel(_log, _ea, _eventSetupModel)
        {
            Id = deviceModel.Id,
            DeviceModel = deviceModel,
            SymbolModel = symbolModel
        };

        _deviceSymbolLookup[deviceModel.Id] = lookup;
    }

    // 센서 이벤트 처리
    public void ProcessDeviceEvent(int id, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
    {
        if (_deviceSymbolLookup.TryGetValue(id, out var lookup))
        {
            lookup.ProcessEvent(eventType, severity);
            var device = lookup.DeviceModel;
            switch (device.DeviceType)
            {
                case EnumDeviceType.NONE:
                    break;
                case EnumDeviceType.Controller:
                    {
                        if (!(device is IControllerDeviceModel controller)) break;
                        _log?.Info($"센서 이벤트 처리: Controller({controller.Id}) -> {eventType}");
                    }
                    break;
                case EnumDeviceType.Multi:
                case EnumDeviceType.Fence:
                case EnumDeviceType.Underground:
                case EnumDeviceType.Contact:
                case EnumDeviceType.PIR:
                case EnumDeviceType.IoController:
                case EnumDeviceType.Laser:
                case EnumDeviceType.Cable:
                case EnumDeviceType.SmartSensor:
                case EnumDeviceType.SmartSensor2:
                case EnumDeviceType.SmartCompound:
                case EnumDeviceType.Radar:
                    {
                        if (!(device is ISensorDeviceModel sensor)) break;
                        _log?.Info($"센서 이벤트 처리: Controller({sensor.Controller.Id})-Sensor({sensor.Id}) -> {eventType}");
                    }
                    break;
                case EnumDeviceType.IpCamera:
                    break;
                case EnumDeviceType.IpSpeaker:
                    break;
                case EnumDeviceType.OpticalCable:
                    break;
                default:
                    break;
            }
        }
        else
        {
            _log?.Warning($"매핑되지 않은 센서: Sensor({id})");
        }
    }

    // 컨트롤러 이벤트 처리
    public void ProcessControllerEvent(int controllerId, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
    {
        if (_deviceSymbolLookup.TryGetValue(controllerId, out var lookup))
        {
            lookup.ProcessEvent(eventType, severity);
            _log?.Info($"컨트롤러 이벤트 처리: Controller({controllerId}) -> {eventType}");
        }
        else
        {
            _log?.Warning($"매핑되지 않은 컨트롤러: Controller({controllerId})");
        }
    }

    public void ProcessEventReport(int deviceId)
    {
        if (_deviceSymbolLookup.TryGetValue(deviceId, out var lookup))
        {
            lookup.ProcessEventReport();
            _log?.Info($"조치보고 처리: Device({deviceId})");
        }
        else
        {
            _log?.Warning($"조치보고 실패 - 매핑되지 않은 장비: Device({deviceId})");
        }
    }

    public void Dispose()
    {
        _deviceSymbolLookup.Clear();
    }
}