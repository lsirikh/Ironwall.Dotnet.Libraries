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
                  ★ 2026-10-18(실기: 오버레이로 만들면 건물이 하나도 안 서고 그림만 깔림 — 모든 이미지를 mask=true 로 보내
                    map-3d 가 그 범위의 실제 건물을 지웠다): GIS 만들기 방식은 넷 — tiles · photo(사진으로, 건물 유지 · 기본) ·
                    mask(가리기, 보안) · extract(가리기 + 건물 찾기). 서버로는 overlay_mode(ServerOverlayMode) + 이미지마다 mask(MaskFor)로 바꿔 보낸다.
                    옛 설정값 "composite" 는 photo 로 읽는다 — 그 값을 고른 사용자는 "오버레이 포함"(사진)을 원했고, mask 로 읽으면
                    다시 만들어도 실제 건물이 지워진다(2026-10-18 실기: 저장값 composite → 가리기로 되살아남).
                  ★ 2026-10-18(사용자 요청 "오버레이 영역은 오버레이 이미지대로, 나머지는 타일대로"): 기본 방식 = extract —
                    이미지 범위 안은 이미지 그림 + 이미지에서 찾은 건물(지도 건물 자료는 그 안에서 뺀다), 범위 밖은 위성 타일 + 지도 건물 자료.
                    옛 설정값 "composite" 와 비어 있는 마지막 값도 extract 로 읽는다(위 photo 읽기를 대신한다).
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
    /// <summary>위성 타일만 — 오버레이 이미지는 3D 화면에서 덮기만 한다.</summary>
    public const string ModeTiles = "tiles";
    /// <summary>서버 overlay_mode "composite"(이미지를 지형 텍스처에 합성) — GIS 방식으로는 옛 설정값(= <see cref="ModeExtract"/>).</summary>
    public const string ModeComposite = "composite";
    /// <summary>오버레이 영역은 이미지에서 건물 찾기(기본) — 이미지 범위 안은 이미지 그림 + 찾은 건물, 밖은 위성 타일 + 지도 건물(서버 overlay_mode 도 "extract", mask=true).</summary>
    public const string ModeExtract = "extract";
    /// <summary>오버레이를 바닥 사진으로만(지도 건물 유지) — composite + mask=false: 실제 건물은 세우고 지면 · 지붕 그림만 오버레이 사진.</summary>
    public const string ModePhoto = "photo";
    /// <summary>오버레이로 가리기(보안) — composite + mask=true: 그 범위의 실제 건물 · 사진을 지운다.</summary>
    public const string ModeMask = "mask";

    /// <summary>서버가 받는 이미지 크기 상한(50 MB — 1000 단위로 작게 잡는다).</summary>
    public const long MaxImageBytes = 50_000_000;

    private const double MetersPerDegree = 111_320d;

    /// <summary>GIS 만들기 방식 — tiles · photo · mask · extract. 옛 값 "composite" 는 extract, 알 수 없는 값 · 빈 값은 tiles.</summary>
    public static string NormalizeMode(string? mode)
    {
        string m = mode?.Trim().ToLowerInvariant() ?? "";
        return m == ModeComposite ? ModeExtract : m is ModePhoto or ModeMask or ModeExtract ? m : ModeTiles;
    }

    /// <summary>서버 overlay_mode — tiles → "tiles", photo · mask → "composite", extract → "extract".</summary>
    public static string ServerOverlayMode(string? mode) => NormalizeMode(mode) switch
    {
        ModePhoto or ModeMask => ModeComposite,
        var m => m,
    };

    /// <summary>이미지마다 보낼 mask — 가리기 방식(mask · extract)만 true. photo 는 false(실제 건물 · 사진을 지우지 않는다).</summary>
    public static bool MaskFor(string? mode) => NormalizeMode(mode) is ModeMask or ModeExtract;

    /// <summary>방식 고르기(라디오)를 보일지 — 영역과 겹치는 오버레이 이미지가 하나라도 있을 때만. 패널 자체는 줌을 고르게 늘 띄운다(2026-10-18).</summary>
    public static bool ShouldAsk(int overlayCount) => overlayCount > 0;

    /// <summary>패널을 띄우지 않을 때 쓰는 모드 · 띄울 때 처음 골라 둘 모드 — 겹치는 이미지가 없으면 tiles, 있으면 마지막 고른 값(없으면 extract).</summary>
    public static string DefaultMode(string? lastMode, int overlayCount)
        => !ShouldAsk(overlayCount) ? ModeTiles : string.IsNullOrWhiteSpace(lastMode) ? ModeExtract : NormalizeMode(lastMode);

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
    /// 굽기 요청 본문(POST /api/jobs). tiles 면 {"bbox":[...]} 그대로, 아니면 overlay_mode(<see cref="ServerOverlayMode"/>) · overlays 를 더한다.
    /// 이미지의 mask 는 방식이 가리기일 때만 그 이미지 값을 따르고, photo 면 늘 false(<see cref="MaskFor"/>).
    /// 회전은 [0, 360), 투명도는 0~1 로 자른다.
    /// </summary>
    public static JObject BuildBody(double[] bbox, string? mode, IEnumerable<Map3DBakeOverlay>? overlays)
        => BuildBody(bbox, mode, overlays, null);

    /// <summary>
    /// 굽기 요청 본문 — 그 영역의 3D 로 바뀌는 줌(enter_zoom, 2026-10-18)을 함께 보낸다. map-3d 가 scene.json 에 그대로 기록하고
    /// 장면 목록(GET /api/jobs)으로 돌려준다. 옛 서버는 모르는 키를 무시한다. 줌이 없거나 수가 아니면 넣지 않는다.
    /// </summary>
    public static JObject BuildBody(double[] bbox, string? mode, IEnumerable<Map3DBakeOverlay>? overlays, double? enterZoom)
        => BuildBody(bbox, mode, overlays, enterZoom, null);

    /// <summary>
    /// 굽기 요청 본문 — 회전 영역(2026-10-19)이면 rect {lon, lat, width_m, height_m, rotation_deg} 도 넣는다(bbox 는 감싸는 상자 그대로 —
    /// 옛 서버는 rect 를 모르고 bbox 로 굽는다). rect 가 없으면 예전 본문과 똑같다.
    /// </summary>
    public static JObject BuildBody(double[] bbox, string? mode, IEnumerable<Map3DBakeOverlay>? overlays, double? enterZoom, Map3DRotRect? rect)
    {
        var body = new JObject { ["bbox"] = new JArray(bbox.Cast<object>().ToArray()) };
        if (enterZoom is double z && double.IsFinite(z)) body["enter_zoom"] = z;
        if (rect != null) body["rect"] = rect.ToJson();
        string m = NormalizeMode(mode);
        if (m == ModeTiles) return body;
        body["overlay_mode"] = ServerOverlayMode(m);
        bool mask = MaskFor(m);
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
                ["mask"] = mask && o.Mask,
            };
        }));
        return body;
    }

    private static bool AllFinite(params double[] values) => values.All(double.IsFinite);
}
