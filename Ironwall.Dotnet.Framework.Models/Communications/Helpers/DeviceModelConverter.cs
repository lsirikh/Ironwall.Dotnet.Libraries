using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Text;

namespace Ironwall.Dotnet.Framework.Models.Communications.Helpers
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
        public override BaseDeviceModel? ReadJson(JsonReader reader, Type objectType, BaseDeviceModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);

            if (jo == null) return null;
            // DeviceType에 따라 적절한 클래스 인스턴스를 생성
            //EnumDeviceType deviceType = jo["device_type"].ToObject<EnumDeviceType>();
            if (!jo.TryGetValue("device_type", out var token))
                throw new JsonSerializationException("device_type 필드가 존재하지 않습니다.");

            EnumDeviceType deviceType = token.ToObject<EnumDeviceType>();

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

            if(device == null) return null;

            serializer.Populate(jo.CreateReader(), device);
            return device;
        }

        public override void WriteJson(JsonWriter writer, BaseDeviceModel? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}