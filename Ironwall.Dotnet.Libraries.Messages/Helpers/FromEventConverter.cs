using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/// <summary>
/// ActionEvent 의 <c>from_event</c> 필드를 위한 Custom JsonConverter.
/// <para><b>판별자(<c>category_event</c>) 기반 분기</b>(F-07). <c>from_event</c> 는 탐지·장애·연결·운영
/// 네 이벤트의 Union 이고, 서버는 <c>app/schemas/event.py:860</c> 에서
/// <c>discriminator='category_event'</c> 로 선언한다(명세 §6.4 "★ from_event 는 네 이벤트의 Union 이고
/// 판별자는 category_event 입니다").</para>
/// <para><b>왜 바꿨나</b> — 6.3.17 까지는 판별자가 없어 소비자가 <c>type_event</c>·필드 모양으로
/// 카테고리를 <b>추측</b>해야 했다. 운영 이벤트는 <c>reason</c> 을 가져 장애로 오분류됐고,
/// <c>"Operation"</c> 이 클라 어휘에 없어 조치 이력이 통째로 버려졌다(라이브 GIS 파손).</para>
/// <para><b>미지 값 폴백</b>(명세 VER-03) — 서버는 카테고리 어휘를 계속 늘린다(<c>operation</c> 이 그 예).
/// 모르는 <c>category_event</c> 나 판별자 부재(운영 6.3.2 응답에는 이 키가 없다)에서는
/// <b>예외를 내지 않고</b> 종전 구조/<c>type_event</c> 추론으로 내려간다 — 최악이 <c>null</c>(조치 1건 원본 미해석)이고
/// 목록 로딩 전체가 끊기지는 않는다.</para>
/// </summary>
public class FromEventConverter : JsonConverter<IEventDto>
{
    public override IEventDto? ReadJson(JsonReader reader, Type objectType, IEventDto? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        // JSON 객체 로드
        var jsonObject = JObject.Load(reader);

        // ── 1순위: 판별자 `category_event` (서버 7.0+ 응답에 항상 실린다) ──────────────
        //   8.0.1 Response 스키마가 const 로 고정한 네 값: detection · malfunction · connection · operation
        //   (`action` 은 조치 자신의 카테고리라 `from_event` 에는 오지 않는다 — ActionEventDto 는 IEventDto 가 아니다.)
        var category = (jsonObject["category_event"] as JValue)?.Value as string;
        switch (category?.Trim().ToLowerInvariant())
        {
            case "detection": return jsonObject.ToObject<DetectionEventDto>(serializer);
            case "malfunction": return jsonObject.ToObject<MalfunctionEventDto>(serializer);
            case "connection": return jsonObject.ToObject<ConnectionEventDto>(serializer);
            case "operation": return jsonObject.ToObject<OperationEventDto>(serializer);
        }

        // ── 폴백: 판별자가 없거나(6.3.2 응답) 미지 값(서버가 어휘를 늘린 직후) ──────────
        return ResolveWithoutDiscriminator(jsonObject, serializer);
    }

    /// <summary>
    /// 판별자를 못 쓸 때의 추론 경로. <b>구조 → <c>type_event</c></b> 순으로 본다.
    /// </summary>
    private static IEventDto? ResolveWithoutDiscriminator(JObject jsonObject, JsonSerializer serializer)
    {
        // 운영(operation) 은 `reason` + `severity` 를 함께 싣는 유일한 카테고리다 —
        //   장애(`reason` 만 있음)보다 **먼저** 판정해야 오분류가 나지 않는다.
        if (jsonObject["severity"] != null && jsonObject["reason"] != null)
            return jsonObject.ToObject<OperationEventDto>(serializer);

        if (jsonObject["result"] != null)
            return jsonObject.ToObject<DetectionEventDto>(serializer);

        if (jsonObject["reason"] != null)
            return jsonObject.ToObject<MalfunctionEventDto>(serializer);

        // 마지막 보조 — `type_event` 허용 목록(명세 `ALLOWED_TYPE_EVENT_BY_CATEGORY`):
        //   탐지 {Intrusion, Alert, ContactOn, ContactOff, WindyMode} · 장애 {Fault} ·
        //   연결 {Connection} · 운영 {Operation}
        var typeEvent = (jsonObject["type_event"] as JValue)?.Value as string;
        return typeEvent?.Trim().ToLowerInvariant() switch
        {
            "intrusion" or "alert" or "contacton" or "contactoff" or "windymode" or "detection"
                => jsonObject.ToObject<DetectionEventDto>(serializer),
            "fault" or "malfunction"
                => jsonObject.ToObject<MalfunctionEventDto>(serializer),
            "connection"
                => jsonObject.ToObject<ConnectionEventDto>(serializer),
            "operation"
                => jsonObject.ToObject<OperationEventDto>(serializer),
            _ => null,   // 끝내 모르면 null — 예외로 목록 전체를 끊지 않는다(VER-03).
        };
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
