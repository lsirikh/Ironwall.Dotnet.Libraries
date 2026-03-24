using Ironwall.Dotnet.Libraries.Api.Messages.Defines;
using Ironwall.Dotnet.Libraries.Api.Messages.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Api.Messages.Helpsers;

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

        // type_event 필드로 타입 결정
        var typeEvent = jsonObject["type_event"]?.Value<string>();

        IEventDto? result = typeEvent?.ToLower() switch
        {
            "intrusion" or "detection" => jsonObject.ToObject<DetectionEventDto>(serializer),
            "fault" or "malfunction" => jsonObject.ToObject<MalfunctionEventDto>(serializer),
            _ => throw new JsonSerializationException($"Unknown event type: {typeEvent}")
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
