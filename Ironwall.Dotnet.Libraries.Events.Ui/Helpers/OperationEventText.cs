using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// 운영 이벤트의 서버 어휘 → 화면 글자. 순수 함수만 둔다(시험 가능).
/// </summary>
/// <remarks>
/// 사유 어휘 정본은 서버 <c>EnumOperationType</c>(api-test-server <c>utils/enums.py</c>) 11값이다.
/// 서버가 어휘를 늘리면 <b>원문을 그대로</b> 보인다 — 모르는 값을 지우거나 다른 말로 바꾸지 않는다.
/// </remarks>
public static class OperationEventText
{
    private static readonly IReadOnlyDictionary<string, string> Reasons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENCLOSURE_DOOR_OPEN"] = "함체 문 열림",
            ["ENCLOSURE_DOOR_CLOSED"] = "함체 문 닫힘",
            ["ENCLOSURE_TEMP_HIGH"] = "함체 고온",
            ["ENCLOSURE_TEMP_LOW"] = "함체 저온",
            ["ENCLOSURE_HUMIDITY_HIGH"] = "함체 고습",
            ["ENCLOSURE_VOLTAGE_LOW"] = "함체 저전압",
            ["ENCLOSURE_CURRENT_HIGH"] = "함체 과전류",
            ["ENCLOSURE_VIBRATION_HIGH"] = "함체 진동",
            ["ENCLOSURE_UPS_BATTERY_LOW"] = "UPS 배터리 부족",
            ["GATE_OPEN"] = "통문 열림",
            ["GATE_CLOSED"] = "통문 닫힘",
        };

    private static readonly IReadOnlyDictionary<string, string> Severities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["INFO"] = "정보",
            ["WARNING"] = "경고",
            ["CRITICAL"] = "심각",
        };

    /// <summary>사유 글자. 모르는 값은 원문, 비었으면 "—".</summary>
    public static string ReasonText(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return "—";
        return Reasons.TryGetValue(reason.Trim(), out var text) ? text : reason.Trim();
    }

    /// <summary>심각도 글자. 모르는 값은 원문, 없으면 "—".</summary>
    public static string SeverityText(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return "—";
        return Severities.TryGetValue(severity.Trim(), out var text) ? text : severity.Trim();
    }

    /// <summary>경고 이상인가 — 행을 경고색으로 칠하고 '경고 이상' 칩으로 거르는 기준.</summary>
    public static bool IsWarningOrAbove(string? severity)
        => string.Equals(severity?.Trim(), "WARNING", StringComparison.OrdinalIgnoreCase)
        || string.Equals(severity?.Trim(), "CRITICAL", StringComparison.OrdinalIgnoreCase);

    /// <summary>개폐 사유인가(서버 접미사 규약 — <c>_OPEN</c> · <c>_OPENED</c> · <c>_CLOSED</c> · <c>_CLOSE</c>).</summary>
    public static bool IsDoorReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;
        var r = reason.Trim();
        return r.EndsWith("_OPEN", StringComparison.OrdinalIgnoreCase)
            || r.EndsWith("_OPENED", StringComparison.OrdinalIgnoreCase)
            || r.EndsWith("_CLOSED", StringComparison.OrdinalIgnoreCase)
            || r.EndsWith("_CLOSE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 상세(JSONB)를 "키=값 · 키=값" 한 줄로 편다. 키 이름을 해석하지 않는다 — 사유마다 키가 다르고
    /// 문서와 배포가 어긋난 자리라(OperationEventDto.Detail 주석) 받은 그대로 보인다. 비었으면 null.
    /// </summary>
    public static string? DetailText(JObject? detail)
    {
        if (detail is null || !detail.HasValues) return null;
        var parts = detail.Properties()
                          .Select(p => $"{p.Name}={ValueText(p.Value)}")
                          .ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);

        static string ValueText(JToken token) => token.Type switch
        {
            JTokenType.Null => "null",
            JTokenType.String => token.Value<string>() ?? string.Empty,
            JTokenType.Object or JTokenType.Array => token.ToString(Newtonsoft.Json.Formatting.None),
            _ => token.ToString(Newtonsoft.Json.Formatting.None),
        };
    }
}
