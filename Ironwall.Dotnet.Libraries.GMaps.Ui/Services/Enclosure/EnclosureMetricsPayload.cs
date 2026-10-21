using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

/****************************************************************************
   Purpose      : ENCLOSURE_METRICS 페이로드 해석(순수) — 브로커 연동설계 §6.6 · 이슈 #7-20
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 함체 한 대의 최신 계측. <paramref name="ReceivedAt"/>=GIS 가 받은 시각(로컬), <paramref name="IsStale"/>=오래된 값,
/// <paramref name="MetricId"/>=계측 행 id(같은 행 반복 발행 구분 · 8.0.15), <paramref name="ObservedAt"/>=매니저가 잰 시각(로컬).
/// </summary>
public sealed record EnclosureMetricsReading(
    int EnclosureId,
    double? Temperature,
    double? Humidity,
    double? Voltage,
    double? Current,
    int? Vibration,
    int? UpsBatteryLevel,
    bool? UpsCharging,
    DateTime ReceivedAt,
    bool IsStale = false,
    long? MetricId = null,
    DateTime? ObservedAt = null)
{
    /// <summary>잰 시각 — '오래된 값' 판정 · 표시 기준. 시각을 못 읽은 옛 봉투는 받은 시각.</summary>
    public DateTime MeasuredAt => ObservedAt ?? ReceivedAt;
}

/// <summary><c>ENCLOSURE_METRICS</c> 봉투 해석. NATS·WPF 무의존이라 헤드리스로 단언할 수 있다.</summary>
public static class EnclosureMetricsPayload
{
    public const string Command = "ENCLOSURE_METRICS";
    public const string SubjectSuffix = "gis.enclosure-metrics";

    /// <summary>봉투에서 계측을 꺼낸다. cmd 가 다르거나 <c>enclosure_id</c> 가 없으면 null.</summary>
    public static EnclosureMetricsReading? TryParse(string? json, DateTime receivedAt)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var root = JObject.Parse(json);
            if (!string.Equals(root.Value<string>("cmd"), Command, StringComparison.OrdinalIgnoreCase)) return null;
            if (root["body"] is not JObject body) return null;
            return FromItem(body, null, receivedAt);
        }
        catch { return null; }
    }

    /// <summary>
    /// 계측 항목 하나(NATS body · REST <c>latest_metrics</c> — 같은 키 집합, 브로커 §6.6)를 읽는다.
    /// <paramref name="enclosureId"/> 를 주면 항목 안 <c>enclosure_id</c> 대신 쓴다(REST 는 항목 바깥에 둔다).
    /// </summary>
    public static EnclosureMetricsReading? FromItem(JObject item, int? enclosureId, DateTime receivedAt)
    {
        var id = enclosureId ?? Int(item["enclosure_id"]);
        if (id is not > 0) return null;

        return new EnclosureMetricsReading(
            id.Value,
            Number(item["temperature"]),
            Number(item["humidity"]),
            Number(item["voltage"]),
            Number(item["current"]),
            Int(item["vibration"]),
            Int(item["ups_battery_level"]),
            item["ups_charging"] is { Type: JTokenType.Boolean } charging ? charging.Value<bool>() : null,
            receivedAt,
            MetricId: Long(item["id"]),
            ObservedAt: MeasuredTime(item));
    }


    // v2.0 은 숫자로 오지만 v1.9 처럼 문자열("25.5")로 와도 읽는다 — 못 읽으면 null(값 없음).
    private static double? Number(JToken? token)
        => token is null || token.Type == JTokenType.Null ? null
            : double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;

    private static int? Int(JToken? token)
        => Number(token) is { } v ? (int)Math.Round(v) : null;

    private static long? Long(JToken? token)
        => Number(token) is { } v && v > 0 ? (long)Math.Round(v) : null;

    /// <summary>
    /// 잰 시각(로컬) — <c>observed_at</c>(v2.0) → 옛 <c>measured_at</c>(v1.9) → <c>created_at</c>(저장 시각).
    /// 셋 다 못 읽으면 null(받은 시각으로 대신한다).
    /// </summary>
    private static DateTime? MeasuredTime(JObject item)
    {
        foreach (var key in TimeKeys)
            if (Time(item[key]) is { } at) return at;
        return null;
    }

    private static readonly string[] TimeKeys = { "observed_at", "measured_at", "created_at" };

    private static DateTime? Time(JToken? token)
    {
        if (token is null || token.Type == JTokenType.Null) return null;
        // JObject.Parse 는 ISO 문자열을 날짜로 바꿔 둘 수 있다 — 같은 순간을 가리키는 "o" 문자열로 다시 읽는다.
        return ParseTime(token.Type == JTokenType.Date
            ? token.Value<DateTime>().ToString("o", CultureInfo.InvariantCulture)
            : token.ToString());
    }

    /// <summary>ISO 8601 시각 → 로컬 시각. 오프셋이 없으면 로컬로 본다. 못 읽으면 null.</summary>
    public static DateTime? ParseTime(string? text)
        => !string.IsNullOrWhiteSpace(text)
           && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var at)
            ? at.LocalDateTime : null;
}
