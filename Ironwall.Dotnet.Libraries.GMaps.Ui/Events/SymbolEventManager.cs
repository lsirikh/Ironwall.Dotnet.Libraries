using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 8/27/2025 4:49:01 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class SymbolEventManager : IDisposable
    {
        private readonly Dictionary<int, DeviceSymbolLookupModel> _deviceSymbolLookup;
        private readonly Dictionary<int, DeviceSymbolLookupModel> _controllerSymbolLookup;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogService _log;

        public SymbolEventManager(IEventAggregator eventAggregator, ILogService log)
        {
            _eventAggregator = eventAggregator;
            _log = log;
            _deviceSymbolLookup = new Dictionary<int, DeviceSymbolLookupModel>();
            _controllerSymbolLookup = new Dictionary<int, DeviceSymbolLookupModel>();
        }

        // 센서 장비-심볼 매핑 등록
        public void RegisterDeviceSymbol(IBaseDeviceModel deviceModel, IPidsSymbolModel symbolModel)
        {
            switch (deviceModel.DeviceType)
            {
                case EnumDeviceType.NONE:
                    break;
                case EnumDeviceType.Controller:
                    {
                        if (!(deviceModel is ControllerDeviceModel controller)) break;

                        var lookup = new DeviceSymbolLookupModel(_log)
                        {
                            Id = controller.Id,
                            DeviceModel = deviceModel,
                            SymbolModel = symbolModel
                        };

                        _controllerSymbolLookup[controller.Id] = lookup;
                        _log?.Info($"컨트롤러 장비-심볼 매핑 등록: Controller({controller.Id}) -> Symbol({symbolModel.Title})");
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
                    {
                        if (!(deviceModel is SensorDeviceModel sensor)) break;

                        var compositeKey = GenerateCompositeKey(sensor.Controller.Id, sensor.Id);
                        var lookup = new DeviceSymbolLookupModel(_log)
                        {
                            Id = compositeKey,
                            DeviceModel = deviceModel,
                            SymbolModel = symbolModel
                        };

                        _deviceSymbolLookup[compositeKey] = lookup;
                        _log?.Info($"센서 장비-심볼 매핑 등록: Controller({sensor.Controller.Id})-Sensor({sensor.Id}) -> Symbol({symbolModel.Title})");
                    }
                    break;
                case EnumDeviceType.IpCamera:
                    {
                        if (!(deviceModel is CameraDeviceModel camera)) break;

                        var lookup = new DeviceSymbolLookupModel(_log)
                        {
                            Id = camera.Id,
                            DeviceModel = deviceModel,
                            SymbolModel = symbolModel
                        };

                        _deviceSymbolLookup[camera.Id] = lookup;
                        _log?.Info($"센서 장비-심볼 매핑 등록: Camera({camera.Id}) -> Symbol({symbolModel.Title})");
                    }
                    break;
                case EnumDeviceType.SmartSensor:
                    break;
                case EnumDeviceType.SmartSensor2:
                    break;
                case EnumDeviceType.SmartCompound:
                    break;
                case EnumDeviceType.IpSpeaker:
                    break;
                case EnumDeviceType.Radar:
                    break;
                case EnumDeviceType.OpticalCable:
                    break;
                default:
                    break;
            }
        }

        // 컨트롤러 장비-심볼 매핑 등록
        public void RegisterControllerSymbol(int controllerId, IBaseDeviceModel deviceModel, IPidsSymbolModel symbolModel)
        {
            var lookup = new DeviceSymbolLookupModel(_log)
            {
                Id = controllerId,
                DeviceModel = deviceModel,
                SymbolModel = symbolModel
            };

            _controllerSymbolLookup[controllerId] = lookup;
            _log?.Info($"컨트롤러 장비-심볼 매핑 등록: Controller({controllerId}) -> Symbol({symbolModel.Title})");
        }

        // 센서 이벤트 처리
        public void ProcessSensorEvent(int controllerId, int sensorId, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
        {
            var compositeKey = GenerateCompositeKey(controllerId, sensorId);
            if (_deviceSymbolLookup.TryGetValue(compositeKey, out var lookup))
            {
                lookup.ProcessEvent(eventType, severity);
                _log?.Info($"센서 이벤트 처리: Controller({controllerId})-Sensor({sensorId}) -> {eventType}");
            }
            else
            {
                _log?.Warning($"매핑되지 않은 센서: Controller({controllerId})-Sensor({sensorId})");
            }
        }

        // 컨트롤러 이벤트 처리
        public void ProcessControllerEvent(int controllerId, EnumEventType eventType, EnumSeverityLevel severity = EnumSeverityLevel.WARNING)
        {
            if (_controllerSymbolLookup.TryGetValue(controllerId, out var lookup))
            {
                lookup.ProcessEvent(eventType, severity);
                _log?.Info($"컨트롤러 이벤트 처리: Controller({controllerId}) -> {eventType}");
            }
            else
            {
                _log?.Warning($"매핑되지 않은 컨트롤러: Controller({controllerId})");
            }
        }

        // 복합 키 생성 (Controller + Sensor)
        private int GenerateCompositeKey(int controllerId, int sensorId)
        {
            return controllerId * 10000 + sensorId; // 예: Controller(1) + Sensor(5) = 10005
        }

        public void Dispose()
        {
            _deviceSymbolLookup.Clear();
            _controllerSymbolLookup.Clear();
        }
    }
}