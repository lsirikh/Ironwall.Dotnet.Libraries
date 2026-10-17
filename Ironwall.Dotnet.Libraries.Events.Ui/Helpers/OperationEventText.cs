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
    /// 상세(JSONB)를 사람이 읽는 한 줄로 편다. 비었으면 null.
    /// <para>아는 키(브로커 연동설계 §6.7 「detail — 발생 경로별 키」)는 말로 바꾼다:
    /// 문 · 통문 = <c>previous</c> → <c>state</c>(옛 행은 <c>door_status</c> · <c>gate_status</c>) · <c>device_status</c>(점검 중 · 장비 장애만) /
    /// 함체 환경 = <c>field</c> · <c>value</c> · <c>threshold</c> · <c>direction</c> → "온도 65℃ — 기준 60℃ 초과" /
    /// <c>suppressed_schedule_id</c> → "억제 시간 중 발생" / 매니저 문구 <c>display.title</c> · <c>display.message[]</c>(파싱 · 분기하지 않고 보이기만).
    /// 식별용 키(<c>component</c> · <c>metric_id</c>)는 화면에 내지 않는다.</para>
    /// <para>모르는 키는 지우지 않고 "키=값" 으로 뒤에 붙인다 — 서버가 키를 늘려도 정보가 사라지지 않게.</para>
    /// </summary>
    public static string? DetailText(JObject? detail)
    {
        if (detail is null || !detail.HasValues) return null;
        var parts = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "component", "metric_id" };

        // 매니저가 실은 사람용 문구 — 있으면 맨 앞(이슈 #7 · 21).
        if (detail["display"] is JObject display)
        {
            used.Add("display");
            var title = display.Value<string>("title");
            if (!string.IsNullOrWhiteSpace(title)) parts.Add(title.Trim());
            if (display["message"] is JArray messages)
                parts.AddRange(messages.Select(m => m.Type == JTokenType.String ? m.Value<string>() : null)
                                       .Where(m => !string.IsNullOrWhiteSpace(m))!);
        }

        // 문 · 통문 — 지금 상태(이전 상태).
        var state = Str(detail, "state") ?? Str(detail, "door_status") ?? Str(detail, "gate_status");
        if (state is not null)
        {
            used.UnionWith(new[] { "state", "door_status", "gate_status", "previous" });
            var previous = Str(detail, "previous");
            parts.Add(previous is null ? DoorStateText(state) : $"{DoorStateText(previous)} → {DoorStateText(state)}");
        }
        var deviceStatus = Str(detail, "device_status");
        if (deviceStatus is not null)
        {
            used.Add("device_status");
            if (string.Equals(deviceStatus, "DEACTIVATED", StringComparison.OrdinalIgnoreCase)) parts.Add("장비 점검 중");
            else if (string.Equals(deviceStatus, "ERROR", StringComparison.OrdinalIgnoreCase)) parts.Add("장비 장애 상태");
        }

        // 함체 환경 — 계측값과 넘은 경계.
        var field = Str(detail, "field");
        if (field is not null && detail["value"] is JToken value && value.Type is JTokenType.Integer or JTokenType.Float)
        {
            used.UnionWith(new[] { "field", "value", "threshold", "direction" });
            var (name, unit) = MetricText(field);
            var text = $"{name} {Number(value)}{unit}";
            if (detail["threshold"] is JToken threshold && threshold.Type is JTokenType.Integer or JTokenType.Float)
            {
                var direction = Str(detail, "direction");
                var word = string.Equals(direction, "LOW", StringComparison.OrdinalIgnoreCase) ? "미만" : "초과";
                text += $" — 기준 {Number(threshold)}{unit} {word}";
            }
            parts.Add(text);
        }

        if (detail["suppressed_schedule_id"] is JToken suppressed && suppressed.Type != JTokenType.Null)
        {
            used.Add("suppressed_schedule_id");
            parts.Add("억제 시간 중 발생");
        }

        // 모르는 키 — 받은 그대로.
        parts.AddRange(detail.Properties().Where(p => !used.Contains(p.Name))
                             .Select(p => $"{p.Name}={ValueText(p.Value)}"));
        return parts.Count == 0 ? null : string.Join(" · ", parts);

        static string? Str(JObject o, string key)
            => o[key] is JToken t && t.Type == JTokenType.String && !string.IsNullOrWhiteSpace(t.Value<string>()) ? t.Value<string>()!.Trim() : null;

        static string Number(JToken t) => t.Type == JTokenType.Integer
            ? t.Value<long>().ToString(System.Globalization.CultureInfo.InvariantCulture)
            : t.Value<double>().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static string ValueText(JToken token) => token.Type switch
        {
            JTokenType.Null => "null",
            JTokenType.String => token.Value<string>() ?? string.Empty,
            _ => token.ToString(Newtonsoft.Json.Formatting.None),
        };
    }

    /// <summary>문 위치 상태 글자 — 모르는 값은 원문.</summary>
    private static string DoorStateText(string state) => state.ToUpperInvariant() switch
    {
        "OPEN" or "OPENED" => "열림",
        "CLOSED" or "CLOSE" => "닫힘",
        "RUNNING" => "움직이는 중",
        _ => state,
    };

    /// <summary>함체 계측 이름 · 단위(서버 <c>device_config.thresholds</c> 의 7개 경계). 모르는 이름은 원문 · 단위 없음.</summary>
    private static (string Name, string Unit) MetricText(string field) => field.ToLowerInvariant() switch
    {
        "temperature" => ("온도", "℃"),
        "humidity" => ("습도", "%"),
        "voltage" => ("전압", "V"),
        "current" => ("전류", "A"),
        "vibration" => ("진동", ""),
        "ups_battery_level" => ("UPS 배터리", "%"),
        _ => (field, ""),
    };
}
