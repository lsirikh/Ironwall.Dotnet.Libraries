using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
/****************************************************************************
   Purpose      : 3D 날씨 자동 모드의 제공자 창구(env3d-contract B) — 제공자를 바꿔 끼울 수 있게 인터페이스 하나 + 공통 날씨 값.
                  지금 구현은 기상청(KmaWeatherProvider) 하나.
   Note         : 실패는 WeatherFetchException(종류 + 쉬운 말)로 던진다 — 받는 쪽(Map3DEnvironmentViewModel)이 기록하고 상태 글로 알린다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public interface IWeatherProvider
{
    /// <summary>제공자 이름(env.set weather.source 에 그대로 — "kma").</summary>
    string Source { get; }

    /// <summary>위경도의 지금 날씨 — 실패하면 <see cref="WeatherFetchException"/>.</summary>
    Task<WeatherSnapshot> FetchAsync(double lat, double lon, CancellationToken ct = default);
}

/// <summary>
/// 공통 날씨 값 — Sky = clear · partly · cloudy · overcast, Precip = none · rain · snow · sleet, Intensity · Fog = 0~1,
/// WindFromDeg = 바람이 불어오는 방위(도), Source = "kma" · "manual", Observed = 관측 시각(수동은 null).
/// </summary>
public sealed record WeatherSnapshot(string Sky, string Precip, double Intensity, double Fog,
                                     double? WindMps, double? WindFromDeg, double? TempC, string Source, DateTimeOffset? Observed);

/// <summary>날씨를 못 받은 까닭.</summary>
public enum WeatherFailure { NoKey, KeyRejected, Unreachable, Timeout, BadResponse }

public sealed class WeatherFetchException : Exception
{
    public WeatherFailure Kind { get; }

    public WeatherFetchException(WeatherFailure kind, string detail, Exception? inner = null) : base(detail, inner) => Kind = kind;

    /// <summary>화면에 보일 쉬운 말.</summary>
    public string UserText => TextOf(Kind);

    public static string TextOf(WeatherFailure kind) => kind switch
    {
        WeatherFailure.NoKey => "인증키를 넣어 주세요",
        WeatherFailure.KeyRejected => "인증키가 맞지 않습니다",
        WeatherFailure.Unreachable => "인터넷에 닿지 않습니다 — 폐쇄망이면 수동으로",
        WeatherFailure.Timeout => "기상청이 10초 안에 답하지 않습니다 — 폐쇄망이면 수동으로",
        _ => "기상청 응답을 읽지 못했습니다",
    };
}
