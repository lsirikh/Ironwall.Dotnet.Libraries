using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// ActionEvent의 FromEvent 필드를 위한 Custom JsonConverter
/// type_event 값에 따라 DetectionEventDto 또는 MalfunctionEventDto로 역직렬화
/// </summary>
public class FromEventConverter : JsonConverter<IEventDto>
{
    public override IEventDto? ReadJson(JsonReader reader, Type objectType, IEventDto? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        // JSON 객체 로드
        var jsonObject = JObject.Load(reader);

        // 라우팅: 서버 type_event는 세부유형(ContactOn/Fire/Fault 등) 다양 → type_event 문자열 매칭만으로는 누락.
        //   구조(필드 존재)로 우선 판별 — Detection은 result, Malfunction은 reason 보유. type_event는 보조 폴백.
        IEventDto? result;
        if (jsonObject["result"] != null)
            result = jsonObject.ToObject<DetectionEventDto>(serializer);
        else if (jsonObject["reason"] != null)
            result = jsonObject.ToObject<MalfunctionEventDto>(serializer);
        else
            result = jsonObject["type_event"]?.Value<string>()?.ToLower() switch
            {
                "intrusion" or "detection" => jsonObject.ToObject<DetectionEventDto>(serializer),
                "fault" or "malfunction" => jsonObject.ToObject<MalfunctionEventDto>(serializer),
                _ => null
            };

        return result;
    }

    public override void WriteJson(JsonWriter writer, IEventDto? value, JsonSerializer serializer)
    {
        // 직렬화는 그대로 진행
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        serializer.Serialize(writer, value);
    }
}
