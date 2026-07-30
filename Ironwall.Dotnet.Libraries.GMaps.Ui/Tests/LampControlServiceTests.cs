using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// LampControlService 단위 테스트 — GIS.md v1.5.2 §2.2~2.5 (4종 전부 REQ+RSP).
/// UI 미배선(로직 계층 선행) — subject/cmd/body 와이어 정합과 사전 검증을 고정한다.
/// (GIS_Nats_v152_Req_Transition FR-07)
/// </summary>
public class LampControlServiceTests
{
    #region - Helpers -

    private static (LampControlService svc, Mock<IBrokerRequestClient> client) Create(
        string domain = "sensorway", string group = "unit001")
    {
        var stubSetup = new StubLampNatsSetupModel { DomainNats = domain, GroupNats = group };
        var client = new Mock<IBrokerRequestClient>();
        // Moq 제네릭 매칭: TBody별로 셋업해야 실제 호출 타입과 매칭된다 (It.IsAny<object>는 TBody=object 전용)
        var ok = BrokerRequestResult.Ok("ok", "r0");
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampClearBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampOffBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampColorSetBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampBuzzerSetBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        return (new LampControlService(client.Object, stubSetup), client);
    }

    #endregion

    // ── Subject 빌드 ─────────────────────────────────────────────────────────

    [Theory]
    [Trait("Category", "LampControl")]
    [InlineData("lamp-clear", "sensorway.unit001.proxy.lamp-clear")]
    [InlineData("lamp-off", "sensorway.unit001.proxy.lamp-off")]
    [InlineData("lamp-color", "sensorway.unit001.proxy.lamp-color")]
    [InlineData("lamp-buzzer", "sensorway.unit001.proxy.lamp-buzzer")]
    public void should_build_proxy_subject_when_action_given(string action, string expected)
    {
        var (svc, _) = Create();
        Assert.Equal(expected, svc.BuildSubject(action));
    }

    // ── LAMP_CLEAR ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_send_clear_with_null_lamp_ids_when_clear_all()
    {
        var (svc, client) = Create();

        var result = await svc.ClearAsync();   // 전체 해제 — lamp_ids 생략 시맨틱(§2.2)

        Assert.True(result.Success);
        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.proxy.lamp-clear",
            "LAMP_CLEAR",
            It.Is<LampClearBodyDto>(b => b.LampIds == null),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_return_invalid_without_request_when_clear_ids_empty()
    {
        // 빈 리스트는 "lamp_ids":[](스펙 미정의 상태)로 나가면 안 됨 — 전체 해제는 명시적 null만 허용
        var (svc, client) = Create();

        var result = await svc.ClearAsync(new List<int>());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Invalid, result.Reason);
        client.Verify(c => c.RequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampClearBodyDto>(),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_send_clear_with_ids_when_targets_given()
    {
        var (svc, client) = Create();

        await svc.ClearAsync(new List<int> { 501, 502 });

        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.proxy.lamp-clear",
            "LAMP_CLEAR",
            It.Is<LampClearBodyDto>(b => b.LampIds != null && b.LampIds.Count == 2 && b.LampIds[0] == 501),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── LAMP_OFF ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_send_off_when_lamp_ids_given()
    {
        var (svc, client) = Create();

        await svc.OffAsync(new List<int> { 501 });

        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.proxy.lamp-off",
            "LAMP_OFF",
            It.Is<LampOffBodyDto>(b => b.LampIds.Count == 1 && b.LampIds[0] == 501),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_return_invalid_without_request_when_off_ids_empty()
    {
        var (svc, client) = Create();

        var result = await svc.OffAsync(new List<int>());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Invalid, result.Reason);
        client.Verify(c => c.RequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampOffBodyDto>(),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── LAMP_COLOR_SET ──────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_send_color_set_with_enum_values_when_color_given()
    {
        var (svc, client) = Create();

        await svc.SetColorAsync(new List<int> { 501, 502 }, EnumLampColor.Red, EnumLightMode.Blinking);

        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.proxy.lamp-color",
            "LAMP_COLOR_SET",
            It.Is<LampColorSetBodyDto>(b =>
                b.LampIds.Count == 2 &&
                b.Color == EnumLampColor.Red &&
                b.Mode == EnumLightMode.Blinking),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── LAMP_BUZZER_SET ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_send_buzzer_set_when_buzzer_given()
    {
        var (svc, client) = Create();

        await svc.SetBuzzerAsync(new List<int> { 501 }, EnumBuzzerSound.PiContinue);

        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.proxy.lamp-buzzer",
            "LAMP_BUZZER_SET",
            It.Is<LampBuzzerSetBodyDto>(b => b.Buzzer == EnumBuzzerSound.PiContinue),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 실패 결과 전파 ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LampControl")]
    public async Task should_propagate_failure_result_when_client_reports_rejected()
    {
        var (svc, client) = Create();
        client.Setup(c => c.RequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LampOffBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrokerRequestResult.Fail(EnumBrokerFailure.Rejected, "경광등 장비를 찾을 수 없습니다.", "Lamp not found with id=999"));

        var result = await svc.OffAsync(new List<int> { 999 });

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Rejected, result.Reason);
        Assert.Equal("Lamp not found with id=999", result.Message);
    }
}

/// <summary>테스트용 INatsSetupModel 스텁</summary>
file class StubLampNatsSetupModel : INatsSetupModel
{
    public string IpAddressNats    { get; set; } = "localhost";
    public int    PortNats         { get; set; } = 4222;
    public string? DefaultSubjectNats { get; set; }
    public string? DomainNats      { get; set; }
    public string? GroupNats       { get; set; }
    public string? SubsystemNats   { get; set; }
    public string? UsernameNats    { get; set; }
    public string? PasswordNats    { get; set; }
    public int    ConnectionTimeoutNats { get; set; } = 5000;
    public string EffectiveSubject => $"{DomainNats}.{GroupNats}.{SubsystemNats}.>";
}
