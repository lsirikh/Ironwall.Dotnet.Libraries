using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Monitoring.Models.Helpers
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2024 9:02:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventModelConverter : JsonConverter<ExEventModel>
    {
        private ILogService? _log;
        public override bool CanWrite => false;
        public EventModelConverter(){}
        public EventModelConverter(ILogService? log)
        {
            _log = log;
        }
        public override ExEventModel ReadJson(JsonReader reader, Type objectType, ExEventModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                JObject jo = JObject.Load(reader);

                // DeviceType에 따라 적절한 클래스 인스턴스를 생성
                EnumEventType eventType = (jo["type_event"] ?? throw new NullReferenceException("jo[\"type_event\"] is null")).ToObject<EnumEventType>();
                ExEventModel? eventModel = null;

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

                serializer.Populate(jo.CreateReader(), eventModel);
                return eventModel;
            }
            catch (Exception ex)
            {
                _log?.Info(ex.Message);
                return null;
            }
            
        }

        public override void WriteJson(JsonWriter writer, ExEventModel value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}