using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;

/****************************************************************************
   Purpose      : GPS 좌표 표시 서식(순수) — 상세 보기 창 위치 정보
   Created By   : Claude Code
   Created On   : 2026-09-09
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 장비·심볼의 WGS84 좌표를 사람이 읽는 문자열로 만든다. WPF 무의존 순수 함수.
///
/// <para><b>왜 DD 와 DMS 를 같이 쓰나</b>: 십진수 도(DD)는 다른 시스템에 그대로 붙여넣기 좋고,
/// 도분초(DMS)는 지도·군용 좌표를 다루는 사람이 눈으로 읽기 좋다. 상세 창은 둘 다 필요하다.</para>
///
/// <para><b>(0, 0) 은 좌표가 아니라 "미등록"이다</b> — 서버 <c>geolocation</c> 이 비면 모델이 0 으로 남는데,
/// 그걸 기니 만(Gulf of Guinea) 한복판 좌표로 표시하면 "등록된 위치"로 오해한다.</para>
/// </summary>
public static class GeoFormat
{
    /// <summary>좌표가 실제로 등록된 값인가. (0,0)·비유한값은 미등록으로 본다.</summary>
    public static bool HasPosition(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude)
           && (Math.Abs(latitude) > 1e-9 || Math.Abs(longitude) > 1e-9)
           && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180;

    /// <summary>위도 한 줄 — <c>37.392779 (37°23'34.0"N)</c>. 미등록이면 null.</summary>
    public static string? Latitude(double latitude, double longitude)
        => HasPosition(latitude, longitude) ? $"{Decimal(latitude)} ({Dms(latitude, isLatitude: true)})" : null;

    /// <summary>경도 한 줄 — <c>126.967959 (126°58'04.7"E)</c>. 미등록이면 null.</summary>
    public static string? Longitude(double latitude, double longitude)
        => HasPosition(latitude, longitude) ? $"{Decimal(longitude)} ({Dms(longitude, isLatitude: false)})" : null;

    /// <summary>십진수 도 6자리(약 11 cm 정밀도) — 다른 시스템에 붙여넣는 용도.</summary>
    public static string Decimal(double degrees)
        => double.IsFinite(degrees) ? degrees.ToString("F6", CultureInfo.InvariantCulture) : "—";

    /// <summary>도분초 — <c>37°23'34.0"N</c>.</summary>
    public static string Dms(double degrees, bool isLatitude)
    {
        if (!double.IsFinite(degrees)) return "—";
        char hemisphere = isLatitude ? (degrees >= 0 ? 'N' : 'S') : (degrees >= 0 ? 'E' : 'W');
        double abs = Math.Abs(degrees);
        int d = (int)abs;
        double minutesTotal = (abs - d) * 60.0;
        int m = (int)minutesTotal;
        double sec = (minutesTotal - m) * 60.0;

        // 반올림이 60 을 만들면 자리올림한다 — 59.96" 가 "60.0"" 로 찍히면 잘못된 좌표가 된다.
        if (Math.Round(sec, 1) >= 60.0) { sec = 0; m += 1; }
        if (m >= 60) { m = 0; d += 1; }

        return $"{d}°{m:00}'{sec.ToString("00.0", CultureInfo.InvariantCulture)}\"{hemisphere}";
    }

    /// <summary>고도 — 미터. 값이 없으면 null.</summary>
    public static string? Altitude(double? meters)
        => meters is { } v && double.IsFinite(v) ? $"{v.ToString("0.#", CultureInfo.InvariantCulture)} m" : null;

    /// <summary>방위 — 0~360°. 값이 없으면 null.</summary>
    public static string? Heading(double? degrees)
        => degrees is { } v && double.IsFinite(v) ? $"{((v % 360) + 360) % 360:0.#}°" : null;

    /// <summary>
    /// 두 좌표 사이 거리(m) — Haversine. 어느 한쪽이 미등록이면 null.
    /// <para>상세 창은 <b>장비 등록 좌표</b>와 <b>심볼 배치 좌표</b>를 함께 보여준다.
    /// 둘이 벌어져 있으면 "현재위치 적용"을 안 한 심볼이라는 뜻이라, 그 차이를 숫자로 보여주는 편이
    /// 두 탭을 오가며 눈으로 비교하게 두는 것보다 낫다.</para>
    /// </summary>
    public static double? DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        if (!HasPosition(lat1, lon1) || !HasPosition(lat2, lon2)) return null;
        const double earthRadius = 6371000.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>거리 표시 — 1 km 미만은 m, 그 이상은 km.</summary>
    public static string Distance(double meters)
        => meters < 1000
            ? $"{meters.ToString("0.#", CultureInfo.InvariantCulture)} m"
            : $"{(meters / 1000).ToString("0.##", CultureInfo.InvariantCulture)} km";

    /// <summary>심볼과 장비 좌표 차이를 어느 정도부터 눈에 띄게 할지(m).</summary>
    public const double SymbolMismatchWarnMeters = 20.0;
}
