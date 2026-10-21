using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 방송 음원 파일 그룹 DTO — <c>GET /api/file-groups</c>(명세 §5.10).
/// <para>명세 모양: <c>{id, server_id, group_id, group_name, files:[문자열], created_at, updated_at}</c>.</para>
/// <para>(8.0.15 대조 G25) 종전에는 <c>name</c>·<c>description</c> 키로 읽어 서버 응답의 <c>group_name</c> 이
/// 조용히 버려졌다. 코드 쪽 이름(<see cref="Name"/>)은 그대로 두고 JSON 키만 명세에 맞췄다.</para>
/// </summary>
public class FileGroupDto : BaseDto
{
    /// <summary>방송 서버 id(<c>server_id</c>, <c>category_server=SPEAKER_API</c> 서버를 가리키는 것이 맞다).</summary>
    [JsonProperty("server_id", Order = 2)]
    public int ServerId { get; set; }

    /// <summary>방송 서버 안의 그룹 번호(<c>group_id</c>) — <c>(server_id, group_id)</c> 가 유일하다.</summary>
    [JsonProperty("group_id", Order = 3)]
    public int GroupId { get; set; }

    /// <summary>그룹 이름 — JSON 키는 <c>group_name</c>(명세 §5.10.1).</summary>
    [JsonProperty("group_name", Order = 4)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 음원 파일 이름 목록. 명세는 문자열 배열이지만, 원소가 객체로 와도(<c>{name}</c> · <c>{file_name}</c> 등) 이름을 꺼내 읽는다.
    /// </summary>
    [JsonProperty("files", Order = 5)]
    [JsonConverter(typeof(FileNameListConverter))]
    public List<string> Files { get; set; } = new List<string>();

    /// <summary>설명 — 명세에는 없는 키라 값이 있을 때만 싣는다(옛 코드 호환용).</summary>
    [JsonProperty("description", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}

/// <summary>
/// 파일 목록 읽기 변환기 — 원소가 문자열이면 그대로, 객체면 <c>name</c>·<c>file_name</c>·<c>filename</c>·<c>path</c> 순으로 이름을 꺼낸다.
/// 쓰기는 늘 문자열 배열이다.
/// </summary>
public sealed class FileNameListConverter : JsonConverter<List<string>>
{
    private static readonly string[] NameKeys = { "name", "file_name", "filename", "path" };

    public override List<string>? ReadJson(JsonReader reader, Type objectType, List<string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var result = new List<string>();
        if (reader.TokenType == JsonToken.Null) return result;

        var token = JToken.Load(reader);
        if (token is not JArray array) return result;

        foreach (var item in array)
        {
            switch (item)
            {
                case JValue { Type: JTokenType.String } text:
                    result.Add((string)text!);
                    break;
                case JObject obj:
                    var name = NameKeys
                        .Select(key => obj[key])
                        .FirstOrDefault(value => value is JValue { Type: JTokenType.String });
                    if (name != null) result.Add((string)name!);
                    break;
                case JValue { Type: not JTokenType.Null } other:
                    result.Add(other.ToString());
                    break;
            }
        }
        return result;
    }

    public override void WriteJson(JsonWriter writer, List<string>? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var name in value ?? new List<string>()) writer.WriteValue(name);
        writer.WriteEndArray();
    }
}
