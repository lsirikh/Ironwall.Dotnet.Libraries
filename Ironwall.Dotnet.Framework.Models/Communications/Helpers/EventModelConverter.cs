using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using Ironwall.Dotnet.Framework.Models.Events;
using Autofac.Core;

namespace Ironwall.Dotnet.Framework.Models.Communications.Helpers
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2024 9:02:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventModelConverter : JsonConverter<MetaEventModel>
    {
        public override MetaEventModel? ReadJson(JsonReader reader, Type objectType, MetaEventModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            if (jo == null) return null;

            // DeviceType에 따라 적절한 클래스 인스턴스를 생성
            //EnumEventType eventType = jo["type_event"].ToObject<EnumEventType>();
            if (!jo.TryGetValue("type_event", out var token))
                throw new JsonSerializationException("device_type 필드가 존재하지 않습니다.");

            EnumEventType eventType = token.ToObject<EnumEventType>();

            MetaEventModel? eventModel = null;

            switch (eventType)
            {
                case EnumEventType.Intrusion:
                    eventModel = jo.ToObject<DetectionEventModel>();
                    break;
                case EnumEventType.ContactOn:
                    break;
                case EnumEventType.ContactOff:
                    break;
                case EnumEventType.Connection:
                    break;
                case EnumEventType.Action:
                    break;
                case EnumEventType.Fault:
                    eventModel = jo.ToObject<MalfunctionEventModel>();
                    break;
                case EnumEventType.WindyMode:
                    break;
                default:
                    break;
            }

            if (eventModel == null) return null;

            serializer.Populate(jo.CreateReader(), eventModel);
            return eventModel;
        }

        public override void WriteJson(JsonWriter writer, MetaEventModel? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}