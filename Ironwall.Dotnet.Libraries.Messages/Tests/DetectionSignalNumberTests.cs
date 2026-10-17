using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/****************************************************************************
   Purpose      : 탐지 신호값 실수(#4) 결함 회귀 시험
   Description  : 서버 규격 detail.signal = Optional[float](정수 · 실수), objects[].bbox = List[float].
                  수정 전: signal 이 int? 라 실수 한 건이 목록 쪽 전체를 실패시켰고(REST),
                  JObject.ToObject(NATS) 는 21.5 → 22 로 조용히 반올림했다.
                  수정 후: 실수 그대로 읽고, 정수는 정수 모양(21, 21.0 아님)으로 되돌려 쓴다.
                  한 항목이 정말 깨졌으면(정수 칸에 실수 등) 그 항목만 빼고 나머지를 싣는다.
****************************************************************************/
public class DetectionSignalNumberTests
{
    #region - Helpers -
    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>탐지 3건 쪽 — 가운데 항목의 signal 만 <paramref name="middleSignal"/>(JSON 원문)로 바꾼다.</summary>
    private static string DetectionPage(string middleSignal)
        => "{\"success\":true,\"message\":\"ok\",\"data\":[" +
           "{\"id\":1,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"created_at\":\"2026-10-12T10:00:00+09:00\",\"detail\":{\"signal\":10}}," +
           "{\"id\":2,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"created_at\":\"2026-10-12T10:01:00+09:00\",\"detail\":{\"signal\":" + middleSignal + "}}," +
           "{\"id\":3,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"created_at\":\"2026-10-12T10:02:00+09:00\",\"detail\":{\"signal\":30}}" +
           "],\"pagination\":{\"page\":1,\"limit\":20,\"total\":3,\"total_pages\":1}}";
    #endregion

    #region - REST 목록: 실수 신호 한 건이 쪽을 날리지 않는다 -
    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-001")]
    [InlineData("21", 21d)]
    [InlineData("21.0", 21d)]
    [InlineData("21.5", 21.5d)]
    [InlineData("32.2", 32.2d)]
    public async Task should_read_whole_page_when_one_detection_has_fractional_signal(string raw, double expected)
    {
        var parsed = await Json(DetectionPage(raw)).ToApiListResponseAsync<DetectionEventDto>();

        Assert.True(parsed.Success, parsed.Error?.Details);
        Assert.Equal(3, parsed.Data!.Count);
        Assert.Equal(new[] { 1, 2, 3 }, parsed.Data.Select(d => d.Id));
        Assert.Equal(expected, parsed.Data[1].Detail!.Signal);
        Assert.Null(parsed.SkippedItems);
        Assert.Equal(3, parsed.Pagination!.Total);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-002")]
    public void should_keep_fraction_when_single_detection_has_fractional_signal()
    {
        var json = "{\"id\":2,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"detail\":{\"signal\":21.5}}";

        var dto = JsonConvert.DeserializeObject<DetectionEventDto>(json, ApiMessageHelper.JsonSettings);

        Assert.Equal(21.5, dto!.Detail!.Signal);
    }
    #endregion

    #region - NATS 경로(JObject.ToObject): 반올림 없음 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-003")]
    public void should_not_round_signal_when_nats_body_is_converted_with_toobject()
    {
        var body = JObject.Parse("{\"id\":9,\"type_event\":\"Intrusion\",\"result\":\"PIR_SENSOR\",\"detail\":{\"signal\":21.5}}");

        var dto = body.ToObject<DetectionEventDto>();

        Assert.Equal(21.5, dto!.Detail!.Signal);   // 수정 전: 22 로 조용히 반올림
    }
    #endregion

    #region - 쓰기: 정수는 정수 모양, 실수는 그대로 -
    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-004")]
    [InlineData(21d, "\"signal\":21}")]
    [InlineData(0d, "\"signal\":0}")]
    [InlineData(3400d, "\"signal\":3400}")]
    [InlineData(21.5d, "\"signal\":21.5}")]
    [InlineData(32.2d, "\"signal\":32.2}")]
    public void should_write_signal_in_its_read_shape_when_serialized(double signal, string expectedFragment)
    {
        var json = JsonConvert.SerializeObject(new DetectionDetailDto { Signal = signal });

        Assert.Contains(expectedFragment, json);
        Assert.DoesNotContain(".0}", json);
    }

    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-005")]
    [InlineData("{\"signal\":21}", "{\"signal\":21}")]
    [InlineData("{\"signal\":21.5}", "{\"signal\":21.5}")]
    [InlineData("{\"signal\":32.2,\"vendor_key\":7}", "{\"signal\":32.2,\"vendor_key\":7}")]
    public void should_roundtrip_detail_body_without_changing_number_shape(string read, string expectedWrite)
    {
        // 읽은 detail 을 손대지 않고 PATCH/PUT 본문으로 되돌려 쓰는 경로 — 6.3 등 정수 기대 서버 무회귀
        var dto = JsonConvert.DeserializeObject<DetectionDetailDto>(read);

        var written = JsonConvert.SerializeObject(dto);

        Assert.Equal(expectedWrite, written);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-006")]
    public void should_omit_signal_key_when_signal_is_null()
    {
        // NullValueHandling.Ignore 계약 유지 — 명시적 null 은 서버 422
        var json = JsonConvert.SerializeObject(new DetectionDetailDto { Signal = null, Thumbnail = "t" });

        Assert.DoesNotContain("signal", json);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-007")]
    public void should_write_whole_signal_as_integer_when_replace_body_built()
    {
        var replace = new DetectionEventReplaceDto
        {
            TypeEvent = "Intrusion",
            Result = "PIR_SENSOR",
            Detail = new DetectionDetailDto { Signal = 21 }
        };

        var json = JsonConvert.SerializeObject(replace);

        Assert.Contains("\"signal\":21}", json);
        Assert.DoesNotContain("21.0", json);
    }
    #endregion

    #region - bbox: 정규화 비율(실수) · 픽셀(정수) 모두 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-008")]
    public async Task should_read_page_when_bbox_is_normalized_ratio()
    {
        var body = "{\"success\":true,\"data\":[" +
                   "{\"id\":1,\"type_event\":\"Intrusion\",\"result\":\"AI_DETECT\",\"detail\":{\"signal\":0,\"objects\":[{\"label\":\"person\",\"confidence\":0.9,\"bbox\":[0.168,0.213,0.095,0.453]}]}}," +
                   "{\"id\":2,\"type_event\":\"Intrusion\",\"result\":\"AI_DETECT\",\"detail\":{\"signal\":0,\"objects\":[{\"label\":\"person\",\"confidence\":0.8,\"bbox\":[100,200,50,80]}]}}" +
                   "]}";

        var parsed = await Json(body).ToApiListResponseAsync<DetectionEventDto>();

        Assert.True(parsed.Success, parsed.Error?.Details);
        Assert.Equal(2, parsed.Data!.Count);
        Assert.Equal(new[] { 0.168, 0.213, 0.095, 0.453 }, parsed.Data[0].Detail!.Objects![0].Bbox);
        Assert.Equal(new[] { 100d, 200d, 50d, 80d }, parsed.Data[1].Detail!.Objects![0].Bbox);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-009")]
    public void should_write_pixel_bbox_as_integers_when_serialized()
    {
        var json = JsonConvert.SerializeObject(new DetectedObjectDto { Label = "p", Confidence = 0.5, Bbox = new List<double> { 100, 200, 0.5, 80 } });

        Assert.Contains("\"bbox\":[100,200,0.5,80]", json);
    }
    #endregion

    #region - 목록 한 건이 정말 깨졌을 때: 그 항목만 빼고 나머지를 싣는다 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-010")]
    public async Task should_skip_only_broken_item_and_report_id_and_path_when_one_item_is_malformed()
    {
        // frame_width 는 서버 규격도 int — 실수가 오면 그 항목은 형식 오류다
        var body = "{\"success\":true,\"data\":[" +
                   "{\"id\":1,\"type_event\":\"Intrusion\",\"result\":\"AI_DETECT\",\"created_at\":\"2026-10-12T10:00:00+09:00\",\"detail\":{\"frame_width\":1920}}," +
                   "{\"id\":2,\"type_event\":\"Intrusion\",\"result\":\"AI_DETECT\",\"detail\":{\"frame_width\":12.5}}," +
                   "{\"id\":3,\"type_event\":\"Intrusion\",\"result\":\"AI_DETECT\",\"detail\":{\"signal\":21.5}}" +
                   "],\"pagination\":{\"page\":1,\"limit\":20,\"total\":3,\"total_pages\":1}}";

        var parsed = await Json(body).ToApiListResponseAsync<DetectionEventDto>();

        Assert.True(parsed.Success);
        Assert.Equal(new[] { 1, 3 }, parsed.Data!.Select(d => d.Id));
        Assert.Equal("2026-10-12T10:00:00+09:00", parsed.Data[0].CreatedAt);   // 날짜 문자열을 바꾸지 않는다
        Assert.Equal(21.5, parsed.Data[1].Detail!.Signal);
        var skip = Assert.Single(parsed.SkippedItems!);
        Assert.Equal(1, skip.Index);
        Assert.Equal("2", skip.Id);
        Assert.Equal("data[1].detail.frame_width", skip.Path);
        Assert.Contains("id=2", skip.Describe());
        Assert.Contains("data[1].detail.frame_width", skip.Describe());
        Assert.Equal(3, parsed.Pagination!.Total);   // 서버 총계는 그대로
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-011")]
    public async Task should_fail_as_before_when_envelope_itself_is_broken()
    {
        // data 가 배열이 아니면 되살릴 항목이 없다 — 예전처럼 실패로 알린다
        var parsed = await Json("{\"success\":true,\"data\":{\"signal\":21.5},\"pagination\":\"x\"}").ToApiListResponseAsync<DetectionEventDto>();

        Assert.False(parsed.Success);
        Assert.Equal(ApiErrorCodes.InternalError, parsed.Error!.Code);
    }
    #endregion
}
