using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 지도 꾸미기 항목(나무 · 바닥 · 소품 · 담장, twin-contract C) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DDecorationTests).
                  ① 도구 → 종류 · 도형 ② 값 목록(쉬운 말) · 기본값 · 범위 ③ map-3d 요청 본문(PUT …/decorations/{uid}) · 응답 해석
                  ④ GIS → 3D 메시지(decorations.reload · decoration.select) ⑤ 2D 고르기(화면 좌표 맞추기) · 목록 글.
   Note         : map-3d 는 굽지 않고 뷰어가 실행 중에 그린다(decorations.py) — 저장하면 3D 에 바로 반영(decorations.reload).
                  좌표는 [경도, 위도] 소수 7자리 · 다각형 고리는 GeoJSON 처럼 처음 점으로 닫아 보낸다(받을 때는 겹친 끝점을 뺀다).
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>꾸미기 도구 — 나무 한 그루(클릭) · 나무 줄(선) · 나무 영역(다각형) · 바닥(다각형) · 소품(클릭) · 담장(선).</summary>
public enum DecoTool { TreePoint, TreeLine, TreeArea, Ground, Prop, Wall }

/// <summary>꾸미기 항목 하나. Geometry = "Point" | "LineString" | "Polygon", Points = 위경도(다각형은 닫는 끝점 없이).</summary>
public sealed record Map3DDecorationItem(string Uid, string Kind, string Geometry, IReadOnlyList<UbPoint> Points, JObject Props, string? Name)
{
    /// <summary>이 항목을 만든 도구.</summary>
    public DecoTool Tool => Map3DDecoration.ToolOf(Kind, Geometry);
    /// <summary>판 목록 한 줄.</summary>
    public string Text => Map3DDecoration.ListText(this);
}

public static class Map3DDecoration
{
    public const string KindTree = "tree", KindGround = "ground", KindProp = "prop", KindWall = "wall";
    public const string GeoPoint = "Point", GeoLine = "LineString", GeoPolygon = "Polygon";
    public const int MaxPoints = 500;
    /// <summary>2D 에서 점 · 선 항목을 고르는 거리(px).</summary>
    public const double PickRadiusPx = 8.0;
    /// <summary>되돌리기 기록 설명 앞머리 — 꾸미기 중 Ctrl+Z · Ctrl+Y 가 이 기록만 되돌린다.</summary>
    public const string UndoPrefix = "꾸미기";
    public const string OldEngineText = "엔진이 옛 판이라 꾸미기를 쓸 수 없습니다";

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Species = Pairs(("broadleaf", "활엽수"), ("conifer", "침엽수"), ("shrub", "관목"), ("palm", "야자수"));
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Surfaces = Pairs(("grass", "잔디"), ("asphalt", "아스팔트"), ("concrete", "콘크리트"),
        ("gravel", "자갈"), ("water", "물"), ("sand", "모래"), ("paving", "보도블록"));
    public static readonly IReadOnlyList<KeyValuePair<string, string>> PropTypes = Pairs(("car", "승용차"), ("truck", "트럭"), ("bus", "버스"),
        ("container", "컨테이너"), ("streetlight", "가로등"), ("bench", "벤치"), ("bollard", "차 막음 기둥"), ("flagpole", "깃대"), ("barrier", "차단기"),
        ("cctv_pole", "CCTV 기둥"), ("bush", "덤불"), ("rock", "바위"));
    public static readonly IReadOnlyList<KeyValuePair<string, string>> WallTypes = Pairs(("wall", "담"), ("fence", "철망 울타리"), ("hedge", "생울타리"),
        ("razor", "윤형 철조망"), ("guardrail", "가드레일"));

    /// <summary>숫자 값 범위(최소 · 최대 · 기본) — map-3d 와 같은 값.</summary>
    public static readonly (double Min, double Max, double Default) TreeHeight = (1, 40, 8), Spacing = (2, 50, 6), Density = (0.1, 10, 1),
        Heading = (0, 360, 0), Scale = (0.5, 3, 1), WallHeight = (0.5, 10, 2);

    private static IReadOnlyList<KeyValuePair<string, string>> Pairs(params (string Key, string Text)[] p) => p.Select(x => new KeyValuePair<string, string>(x.Key, x.Text)).ToArray();

    // ── 도구 ──

    public static string KindOf(DecoTool t) => t switch
    {
        DecoTool.Ground => KindGround,
        DecoTool.Prop => KindProp,
        DecoTool.Wall => KindWall,
        _ => KindTree,
    };

    public static string GeometryOf(DecoTool t) => t switch
    {
        DecoTool.TreePoint or DecoTool.Prop => GeoPoint,
        DecoTool.TreeLine or DecoTool.Wall => GeoLine,
        _ => GeoPolygon,
    };

    public static DecoTool ToolOf(string kind, string geometry) => kind switch
    {
        KindGround => DecoTool.Ground,
        KindProp => DecoTool.Prop,
        KindWall => DecoTool.Wall,
        _ => geometry == GeoLine ? DecoTool.TreeLine : geometry == GeoPolygon ? DecoTool.TreeArea : DecoTool.TreePoint,
    };

    /// <summary>그 도형이 되는 최소 점 수(점 1 · 선 2 · 다각형 3).</summary>
    public static int MinPoints(DecoTool t) => GeometryOf(t) switch { GeoPoint => 1, GeoLine => 2, _ => 3 };

    /// <summary>도구 쉬운 말(판 단추 · 목록).</summary>
    public static string ToolText(DecoTool t) => t switch
    {
        DecoTool.TreePoint => "나무 한 그루",
        DecoTool.TreeLine => "나무 줄",
        DecoTool.TreeArea => "나무 영역",
        DecoTool.Ground => "바닥",
        DecoTool.Prop => "소품",
        _ => "담장",
    };

    /// <summary>그 도구의 기본 값(props).</summary>
    public static JObject DefaultProps(DecoTool t) => t switch
    {
        DecoTool.Ground => new JObject { ["surface"] = "grass", ["color"] = JValue.CreateNull() },
        DecoTool.Prop => new JObject { ["type"] = "car", ["heading_deg"] = Heading.Default, ["scale"] = Scale.Default },
        DecoTool.Wall => new JObject { ["type"] = "fence", ["height_m"] = WallHeight.Default, ["color"] = JValue.CreateNull() },
        DecoTool.TreeLine => new JObject { ["species"] = "broadleaf", ["height_m"] = TreeHeight.Default, ["spacing_m"] = Spacing.Default },
        DecoTool.TreeArea => new JObject { ["species"] = "broadleaf", ["height_m"] = TreeHeight.Default, ["density"] = Density.Default },
        _ => new JObject { ["species"] = "broadleaf", ["height_m"] = TreeHeight.Default },
    };

    // ── 값 ──

    /// <summary>
    /// 숫자 칸 — 소수 1자리(밀도는 소수 2자리)로 반올림해 범위 안이면 true. 실패면 사용자용 오류 글.
    /// </summary>
    public static bool TryParse(string? text, (double Min, double Max, double Default) range, string name, out double value, out string? error, int digits = 1)
    {
        value = 0;
        var s = (text ?? "").Trim().TrimEnd('m', '°', '배').Trim();
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
        { error = $"{name}는 숫자로 넣어 주세요."; return false; }
        v = Math.Round(v, digits, MidpointRounding.AwayFromZero);
        if (v < range.Min || v > range.Max) { error = $"{name}는 {Num(range.Min)} ~ {Num(range.Max)} 사이로 넣어 주세요."; return false; }
        value = v + 0.0;
        error = null;
        return true;
    }

    /// <summary>방향 한 칸(↑/↓ · ↺/↻) — 0~360 을 돌아 감는다(360 = 0).</summary>
    public static double StepHeading(double heading, double delta)
    {
        double h = (heading + delta) % 360.0;
        return Math.Round(h < 0 ? h + 360.0 : h, 1) + 0.0;
    }

    public static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ── 검사 ──

    /// <summary>점 검사 — 문제가 없으면 null. inside = 3D 영역 안 판정(없으면 검사 안 함).</summary>
    public static string? Validate(DecoTool tool, IReadOnlyList<UbPoint> geo, Func<UbPoint, bool>? inside = null)
    {
        int min = MinPoints(tool);
        if (geo.Count < min) return GeometryOf(tool) == GeoLine ? "점이 2개 이상 있어야 선이 됩니다." : "점이 3개 이상 있어야 영역이 됩니다.";
        if (geo.Count > MaxPoints) return $"점은 {MaxPoints}개까지입니다.";
        if (inside != null && geo.Any(p => !inside(p))) return Map3DUserBuilding.OutsideText;
        if (GeometryOf(tool) == GeoPolygon && Map3DUserBuilding.IsSelfIntersecting(geo, closed: true)) return "영역 선이 서로 엇갈립니다 — 다시 그려 주세요.";
        return null;
    }

    // ── 서버 · 3D 메시지 ──

    private static JArray Coord(UbPoint p) => new(Math.Round(p.X, 7), Math.Round(p.Y, 7));

    /// <summary>GeoJSON geometry — Point [lon,lat] · LineString [[…]] · Polygon [[[…], 처음 점]].</summary>
    public static JObject GeometryJson(string geometry, IReadOnlyList<UbPoint> pts) => new()
    {
        ["type"] = geometry,
        ["coordinates"] = geometry switch
        {
            GeoPoint => Coord(pts[0]),
            GeoLine => new JArray(pts.Select(p => (object)Coord(p)).ToArray()),
            _ => new JArray((object)new JArray(pts.Append(pts[0]).Select(p => (object)Coord(p)).ToArray())),   // (object) — new JArray(JArray) 는 감싸지 않고 복사한다
        },
    };

    /// <summary>PUT …/decorations/{uid} 본문 — {kind, geometry, props, name?}.</summary>
    public static JObject PutBody(Map3DDecorationItem it)
    {
        var o = new JObject { ["kind"] = it.Kind, ["geometry"] = GeometryJson(it.Geometry, it.Points), ["props"] = it.Props.DeepClone() };
        if (!string.IsNullOrWhiteSpace(it.Name)) o["name"] = it.Name.Trim().Length > Map3DUserBuilding.MaxName ? it.Name.Trim()[..Map3DUserBuilding.MaxName] : it.Name.Trim();
        return o;
    }

    /// <summary>서버 항목 해석 — uid(16진수 12자) · 아는 종류 · 도형이 맞지 않으면 null.</summary>
    public static Map3DDecorationItem? ParseItem(JObject? o)
    {
        if (o == null || o["uid"]?.Type != JTokenType.String || !Map3DUserBuilding.IsUid(o.Value<string>("uid"))) return null;
        string? kind = o["kind"]?.Type == JTokenType.String ? o.Value<string>("kind") : null;
        if (kind is not (KindTree or KindGround or KindProp or KindWall) || o["geometry"] is not JObject g) return null;
        string? type = g["type"]?.Type == JTokenType.String ? g.Value<string>("type") : null;
        var pts = new List<UbPoint>();
        bool ok = type switch
        {
            GeoPoint => AddPoint(g["coordinates"], pts),
            GeoLine => g["coordinates"] is JArray line && line.All(t => AddPoint(t, pts)),
            GeoPolygon => g["coordinates"] is JArray rings && rings.FirstOrDefault() is JArray ring && ring.All(t => AddPoint(t, pts)),
            _ => false,
        };
        if (!ok) return null;
        if (type == GeoPolygon && pts.Count > 1 && pts[0] == pts[^1]) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < MinPoints(ToolOf(kind, type!))) return null;
        if (type != GeometryOf(ToolOf(kind, type!))) return null;   // 바닥 = 다각형 · 소품 = 점 · 담장 = 선
        string? name = o["name"]?.Type == JTokenType.String && o.Value<string>("name") is { Length: > 0 } s ? s : null;
        return new Map3DDecorationItem(o.Value<string>("uid")!, kind, type!, pts, o["props"] as JObject ?? new JObject(), name);
    }

    private static bool AddPoint(JToken? t, List<UbPoint> pts)
    {
        if (t is not JArray p || p.Count < 2 || p[0].Type is not (JTokenType.Float or JTokenType.Integer) || p[1].Type is not (JTokenType.Float or JTokenType.Integer))
            return false;
        double x = p[0].Value<double>(), y = p[1].Value<double>();
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 180 || Math.Abs(y) > 90) return false;
        pts.Add(new UbPoint(x, y));
        return true;
    }

    /// <summary>GET …/decorations 응답 {"items":[…]} — 형식이 틀린 항목은 뺀다.</summary>
    public static IReadOnlyList<Map3DDecorationItem> ParseList(string json)
    {
        var list = new List<Map3DDecorationItem>();
        if (JObject.Parse(json)["items"] is JArray items)
            foreach (var o in items.OfType<JObject>())
                if (ParseItem(o) is { } it) list.Add(it);
        return list;
    }

    /// <summary>3D 가 항목을 다시 받아 그리게.</summary>
    public static string ReloadMessageJson() => new JObject { ["type"] = "decorations.reload" }.ToString(Formatting.None);

    /// <summary>3D 강조(null = 지움).</summary>
    public static string SelectMessageJson(string? uid)
        => new JObject { ["type"] = "decoration.select", ["uid"] = uid is null ? JValue.CreateNull() : uid }.ToString(Formatting.None);

    // ── 2D 고르기 ──

    /// <summary>
    /// 화면 점에 맞는 항목(나중에 그린 것 먼저) — 점은 <see cref="PickRadiusPx"/> 안, 선은 선분까지 그 거리 안, 다각형은 안쪽 또는 테두리 가까이.
    /// toScreen = 위경도 → 화면(지도 회전 포함).
    /// </summary>
    public static string? HitTest(IReadOnlyList<Map3DDecorationItem> items, UbPoint screen, Func<UbPoint, UbPoint> toScreen)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            var s = it.Points.Select(toScreen).ToList();
            bool closed = it.Geometry == GeoPolygon;
            if (s.Count == 1 ? Map3DUserBuilding.Dist(s[0], screen) <= PickRadiusPx
                : NearPath(s, screen, closed) || closed && Map3DUserBuilding.Contains(s, screen)) return it.Uid;
        }
        return null;
    }

    private static bool NearPath(IReadOnlyList<UbPoint> s, UbPoint p, bool closed)
    {
        int n = closed ? s.Count : s.Count - 1;
        for (int i = 0; i < n; i++)
        {
            UbPoint a = s[i], b = s[(i + 1) % s.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
            double t = len2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            if (Map3DUserBuilding.Dist(p, new UbPoint(a.X + t * dx, a.Y + t * dy)) <= PickRadiusPx) return true;
        }
        return false;
    }

    // ── 글 ──

    /// <summary>목록 한 줄 — "정문 소나무 · 나무 줄" · 이름이 없으면 "나무 줄 · 침엽수".</summary>
    public static string ListText(Map3DDecorationItem it)
    {
        string kind = ToolText(it.Tool);
        string? sub = it.Kind switch
        {
            KindTree => Text(Species, it.Props.Value<string>("species")),
            KindGround => Text(Surfaces, it.Props.Value<string>("surface")),
            _ => Text(it.Kind == KindProp ? PropTypes : WallTypes, it.Props.Value<string>("type")),
        };
        return it.Name is { } n ? $"{n} · {kind}" : sub is null ? kind : $"{kind} · {sub}";
    }

    private static string? Text(IReadOnlyList<KeyValuePair<string, string>> list, string? key) => list.FirstOrDefault(p => p.Key == key).Value;

    /// <summary>판 상태 한 줄 — 도구 · 찍은 점 수에 따라.</summary>
    public static string StatusText(DecoTool? tool, int points)
        => tool is not { } t ? "도구를 고르거나 지도 위 항목을 눌러 고치세요"
         : GeometryOf(t) == GeoPoint ? $"지도를 눌러 {ToolText(t)}을(를) 놓으세요"
         : points == 0 ? "지도를 눌러 첫 점을 찍으세요"
         : points < MinPoints(t) ? $"점 {points}개 — {MinPoints(t)}개 이상 찍으세요"
         : $"점 {points}개 — Enter 또는 두 번 눌러 마치기";

    /// <summary>"꾸미기 항목 3개"(0 이면 "꾸미기 항목 없음").</summary>
    public static string CountText(int count) => count > 0 ? $"꾸미기 항목 {count}개" : "꾸미기 항목 없음";
}
