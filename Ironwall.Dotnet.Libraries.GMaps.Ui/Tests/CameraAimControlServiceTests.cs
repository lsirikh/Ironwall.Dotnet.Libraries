using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// CameraAimControlService 단위 테스트 — PTZ_AIM_LOCATION REQ 전환(v1.5.2 §2.9, PUB 예외 폐지).
/// (GIS_Nats_v152_Req_Transition FR-05)
/// </summary>
public class CameraAimControlServiceTests
{
    #region - Helpers -

    private static (CameraAimControlService svc, Mock<IBrokerRequestClient> client) Create(
        BrokerRequestResult? reply = null)
    {
        var stubSetup = new StubAimNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit001" };
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraAimLocationBodyDto>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply ?? BrokerRequestResult.Ok("ok", "r1"));
        return (new CameraAimControlService(client.Object, stubSetup), client);
    }

    private static CameraAimLocationBodyDto ValidBody() => new()
    {
        CameraId = 201,
        Latitude = 38.1240,
        Longitude = 127.5690,
        CameraLatitude = 38.1234,
        CameraLongitude = 127.5678,
        DistanceM = 73.5,
        BearingDeg = 64,
        RequestedBy = "operator"
    };

    #endregion

    [Fact]
    [Trait("Category", "CameraAim")]
    public void should_build_nvr_manager_subject_when_ptz_action()
    {
        var (svc, _) = Create();
        Assert.Equal("sensorway.unit001.nvr_manager.ptz", svc.BuildSubject("ptz"));
    }

    [Fact]
    [Trait("Category", "CameraAim")]
    public async Task should_request_aim_as_req_when_body_valid()
    {
        var (svc, client) = Create();

        var result = await svc.PublishAimAsync(ValidBody());

        Assert.True(result.Success);
        client.Verify(c => c.RequestAsync(
            "sensorway.unit001.nvr_manager.ptz",
            "PTZ_AIM_LOCATION",
            It.Is<CameraAimLocationBodyDto>(b => b.CameraId == 201),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "CameraAim")]
    public async Task should_return_invalid_without_request_when_camera_id_invalid()
    {
        var (svc, client) = Create();
        var body = ValidBody();
        body.CameraId = 0;

        var result = await svc.PublishAimAsync(body);

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Invalid, result.Reason);
        client.Verify(c => c.RequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraAimLocationBodyDto>(),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "CameraAim")]
    public async Task should_return_invalid_without_request_when_latlng_invalid()
    {
        var (svc, client) = Create();
        var body = ValidBody();
        body.Latitude = 999;   // WGS84 범위 밖

        var result = await svc.PublishAimAsync(body);

        Assert.Equal(EnumBrokerFailure.Invalid, result.Reason);
        client.Verify(c => c.RequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraAimLocationBodyDto>(),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "CameraAim")]
    public async Task should_propagate_failure_result_when_client_reports_no_response()
    {
        var (svc, _) = Create(BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "대상 서비스 응답 없음"));

        var result = await svc.PublishAimAsync(ValidBody());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.NoResponse, result.Reason);
    }
}

/// <summary>테스트용 INatsSetupModel 스텁</summary>
file class StubAimNatsSetupModel : INatsSetupModel
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
