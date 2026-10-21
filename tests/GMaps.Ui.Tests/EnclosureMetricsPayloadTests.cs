using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// ENCLOSURE_METRICS 해석 · 표시 글 — 이슈 #7-20, 브로커 연동설계 §6.6.
/// </summary>
public class EnclosureMetricsPayloadTests
{
    private static readonly DateTime Received = new(2026, 9, 12, 12, 15, 39);

    // §6.6 Publish Body 예시 그대로
    private const string Spec = @"{
      ""id"": ""6c0a2f31-7d48-4b52-9e16-0a3b5c7d8e9f"", ""m_type"": ""PUB"", ""cmd"": ""ENCLOSURE_METRICS"", ""from"": ""DBApi"",
      ""body"": { ""id"": 90, ""enclosure_id"": 394, ""temperature"": 25.5, ""humidity"": 48.0, ""voltage"": 220.0, ""current"": 3.2,
                  ""vibration"": 12, ""ups_battery_level"": 95, ""ups_charging"": true, ""detail"": null,
                  ""observed_at"": ""2026-09-12T12:30:00.000000+09:00"", ""created_at"": ""2026-09-12T12:15:34.400323+09:00"" },
      ""created"": ""2026-09-12T12:15:39.004321+09:00"" }";

    [Fact]
    public void should_read_every_metric_from_spec_example()
    {
        var r = EnclosureMetricsPayload.TryParse(Spec, Received)!;
        Assert.Equal(394, r.EnclosureId);
        Assert.Equal(25.5, r.Temperature);
        Assert.Equal(48.0, r.Humidity);
        Assert.Equal(220.0, r.Voltage);
        Assert.Equal(3.2, r.Current);
        Assert.Equal(12, r.Vibration);
        Assert.Equal(95, r.UpsBatteryLevel);
        Assert.True(r.UpsCharging);
        Assert.Equal(Received, r.ReceivedAt);
        Assert.False(r.IsStale);
    }

    [Theory]
    [InlineData(@"{""cmd"":""BROADCAST_STATUS"",""body"":{""enclosure_id"":1}}")]   // 다른 명령
    [InlineData(@"{""cmd"":""ENCLOSURE_METRICS"",""body"":{""temperature"":1}}")]     // 함체 id 없음
    [InlineData(@"{""cmd"":""ENCLOSURE_METRICS""}")]
    [InlineData("not json")]
    [InlineData("")]
    public void should_ignore_other_or_broken_messages(string json)
        => Assert.Null(EnclosureMetricsPayload.TryParse(json, Received));

    [Fact]
    public void should_keep_null_and_unreadable_values_empty()
    {
        var r = EnclosureMetricsPayload.TryParse(
            @"{""cmd"":""ENCLOSURE_METRICS"",""body"":{""enclosure_id"":5,""temperature"":""48.5"",""humidity"":null,""voltage"":""abc""}}", Received)!;
        Assert.Equal(48.5, r.Temperature);   // v1.9 문자열 숫자도 읽는다
        Assert.Null(r.Humidity);
        Assert.Null(r.Voltage);
        Assert.Null(r.UpsCharging);
    }

    [Fact]
    public void should_write_plain_words_with_units()
    {
        var r = EnclosureMetricsPayload.TryParse(Spec, Received)!;
        var rows = EnclosureMetricsText.Rows(r, Observed).ToList();
        Assert.Contains(("온도", "25.5 ℃"), rows);
        Assert.Contains(("습도", "48 %"), rows);
        Assert.Contains(("전압", "220 V"), rows);
        Assert.Contains(("전류", "3.2 A"), rows);
        Assert.Contains(("UPS 배터리", "95 % · 충전 중"), rows);
        // 8.0.15: 표시 시각은 받은 때가 아니라 매니저가 잰 때(observed_at)
        Assert.Equal(("잰 시각", Hms(Observed)), rows[^1]);

        var lines = EnclosureMetricsText.ToolTipSection(r, Observed).Split(Environment.NewLine);
        Assert.Equal("온도 25.5 ℃ · 습도 48 % · 전압 220 V · 전류 3.2 A · 진동 12 · UPS 배터리 95 % · 충전 중", lines[0]);
        Assert.Equal($"잰 시각 {Hms(Observed)}", lines[1]);
    }

    [Fact]
    public void should_mark_old_value_after_30_seconds_without_new_metrics()
    {
        Assert.False(EnclosureMetricsText.IsStale(Received, Received.AddSeconds(30)));
        Assert.True(EnclosureMetricsText.IsStale(Received, Received.AddSeconds(31)));

        var stale = EnclosureMetricsPayload.TryParse(Spec, Received)! with { IsStale = true };
        Assert.Equal($"잰 시각 {Hms(Observed)} · 오래된 값", EnclosureMetricsText.ToolTipSection(stale, Observed.AddMinutes(1)).Split(Environment.NewLine)[^1]);
        Assert.Equal($"{Observed:MM-dd HH:mm:ss} · 오래된 값", EnclosureMetricsText.Rows(stale, Observed.AddDays(1)).Last().Value);
    }

    // ── G35: 8.0.15 계측 행 id · 관측 시각(observed_at) ──

    private static readonly DateTime Observed = DateTimeOffset.Parse("2026-09-12T12:30:00.000000+09:00").LocalDateTime;
    private static string Hms(DateTime t) => t.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void should_read_row_id_and_observed_at()
    {
        var r = EnclosureMetricsPayload.TryParse(Spec, Received)!;
        Assert.Equal(90, r.MetricId);
        Assert.Equal(Observed, r.ObservedAt);
        Assert.Equal(Observed, r.MeasuredAt);
        Assert.Equal(Received, r.ReceivedAt);   // 받은 시각은 그대로 따로 든다
    }

    [Theory]
    // observed_at 없음 → 옛 measured_at(v1.9) → created_at → 받은 시각
    [InlineData(@"""measured_at"":""2026-09-12T11:00:00+09:00"",""created_at"":""2026-09-12T10:00:00+09:00""", "2026-09-12T11:00:00+09:00")]
    [InlineData(@"""created_at"":""2026-09-12T10:00:00+09:00""", "2026-09-12T10:00:00+09:00")]
    [InlineData(@"""observed_at"":null,""created_at"":""2026-09-12T10:00:00+09:00""", "2026-09-12T10:00:00+09:00")]
    [InlineData(@"""observed_at"":""not a time""", null)]
    [InlineData(@"""x"":1", null)]
    public void should_fall_back_measured_time(string timeKeys, string? expected)
    {
        var r = EnclosureMetricsPayload.TryParse($@"{{""cmd"":""ENCLOSURE_METRICS"",""body"":{{""enclosure_id"":5,{timeKeys}}}}}", Received)!;
        Assert.Null(r.MetricId);
        var want = expected is null ? Received : DateTimeOffset.Parse(expected).LocalDateTime;
        Assert.Equal(want, r.MeasuredAt);
    }

    [Fact]
    public void should_judge_old_value_by_measured_time_not_received_time()
    {
        var r = EnclosureMetricsPayload.TryParse(Spec, Observed.AddSeconds(40))!;   // 방금 받았지만 40초 전에 잰 값
        Assert.True(EnclosureMetricsText.IsStale(r, Observed.AddSeconds(40)));
        Assert.False(EnclosureMetricsText.IsStale(r, Observed.AddSeconds(30)));
    }
}
