using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 측정 리드아웃 단위 포맷터 — 거리 m/km, 면적 m²/ha/km² 자동 전환.
                  Measure_Tools FR-02 (스토리보드 승인 임계·소수·소문자 표기).
   Created On   : 2026-07-24 · Sensorway Co., Ltd.
****************************************************************************/
public static class MeasureFormat
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    /// <summary>거리 표기 — &lt;1000m: "{F1} m" / ≥1000m: "{F2} km".</summary>
    public static string Distance(double meters)
    {
        if (double.IsNaN(meters) || meters < 0) meters = 0;
        return meters < 1000d
            ? meters.ToString("F1", Ci) + " m"
            : (meters / 1000d).ToString("F2", Ci) + " km";
    }

    /// <summary>면적 표기 — &lt;1만: "{F1} m²" / &lt;100만: "{F2} ha" / ≥100만: "{F2} km²".</summary>
    public static string Area(double squareMeters)
    {
        if (double.IsNaN(squareMeters) || squareMeters < 0) squareMeters = 0;
        if (squareMeters < 10_000d)
            return squareMeters.ToString("F1", Ci) + " m²";
        if (squareMeters < 1_000_000d)
            return (squareMeters / 10_000d).ToString("F2", Ci) + " ha";
        return (squareMeters / 1_000_000d).ToString("F2", Ci) + " km²";
    }

    /// <summary>값/단위 분리 — 리드아웃 칩에서 숫자와 단위를 다른 색으로 렌더할 때.
    /// 반환: (숫자문자열, 단위문자열).</summary>
    public static (string value, string unit) DistanceParts(double meters)
    {
        var s = Distance(meters);
        int sp = s.LastIndexOf(' ');
        return (s.Substring(0, sp), s.Substring(sp + 1));
    }

    public static (string value, string unit) AreaParts(double squareMeters)
    {
        var s = Area(squareMeters);
        int sp = s.LastIndexOf(' ');
        return (s.Substring(0, sp), s.Substring(sp + 1));
    }
}
