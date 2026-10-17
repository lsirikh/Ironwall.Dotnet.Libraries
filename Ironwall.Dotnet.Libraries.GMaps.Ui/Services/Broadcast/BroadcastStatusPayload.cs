using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;

/****************************************************************************
   Purpose      : BROADCAST_STATUS 페이로드 해석(순수) — PRD symbol-detail-and-door-control FR-24
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>BROADCAST_STATUS</c> body 해석. NATS·WPF 무의존 순수 함수라 헤드리스로 단언할 수 있다.
///
/// <para><b>관대하게 읽는 이유</b>: 이 메시지는 실발행을 관측하지 못한 채(VER-04) 규격만 보고 구현했다.
/// <c>ON</c>/<c>true</c>/<c>1</c> 처럼 표기가 흔들리거나 여러 대를 한 번에 보고해도 표시가 죽지 않아야 한다 —
/// 못 알아들으면 조용히 무시되어 "방송 중인데 화면은 조용한" 최악의 상태가 된다.</para>
/// </summary>
public static class BroadcastStatusPayload
{
    /// <summary>봉투에서 body 를 꺼낸다. cmd 가 <c>BROADCAST_STATUS</c> 가 아니면 null.</summary>
    public static JObject? TryReadBody(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var root = JObject.Parse(json);
            return string.Equals(root.Value<string>("cmd"), "BROADCAST_STATUS", StringComparison.OrdinalIgnoreCase)
                ? root["body"] as JObject
                : null;
        }
        catch { return null; }
    }

    /// <summary>ON/OFF · true/false · 1/0 을 모두 받는다. 해석 불가면 null(무시 대상).</summary>
    public static bool? ParseStatus(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.Boolean) return token.Value<bool>();
        if (token.Type == JTokenType.Integer) return token.Value<int>() != 0;
        return token.Value<string>()?.Trim().ToUpperInvariant() switch
        {
            "ON" or "TRUE" or "1" or "PLAYING" or "START" or "STARTED" => true,
            "OFF" or "FALSE" or "0" or "STOPPED" or "STOP" or "IDLE" => false,
            _ => null,
        };
    }

    /// <summary>단수 <c>speaker_id</c> 와 복수 <c>speaker_ids</c> 를 모두 받는다. 0 이하는 버린다.</summary>
    public static IReadOnlyList<int> ParseSpeakerIds(JObject? body)
    {
        if (body == null) return Array.Empty<int>();

        var single = body["speaker_id"];
        if (single != null && single.Type != JTokenType.Null && int.TryParse(single.ToString(), out var id) && id > 0)
            return new[] { id };

        if (body["speaker_ids"] is JArray array)
            return array.Select(x => int.TryParse(x.ToString(), out var v) ? v : 0).Where(v => v > 0).Distinct().ToList();

        return Array.Empty<int>();
    }
}
