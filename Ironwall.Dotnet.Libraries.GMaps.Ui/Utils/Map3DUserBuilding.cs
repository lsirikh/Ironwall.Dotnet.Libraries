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
public sealed record Map3DUserBuildingItem(string Uid, IReadOnlyList<UbPoint> Outline, double Height, int? Floors, string? Name, int? BakedId)
{
    /// <summary>지붕 모양(flat · gable · cone · dome).</summary>
    public string Roof { get; init; } = Map3DUserBuilding.RoofFlat;
    /// <summary>외벽 성격(auto · commercial · industrial · residential).</summary>
    public string Use { get; init; } = Map3DUserBuilding.UseAuto;
    /// <summary>외벽 색 · 지붕 색("#rrggbb", null = 자동) · 외벽 무늬(Map3DLook.Facades, twin-contract B).</summary>
    public string? WallColor { get; init; }
    public string? RoofColor { get; init; }
    public string Facade { get; init; } = Map3DLook.FacadeAuto;
    /// <summary>위로 갈수록 좁게(+) · 넓게(−) 기울기(°, −30~+30, 0 = 수직) — user-buildings 항목 taper_deg(옛 항목은 키가 없어 0).</summary>
    public double TaperDeg { get; init; }
}

/// <summary>합치기(POST …/user-buildings/merge) 응답 — 합친 건물 · 합쳐진(지워진) uid · 채운 안쪽 빈 곳 수 · 지붕을 평지붕으로 바꿨는가.</summary>
public sealed record Map3DUserBuildingMergeResult(Map3DUserBuildingItem Item, IReadOnlyList<string> Merged, int HolesFilled, bool RoofForcedFlat);

/// <summary>윤곽 그리기 도구 — 다각형(선 클릭) · 사각형 · 원 · 타원 · 삼각형.</summary>
public enum UbTool { Polygon, Rect, Circle, Ellipse, Triangle }

/// <summary>손잡이 종류 — 꼭짓점 · 선분 가운데(끌면 점이 생긴다) · 회전 손잡이 · 몸통(끌면 통째로 옮김).</summary>
public enum UbHandleKind { None, Vertex, Midpoint, Rotate, Body }

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
    /// <summary>열린 다각형의 첫 점에 이만큼(px) 안을 누르면 닫힌다(붙는 거리).</summary>
    public const double CloseSnapPx = 10.0;
    /// <summary>회전 손잡이 — 윤곽 위쪽 끝에서 이만큼(px) 위.</summary>
    public const double RotateHandleOffsetPx = 28.0;
    /// <summary>원 · 타원 근사 꼭짓점 수(기본 · 범위).</summary>
    public const int DefaultCircleSides = 32, MinCircleSides = 8, MaxCircleSides = 128;

    public const string RoofFlat = "flat", RoofGable = "gable", RoofCone = "cone", RoofDome = "dome", RoofHip = "hip", RoofShed = "shed";
    public const string UseAuto = "auto", UseCommercial = "commercial", UseIndustrial = "industrial", UseResidential = "residential";
    /// <summary>판의 지붕 모양 고르기(값, 쉬운 말).</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Roofs = new[]
    {
        new KeyValuePair<string, string>(RoofFlat, "평지붕"), new(RoofGable, "박공(세모 지붕)"),
        new(RoofCone, "원뿔(둥근 윤곽)"), new(RoofDome, "돔(둥근 윤곽 · 탱크)"),
        new(RoofHip, "모임지붕(네 면 경사)"), new(RoofShed, "외쪽 지붕(한쪽 경사)"),
    };
    /// <summary>판의 외벽 성격 고르기(값, 쉬운 말).</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Uses = new[]
    {
        new KeyValuePair<string, string>(UseAuto, "자동"), new(UseCommercial, "사무"), new(UseIndustrial, "공장"), new(UseResidential, "주거"),
    };

    // ── 형상(화면 좌표) ──

    /// <summary>사각형(대각 두 점, 화면 기준) — square = 정사각형(긴 변에 맞춤). a → 가로 → b → 세로 순서.</summary>
    public static IReadOnlyList<UbPoint> RectPoints(UbPoint a, UbPoint b, bool square = false)
    {
        if (square)
        {
            double s = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            b = new UbPoint(a.X + (b.X >= a.X ? s : -s), a.Y + (b.Y >= a.Y ? s : -s));
        }
        return new[] { a, new UbPoint(b.X, a.Y), b, new UbPoint(a.X, b.Y) };
    }

    /// <summary>
    /// 이등변 삼각형(끈 상자의 대각 두 점, 화면 기준) — 꼭대기 = 상자 위 변 가운데, 밑변 = 상자 아래 변. 꼭대기 → 오른쪽 아래 → 왼쪽 아래 순서.
    /// equilateral = 정삼각형(높이를 밑변 폭 × √3/2 로, a 쪽 높이에서 끈 방향으로).
    /// </summary>
    public static IReadOnlyList<UbPoint> TrianglePoints(UbPoint a, UbPoint b, bool equilateral = false)
    {
        if (equilateral) b = new UbPoint(b.X, a.Y + (b.Y >= a.Y ? 1 : -1) * Math.Abs(b.X - a.X) * Math.Sqrt(3) / 2);
        double left = Math.Min(a.X, b.X), right = Math.Max(a.X, b.X), top = Math.Min(a.Y, b.Y), bottom = Math.Max(a.Y, b.Y);
        return new[] { new UbPoint((left + right) / 2, top), new UbPoint(right, bottom), new UbPoint(left, bottom) };
    }

    /// <summary>타원(가운데 · 가로 반지름 · 세로 반지름 · 회전 rad, 화면 기준)을 n 각형으로.</summary>
    public static IReadOnlyList<UbPoint> EllipsePoints(UbPoint c, double rx, double ry, double angle, int n)
    {
        n = Math.Clamp(n, MinCircleSides, MaxCircleSides);
        double ca = Math.Cos(angle), sa = Math.Sin(angle);
        var pts = new UbPoint[n];
        for (int i = 0; i < n; i++)
        {
            double t = 2 * Math.PI * i / n, x = rx * Math.Cos(t), y = ry * Math.Sin(t);
            pts[i] = new UbPoint(c.X + x * ca - y * sa, c.Y + x * sa + y * ca);
        }
        return pts;
    }

    /// <summary>원(가운데 · 반지름)을 n 각형으로.</summary>
    public static IReadOnlyList<UbPoint> CirclePoints(UbPoint c, double r, int n) => EllipsePoints(c, r, r, 0, n);

    /// <summary>타원 세 번 클릭 — 가운데 · 긴 축 끝 · 짧은 축 쪽 점(긴 축 선까지의 거리 = 세로 반지름).</summary>
    public static IReadOnlyList<UbPoint> EllipseFrom3(UbPoint c, UbPoint major, UbPoint minor, int n)
    {
        double rx = Dist(c, major), angle = Math.Atan2(major.Y - c.Y, major.X - c.X);
        double ry = rx < 1e-9 ? 0 : Math.Abs((major.X - c.X) * (minor.Y - c.Y) - (major.Y - c.Y) * (minor.X - c.X)) / rx;
        return EllipsePoints(c, rx, ry, angle, n);
    }

    /// <summary>
    /// 직각 맞춤(Shift) — 다음 점을 앞 변 방향 기준 0 · 45 · 90 ° 단위 방향으로(길이는 그대로). 앞 변이 없으면(점 1개) 화면 가로 기준.
    /// </summary>
    public static UbPoint SnapAngle(UbPoint? prev, UbPoint last, UbPoint cand)
    {
        double len = Dist(last, cand);
        if (len < 1e-9) return cand;
        double baseAngle = prev is { } p ? Math.Atan2(last.Y - p.Y, last.X - p.X) : 0;
        double a = Math.Atan2(cand.Y - last.Y, cand.X - last.X) - baseAngle;
        double step = Math.PI / 4, snapped = baseAngle + Math.Round(a / step) * step;
        return new UbPoint(last.X + len * Math.Cos(snapped), last.Y + len * Math.Sin(snapped));
    }

    internal static double Dist(UbPoint a, UbPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    /// <summary>되돌리기 기록 설명 앞머리 — 그리기 중 Ctrl+Z 가 GIS 편집 기록의 이 기록만 되돌린다.</summary>
    public const string UndoPrefix = "직접 그린 건물";

    /// <summary>위로 갈수록 좁게 · 넓게(taper_deg) 범위 — 1° 단위.</summary>
    public const int MinTaper = -30, MaxTaper = 30;

    /// <summary>기울기를 1° 단위 · −30~+30 으로(유한하지 않으면 0 = 수직).</summary>
    public static double ClampTaper(double deg)
        => double.IsFinite(deg) ? Math.Clamp(Math.Round(deg, MidpointRounding.AwayFromZero), MinTaper, MaxTaper) + 0.0 : 0;

    /// <summary>판 숫자 칸 — 정수 −30~+30(앞의 + 허용). 틀리면 사용자용 이유.</summary>
    public static bool TryParseTaper(string? text, out double deg, out string? error)
    {
        deg = 0;
        error = null;
        var t = (text ?? "").Trim().Replace("°", "");
        if (t.Length == 0) t = "0";
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v)
            || v < MinTaper || v > MaxTaper)
        {
            error = $"좁게 · 넓게는 {MinTaper} ~ +{MaxTaper}° 사이로 넣어 주세요.";
            return false;
        }
        deg = ClampTaper(v);
        return true;
    }

    /// <summary>판 기울기 줄 — "수직" · "위로 갈수록 좁게 10°" · "위로 갈수록 넓게 5°".</summary>
    public static string TaperText(double deg)
        => deg == 0 ? "수직(위아래 같은 넓이)" : deg > 0 ? $"위로 갈수록 좁게 {deg:0}°" : $"위로 갈수록 넓게 {-deg:0}°";

    public const string TooFewText = "점이 3개 이상 있어야 건물 윤곽이 됩니다.";
    public const string CrossText = "윤곽 선이 서로 엇갈립니다 — 꼭짓점을 옮겨 주세요.";
    public const string TooSmallText = "윤곽이 너무 작습니다(4 m² 미만).";
    public const string OutsideText = "3D 영역 밖입니다 — 영역 안에 그려 주세요.";
    public const string TooManyText = "꼭짓점은 200개까지입니다.";
    public const string OldServerText = "이 3D 서버(map-3d)는 직접 그린 건물을 저장하지 못합니다 — map-3d 를 새 판으로 바꿔 주세요.";

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

    /// <summary>박공 지붕이 되는 채움 비율 — 윤곽 넓이 ÷ 그것을 감싸는 가장 작은(돌린) 직사각형 넓이(map-3d buildings.build_building 의 0.85).</summary>
    public const double GableMinFill = 0.85;
    /// <summary>박공(또는 원뿔 · 돔)을 골랐지만 그 모양으로는 안 될 때 판의 상태 글.</summary>
    public const string GableFlatOnlyText = "이 모양은 평지붕만 됩니다";

    /// <summary>
    /// 박공 지붕이 되는 윤곽인가 — map-3d buildings.build_building 과 같은 판정: 감싸는 가장 작은(돌린) 직사각형이 4 m² 보다 넓고
    /// 윤곽이 그 넓이의 <see cref="GableMinFill"/> 이상을 채워야 한다. 아니면(ㄱ자 · ㄷ자 · 원 · 타원 등) map-3d 가 평지붕으로 짓는다.
    /// 미터는 <see cref="AreaSquareMeters"/> 와 같은 첫 점 위도의 등장방형 근사.
    /// </summary>
    public static bool CanGable(IReadOnlyList<UbPoint> geo)
    {
        if (geo.Count < MinPoints) return false;
        double kx = 111320.0 * Math.Cos(geo[0].Y * Math.PI / 180.0), ky = 110540.0;
        var m = geo.Select(p => new UbPoint((p.X - geo[0].X) * kx, (p.Y - geo[0].Y) * ky)).ToList();
        var hull = ConvexHull(m);
        double best = double.MaxValue;
        for (int i = 0; i < hull.Count; i++)   // 가장 작은 돌린 직사각형의 한 변은 볼록 껍질의 한 변과 나란하다
        {
            var a = hull[i]; var b = hull[(i + 1) % hull.Count];
            double len = Dist(a, b);
            if (len < 1e-9) continue;
            double ux = (b.X - a.X) / len, uy = (b.Y - a.Y) / len;
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            foreach (var p in hull)
            {
                double u = p.X * ux + p.Y * uy, v = -p.X * uy + p.Y * ux;
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }
            best = Math.Min(best, (maxU - minU) * (maxV - minV));
        }
        return best < double.MaxValue && best > MinAreaM2 && AreaSquareMeters(geo) / best >= GableMinFill;
    }

    /// <summary>
    /// 원뿔 · 돔 지붕이 되는 둥근 윤곽인가 — map-3d buildings.is_round 와 같은 판정: 꼭짓점 16개 이상(닫힌 고리 좌표 &gt; 16)이고
    /// 원형도 4π·넓이 ÷ 둘레² ≥ 0.9(원 도구 32각은 통과). 아니면 map-3d 가 평지붕으로 짓는다.
    /// </summary>
    public static bool CanRoundRoof(IReadOnlyList<UbPoint> geo)
    {
        if (geo.Count < 16) return false;
        double kx = 111320.0 * Math.Cos(geo[0].Y * Math.PI / 180.0), ky = 110540.0, perimeter = 0;
        for (int i = 0; i < geo.Count; i++)
        {
            var a = geo[i]; var b = geo[(i + 1) % geo.Count];
            perimeter += Math.Sqrt(Math.Pow((b.X - a.X) * kx, 2) + Math.Pow((b.Y - a.Y) * ky, 2));
        }
        return perimeter > 0 && 4 * Math.PI * AreaSquareMeters(geo) / (perimeter * perimeter) >= 0.9;
    }

    /// <summary>고른 지붕을 그 윤곽으로 지을 수 있는가(평지붕은 늘 된다) — 안 되면 map-3d 가 평지붕으로 짓는다.</summary>
    public static bool CanBuildRoof(string roof, IReadOnlyList<UbPoint> geo)
        => roof is RoofGable or RoofHip or RoofShed ? CanGable(geo) : roof is RoofCone or RoofDome ? CanRoundRoof(geo) : true;   // 모임 · 외쪽도 박공처럼 네모에 가까운 모양

    /// <summary>볼록 껍질(단조 사슬, 반시계).</summary>
    private static List<UbPoint> ConvexHull(IReadOnlyList<UbPoint> pts)
    {
        var s = pts.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (s.Count < 3) return s;
        var h = new List<UbPoint>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = h.Count;
            foreach (var p in s)
            {
                while (h.Count >= start + 2 && Cross(h[^2], h[^1], p) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            h.RemoveAt(h.Count - 1);
            s.Reverse();
        }
        return h;
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
        => PutBody(outline, height, floors, name, RoofFlat, UseAuto);

    /// <summary>PUT 본문 — 지붕 모양 · 외벽 성격까지(모르는 값은 평지붕 · 자동).</summary>
    public static JObject PutBody(IReadOnlyList<UbPoint> outline, double height, int? floors, string? name, string roof, string use)
        => PutBody(outline, height, floors, name, roof, use, null);

    /// <summary>PUT 본문 — 외벽 색 · 지붕 색(null = 자동) · 외벽 무늬까지(look 의 Hidden 은 쓰지 않는다). look 이 null 이면 그 키를 빼서 옛 본문 그대로.</summary>
    public static JObject PutBody(IReadOnlyList<UbPoint> outline, double height, int? floors, string? name, string roof, string use, Map3DLookState? look)
        => PutBody(outline, height, floors, name, roof, use, look, null);

    /// <summary>PUT 본문 — 위로 갈수록 좁게 · 넓게(taper_deg, null = 키를 빼서 옛 본문 그대로 — 엔진이 0)까지.</summary>
    public static JObject PutBody(IReadOnlyList<UbPoint> outline, double height, int? floors, string? name, string roof, string use, Map3DLookState? look,
                                  double? taperDeg)
    {
        var body = new JObject
        {
            ["outline"] = OutlineJson(outline),
            ["height"] = Map3DBuilding.ClampHeight(height),
            ["floors"] = floors is int f ? f : JValue.CreateNull(),
            ["name"] = string.IsNullOrWhiteSpace(name) ? JValue.CreateNull() : name.Trim().Length > MaxName ? name.Trim()[..MaxName] : name.Trim(),
            ["roof"] = Roofs.Any(r => r.Key == roof) ? roof : RoofFlat,
            ["use"] = Uses.Any(u => u.Key == use) ? use : UseAuto,
        };
        if (look != null) body.Merge(Map3DLook.PutBody(null, look), Map3DLook.KeepNulls);   // wall_color · roof_color · facade
        if (taperDeg is double t) body["taper_deg"] = ClampTaper(t);
        return body;
    }

    /// <summary>항목의 외관(외벽 색 · 지붕 색 · 외벽 무늬).</summary>
    public static Map3DLookState LookOf(Map3DUserBuildingItem it) => new(false, it.WallColor, it.RoofColor, it.Facade);

    /// <summary>3D 상자 미리보기(다시 만들기 전) — 높이 null = 그 상자 지움.</summary>
    public static string PreviewMessageJson(string uid, IReadOnlyList<UbPoint>? outline, double? height)
    {
        var o = new JObject { ["type"] = "userbuilding.preview", ["uid"] = uid };
        if (height is double h && outline != null) { o["outline"] = OutlineJson(outline); o["height"] = h; }
        else o["height"] = JValue.CreateNull();
        return o.ToString(Formatting.None);
    }

    /// <summary>저장된 건물 그대로 3D 상자 미리보기 — 층수 · 외벽 색 · 지붕 색 · 외벽 무늬 · 지붕 모양 · 외벽 성격까지(뷰어가 층 띠 · 창 칸 · 지붕 색을 입힌다).</summary>
    public static string PreviewMessageJson(Map3DUserBuildingItem it)
    {
        var o = JObject.Parse(PreviewMessageJson(it.Uid, it.Outline, it.Height));
        o["floors"] = it.Floors is int f ? f : JValue.CreateNull();
        o.Merge(Map3DLook.PutBody(null, LookOf(it)), Map3DLook.KeepNulls);
        o["roof"] = it.Roof;
        o["use"] = it.Use;
        o["taper_deg"] = ClampTaper(it.TaperDeg);   // 위로 갈수록 좁게(+) · 넓게(−) — 이 키를 모르는 뷰어는 무시한다(수직 상자)   // 외벽 무늬가 자동이면 뷰어는 외벽 성격으로 무늬를 고른다(userbuilding.js boxLook) — 빠지면 성격을 바꿔 저장해도 상자가 그대로였다
        return o.ToString(Formatting.None);
    }

    /// <summary>3D 상자 고름 표시(null = 지움) — {"type":"userbuilding.select","uid"}.</summary>
    public static string SelectMessageJson(string? uid)
        => new JObject { ["type"] = "userbuilding.select", ["uid"] = uid is null ? JValue.CreateNull() : uid }.ToString(Formatting.None);

    /// <summary>map-3d UID_RE — 소문자 16진수 12자.</summary>
    public static bool IsUid(string? uid) => uid is { Length: 12 } && uid.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>3D 상자 모두 지움(「모두 지우기」).</summary>
    public static string ClearMessageJson() => new JObject { ["type"] = "userbuilding.clear" }.ToString(Formatting.None);

    /// <summary>서버 항목 해석 — uid · 윤곽(3점 이상 · 유한한 경위도) · 양의 높이가 없으면 null.</summary>
    public static Map3DUserBuildingItem? ParseItem(JObject? o)
    {
        if (o?["uid"]?.Type != JTokenType.String || o["outline"] is not JArray arr) return null;
        string uid = o.Value<string>("uid")!;
        if (!IsUid(uid)) return null;
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
        string? roof = o["roof"]?.Type == JTokenType.String ? o.Value<string>("roof") : null;
        string? use = o["use"]?.Type == JTokenType.String ? o.Value<string>("use") : null;
        var look = Map3DLook.StateOf(o);
        return new Map3DUserBuildingItem(uid, pts, h, floors, name, baked)
        {
            Roof = Roofs.Any(r => r.Key == roof) ? roof! : RoofFlat,
            Use = Uses.Any(u => u.Key == use) ? use! : UseAuto,
            WallColor = look.WallColor, RoofColor = look.RoofColor, Facade = look.Facade,
            TaperDeg = Num(o["taper_deg"]) is double taper ? ClampTaper(taper) : 0,
        };
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

    /// <summary>POST …/user-buildings/merge 본문 — {"uids":[…],"height","floors","name","use"}(null = 서버가 정함).</summary>
    public static JObject MergeBody(IReadOnlyList<string> uids, double? height = null, int? floors = null, string? name = null, string? use = null,
                                    string? uid = null, Map3DLookState? look = null)
    {
        var body = new JObject
        {
            ["uids"] = new JArray(uids.Cast<object>().ToArray()),
            ["height"] = height is double h ? Map3DBuilding.ClampHeight(h) : JValue.CreateNull(),
            ["floors"] = floors is int f ? f : JValue.CreateNull(),
            ["name"] = string.IsNullOrWhiteSpace(name) ? JValue.CreateNull() : name.Trim().Length > MaxName ? name.Trim()[..MaxName] : name.Trim(),
            ["use"] = use != null && Uses.Any(u => u.Key == use) ? use : JValue.CreateNull(),
        };
        // 다시 하기 — 처음 합친 건물 uid 그대로(그 뒤 높이 고치기 기록이 같은 건물을 가리키게). 처음 합칠 때는 보내지 않는다(옛 엔진 호환).
        if (!string.IsNullOrEmpty(uid)) body["uid"] = uid;
        if (look != null) body.Merge(Map3DLook.PutBody(null, look), Map3DLook.KeepNulls);   // 다시 하기 — 처음 합친 건물의 외관 그대로(처음 합칠 때는 서버가 정함)
        return body;
    }

    /// <summary>합치기 응답 {"item":{…},"merged":[uid…],"holes_filled":n,"roof_forced_flat":bool} — item 이 틀리면 null.</summary>
    public static Map3DUserBuildingMergeResult? ParseMergeResult(string json)
    {
        var o = JObject.Parse(json);
        if (ParseItem(o["item"] as JObject) is not { } item) return null;
        var merged = o["merged"] is JArray m ? m.Where(t => t.Type == JTokenType.String).Select(t => t.Value<string>()!).ToList() : new List<string>();
        int holes = o["holes_filled"]?.Type == JTokenType.Integer && o.Value<long>("holes_filled") is long n && n is > 0 and < int.MaxValue ? (int)n : 0;
        return new Map3DUserBuildingMergeResult(item, merged, holes, o["roof_forced_flat"]?.Type == JTokenType.Boolean && o.Value<bool>("roof_forced_flat"));
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

    /// <summary>판의 여러 개 고름 한 줄 — 1개면 더 고르는 법, 2개 이상이면 합칠 수 있음.</summary>
    public static string SelectionText(int count)
        => count <= 0 ? "" : count == 1 ? "도형 1개 선택 — Ctrl+클릭으로 더 고르세요" : $"도형 {count}개 선택";

    /// <summary>합친 뒤 상태 글 — "N개를 하나로 합쳤습니다 · 지붕은 평지붕"(+ 채운 안쪽 빈 곳).</summary>
    public static string MergedText(int count, int holesFilled)
        => $"{count}개를 하나로 합쳤습니다 · 지붕은 평지붕" + (holesFilled > 0 ? $" · 안쪽 빈 곳 {holesFilled}개를 채움" : "");

    /// <summary>저장 뒤 상태 글.</summary>
    public static string SavedText(double height, int floors)
        => $"건물을 저장했습니다 — {Map3DBuilding.FormatMeters(height)} m({floors}층). 3D 를 다시 만들면 사진으로 꾸며집니다";
}



/// <summary>
/// 건물 윤곽 그리기 상태 기계 — ImageCropViewModel 의 편집 관용구(누르기 · 끌기 · 놓기 · 오른쪽 클릭 · 키 · 되돌리기)를 지도 위경도로.
/// 형상 도구: 다각형(선 클릭 · 처음 점 붙는 거리 안 = 닫기 · Shift 직각 맞춤) · 사각형 · 원 · 타원(끌기 또는 클릭 두세 번).
/// 형상은 화면 기준으로 만들고 꼭짓점마다 위경도로 바꾼다(회전된 지도에서도 화면에 보이는 모양 그대로).
/// 완성 뒤에는 모두 같은 편집 — 꼭짓점 끌기 · 선분 가운데 손잡이 점 넣기 · 지우기 · 몸통 끌어 옮기기 · 회전 손잡이.
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
    private UbPoint[]? _dragStart;               // 몸통 · 회전 끌기 시작 때 화면 꼭짓점
    private readonly List<UbPoint> _anchors = new();   // 형상 도구 클릭(화면)
    private List<UbPoint> _preview = new();       // 형상 도구 미리보기(위경도)
    private bool _shapeDragging;
    private int _circleSides = Map3DUserBuilding.DefaultCircleSides;

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
    public bool IsDragging => _dragging || _shapeDragging;
    public int DraggingVertex => _dragVertex;
    /// <summary>마지막으로 막은 이유(밖 · 엇갈림 · 3점 미만 · 너무 작음) — 다음 성공한 편집에서 지운다.</summary>
    public string? LastError { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    /// <summary>지금 커서(화면) — 열린 윤곽의 다음 선 미리보기.</summary>
    public UbPoint? Hover { get; private set; }
    /// <summary>지금 도구(빈 윤곽에서만 바뀐다).</summary>
    public UbTool Tool { get; private set; } = UbTool.Polygon;
    /// <summary>윤곽을 만든 형상 도구(그린 · 불러온 윤곽은 null) — 원이면 돔 지붕을 제안한다.</summary>
    public UbTool? CompletedBy { get; private set; }
    /// <summary>형상 도구 미리보기(위경도) — 끄는 중 · 클릭 사이.</summary>
    public IReadOnlyList<UbPoint> Preview => _preview;
    /// <summary>형상 도구로 찍은 점(화면).</summary>
    public IReadOnlyList<UbPoint> Anchors => _anchors;

    /// <summary>원 · 타원 근사 꼭짓점 수(8~128).</summary>
    public int CircleSides
    {
        get => _circleSides;
        set => _circleSides = Math.Clamp(value, Map3DUserBuilding.MinCircleSides, Map3DUserBuilding.MaxCircleSides);
    }

    /// <summary>형상 도구로 그리는 중인가(빈 윤곽 + 다각형 아닌 도구).</summary>
    private bool IsShapeTool => Tool != UbTool.Polygon && !_closed && _points.Count == 0;

    /// <summary>도구 바꾸기 — 빈 윤곽에서만(찍던 형상 점은 버린다). 바꿨으면 true.</summary>
    public bool SetTool(UbTool tool)
    {
        if (_points.Count > 0) return false;
        Tool = tool;
        _anchors.Clear();
        _preview.Clear();
        _shapeDragging = false;
        LastError = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>저장된 건물 고치기 — 닫힌 윤곽으로 연다(되돌리기 칸 비움).</summary>
    public void Load(IEnumerable<UbPoint> outline)
    {
        _points = outline.ToList();
        _closed = _points.Count >= Map3DUserBuilding.MinPoints;
        _undo.Clear();
        SelectedVertex = -1;
        CompletedBy = null;
        LastError = null;
        Changed?.Invoke();
    }

    /// <summary>지금 윤곽 검사(닫힘 포함) — 문제 없으면 null.</summary>
    public string? Validate() => !_closed ? Map3DUserBuilding.TooFewText : Map3DUserBuilding.Validate(_points, _inside);

    // ── 손잡이 ──

    public UbHandleHit HitTest(UbPoint screen)
    {
        for (int i = _points.Count - 1; i >= 0; i--)
            if (Map3DUserBuilding.Dist(_toScreen(_points[i]), screen) <= Map3DUserBuilding.HandleRadius) return new(UbHandleKind.Vertex, i);
        foreach (var (index, mid) in MidpointsScreen())
            if (Map3DUserBuilding.Dist(mid, screen) <= Map3DUserBuilding.HandleRadius) return new(UbHandleKind.Midpoint, index);
        if (RotateHandleScreen() is { } r && Map3DUserBuilding.Dist(r, screen) <= Map3DUserBuilding.HandleRadius) return new(UbHandleKind.Rotate, -1);
        if (_closed && Map3DUserBuilding.Contains(_points.Select(_toScreen).ToList(), screen)) return new(UbHandleKind.Body, -1);
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

    /// <summary>회전 손잡이(화면) — 닫힌 윤곽의 위쪽 끝(화면) 가운데에서 조금 위. 열린 윤곽이면 null.</summary>
    public UbPoint? RotateHandleScreen()
    {
        if (!_closed || _points.Count == 0) return null;
        var s = _points.Select(_toScreen).ToList();
        return new UbPoint(s.Average(p => p.X), s.Min(p => p.Y) - Map3DUserBuilding.RotateHandleOffsetPx);
    }

    // ── 누르기 · 끌기 · 놓기 ──

    public void PointerDown(UbPoint screen)
    {
        _press = screen;
        _pressHit = IsShapeTool ? new(UbHandleKind.None, -1) : HitTest(screen);
        _dragging = false;
        _dragVertex = -1;
        SelectedVertex = _pressHit.Kind == UbHandleKind.Vertex ? _pressHit.Index : -1;
        Changed?.Invoke();
    }

    /// <summary>움직임 — shift = 사각형 정사각형 · 타원 원.</summary>
    public void PointerMove(UbPoint screen, bool shift = false)
    {
        Hover = screen;
        if (IsShapeTool)
        {
            if (_press is { } p && _anchors.Count == 0 && (_shapeDragging || Map3DUserBuilding.Dist(p, screen) > Map3DUserBuilding.DragDeadZonePx))
            {
                _shapeDragging = true;
                _preview = ShapeFromDrag(p, screen, shift).Select(_toGeo).ToList();
                Changed?.Invoke();
            }
            else if (_anchors.Count > 0)
            {
                _preview = ShapeFromClicks(_anchors.Append(screen).ToList(), shift).Select(_toGeo).ToList();
                Changed?.Invoke();
            }
            return;
        }
        if (_press is not { } press)
        {
            if (!_closed && _points.Count > 0) Changed?.Invoke();   // 다음 선 미리보기
            return;
        }
        if (!_dragging)
        {
            if (Map3DUserBuilding.Dist(press, screen) <= Map3DUserBuilding.DragDeadZonePx) return;
            if (_pressHit.Kind == UbHandleKind.None) return;   // 빈 곳 끌기는 아무것도 안 한다
            _dragging = true;
            Snapshot();
            switch (_pressHit.Kind)
            {
                case UbHandleKind.Vertex: _dragVertex = _pressHit.Index; break;
                case UbHandleKind.Midpoint:
                    _dragVertex = _pressHit.Index + 1;
                    _points.Insert(_dragVertex, _toGeo(screen));
                    break;
                default: _dragStart = _points.Select(_toScreen).ToArray(); break;
            }
        }
        if (_dragVertex >= 0) _points[_dragVertex] = _toGeo(screen);
        else if (_dragStart is { } start) _points = Transform(start, press, screen, _pressHit.Kind).Select(_toGeo).ToList();
        Changed?.Invoke();
    }

    /// <summary>몸통 끌기 = 그대로 옮김, 회전 손잡이 = 가운데를 축으로 누른 자리 → 지금 자리 각만큼 돌림(화면 기준).</summary>
    private static IEnumerable<UbPoint> Transform(UbPoint[] start, UbPoint press, UbPoint now, UbHandleKind kind)
    {
        if (kind == UbHandleKind.Body) return start.Select(p => new UbPoint(p.X + now.X - press.X, p.Y + now.Y - press.Y));
        var c = new UbPoint(start.Average(p => p.X), start.Average(p => p.Y));
        double a = Math.Atan2(now.Y - c.Y, now.X - c.X) - Math.Atan2(press.Y - c.Y, press.X - c.X), ca = Math.Cos(a), sa = Math.Sin(a);
        return start.Select(p => new UbPoint(c.X + (p.X - c.X) * ca - (p.Y - c.Y) * sa, c.Y + (p.X - c.X) * sa + (p.Y - c.Y) * ca));
    }

    /// <summary>놓기 — 형상 도구면 끌기 완성 · 클릭 점 쌓기, 끌었으면 검사(엇갈림 · 밖이면 끌기 전으로), 아니면 클릭(첫 점 붙는 거리 = 닫기 · 빈 곳 = 점 더하기).</summary>
    public void PointerUp(UbPoint screen, bool shift = false)
    {
        if (_press is not { } press) return;
        _press = null;
        if (IsShapeTool)
        {
            if (_shapeDragging) { _shapeDragging = false; Finish(ShapeFromDrag(press, screen, shift)); return; }
            _anchors.Add(screen);
            if (_anchors.Count >= ClicksNeeded(Tool)) Finish(ShapeFromClicks(_anchors, shift));
            else Changed?.Invoke();
            return;
        }
        var hit = _pressHit;
        bool dragged = _dragging;
        _dragging = false;
        int moved = _dragVertex;
        _dragVertex = -1;
        _dragStart = null;
        if (dragged)
        {
            var check = moved >= 0 ? new[] { _points[moved] } : _points.ToArray();
            string? why = _inside != null && check.Any(p => !_inside(p)) ? Map3DUserBuilding.OutsideText
                        : Map3DUserBuilding.IsSelfIntersecting(_points, _closed) ? Map3DUserBuilding.CrossText : null;
            if (why != null) { Restore(_undo.Pop()); LastError = why; }
            else LastError = null;
            Changed?.Invoke();
            return;
        }
        if (_closed) { Changed?.Invoke(); return; }
        if (_points.Count >= Map3DUserBuilding.MinPoints
            && Map3DUserBuilding.Dist(_toScreen(_points[0]), screen) <= Map3DUserBuilding.CloseSnapPx) { Close(); return; }
        if (hit.Kind == UbHandleKind.Vertex) return;   // 다른 꼭짓점 클릭 = 고르기만(Delete 대상)
        var target = shift && _points.Count > 0
            ? Map3DUserBuilding.SnapAngle(_points.Count >= 2 ? _toScreen(_points[^2]) : null, _toScreen(_points[^1]), screen)
            : screen;
        AddPoint(_toGeo(target));
    }

    private static int ClicksNeeded(UbTool tool) => tool == UbTool.Ellipse ? 3 : 2;

    private IReadOnlyList<UbPoint> ShapeFromDrag(UbPoint a, UbPoint b, bool shift) => Tool switch
    {
        UbTool.Rect => Map3DUserBuilding.RectPoints(a, b, shift),
        UbTool.Triangle => Map3DUserBuilding.TrianglePoints(a, b, shift),
        UbTool.Circle => Map3DUserBuilding.CirclePoints(a, Map3DUserBuilding.Dist(a, b), _circleSides),
        _ => shift ? Map3DUserBuilding.CirclePoints(a, Map3DUserBuilding.Dist(a, b), _circleSides)
                   : Map3DUserBuilding.EllipsePoints(a, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y), 0, _circleSides),
    };

    /// <summary>클릭 점(마지막은 커서일 수 있다)으로 형상 — 사각형 · 삼각형 2점(대각) · 원 2점 · 타원 3점(2점이면 원으로 미리보기).</summary>
    private IReadOnlyList<UbPoint> ShapeFromClicks(IReadOnlyList<UbPoint> c, bool shift) => Tool switch
    {
        UbTool.Rect => Map3DUserBuilding.RectPoints(c[0], c[1], shift),
        UbTool.Triangle => Map3DUserBuilding.TrianglePoints(c[0], c[1], shift),
        UbTool.Circle => Map3DUserBuilding.CirclePoints(c[0], Map3DUserBuilding.Dist(c[0], c[1]), _circleSides),
        _ => c.Count >= 3 ? Map3DUserBuilding.EllipseFrom3(c[0], c[1], c[2], _circleSides)
                          : Map3DUserBuilding.CirclePoints(c[0], Map3DUserBuilding.Dist(c[0], c[1]), _circleSides),
    };

    /// <summary>형상 완성 — 검사(너무 작음 · 영역 밖) 뒤 닫힌 윤곽으로. 막히면 찍던 점을 버리고 이유를 남긴다.</summary>
    private void Finish(IReadOnlyList<UbPoint> screen)
    {
        // 화면 → 위경도가 1 px 로 반올림되어 이웃 꼭짓점이 같은 자리가 되면(작은 원 · 많은 꼭짓점) 하나로 — 엇갈림으로 막지 않게
        var geo = new List<UbPoint>();
        foreach (var p in screen.Select(_toGeo))
            if (geo.Count == 0 || geo[^1] != p) geo.Add(p);
        if (geo.Count > 1 && geo[0] == geo[^1]) geo.RemoveAt(geo.Count - 1);
        _anchors.Clear();
        _preview.Clear();
        if (Map3DUserBuilding.Validate(geo, _inside) is { } why) { LastError = why; Changed?.Invoke(); return; }
        Snapshot();
        _points = geo;
        _closed = true;
        CompletedBy = Tool;
        LastError = null;
        Changed?.Invoke();
    }

    /// <summary>캡처를 잃었거나 끄는 중 Esc — 끌기 전으로. 끄는 중이었으면 true.</summary>
    public bool PointerCancel()
    {
        bool was = _dragging, shape = _shapeDragging;
        _press = null;
        _dragging = false;
        _shapeDragging = false;
        _dragVertex = -1;
        _dragStart = null;
        if (shape) { _preview.Clear(); Changed?.Invoke(); return true; }
        if (!was) return false;
        Restore(_undo.Pop());
        return true;
    }

    /// <summary>두 번 누르기 = 닫기(다각형 — 첫 누르기가 이미 점을 놓았다).</summary>
    public bool DoubleClick() => Tool == UbTool.Polygon && Close();

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

    /// <summary>Backspace — 그리는 중이면 마지막 점(형상 도구면 마지막 클릭), 닫힌 윤곽이면 고른 점.</summary>
    public bool RemoveLast()
    {
        if (IsShapeTool && _anchors.Count > 0) { _anchors.RemoveAt(_anchors.Count - 1); _preview.Clear(); Changed?.Invoke(); return true; }
        return RemoveVertex(_closed ? SelectedVertex : _points.Count - 1);
    }

    /// <summary>Delete — 고른 점.</summary>
    public bool RemoveSelected() => RemoveVertex(SelectedVertex);

    /// <summary>Esc 두 단계 — 끄는 중이면 끌기 취소, 그리던(열린) 점 · 형상 클릭이 있으면 지움, 아니면 그리기 끝내기.</summary>
    public UbEscapeResult Escape()
    {
        if (PointerCancel()) return UbEscapeResult.CanceledDrag;
        if (_anchors.Count > 0)
        {
            _anchors.Clear();
            _preview.Clear();
            Changed?.Invoke();
            return UbEscapeResult.ClearedPoints;
        }
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
        if (!_closed) CompletedBy = null;
        SelectedVertex = -1;
        LastError = null;
        Changed?.Invoke();
    }
}