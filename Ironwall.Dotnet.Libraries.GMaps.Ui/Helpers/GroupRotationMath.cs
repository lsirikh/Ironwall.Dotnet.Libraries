using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 선택 묶음 회전 순수 수학(WPF/GMap 무의존, 단위 테스트 가능) — 위경도 ↔ 웹 메르카토르 평면 왕복 ·
                  묶음 중심(경계 상자 중심) · 중심 기준 회전 · 방향 각 누적/정규화 · 드래그 각도 · Shift 스냅.
   Note         : 각도 = 도, **시계 방향 +**(나침반 방위 · 화면 y 아래 방향 좌표와 같은 부호).
                  메르카토르는 등각이라 좁은 범위에서 모양(각도·거리 비)이 그대로 돈다(위경도 직접 회전은 찌그러짐).
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/
internal static class GroupRotationMath
{
    /// <summary>Shift 드래그 스냅 단위(도).</summary>
    public const double SnapStepDegrees = 15.0;

    /// <summary>키보드 회전 한 번 — 기본 1°, Shift 0.1°.</summary>
    public const double KeyStepDegrees = 1.0;
    public const double FineKeyStepDegrees = 0.1;

    // 메르카토르 극 근처 발산 방지(웹 메르카토르 표준 한계 위도)
    private const double MaxLat = 85.05112878;

    /// <summary>위경도 → 웹 메르카토르 평면(단위 구, x=경도 라디안, y=ln tan(π/4+φ/2)). y 는 북쪽 +.</summary>
    public static (double x, double y) ToMercator(double lat, double lng)
    {
        double phi = Math.Clamp(lat, -MaxLat, MaxLat) * Math.PI / 180.0;
        return (lng * Math.PI / 180.0, Math.Log(Math.Tan(Math.PI / 4.0 + phi / 2.0)));
    }

    /// <summary>웹 메르카토르 평면 → 위경도(<see cref="ToMercator"/> 의 역).</summary>
    public static (double lat, double lng) FromMercator(double x, double y)
        => ((2.0 * Math.Atan(Math.Exp(y)) - Math.PI / 2.0) * 180.0 / Math.PI, x * 180.0 / Math.PI);

    /// <summary>묶음 중심 = 메르카토르 평면의 경계 상자 중심. 점이 없으면 null.</summary>
    public static (double lat, double lng)? BoundsCenter(IEnumerable<(double lat, double lng)> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var p in points)
        {
            var (x, y) = ToMercator(p.lat, p.lng);
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            any = true;
        }
        if (!any) return null;
        return FromMercator((minX + maxX) / 2.0, (minY + maxY) / 2.0);
    }

    /// <summary>점을 중심 기준으로 시계 방향 <paramref name="degrees"/>° 회전(메르카토르 평면에서 회전 후 위경도로).</summary>
    public static (double lat, double lng) RotateAround((double lat, double lng) point, (double lat, double lng) center, double degrees)
    {
        var (px, py) = ToMercator(point.lat, point.lng);
        var (cx, cy) = ToMercator(center.lat, center.lng);
        double r = degrees * Math.PI / 180.0, cos = Math.Cos(r), sin = Math.Sin(r);
        double dx = px - cx, dy = py - cy;
        // y 북쪽 + 평면에서 시계 방향: 북(0,1) → 동(1,0)
        return FromMercator(cx + dx * cos + dy * sin, cy - dx * sin + dy * cos);
    }

    /// <summary>방향 각 정규화 [0, 360). NaN/무한 → 0.</summary>
    public static double Normalize360(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return 0.0;
        double d = degrees % 360.0;
        if (d < 0) d += 360.0;
        return d >= 360.0 ? 0.0 : d + 0.0;   // +0.0 = -0 제거
    }

    /// <summary>회전량 정규화 (-180, 180] — 드래그 누적이 한 바퀴를 넘어도 가장 가까운 같은 방향으로.</summary>
    public static double WrapSigned(double degrees)
    {
        double d = Normalize360(degrees);
        return d > 180.0 ? d - 360.0 : d;
    }

    /// <summary>방향 각에 회전량을 더한 새 방향 [0,360).</summary>
    public static double AddBearing(double bearing, double delta) => Normalize360(bearing + delta);

    /// <summary>화면 좌표(y 아래 +)에서 중심 → 점 방향 각(도, 시계 방향 +).</summary>
    public static double ScreenAngle(double cx, double cy, double px, double py)
        => Math.Atan2(py - cy, px - cx) * 180.0 / Math.PI;

    /// <summary>드래그 회전량 — 누른 점과 현재 점의 중심 기준 각 차이(-180,180]. <paramref name="snap"/>(Shift)=15° 단위.</summary>
    public static double DragAngle(double cx, double cy, double startX, double startY, double curX, double curY, bool snap)
    {
        double a = WrapSigned(ScreenAngle(cx, cy, curX, curY) - ScreenAngle(cx, cy, startX, startY));
        return snap ? Snap(a, SnapStepDegrees) : a;
    }

    /// <summary>가장 가까운 <paramref name="step"/> 배수로 맞춤(step ≤ 0 이면 그대로).</summary>
    public static double Snap(double degrees, double step)
        => step > 0 ? WrapSigned(Math.Round(degrees / step, MidpointRounding.AwayFromZero) * step) : degrees;

    /// <summary>화면 점을 중심 기준 시계 방향 회전(손잡이 위치 계산용).</summary>
    public static (double x, double y) RotateScreen(double cx, double cy, double px, double py, double degrees)
    {
        double r = degrees * Math.PI / 180.0, cos = Math.Cos(r), sin = Math.Sin(r);
        double dx = px - cx, dy = py - cy;
        // y 아래 + 화면에서 시계 방향: 위(0,-1) → 오른쪽(1,0)
        return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
    }

    /// <summary>회전량 표시 글 — "+12.0°" / "-0.5°" / "0.0°".</summary>
    public static string FormatAngle(double degrees)
    {
        double d = Math.Round(WrapSigned(degrees), 1);
        if (d == 0) return "0.0°";
        return (d > 0 ? "+" : "") + d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "°";
    }
}
