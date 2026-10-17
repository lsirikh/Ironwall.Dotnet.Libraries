using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

/****************************************************************************
   Purpose      : 함체 환경 · 부품 판정 경고 해석(순수) — 이슈 #7 Q-4a · 19
                  OPERATION_EVENT(함체 · 개폐 아닌 사유 · WARNING 이상) → 경고, ACTION_REPORT(운영 원본) → 해제.
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>함체 한 대에 걸린 운영 이벤트 경고 한 건. <paramref name="IsCritical"/>=severity CRITICAL.</summary>
public sealed record EnclosureAlert(int EnclosureId, int EventId, string Reason, bool IsCritical);

/// <summary>함체 심볼에 띄울 경고 요약 — 가장 급한 한 건 + 걸린 수.</summary>
public sealed record EnclosureAlertSummary(string ReasonText, bool IsCritical, int Count)
{
    /// <summary>경고 목록 → 요약(CRITICAL 먼저, 같으면 최근 이벤트). 비었으면 null.</summary>
    public static EnclosureAlertSummary? Of(IEnumerable<EnclosureAlert> alerts)
    {
        var list = alerts.ToList();
        if (list.Count == 0) return null;
        var lead = list.OrderByDescending(a => a.IsCritical).ThenByDescending(a => a.EventId).First();
        return new EnclosureAlertSummary(OperationEventText.ReasonText(lead.Reason), lead.IsCritical, list.Count);
    }

    /// <summary>툴팁 줄 — "환경 경고: 함체 고온(심각) 외 1건".</summary>
    public string ToolTipLine
        => $"환경 경고: {ReasonText}({(IsCritical ? "심각" : "경고")})" + (Count > 1 ? $" 외 {Count - 1}건" : string.Empty);
}

/// <summary>운영 이벤트 · 조치보고 봉투 해석. NATS·WPF 무의존이라 헤드리스로 단언할 수 있다.</summary>
public static class EnclosureAlertPayload
{
    public const string OperationCommand = "OPERATION_EVENT";
    public const string ActionReportCommand = "ACTION_REPORT";

    /// <summary>봉투 문자열 → 항목(배열 봉투는 항목마다). 응답(RSP)과 못 읽는 글은 뺀다.</summary>
    public static IEnumerable<JObject> Items(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<JObject>();
        try
        {
            var root = JToken.Parse(json);
            var items = root is JArray array ? array.OfType<JObject>() : root is JObject one ? new[] { one } : Enumerable.Empty<JObject>();
            return items.Where(i => !string.Equals(i.Value<string>("m_type"), "RSP", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        catch { return Array.Empty<JObject>(); }
    }

    /// <summary>함체 환경 · 부품 판정 경고. 함체가 아니거나 · 개폐 사유거나 · WARNING 미만이거나 · 번호가 없으면 null.</summary>
    public static EnclosureAlert? TryParseAlert(JObject envelope)
    {
        if (!IsCommand(envelope, OperationCommand) || envelope["body"] is not JObject body) return null;
        var reason = body.Value<string>("reason")?.Trim();
        var severity = body.Value<string>("severity");
        if (string.IsNullOrEmpty(reason) || OperationEventText.IsDoorReason(reason) || !OperationEventText.IsWarningOrAbove(severity)) return null;

        var device = body["device"] as JObject;   // 지워진 장비는 device: null
        var isEnclosure = string.Equals(device?.Value<string>("category_device"), "enclosure", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(device?.Value<string>("type_device"), "Enclosure", StringComparison.OrdinalIgnoreCase);
        var enclosureId = device?.Value<int?>("id") ?? 0;
        var eventId = body.Value<int?>("id") ?? 0;
        if (!isEnclosure || enclosureId <= 0 || eventId <= 0) return null;

        return new EnclosureAlert(enclosureId, eventId, reason,
            string.Equals(severity?.Trim(), "CRITICAL", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>조치보고의 원본이 운영 이벤트면 그 번호, 아니면 null.</summary>
    public static int? TryParseReportedOperationId(JObject envelope)
    {
        if (!IsCommand(envelope, ActionReportCommand) || envelope["body"] is not JObject body) return null;
        if (body["from_event"] is not JObject origin) return null;
        if (!string.Equals(origin.Value<string>("category_event"), "operation", StringComparison.OrdinalIgnoreCase)) return null;
        var id = origin.Value<int?>("id");
        return id > 0 ? id : null;
    }

    private static bool IsCommand(JObject envelope, string command)
        => string.Equals(envelope.Value<string>("cmd"), command, StringComparison.OrdinalIgnoreCase);
}
