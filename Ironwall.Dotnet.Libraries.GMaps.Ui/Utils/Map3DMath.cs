using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 3D 바꿔 끼우기(map-3d POC) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DMathTests).
                  ① 줌 게이트(진입 17.0 · 이탈 16.5 히스테리시스) ② 방위 변환 ③ 실효줌 ↔ 메르카토르 줌
                  ④ 영역(bbox) · 서버 주소 만들기 ⑤ WebView2 메시지 만들기 · 해석.
   Note         : 방위 — GMap 은 `_rotationMatrix.Angle = -Bearing`(WPF 는 시계 방향 +)이라 지도가 Bearing 만큼 반시계로 돈다.
                  곧 화면 위쪽이 가리키는 방위 = Bearing(북 0°, 시계 방향) — map-3d bearing 과 같은 규약이므로 변환은 범위 정규화뿐이다.
                  줌 — 2D 실효줌 = 타일줌 + 0.5·디지털줌단계지만 실제 배율은 표 {1, 1.25, 1.5, 2} 이다.
                  3D(256 px 타일 메르카토르, DIP)와 같은 축척이 되려면 메르카토르 줌 = 타일줌 + log2(배율) 을 보낸다.
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 카메라 — 경도 · 위도 · 메르카토르 줌 · 화면 위쪽 방위(북 0, 시계) · 기울기(0 = 바로 위).</summary>
public readonly record struct Map3DCamera(double Lon, double Lat, double Zoom, double Bearing, double Pitch);

/// <summary>3D 영역 하나의 게이트 줌 — 3D 로 바뀌는 줌 · 2D 로 돌아가는 줌 · 미리 준비하는 줌(<see cref="Map3DMath.AreaGate"/>).</summary>
public readonly record struct Map3DZoomGate(double Enter, double Exit, double Preload);

/// <summary>3D → GIS 메시지. Type = "ready" | "rendered" | "camera" | "pick" | "overlay.resync" | "progress". camera 일 때만 <see cref="Camera"/>, pick 일 때만 <see cref="Pick"/> 가 있다.</summary>
public sealed record Map3DMessage(string Type, string? Reason, string? Job, Map3DCamera? Camera)
{
    public Map3DPick? Pick { get; init; }

    /// <summary>progress 일 때 0~100(모르면 null). 단계 이름은 <see cref="Reason"/>("page" · "scene" · "model").</summary>
    public double? Progress { get; init; }

    /// <summary>rendered 일 때 3D 가 그린 오버레이 항목 수(모르면 null).</summary>
    public int? Items { get; init; }
}

/// <summary>
/// 3D 클릭 — Action = "click" | "dblclick" | "context". Kind · Id 가 없으면 빈 곳. 화면 좌표는 3D 화면 기준 DIP(없으면 null).
/// Kind = "pids" · "symbol" · "geometric" · "military" · "infra" · "pidsGroup" · "line" · "polygon" · "image" · "track".
/// 추적(track)은 id 가 문자열이라 <see cref="TrackId"/> 에 담고 Id 는 null 이다.
/// </summary>
public sealed record Map3DPick(string Action, string? Kind, int? Id, double? ScreenX, double? ScreenY)
{
    /// <summary>kind = "track" 일 때 추적 id(track.upsert 의 id).</summary>
    public string? TrackId { get; init; }
}

public static class Map3DMath
{
    /// <summary>게이트 진입 실효줌.</summary>
    public const double EnterZoom = 17.0;
    /// <summary>게이트 이탈 줌(3D 가 보고한 줌이 이보다 작으면 2D 로).</summary>
    public const double ExitZoom = 16.5;
    /// <summary>3D 로 들어갈 때 눕힐 기울기(°).</summary>
    public const double DefaultPitchDeg = 60.0;
    /// <summary>장면을 미리 불러오기 시작하는 실효줌("3D 가능" 칩이 보이는 영역 안).</summary>
    public const double PreloadZoom = 15.0;

    /// <summary>
    /// 3D 게이트 판정. 장면이 없으면 항상 false. 직전에 3D(또는 3D 준비 중)였으면 <see cref="ExitZoom"/> 이상 유지,
    /// 아니면 <see cref="EnterZoom"/> 이상에서 진입. NaN 은 false.
    /// </summary>
    public static bool Decide(double zoom, bool wasActive, bool hasScene) => Decide(zoom, wasActive, hasScene, EnterZoom, ExitZoom);

    /// <summary>게이트 판정 — 진입 · 이탈 줌을 설정(Map3DModel.EnterZoom · ExitZoom)에서 받는다.</summary>
    public static bool Decide(double zoom, bool wasActive, bool hasScene, double enterZoom, double exitZoom)
    {
        if (!hasScene || double.IsNaN(zoom)) return false;
        return zoom >= (wasActive ? exitZoom : enterZoom);
    }

    /// <summary>3D 가 보고한 줌으로 2D 로 돌아가야 하는가(&lt; 16.5).</summary>
    public static bool ShouldLeave3D(double reportedZoom) => ShouldLeave3D(reportedZoom, ExitZoom);

    /// <summary>3D 가 보고한 줌으로 2D 로 돌아가야 하는가(&lt; 설정의 이탈 줌).</summary>
    public static bool ShouldLeave3D(double reportedZoom, double exitZoom) => reportedZoom < exitZoom;

    /// <summary>
    /// 3D 표시 중 2D 이동(이벤트 시 지도 이동 · 지도에서 보기)이 끝난 자리에서 3D 를 유지하는가 —
    /// 지금 장면(구운 영역) 안이고 실효줌 ≥ 16.5. 아니면 2D 로 돌아온다. NaN 은 false.
    /// </summary>
    public static bool ShouldStay3DAfterMove(double effectiveZoom, bool insideScene) => ShouldStay3DAfterMove(effectiveZoom, insideScene, ExitZoom);

    /// <summary>2D 이동 뒤 3D 유지 판정 — 이탈 줌을 설정에서 받는다.</summary>
    public static bool ShouldStay3DAfterMove(double effectiveZoom, bool insideScene, double exitZoom)
        => insideScene && !double.IsNaN(effectiveZoom) && effectiveZoom >= exitZoom;

    /// <summary>
    /// 2D 에서 장면을 1×1 대기 상태로 미리 열까 — 지도 가운데가 구운 장면 안이고 실효줌 ≥ 15, 호스트가 그 장면을 아직 열지 않았고
    /// (실패했어도 같은 장면은 다시 열지 않는다 — 진입 때 다시 연다) 다른 장면을 불러오는 중도 아닐 때(한 번에 한 장면).
    /// blocked = 3D 설정 꺼짐 · 3D 표시 · 로딩 중 · 굽는 중 · 입력 모드 중. NaN 은 false.
    /// </summary>
    public static bool ShouldPreload(double effectiveZoom, string? sceneAtCenter, string? hostJob, bool hostLoading, bool blocked)
        => ShouldPreload(effectiveZoom, sceneAtCenter, hostJob, hostLoading, blocked, PreloadZoom);

    /// <summary>미리 불러오기 판정 — 미리 준비하는 줌을 설정에서 받는다.</summary>
    public static bool ShouldPreload(double effectiveZoom, string? sceneAtCenter, string? hostJob, bool hostLoading, bool blocked, double preloadZoom)
        => !blocked && !hostLoading && sceneAtCenter != null && sceneAtCenter != hostJob
           && !double.IsNaN(effectiveZoom) && effectiveZoom >= preloadZoom;

    /// <summary>
    /// 영역 하나의 게이트 줌(2026-10-18, 영역마다 3D 줌) — 진입 = 그 영역 진입 줌, 이탈 = 영역 진입 − (세팅 진입 − 세팅 이탈),
    /// 미리 준비 = min(세팅 미리 준비, 영역 진입 − 1). 영역 진입 줌이 세팅과 같으면 이탈은 세팅 그대로다.
    /// </summary>
    public static Map3DZoomGate AreaGate(double areaEnterZoom, double enterZoom, double exitZoom, double preloadZoom)
        => new(areaEnterZoom, areaEnterZoom - (enterZoom - exitZoom), Math.Min(preloadZoom, areaEnterZoom - 1d));

    /// <summary>
    /// 영역 진입 줌 — GIS 로컬 값(서버가 줌 바꾸기를 모를 때 저장, map3d-areas.json) &gt; 서버 장면의 enter_zoom &gt; 세팅의 3D 최소 줌.
    /// 수가 아닌 값은 건너뛰고, 고른 값은 3D 줌 범위(10~20.5)로 자른다.
    /// </summary>
    public static double ResolveAreaEnterZoom(double? localZoom, double? serverZoom, double settingsEnterZoom)
    {
        double zoom = localZoom is double l && double.IsFinite(l) ? l
                    : serverZoom is double s && double.IsFinite(s) ? s
                    : settingsEnterZoom;
        return Math.Clamp(zoom, Map3DModel.MinZoom, Map3DModel.MaxZoom);
    }

    /// <summary>독점 입력 모드(편집 · 선 그리기 · 측정 · 심볼 배치 · 조준 · 홈/앵커 배치) 중에는 3D 에 들어가지 않는다 — 막으면 true.</summary>
    public static bool IsBlockedByInputMode(bool isEditMode, bool isExclusiveInputMode) => isEditMode || isExclusiveInputMode;

    // ── 방위 ──

    /// <summary>GMap Bearing → map-3d bearing [0, 360). NaN/∞ → 0.</summary>
    public static double ToMap3DBearing(double gmapBearing)
    {
        if (!double.IsFinite(gmapBearing)) return 0d;
        double b = gmapBearing % 360d;
        if (b < 0) b += 360d;
        return b >= 360d || b == 0d ? 0d : b;
    }

    /// <summary>map-3d bearing → GMap 정규 방위 [-180, 180)(RotationMath.NormalizeDeg 와 같은 범위). NaN/∞ → 0.</summary>
    public static double ToGMapBearing(double map3DBearing)
    {
        double b = ToMap3DBearing(map3DBearing);
        return b >= 180d ? b - 360d : b;
    }

    // ── 줌 ──

    /// <summary>2D(타일줌 + 디지털 배율) → 메르카토르 줌.</summary>
    public static double ToMercatorZoom(double tileZoom, double digitalScale)
        => tileZoom + Math.Log2(digitalScale > 0 ? digitalScale : 1d);

    /// <summary>
    /// 메르카토르 줌 → 2D 가 낼 수 있는 실효줌 중 가장 가까운 값(0.5 그리드).
    /// 2D 는 MaxZoom 미만에서 디지털 0 · 1 단계만, MaxZoom 에서 표 전체 단계를 쓴다(SetEffectiveZoom 규약).
    /// </summary>
    public static double ToEffectiveZoom(double mercatorZoom, int minZoom, int maxZoom, IReadOnlyList<double> digitalScales)
    {
        if (!double.IsFinite(mercatorZoom) || digitalScales.Count == 0) return minZoom;
        double best = minZoom, bestErr = double.MaxValue;
        for (int tile = minZoom; tile <= maxZoom; tile++)
        {
            int maxStep = tile < maxZoom ? Math.Min(1, digitalScales.Count - 1) : digitalScales.Count - 1;
            for (int step = 0; step <= maxStep; step++)
            {
                double err = Math.Abs(ToMercatorZoom(tile, digitalScales[step]) - mercatorZoom);
                if (err < bestErr) { bestErr = err; best = tile + 0.5 * step; }
            }
        }
        return best;
    }

    // ── 영역 · 주소 ──

    /// <summary>두 모서리(위도 · 경도) → map-3d bbox [minLon, minLat, maxLon, maxLat].</summary>
    public static double[] BuildBbox(double lat1, double lon1, double lat2, double lon2)
        => new[] { Math.Min(lon1, lon2), Math.Min(lat1, lat2), Math.Max(lon1, lon2), Math.Max(lat1, lat2) };

    /// <summary>bbox 가 지점을 덮는가(경계 포함).</summary>
    public static bool BboxContains(IReadOnlyList<double> bbox, double lon, double lat)
        => bbox.Count == 4 && lon >= bbox[0] && lat >= bbox[1] && lon <= bbox[2] && lat <= bbox[3];

    public static string JobsUrl(string server) => $"{server}/api/jobs";

    public static string JobUrl(string server, string id) => $"{server}/api/jobs/{Uri.EscapeDataString(id)}";

    public static string SceneAtUrl(string server, double lon, double lat)
        => $"{server}/api/scenes/at?lon={Num(lon)}&lat={Num(lat)}";

    /// <summary>3D 화면 주소(embed=1 — 장식 없이 3D 만).</summary>
    /// <param name="lowQuality">원격(RDP) 세션 — 3D 가 저사양(해상도 1배 · 그림자 · AO 끔 · 초당 30장)으로 돈다(<c>&amp;low=1</c>).</param>
    public static string ViewUrl(string server, string job, Map3DCamera cam, bool animate, bool lowQuality = false)
        => $"{server}/view?job={Uri.EscapeDataString(job)}&embed=1&lon={Num(cam.Lon)}&lat={Num(cam.Lat)}&zoom={Num(cam.Zoom)}"
           + $"&bearing={Num(cam.Bearing)}&pitch={Num(cam.Pitch)}&animate={(animate ? 1 : 0)}" + (lowQuality ? "&low=1" : "");

    // ── 메시지 ──

    /// <summary>GIS → 3D 카메라 메시지(PostWebMessageAsJson 용).</summary>
    public static string CameraMessageJson(Map3DCamera cam, bool animate)
        => new JObject
        {
            ["type"] = "camera",
            ["lon"] = cam.Lon,
            ["lat"] = cam.Lat,
            ["zoom"] = cam.Zoom,
            ["bearing"] = cam.Bearing,
            ["pitch"] = cam.Pitch,
            ["animate"] = animate,
        }.ToString(Formatting.None);

    /// <summary>
    /// 3D → GIS 메시지 해석(WebMessageAsJson). 페이지가 문자열로 보냈으면(JSON.stringify) 한 번 더 푼다.
    /// 모르는 형식 · camera 인데 lon/lat/zoom 이 수가 아니면 · pick 인데 action 이 모르는 값이면 null.
    /// </summary>
    public static Map3DMessage? ParseMessage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var token = JToken.Parse(json);
            if (token.Type == JTokenType.String) token = JToken.Parse(token.Value<string>()!);
            if (token is not JObject o) return null;
            string? type = o.Value<string>("type");
            if (type == "ready") return new Map3DMessage(type, null, o["job"]?.ToString(), null);
            if (type == "rendered")
                return new Map3DMessage(type, null, o["job"]?.ToString(), null)
                    { Items = o["items"]?.Type == JTokenType.Integer ? o.Value<int>("items") : null };
            if (type == "overlay.resync") return new Map3DMessage(type, null, null, null);
            if (type == "pick") return ParsePick(o);
            if (type == "progress")
            {
                double? pct = Finite(o["pct"]);
                return new Map3DMessage(type, o["stage"]?.Type == JTokenType.String ? o.Value<string>("stage") : null, null, null)
                    { Progress = pct is double v ? Math.Clamp(v, 0, 100) : null };
            }
            if (type != "camera") return null;
            double? lon = Finite(o["lon"]), lat = Finite(o["lat"]), zoom = Finite(o["zoom"]);
            if (lon == null || lat == null || zoom == null) return null;
            var cam = new Map3DCamera(lon.Value, lat.Value, zoom.Value, Finite(o["bearing"]) ?? 0d, Finite(o["pitch"]) ?? 0d);
            return new Map3DMessage(type, o.Value<string>("reason"), null, cam);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// pick 해석 — action 이 click · dblclick · context 가 아니면 null. kind 와 id 는 둘 다 있을 때만 쓴다(하나라도 없으면 빈 곳).
    /// kind = "track" 은 id 가 문자열(정수여도 문자열로)이라 <see cref="Map3DPick.TrackId"/> 로 넘긴다.
    /// </summary>
    private static Map3DMessage? ParsePick(JObject o)
    {
        string? action = o["action"]?.Type == JTokenType.String ? o.Value<string>("action") : null;
        if (action is not ("click" or "dblclick" or "context")) return null;
        string? kind = o["kind"]?.Type == JTokenType.String ? o.Value<string>("kind") : null;
        var screen = o["screen"] as JObject;
        if (kind == "track")
        {
            string? trackId = o["id"]?.Type is JTokenType.String or JTokenType.Integer ? o["id"]!.ToString() : null;
            return new Map3DMessage("pick", null, null, null)
            {
                Pick = new Map3DPick(action, string.IsNullOrEmpty(trackId) ? null : kind, null, Finite(screen?["x"]), Finite(screen?["y"]))
                    { TrackId = string.IsNullOrEmpty(trackId) ? null : trackId },
            };
        }
        int? id = o["id"]?.Type == JTokenType.Integer ? o.Value<int>("id") : null;
        if (string.IsNullOrEmpty(kind) || id == null) { kind = null; id = null; }
        return new Map3DMessage("pick", null, null, null)
        {
            Pick = new Map3DPick(action, kind, id, Finite(screen?["x"]), Finite(screen?["y"])),
        };
    }

    private static double? Finite(JToken? t)
        => t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) && double.IsFinite(t.Value<double>())
            ? t.Value<double>() : null;

    private static string Num(double v) => v.ToString("0.#########", CultureInfo.InvariantCulture);
}
