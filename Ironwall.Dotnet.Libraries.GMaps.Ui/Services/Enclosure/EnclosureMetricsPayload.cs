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

/// <summary>함체 한 대의 최신 계측. <paramref name="ReceivedAt"/>=GIS 가 받은 시각(로컬), <paramref name="IsStale"/>=오래된 값.</summary>
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
    bool IsStale = false);

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
            var id = Int(body["enclosure_id"]);
            if (id is not > 0) return null;

            return new EnclosureMetricsReading(
                id.Value,
                Number(body["temperature"]),
                Number(body["humidity"]),
                Number(body["voltage"]),
                Number(body["current"]),
                Int(body["vibration"]),
                Int(body["ups_battery_level"]),
                body["ups_charging"] is { Type: JTokenType.Boolean } charging ? charging.Value<bool>() : null,
                receivedAt);
        }
        catch { return null; }
    }

    // v2.0 은 숫자로 오지만 v1.9 처럼 문자열("25.5")로 와도 읽는다 — 못 읽으면 null(값 없음).
    private static double? Number(JToken? token)
        => token is null || token.Type == JTokenType.Null ? null
            : double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;

    private static int? Int(JToken? token)
        => Number(token) is { } v ? (int)Math.Round(v) : null;
}
