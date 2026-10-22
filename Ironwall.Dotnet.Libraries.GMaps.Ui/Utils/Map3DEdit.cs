using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 안에서 편집(edit3d-contract, 2026-10-21) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DEditTests).
                  ① GIS → 3D 메시지(draw.start · draw.cancel · edit.start · edit.stop · edit.revert · edit.key)
                  ② 3D → GIS 메시지 해석(draw.point · draw.done · draw.end · edit.commit · edit.end) ③ 도구 → 그리기 방식.
                  ④ 지도 심볼(2026-10-22): overlay.select · edit.start/edit.revert(target kind "overlay") · edit.commit(overlay) · overlay.box.
   Note         : 메시지 type · 키 이름은 계약 그대로(edit.key · edit.end = 엔진 dac1e69 덧붙임). 좌표는 [경도, 위도] 소수 7자리.
                  판 단추(옮기기 · 돌리기 · 꼭짓점 · 확정)는 edit.key 로 뷰어 키보드와 똑같이 — 미리보기는 뷰어, 저장은 Enter 뒤 edit.commit.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 그리기 결과 — draw.point(점 1개 · 고도) · draw.done(방식 · 점 목록, 다각형은 닫지 않은 점) · draw.end(이유).</summary>
public sealed record Map3DDrawEvent(string? Mode, IReadOnlyList<UbPoint> Points, double? Alt, string? Reason);

/// <summary>3D 손잡이로 고친 결과(edit.commit) — 대상(종류 · uid) · 새 도형(Point · LineString · Polygon, 다각형은 닫는 끝점 없이) · 소품 · 모델 방향.</summary>
public sealed record Map3DEditCommit(string Kind, string Uid, string Geometry, IReadOnlyList<UbPoint> Points, double? HeadingDeg)
{
    /// <summary>넣은 모델 크기(배, 엔진 8cacc6e — 크기 고리 · + − 키). 다른 대상 · 옛 엔진은 null.</summary>
    public double? Scale { get; init; }
}

/// <summary>
/// 3D 손잡이로 고친 지도 심볼(edit.commit target {kind:"overlay", overlayKind, id}) — 점 심볼 = Lon · Lat(+ HeadingDeg),
/// 선 · 구역(pidsGroup · line · polygon) = Points 전체([경도, 위도], 닫는 끝점 없이).
/// </summary>
public sealed record Map3DOverlayEdit(string OverlayKind, int Id, double? Lon, double? Lat, double? HeadingDeg, IReadOnlyList<UbPoint>? Points);

/// <summary>3D {Shift}+끌기 상자 안 지도 심볼(overlay.box) — Add = 지금 고름에 더하기(2D 러버밴드 규칙), false = 상자 안 것으로 바꾸기.</summary>
public sealed record Map3DOverlayBox(IReadOnlyList<(string Kind, int Id)> Items, bool Add);

public static class Map3DEdit
{
    /// <summary>edit.* 대상 종류(overlay = 지도 심볼 — overlayKind · id 로 가리킨다).</summary>
    public const string TargetDecoration = "decoration", TargetUserBuilding = "userbuilding", TargetModel = "model", TargetOverlay = "overlay";
    /// <summary>그리기 방식.</summary>
    public const string ModePoint = "point", ModeLine = "line", ModePolygon = "polygon", ModeRect = "rect", ModeCircle = "circle";
    // ── GIS → 3D ──

    /// <summary>그리기 모드 켜기 — {"type":"draw.start","mode","snap":"ground","kind","preview"}.</summary>
    public static string DrawStartJson(string mode, string kind, JObject? preview)
        => new JObject
        {
            ["type"] = "draw.start", ["mode"] = mode, ["snap"] = "ground", ["kind"] = kind,
            ["preview"] = preview?.DeepClone() ?? new JObject(),
        }.ToString(Formatting.None);

    public static string DrawCancelJson() => new JObject { ["type"] = "draw.cancel" }.ToString(Formatting.None);

    private static JObject Target(string kind, string uid) => new() { ["kind"] = kind, ["uid"] = uid };

    /// <summary>고른 항목에 손잡이 띄우기.</summary>
    public static string EditStartJson(string kind, string uid) => new JObject { ["type"] = "edit.start", ["target"] = Target(kind, uid) }.ToString(Formatting.None);

    public static string EditStopJson() => new JObject { ["type"] = "edit.stop" }.ToString(Formatting.None);

    /// <summary>저장 실패 — 손잡이 · 모양을 원위치로.</summary>
    public static string EditRevertJson(string kind, string uid) => new JObject { ["type"] = "edit.revert", ["target"] = Target(kind, uid) }.ToString(Formatting.None);

    // ── 지도 심볼(overlay) — 3D 에서 고르기 · 고치기(2026-10-22) ──

    /// <summary>고른 심볼 표시 — {"type":"overlay.select","items":[{kind,id}…]}. 빈 목록 = 풀기.</summary>
    public static string OverlaySelectJson(IEnumerable<(string Kind, int Id)> items)
        => new JObject { ["type"] = "overlay.select", ["items"] = new JArray(items.Select(i => new JObject { ["kind"] = i.Kind, ["id"] = i.Id })) }.ToString(Formatting.None);

    private static JObject OverlayTarget(string overlayKind, int id) => new() { ["kind"] = TargetOverlay, ["overlayKind"] = overlayKind, ["id"] = id };

    /// <summary>심볼 하나에 손잡이 — 점 = 옮기기 원판(+ rotatable 이면 돌리기 원호), 선 · 구역 = 꼭짓점 공 + 통째 옮기기 원판.</summary>
    public static string OverlayEditStartJson(string overlayKind, int id, bool rotatable)
    {
        var t = OverlayTarget(overlayKind, id);
        t["rotatable"] = rotatable;
        return new JObject { ["type"] = "edit.start", ["target"] = t }.ToString(Formatting.None);
    }

    /// <summary>심볼 저장을 받지 않았다(편집 모드 꺼짐 · 잠김 · 권한 없음 · 모양 틀림) — 3D 모양을 원위치로.</summary>
    public static string OverlayEditRevertJson(string overlayKind, int id) => new JObject { ["type"] = "edit.revert", ["target"] = OverlayTarget(overlayKind, id) }.ToString(Formatting.None);

    // ── 3D → GIS ──

    /// <summary>draw.* · edit.commit 해석 — 형식이 틀리면 null(Map3DMath.ParseMessage 가 부른다).</summary>
    public static Map3DMessage? Parse(string type, JObject o)
    {
        switch (type)
        {
            case "draw.point":
            {
                var pts = new List<UbPoint>();
                if (!Map3DDecoration.AddPoint(new JArray(o["lon"] ?? JValue.CreateNull(), o["lat"] ?? JValue.CreateNull()), pts)) return null;
                double? alt = o["alt"]?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(o.Value<double>("alt")) ? o.Value<double>("alt") : null;
                return new Map3DMessage(type, null, null, null) { Draw = new Map3DDrawEvent(ModePoint, pts, alt, null) };
            }
            case "draw.done":
            {
                string? mode = Str(o["mode"]);
                if (mode is not (ModePoint or ModeLine or ModePolygon or ModeRect or ModeCircle) || o["coordinates"] is not JArray arr) return null;
                var pts = new List<UbPoint>();
                if (arr.Count == 0 || !arr.All(t => Map3DDecoration.AddPoint(t, pts))) return null;
                if (mode != ModeLine && pts.Count > 1 && pts[0] == pts[^1]) pts.RemoveAt(pts.Count - 1);
                return new Map3DMessage(type, null, null, null) { Draw = new Map3DDrawEvent(mode, pts, null, null) };
            }
            case "draw.end":
                return new Map3DMessage(type, null, null, null) { Draw = new Map3DDrawEvent(null, Array.Empty<UbPoint>(), null, Str(o["reason"])) };
            case "edit.end":   // 바뀐 것 없이 3D 에서 Esc 로 편집이 끝났다(엔진 덧붙임) — 판의 편집 표시를 끈다
                return new Map3DMessage(type, Str(o["reason"]), null, null);
            case "overlay.box":
            {
                if (o["items"] is not JArray arr) return null;
                var items = new List<(string, int)>();
                foreach (var t in arr)
                    if (OverlayRef(t) is { } r) items.Add(r);
                return new Map3DMessage(type, null, null, null) { OverlayBox = new Map3DOverlayBox(items, o["add"]?.Type != JTokenType.Boolean || o.Value<bool>("add")) };
            }
            case "edit.commit" when o["target"] is JObject ot && Str(ot["kind"]) == TargetOverlay:
                return ParseOverlayCommit(o, ot);
            case "edit.commit":
            {
                if (o["target"] is not JObject t || Str(t["kind"]) is not ({ } kind and (TargetDecoration or TargetUserBuilding or TargetModel))
                    || Str(t["uid"]) is not { } uid || !Map3DUserBuilding.IsUid(uid) || o["geometry"] is not JObject g) return null;
                string? geo = Str(g["type"]);
                var pts = new List<UbPoint>();
                bool ok = geo switch
                {
                    Map3DDecoration.GeoPoint => Map3DDecoration.AddPoint(g["coordinates"], pts),
                    Map3DDecoration.GeoLine => g["coordinates"] is JArray line && line.Count > 0 && line.All(p => Map3DDecoration.AddPoint(p, pts)),
                    // GeoJSON 고리([[[…]]]) — 고리 없이 점 목록([[…]])으로 와도 받는다
                    Map3DDecoration.GeoPolygon => g["coordinates"] is JArray rings && rings.Count > 0
                                                  && (rings[0] is JArray { Count: > 0 } r0 && r0[0] is JArray ? r0 : rings).All(p => Map3DDecoration.AddPoint(p, pts)),
                    _ => false,
                };
                if (!ok || pts.Count < MinVertices(geo!)) return null;
                if (geo == Map3DDecoration.GeoPolygon && pts[0] == pts[^1]) pts.RemoveAt(pts.Count - 1);
                double? heading = o["heading_deg"]?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(o.Value<double>("heading_deg"))
                    ? Map3DDecoration.StepHeading(o.Value<double>("heading_deg"), 0) : null;
                if (kind == TargetModel && geo != Map3DDecoration.GeoPoint) return null;   // 넣은 모델은 한 점(자리)만
                double? scale = o["scale"]?.Type is JTokenType.Float or JTokenType.Integer && o.Value<double>("scale") is var sc && double.IsFinite(sc) && sc > 0
                    ? Math.Clamp(sc, 0.01, 100) : null;
                return new Map3DMessage(type, null, null, null) { EditCommit = new Map3DEditCommit(kind, uid, geo!, pts, heading) { Scale = scale } };
            }
        }
        return null;
    }

    private static string? Str(JToken? t) => t?.Type == JTokenType.String ? t.Value<string>() : null;

    private static double? Num(JToken? t) => t?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(t.Value<double>()) ? t.Value<double>() : null;

    /// <summary>{kind, id} — kind 가 빈 문자열이거나 id 가 정수가 아니면 null.</summary>
    private static (string Kind, int Id)? OverlayRef(JToken? t)
        => t is JObject r && Str(r["kind"]) is { Length: > 0 } k && r["id"]?.Type == JTokenType.Integer ? (k, r.Value<int>("id")) : null;

    /// <summary>
    /// 심볼 edit.commit — 선 · 구역 종류는 points(점 2개 이상, 닫는 끝점은 뗀다), 점 심볼은 lon · lat(+ heading_deg)이 있어야 한다.
    /// 없거나 틀리면 null(구판 엔진 · 깨진 메시지는 무시).
    /// </summary>
    private static Map3DMessage? ParseOverlayCommit(JObject o, JObject t)
    {
        if (Str(t["overlayKind"]) is not { Length: > 0 } kind || t["id"]?.Type != JTokenType.Integer) return null;
        int id = t.Value<int>("id");
        List<UbPoint>? pts = null;
        double? lon = Num(o["lon"]), lat = Num(o["lat"]);
        if (Map3DOverlay.IsLineKind(kind))
        {
            pts = new List<UbPoint>();
            if (o["points"] is not JArray arr || !arr.All(p => Map3DDecoration.AddPoint(p, pts))) return null;
            if (pts.Count > 2 && pts[0] == pts[^1]) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 2) return null;
        }
        else if (lon is not (>= -180 and <= 180) || lat is not (>= -90 and <= 90)) return null;
        double? heading = Num(o["heading_deg"]) is double h ? Map3DDecoration.StepHeading(h, 0) : null;
        return new Map3DMessage("edit.commit", null, null, null) { OverlayEdit = new Map3DOverlayEdit(kind, id, lon, lat, heading, pts) };
    }

    // ── 도구 → 그리기 방식 ──

    /// <summary>꾸미기 도구 → point · line · polygon.</summary>
    public static string ModeOf(DecoTool t) => Map3DDecoration.GeometryOf(t) switch
    {
        Map3DDecoration.GeoPoint => ModePoint,
        Map3DDecoration.GeoLine => ModeLine,
        _ => ModePolygon,
    };

    /// <summary>건물 형상 도구 → rect · circle · polygon(삼각형 · 타원은 계약에 없어 다각형으로 그린다).</summary>
    public static string ModeOf(UbTool t) => t switch { UbTool.Rect => ModeRect, UbTool.Circle => ModeCircle, _ => ModePolygon };

    /// <summary>도형의 최소 꼭짓점 수(점 1 · 선 2 · 다각형 3).</summary>
    public static int MinVertices(string geometry) => geometry switch { Map3DDecoration.GeoPoint => 1, Map3DDecoration.GeoLine => 2, _ => 3 };

    // ── 판 단추 → 뷰어 키(edit.key, 엔진 dac1e69 덧붙임) ──

    /// <summary>뷰어가 받는 키 — 방향키 = 화면 기준(↑ = 카메라가 보는 쪽), [ ] = 돌리기, Tab = 다음 꼭짓점(Shift = 앞), Delete · Enter(확정) · Escape,
    /// + − = 넣은 모델 크기 ×1.1 · ÷1.1(Shift = 1.01, 엔진 8cacc6e).</summary>
    public static readonly IReadOnlyList<string> Keys = new[] { "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "[", "]", "Tab", "Delete", "Enter", "Escape", "+", "-" };

    /// <summary>
    /// 판 단추 매개변수 → {"type":"edit.key","key","shift"}. "Shift+Tab" 처럼 앞에 "Shift+" 를 붙이면 shift 를 켠다(꼭짓점 이전).
    /// 모르는 키면 null.
    /// </summary>
    public static string? EditKeyJson(string? key, bool shift)
    {
        if (key?.StartsWith("Shift+", StringComparison.Ordinal) == true) { key = key["Shift+".Length..]; shift = true; }
        if (key == null || !Keys.Contains(key)) return null;
        return new JObject { ["type"] = "edit.key", ["key"] = key, ["shift"] = shift }.ToString(Formatting.None);
    }

    // ── 글 ──

    /// <summary>3D 꾸미기 판 상태 한 줄 — 도구에 따라.</summary>
    public static string DecorationStatusText(DecoTool? tool)
        => tool is not { } t ? "도구를 고르거나 3D 에서 항목을 눌러 고치세요"
         : Map3DDecoration.GeometryOf(t) == Map3DDecoration.GeoPoint ? $"3D 에서 땅을 눌러 {Map3DDecoration.ToolText(t)}을(를) 놓으세요 — Esc 로 끝"
         : "3D 에서 땅을 눌러 점을 찍고 두 번 누르기 · Enter 로 마치세요";
}
