using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 회전된 3D 영역(2026-10-19) — 지도가 돌아간 상태에서 그린 화면 사각형 그대로(중심 · 가로 · 세로 · 회전각) 영역으로 둔다.
                  회전각 = 그릴 때 지도 방위(화면 위쪽이 가리키는 방위, 북 0 · 시계 — map-3d bearing 규약). 회전 0 이면 영역은 예전처럼 북쪽 정렬 bbox 뿐이다.
   Note         : 위경도 ↔ 사각형 로컬(x 오른쪽 · y 위쪽, m)은 중심 위도의 등장방형 근사 — 1° 길이는 WGS84 묘유선 · 자오선 곡률 반지름.
                  map-3d geo.RotRect 와 같은 식이라 GIS 가 그린 모서리와 map-3d 가 구운 지형 테두리가 cm 안으로 맞는다(구로 셈하면 2 km 에서 수 m 어긋남).
                  WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DRotatedAreaTests).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>회전 영역 — 중심(경도 · 위도) · 가로 · 세로(m) · 회전각(도, [0, 360)).</summary>
public sealed record Map3DRotRect(double Lon, double Lat, double WidthM, double HeightM, double RotationDeg)
{
    private const double A = 6378137d, E2 = 6.69437999014e-3;

    /// <summary>1° 의 길이(m) — (동서, 남북).</summary>
    private (double Kx, double Ky) K()
    {
        double s = Math.Sin(Lat * Math.PI / 180d), w = 1d - E2 * s * s, deg = Math.PI / 180d;
        return (deg * A / Math.Sqrt(w) * Math.Cos(Lat * Math.PI / 180d), deg * A * (1d - E2) / Math.Pow(w, 1.5));
    }

    /// <summary>사각형 로컬(x 오른쪽 · y 위쪽, m) → (경도, 위도).</summary>
    public (double Lon, double Lat) ToLonLat(double x, double y)
    {
        double b = RotationDeg * Math.PI / 180d;
        double e = x * Math.Cos(b) + y * Math.Sin(b), n = -x * Math.Sin(b) + y * Math.Cos(b);
        var (kx, ky) = K();
        return (Lon + e / kx, Lat + n / ky);
    }

    /// <summary>(경도, 위도) → 사각형 로컬(x 오른쪽 · y 위쪽, m).</summary>
    public (double X, double Y) ToLocal(double lon, double lat)
    {
        double b = RotationDeg * Math.PI / 180d;
        var (kx, ky) = K();
        double e = (lon - Lon) * kx, n = (lat - Lat) * ky;
        return (e * Math.Cos(b) - n * Math.Sin(b), e * Math.Sin(b) + n * Math.Cos(b));
    }

    /// <summary>사각형 안인가(경계 포함).</summary>
    public bool Contains(double lon, double lat)
    {
        var (x, y) = ToLocal(lon, lat);
        return Math.Abs(x) <= WidthM / 2d + 1e-6 && Math.Abs(y) <= HeightM / 2d + 1e-6;
    }

    /// <summary>네 모서리(위도, 경도) — 왼쪽 위 · 오른쪽 위 · 오른쪽 아래 · 왼쪽 아래(사각형 로컬 기준, 테두리 · 배지 자리).</summary>
    public IReadOnlyList<(double Lat, double Lon)> Corners()
        => new[] { (-1d, 1d), (1d, 1d), (1d, -1d), (-1d, -1d) }
            .Select(p => { var (lo, la) = ToLonLat(p.Item1 * WidthM / 2d, p.Item2 * HeightM / 2d); return (la, lo); }).ToArray();

    /// <summary>감싸는 북쪽 정렬 bbox [minLon, minLat, maxLon, maxLat] — 1e-5° 여유, 소수 7자리(map-3d RotRect.bbox 와 같은 값). 자료 받기 · 장면 찾기용.</summary>
    public double[] Bbox()
    {
        var c = Corners();
        const double pad = 1e-5;
        return new[]
        {
            Math.Round(c.Min(p => p.Lon) - pad, 7), Math.Round(c.Min(p => p.Lat) - pad, 7),
            Math.Round(c.Max(p => p.Lon) + pad, 7), Math.Round(c.Max(p => p.Lat) + pad, 7),
        };
    }

    /// <summary>굽기 요청 rect — {lon, lat, width_m, height_m, rotation_deg}(map-3d geo.parse_rect).</summary>
    public JObject ToJson() => new()
    {
        ["lon"] = Lon,
        ["lat"] = Lat,
        ["width_m"] = WidthM,
        ["height_m"] = HeightM,
        ["rotation_deg"] = RotationDeg,
    };

    /// <summary>
    /// 서버 rect 해석(scene.json · 장면 목록 · /api/scenes/at). 다섯 값이 수가 아니거나 크기가 0 이하면 null(= bbox 로),
    /// 회전각이 0(360 배수)이면 null(북쪽 정렬 영역).
    /// </summary>
    public static Map3DRotRect? Parse(JToken? t)
    {
        if (t is not JObject o) return null;
        double? Num(string k) => o[k]?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(o[k]!.Value<double>()) ? o[k]!.Value<double>() : null;
        if (Num("lon") is not double lon || Num("lat") is not double lat || Num("width_m") is not double w || Num("height_m") is not double h
            || Num("rotation_deg") is not double r || w <= 0 || h <= 0) return null;
        double rot = Map3DMath.ToMap3DBearing(r);
        return rot == 0d ? null : new Map3DRotRect(lon, lat, w, h, rot);
    }
}

/// <summary>
/// 3D 영역 — 북쪽 정렬 bbox [minLon, minLat, maxLon, maxLat] 와 회전 영역(Rect, 없으면 예전 북쪽 정렬 영역).
/// 회전 영역이면 bbox 는 그것을 감싸는 상자(자료 받기 · 옛 서버 호환)이고, 크기 · 안/밖 · 가운데 · 테두리는 회전 사각형으로 셈한다.
/// </summary>
public sealed record Map3DArea(double[] Bbox, Map3DRotRect? Rect)
{
    /// <summary>(가로 m, 세로 m) — 회전 영역은 실제 변 길이, 아니면 bbox(Map3DGuide.AreaSizeMeters).</summary>
    public (double WidthM, double HeightM) SizeMeters => Rect is { } r ? (r.WidthM, r.HeightM) : Map3DGuide.AreaSizeMeters(Bbox);

    /// <summary>가운데(위도, 경도).</summary>
    public (double Lat, double Lon) Center => Rect is { } r ? (r.Lat, r.Lon) : Map3DGuide.AreaCenter(Bbox);

    /// <summary>지점이 영역 안인가(경계 포함).</summary>
    public bool Contains(double lon, double lat) => Rect?.Contains(lon, lat) ?? Map3DMath.BboxContains(Bbox, lon, lat);

    /// <summary>테두리 네 모서리(위도, 경도) — 왼쪽 위부터 시계 방향. bbox 영역은 북서 · 북동 · 남동 · 남서.</summary>
    public IReadOnlyList<(double Lat, double Lon)> Corners
        => Rect?.Corners()
           ?? (Bbox.Length == 4 ? new[] { (Bbox[3], Bbox[0]), (Bbox[3], Bbox[2]), (Bbox[1], Bbox[2]), (Bbox[1], Bbox[0]) } : Array.Empty<(double, double)>());
}
