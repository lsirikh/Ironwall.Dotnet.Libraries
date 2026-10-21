using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 함체 계측(<c>ENCLOSURE_METRICS</c>)을 툴팁 · 상세 창 글로 바꾸는 순수 함수(이슈 #7-20).
/// </summary>
public static class EnclosureMetricsText
{
    /// <summary>이만큼 새 계측이 안 오면 오래된 값으로 본다(발행 주기 기본 10초의 3배).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    public const string StaleText = "오래된 값";

    /// <summary>시각 줄 이름 — 매니저가 잰 시각(8.0.15 observed_at)을 보인다.</summary>
    public const string TimeLabel = "잰 시각";

    public static bool IsStale(DateTime measuredAt, DateTime now) => now - measuredAt > StaleAfter;

    /// <summary>
    /// 오래된 값 판정 — 받은 때가 아니라 <b>잰 때</b> 기준이다. 서버는 새 측정이 없어도 같은 행(같은 id · observed_at)을
    /// 10초마다 다시 보내므로 받은 시각으로 재면 영영 '오래된 값' 이 안 된다(8.0.15 브로커 §6.6 재발행).
    /// </summary>
    public static bool IsStale(EnclosureMetricsReading reading, DateTime now) => IsStale(reading.MeasuredAt, now);

    /// <summary>항목별 (이름, 값) — 값이 없는 항목은 뺀다. 마지막 줄은 늘 <see cref="TimeLabel"/>.</summary>
    public static IEnumerable<(string Label, string Value)> Rows(EnclosureMetricsReading reading, DateTime now)
    {
        if (reading.Temperature is { } t) yield return ("온도", $"{F(t, "0.#")} ℃");
        if (reading.Humidity is { } h) yield return ("습도", $"{F(h, "0.#")} %");
        if (reading.Voltage is { } v) yield return ("전압", $"{F(v, "0.#")} V");
        if (reading.Current is { } c) yield return ("전류", $"{F(c, "0.##")} A");
        if (reading.Vibration is { } vib) yield return ("진동", vib.ToString(CultureInfo.InvariantCulture));
        if (reading.UpsBatteryLevel is { } ups)
            yield return ("UPS 배터리", reading.UpsCharging == true ? $"{ups} % · 충전 중" : $"{ups} %");
        else if (reading.UpsCharging is { } charging)
            yield return ("UPS 배터리", charging ? "충전 중" : "충전 안 함");
        yield return (TimeLabel, MeasuredText(reading, now));
    }

    /// <summary>툴팁 절 — 계측 한 줄 + 잰 시각 한 줄.</summary>
    public static string ToolTipSection(EnclosureMetricsReading reading, DateTime now)
    {
        var rows = Rows(reading, now).ToList();
        var values = rows.Take(rows.Count - 1).Select(r => $"{r.Label} {r.Value}").ToList();
        var lines = new List<string>(2);
        if (values.Count > 0) lines.Add(string.Join(" · ", values));
        lines.Add($"{rows[^1].Label} {rows[^1].Value}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string MeasuredText(EnclosureMetricsReading reading, DateTime now)
    {
        var at = reading.MeasuredAt;
        var text = at.Date == now.Date
            ? at.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : at.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return reading.IsStale ? $"{text} · {StaleText}" : text;
    }

    private static string F(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
