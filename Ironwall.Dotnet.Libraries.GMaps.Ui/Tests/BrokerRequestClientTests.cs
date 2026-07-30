using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// BrokerRequestClient 단위 테스트 — GIS.md v1.5.2 §6.4 REQ+RSP 3분기 + §6.9 문구 매핑.
/// (GIS_Nats_v152_Req_Transition FR-01 / NFR-01 / NFR-02)
/// </summary>
public class BrokerRequestClientTests
{
    private const string SUBJECT = "sensorway.unit001.proxy.lamp-off";
    private const string CMD = "LAMP_OFF";

    private static (BrokerRequestClient client, Mock<INatsService> nats) Create(string? reply)
    {
        var nats = new Mock<INatsService>();
        nats.Setup(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(reply);
        return (new BrokerRequestClient(nats.Object), nats);
    }

    private static LampOffBodyDto Body() => new() { LampIds = new List<int> { 501 } };

    // ── 성공 RSP ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_success_with_reqid_when_rsp_success_true()
    {
        var rsp = "{\"id\":\"r-uuid\",\"m_type\":\"RSP\",\"cmd\":\"LAMP_OFF\",\"from\":\"PidsProxy\",\"body\":null," +
                  "\"success\":true,\"message\":\"처리 완료\",\"req_id\":\"orig-1\",\"created\":\"2026-07-30T10:00:00.000Z\"}";
        var (client, nats) = Create(rsp);

        var result = await client.RequestAsync(SUBJECT, CMD, Body());

        Assert.True(result.Success);
        Assert.Equal(EnumBrokerFailure.None, result.Reason);
        Assert.Equal("처리 완료", result.Message);
        Assert.Equal("orig-1", result.ReqId);

        // 와이어 검증: m_type=REQ, cmd, from=GIS + 기본 타임아웃 5초 (NFR-01)
        nats.Verify(n => n.RequestAsync(
            SUBJECT,
            It.Is<string>(json =>
                json.Contains("\"m_type\":\"REQ\"") &&
                json.Contains("\"cmd\":\"LAMP_OFF\"") &&
                json.Contains("\"from\":\"GIS\"") &&
                json.Contains("\"lamp_ids\":[501]")),
            It.Is<TimeSpan?>(t => t == TimeSpan.FromSeconds(5))),
            Times.Once);
    }

    // ── 서버 거부 (success=false) ───────────────────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_rejected_with_mapped_user_message_when_rsp_success_false()
    {
        var rsp = "{\"id\":\"r2\",\"m_type\":\"RSP\",\"cmd\":\"LAMP_OFF\",\"from\":\"PidsProxy\",\"body\":null," +
                  "\"success\":false,\"message\":\"Lamp not found with id=999\",\"req_id\":\"orig-2\",\"created\":\"2026-07-30T10:00:00.000Z\"}";
        var (client, _) = Create(rsp);

        var result = await client.RequestAsync(SUBJECT, CMD, Body());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Rejected, result.Reason);
        Assert.Equal("Lamp not found with id=999", result.Message);       // 서버 원문 보존
        Assert.Equal("경광등 장비를 찾을 수 없습니다.", result.UserMessage); // §6.9 매핑
        Assert.Equal("orig-2", result.ReqId);
    }

    // ── 무응답 (타임아웃/no-responders → null) ──────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_no_response_when_reply_is_null()
    {
        var (client, _) = Create(reply: null);

        var result = await client.RequestAsync(SUBJECT, CMD, Body());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.NoResponse, result.Reason);
        Assert.Contains("응답 없음", result.UserMessage);   // RISK-01: 거부와 구분되는 문구
        Assert.NotNull(result.ReqId);                        // 발행한 req id는 보존
    }

    // ── RSP 해석 실패 ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_parse_error_when_reply_is_not_valid_envelope()
    {
        var (client, _) = Create(reply: "not-a-json !!");

        var result = await client.RequestAsync(SUBJECT, CMD, Body());

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.ParseError, result.Reason);
    }

    // ── 취소 ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_cancelled_without_publishing_when_token_already_cancelled()
    {
        var (client, nats) = Create(reply: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await client.RequestAsync(SUBJECT, CMD, Body(), ct: cts.Token);

        Assert.False(result.Success);
        Assert.Equal(EnumBrokerFailure.Cancelled, result.Reason);
        nats.Verify(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    // ── 사전 검증 ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "BrokerRequest")]
    public async Task should_return_invalid_when_subject_is_empty()
    {
        var (client, nats) = Create(reply: null);

        var result = await client.RequestAsync("", CMD, Body());

        Assert.Equal(EnumBrokerFailure.Invalid, result.Reason);
        nats.Verify(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    // ── §6.9 문구 매핑 단위 검증 ─────────────────────────────────────────────

    [Theory]
    [Trait("Category", "BrokerRequest")]
    [InlineData("Unknown command: LAMP_OFF", "서버가 명령을 인식하지 못했습니다. (버전 불일치 가능)")]
    [InlineData("Handler error: NPE", "서버 처리 중 오류가 발생했습니다.")]
    [InlineData("Camera not found with id=1", "카메라 장비를 찾을 수 없습니다.")]
    [InlineData("NVR connection failed", "NVR 서버 연결에 실패했습니다.")]
    [InlineData("Timeout waiting for response", "서버 응답 대기 시간을 초과했습니다.")]
    [InlineData("사용자 정의 사유", "사용자 정의 사유")]   // 미분류 → 원문 노출
    public void should_map_server_message_patterns_when_rejected(string server, string expected)
    {
        Assert.Equal(expected, BrokerRequestClient.MapServerMessage(server));
    }
}
