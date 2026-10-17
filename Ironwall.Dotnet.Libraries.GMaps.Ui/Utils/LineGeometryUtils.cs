using System;
using System.Collections.Generic;
using System.Windows;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : Line/Area(폴리라인·닫힌폴리곤) 심볼 리사이즈용 순수 지오 스케일 수학.
                  DB/컨트롤 무관 — 투영 델리게이트(toLocal/toLatLng)로 화면좌표 프레임 스케일.
   Note         : LineArea_Symbol_Resize FR-01/FR-03. 퇴화·극단(0나눗셈·부호반전·NaN) 가드 포함(§5-C R-08/C6).
                  헤드리스 단위테스트 대상(가짜 선형 투영 주입).
   Created On   : 2026-07-13 · Sensorway Co., Ltd.
****************************************************************************/
public static class LineGeometryUtils
{
    /// <summary>배율 하한/상한(부호반전·0·과대 방지, §5-C S-C6/B8).</summary>
    public const double MIN_SCALE = 0.05;
    public const double MAX_SCALE = 20.0;

    /// <summary>퇴화 가드된 배율 산출: old extent가 ε 미만이면 1(축 스케일 무시), 아니면 new/old를 [MIN,MAX] 클램프(부호 양수).</summary>
    public static double SafeRatio(double oldExtent, double newExtent, double eps = 1e-9)
    {
        if (!double.IsFinite(oldExtent) || Math.Abs(oldExtent) < eps) return 1.0;      // 수직/수평선·중첩점 → 0나눗셈 방지
        double r = newExtent / oldExtent;
        if (!double.IsFinite(r)) return 1.0;
        if (r < MIN_SCALE) r = MIN_SCALE;   // 부호반전(음수)·0 붕괴 방지
        if (r > MAX_SCALE) r = MAX_SCALE;
        return r;
    }

    /// <summary>점 리스트를 center 기준으로 화면좌표 프레임에서 (sx,sy) 배율 스케일. 각 점 독립 변환(줌 왜곡 없음).
    /// 비유한/0 배율은 1로, 변환 결과가 비유한이면 원점 유지(NaN 좌표 DB 유입 차단).</summary>
    public static List<PointLatLng> Scale(
        IReadOnlyList<PointLatLng> points, PointLatLng center, double sx, double sy,
        Func<PointLatLng, Point> toLocal, Func<Point, PointLatLng> toLatLng)
    {
        var result = new List<PointLatLng>(points?.Count ?? 0);
        if (points == null || points.Count == 0) return result;
        if (!double.IsFinite(sx) || sx == 0) sx = 1.0;   // 퇴화 방어
        if (!double.IsFinite(sy) || sy == 0) sy = 1.0;

        var c = toLocal(center);
        foreach (var p in points)
        {
            var lp = toLocal(p);
            var scaled = new Point(c.X + (lp.X - c.X) * sx, c.Y + (lp.Y - c.Y) * sy);
            var geo = toLatLng(scaled);
            // 비유한(NaN/Inf) 좌표는 원점 유지 — DB 손상 차단(§5-C R-08).
            result.Add(double.IsFinite(geo.Lat) && double.IsFinite(geo.Lng) ? geo : p);
        }
        return result;
    }

    /// <summary>점 집합의 위경도 bounding box 중심(스케일 앵커·리사이즈 후 Position, D-06). 빈 리스트면 (0,0).</summary>
    public static PointLatLng BoundsCenter(IReadOnlyList<PointLatLng> points)
    {
        if (points == null || points.Count == 0) return new PointLatLng(0, 0);
        double minLat = double.MaxValue, maxLat = double.MinValue, minLng = double.MaxValue, maxLng = double.MinValue;
        foreach (var p in points)
        {
            if (p.Lat < minLat) minLat = p.Lat;
            if (p.Lat > maxLat) maxLat = p.Lat;
            if (p.Lng < minLng) minLng = p.Lng;
            if (p.Lng > maxLng) maxLng = p.Lng;
        }
        return new PointLatLng((minLat + maxLat) / 2.0, (minLng + maxLng) / 2.0);
    }
}
