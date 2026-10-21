using System;
using System.Collections.Generic;
using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : 3D 환경(해 · 날씨 · 차 · 사람) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DEnvironmentTests).
                  ① GIS → 3D env.set 메시지 ② 3D → GIS env.state 해석 · 해 뜸/짐 글 ③ 판 · 설정 화면의 고르기 목록(쉬운 말) · 날씨 요약 글.
   Note         : env3d-contract C. realtime 은 iso = 지금(엔진이 그 뒤로는 스스로 시계를 따라간다), manual · cycle 은 오늘 날짜의 고른 시각
                  (cycle 은 그 시각에서 빨리 감기 시작). 자동 모드인데 한 번도 못 받았으면 수동 값을 보낸다(source "manual").
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/

/// <summary>3D 가 알려 준 환경 상태(env.state) — 해 고도 · 방위(도), 해 뜸 · 짐("HH:mm"), 지금 차 · 사람 수. 모르는 칸은 null.</summary>
public sealed record Map3DEnvState(double? ElevationDeg, double? AzimuthDeg, string? Sunrise, string? Sunset, int? Cars, int? People)
{
    /// <summary>"해 뜸 06:41 · 해 짐 17:52"(모르는 쪽은 뺀다, 둘 다 모르면 빈 글).</summary>
    public string SunText
        => string.Join(" · ", new[] { Sunrise is { } r ? $"해 뜸 {r}" : null, Sunset is { } s ? $"해 짐 {s}" : null }.Where(x => x != null));
}

public static class Map3DEnvironment
{
    public static readonly IReadOnlyList<KeyValuePair<string, string>> WeatherModes = new[]
    {
        new KeyValuePair<string, string>(Map3DEnvironmentModel.ModeAuto, "자동(기상청)"), new(Map3DEnvironmentModel.ModeManual, "직접 고르기"),
    };

    public static readonly IReadOnlyList<KeyValuePair<string, string>> TimeModes = new[]
    {
        new KeyValuePair<string, string>(Map3DEnvironmentModel.TimeRealtime, "지금 시각"), new(Map3DEnvironmentModel.TimeManual, "고른 시각"),
        new(Map3DEnvironmentModel.TimeCycle, "빨리 감기"),
    };

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Skies = new[]
    {
        new KeyValuePair<string, string>("clear", "맑음"), new("partly", "구름 조금"), new("cloudy", "구름 많음"), new("overcast", "흐림"),
    };

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Precips = new[]
    {
        new KeyValuePair<string, string>("none", "없음"), new("rain", "비"), new("snow", "눈"), new("sleet", "진눈깨비"),
    };

    /// <summary>수동 날씨 — 바람 없음 · 기온 모름.</summary>
    public static WeatherSnapshot Manual(Map3DEnvironmentModel m)
        => new(m.ManualSky, m.ManualPrecip, m.ManualPrecip == "none" ? 0 : m.ManualIntensity, m.ManualFog, 0, 0, null, "manual", null);

    /// <summary>
    /// env.set — {"type":"env.set","time":{mode,iso,minutes_per_second},"weather":{sky,precip,intensity,fog,wind_mps,wind_from_deg,temp_c,source,observed},
    /// "traffic":{cars,car_density,people,people_density}}. weather = 자동이고 받은 값이 있으면 그것, 아니면 수동 값.
    /// </summary>
    public static string SetMessageJson(Map3DEnvironmentModel m, WeatherSnapshot? fetched, DateTimeOffset now)
    {
        var w = m.WeatherMode == Map3DEnvironmentModel.ModeAuto && fetched != null ? fetched : Manual(m);
        return new JObject
        {
            ["type"] = "env.set",
            ["time"] = new JObject
            {
                ["mode"] = m.TimeMode,
                ["iso"] = Iso(TimeOf(m, now)),
                ["minutes_per_second"] = m.CycleMinutesPerSecond,
            },
            ["weather"] = new JObject
            {
                ["sky"] = w.Sky, ["precip"] = w.Precip, ["intensity"] = Math.Round(w.Intensity, 3), ["fog"] = Math.Round(w.Fog, 3),
                ["wind_mps"] = w.WindMps, ["wind_from_deg"] = w.WindFromDeg, ["temp_c"] = w.TempC,
                ["source"] = w.Source, ["observed"] = w.Observed is { } o ? Iso(o) : null,
            },
            ["traffic"] = new JObject
            {
                ["cars"] = m.CarsEnabled, ["car_density"] = Math.Round(m.CarDensity, 3),
                ["people"] = m.PeopleEnabled, ["people_density"] = Math.Round(m.PeopleDensity, 3),
            },
        }.ToString(Formatting.None);
    }

    /// <summary>보낼 시각 — realtime = 지금, manual · cycle = 오늘(now 의 날짜 · 시간대)의 고른 시각.</summary>
    public static DateTimeOffset TimeOf(Map3DEnvironmentModel m, DateTimeOffset now)
    {
        if (m.TimeMode == Map3DEnvironmentModel.TimeRealtime) return now;
        int minutes = Map3DEnvironmentModel.ParseTime(m.ManualTime) ?? 12 * 60;
        return new DateTimeOffset(now.Date.AddMinutes(minutes), now.Offset);
    }

    /// <summary>"2026-10-21T14:30:00+09:00".</summary>
    public static string Iso(DateTimeOffset t) => t.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>env.state 해석 — 형식이 틀린 칸은 null(메시지는 버리지 않는다).</summary>
    public static Map3DEnvState ParseState(JObject o)
    {
        var sun = o["sun"] as JObject;
        return new Map3DEnvState(Finite(sun?["elevation_deg"]), Finite(sun?["azimuth_deg"]), Clock(sun?["sunrise"]), Clock(sun?["sunset"]),
                                 Count(o["cars"]), Count(o["people"]));
    }

    /// <summary>날씨 요약 — "맑음 · 12℃", "흐림 · 비 · 8.5℃"(기온 모르면 뺀다).</summary>
    public static string Summary(WeatherSnapshot w)
    {
        var parts = new List<string> { Label(Skies, w.Sky) };
        if (w.Precip != "none") parts.Add(Label(Precips, w.Precip));
        if (w.TempC is double t) parts.Add(t.ToString("0.#", CultureInfo.InvariantCulture) + "℃");
        return string.Join(" · ", parts);
    }

    public static string Label(IReadOnlyList<KeyValuePair<string, string>> list, string key)
    {
        foreach (var p in list) if (p.Key == key) return p.Value;
        return key;
    }

    private static double? Finite(JToken? t)
        => t?.Type is JTokenType.Float or JTokenType.Integer && t.Value<double>() is var d && double.IsFinite(d) ? d : null;

    private static string? Clock(JToken? t)
        => t?.Type == JTokenType.String && Map3DEnvironmentModel.ParseTime(t.Value<string>()) is { } m ? Map3DEnvironmentModel.FormatTime(m) : null;

    private static int? Count(JToken? t) => t?.Type == JTokenType.Integer && t.Value<long>() is long n && n is >= 0 and <= int.MaxValue ? (int)n : null;
}
