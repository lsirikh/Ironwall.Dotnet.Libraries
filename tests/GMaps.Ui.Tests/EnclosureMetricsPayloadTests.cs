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
        var rows = EnclosureMetricsText.Rows(r, Received).ToList();
        Assert.Contains(("온도", "25.5 ℃"), rows);
        Assert.Contains(("습도", "48 %"), rows);
        Assert.Contains(("전압", "220 V"), rows);
        Assert.Contains(("전류", "3.2 A"), rows);
        Assert.Contains(("UPS 배터리", "95 % · 충전 중"), rows);
        Assert.Equal(("받은 시각", "12:15:39"), rows[^1]);

        var lines = EnclosureMetricsText.ToolTipSection(r, Received).Split(Environment.NewLine);
        Assert.Equal("온도 25.5 ℃ · 습도 48 % · 전압 220 V · 전류 3.2 A · 진동 12 · UPS 배터리 95 % · 충전 중", lines[0]);
        Assert.Equal("받은 시각 12:15:39", lines[1]);
    }

    [Fact]
    public void should_mark_old_value_after_30_seconds_without_new_metrics()
    {
        Assert.False(EnclosureMetricsText.IsStale(Received, Received.AddSeconds(30)));
        Assert.True(EnclosureMetricsText.IsStale(Received, Received.AddSeconds(31)));

        var stale = EnclosureMetricsPayload.TryParse(Spec, Received)! with { IsStale = true };
        Assert.Equal("받은 시각 12:15:39 · 오래된 값", EnclosureMetricsText.ToolTipSection(stale, Received.AddMinutes(1)).Split(Environment.NewLine)[^1]);
        Assert.Equal("09-12 12:15:39 · 오래된 값", EnclosureMetricsText.Rows(stale, Received.AddDays(1)).Last().Value);
    }
}
