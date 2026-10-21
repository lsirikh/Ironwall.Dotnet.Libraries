using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
/****************************************************************************
   Purpose      : 기상청 단기예보 API(공공데이터포털 VilageFcstInfoService_2.0) 날씨 제공자(env3d-contract B).
                  위경도 → 격자(nx, ny, 기상청 LCC 공식) → getUltraSrtNcst(초단기실황: PTY · RN1 · T1H · WSD · VEC · REH)
                  + getUltraSrtFcst(초단기예보: SKY) → 공통 날씨 값.
   Note         : 발표 시각 — 실황은 매시 40분 이후 그 시각(HH00), 예보는 매시 45분 이후 그 시각 30분(HH30), 아직이면 한 시간 앞(KST).
                  시간 초과 10초. HTTPS 인증서 검증은 기본 그대로. 인증키는 로그에 앞 4자만(Map3DEnvironmentModel.MaskKey).
                  주소 · 인증키는 부를 때마다 설정 모델에서 읽는다(설정 화면에서 바꾸면 다음 호출부터).
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public sealed class KmaWeatherProvider : IWeatherProvider, IDisposable
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

    private readonly Map3DEnvironmentModel _settings;
    private readonly HttpClient _http;
    private readonly ILogService? _log;
    private readonly Func<DateTimeOffset> _now;

    /// <param name="handler">시험용 가짜 HTTP 처리기(없으면 기본).</param>
    public KmaWeatherProvider(Map3DEnvironmentModel settings, ILogService? log = null, HttpMessageHandler? handler = null, Func<DateTimeOffset>? now = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log;
        _http = handler != null ? new HttpClient(handler) : new HttpClient();
        _http.Timeout = Timeout;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string Source => "kma";

    public async Task<WeatherSnapshot> FetchAsync(double lat, double lon, CancellationToken ct = default)
    {
        string key = (_settings.KmaServiceKey ?? "").Trim();
        if (key.Length == 0) throw new WeatherFetchException(WeatherFailure.NoKey, "인증키 없음");
        var (nx, ny) = ToGrid(lat, lon);
        var now = _now();
        var (nDate, nTime) = NcstBase(now);
        var (fDate, fTime) = FcstBase(now);
        _log?.Info($"[Weather] 기상청 요청 nx={nx} ny={ny} 실황={nDate} {nTime} 예보={fDate} {fTime} key={Map3DEnvironmentModel.MaskKey(key)}");
        var ncst = await GetItemsAsync("getUltraSrtNcst", key, nDate, nTime, nx, ny, ct).ConfigureAwait(false);
        var fcst = await GetItemsAsync("getUltraSrtFcst", key, fDate, fTime, nx, ny, ct).ConfigureAwait(false);
        return ToSnapshot(ncst, fcst, nDate, nTime);
    }

    private async Task<JArray> GetItemsAsync(string op, string key, string date, string time, int nx, int ny, CancellationToken ct)
    {
        // 포털의 「인코딩」 키(% 가 든 것)는 그대로, 「디코딩」 키는 여기서 인코딩한다 — 두 번 인코딩하면 인증 실패
        string encodedKey = key.Contains('%') ? key : Uri.EscapeDataString(key);
        string url = $"{_settings.KmaBaseUrl.TrimEnd('/')}/{op}?serviceKey={encodedKey}&pageNo=1&numOfRows=100&dataType=JSON"
                   + $"&base_date={date}&base_time={time}&nx={nx}&ny={ny}";
        HttpResponseMessage res;
        try { res = await _http.GetAsync(url, ct).ConfigureAwait(false); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new WeatherFetchException(WeatherFailure.Timeout, $"{op} 시간 초과", ex); }
        catch (HttpRequestException ex) { throw new WeatherFetchException(WeatherFailure.Unreachable, $"{op}: {ex.Message}", ex); }
        using (res)
        {
            string body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new WeatherFetchException(WeatherFailure.KeyRejected, $"{op} HTTP {(int)res.StatusCode}");
            return ParseItems(op, (int)res.StatusCode, body);
        }
    }

    /// <summary>
    /// 응답 본문 → item 배열. 포털 오류는 XML(OpenAPI_ServiceResponse · returnAuthMsg)이나 JSON header.resultCode 로 온다 —
    /// 인증키 문제(SERVICE_KEY · 30/31/32)는 KeyRejected, 그 밖은 BadResponse.
    /// </summary>
    public static JArray ParseItems(string op, int status, string body)
    {
        string text = (body ?? "").Trim();
        if (text.Contains("SERVICE_KEY", StringComparison.OrdinalIgnoreCase) || text.Contains("SERVICE KEY", StringComparison.OrdinalIgnoreCase))
            throw new WeatherFetchException(WeatherFailure.KeyRejected, $"{op} 인증키 거절");
        if (status is < 200 or > 299) throw new WeatherFetchException(WeatherFailure.BadResponse, $"{op} HTTP {status}");
        JObject o;
        try { o = JObject.Parse(text); }
        catch (JsonException ex) { throw new WeatherFetchException(WeatherFailure.BadResponse, $"{op} JSON 아님: {Head(text)}", ex); }
        string? code = o.SelectToken("response.header.resultCode")?.ToString();
        if (code is "30" or "31" or "32") throw new WeatherFetchException(WeatherFailure.KeyRejected, $"{op} resultCode={code}");
        if (code != "00")
            throw new WeatherFetchException(WeatherFailure.BadResponse, $"{op} resultCode={code ?? "-"} {o.SelectToken("response.header.resultMsg")}");
        return o.SelectToken("response.body.items.item") as JArray ?? throw new WeatherFetchException(WeatherFailure.BadResponse, $"{op} item 없음");
    }

    /// <summary>실황 · 예보 item → 공통 날씨 값(관측 시각 = 실황 발표 시각, KST).</summary>
    public static WeatherSnapshot ToSnapshot(JArray ncst, JArray fcst, string baseDate, string baseTime)
    {
        var obs = ncst.OfType<JObject>().Where(i => i["category"] != null)
                      .GroupBy(i => i.Value<string>("category")!).ToDictionary(g => g.Key, g => g.First()["obsrValue"]?.ToString());
        // 예보는 시각마다 한 줄 — 가장 이른 시각의 SKY
        string? sky = fcst.OfType<JObject>().Where(i => i.Value<string>("category") == "SKY")
                          .OrderBy(i => i.Value<string>("fcstDate") + i.Value<string>("fcstTime")).FirstOrDefault()?["fcstValue"]?.ToString();
        int pty = (int)(Num(obs.GetValueOrDefault("PTY")) ?? 0);
        var (precip, weak) = PrecipOf(pty);
        double intensity = precip == "none" ? 0 : weak ? 0.2 : IntensityOf(Rain(obs.GetValueOrDefault("RN1")));
        double? reh = Num(obs.GetValueOrDefault("REH"));
        double fog = precip == "none" && reh >= 95 ? 0.3 : 0;
        DateTimeOffset? observed = DateTime.TryParseExact(baseDate + baseTime, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? new DateTimeOffset(t, Kst) : null;
        return new WeatherSnapshot(SkyOf(sky, precip), precip, intensity, fog,
                                   Num(obs.GetValueOrDefault("WSD")), Num(obs.GetValueOrDefault("VEC")), Num(obs.GetValueOrDefault("T1H")), "kma", observed);
    }

    /// <summary>SKY 1 맑음 → clear · 2 구름 조금 → partly · 3 구름 많음 → cloudy · 4 흐림 → overcast. 없으면 비 · 눈이 오면 overcast, 아니면 clear.</summary>
    public static string SkyOf(string? sky, string precip) => sky?.Trim() switch
    {
        "1" => "clear", "2" => "partly", "3" => "cloudy", "4" => "overcast",
        _ => precip == "none" ? "clear" : "overcast",
    };

    /// <summary>PTY 0 없음 · 1 비 · 2 비/눈 → sleet · 3 눈 · 5 빗방울 → rain(약) · 6 빗방울눈날림 → sleet(약) · 7 눈날림 → snow(약).</summary>
    public static (string Precip, bool Weak) PrecipOf(int pty) => pty switch
    {
        1 => ("rain", false), 2 => ("sleet", false), 3 => ("snow", false),
        5 => ("rain", true), 6 => ("sleet", true), 7 => ("snow", true),
        _ => ("none", false),
    };

    /// <summary>1시간 강수량(mm) → 세기: 1 미만 약 0.3 · 1~5 보통 0.6 · 5 이상 강 0.9.</summary>
    public static double IntensityOf(double mm) => mm < 1 ? 0.3 : mm < 5 ? 0.6 : 0.9;

    /// <summary>RN1 값 — 숫자("1.5") · "1.0mm 미만"(→ 0.5) · "강수없음"(→ 0).</summary>
    public static double Rain(string? v)
    {
        if (Num(v) is double d) return d;
        if (v == null) return 0;
        if (v.Contains("미만")) return 0.5;
        var digits = new string(v.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    /// <summary>실황 발표 시각(KST) — 매시 40분 이후 그 시각 HH00, 아니면 한 시간 앞.</summary>
    public static (string Date, string Time) NcstBase(DateTimeOffset now)
    {
        var t = now.ToOffset(Kst);
        if (t.Minute < 40) t = t.AddHours(-1);
        return (t.ToString("yyyyMMdd", CultureInfo.InvariantCulture), t.ToString("HH", CultureInfo.InvariantCulture) + "00");
    }

    /// <summary>예보 발표 시각(KST) — 매시 45분 이후 그 시각 HH30, 아니면 한 시간 앞.</summary>
    public static (string Date, string Time) FcstBase(DateTimeOffset now)
    {
        var t = now.ToOffset(Kst);
        if (t.Minute < 45) t = t.AddHours(-1);
        return (t.ToString("yyyyMMdd", CultureInfo.InvariantCulture), t.ToString("HH", CultureInfo.InvariantCulture) + "30");
    }

    /// <summary>위경도 → 기상청 동네예보 격자(nx, ny) — 기상청 LCC 공식(격자 5 km, 표준위도 30 · 60, 기준점 126°E 38°N = (43, 136)).</summary>
    public static (int Nx, int Ny) ToGrid(double lat, double lon)
    {
        const double re0 = 6371.00877, grid = 5.0, slat1d = 30.0, slat2d = 60.0, olond = 126.0, olatd = 38.0, xo = 43, yo = 136;
        const double deg = Math.PI / 180.0;
        double re = re0 / grid, slat1 = slat1d * deg, slat2 = slat2d * deg, olon = olond * deg, olat = olatd * deg;
        double sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) / Math.Log(Math.Tan(Math.PI * 0.25 + slat2 * 0.5) / Math.Tan(Math.PI * 0.25 + slat1 * 0.5));
        double sf = Math.Pow(Math.Tan(Math.PI * 0.25 + slat1 * 0.5), sn) * Math.Cos(slat1) / sn;
        double ro = re * sf / Math.Pow(Math.Tan(Math.PI * 0.25 + olat * 0.5), sn);
        double ra = re * sf / Math.Pow(Math.Tan(Math.PI * 0.25 + lat * deg * 0.5), sn);
        double theta = lon * deg - olon;
        if (theta > Math.PI) theta -= 2.0 * Math.PI;
        if (theta < -Math.PI) theta += 2.0 * Math.PI;
        theta *= sn;
        return ((int)Math.Floor(ra * Math.Sin(theta) + xo + 0.5), (int)Math.Floor(ro - ra * Math.Cos(theta) + yo + 0.5));
    }

    private static double? Num(string? v)
        => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) && d > -900 ? d : null;   // -999 등 = 결측

    private static string Head(string s) => s.Length > 80 ? s[..80] : s;

    public void Dispose() => _http.Dispose();
}
