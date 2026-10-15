using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>함체 환경 · 부품 판정 경고 해석(이슈 #7 Q-4a) — 무엇이 경고를 걸고 무엇이 푸는가.</summary>
public class EnclosureAlertPayloadTests
{
    private static string Op(string reason, string severity, string device = @"{""id"":394,""category_device"":""enclosure""}", int id = 7, string mType = "PUB")
        => $@"{{""m_type"":""{mType}"",""cmd"":""OPERATION_EVENT"",""body"":{{""id"":{id},""reason"":""{reason}"",""severity"":""{severity}"",""device"":{device}}}}}";

    private static EnclosureAlert? Alert(string json) => EnclosureAlertPayload.Items(json).Select(EnclosureAlertPayload.TryParseAlert).SingleOrDefault();

    [Fact]
    public void should_raise_alert_for_enclosure_environment_and_combination_reasons()
    {
        Assert.Equal(new EnclosureAlert(394, 7, "ENCLOSURE_TEMP_HIGH", true), Alert(Op("ENCLOSURE_TEMP_HIGH", "CRITICAL")));
        Assert.Equal(new EnclosureAlert(394, 7, "ENCLOSURE_BREAKER_TRIPPED", false), Alert(Op("ENCLOSURE_BREAKER_TRIPPED", "warning")));   // 앞으로 올 조합 판정 사유
        Assert.NotNull(Alert(Op("ENCLOSURE_UPS_BATTERY_LOW", "WARNING", @"{""id"":394,""type_device"":""Enclosure""}")));                 // 옛 서버 장비 표현
    }

    [Theory]
    [InlineData("ENCLOSURE_DOOR_OPEN", "WARNING", @"{""id"":394,""category_device"":""enclosure""}")]   // 개폐는 문 표시 몫
    [InlineData("ENCLOSURE_TEMP_HIGH", "INFO", @"{""id"":394,""category_device"":""enclosure""}")]      // 정보 등급
    [InlineData("GATE_FORCED_ENTRY", "CRITICAL", @"{""id"":5,""category_device"":""gate""}")]          // 함체 아님
    [InlineData("ENCLOSURE_TEMP_HIGH", "CRITICAL", "null")]                                              // 지워진 장비
    public void should_not_raise_alert(string reason, string severity, string device)
        => Assert.Null(Alert(Op(reason, severity, device)));

    [Fact]
    public void should_ignore_replies_and_read_every_item_of_array_envelope()
    {
        Assert.Empty(EnclosureAlertPayload.Items(Op("ENCLOSURE_TEMP_HIGH", "CRITICAL", mType: "RSP")));
        var array = $"[{Op("ENCLOSURE_TEMP_HIGH", "CRITICAL", id: 1)},{Op("ENCLOSURE_HUMIDITY_HIGH", "WARNING", id: 2)}]";
        Assert.Equal(new[] { 1, 2 }, EnclosureAlertPayload.Items(array).Select(EnclosureAlertPayload.TryParseAlert).Select(a => a!.EventId));
        Assert.Empty(EnclosureAlertPayload.Items("not json"));
    }

    [Theory]
    [InlineData("operation", 7, 7)]
    [InlineData("detection", 7, null)]   // 탐지 7번 조치가 운영 7번 경고를 풀면 안 된다
    [InlineData("operation", 0, null)]
    public void should_release_only_on_action_report_of_operation_origin(string category, int originId, int? expected)
    {
        var json = $@"{{""m_type"":""PUB"",""cmd"":""ACTION_REPORT"",""body"":{{""id"":900,""from_event"":{{""id"":{originId},""category_event"":""{category}""}}}}}}";
        Assert.Equal(expected, EnclosureAlertPayload.Items(json).Select(EnclosureAlertPayload.TryParseReportedOperationId).Single());
    }

    [Fact]
    public void should_lead_with_critical_alert_and_count_the_rest()
    {
        var summary = EnclosureAlertSummary.Of(new[]
        {
            new EnclosureAlert(394, 30, "ENCLOSURE_HUMIDITY_HIGH", false),
            new EnclosureAlert(394, 20, "ENCLOSURE_TEMP_HIGH", true),
            new EnclosureAlert(394, 10, "ENCLOSURE_SMOKE", false),   // 모르는 사유는 원문
        })!;
        Assert.Equal(new EnclosureAlertSummary("함체 고온", true, 3), summary);
        Assert.Equal("환경 경고: 함체 고온(심각) 외 2건", summary.ToolTipLine);
        Assert.Null(EnclosureAlertSummary.Of(Enumerable.Empty<EnclosureAlert>()));
    }
}
