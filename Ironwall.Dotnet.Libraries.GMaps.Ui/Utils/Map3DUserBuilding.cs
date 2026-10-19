using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 직접 그린 건물(「건물 그리기」) 순수 함수 · 그리기 상태 기계 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DUserBuildingTests).
                  ① 윤곽 그리기 상태 기계(점 추가 · 첫 점/더블클릭/Enter 로 닫기 · 꼭짓점 끌기 · 선분 가운데 손잡이로 점 넣기 ·
                    오른쪽 클릭/Delete 지우기 · Backspace · Esc 두 단계 · 되돌리기) — 이미지 다각형 자르기(ImageCropViewModel) 편집 관용구.
                  ② 검사 — 3점 미만 · 자기 교차 · 너무 작음 · 3D 영역 밖
                  ③ map-3d 요청 본문(PUT …/user-buildings/{uid}) · 응답 해석 · 3D 상자 미리보기 메시지(userbuilding.preview)
   Note         : 점은 위경도(X = 경도, Y = 위도)로 갖고, 손잡이 찾기 · 끌기는 화면 좌표로 한다 — 화면 ↔ 위경도는 지도(회전 · 디지털 줌 포함)가
                  넘겨준 함수로 바꾼다(회전된 지도에서도 같은 규칙). 저장 · 굽기는 map-3d(userbuildings.py).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>평면 점 — 위경도면 X = 경도 · Y = 위도, 화면이면 px.</summary>
public readonly record struct UbPoint(double X, double Y);

/// <summary>서버에 저장된 직접 그린 건물 하나. BakedId = 지금 장면에 구워진 건물 id(아직 다시 만들지 않았으면 null).</summary>
public sealed record Map3DUserBuildingItem(string Uid, IReadOnlyList<UbPoint> Outline, double Height, int? Floors, string? Name, int? BakedId);

/// <summary>손잡이 종류 — 꼭짓점 · 선분 가운데(끌면 점이 생긴다).</summary>
public enum UbHandleKind { None, Vertex, Midpoint }

public readonly record struct UbHandleHit(UbHandleKind Kind, int Index);

/// <summary>Esc 한 번의 결과 — 끌기 취소 · 그리던 점 지움(그리기는 그대로) · 그리기 끝내기(호출자가 모드를 끝낸다).</summary>
public enum UbEscapeResult { CanceledDrag, ClearedPoints, Exit }

public static class Map3DUserBuilding
{
    public const int MinPoints = 3, MaxPoints = 200;
    public const double MinAreaM2 = 4.0;
    /// <summary>손잡이 잡는 반지름(px).</summary>
    public const double HandleRadius = 7.0;
    /// <summary>누름 → 뗌이 이만큼(px) 안이면 클릭, 넘으면 끌기.</summary>
    public const double DragDeadZonePx = 4.0;
    public const int MaxName = 60;
    /// <summary>되돌리기 기록 설명 앞머리 — 그리기 중 Ctrl+Z 가 GIS 편집 기록의 이 기록만 되돌린다.</summary>
    public const string UndoPrefix = "직접 그린 건물";

    public const string TooFewText = "점이 3개 이상 있어야 건물 윤곽이 됩니다.";
    public const string CrossText = "윤곽 선이 서로 엇갈립니다 — 꼭짓점을 옮겨 주세요.";
    public const string TooSmallText = "윤곽이 너무 작습니다(4 m² 미만).";
    public const string OutsideText = "3D 영역 밖입니다 — 영역 안에 그려 주세요.";
    public const string TooManyText = "꼭짓점은 200개까지입니다.";

    // ── 검사 ──

    /// <summary>닫힌 윤곽 검사 — 문제가 없으면 null, 있으면 사용자용 이유 한 줄. inside = 3D 영역 안 판정(없으면 검사 안 함).</summary>
    public static string? Validate(IReadOnlyList<UbPoint> geo, Func<UbPoint, bool>? inside = null)
    {
        if (geo.Count < MinPoints) return TooFewText;
        if (geo.Count > MaxPoints) return TooManyText;
        if (inside != null && geo.Any(p => !inside(p))) return OutsideText;
        if (IsSelfIntersecting(geo, closed: true)) return CrossText;
        if (AreaSquareMeters(geo) < MinAreaM2) return TooSmallText;
        return null;
    }

    /// <summary>
    /// 선분끼리 엇갈리나(이웃 선분 제외 · 같은 점 겹침 포함). 위경도 그대로 셈한다 — 작은 영역에서 위경도 → 미터는 아핀이라 교차 판정이 같다.
    /// </summary>
    public static bool IsSelfIntersecting(IReadOnlyList<UbPoint> pts, bool closed)
    {
        int n = pts.Count;
        if (n < 3) return false;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (pts[i] == pts[j]) return true;   // 같은 자리 점 두 개
        int edges = closed ? n : n - 1;
        for (int i = 0; i < edges; i++)
            for (int j = i + 1; j < edges; j++)
            {
                if (j == i + 1 || (closed && i == 0 && j == n - 1)) continue;   // 이웃 선분(꼭짓점 공유)
                if (SegmentsTouch(pts[i], pts[(i + 1) % n], pts[j], pts[(j + 1) % n])) return true;
            }
        return false;
    }

    /// <summary>열린 윤곽 끝에 점을 더하면 새 선분이 앞 선분과 엇갈리나(마지막 선분 · 이웃 제외).</summary>
    public static bool WouldCross(IReadOnlyList<UbPoint> open, UbPoint next)
    {
        int n = open.Count;
        if (n == 0) return false;
        if (open.Contains(next)) return true;
        var a = open[n - 1];
        for (int i = 0; i < n - 2; i++)
            if (SegmentsTouch(open[i], open[i + 1], a, next)) return true;
        return false;
    }

    private static bool SegmentsTouch(UbPoint p1, UbPoint p2, UbPoint q1, UbPoint q2)
    {
        double d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2> 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
        return (d1 == 0 && OnSegment(q1, q2, p1)) || (d2 == 0 && OnSegment(q1, q2, p2))
            || (d3 == 0 && OnSegment(p1, p2, q1)) || (d4 == 0 && OnSegment(p1, p2, q2));
    }

    private static double Cross(UbPoint a, UbPoint b, UbPoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(UbPoint a, UbPoint b, UbPoint p)
        => Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);

    /// <summary>위경도 윤곽 넓이(m², 첫 점 위도의 등장방형 근사 — map-3d heights._meters 와 같은 식).</summary>
    public static double AreaSquareMeters(IReadOnlyList<UbPoint> geo)
    {
        if (geo.Count < 3) return 0;
        double kx = 111320.0 * Math.Cos(geo[0].Y * Math.PI / 180.0), ky = 110540.0, s = 0;
        for (int i = 0; i < geo.Count; i++)
        {
            var a = geo[i]; var b = geo[(i + 1) % geo.Count];
            s += (a.X - geo[0].X) * kx * (b.Y - geo[0].Y) * ky - (b.X - geo[0].X) * kx * (a.Y - geo[0].Y) * ky;
        }
        return Math.Abs(s) / 2.0;
    }

    /// <summary>윤곽 가운데(꼭짓점 평균 — 표시용).</summary>
    public static UbPoint Center(IReadOnlyList<UbPoint> pts)
        => pts.Count == 0 ? default : new UbPoint(pts.Average(p => p.X), pts.Average(p => p.Y));

    /// <summary>점(위경도)이 다각형(위경도) 안인가(짝홀 규칙) — 2D 에서 저장된 건물 고르기.</summary>
    public static bool Contains(IReadOnlyList<UbPoint> poly, UbPoint p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    // ── 서버 · 3D 메시지 ──

    /// <summary>새 건물 식별자 — 소문자 16진수 12자(map-3d UID_RE).</summary>
    public static string NewUid() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>위경도 소수 7자리(약 1 cm).</summary>
    public static UbPoint Round(UbPoint p) => new(Math.Round(p.X, 7) + 0.0, Math.Round(p.Y, 7) + 0.0);

    private static JArray OutlineJson(IEnumerable<UbPoint> outline)
        => new(outline.Select(p => (JToken)new JArray(Math.Round(p.X, 7), Math.Round(p.Y, 7))));

    /// <summary>PUT …/user-buildings/{uid} 본문 — {"outline":[[lon,lat]…],"height","floors","name"}(이름이 없으면 null).</summary>
    public static JObject PutBody(IReadOnlyList<UbPoint> outline, double height, int? floors, string? name)
        => new()
        {
            ["outline"] = OutlineJson(outline),
            ["height"] = Map3DBuilding.ClampHeight(height),
            ["floors"] = floors is int f ? f : JValue.CreateNull(),
            ["name"] = string.IsNullOrWhiteSpace(name) ? JValue.CreateNull() : name.Trim().Length > MaxName ? name.Trim()[..MaxName] : name.Trim(),
        };

    /// <summary>3D 상자 미리보기(다시 만들기 전) — 높이 null = 그 상자 지움.</summary>
    public static string PreviewMessageJson(string uid, IReadOnlyList<UbPoint>? outline, double? height)
    {
        var o = new JObject { ["type"] = "userbuilding.preview", ["uid"] = uid };
        if (height is double h && outline != null) { o["outline"] = OutlineJson(outline); o["height"] = h; }
        else o["height"] = JValue.CreateNull();
        return o.ToString(Formatting.None);
    }

    /// <summary>3D 상자 모두 지움(「모두 지우기」).</summary>
    public static string ClearMessageJson() => new JObject { ["type"] = "userbuilding.clear" }.ToString(Formatting.None);

    /// <summary>서버 항목 해석 — uid · 윤곽(3점 이상 · 유한한 경위도) · 양의 높이가 없으면 null.</summary>
    public static Map3DUserBuildingItem? ParseItem(JObject? o)
    {
        if (o?["uid"]?.Type != JTokenType.String || o["outline"] is not JArray arr) return null;
        string uid = o.Value<string>("uid")!;
        if (uid.Length != 12 || !uid.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return null;
        var pts = new List<UbPoint>();
        foreach (var t in arr)
        {
            if (t is not JArray p || p.Count != 2 || Num(p[0]) is not double x || Num(p[1]) is not double y || Math.Abs(x) > 180 || Math.Abs(y) > 90) return null;
            pts.Add(new UbPoint(x, y));
        }
        if (pts.Count < MinPoints || Num(o["height"]) is not double h || h <= 0) return null;
        int? floors = o["floors"]?.Type == JTokenType.Integer && o.Value<long>("floors") is long f && f is > 0 and <= 10_000 ? (int)f : null;
        int? baked = o["baked_id"]?.Type == JTokenType.Integer && o.Value<long>("baked_id") is long b && b is > 0 and <= int.MaxValue ? (int)b : null;
        string? name = o["name"]?.Type == JTokenType.String && o.Value<string>("name") is { Length: > 0 } s ? s : null;
        return new Map3DUserBuildingItem(uid, pts, h, floors, name, baked);
    }

    /// <summary>GET …/user-buildings 응답 {"items":[…]} — 형식이 틀린 항목은 뺀다.</summary>
    public static IReadOnlyList<Map3DUserBuildingItem> ParseList(string json)
    {
        var list = new List<Map3DUserBuildingItem>();
        if (JObject.Parse(json)["items"] is JArray items)
            foreach (var o in items.OfType<JObject>())
                if (ParseItem(o) is { } it) list.Add(it);
        return list;
    }

    private static double? Num(JToken? t)
        => t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) && double.IsFinite(t.Value<double>()) ? t.Value<double>() : null;

    // ── 글 ──

    /// <summary>"직접 그린 건물 3개"(0 이면 빈 글).</summary>
    public static string CountText(int count) => count > 0 ? $"직접 그린 건물 {count}개" : "";

    /// <summary>판 · 띠의 꼭짓점 한 줄.</summary>
    public static string PointCountText(int count, bool closed)
        => closed ? $"꼭짓점 {count}개 · 닫힘"
         : count == 0 ? "지도를 눌러 첫 꼭짓점을 찍으세요"
         : count < MinPoints ? $"꼭짓점 {count}개 — 3개 이상 찍으세요"
         : $"꼭짓점 {count}개 — 첫 점을 누르거나 Enter 로 닫기";

    /// <summary>저장 뒤 상태 글.</summary>
    public static string SavedText(double height, int floors)
        => $"건물을 저장했습니다 — {Map3DBuilding.FormatMeters(height)} m({floors}층). 3D 를 다시 만들면 사진으로 꾸며집니다";
}

/// <summary>
/// 건물 윤곽 그리기 상태 기계 — ImageCropViewModel 의 편집 관용구(누르기 · 끌기 · 놓기 · 오른쪽 클릭 · 키 · 되돌리기)를 지도 위경도로.
/// toScreen · toGeo 는 지도가 준다(회전 · 디지털 줌 포함). inside 는 3D 영역 안 판정(밖 점은 받지 않는다).
/// </summary>
public sealed class Map3DUserBuildingDraft
{
    private readonly Func<UbPoint, UbPoint> _toScreen;
    private readonly Func<UbPoint, UbPoint> _toGeo;
    private readonly Func<UbPoint, bool>? _inside;
    private List<UbPoint> _points = new();
    private bool _closed;
    private readonly Stack<(UbPoint[] Points, bool Closed)> _undo = new();
    private UbPoint? _press;
    private UbHandleHit _pressHit;
    private bool _dragging;
    private int _dragVertex = -1;

    public Map3DUserBuildingDraft(Func<UbPoint, UbPoint> toScreen, Func<UbPoint, UbPoint> toGeo, Func<UbPoint, bool>? inside = null)
    {
        _toScreen = toScreen;
        _toGeo = toGeo;
        _inside = inside;
    }

    /// <summary>윤곽이 바뀌었거나 고른 점 · 끌기 상태가 바뀌었다(다시 그리기).</summary>
    public event Action? Changed;

    public IReadOnlyList<UbPoint> Points => _points;
    public bool IsClosed => _closed;
    public int SelectedVertex { get; private set; } = -1;
    public bool IsDragging => _dragging;
    public int DraggingVertex => _dragVertex;
    /// <summary>마지막으로 막은 이유(밖 · 엇갈림 · 3점 미만) — 다음 성공한 편집에서 지운다.</summary>
    public string? LastError { get; private set; }
    /// <summary>되돌릴 칸이 있나.</summary>
    public bool CanUndo => _undo.Count > 0;
    /// <summary>지금 커서(화면) — 열린 윤곽의 다음 선 미리보기.</summary>
    public UbPoint? Hover { get; private set; }

    /// <summary>저장된 건물 고치기 — 닫힌 윤곽으로 연다(되돌리기 칸 비움).</summary>
    public void Load(IEnumerable<UbPoint> outline)
    {
        _points = outline.ToList();
        _closed = _points.Count >= Map3DUserBuilding.MinPoints;
        _undo.Clear();
        SelectedVertex = -1;
        LastError = null;
        Changed?.Invoke();
    }

    /// <summary>지금 윤곽 검사(닫힘 포함) — 문제 없으면 null.</summary>
    public string? Validate() => !_closed ? Map3DUserBuilding.TooFewText : Map3DUserBuilding.Validate(_points, _inside);

    // ── 손잡이 ──

    public UbHandleHit HitTest(UbPoint screen)
    {
        for (int i = _points.Count - 1; i >= 0; i--)
            if (Dist(_toScreen(_points[i]), screen) <= Map3DUserBuilding.HandleRadius) return new(UbHandleKind.Vertex, i);
        foreach (var (index, mid) in MidpointsScreen())
            if (Dist(mid, screen) <= Map3DUserBuilding.HandleRadius) return new(UbHandleKind.Midpoint, index);
        return new(UbHandleKind.None, -1);
    }

    /// <summary>선분 가운데 손잡이(화면) — 닫힌 윤곽만(그리는 중에는 점 찍기와 헷갈리지 않게).</summary>
    public IEnumerable<(int EdgeIndex, UbPoint Screen)> MidpointsScreen()
    {
        if (!_closed) yield break;
        for (int i = 0; i < _points.Count; i++)
        {
            var a = _toScreen(_points[i]);
            var b = _toScreen(_points[(i + 1) % _points.Count]);
            yield return (i, new UbPoint((a.X + b.X) / 2d, (a.Y + b.Y) / 2d));
        }
    }

    private static double Dist(UbPoint a, UbPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ── 누르기 · 끌기 · 놓기 ──

    public void PointerDown(UbPoint screen)
    {
        _press = screen;
        _pressHit = HitTest(screen);
        _dragging = false;
        _dragVertex = -1;
        SelectedVertex = _pressHit.Kind == UbHandleKind.Vertex ? _pressHit.Index : -1;
        Changed?.Invoke();
    }

    public void PointerMove(UbPoint screen)
    {
        Hover = screen;
        if (_press is not { } press)
        {
            if (!_closed && _points.Count > 0) Changed?.Invoke();   // 다음 선 미리보기
            return;
        }
        if (!_dragging)
        {
            if (Dist(press, screen) <= Map3DUserBuilding.DragDeadZonePx) return;
            if (_pressHit.Kind == UbHandleKind.None) return;   // 빈 곳 끌기는 아무것도 안 한다
            _dragging = true;
            Snapshot();
            if (_pressHit.Kind == UbHandleKind.Vertex) _dragVertex = _pressHit.Index;
            else
            {
                _dragVertex = _pressHit.Index + 1;
                _points.Insert(_dragVertex, _toGeo(screen));
            }
        }
        _points[_dragVertex] = _toGeo(screen);
        Changed?.Invoke();
    }

    /// <summary>놓기 — 끌었으면 검사(엇갈림 · 밖이면 끌기 전으로), 아니면 클릭(첫 점 = 닫기 · 빈 곳 = 점 더하기).</summary>
    public void PointerUp(UbPoint screen)
    {
        if (_press is null) return;
        var hit = _pressHit;
        bool dragged = _dragging;
        _press = null;
        _dragging = false;
        int moved = _dragVertex;
        _dragVertex = -1;
        if (dragged)
        {
            string? why = _inside != null && !_inside(_points[moved]) ? Map3DUserBuilding.OutsideText
                        : Map3DUserBuilding.IsSelfIntersecting(_points, _closed) ? Map3DUserBuilding.CrossText : null;
            if (why != null) { Restore(_undo.Pop()); LastError = why; }
            else LastError = null;
            Changed?.Invoke();
            return;
        }
        if (_closed) { Changed?.Invoke(); return; }
        if (hit.Kind == UbHandleKind.Vertex && hit.Index == 0 && _points.Count >= Map3DUserBuilding.MinPoints) { Close(); return; }
        if (hit.Kind == UbHandleKind.Vertex) return;   // 다른 꼭짓점 클릭 = 고르기만(Delete 대상)
        AddPoint(_toGeo(screen));
    }

    /// <summary>캡처를 잃었거나 끄는 중 Esc — 끌기 전으로. 끄는 중이었으면 true.</summary>
    public bool PointerCancel()
    {
        bool was = _dragging;
        _press = null;
        _dragging = false;
        _dragVertex = -1;
        if (!was) return false;
        Restore(_undo.Pop());
        return true;
    }

    /// <summary>두 번 누르기 = 닫기(첫 누르기가 이미 점을 놓았다).</summary>
    public bool DoubleClick() => Close();

    /// <summary>오른쪽 클릭 — 꼭짓점 위면 지운다. 지웠으면 true.</summary>
    public bool RightClick(UbPoint screen)
    {
        var hit = HitTest(screen);
        return hit.Kind == UbHandleKind.Vertex && RemoveVertex(hit.Index);
    }

    // ── 편집 ──

    /// <summary>점 더하기(열린 윤곽) — 영역 밖 · 앞 선과 엇갈림 · 200개 넘음이면 막고 이유를 남긴다.</summary>
    public bool AddPoint(UbPoint geo)
    {
        if (_closed) return false;
        string? why = _inside != null && !_inside(geo) ? Map3DUserBuilding.OutsideText
                    : _points.Count >= Map3DUserBuilding.MaxPoints ? Map3DUserBuilding.TooManyText
                    : Map3DUserBuilding.WouldCross(_points, geo) ? Map3DUserBuilding.CrossText : null;
        if (why != null) { LastError = why; Changed?.Invoke(); return false; }
        Snapshot();
        _points.Add(geo);
        LastError = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>닫기 — 3점 이상 · 닫는 선이 엇갈리지 않을 때만.</summary>
    public bool Close()
    {
        if (_closed) return false;
        if (_points.Count < Map3DUserBuilding.MinPoints) { LastError = Map3DUserBuilding.TooFewText; Changed?.Invoke(); return false; }
        if (Map3DUserBuilding.IsSelfIntersecting(_points, closed: true)) { LastError = Map3DUserBuilding.CrossText; Changed?.Invoke(); return false; }
        Snapshot();
        _closed = true;
        LastError = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>꼭짓점 지우기 — 닫힌 윤곽이 3점 밑이 되면 열린다. 지운 뒤 엇갈리면 막는다.</summary>
    public bool RemoveVertex(int index)
    {
        if (index < 0 || index >= _points.Count) return false;
        var next = _points.ToList();
        next.RemoveAt(index);
        bool closed = _closed && next.Count >= Map3DUserBuilding.MinPoints;
        if (Map3DUserBuilding.IsSelfIntersecting(next, closed)) { LastError = Map3DUserBuilding.CrossText; Changed?.Invoke(); return false; }
        Snapshot();
        _points = next;
        _closed = closed;
        SelectedVertex = -1;
        LastError = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Backspace — 그리는 중이면 마지막 점, 닫힌 윤곽이면 고른 점.</summary>
    public bool RemoveLast() => RemoveVertex(_closed ? SelectedVertex : _points.Count - 1);

    /// <summary>Delete — 고른 점.</summary>
    public bool RemoveSelected() => RemoveVertex(SelectedVertex);

    /// <summary>Esc 두 단계 — 끄는 중이면 끌기 취소, 그리던(열린) 점이 있으면 지움, 아니면 그리기 끝내기.</summary>
    public UbEscapeResult Escape()
    {
        if (PointerCancel()) return UbEscapeResult.CanceledDrag;
        if (!_closed && _points.Count > 0)
        {
            Snapshot();
            _points.Clear();
            LastError = null;
            Changed?.Invoke();
            return UbEscapeResult.ClearedPoints;
        }
        return UbEscapeResult.Exit;
    }

    /// <summary>되돌리기(Ctrl+Z) — 했으면 true.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        Restore(_undo.Pop());
        return true;
    }

    private void Snapshot() => _undo.Push((_points.ToArray(), _closed));

    private void Restore((UbPoint[] Points, bool Closed) s)
    {
        _points = s.Points.ToList();
        _closed = s.Closed;
        SelectedVertex = -1;
        LastError = null;
        Changed?.Invoke();
    }
}
