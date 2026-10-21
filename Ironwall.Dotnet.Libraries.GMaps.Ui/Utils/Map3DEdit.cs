using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 안에서 편집(edit3d-contract, 2026-10-21) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DEditTests).
                  ① GIS → 3D 메시지(draw.start · draw.cancel · edit.start · edit.stop · edit.revert)
                  ② 3D → GIS 메시지 해석(draw.point · draw.done · draw.end · edit.commit)
                  ③ 판 단추 폴백 계산(미터 옮기기 · 꼭짓점 고르기 · 지우기 최소 개수) ④ 도구 → 그리기 방식.
   Note         : 메시지 type · 키 이름은 계약 그대로. 좌표는 [경도, 위도] 소수 7자리.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 그리기 결과 — draw.point(점 1개 · 고도) · draw.done(방식 · 점 목록, 다각형은 닫지 않은 점) · draw.end(이유).</summary>
public sealed record Map3DDrawEvent(string? Mode, IReadOnlyList<UbPoint> Points, double? Alt, string? Reason);

/// <summary>3D 손잡이로 고친 결과(edit.commit) — 대상(종류 · uid) · 새 도형(Point · LineString · Polygon, 다각형은 닫는 끝점 없이) · 소품 방향.</summary>
public sealed record Map3DEditCommit(string Kind, string Uid, string Geometry, IReadOnlyList<UbPoint> Points, double? HeadingDeg);

public static class Map3DEdit
{
    /// <summary>edit.* 대상 종류.</summary>
    public const string TargetDecoration = "decoration", TargetUserBuilding = "userbuilding";
    /// <summary>그리기 방식.</summary>
    public const string ModePoint = "point", ModeLine = "line", ModePolygon = "polygon", ModeRect = "rect", ModeCircle = "circle";
    /// <summary>판 단추 · 방향키 한 칸(m) — Shift = 5 m.</summary>
    public const double MoveStepM = 0.5, MoveStepShiftM = 5.0;

    private const double MetersPerDegLat = 110_540.0, MetersPerDegLonAtEquator = 111_320.0;

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
            case "edit.commit":
            {
                if (o["target"] is not JObject t || Str(t["kind"]) is not ({ } kind and (TargetDecoration or TargetUserBuilding))
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
                return new Map3DMessage(type, null, null, null) { EditCommit = new Map3DEditCommit(kind, uid, geo!, pts, heading) };
            }
        }
        return null;
    }

    private static string? Str(JToken? t) => t?.Type == JTokenType.String ? t.Value<string>() : null;

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

    // ── 판 단추 폴백 ──

    /// <summary>도형의 최소 꼭짓점 수(점 1 · 선 2 · 다각형 3).</summary>
    public static int MinVertices(string geometry) => geometry switch { Map3DDecoration.GeoPoint => 1, Map3DDecoration.GeoLine => 2, _ => 3 };

    /// <summary>방향 단추("N" · "S" · "E" · "W") → 동 · 북 미터(Shift = 5 m). 모르는 방향은 null.</summary>
    public static (double East, double North)? MoveStep(string? dir, bool shift)
    {
        double d = shift ? MoveStepShiftM : MoveStepM;
        return dir switch { "N" => (0, d), "S" => (0, -d), "E" => (d, 0), "W" => (-d, 0), _ => null };
    }

    /// <summary>모든 점을 동 · 북으로 미터만큼 옮긴다(가운데 위도 기준, 소수 7자리).</summary>
    public static IReadOnlyList<UbPoint> Move(IReadOnlyList<UbPoint> pts, double eastM, double northM)
    {
        if (pts.Count == 0) return pts;
        double lat = pts.Average(p => p.Y);
        double dLon = eastM / (MetersPerDegLonAtEquator * Math.Cos(lat * Math.PI / 180.0)), dLat = northM / MetersPerDegLat;
        return pts.Select(p => Map3DUserBuilding.Round(new UbPoint(p.X + dLon, p.Y + dLat))).ToList();
    }

    /// <summary>다음 · 이전 꼭짓점(돌아 감음). 고른 것이 없으면 앞으로 = 첫 점, 뒤로 = 끝 점.</summary>
    public static int StepVertex(int index, int count, int delta)
    {
        if (count <= 0) return -1;
        if (index < 0 || index >= count) return delta >= 0 ? 0 : count - 1;
        return ((index + delta) % count + count) % count;
    }

    /// <summary>꼭짓점 지우기 — 최소 개수를 지키지 못하거나 번호가 틀리면 null.</summary>
    public static IReadOnlyList<UbPoint>? RemoveVertex(IReadOnlyList<UbPoint> pts, int index, int min)
    {
        if (index < 0 || index >= pts.Count || pts.Count - 1 < min) return null;
        return pts.Where((_, i) => i != index).ToList();
    }

    // ── 글 ──

    /// <summary>판의 꼭짓점 줄 — "꼭짓점 3 / 8" · 고른 것이 없으면 "꼭짓점 8개 — [다음] 으로 고르세요".</summary>
    public static string VertexText(int index, int count)
        => index >= 0 && index < count ? $"꼭짓점 {index + 1} / {count}" : $"꼭짓점 {count}개 — [다음] 으로 고르세요";

    /// <summary>꼭짓점을 더 지울 수 없을 때.</summary>
    public static string MinVertexText(int min) => $"꼭짓점은 {min}개보다 적게 할 수 없습니다.";

    /// <summary>3D 꾸미기 판 상태 한 줄 — 도구에 따라.</summary>
    public static string DecorationStatusText(DecoTool? tool)
        => tool is not { } t ? "도구를 고르거나 3D 에서 항목을 눌러 고치세요"
         : Map3DDecoration.GeometryOf(t) == Map3DDecoration.GeoPoint ? $"3D 에서 땅을 눌러 {Map3DDecoration.ToolText(t)}을(를) 놓으세요 — Esc 로 끝"
         : "3D 에서 땅을 눌러 점을 찍고 두 번 누르기 · Enter 로 마치세요";
}
