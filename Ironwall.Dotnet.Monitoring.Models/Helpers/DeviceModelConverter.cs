using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Enums;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Monitoring.Models.Helpers
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2024 2:30:36 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DeviceModelConverter : JsonConverter<BaseDeviceModel>
    {
        private ILogService? _log;
        public override bool CanWrite => false;
        public DeviceModelConverter() { }
        public DeviceModelConverter(ILogService log) => _log = log;
        public override BaseDeviceModel? ReadJson(JsonReader reader, Type objectType, BaseDeviceModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                JObject jo = JObject.Load(reader);

                // DeviceType에 따라 적절한 클래스 인스턴스를 생성
                EnumDeviceType deviceType = (jo["device_type"] ?? throw new NullReferenceException("jo[\"device_type\"] is null")).ToObject<EnumDeviceType>();
                BaseDeviceModel? device = null;

                switch (deviceType)
                {
                    case EnumDeviceType.NONE:
                        break;
                    case EnumDeviceType.Controller:
                        device = jo.ToObject<ControllerDeviceModel>();
                        break;
                    case EnumDeviceType.Multi:
                    case EnumDeviceType.Fence:
                    case EnumDeviceType.Underground:
                    case EnumDeviceType.Contact:
                    case EnumDeviceType.PIR:
                    case EnumDeviceType.IoController:
                    case EnumDeviceType.Laser:
                    case EnumDeviceType.Radar:
                    case EnumDeviceType.OpticalCable:
                    case EnumDeviceType.SmartSensor:
                    case EnumDeviceType.SmartSensor2:
                    case EnumDeviceType.SmartCompound:

                        device = jo.ToObject<SensorDeviceModel>();
                        break;
                    case EnumDeviceType.Cable:
                        break;
                    case EnumDeviceType.IpCamera:
                        device = jo.ToObject<CameraDeviceModel>();
                        break;
                    case EnumDeviceType.IpSpeaker:
                        break;
                    case EnumDeviceType.Fence_Line:
                        break;
                    default:
                        throw new Exception($"Unknown device type: {deviceType}");
                }

                if (device == null) throw new NullReferenceException();
                serializer.Populate(jo.CreateReader(), device);
                return device;
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
                throw;
            }


            
        }

        public override void WriteJson(JsonWriter writer, BaseDeviceModel? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }


    }
}