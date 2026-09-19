using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Blocks;

/// <summary>
/// 가족 + 상자 크기 → 블록 <b>바깥선</b>(FR-04). 시각트리가 없어도 돈다 — 헤드리스로 단언할 수 있는 순수 계산이다.
/// </summary>
/// <remarks>
/// <para><b>색이 아니라 형태로</b> 가른다. 라이트에서 <c>PrimaryBrush</c> = <c>SelectionBrush</c> = <c>FocusRingBrush</c> 가
/// 전부 같은 색(대비 1.00:1)이라 색으로 가족을 가르면 <b>보이지 않는다</b>(드래그 규칙 §시각 피드백).</para>
/// <list type="bullet">
///   <item><b>감지</b> — 둥근 칩 + 왼쪽 위 <b>홈</b>(모서리를 네모로 베어 낸다).</item>
///   <item><b>구동</b> — 각진 네모 + 오른쪽 아래 <b>삼각</b> 컷.</item>
///   <item><b>전원 · 환경</b> — 납작한 <b>육각</b>.</item>
///   <item><b>네트워크</b> — <b>마름모</b>(보드에서 늘 맨 끝).</item>
///   <item><b>광학 · 기타</b> — 둥근 칩(광학은 <see cref="OpticsMark"/> 의 작은 <b>원</b>이 더 붙는다).</item>
/// </list>
/// <para><b>캐시</b>는 얼린(<c>Freeze</c>) <see cref="Geometry"/> 만 한다 — 얼린 Freezable 은 스레드를 타지 않는다.
/// <b>브러시는 절대 캐싱하지 않는다</b>(테마 전환 때 옛 색으로 고착된 선례가 있다 — <c>LineDrawingAdorner</c>).</para>
/// </remarks>
public static class ComponentBlockGeometry
{
    /// <summary>둥근 칩의 모서리 반지름(상한). 실제 값은 상자가 작으면 함께 줄어든다.</summary>
    public const double CORNER_RADIUS = 6.0;

    /// <summary>감지 블록 왼쪽 위 홈의 한 변(상한).</summary>
    public const double NOTCH = 9.0;

    /// <summary>구동 블록 오른쪽 아래 삼각 컷의 한 변(상한).</summary>
    public const double CUT = 12.0;

    /// <summary>
    /// <paramref name="width"/> × <paramref name="height"/> 상자 안에 꽉 차는 <b>닫힌</b> 바깥선.
    /// 경계 상자는 언제나 <c>(0,0,width,height)</c> 다 — 어느 가족이든 네 변에 닿는다.
    /// </summary>
    public static Geometry Outline(ComponentFamily family, double width, double height)
    {
        if (!IsUsable(width) || !IsUsable(height)) return Geometry.Empty;

        var key = new CacheKey(family, Quantize(width), Quantize(height));
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var geometry = Build(family, width, height);
        geometry.Freeze();                                  // 얼려야 다른 스레드(그리고 두 요소)가 같이 쓸 수 있다

        if (_cache.Count < CACHE_CAP) _cache.TryAdd(key, geometry);
        return geometry;
    }

    /// <summary>
    /// 광학 블록의 <b>원 표지</b> 중심 · 반지름. 광학이 아니면 <c>null</c> 이다.
    /// 그리는 쪽이 원을 직접 만들 수 있게 좌표만 준다(어느 어도너에도 묶이지 않는다).
    /// </summary>
    public static (Point Center, double Radius)? OpticsMark(ComponentFamily family, double width, double height)
    {
        if (family != ComponentFamily.Optics) return null;
        if (!IsUsable(width) || !IsUsable(height)) return null;

        var radius = Math.Min(4.5, Math.Min(width, height) / 6.0);
        if (radius < 0.5) return null;                       // 너무 작으면 표지를 그리지 않는다
        var margin = Math.Min(6.0, width / 6.0);
        var cx = Math.Max(radius, width - radius - margin);
        var cy = height / 2.0;
        return (new Point(cx, cy), radius);
    }

    #region - Processes -
    private static Geometry Build(ComponentFamily family, double w, double h) => family switch
    {
        ComponentFamily.Sensing => NotchedChip(w, h),
        ComponentFamily.Actuation => CutSquare(w, h),
        ComponentFamily.PowerEnvironment => Hexagon(w, h),
        ComponentFamily.Network => Diamond(w, h),
        _ => RoundedChip(w, h),                              // 광학 · 기타
    };

    /// <summary>둥근 칩 — 광학 · 기타의 바탕이자 감지 칩의 뼈대.</summary>
    private static Geometry RoundedChip(double w, double h)
    {
        var r = Radius(w, h);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(r, 0), isFilled: true, isClosed: true);
            TopRight(ctx, w, r);
            BottomRight(ctx, w, h, r);
            BottomLeft(ctx, h, r);
            ctx.LineTo(new Point(0, r), true, false);
            Arc(ctx, new Point(r, 0), r);
        }
        return geometry;
    }

    /// <summary>감지 — 둥근 칩에서 <b>왼쪽 위 모서리를 네모로 베어 낸다</b>(홈). 왼쪽 변과 윗변에는 그대로 닿는다.</summary>
    private static Geometry NotchedChip(double w, double h)
    {
        var r = Radius(w, h);
        var n = Math.Min(NOTCH, Math.Min(w, h) / 3.0);     // 상자보다 큰 홈이 생기지 않게 상한을 묶는다

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(n, 0), isFilled: true, isClosed: true);
            TopRight(ctx, w, r);
            BottomRight(ctx, w, h, r);
            BottomLeft(ctx, h, r);
            ctx.LineTo(new Point(0, n), true, false);        // 왼쪽 변 — 홈 아래까지만 올라온다
            ctx.LineTo(new Point(n, n), true, false);        // 홈 안쪽 가로
            // 마지막 세로(n,n)→(n,0) 는 닫히면서 그어진다
        }
        return geometry;
    }

    /// <summary>구동 — 각진 네모에서 <b>오른쪽 아래를 삼각으로 베어 낸다</b>.</summary>
    private static Geometry CutSquare(double w, double h)
    {
        var t = Math.Min(CUT, Math.Min(w, h) / 3.0);       // 상자보다 큰 컷이 생기지 않게 상한을 묶는다

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(w, 0), true, false);
            ctx.LineTo(new Point(w, h - t), true, false);
            ctx.LineTo(new Point(w - t, h), true, false);    // 삼각 컷
            ctx.LineTo(new Point(0, h), true, false);
        }
        return geometry;
    }

    /// <summary>전원 · 환경 — 납작한 육각(좌우 끝이 뾰족하다).</summary>
    private static Geometry Hexagon(double w, double h)
    {
        var inset = Math.Min(w * 0.18, h / 2.0);
        var mid = h / 2.0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, mid), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(inset, 0), true, false);
            ctx.LineTo(new Point(w - inset, 0), true, false);
            ctx.LineTo(new Point(w, mid), true, false);
            ctx.LineTo(new Point(w - inset, h), true, false);
            ctx.LineTo(new Point(inset, h), true, false);
        }
        return geometry;
    }

    /// <summary>네트워크 — 마름모. 보드에서 늘 맨 끝이라 멀리서도 줄 끝이 보인다.</summary>
    private static Geometry Diamond(double w, double h)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(w / 2.0, 0), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(w, h / 2.0), true, false);
            ctx.LineTo(new Point(w / 2.0, h), true, false);
            ctx.LineTo(new Point(0, h / 2.0), true, false);
        }
        return geometry;
    }

    private static void TopRight(StreamGeometryContext ctx, double w, double r)
    {
        ctx.LineTo(new Point(w - r, 0), true, false);
        Arc(ctx, new Point(w, r), r);
    }

    private static void BottomRight(StreamGeometryContext ctx, double w, double h, double r)
    {
        ctx.LineTo(new Point(w, h - r), true, false);
        Arc(ctx, new Point(w - r, h), r);
    }

    private static void BottomLeft(StreamGeometryContext ctx, double h, double r)
    {
        ctx.LineTo(new Point(r, h), true, false);
        Arc(ctx, new Point(0, h - r), r);
    }

    private static void Arc(StreamGeometryContext ctx, Point to, double r)
        => ctx.ArcTo(to, new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);

    // 반지름은 늘 짧은 변의 1/4 이하다 — 더 키우면 모서리 호가 서로를 넘어 경계 상자 밖으로 부푼다.
    private static double Radius(double w, double h)
        => Math.Min(CORNER_RADIUS, Math.Min(w, h) / 4.0);

    private static bool IsUsable(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.5;

    /// <summary>캐시 키를 0.5 DIU 격자에 맞춘다 — 리사이즈 한 픽셀마다 새 도형을 굳히지 않게.</summary>
    private static double Quantize(double value) => Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;

    private readonly record struct CacheKey(ComponentFamily Family, double Width, double Height);

    private const int CACHE_CAP = 64;
    private static readonly ConcurrentDictionary<CacheKey, Geometry> _cache = new();
    #endregion
}
