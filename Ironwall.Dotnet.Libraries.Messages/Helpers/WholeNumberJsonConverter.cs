using Newtonsoft.Json;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// 서버 규격이 <c>number</c>(정수 · 실수 모두 허용)인 칸을 <see cref="double"/> 로 받고,
/// <b>정수 값은 정수 모양 그대로</b>(<c>21</c>, <c>21.0</c> 아님) 되돌려 쓰는 변환기.
/// <para><b>왜</b> — 탐지 <c>detail.signal</c> 은 서버 규격이 <c>Optional[float]</c> 이고 실제 저장값의 대부분이 실수다
/// (예 <c>32.2</c>, <c>api-test-server app/schemas/event.py</c> <c>DetectionDetail.signal</c>).
/// <c>int?</c> 로 받던 시절엔 실수 한 건이 목록 쪽 전체 역직렬화를 실패시켰고(REST),
/// <c>JObject.ToObject</c> 경로(NATS)는 <c>21.5</c> 를 <c>22</c> 로 조용히 반올림했다.</para>
/// <para><b>쓰기</b> — <see cref="double"/> 을 기본 직렬화하면 정수 <c>21</c> 이 <c>21.0</c> 으로 나간다.
/// 정수를 기대하는 옛 서버(6.3 등)와 "읽은 값을 그대로 되돌려 쓰기" 계약을 지키려고,
/// 소수부가 없는 값은 정수 토큰으로 쓴다. 소수부가 있으면 그대로 실수로 쓴다.</para>
/// <para>사용: 단일 값은 <c>[JsonConverter(typeof(WholeNumberJsonConverter))]</c>,
/// 목록 원소는 <c>[JsonProperty(ItemConverterType = typeof(WholeNumberJsonConverter))]</c>.</para>
/// </summary>
public sealed class WholeNumberJsonConverter : JsonConverter
{
    // double 이 정수를 정확히 표현하는 한계(2^53). 이 범위 밖의 정수형 double 은 실수 표기로 둔다.
    private const double MAX_EXACT_INTEGER = 9007199254740992d;

    public override bool CanConvert(Type objectType)
        => objectType == typeof(double) || objectType == typeof(double?);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
            case JsonToken.Undefined:
                if (objectType == typeof(double?)) return null;
                throw new JsonSerializationException($"null 을 double 로 바꿀 수 없습니다. Path '{reader.Path}'.");
            case JsonToken.Integer:
            case JsonToken.Float:
                return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
            case JsonToken.String:
                // 숫자 문자열("21.5")은 너그럽게 받는다 — 그 밖의 문자열은 형식 오류다.
                var text = reader.Value as string;
                if (string.IsNullOrWhiteSpace(text) && objectType == typeof(double?)) return null;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                throw new JsonSerializationException($"'{text}' 는 숫자가 아닙니다. Path '{reader.Path}'.");
            default:
                throw new JsonSerializationException($"숫자 자리에 {reader.TokenType} 토큰이 왔습니다. Path '{reader.Path}'.");
        }
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is not double d)
        {
            writer.WriteNull();
            return;
        }
        if (IsWholeNumber(d))
            writer.WriteValue((long)d);
        else
            writer.WriteValue(d);
    }

    /// <summary>소수부가 없고 정수로 정확히 옮길 수 있는 값인가(NaN · 무한대 제외).</summary>
    public static bool IsWholeNumber(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value)
           && Math.Abs(value) <= MAX_EXACT_INTEGER
           && value == Math.Truncate(value);
}
