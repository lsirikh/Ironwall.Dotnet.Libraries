using Ironwall.Dotnet.Libraries.Base.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/****************************************************************************
   Purpose      : NATS 메시지 본문 → 봉투 목록. 단일 객체면 하나, 배열이면 객체 항목마다 하나(WP-1 ⑰).
                  호스트 라우터(NatsBrokerService.MessageSelector — ParseMessageItems)와 같은 의미다. 경로 B(라이브러리 수신
                  서비스)는 JObject.Parse 만 써서 배열 봉투가 오면 통째로 예외 → 로그 한 줄로 버려졌다.
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
internal static class NatsEnvelopeItems
{
    /// <summary>
    /// 본문을 봉투(객체) 목록으로. 비었거나 JSON 이 아니면 빈 목록(한 줄 로그). 배열 안의 객체가 아닌 항목은 건너뛴다.
    /// </summary>
    /// <param name="data">NATS 메시지 본문.</param>
    /// <param name="log">파싱 실패를 남길 곳(없으면 조용히).</param>
    /// <param name="channel">로그 머리(예: "DETECTION").</param>
    internal static IReadOnlyList<JObject> Parse(string? data, ILogService? log, string channel)
    {
        if (string.IsNullOrWhiteSpace(data)) return Array.Empty<JObject>();
        try
        {
            return JToken.Parse(data) switch
            {
                JObject single => new[] { single },
                JArray array => array.OfType<JObject>().ToList(),
                _ => Array.Empty<JObject>(),
            };
        }
        catch (JsonException ex)
        {
            log?.Error($"[{channel}] NATS 본문 JSON 파싱 실패: {ex.Message}");
            return Array.Empty<JObject>();
        }
    }
}
