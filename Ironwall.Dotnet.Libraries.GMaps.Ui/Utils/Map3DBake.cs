using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 3D 굽기 옵션(오버레이 이미지 포함 여부) 순수 함수 — 영역과 겹치는 이미지 판정 · 요청 본문 · 기본값 결정.
   Note         : ★ 규약(GIS ↔ map-3d, 2026-10-17): POST /api/jobs 본문에 overlay_mode("tiles" | "composite" | "extract") ·
                    overlays([{id,title,image_path,bounds{left,top,right,bottom},rotation,opacity,mask}]) 를 더한다.
                    tiles 면 overlays 는 보내지 않고 본문을 예전 그대로(bbox 만) 둔다 — 옛 서버와도 맞는다.
                  ★ 서버는 PNG/JPG · 50 MB 이하만 받는다 — 그 밖(TIF 등)은 GIS 가 PNG/JPG 로 바꾼 캐시 파일을 보낸다(NeedsConversion).
                  WPF 무의존 — tests/GMaps.Ui.Tests 가 실코드를 링크해 시험한다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>굽기 요청에 넣는 오버레이 이미지 한 장. 경계는 경도(Left · Right) · 위도(Top · Bottom), Rotation = 시계 방향 도.</summary>
public sealed record Map3DBakeOverlay(
    int Id, string Title, string ImagePath, double Left, double Top, double Right, double Bottom,
    double Rotation, double Opacity, bool Mask);

public static class Map3DBake
{
    /// <summary>위성 타일만 — 오버레이 이미지는 3D 화면에서 덮기만 한다(기본).</summary>
    public const string ModeTiles = "tiles";
    /// <summary>오버레이 이미지를 지형 텍스처에 합성.</summary>
    public const string ModeComposite = "composite";
    /// <summary>합성 + 이미지에서 건물 윤곽을 찾아 3D 건물로.</summary>
    public const string ModeExtract = "extract";

    /// <summary>서버가 받는 이미지 크기 상한(50 MB — 1000 단위로 작게 잡는다).</summary>
    public const long MaxImageBytes = 50_000_000;

    private const double MetersPerDegree = 111_320d;

    /// <summary>알 수 없는 값 · 빈 값은 tiles.</summary>
    public static string NormalizeMode(string? mode)
    {
        string m = mode?.Trim().ToLowerInvariant() ?? "";
        return m is ModeComposite or ModeExtract ? m : ModeTiles;
    }

    /// <summary>선택 패널을 띄울지 — 영역과 겹치는 오버레이 이미지가 하나라도 있을 때만.</summary>
    public static bool ShouldAsk(int overlayCount) => overlayCount > 0;

    /// <summary>패널을 띄우지 않을 때 쓰는 모드 · 띄울 때 처음 골라 둘 모드 — 겹치는 이미지가 없으면 tiles, 있으면 마지막 고른 값.</summary>
    public static string DefaultMode(string? lastMode, int overlayCount)
        => ShouldAsk(overlayCount) ? NormalizeMode(lastMode) : ModeTiles;

    /// <summary>
    /// 이미지(회전 포함 사각형)가 굽기 영역 bbox=[minLon, minLat, maxLon, maxLat] 와 겹치는가.
    /// 회전한 네 모서리를 감싸는 사각형으로 본다(가장자리 근처는 넉넉히 겹친다고 본다 — 빠뜨리는 쪽보다 낫다).
    /// </summary>
    public static bool Intersects(double[] bbox, double left, double top, double right, double bottom, double rotation)
    {
        if (bbox is not { Length: 4 } || !AllFinite(left, top, right, bottom)) return false;
        var (minLon, minLat, maxLon, maxLat) = RotatedExtent(left, top, right, bottom, double.IsFinite(rotation) ? rotation : 0d);
        return minLon <= bbox[2] && maxLon >= bbox[0] && minLat <= bbox[3] && maxLat >= bbox[1];
    }

    /// <summary>회전한 이미지 사각형의 경위도 범위(가운데 기준 회전, 경도는 위도 cos 로 보정).</summary>
    public static (double MinLon, double MinLat, double MaxLon, double MaxLat) RotatedExtent(
        double left, double top, double right, double bottom, double rotation)
    {
        double cLon = (left + right) / 2d, cLat = (top + bottom) / 2d;
        double cos = Math.Max(Math.Cos(cLat * Math.PI / 180d), 1e-6);
        double hx = Math.Abs(right - left) / 2d * cos * MetersPerDegree;   // 반폭(m)
        double hy = Math.Abs(top - bottom) / 2d * MetersPerDegree;         // 반높이(m)
        double r = rotation * Math.PI / 180d;
        double ex = Math.Abs(hx * Math.Cos(r)) + Math.Abs(hy * Math.Sin(r));
        double ey = Math.Abs(hx * Math.Sin(r)) + Math.Abs(hy * Math.Cos(r));
        double dLon = ex / (cos * MetersPerDegree), dLat = ey / MetersPerDegree;
        return (cLon - dLon, cLat - dLat, cLon + dLon, cLat + dLat);
    }

    /// <summary>서버에 그대로 보낼 수 없는 파일인가 — PNG/JPG 가 아니거나 50 MB 를 넘으면 PNG/JPG 캐시로 바꿔 보낸다.</summary>
    public static bool NeedsConversion(string path, long length)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is not (".png" or ".jpg" or ".jpeg") || length > MaxImageBytes;
    }

    /// <summary>
    /// 굽기 요청 본문(POST /api/jobs). tiles 면 {"bbox":[...]} 그대로, 아니면 overlay_mode · overlays 를 더한다.
    /// 회전은 [0, 360), 투명도는 0~1 로 자른다.
    /// </summary>
    public static JObject BuildBody(double[] bbox, string? mode, IEnumerable<Map3DBakeOverlay>? overlays)
    {
        var body = new JObject { ["bbox"] = new JArray(bbox.Cast<object>().ToArray()) };
        string m = NormalizeMode(mode);
        if (m == ModeTiles) return body;
        body["overlay_mode"] = m;
        body["overlays"] = new JArray((overlays ?? Enumerable.Empty<Map3DBakeOverlay>()).Select(o =>
        {
            double r = (double.IsFinite(o.Rotation) ? o.Rotation : 0d) % 360d;
            return new JObject
            {
                ["id"] = o.Id,
                ["title"] = o.Title ?? "",
                ["image_path"] = o.ImagePath,
                ["bounds"] = new JObject { ["left"] = o.Left, ["top"] = o.Top, ["right"] = o.Right, ["bottom"] = o.Bottom },
                ["rotation"] = r < 0 ? r + 360d : r,
                ["opacity"] = Math.Clamp(double.IsFinite(o.Opacity) ? o.Opacity : 1d, 0d, 1d),
                ["mask"] = o.Mask,
            };
        }));
        return body;
    }

    private static bool AllFinite(params double[] values) => values.All(double.IsFinite);
}
