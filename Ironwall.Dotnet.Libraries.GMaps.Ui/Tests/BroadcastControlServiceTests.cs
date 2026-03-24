using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// BroadcastControlService 단위 테스트 (Phase 1 — Speaker Broadcast ContextMenu)
/// </summary>
public class BroadcastControlServiceTests
{
    #region - Helpers -

    private static BroadcastControlService CreateService(
        string domain = "sensorway",
        string group  = "unit001",
        Mock<INatsService>? mockNats = null)
    {
        var stubSetup = new StubNatsSetupModel { DomainNats = domain, GroupNats = group };
        var nats = mockNats?.Object ?? new Mock<INatsService>().Object;
        return new BroadcastControlService(nats, stubSetup);
    }

    #endregion

    // ── Test 1.1 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Play_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.play", svc.BuildSubject("play"));
    }

    // ── Test 1.2 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Tts_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.tts", svc.BuildSubject("tts"));
    }

    // ── Test 1.3 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public void BuildSubject_Stop_ReturnsCorrectSubject()
    {
        var svc = CreateService();
        Assert.Equal("sensorway.unit001.broadcast_manager.stop", svc.BuildSubject("stop"));
    }

    // ── Test 1.4 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task PublishPlayAsync_CallsNatsServiceWithCorrectJson()
    {
        // Arrange
        var mockNats = new Mock<INatsService>();
        mockNats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        var svc = CreateService(mockNats: mockNats);

        // Act
        await svc.PublishPlayAsync(speakerId: 101, fileGroupId: 5, repeat: 2);

        // Assert — subject
        mockNats.Verify(n => n.PublishAsync(
            "sensorway.unit001.broadcast_manager.play",
            It.Is<string>(json =>
                json.Contains("\"speaker_ids\"") &&
                json.Contains("101") &&
                json.Contains("\"file_group_id\"") &&
                json.Contains("5") &&
                json.Contains("\"repeat\"") &&
                json.Contains("2") &&
                json.Contains("\"cmd\"") &&
                json.Contains("BROADCAST_PLAY"))),
            Times.Once);
    }

    // ── Test 1.5 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task PublishTtsAsync_CallsNatsServiceWithCorrectJson()
    {
        // Arrange
        var mockNats = new Mock<INatsService>();
        mockNats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        var svc = CreateService(mockNats: mockNats);

        // Act
        await svc.PublishTtsAsync(speakerId: 101, message: "경계 경보입니다.");

        // Assert
        mockNats.Verify(n => n.PublishAsync(
            "sensorway.unit001.broadcast_manager.tts",
            It.Is<string>(json =>
                json.Contains("\"speaker_ids\"") &&
                json.Contains("101") &&
                json.Contains("\"message\"") &&
                json.Contains("경계 경보입니다.") &&
                json.Contains("\"cmd\"") &&
                json.Contains("TTS"))),
            Times.Once);
    }

    // ── Test 1.6 ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BroadcastControl")]
    public async Task PublishStopAsync_CallsNatsServiceWithCorrectJson()
    {
        // Arrange
        var mockNats = new Mock<INatsService>();
        mockNats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        var svc = CreateService(mockNats: mockNats);

        // Act
        await svc.PublishStopAsync(speakerId: 101);

        // Assert
        mockNats.Verify(n => n.PublishAsync(
            "sensorway.unit001.broadcast_manager.stop",
            It.Is<string>(json =>
                json.Contains("\"speaker_ids\"") &&
                json.Contains("101") &&
                json.Contains("\"cmd\"") &&
                json.Contains("BROADCAST_STOP"))),
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
