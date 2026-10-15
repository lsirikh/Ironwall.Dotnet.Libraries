using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
/// 제어 명령 공통 선택 칸 <c>requested_by</c> · <c>requested_client_id</c>(브로커 v2.0.12 §7 머리 · 이슈 #7 G7-4).
/// 문 · 경광등 · 방송 · 조준 · 팝업 · TTS 본문에 실리고, 값이 없으면 칸을 싣지 않는다.
/// </summary>
public class CommandRequesterTests
{
    private const string ClientId = "gis-7f3a9c2e41b6";

    private sealed class FakeRequester : ICommandRequester
    {
        public string? RequestedBy { get; init; }
        public string? RequestedClientId { get; init; }
    }

    private static JObject Parse(string json)
    {
        using var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    private static (Mock<INatsService> nats, List<(string subject, string json)> sent) Nats()
    {
        var sent = new List<(string, string)>();
        var nats = new Mock<INatsService>();
        nats.Setup(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((s, j, _) => sent.Add((s, j)))
            .ReturnsAsync("{\"id\":\"r-uuid\",\"m_type\":\"RSP\",\"cmd\":\"LAMP_OFF\",\"from\":\"PidsProxy\",\"body\":null," +
                          "\"success\":true,\"message\":\"처리 완료\",\"req_id\":\"orig-1\",\"created\":\"2026-07-30T10:00:00.000Z\"}");
        nats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((s, j) => sent.Add((s, j)))
            .Returns(Task.CompletedTask);
        return (nats, sent);
    }

    private static Mock<INatsSetupModel> Setup()
    {
        var setup = new Mock<INatsSetupModel>();
        setup.Setup(s => s.DomainNats).Returns("sensorway");
        setup.Setup(s => s.GroupNats).Returns("unit001");
        return setup;
    }

    [Theory]
    [InlineData("op01", "sub01", "홍길동", "op01")]       // login_id 가 먼저
    [InlineData(" ", "op02", "홍길동", "op02")]           // 없으면 JWT sub(= login_id)
    [InlineData(null, null, "홍길동", "홍길동")]          // 그것도 없으면 표시이름
    [InlineData(null, " ", null, null)]                   // 모두 없으면 칸 없음
    public void should_pick_login_id_first_for_requested_by(string? loginId, string? tokenUserId, string? name, string? expected)
        => Assert.Equal(expected, CommandRequester.ResolveRequestedBy(loginId, tokenUserId, name));

    [Fact]
    public async Task should_stamp_requester_and_terminal_on_every_control_request_body()
    {
        var (nats, sent) = Nats();
        var client = new BrokerRequestClient(nats.Object, null, new FakeRequester { RequestedBy = "op01", RequestedClientId = ClientId });
        var lamp = new LampControlService(client, Setup().Object);

        var result = await lamp.OffAsync(new List<int> { 3 });

        Assert.True(result.Success);
        var body = (JObject)Parse(Assert.Single(sent).json)["body"]!;
        Assert.Equal(new[] { 3 }, body["lamp_ids"]!.ToObject<int[]>());
        Assert.Equal("op01", (string?)body["requested_by"]);
        Assert.Equal(ClientId, (string?)body["requested_client_id"]);
    }

    [Fact]
    public async Task should_not_overwrite_requested_by_already_in_body_but_fill_empty_one()
    {
        var (nats, sent) = Nats();
        var client = new BrokerRequestClient(nats.Object, null, new FakeRequester { RequestedBy = "op01", RequestedClientId = ClientId });

        await client.RequestAsync("s", "CAMERA_POPUP_OPEN", new CameraPopupOpenBodyDto { TargetClientId = ClientId, CameraId = 5, RequestedBy = "홍길동" });
        await client.RequestAsync("s", "PTZ_AIM_LOCATION", new CameraAimLocationBodyDto { CameraId = 5, Latitude = 37.5, Longitude = 127.0, RequestedBy = "" });

        Assert.Equal("홍길동", (string?)Parse(sent[0].json)["body"]!["requested_by"]);
        Assert.Equal("op01", (string?)Parse(sent[1].json)["body"]!["requested_by"]);
        Assert.Equal(ClientId, (string?)Parse(sent[1].json)["body"]!["requested_client_id"]);
    }

    [Fact]
    public async Task should_omit_both_fields_when_values_are_unknown()
    {
        var (nats, sent) = Nats();
        var client = new BrokerRequestClient(nats.Object, null, new FakeRequester());
        var lamp = new LampControlService(client, Setup().Object);

        await lamp.OffAsync(new List<int> { 3 });

        var body = (JObject)Parse(Assert.Single(sent).json)["body"]!;
        Assert.Null(body.Property("requested_by"));
        Assert.Null(body.Property("requested_client_id"));
    }

    [Fact]
    public async Task should_carry_terminal_and_fallback_requester_on_door_command()
    {
        var (nats, sent) = Nats();
        var door = new DoorControlService(nats.Object, Setup().Object, null, new FakeRequester { RequestedBy = "op01", RequestedClientId = ClientId });

        Assert.True(await door.PublishDoorCommandAsync(77, EnumDeviceType.Gate, "OPEN"));                       // 호출부가 계정을 모르면 공통 값
        Assert.True(await door.PublishDoorCommandAsync(77, EnumDeviceType.Gate, "CLOSE", requestedBy: "op09"));  // 호출부 값이 이긴다

        var first = (JObject)Parse(sent[0].json)["body"]!;
        Assert.Equal("op01", (string?)first["requested_by"]);
        Assert.Equal(ClientId, (string?)first["requested_client_id"]);
        Assert.Equal("op09", (string?)Parse(sent[1].json)["body"]!["requested_by"]);
        Assert.Matches(@"[+-]\d\d:\d\d$", (string?)first["requested_at"]);   // 다시 써도 날짜 형식은 그대로
    }

    [Fact]
    public async Task should_omit_terminal_on_door_command_without_requester()
    {
        var (nats, sent) = Nats();
        var door = new DoorControlService(nats.Object, Setup().Object);

        Assert.True(await door.PublishDoorCommandAsync(77, EnumDeviceType.Enclosure, "OPEN"));

        var body = (JObject)Parse(Assert.Single(sent).json)["body"]!;
        Assert.Null(body.Property("requested_by"));
        Assert.Null(body.Property("requested_client_id"));
    }

    [Fact]
    public async Task should_stamp_tts_publish_body()
    {
        var (nats, sent) = Nats();
        var requester = new FakeRequester { RequestedBy = "op01", RequestedClientId = ClientId };
        var broadcast = new BroadcastControlService(nats.Object, Setup().Object, new BrokerRequestClient(nats.Object, null, requester), requester);

        await broadcast.PublishTtsAsync(4, "대피하십시오");

        var body = (JObject)Parse(Assert.Single(sent).json)["body"]!;
        Assert.Equal("대피하십시오", (string?)body["message"]);
        Assert.Equal("op01", (string?)body["requested_by"]);
        Assert.Equal(ClientId, (string?)body["requested_client_id"]);
    }
}
