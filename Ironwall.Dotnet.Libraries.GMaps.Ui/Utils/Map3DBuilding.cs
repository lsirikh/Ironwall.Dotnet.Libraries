using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 건물 높이 직접 입력(「건물 높이」 판) 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DBuildingTests).
                  ① 3D 의 building 메시지 · 서버 건물 줄 해석 ② GIS → 3D 메시지(미리보기 · 강조 · 저장됨)
                  ③ 판 규칙 — 층수 ↔ 높이(층고) · 입력 검증(층수 1~200 · 높이 1~600 m 소수 1자리 · 층고 2~10 m) · ±1층 · 키(↑/↓, Shift) · 손잡이 끌기
                  ④ 높이 출처 쉬운 말 · 3D 목록 글.
   Note         : 보안 구역처럼 공개 건물 자료(층수)를 못 구할 때 운영자가 건물을 골라 층수를 넣는다. 저장 · 다시 굽기 유지는 map-3d(heights.py).
                  층수 = 높이 ÷ 층고 반올림(.5 는 올림, 1~200).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 고른 건물(3D building 메시지 · 서버가 저장한 건물 줄). Height = 지금(저장된) 높이 m. HeightFrom = 높이를 어떻게 알았나
/// (tag · levels · user · oblique · shadow · default · llm · manual …), Origin = 건물 자료 출처(map · national · photo).
/// Manual = 직접 입력한 높이인가. 모르는 값은 null.
/// </summary>
public sealed record Map3DBuildingPick(int Id, string? Origin, double Height, string? HeightSource, string? HeightFrom, int? Levels,
                                       double? AreaM2, double? Lon, double? Lat, bool Manual);

public static class Map3DBuilding
{
    public const double MinHeight = 1.0;
    public const double MaxHeight = 600.0;
    public const int MinFloors = 1;
    public const int MaxFloors = 200;
    public const double DefaultFloorHeight = 3.0;
    public const double MinFloorHeight = 2.0;
    public const double MaxFloorHeight = 10.0;
    /// <summary>Shift + ↑/↓ 한 칸(m).</summary>
    public const double FineStep = 0.1;
    /// <summary>손잡이 끌기 — 1 px 당 높이(m, 위로 끌면 높게). 10 px = 1 m.</summary>
    public const double DragMetersPerPixel = 0.1;
    /// <summary>손잡이 끌기 데드존(px) — 이만큼 안 움직였으면 끌기가 아니다(클릭 · 손떨림).</summary>
    public const double DragDeadZonePx = 4.0;

    /// <summary>판 머리 띠 제목.</summary>
    public const string Title = "건물 높이";
    /// <summary>지도 편집 권한이 없을 때 판에 보이는 이유 한 줄.</summary>
    public const string ReadOnlyReason = "지도 편집 권한이 없어 보기만 할 수 있습니다.";

    // ── 해석 ──

    /// <summary>
    /// 건물 하나 해석 — 3D building 메시지(lon · lat · manual=bool) 또는 서버 건물 줄(center=[lon,lat] · manual={…}).
    /// id 가 양의 정수가 아니거나 높이가 양수가 아니면 null.
    /// </summary>
    public static Map3DBuildingPick? Parse(JObject? o)
    {
        if (o == null || o["id"]?.Type != JTokenType.Integer) return null;
        long id = o.Value<long>("id");
        if (id <= 0 || id > int.MaxValue) return null;
        if (Finite(o["height"]) is not double h || h <= 0) return null;
        double? lon = Finite(o["lon"]), lat = Finite(o["lat"]);
        if ((lon == null || lat == null) && o["center"] is JArray c && c.Count == 2) { lon = Finite(c[0]); lat = Finite(c[1]); }
        if (lon is double x && lat is double y && (Math.Abs(x) > 180 || Math.Abs(y) > 90)) { lon = null; lat = null; }
        var manual = o["manual"];
        bool isManual = manual?.Type == JTokenType.Boolean ? manual.Value<bool>() : manual?.Type == JTokenType.Object;
        int? levels = o["levels"]?.Type == JTokenType.Integer && o.Value<long>("levels") is long lv && lv > 0 && lv <= 10_000 ? (int)lv : null;
        double? area = Finite(o["area_m2"]) is double a && a > 0 ? a : null;
        return new Map3DBuildingPick((int)id, Str(o["origin"]), h, Str(o["height_source"]), Str(o["height_from"]) ?? Str(o["height_source"]),
                                     levels, area, lon, lat, isManual);
    }

    // ── GIS → 3D 메시지 ──

    /// <summary>그 건물만 그 높이로 미리보기(null = 원래 모양으로).</summary>
    public static string PreviewMessageJson(int id, double? height)
        => new JObject { ["type"] = "building.preview", ["id"] = id, ["height"] = height is double h ? h : JValue.CreateNull() }.ToString(Formatting.None);

    /// <summary>윤곽 강조(null = 지움).</summary>
    public static string SelectMessageJson(int? id)
        => new JObject { ["type"] = "building.select", ["id"] = id is int i ? i : JValue.CreateNull() }.ToString(Formatting.None);

    /// <summary>서버가 저장한 건물 줄 — 3D 가 그 높이를 새 원래 모양으로 삼는다.</summary>
    public static string CommitMessageJson(JObject building)
        => new JObject { ["type"] = "building.commit", ["id"] = building["id"], ["building"] = building }.ToString(Formatting.None);

    // ── 환산 ──

    /// <summary>높이 → 층수(높이 ÷ 층고 반올림, 1~200).</summary>
    public static int FloorsFromHeight(double height, double floorHeight)
    {
        if (!double.IsFinite(height) || !double.IsFinite(floorHeight) || floorHeight <= 0) return MinFloors;
        return (int)Math.Clamp(Math.Round(height / floorHeight, MidpointRounding.AwayFromZero), MinFloors, MaxFloors);
    }

    /// <summary>층수 → 높이(층수 × 층고, 소수 1자리, 1~600 m).</summary>
    public static double HeightFromFloors(int floors, double floorHeight) => ClampHeight(floors * floorHeight);

    /// <summary>높이를 소수 1자리 · 1~600 m 로.</summary>
    public static double ClampHeight(double height)
        => double.IsFinite(height) ? Math.Clamp(Math.Round(height, 1, MidpointRounding.AwayFromZero), MinHeight, MaxHeight) + 0.0 : MinHeight;

    /// <summary>±1층(단추) — 지금 높이의 층수에 delta 를 더한(1~200) 층수 × 층고.</summary>
    public static double StepFloors(double height, double floorHeight, int delta)
        => HeightFromFloors(Math.Clamp(FloorsFromHeight(height, floorHeight) + delta, MinFloors, MaxFloors), floorHeight);

    /// <summary>키 한 칸 — ↑/↓ = ±1층, Shift 와 함께면 ±0.1 m.</summary>
    public static double KeyStep(double height, double floorHeight, bool up, bool shift)
        => shift ? ClampHeight(height + (up ? FineStep : -FineStep)) : StepFloors(height, floorHeight, up ? 1 : -1);

    /// <summary>손잡이 끌기가 시작됐나(누른 자리에서 세로로 데드존을 넘었나).</summary>
    public static bool IsDrag(double totalDy) => double.IsFinite(totalDy) && Math.Abs(totalDy) > DragDeadZonePx;

    /// <summary>손잡이 끌기 높이 — 끌기 시작 높이에서 위로(dy &lt; 0) 끌면 높게. 데드존 안이면 시작 높이 그대로.</summary>
    public static double DragHeight(double startHeight, double totalDy)
        => IsDrag(totalDy) ? ClampHeight(startHeight - totalDy * DragMetersPerPixel) : startHeight;

    // ── 입력 검증 ──

    /// <summary>층수 입력 — 정수 1~200("12" · "12층"). 실패면 false + 사용자용 오류 글.</summary>
    public static bool TryParseFloors(string? text, out int floors, out string? error)
    {
        floors = 0;
        var s = (text ?? "").Trim().TrimEnd('층').Trim();
        if (s.Length == 0) { error = "층수를 넣어 주세요."; return false; }
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) { error = "층수는 정수로 넣어 주세요(예: 5)."; return false; }
        if (v < MinFloors || v > MaxFloors) { error = $"층수는 {MinFloors} ~ {MaxFloors} 사이로 넣어 주세요."; return false; }
        floors = v;
        error = null;
        return true;
    }

    /// <summary>높이 입력 — m, 소수 1자리로 반올림, 1~600("21.5" · "21.5 m").</summary>
    public static bool TryParseHeight(string? text, out double height, out string? error)
        => TryParseMeters(text, MinHeight, MaxHeight, "높이", "21.5", out height, out error);

    /// <summary>층고 입력 — m, 소수 1자리, 2~10.</summary>
    public static bool TryParseFloorHeight(string? text, out double floorHeight, out string? error)
        => TryParseMeters(text, MinFloorHeight, MaxFloorHeight, "층고", "3.0", out floorHeight, out error);

    private static bool TryParseMeters(string? text, double min, double max, string name, string example, out double value, out string? error)
    {
        value = 0;
        var s = (text ?? "").Trim();
        if (s.EndsWith("m", StringComparison.OrdinalIgnoreCase)) s = s[..^1].Trim();
        if (s.Length == 0) { error = $"{name}를 넣어 주세요."; return false; }
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
        { error = $"{name}는 숫자로 넣어 주세요(예: {example})."; return false; }
        v = Math.Round(v, 1, MidpointRounding.AwayFromZero);
        if (v < min || v > max) { error = $"{name}는 {Num(min)} ~ {Num(max)} m 사이로 넣어 주세요."; return false; }
        value = v + 0.0;
        error = null;
        return true;
    }

    // ── 글 ──

    /// <summary>입력칸 글 — "21.5"(소수 1자리).</summary>
    public static string FormatMeters(double v) => (Math.Round(v, 1, MidpointRounding.AwayFromZero) + 0.0).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>지금 높이 한 줄 — "지금 12.0 m · 4층(층고 3.0 m)".</summary>
    public static string CurrentText(double height, double floorHeight)
        => $"지금 {FormatMeters(height)} m · {FloorsFromHeight(height, floorHeight)}층(층고 {FormatMeters(floorHeight)} m)";

    /// <summary>
    /// 높이 출처 쉬운 말 — 직접 입력 · 지도 자료 · 국가 자료 · 사진에서 잼 · 그림자로 잼 · 모름(기본값).
    /// heightFrom(높이 방법) 우선, 층수 · 높이 태그 · 사용자 자료는 건물 자료 출처(origin)로 지도 · 국가를 가른다.
    /// </summary>
    public static string SourceText(string? heightFrom, string? origin)
        => heightFrom switch
        {
            "manual" or "edit" => "직접 입력",
            "tag" or "levels" or "user" => origin == "national" ? "국가 자료" : "지도 자료",
            "oblique" or "overlay" or "llm" => "사진에서 잼",
            "shadow" => "그림자로 잼",
            _ => "모름(기본값)",
        };

    /// <summary>3D 목록 · 배지 한 마디 — "직접 입력한 건물 높이 3개"(0 이면 빈 글).</summary>
    public static string ManualCountText(int count) => count > 0 ? $"직접 입력한 건물 높이 {count}개" : "";

    /// <summary>다시 굽기에서 못 붙인 직접 입력 알림.</summary>
    public static string UnmatchedText(string areaTitle, int count)
        => $"3D 영역 '{areaTitle}' — 다시 붙이지 못한 직접 입력 {count}개(그 자리에 건물이 없습니다). 지우지 않고 남겨 두었습니다.";

    /// <summary>저장 뒤 상태 글.</summary>
    public static string SavedText(double height, int floors) => $"건물 높이를 {FormatMeters(height)} m({floors}층)로 저장했습니다";

    private static double? Finite(JToken? t)
        => t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) && double.IsFinite(t.Value<double>()) ? t.Value<double>() : null;

    private static string? Str(JToken? t) => t?.Type == JTokenType.String && t.Value<string>() is { Length: > 0 } s ? s : null;

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
