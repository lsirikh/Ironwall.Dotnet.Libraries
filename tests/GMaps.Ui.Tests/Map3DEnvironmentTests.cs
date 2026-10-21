using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 환경(env3d-contract A · B · C) 순수 부분 — 설정 모델 기본값 · 정규화 · 직렬화, env.set 만들기 · env.state 해석,
/// 기상청 격자 변환 · 발표 시각 규칙 · 응답 해석 · 실패 종류(가짜 HTTP 처리기 — 실제 외부 호출 없음) · 인증키 가림.
/// </summary>
public class Map3DEnvironmentTests
{
    private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

    // ── A. 설정 모델 ──

    [Fact]
    public void should_match_contract_defaults_when_constructed()
    {
        var m = new Map3DEnvironmentModel();
        Assert.Equal("manual", m.WeatherMode);   // 폐쇄망 안전
        Assert.Equal("kma", m.WeatherProvider);
        Assert.Equal("https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0", m.KmaBaseUrl);
        Assert.Equal("", m.KmaServiceKey);
        Assert.Equal(30, m.WeatherRefreshMinutes);
        Assert.Equal(("clear", "none", 0.5, 0.0), (m.ManualSky, m.ManualPrecip, m.ManualIntensity, m.ManualFog));
        Assert.Equal(("realtime", "12:00", 10), (m.TimeMode, m.ManualTime, m.CycleMinutesPerSecond));
        Assert.Equal((true, 0.5, true, 0.5), (m.CarsEnabled, m.CarDensity, m.PeopleEnabled, m.PeopleDensity));
        Assert.False(m.Normalize());   // 기본값은 고칠 것이 없다
    }

    [Fact]
    public void should_clamp_and_fix_unknown_values_when_normalized()
    {
        var m = new Map3DEnvironmentModel
        {
            WeatherMode = "AUTO?", WeatherRefreshMinutes = 5, ManualSky = "rainbow", ManualPrecip = "hail", ManualIntensity = 3, ManualFog = double.NaN,
            TimeMode = "warp", ManualTime = "25:00", CycleMinutesPerSecond = 500, CarDensity = -1, PeopleDensity = 9, KmaBaseUrl = "ftp://x",
        };
        Assert.True(m.Normalize());
        Assert.Equal(("manual", 10, "clear", "none"), (m.WeatherMode, m.WeatherRefreshMinutes, m.ManualSky, m.ManualPrecip));
        Assert.Equal((1.0, 0.0), (m.ManualIntensity, m.ManualFog));
        Assert.Equal(("realtime", "12:00", 120), (m.TimeMode, m.ManualTime, m.CycleMinutesPerSecond));
        Assert.Equal((0.0, 1.0), (m.CarDensity, m.PeopleDensity));
        Assert.Equal(Map3DEnvironmentModel.DefaultKmaBaseUrl, m.KmaBaseUrl);

        var hi = new Map3DEnvironmentModel { WeatherRefreshMinutes = 999, ManualTime = "07:65", KmaBaseUrl = "https://example.test/kma/" };
        hi.Normalize();
        Assert.Equal((180, "12:00", "https://example.test/kma"), (hi.WeatherRefreshMinutes, hi.ManualTime, hi.KmaBaseUrl));
    }

    [Fact]
    public void should_round_trip_with_contract_key_names_when_serialized()
    {
        var m = new Map3DEnvironmentModel { WeatherMode = "auto", KmaServiceKey = "k", TimeMode = "cycle", ManualTime = "06:30", CarsEnabled = false };
        var o = JObject.FromObject(m);
        var expected = new[] { "WeatherMode", "WeatherProvider", "KmaBaseUrl", "KmaServiceKey", "WeatherRefreshMinutes", "ManualSky", "ManualPrecip",
                               "ManualIntensity", "ManualFog", "TimeMode", "ManualTime", "CycleMinutesPerSecond", "CarsEnabled", "CarDensity",
                               "PeopleEnabled", "PeopleDensity" };
        Assert.Equal(expected.OrderBy(x => x), o.Properties().Select(p => p.Name).OrderBy(x => x));
        var back = o.ToObject<Map3DEnvironmentModel>()!;
        Assert.Equal(m.ToString(), back.ToString());
        Assert.Equal("k", back.KmaServiceKey);

        // 옛 설정(키 일부만) — 없는 키는 기본값
        var partial = JObject.Parse("{\"WeatherMode\":\"auto\"}").ToObject<Map3DEnvironmentModel>()!;
        Assert.Equal(("auto", 30, true), (partial.WeatherMode, partial.WeatherRefreshMinutes, partial.PeopleEnabled));
    }

    [Fact]
    public void should_mask_service_key_when_logged()
    {
        Assert.Equal("abcd…", Map3DEnvironmentModel.MaskKey("abcdefghijkl"));
        Assert.Equal("(없음)", Map3DEnvironmentModel.MaskKey(""));
        var m = new Map3DEnvironmentModel { KmaServiceKey = "SECRETKEY123" };
        Assert.DoesNotContain("SECRETKEY123", m.ToString());
        Assert.Contains("SECR…", m.ToString());
    }

    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("06:41", 401)]
    [InlineData("23:59", 1439)]
    [InlineData("24:00", null)]
    [InlineData("12-00", null)]
    [InlineData(null, null)]
    public void should_parse_clock_text(string? text, int? minutes) => Assert.Equal(minutes, Map3DEnvironmentModel.ParseTime(text));

    // ── C. env.set ──

    [Fact]
    public void should_build_env_set_with_manual_weather_and_realtime_now()
    {
        var now = new DateTimeOffset(2026, 10, 21, 14, 30, 5, Kst);
        var m = new Map3DEnvironmentModel { ManualSky = "cloudy", ManualPrecip = "rain", ManualIntensity = 0.7, ManualFog = 0.2, CarDensity = 0.25, PeopleEnabled = false };
        var o = Msg(Map3DEnvironment.SetMessageJson(m, null, now));

        Assert.Equal("env.set", (string?)o["type"]);
        Assert.Equal("realtime", (string?)o["time"]!["mode"]);
        Assert.Equal("2026-10-21T14:30:05+09:00", (string?)o["time"]!["iso"]);
        Assert.Equal(10, (int)o["time"]!["minutes_per_second"]!);
        var w = (JObject)o["weather"]!;
        Assert.Equal(("cloudy", "rain", 0.7, 0.2), ((string?)w["sky"], (string?)w["precip"], (double)w["intensity"]!, (double)w["fog"]!));
        Assert.Equal(("manual", JTokenType.Null, JTokenType.Null), ((string?)w["source"], w["observed"]!.Type, w["temp_c"]!.Type));
        Assert.Equal((0.0, 0.0), ((double)w["wind_mps"]!, (double)w["wind_from_deg"]!));
        var t = (JObject)o["traffic"]!;
        Assert.Equal((true, 0.25, false, 0.5), ((bool)t["cars"]!, (double)t["car_density"]!, (bool)t["people"]!, (double)t["people_density"]!));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("cycle")]
    public void should_send_today_at_picked_time_when_time_is_picked(string mode)
    {
        var now = new DateTimeOffset(2026, 10, 21, 14, 30, 0, Kst);
        var m = new Map3DEnvironmentModel { TimeMode = mode, ManualTime = "06:40", CycleMinutesPerSecond = 30 };
        var o = Msg(Map3DEnvironment.SetMessageJson(m, null, now));
        Assert.Equal(mode, (string?)o["time"]!["mode"]);
        Assert.Equal("2026-10-21T06:40:00+09:00", (string?)o["time"]!["iso"]);
        Assert.Equal(30, (int)o["time"]!["minutes_per_second"]!);
    }

    [Fact]
    public void should_send_no_rain_intensity_when_manual_precip_is_none()
    {
        var m = new Map3DEnvironmentModel { ManualPrecip = "none", ManualIntensity = 0.9 };
        Assert.Equal(0.0, (double)Msg(Map3DEnvironment.SetMessageJson(m, null, DateTimeOffset.Now))["weather"]!["intensity"]!);
    }

    [Fact]
    public void should_use_fetched_weather_only_in_auto_mode()
    {
        var now = new DateTimeOffset(2026, 10, 21, 14, 50, 0, Kst);
        var kma = new WeatherSnapshot("overcast", "snow", 0.6, 0, 3.2, 270, -1.5, "kma", new DateTimeOffset(2026, 10, 21, 14, 0, 0, Kst));
        var auto = new Map3DEnvironmentModel { WeatherMode = "auto" };
        var w = Msg(Map3DEnvironment.SetMessageJson(auto, kma, now))["weather"]!;
        Assert.Equal(("overcast", "snow", "kma", "2026-10-21T14:00:00+09:00"), ((string?)w["sky"], (string?)w["precip"], (string?)w["source"], (string?)w["observed"]));
        Assert.Equal((3.2, 270.0, -1.5), ((double)w["wind_mps"]!, (double)w["wind_from_deg"]!, (double)w["temp_c"]!));

        // 자동인데 한 번도 못 받았다 → 수동 값
        Assert.Equal("manual", (string?)Msg(Map3DEnvironment.SetMessageJson(auto, null, now))["weather"]!["source"]);
        // 수동이면 받은 값이 있어도 수동
        Assert.Equal("manual", (string?)Msg(Map3DEnvironment.SetMessageJson(new Map3DEnvironmentModel(), kma, now))["weather"]!["source"]);
    }

    // ── C. env.state ──

    [Fact]
    public void should_parse_env_state_into_sun_text()
    {
        var msg = Map3DMath.ParseMessage("{\"type\":\"env.state\",\"sun\":{\"elevation_deg\":31.5,\"azimuth_deg\":200,\"sunrise\":\"06:41\",\"sunset\":\"17:52\"},\"cars\":12,\"people\":40}");
        Assert.NotNull(msg?.EnvState);
        var s = msg!.EnvState!;
        Assert.Equal((31.5, 200.0, 12, 40), (s.ElevationDeg, s.AzimuthDeg, s.Cars, s.People));
        Assert.Equal("해 뜸 06:41 · 해 짐 17:52", s.SunText);
    }

    [Fact]
    public void should_keep_message_but_drop_bad_fields_when_env_state_is_malformed()
    {
        var s = Map3DMath.ParseMessage("{\"type\":\"env.state\",\"sun\":{\"elevation_deg\":\"high\",\"sunrise\":\"6시\",\"sunset\":\"17:52\"},\"cars\":-3}")!.EnvState!;
        Assert.Equal((null, null, null, (int?)null), (s.ElevationDeg, s.Sunrise, s.Cars, s.People));
        Assert.Equal("해 짐 17:52", s.SunText);
        Assert.Equal("", Map3DMath.ParseMessage("{\"type\":\"env.state\"}")!.EnvState!.SunText);
    }

    // ── B. 기상청 격자 · 발표 시각 · 값 바꾸기 ──

    [Theory]
    [InlineData(37.5635694444444, 126.980008333333, 60, 127)]   // 서울특별시(기상청 격자 예시 표)
    [InlineData(38.0, 126.0, 43, 136)]                          // 기준점
    [InlineData(33.4996213, 126.5311884, 53, 38)]                // 제주특별자치도 제주시
    [InlineData(35.1798159, 129.0750222, 98, 76)]                // 부산광역시
    public void should_convert_lat_lon_to_kma_grid(double lat, double lon, int nx, int ny)
        => Assert.Equal((nx, ny), KmaWeatherProvider.ToGrid(lat, lon));

    [Theory]
    [InlineData(14, 39, "20261021", "1300")]
    [InlineData(14, 40, "20261021", "1400")]
    [InlineData(0, 10, "20261020", "2300")]   // 자정 넘김 — 전날 23시
    public void should_pick_ncst_base_time(int h, int min, string date, string time)
        => Assert.Equal((date, time), KmaWeatherProvider.NcstBase(new DateTimeOffset(2026, 10, 21, h, min, 0, Kst)));

    [Theory]
    [InlineData(14, 44, "20261021", "1330")]
    [InlineData(14, 45, "20261021", "1430")]
    [InlineData(0, 30, "20261020", "2330")]
    public void should_pick_fcst_base_time(int h, int min, string date, string time)
        => Assert.Equal((date, time), KmaWeatherProvider.FcstBase(new DateTimeOffset(2026, 10, 21, h, min, 0, Kst)));

    [Fact]
    public void should_use_kst_when_clock_is_in_another_zone()
        => Assert.Equal(("20261021", "1400"), KmaWeatherProvider.NcstBase(new DateTimeOffset(2026, 10, 21, 5, 41, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData(0, "none", false)]
    [InlineData(1, "rain", false)]
    [InlineData(2, "sleet", false)]
    [InlineData(3, "snow", false)]
    [InlineData(5, "rain", true)]
    [InlineData(6, "sleet", true)]
    [InlineData(7, "snow", true)]
    [InlineData(4, "none", false)]   // 옛 소나기 코드 — 초단기실황에는 없다
    public void should_map_pty(int pty, string precip, bool weak) => Assert.Equal((precip, weak), KmaWeatherProvider.PrecipOf(pty));

    [Theory]
    [InlineData("1", "none", "clear")]
    [InlineData("2", "none", "partly")]
    [InlineData("3", "none", "cloudy")]
    [InlineData("4", "none", "overcast")]
    [InlineData(null, "none", "clear")]
    [InlineData(null, "rain", "overcast")]
    public void should_map_sky(string? sky, string precip, string expected) => Assert.Equal(expected, KmaWeatherProvider.SkyOf(sky, precip));

    [Theory]
    [InlineData(0.1, 0.3)]
    [InlineData(0.99, 0.3)]
    [InlineData(1, 0.6)]
    [InlineData(4.9, 0.6)]
    [InlineData(5, 0.9)]
    [InlineData(40, 0.9)]
    public void should_map_rain_amount_to_intensity(double mm, double expected) => Assert.Equal(expected, KmaWeatherProvider.IntensityOf(mm));

    [Theory]
    [InlineData("3.5", 3.5)]
    [InlineData("1.0mm 미만", 0.5)]
    [InlineData("강수없음", 0)]
    [InlineData("30.0~50.0mm", 30)]
    public void should_read_rain_text(string text, double mm) => Assert.Equal(mm, KmaWeatherProvider.Rain(text));

    // ── B. 가짜 HTTP 로 한 번 받기 ──

    [Fact]
    public async Task should_fetch_and_map_kma_response_when_service_answers()
    {
        var http = new FakeHttp(req => req.RequestUri!.AbsolutePath.EndsWith("getUltraSrtNcst")
            ? Ok(Ncst(("PTY", "1"), ("RN1", "3.5"), ("T1H", "12.5"), ("WSD", "3.2"), ("VEC", "270"), ("REH", "80")))
            : Ok(Fcst(("SKY", "1500", "3"), ("SKY", "1450", "4"), ("LGT", "1450", "0"))));
        var settings = new Map3DEnvironmentModel { KmaServiceKey = "abc+def/ghi==" };
        var log = new RecordingLog();
        var p = new KmaWeatherProvider(settings, log, http, () => new DateTimeOffset(2026, 10, 21, 14, 50, 0, Kst));

        var w = await p.FetchAsync(37.5635694444444, 126.980008333333);

        Assert.Equal(("overcast", "rain", 0.6, 0.0), (w.Sky, w.Precip, w.Intensity, w.Fog));   // 가장 이른 예보 시각(14:50)의 SKY 4
        Assert.Equal((3.2, 270.0, 12.5, "kma"), (w.WindMps, w.WindFromDeg, w.TempC, w.Source));
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 14, 0, 0, Kst), w.Observed);

        Assert.Equal(2, http.Requests.Count);
        var ncst = http.Requests[0];
        Assert.Equal("https", ncst.Scheme);
        Assert.Contains("/getUltraSrtNcst?", ncst.AbsoluteUri);
        Assert.Contains("base_date=20261021&base_time=1400&nx=60&ny=127", ncst.Query);
        Assert.Contains("serviceKey=abc%2Bdef%2Fghi%3D%3D", ncst.OriginalString);   // 디코딩 키는 인코딩해 보낸다
        Assert.Contains("dataType=JSON", ncst.Query);
        Assert.Contains("base_time=1430", http.Requests[1].Query);
        // 인증키는 로그에 앞 4자만
        Assert.All(log.All, line => Assert.DoesNotContain("abc+def/ghi==", line));
        Assert.Contains(log.All, line => line.Contains("abc+…"));
    }

    [Fact]
    public async Task should_keep_already_encoded_key_as_is()
    {
        var http = new FakeHttp(req => req.RequestUri!.AbsolutePath.EndsWith("getUltraSrtNcst") ? Ok(Ncst(("PTY", "0"), ("REH", "97"))) : Ok(Fcst()));
        var p = new KmaWeatherProvider(new Map3DEnvironmentModel { KmaServiceKey = "abc%2Bdef%3D%3D" }, null, http);
        var w = await p.FetchAsync(37.56, 126.98);
        Assert.Contains("serviceKey=abc%2Bdef%3D%3D&", http.Requests[0].OriginalString);
        Assert.Equal(("clear", "none", 0.0, 0.3), (w.Sky, w.Precip, w.Intensity, w.Fog));   // 습도 95%↑ · 강수 없음 → 안개 약간
    }

    [Fact]
    public async Task should_fail_with_no_key_without_calling_out()
    {
        var http = new FakeHttp(_ => throw new InvalidOperationException("불리면 안 된다"));
        var p = new KmaWeatherProvider(new Map3DEnvironmentModel(), null, http);
        var ex = await Assert.ThrowsAsync<WeatherFetchException>(() => p.FetchAsync(37.5, 127));
        Assert.Equal(WeatherFailure.NoKey, ex.Kind);
        Assert.Empty(http.Requests);
    }

    public static IEnumerable<object[]> Failures()
    {
        const string xmlKey = "<OpenAPI_ServiceResponse><cmmMsgHeader><errMsg>SERVICE ERROR</errMsg><returnAuthMsg>SERVICE_KEY_IS_NOT_REGISTERED_ERROR</returnAuthMsg><returnReasonCode>30</returnReasonCode></cmmMsgHeader></OpenAPI_ServiceResponse>";
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.OK, xmlKey)), WeatherFailure.KeyRejected, "인증키가 맞지 않습니다" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.Unauthorized, "Unauthorized")), WeatherFailure.KeyRejected, "인증키가 맞지 않습니다" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.OK, Header("30", "SERVICE_KEY_IS_NOT_REGISTERED_ERROR"))), WeatherFailure.KeyRejected, "인증키가 맞지 않습니다" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => throw new HttpRequestException("이름을 확인할 수 없습니다")), WeatherFailure.Unreachable, "인터넷에 닿지 않습니다 — 폐쇄망이면 수동으로" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => throw new TaskCanceledException("시간 초과")), WeatherFailure.Timeout, "기상청이 10초 안에 답하지 않습니다 — 폐쇄망이면 수동으로" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.OK, "<html>점검 중</html>")), WeatherFailure.BadResponse, "기상청 응답을 읽지 못했습니다" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.OK, Header("03", "NO_DATA"))), WeatherFailure.BadResponse, "기상청 응답을 읽지 못했습니다" };
        yield return new object[] { (Func<HttpResponseMessage>)(() => Text(HttpStatusCode.InternalServerError, "{}")), WeatherFailure.BadResponse, "기상청 응답을 읽지 못했습니다" };
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task should_classify_failures_in_plain_words(Func<HttpResponseMessage> respond, WeatherFailure kind, string text)
    {
        var p = new KmaWeatherProvider(new Map3DEnvironmentModel { KmaServiceKey = "key" }, null, new FakeHttp(_ => respond()));
        var ex = await Assert.ThrowsAsync<WeatherFetchException>(() => p.FetchAsync(37.5, 127));
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(text, ex.UserText);
    }

    [Fact]
    public void should_time_out_after_ten_seconds() => Assert.Equal(TimeSpan.FromSeconds(10), KmaWeatherProvider.Timeout);

    [Fact]
    public void should_summarize_weather_in_plain_words()
    {
        Assert.Equal("맑음 · 12℃", Map3DEnvironment.Summary(new WeatherSnapshot("clear", "none", 0, 0, 1, 90, 12, "kma", null)));
        Assert.Equal("흐림 · 비 · 8.5℃", Map3DEnvironment.Summary(new WeatherSnapshot("overcast", "rain", 0.6, 0, 1, 90, 8.5, "kma", null)));
        Assert.Equal("구름 많음 · 진눈깨비", Map3DEnvironment.Summary(new WeatherSnapshot("cloudy", "sleet", 0.2, 0, null, null, null, "kma", null)));
    }

    // ── 가짜 ──

    /// <summary>보낸 글 그대로 읽기 — Json.NET 기본(DateParseHandling.DateTime)은 iso 글을 날짜로 바꿔 서식을 잃는다.</summary>
    private static JObject Msg(string json)
        => Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json, new Newtonsoft.Json.JsonSerializerSettings { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })!;

    private static HttpResponseMessage Ok(string json) => Text(HttpStatusCode.OK, json);

    private static HttpResponseMessage Text(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body, Encoding.UTF8) };

    private static string Header(string code, string msg) => $"{{\"response\":{{\"header\":{{\"resultCode\":\"{code}\",\"resultMsg\":\"{msg}\"}}}}}}";

    private static string Ncst(params (string Cat, string Value)[] items)
        => Wrap(new JArray(items.Select(i => new JObject { ["baseDate"] = "20261021", ["baseTime"] = "1400", ["category"] = i.Cat, ["nx"] = 60, ["ny"] = 127, ["obsrValue"] = i.Value })));

    private static string Fcst(params (string Cat, string Time, string Value)[] items)
        => Wrap(new JArray(items.Select(i => new JObject { ["baseDate"] = "20261021", ["baseTime"] = "1430", ["category"] = i.Cat, ["fcstDate"] = "20261021", ["fcstTime"] = i.Time, ["fcstValue"] = i.Value })));

    private static string Wrap(JArray items)
        => new JObject
        {
            ["response"] = new JObject
            {
                ["header"] = new JObject { ["resultCode"] = "00", ["resultMsg"] = "NORMAL_SERVICE" },
                ["body"] = new JObject { ["dataType"] = "JSON", ["items"] = new JObject { ["item"] = items }, ["totalCount"] = items.Count },
            },
        }.ToString();

    private sealed class FakeHttp : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<Uri> Requests { get; } = new();
        public FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class RecordingLog : ILogService
    {
        public List<string> All { get; } = new();
#pragma warning disable CS0067
        public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
        public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => All.Add(msg);
        public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => All.Add(msg);
        public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => All.Add(msg);
    }
}
