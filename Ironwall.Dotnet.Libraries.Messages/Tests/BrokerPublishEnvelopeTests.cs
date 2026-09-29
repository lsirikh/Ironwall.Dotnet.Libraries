using System;
using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// GIS 가 내는 PUB 봉투의 <c>created</c> 표기 — 브로커 명세 v2.0.7 §3 봉투 표 · §6.4 ACTION_REPORT 예시:
/// ISO 8601 · 마이크로초 6자리 고정 · 오프셋(<c>+09:00</c>). 고정폭 파서가 길이가 다른 표기에서 깨졌다(명세 v2.0 사유).
/// 헤디드 r18-e1 EVT-E2E-049 에서 GIS 는 <c>2026-09-29T21:57:29.802Z</c>(밀리초 3자리 · Z)를 냈다.
/// </summary>
public class BrokerPublishEnvelopeTests
{
    private static readonly Regex SpecCreated = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}[+-]\d{2}:\d{2}$");

    [Fact]
    public void should_stamp_created_with_six_digit_microseconds_and_offset_when_publishing()
    {
        var envelope = new JObject { ["id"] = 1 }.ToBrokerPublish("ACTION_REPORT", "GIS");

        // 시각을 DateTime 으로 바꾸지 않고 글자 그대로 읽는다(기본 JObject.Parse 는 바꿔 버려 표기를 볼 수 없다).
        using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(envelope.ToJson())) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        var created = JObject.Load(reader)["created"]!.Value<string>()!;

        Assert.Matches(SpecCreated, created);
        Assert.True(DateTimeOffset.TryParse(created, out var parsed), created);
        Assert.True((DateTimeOffset.Now - parsed).Duration() < TimeSpan.FromMinutes(1), created);
    }
}
