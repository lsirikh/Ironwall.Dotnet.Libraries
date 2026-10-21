using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// GOP API 8.0.15 명세 대조(L2) — Messages DTO · 공용 해석기 시험(Messages/Tests 는 다른 작업 소유라 여기에 둔다).
/// </summary>
[Trait("Scenario", "Spec8015")]
public class Spec8015DtoTests
{
    #region - G25 FileGroupDto -
    [Fact]
    public void G25_명세모양_group_name과_server_id_group_id를_읽는다()
    {
        var dto = JsonConvert.DeserializeObject<FileGroupDto>(
            """{"id":1,"server_id":1,"group_id":2,"group_name":"화재경보","files":["music01.mp3","music02.mp3"]}""")!;

        Assert.Equal("화재경보", dto.Name);
        Assert.Equal(1, dto.ServerId);
        Assert.Equal(2, dto.GroupId);
        Assert.Equal(2, dto.Files.Count);
    }

    [Fact]
    public void G25_files_원소가_객체여도_이름을_읽는다()
    {
        var dto = JsonConvert.DeserializeObject<FileGroupDto>(
            """{"id":1,"group_name":"g","files":[{"name":"a.mp3"},{"file_name":"b.wav","size":3},"c.mp3",null]}""")!;

        Assert.Equal(new[] { "a.mp3", "b.wav", "c.mp3" }, dto.Files);
    }

    [Fact]
    public void G25_쓰기는_group_name_키와_문자열배열이다()
    {
        var json = JObject.FromObject(new FileGroupDto { Id = 3, ServerId = 1, GroupId = 2, Name = "n", Files = new() { "a.mp3" } });

        Assert.Equal("n", (string?)json["group_name"]);
        Assert.Null(json["name"]);
        Assert.Null(json["description"]);
        Assert.Equal(new[] { "a.mp3" }, json["files"]!.Values<string>());
    }
    #endregion

    #region - G20 RoiDto.is_exclusion -
    [Fact]
    public void G20_is_exclusion을_읽고_되돌려_싣는다()
    {
        var roi = JsonConvert.DeserializeObject<RoiDto>("""{"id":1,"preset_id":2,"name":"정문","is_exclusion":true}""")!;

        var back = JObject.FromObject(roi);

        Assert.True(back.Value<bool>("is_exclusion"));
    }

    [Fact]
    public void G20_is_exclusion을_못읽었으면_싣지_않는다_구판은_422()
    {
        var roi = JsonConvert.DeserializeObject<RoiDto>("""{"id":1,"preset_id":2,"name":"정문"}""")!;

        Assert.Null(JObject.FromObject(roi)["is_exclusion"]);
    }

    [Fact]
    public void G20_is_exclusion_false도_값이면_싣는다()
    {
        var roi = JsonConvert.DeserializeObject<RoiDto>("""{"id":1,"name":"관심","is_exclusion":false}""")!;

        var token = JObject.FromObject(roi)["is_exclusion"];
        Assert.NotNull(token);
        Assert.False(token!.Value<bool>());
    }
    #endregion

    #region - G29 EnumServerStatus.UNKNOWN -
    [Fact]
    public void G29_UNKNOWN을_읽고_기존_서수는_그대로다()
    {
        Assert.True(Enum.TryParse<EnumServerStatus>("UNKNOWN", out var unknown));
        Assert.Equal(3, (int)unknown);
        Assert.Equal(0, (int)EnumServerStatus.NORMAL);
        Assert.Equal(1, (int)EnumServerStatus.WARNING);
        Assert.Equal(2, (int)EnumServerStatus.ERROR);
    }
    #endregion

    #region - G30 CategoryDetailDto.servers 참조 -
    [Fact]
    public void G30_소속서버는_참조_id_category_server로_읽는다()
    {
        var dto = JsonConvert.DeserializeObject<CategoryDetailDto>(
            """{"id":10,"name":"프록시 서버","category_server":"PROXY","servers":[{"id":6,"category_server":"PROXY"},{"id":7,"category_server":"PROXY"}]}""")!;

        Assert.Equal(new[] { 6, 7 }, dto.ServerReferences.Select(s => s.Id));
        Assert.All(dto.ServerReferences, s => Assert.Equal("PROXY", s.CategoryServer));
    }

    [Fact]
    public void G30_63_전체표현이_와도_참조로_읽힌다()
    {
        var dto = JsonConvert.DeserializeObject<CategoryDetailDto>(
            """{"id":1,"name":"VMS","servers":[{"id":10,"category_id":1,"name":"VMS-01","ip_address":"1.1.1.1","port":80,"status":"NORMAL"}]}""")!;

        var reference = Assert.Single(dto.ServerReferences);
        Assert.Equal(10, reference.Id);
        Assert.Equal("VMS-01", dto.Servers![0].Name);
    }
    #endregion

    #region - G36 방송 음량 -
    [Fact]
    public void G36_방송재생_volume은_늘_실리고_0도_실린다()
    {
        var zero = JObject.Parse(JsonConvert.SerializeObject(new BroadcastPlayBodyDto { SpeakerIds = { 1 }, FileGroupId = 2, Volume = 0 }));
        var dflt = JObject.Parse(JsonConvert.SerializeObject(new BroadcastPlayBodyDto { SpeakerIds = { 1 }, FileGroupId = 2 }));

        Assert.Equal(0, zero.Value<int>("volume"));
        Assert.NotNull(dflt["volume"]);
        Assert.Equal(100, dflt.Value<int>("volume"));
    }

    [Fact]
    public void G36_TTS_volume은_늘_실리고_0도_실린다()
    {
        var zero = JObject.Parse(JsonConvert.SerializeObject(new TtsBodyDto { SpeakerIds = { 1 }, Message = "m", Volume = 0 }));
        var dflt = JObject.Parse(JsonConvert.SerializeObject(new TtsBodyDto { SpeakerIds = { 1 }, Message = "m" }));

        Assert.Equal(0, zero.Value<int>("volume"));
        Assert.Equal(100, dflt.Value<int>("volume"));
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-5, 0)]
    [InlineData(42, 42)]
    public void G36_범위밖_음량은_0에서_100으로_잘라낸다(int given, int expected)
    {
        Assert.Equal(expected, new BroadcastPlayBodyDto { Volume = given }.Volume);
        Assert.Equal(expected, new TtsBodyDto { Volume = given }.Volume);
    }

    [Fact]
    public void G36_보낸사람_칸은_여전히_맨뒤에_붙는다()
    {
        var body = new TtsBodyDto { SpeakerIds = { 1 }, Message = "m", Volume = 30, RequestedBy = "op" };
        var names = JObject.Parse(JsonConvert.SerializeObject(body)).Properties().Select(p => p.Name).ToList();

        Assert.Equal("requested_by", names[^1]);
        Assert.Contains("volume", names);
    }
    #endregion

    #region - G17 SaveWarnings -
    private static ResponseWarningDto W(string code, string? field) => new() { Code = code, Field = field, Message = "server" };

    [Fact]
    public void G17_임계치_경고_여러건을_한문장으로_묶고_한글로_보인다()
    {
        var notices = SaveWarnings.Collect(new[]
        {
            W("UNMATCHED_THRESHOLD", "device_config.thresholds.temperature"),
            W("UNMATCHED_THRESHOLD", "thresholds.humidity.high"),
            W("UNMATCHED_THRESHOLD", "thresholds.temperature"),
        });

        var n = Assert.Single(notices);
        Assert.Equal("저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다(온도, 습도)", n.Text);
    }

    [Fact]
    public void G17_서버없음_경고는_알리고_로그용_경고와_모르는_코드는_무시한다()
    {
        var notices = SaveWarnings.Collect(new[]
        {
            W("READ_ONLY_IGNORED", "id"),
            W("OBSERVED_AT_DEFAULTED", "observed_at"),
            W("SOMETHING_NEW", "x"),
            W("SERVER_ID_MISSING", "server_id"),
        });

        var n = Assert.Single(notices);
        Assert.Equal("SERVER_ID_MISSING", n.Code);
        Assert.DoesNotContain("SERVER", n.Text);
        Assert.StartsWith("저장했지만", n.Text);
    }

    [Fact]
    public void G17_모르는_메트릭은_영어키_대신_측정항목으로_보이고_카탈로그이름이_이긴다()
    {
        var unknown = SaveWarnings.Collect(new[] { W("UNMATCHED_THRESHOLD", "thresholds.zzz_new") });
        Assert.Equal("저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다(측정 항목)", Assert.Single(unknown).Text);

        var labelled = SaveWarnings.Collect(new[] { W("UNMATCHED_THRESHOLD", "thresholds.zzz_new") },
            new Dictionary<string, string> { ["zzz_new"] = "새 항목" });
        Assert.Contains("(새 항목)", Assert.Single(labelled).Text);
    }

    [Fact]
    public void G17_원문_JSON에서도_읽고_없거나_깨지면_빈목록이다()
    {
        const string json = """{"success":true,"data":{},"warnings":[{"field":"thresholds.voltage","code":"UNMATCHED_THRESHOLD","message":"m"}]}""";

        Assert.Contains("(전압)", Assert.Single(SaveWarnings.CollectFromJson(json)).Text);
        Assert.Empty(SaveWarnings.CollectFromJson("{\"success\":true}"));
        Assert.Empty(SaveWarnings.CollectFromJson("not json"));
        Assert.Null(SaveWarnings.ToText(SaveWarnings.Collect((IEnumerable<ResponseWarningDto>?)null)));
    }
    #endregion

    #region - G24 EnclosureMetricDto 시각 -
    [Fact]
    public void G24_measured_at을_읽고_쓰기에는_싣지_않는다()
    {
        var dto = JsonConvert.DeserializeObject<EnclosureMetricDto>("""{"id":3,"measured_at":"2026-09-12T11:00:00+09:00","created_at":"2026-09-12T10:00:00+09:00"}""")!;

        Assert.Equal("2026-09-12T11:00:00+09:00", dto.MeasuredAt);
        Assert.Equal("2026-09-12T11:00:00+09:00", dto.ObservedAtEffective);
        dto.ApplyWriteContract(true);
        Assert.Null(JObject.FromObject(dto)["measured_at"]);
    }
    #endregion
}
