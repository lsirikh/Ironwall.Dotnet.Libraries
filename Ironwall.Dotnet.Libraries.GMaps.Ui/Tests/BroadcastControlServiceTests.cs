using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// BroadcastControlService 단위 테스트
/// — PLAY/STOP은 v1.5.2 §6.4에 따라 REQ(BrokerRequestClient 경유)+결과 반환,
///   TTS는 비스펙 cmd로 PUB 유지(PRD OQ-1). (GIS_Nats_v152_Req_Transition FR-03)
/// </summary>
public class BroadcastControlServiceTests
{
    #region - Helpers -

    private static BroadcastControlService CreateService(
        string domain = "sensorway",
        string group  = "unit001",
        Mock<INatsService>? mockNats = null,
        Mock<IBrokerRequestClient>? mockClient = null)
    {
        var stubSetup = new StubNatsSetupModel { DomainNats = domain, GroupNats = group };
        var nats = mockNats?.Object ?? new Mock<INatsService>().Object;
        var client = mockClient?.Object ?? new Mock<IBrokerRequestClient>().Object;
        return new BroadcastControlService(nats, stubSetup, client);
    }

    #endregion

    // ── Subject 빌드 ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Play_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.play", svc.BuildSubject("play"));
    }

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Tts_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.tts", svc.BuildSubject("tts"));
    }

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Stop_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.stop", svc.BuildSubject("stop"));
    }

    // ── BROADCAST_PLAY (REQ) ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task should_request_play_with_req_cmd_and_body_when_publish_play()
    {
        var mockClient = new Mock<IBrokerRequestClient>();
        mockClient.Setup(c => c.RequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BroadcastPlayBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrokerRequestResult.Ok("ok", "r1"));
        var svc = CreateService(mockClient: mockClient);

        var result = await svc.PublishPlayAsync(speakerId: 101, fileGroupId: 5, repeat: 2);

        Assert.True(result.Success);
        mockClient.Verify(c => c.RequestAsync(
            "sensorway.unit001.broadcast_manager.play",
            "BROADCAST_PLAY",
            It.Is<BroadcastPlayBodyDto>(b =>
                b.SpeakerIds.Count == 1 && b.SpeakerIds[0] == 101 &&
                b.FileGroupId == 5 && b.Repeat == 2),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BROADCAST_STOP (REQ) ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task should_request_stop_with_req_cmd_and_body_when_publish_stop()
    {
        var mockClient = new Mock<IBrokerRequestClient>();
        mockClient.Setup(c => c.RequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BroadcastStopBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrokerRequestResult.Ok("ok", "r2"));
        var svc = CreateService(mockClient: mockClient);

        var result = await svc.PublishStopAsync(speakerId: 101);

        Assert.True(result.Success);
        mockClient.Verify(c => c.RequestAsync(
            "sensorway.unit001.broadcast_manager.stop",
            "BROADCAST_STOP",
            It.Is<BroadcastStopBodyDto>(b => b.SpeakerIds.Count == 1 && b.SpeakerIds[0] == 101),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 실패 결과 전파 (NFR-02) ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task should_propagate_failure_result_when_client_reports_no_response()
    {
        var mockClient = new Mock<IBrokerRequestClient>();
        mockClient.Setup(c => c.RequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BroadcastStopBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "대상 서비스 응답 없음"));
        var svc = CreateService(mockClient: mockClient);

        var result = await svc.PublishStopAsync(speakerId: 101);

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.NoResponse, result.Reason);
    }

    // ── TTS (비스펙 cmd — PUB 유지, OQ-1) ───────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task should_publish_tts_as_pub_when_publish_tts()
    {
        var mockNats = new Mock<INatsService>();
        mockNats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        var svc = CreateService(mockNats: mockNats);

        await svc.PublishTtsAsync(speakerId: 101, message: "경계 경보입니다.");

        mockNats.Verify(n => n.PublishAsync(
            "sensorway.unit001.broadcast_manager.tts",
            It.Is<string>(json =>
                json.Contains("\"m_type\":\"PUB\"") &&
                json.Contains("\"speaker_ids\"") &&
                json.Contains("101") &&
                json.Contains("\"message\":\"경계 경보입니다.\"") &&
                json.Contains("\"cmd\":\"TTS\""))),
            Times.Once);
    }
}

/// <summary>테스트용 INatsSetupModel 스텁</summary>
file class StubNatsSetupModel : INatsSetupModel
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
