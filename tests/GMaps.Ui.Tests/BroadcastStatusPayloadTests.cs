using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// BROADCAST_STATUS 페이로드 해석 — PRD symbol-detail-and-door-control FR-24.
/// <para>실발행을 관측하지 못한 채(VER-04) 규격만 보고 구현한 경로라, 표기가 흔들려도
/// <b>조용히 무시되지 않는지</b>가 이 테스트의 핵심이다 — 못 알아들으면 "방송 중인데 화면은 조용"해진다.</para>
/// </summary>
public class BroadcastStatusPayloadTests
{
    private static JObject Body(string json) => BroadcastStatusPayload.TryReadBody(json)!;

    private const string Spec = @"{""cmd"":""BROADCAST_STATUS"",""body"":{""speaker_id"":7,""status"":""ON""}}";

    [Fact]
    public void should_read_body_when_command_matches()
    {
        var body = BroadcastStatusPayload.TryReadBody(Spec);
        Assert.NotNull(body);
        Assert.Equal(7, body!.Value<int>("speaker_id"));
    }

    [Theory]
    [InlineData(@"{""cmd"":""BROADCAST_PLAY"",""body"":{}}")]     // 다른 명령은 남의 것
    [InlineData(@"{""body"":{}}")]                                 // cmd 없음
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public void should_ignore_when_envelope_is_not_broadcast_status(string? json)
        => Assert.Null(BroadcastStatusPayload.TryReadBody(json));

    [Fact]
    public void should_match_command_case_insensitively()
        => Assert.NotNull(BroadcastStatusPayload.TryReadBody(@"{""cmd"":""broadcast_status"",""body"":{}}"));

    [Theory]
    [InlineData("ON", true)]
    [InlineData("on", true)]
    [InlineData(" On ", true)]
    [InlineData("PLAYING", true)]
    [InlineData("OFF", false)]
    [InlineData("stopped", false)]
    [InlineData("IDLE", false)]
    public void should_parse_status_text(string text, bool expected)
        => Assert.Equal(expected, BroadcastStatusPayload.ParseStatus(JToken.FromObject(text)));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void should_parse_status_boolean(bool value, bool expected)
        => Assert.Equal(expected, BroadcastStatusPayload.ParseStatus(JToken.FromObject(value)));

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void should_parse_status_integer(int value, bool expected)
        => Assert.Equal(expected, BroadcastStatusPayload.ParseStatus(JToken.FromObject(value)));

    [Fact]
    public void should_return_null_when_status_is_unknown()
    {
        // 지어내면 안 된다 — 모르는 값을 OFF 로 치면 송출 중인데 꺼진 것처럼 보인다.
        Assert.Null(BroadcastStatusPayload.ParseStatus(JToken.FromObject("PAUSED")));
        Assert.Null(BroadcastStatusPayload.ParseStatus(null));
        Assert.Null(BroadcastStatusPayload.ParseStatus(JValue.CreateNull()));
    }

    [Fact]
    public void should_read_single_speaker_id()
        => Assert.Equal(new[] { 7 }, BroadcastStatusPayload.ParseSpeakerIds(Body(Spec)));

    [Fact]
    public void should_read_speaker_id_array()
    {
        var body = Body(@"{""cmd"":""BROADCAST_STATUS"",""body"":{""speaker_ids"":[3,5,3],""status"":""ON""}}");
        Assert.Equal(new[] { 3, 5 }, BroadcastStatusPayload.ParseSpeakerIds(body));   // 중복 제거
    }

    [Fact]
    public void should_drop_invalid_speaker_ids()
    {
        var body = Body(@"{""cmd"":""BROADCAST_STATUS"",""body"":{""speaker_ids"":[0,-1,""x"",9],""status"":""OFF""}}");
        Assert.Equal(new[] { 9 }, BroadcastStatusPayload.ParseSpeakerIds(body));
    }

    [Fact]
    public void should_return_empty_when_no_speaker_is_named()
    {
        var body = Body(@"{""cmd"":""BROADCAST_STATUS"",""body"":{""status"":""ON""}}");
        Assert.Empty(BroadcastStatusPayload.ParseSpeakerIds(body));
        Assert.Empty(BroadcastStatusPayload.ParseSpeakerIds(null));
    }

    [Fact]
    public void should_prefer_single_id_over_array_when_both_present()
    {
        var body = Body(@"{""cmd"":""BROADCAST_STATUS"",""body"":{""speaker_id"":4,""speaker_ids"":[1,2],""status"":""ON""}}");
        Assert.Equal(new[] { 4 }, BroadcastStatusPayload.ParseSpeakerIds(body));
    }
}
