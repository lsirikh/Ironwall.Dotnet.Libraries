using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;

/****************************************************************************
   Purpose      : 관계도 뷰포트 — 배율 · 오프셋 · 커서 기준 줌 · 전체 보기 · 팬 (unit-relationship-map IMPL-01)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 관계도(그래프 캔버스)의 뷰포트 — <c>화면 = 월드 × <see cref="Scale"/> + <see cref="Offset"/></c>.
/// 불변 값 형식이라 조작마다 새 값을 돌려준다.
/// </summary>
/// <remarks>
/// <para><b>UI 형식을 참조하지 않는다</b>(<see cref="Point"/> · <see cref="Vector"/> · <see cref="Rect"/> · <see cref="Size"/> 는 값 형식일 뿐이다) —
/// 부대 관계도가 처음 쓰지만 서버 · 결선 관계도도 그대로 쓴다(PRD D-4 · FR-04).</para>
/// <para>배율 한계 <see cref="MinScale"/> 0.10 ~ <see cref="MaxScale"/> 1.60, 휠 한 칸 ×<see cref="WheelStep"/>(FR-12).
/// 연속 휠의 합침은 <see cref="GraphWheelAccumulator"/> 가 한다.</para>
/// </remarks>
public readonly record struct GraphViewport(double Scale, Vector Offset)
{
    /// <summary>최소 배율(PRD §3.3). 전체 보기도 여기서 멈추고 나머지는 팬으로 본다.</summary>
    public const double MinScale = 0.10;

    /// <summary>최대 배율(PRD §3.3).</summary>
    public const double MaxScale = 1.60;

    /// <summary>휠 한 칸의 배율 곱(위 = ×1.2, 아래 = ÷1.2).</summary>
    public const double WheelStep = 1.2;

    /// <summary>전체 보기 여백(DIU, FR-15).</summary>
    public const double FitPadding = 16.0;

    /// <summary><c>Ctrl</c>+화살표 한 번에 옮기는 뷰포트의 몫(FR-14).</summary>
    public const double PanFraction = 1.0 / 8.0;

    /// <summary>배율 1 · 오프셋 0.</summary>
    public static GraphViewport Identity => new(1.0, new Vector(0, 0));

    /// <summary>배율을 [<see cref="MinScale"/>, <see cref="MaxScale"/>] 로 자른다. 수가 아니면(<c>NaN</c>) 1 로 되돌린다.</summary>
    public static double ClampScale(double scale)
        => double.IsNaN(scale) ? 1.0 : Math.Clamp(scale, MinScale, MaxScale);

    /// <summary>월드 점 → 화면 점.</summary>
    public Point WorldToScreen(Point world)
        => new(world.X * Scale + Offset.X, world.Y * Scale + Offset.Y);

    /// <summary>화면 점 → 월드 점.</summary>
    public Point ScreenToWorld(Point screen)
        => new((screen.X - Offset.X) / Scale, (screen.Y - Offset.Y) / Scale);

    /// <summary>
    /// <paramref name="cursor"/>(화면) 아래 월드 점을 고정한 채 배율에 <paramref name="factor"/> 를 곱한다 —
    /// <c>o' = c − (c − o)·s'/s</c>. 한계에서 잘리면 잘린 배율로 같은 식을 적용한다(커서 점은 여전히 고정).
    /// </summary>
    public GraphViewport ZoomAt(Point cursor, double factor)
        => ZoomTo(cursor, Scale * factor);

    /// <summary><paramref name="cursor"/> 아래 월드 점을 고정한 채 배율을 <paramref name="scale"/> 로(한계 안으로 잘라) 바꾼다.</summary>
    public GraphViewport ZoomTo(Point cursor, double scale)
    {
        var next = ClampScale(scale);
        if (next == Scale) return this;

        var ratio = next / Scale;
        return new GraphViewport(next, new Vector(
            cursor.X - (cursor.X - Offset.X) * ratio,
            cursor.Y - (cursor.Y - Offset.Y) * ratio));
    }

    /// <summary>화면 단위로 그림을 (<paramref name="dx"/>, <paramref name="dy"/>) 만큼 옮긴다(끌기 팬).</summary>
    public GraphViewport Pan(double dx, double dy)
        => this with { Offset = new Vector(Offset.X + dx, Offset.Y + dy) };

    /// <summary>
    /// <c>Ctrl</c>+화살표 팬 — <b>뷰가</b> 화살표 쪽으로 뷰포트의 1/8 만큼 간다(그림은 반대로 움직인다).
    /// <paramref name="directionX"/> · <paramref name="directionY"/> 는 부호만 본다(−1 · 0 · +1).
    /// </summary>
    public GraphViewport PanStep(Size viewport, int directionX, int directionY)
        => Pan(-Math.Sign(directionX) * viewport.Width * PanFraction,
               -Math.Sign(directionY) * viewport.Height * PanFraction);

    /// <summary>배율은 그대로 두고 <paramref name="world"/> 를 뷰포트 가운데에 둔다(검색 이동 · 선택이 화면 밖일 때).</summary>
    public GraphViewport CenterOn(Point world, Size viewport)
        => this with
        {
            Offset = new Vector(viewport.Width / 2 - world.X * Scale, viewport.Height / 2 - world.Y * Scale),
        };

    /// <summary>
    /// 전체 보기 — <paramref name="worldBounds"/> 가 여백 <paramref name="padding"/> 안에 들도록 배율을 고르고 가운데에 둔다.
    /// 들지 않으면 <see cref="MinScale"/> 에서 멈추고 가운데만 맞춘다(PRD §3.3).
    /// </summary>
    /// <param name="worldBounds">노드 <b>중심점</b>들의 월드 경계.</param>
    /// <param name="viewport">캔버스 크기(DIU).</param>
    /// <param name="padding">여백(DIU).</param>
    /// <param name="nodeBox">
    /// 노드 도형의 <b>화면</b> 크기 — 노드 중심 기준 사각형(예: L2 카드 <c>(-66, -28, 132, 56)</c>).
    /// 의미 줌은 도형을 배율로 키우지 않으므로(FR-21) 월드 경계만 맞추면 가장자리 도형이 잘린다 — 그 몫을 여기서 뺀다.
    /// 기본값(크기 0)이면 점만 맞춘다.
    /// </param>
    /// <returns>경계나 뷰포트가 비었으면 <see cref="Identity"/> — 배율을 지키려면 <see cref="TryFit"/> 를 쓴다.</returns>
    public static GraphViewport Fit(Rect worldBounds, Size viewport, double padding = FitPadding, Rect nodeBox = default)
        => TryFit(worldBounds, viewport, out var fitted, padding, nodeBox) ? fitted : Identity;

    /// <summary>
    /// <see cref="Fit"/> 과 같되, 맞출 것이 없으면(부대 0 · 뷰포트 0) <c>false</c> 를 돌려 호출부가 지금 배율을 지키게 한다
    /// (0 나눗셈 없음 — 시나리오 ISSUE-39).
    /// </summary>
    public static bool TryFit(Rect worldBounds, Size viewport, out GraphViewport fitted, double padding = FitPadding, Rect nodeBox = default)
    {
        fitted = Identity;
        if (worldBounds.IsEmpty || viewport.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0) return false;
        if (!IsFinite(worldBounds) || !IsFinite(nodeBox)) return false;

        var usableWidth = Math.Max(1, viewport.Width - 2 * padding - nodeBox.Width);
        var usableHeight = Math.Max(1, viewport.Height - 2 * padding - nodeBox.Height);
        var scaleX = worldBounds.Width > 0 ? usableWidth / worldBounds.Width : double.PositiveInfinity;
        var scaleY = worldBounds.Height > 0 ? usableHeight / worldBounds.Height : double.PositiveInfinity;
        var scale = ClampScale(Math.Min(scaleX, scaleY));

        // 화면에 그려질 내용(점 경계 × 배율 + 도형)의 가운데를 뷰포트 가운데에 둔다.
        var contentCenterX = (worldBounds.Left + worldBounds.Right) / 2 * scale + (nodeBox.Left + nodeBox.Right) / 2;
        var contentCenterY = (worldBounds.Top + worldBounds.Bottom) / 2 * scale + (nodeBox.Top + nodeBox.Bottom) / 2;
        fitted = new GraphViewport(scale, new Vector(viewport.Width / 2 - contentCenterX, viewport.Height / 2 - contentCenterY));
        return true;
    }

    /// <summary>팬 한계에서 늘 보이는 그림의 몫(조정자 결정 D-2026-09-27-6615ba · 시나리오 ISSUE-40).</summary>
    public const double MinVisibleFraction = 0.2;

    /// <summary>
    /// 팬 한계 — 그림(<paramref name="worldBounds"/> × 배율)이 축마다 <b>최소 <paramref name="minVisibleFraction"/></b> 만큼 뷰포트 안에 남도록
    /// 오프셋을 자른다. 그림이 뷰포트보다 크면 뷰포트 폭의 그 몫이 기준이다(그림 전체를 끝없이 화면 밖으로 밀지 못하게 — SIM-V079 · V080).
    /// 배율은 바꾸지 않는다. 경계가 비었으면 그대로.
    /// </summary>
    /// <remarks>경계 폭이 0(부대 1개 · 한 줄)이면 그 점이 뷰포트 안에 남는다.</remarks>
    public GraphViewport ClampPan(Rect worldBounds, Size viewport, double minVisibleFraction = MinVisibleFraction)
    {
        if (worldBounds.IsEmpty || !IsFinite(worldBounds) || viewport.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0) return this;

        var fraction = Math.Clamp(double.IsFinite(minVisibleFraction) ? minVisibleFraction : MinVisibleFraction, 0, 1);
        var x = ClampAxis(worldBounds.Left * Scale + Offset.X, worldBounds.Width * Scale, viewport.Width, fraction);
        var y = ClampAxis(worldBounds.Top * Scale + Offset.Y, worldBounds.Height * Scale, viewport.Height, fraction);
        return new GraphViewport(Scale, new Vector(Offset.X + x, Offset.Y + y));

        // 그림 왼쪽 끝 left 가 [need − extent, viewportLength − need] 안에 들도록 옮길 양.
        static double ClampAxis(double left, double extent, double viewportLength, double fraction)
        {
            var need = fraction * Math.Min(extent, viewportLength);
            var min = need - extent;
            var max = viewportLength - need;
            return left < min ? min - left : left > max ? max - left : 0;
        }
    }

    /// <summary>
    /// 전체 보기가 최소 배율(<see cref="MinScale"/>)에서도 뷰포트에 들지 않는가 — "전체가 한 화면에 들지 않습니다" 안내의 판정(FR-15 · ISSUE-39).
    /// 뷰포트 크기가 0 이거나 경계가 비었으면 <c>false</c>(맞출 것이 없다).
    /// </summary>
    public static bool Overflows(Rect worldBounds, Size viewport, double padding = FitPadding, Rect nodeBox = default)
    {
        if (worldBounds.IsEmpty || !IsFinite(worldBounds) || viewport.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0) return false;
        var width = worldBounds.Width * MinScale + nodeBox.Width;
        var height = worldBounds.Height * MinScale + nodeBox.Height;
        return width > viewport.Width - 2 * padding + 1e-9 || height > viewport.Height - 2 * padding + 1e-9;
    }

    /// <summary>
    /// 캔버스 크기가 <paramref name="oldViewport"/> 에서 <paramref name="newViewport"/> 로 바뀌었다 — 화면 가운데의 월드 점을 유지한다(FR-12 · ISSUE-54).
    /// 어느 쪽이든 폭 · 높이가 0 이면 그대로(크기가 생긴 뒤 첫 화면 규칙을 다시 적용하는 것은 호출부 몫).
    /// </summary>
    public GraphViewport Resize(Size oldViewport, Size newViewport)
    {
        if (oldViewport.IsEmpty || newViewport.IsEmpty || oldViewport.Width <= 0 || oldViewport.Height <= 0
            || newViewport.Width <= 0 || newViewport.Height <= 0) return this;
        var center = ScreenToWorld(new Point(oldViewport.Width / 2, oldViewport.Height / 2));
        return CenterOn(center, newViewport);
    }

    /// <summary>공유 배치 Δ 의 서버 한계(S-1 ⑤ · ISSUE-40).</summary>
    public const double MaxDelta = 1_000_000;

    /// <summary>Δ(월드 단위)를 서버 한계 ±<see cref="MaxDelta"/> 로 자른다. 수가 아니면 0.</summary>
    public static Vector ClampDelta(Vector delta, double limit = MaxDelta)
        => new(Math.Clamp(double.IsFinite(delta.X) ? delta.X : 0, -limit, limit),
               Math.Clamp(double.IsFinite(delta.Y) ? delta.Y : 0, -limit, limit));

    private static bool IsFinite(Rect r)
        => double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height);
}
