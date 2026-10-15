using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 이슈 #7 G7-4 — GIS 가 보내는 제어 명령(방송 · TTS · 경광등 · 문 개폐 · PTZ_AIM_LOCATION) body 에
/// <c>requested_by</c>(로그인 login_id) · <c>requested_client_id</c>(통제DB 세션 client_id = X-Client-Id)가
/// 실제 서비스 경로로 실리는지, 미로그인이면 <c>requested_by</c> 가 빠지는지 본다(브로커 v2.0.12 §7 머리).
/// </summary>
public class ControlCommandRequesterTests
{
    private const string LoginId = "op01";

    private static IPermissionService Permission(string? loginId = LoginId)
    {
        var p = new Mock<IPermissionService>();
        p.Setup(x => x.LoginId).Returns(loginId);
        p.Setup(x => x.Name).Returns("운영자 표시이름");   // 표시이름은 requested_by 가 아니다
        return p.Object;
    }

    private static INatsSetupModel Setup()
    {
        var s = new Mock<INatsSetupModel>();
        s.Setup(x => x.DomainNats).Returns("sensorway");
        s.Setup(x => x.GroupNats).Returns("unit999");
        return s.Object;
    }

    /// <summary>TBody 별로 실행기 목을 세우고 실려 간 body 를 JSON 으로 모은다.</summary>
    private static void Capture<TBody>(Mock<IBrokerRequestClient> client, List<JObject> sent) where TBody : class
        => client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TBody>(),
                                            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                 .Callback<string, string, TBody, TimeSpan?, CancellationToken>((_, _, b, _, _) => sent.Add(JObject.Parse(JsonConvert.SerializeObject(b))))
                 .ReturnsAsync(BrokerRequestResult.Ok("ok", "r"));

    private static void AssertStamped(JObject body, string? expectedLoginId = LoginId)
    {
        if (expectedLoginId is null) Assert.Null(body.Property("requested_by"));
        else Assert.Equal(expectedLoginId, (string?)body["requested_by"]);
        Assert.Equal(ClientIdentity.Current, (string?)body["requested_client_id"]);
    }

    [Fact]
    public void should_use_install_client_id_not_sso_client_id_when_stamping()
    {
        var body = ControlCommandRequester.Stamp(new BroadcastStopBodyDto(), Permission());

        Assert.Equal(LoginId, body.RequestedBy);
        Assert.Equal(ClientIdentity.Current, body.RequestedClientId);
        Assert.NotEqual("gis-monitoring", body.RequestedClientId);   // SSO OIDC client_id 는 쓰지 않는다
    }

    [Fact]
    public async Task should_carry_both_fields_on_broadcast_play_stop_and_tts_when_logged_in()
    {
        var client = new Mock<IBrokerRequestClient>();
        var sent = new List<JObject>();
        Capture<BroadcastPlayBodyDto>(client, sent);
        Capture<BroadcastStopBodyDto>(client, sent);
        var nats = new Mock<INatsService>();
        string? ttsJson = null;
        nats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, j) => ttsJson = j).Returns(Task.CompletedTask);
        var svc = new BroadcastControlService(nats.Object, Setup(), client.Object, Permission());

        await svc.PublishPlayAsync(101, 5, 1);
        await svc.PublishStopAsync(101);
        await svc.PublishTtsAsync(101, "경계 경보");

        Assert.Equal(2, sent.Count);
        AssertStamped(sent[0]);
        Assert.Equal(5, (int)sent[0]["file_group_id"]!);   // 다른 키는 그대로
        AssertStamped(sent[1]);
        var tts = (JObject)JObject.Parse(ttsJson!)["body"]!;
        AssertStamped(tts);
        Assert.Equal("경계 경보", (string?)tts["message"]);
    }

    [Fact]
    public async Task should_carry_both_fields_on_all_lamp_commands_when_logged_in()
    {
        var client = new Mock<IBrokerRequestClient>();
        var sent = new List<JObject>();
        Capture<LampClearBodyDto>(client, sent);
        Capture<LampOffBodyDto>(client, sent);
        Capture<LampColorSetBodyDto>(client, sent);
        Capture<LampBuzzerSetBodyDto>(client, sent);
        var svc = new LampControlService(client.Object, Setup(), permission: Permission());

        await svc.ClearAsync();
        await svc.OffAsync(new[] { 1 });
        await svc.SetColorAsync(new[] { 1 }, EnumLampColor.Red, EnumLightMode.Blinking);
        await svc.SetBuzzerAsync(new[] { 1 }, EnumBuzzerSound.PiPiPi);

        Assert.Equal(4, sent.Count);
        foreach (var b in sent) AssertStamped(b);
        Assert.Null(sent[0].Property("lamp_ids"));   // 전체 해제(lamp_ids 생략) 계약은 그대로
    }

    [Fact]
    public async Task should_omit_requested_by_but_keep_client_id_when_not_logged_in()
    {
        var client = new Mock<IBrokerRequestClient>();
        var sent = new List<JObject>();
        Capture<LampOffBodyDto>(client, sent);
        Capture<BroadcastStopBodyDto>(client, sent);

        await new LampControlService(client.Object, Setup(), permission: Permission(loginId: null)).OffAsync(new[] { 1 });
        await new BroadcastControlService(Mock.Of<INatsService>(), Setup(), client.Object).PublishStopAsync(101);   // 권한 서비스 미등록

        Assert.Equal(2, sent.Count);
        foreach (var b in sent) AssertStamped(b, expectedLoginId: null);
    }

    [Fact]
    public async Task should_carry_requested_client_id_next_to_requested_by_when_door_command_published()
    {
        var nats = new Mock<INatsService>();
        string? json = null;
        nats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, j) => json = j).Returns(Task.CompletedTask);
        var svc = new DoorControlService(nats.Object, Setup());

        Assert.True(await svc.PublishDoorCommandAsync(77, EnumDeviceType.Gate, "OPEN", requestedBy: LoginId));

        var body = (JObject)JObject.Parse(json!)["body"]!;
        AssertStamped(body);
        Assert.Equal("OPEN", (string?)body["command"]);
    }

    [Fact]
    public async Task should_carry_requested_client_id_when_aim_location_requested()
    {
        var client = new Mock<IBrokerRequestClient>();
        var sent = new List<JObject>();
        Capture<CameraAimLocationBodyDto>(client, sent);
        var svc = new CameraAimControlService(client.Object, Setup());

        await svc.PublishAimAsync(new CameraAimLocationBodyDto { CameraId = 5, Latitude = 37.5, Longitude = 127.0, RequestedBy = LoginId });

        AssertStamped(Assert.Single(sent));
    }
}
