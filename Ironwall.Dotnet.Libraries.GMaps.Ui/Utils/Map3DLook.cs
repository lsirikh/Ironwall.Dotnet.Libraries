using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 건물 모양 덮어쓰기(숨기기 · 외벽 색 · 지붕 색 · 외벽 무늬) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DLookTests).
                  ① map-3d 요청 본문(PUT …/buildings/{bid}/look) · 응답 · 목록(GET …/looks) 해석 ② GIS → 3D 메시지(building.look)
                  ③ 판의 색 · 외벽 무늬 고르기 목록(쉬운 말).
   Note         : map-3d 는 건물 id 가 아니라 건물 자리(바닥 윤곽)로 영역 단위 저장한다(looks.py, twin-contract A) — 다시 만들어도 다시 붙는다.
                  색 = "#rrggbb" 소문자 또는 null(자동). 직접 그린 건물(origin user)은 숨기지 않고 user-buildings DELETE 로 지운다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>건물 하나의 모양 덮어쓰기 상태 — 숨김 · 외벽 색 · 지붕 색(null = 자동) · 외벽 무늬(auto = 엔진이 고름).</summary>
public sealed record Map3DLookState(bool Hidden, string? WallColor, string? RoofColor, string Facade)
{
    /// <summary>덮어쓰기 없음(자동).</summary>
    public static readonly Map3DLookState Auto = new(false, null, null, Map3DLook.FacadeAuto);
}

/// <summary>저장 응답의 건물(PUT/DELETE …/look 의 building) — Origin = map · national · photo · user, UserUid = 직접 그린 건물 uid.</summary>
public sealed record Map3DBuildingLook(int Id, Map3DLookState State, string? Origin, string? UserUid)
{
    /// <summary>다시 굽기에서 숨겨 장면(GLB)에 없는 건물을 되살렸다(needs_rebake) — 다시 만들어야 화면에 보인다.</summary>
    public bool NeedsRebake { get; init; }
}

/// <summary>저장된 덮어쓰기 하나(GET …/looks) — Id = 지금 장면 건물 id(null = 지금 장면에 못 붙인 것).</summary>
public sealed record Map3DLookItem(int? Id, IReadOnlyList<UbPoint> Outline, Map3DLookState State)
{
    /// <summary>목록 한 줄 — "건물 12 · 바닥 320 m²" · 지금 장면에 못 붙였으면 그렇게.</summary>
    public string Text => (Id is { } id ? $"건물 {id}" : "지금 장면에 없는 건물")
                          + (Outline.Count >= 3 ? $" · 바닥 {Map3DUserBuilding.AreaSquareMeters(Outline):0} m²" : "");
}

public static class Map3DLook
{
    public const string FacadeAuto = "auto";

    /// <summary>판의 외벽 무늬 고르기(값, 쉬운 말).</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Facades = new[]
    {
        new KeyValuePair<string, string>(FacadeAuto, "자동"), new("residential", "주거"), new("commercial", "사무"), new("industrial", "공장"),
        new("glass", "유리"), new("brick", "벽돌"), new("concrete", "콘크리트"),
    };

    /// <summary>판의 색 고르기(값 "" = 자동, 쉬운 말).</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Colors = new[]
    {
        new KeyValuePair<string, string>("", "자동"), new("#f2f2f2", "흰색"), new("#c8c8c8", "밝은 회색"), new("#8c8c8c", "회색"),
        new("#4a4a4a", "짙은 회색"), new("#d9c7a3", "베이지"), new("#a0522d", "벽돌색"), new("#6b4f3a", "갈색"), new("#5f7a8a", "청회색"),
        new("#6fa8b8", "청록"), new("#4f7a4f", "녹색"), new("#b03a2e", "붉은색"), new("#2f3e66", "남색"),
    };

    /// <summary>
    /// 색 고르기 목록 + 목록에 없는 지금 색("지정 색 #rrggbb") — 엔진 · 다른 곳에서 넣은 색이 콤보에서 빈칸으로 보이지 않게.
    /// 모두 목록 안이면 <see cref="Colors"/> 그대로.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ColorsWith(params string?[] current)
    {
        var extra = current.Select(NormalizeColor).OfType<string>().Distinct()
                           .Where(c => Colors.All(p => p.Key != c)).Select(c => new KeyValuePair<string, string>(c, "지정 색 " + c)).ToList();
        return extra.Count == 0 ? Colors : Colors.Concat(extra).ToList();
    }

    /// <summary>JObject.Merge 가 null 값(= 자동으로)을 버리지 않게.</summary>
    public static readonly JsonMergeSettings KeepNulls = new() { MergeNullValueHandling = MergeNullValueHandling.Merge };

    public const string OldEngineText = "엔진이 옛 판이라 외관 바꾸기 · 숨기기를 쓸 수 없습니다";
    /// <summary>되살렸지만 장면에 없는 건물(needs_rebake) · 외벽 무늬를 바꿨을 때 — 엔진은 다시 굽기에서만 그린다.</summary>
    public const string NeedsRebakeText = "되살렸습니다 — 3D 를 다시 만들면 보입니다";
    public const string FacadeRebakeText = "저장했습니다 — 3D 를 다시 만들면 외벽 무늬가 바뀝니다";

    /// <summary>"#RRGGBB" · "rrggbb" → "#rrggbb", 빈 값 · 틀린 값 → null(자동).</summary>
    public static string? NormalizeColor(string? text)
    {
        var s = (text ?? "").Trim().TrimStart('#').ToLowerInvariant();
        return s.Length == 6 && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? "#" + s : null;
    }

    /// <summary>모르는 외벽 무늬는 auto.</summary>
    public static string NormalizeFacade(string? facade) => Facades.Any(f => f.Key == facade) ? facade! : FacadeAuto;

    /// <summary>
    /// PUT …/look 본문 — hidden 이 null 이면 그 키를 빼고(그대로), look 이 null 이면 색 · 무늬 키를 뺀다(부분 갱신).
    /// 색 null = 자동으로 되돌림.
    /// </summary>
    public static JObject PutBody(bool? hidden, Map3DLookState? look)
    {
        var o = new JObject();
        if (hidden is bool h) o["hidden"] = h;
        if (look != null)
        {
            o["wall_color"] = NormalizeColor(look.WallColor) is { } w ? w : JValue.CreateNull();
            o["roof_color"] = NormalizeColor(look.RoofColor) is { } r ? r : JValue.CreateNull();
            o["facade"] = NormalizeFacade(look.Facade);
        }
        return o;
    }

    /// <summary>3D 에 바로 보이게(다시 불러오지 않음) — {"type":"building.look","id","hidden","wall_color","roof_color"}.</summary>
    public static string LookMessageJson(int id, Map3DLookState s)
        => new JObject
        {
            ["type"] = "building.look", ["id"] = id, ["hidden"] = s.Hidden,
            ["wall_color"] = NormalizeColor(s.WallColor) is { } w ? w : JValue.CreateNull(),
            ["roof_color"] = NormalizeColor(s.RoofColor) is { } r ? r : JValue.CreateNull(),
        }.ToString(Formatting.None);

    /// <summary>건물 줄(3D building 메시지 · 서버 건물 줄)의 모양 키 — 없으면 자동.</summary>
    public static Map3DLookState StateOf(JObject o)
        => new(o["hidden"]?.Type == JTokenType.Boolean && o.Value<bool>("hidden"),
               NormalizeColor(o["wall_color"]?.Type == JTokenType.String ? o.Value<string>("wall_color") : null),
               NormalizeColor(o["roof_color"]?.Type == JTokenType.String ? o.Value<string>("roof_color") : null),
               NormalizeFacade(o["facade"]?.Type == JTokenType.String ? o.Value<string>("facade") : null));

    /// <summary>PUT/DELETE …/look 응답 {"building":{…},"looks":{"count"}} — id 가 없으면 null.</summary>
    public static Map3DBuildingLook? ParseSaved(string json)
    {
        if (JObject.Parse(json)["building"] is not JObject b || b["id"]?.Type != JTokenType.Integer || b.Value<long>("id") is not (> 0 and <= int.MaxValue)) return null;
        return new Map3DBuildingLook(b.Value<int>("id"), StateOf(b), Str(b["origin"]), Str(b["user_uid"]))
               { NeedsRebake = b["needs_rebake"]?.Type == JTokenType.Boolean && b.Value<bool>("needs_rebake") };
    }

    /// <summary>GET …/looks 응답 {"items":[…]} — 형식이 틀린 항목은 뺀다.</summary>
    public static IReadOnlyList<Map3DLookItem> ParseList(string json)
    {
        var list = new List<Map3DLookItem>();
        if (JObject.Parse(json)["items"] is not JArray items) return list;
        foreach (var o in items.OfType<JObject>())
        {
            int? id = o["id"]?.Type == JTokenType.Integer && o.Value<long>("id") is long n && n is > 0 and <= int.MaxValue ? (int)n : null;
            var pts = new List<UbPoint>();
            if (o["outline"] is JArray arr)
                foreach (var t in arr)
                    if (t is JArray p && p.Count == 2 && p[0].Type is JTokenType.Float or JTokenType.Integer && p[1].Type is JTokenType.Float or JTokenType.Integer)
                        pts.Add(new UbPoint(p[0].Value<double>(), p[1].Value<double>()));
            list.Add(new Map3DLookItem(id, pts, StateOf(o)));
        }
        return list;
    }

    /// <summary>"숨긴 건물 3개"(0 이면 빈 글).</summary>
    public static string HiddenCountText(int count) => count > 0 ? $"숨긴 건물 {count}개" : "숨긴 건물 없음";

    private static string? Str(JToken? t) => t?.Type == JTokenType.String && t.Value<string>() is { Length: > 0 } s ? s : null;
}
