using System;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 3D 환경 설정 — 해(시각) · 날씨(자동 = 기상청 API / 수동 = 직접 고름) · 차 · 사람(env3d-contract A).
                  AppSettings.Map3DEnvironment 에 저장. 키가 없으면 기본값(옛 설정 호환), 범위 밖 · 모르는 값은 Normalize 가 고친다.
   Note         : 날씨 기본은 manual — 폐쇄망에서 바깥 호출을 하지 않는다. KmaServiceKey 는 로그 · 화면에 그대로 쓰지 않는다(MaskKey).
                  저장: MapSettingsHelper.SaveMap3DEnvironmentAsync, 로드: MapSettingsHelper.LoadMap3DEnvironment(부팅 DI 싱글톤).
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public class Map3DEnvironmentModel
{
    public const string ModeAuto = "auto";
    public const string ModeManual = "manual";
    public const string TimeRealtime = "realtime";
    public const string TimeManual = "manual";
    public const string TimeCycle = "cycle";
    public const string DefaultKmaBaseUrl = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0";

    public static readonly string[] Skies = { "clear", "partly", "cloudy", "overcast" };
    public static readonly string[] Precips = { "none", "rain", "snow", "sleet" };
    public static readonly string[] TimeModes = { TimeRealtime, TimeManual, TimeCycle };

    /// <summary>"auto"(기상청 API) | "manual"(직접 고름).</summary>
    public string WeatherMode { get; set; } = ModeManual;
    /// <summary>날씨 제공자 — 지금은 "kma"(기상청) 하나.</summary>
    public string WeatherProvider { get; set; } = "kma";
    public string KmaBaseUrl { get; set; } = DefaultKmaBaseUrl;
    /// <summary>공공데이터포털 인증키(인코딩 · 디코딩 어느 쪽이든).</summary>
    public string KmaServiceKey { get; set; } = "";
    /// <summary>자동 모드 갱신 간격(분, 10~180).</summary>
    public int WeatherRefreshMinutes { get; set; } = 30;
    public string ManualSky { get; set; } = "clear";
    public string ManualPrecip { get; set; } = "none";
    /// <summary>비 · 눈 세기(0~1).</summary>
    public double ManualIntensity { get; set; } = 0.5;
    /// <summary>안개(0~1).</summary>
    public double ManualFog { get; set; }
    /// <summary>"realtime"(지금 시각) | "manual"(고른 시각) | "cycle"(빨리 감기).</summary>
    public string TimeMode { get; set; } = TimeRealtime;
    /// <summary>고른 시각 "HH:mm"(오늘 날짜) — cycle 도 이 시각에서 시작한다.</summary>
    public string ManualTime { get; set; } = "12:00";
    /// <summary>빨리 감기: 1초에 몇 분 흐르나(1~120).</summary>
    public int CycleMinutesPerSecond { get; set; } = 10;
    public bool CarsEnabled { get; set; } = true;
    public double CarDensity { get; set; } = 0.5;
    public bool PeopleEnabled { get; set; } = true;
    public double PeopleDensity { get; set; } = 0.5;

    /// <summary>제자리 정규화 — 불러온 뒤 · 화면에서 바꾼 뒤 부른다. 고친 것이 있으면 true.</summary>
    public bool Normalize()
    {
        string before = ToString() + KmaBaseUrl + KmaServiceKey;
        WeatherMode = WeatherMode == ModeAuto ? ModeAuto : ModeManual;
        WeatherProvider = string.IsNullOrWhiteSpace(WeatherProvider) ? "kma" : WeatherProvider.Trim();
        KmaBaseUrl = Uri.TryCreate((KmaBaseUrl ?? "").Trim().TrimEnd('/'), UriKind.Absolute, out var u) && u.Scheme is "https" or "http"
            ? u.ToString().TrimEnd('/') : DefaultKmaBaseUrl;
        KmaServiceKey = (KmaServiceKey ?? "").Trim();
        WeatherRefreshMinutes = Math.Clamp(WeatherRefreshMinutes, 10, 180);
        ManualSky = Skies.Contains(ManualSky) ? ManualSky : "clear";
        ManualPrecip = Precips.Contains(ManualPrecip) ? ManualPrecip : "none";
        ManualIntensity = Unit(ManualIntensity, 0.5);
        ManualFog = Unit(ManualFog, 0);
        TimeMode = TimeModes.Contains(TimeMode) ? TimeMode : TimeRealtime;
        ManualTime = ParseTime(ManualTime) is { } t ? FormatTime(t) : "12:00";
        CycleMinutesPerSecond = Math.Clamp(CycleMinutesPerSecond, 1, 120);
        CarDensity = Unit(CarDensity, 0.5);
        PeopleDensity = Unit(PeopleDensity, 0.5);
        return before != ToString() + KmaBaseUrl + KmaServiceKey;
    }

    /// <summary>"HH:mm" → 하루 안 분(0~1439), 틀리면 null.</summary>
    public static int? ParseTime(string? text)
        => TimeSpan.TryParseExact((text ?? "").Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1)
            ? (int)t.TotalMinutes : null;

    /// <summary>하루 안 분 → "HH:mm"(범위 밖은 하루로 감는다).</summary>
    public static string FormatTime(int minutes)
    {
        int m = ((minutes % 1440) + 1440) % 1440;
        return $"{m / 60:00}:{m % 60:00}";
    }

    /// <summary>인증키 가리기 — 앞 4자만("abcd…"), 비면 "(없음)".</summary>
    public static string MaskKey(string? key)
        => string.IsNullOrEmpty(key) ? "(없음)" : (key.Length <= 4 ? key : key[..4]) + "…";

    private static double Unit(double v, double fallback) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : fallback;

    public override string ToString()
        => $"Map3DEnvironment[weather={WeatherMode}/{WeatherProvider} key={MaskKey(KmaServiceKey)} every={WeatherRefreshMinutes}m " +
           $"manual={ManualSky}/{ManualPrecip}/{ManualIntensity:0.##}/fog {ManualFog:0.##} time={TimeMode} {ManualTime} x{CycleMinutesPerSecond} " +
           $"cars={CarsEnabled}/{CarDensity:0.##} people={PeopleEnabled}/{PeopleDensity:0.##}]";
}
