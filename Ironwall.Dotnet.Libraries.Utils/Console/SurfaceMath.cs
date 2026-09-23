using System;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 셸 표면(콘솔 창) 이동 · 크기 판정 — 화면 없이 잠글 수 있는 순수 함수만
   Created By   : Claude (N-14 셸 표면 · D-09/D-10 라이브러리 승격)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>표면 하나의 자리. 셸 <b>안쪽</b> 좌표다(데스크톱 좌표가 아니다).</summary>
public readonly record struct SurfaceBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>
    /// 기억해 둔 값으로 쓸 수 있는가. <b>NaN · 무한대 · 음수 크기를 여기서 끊는다</b> —
    /// 설정 파일은 사람이 열어 고칠 수 있고, 한 번 들어간 <c>NaN</c> 은 모든 비교를 조용히 통과한다.
    /// </summary>
    public bool IsUsable
        => double.IsFinite(X) && double.IsFinite(Y)
        && double.IsFinite(Width) && double.IsFinite(Height)
        && Width >= SurfaceMath.AbsoluteMinWidth
        && Height >= SurfaceMath.AbsoluteMinHeight;
}

/// <summary>
/// 표면이 놓일 수 있는 자리 — 패널 층의 크기에서 <b>금지 구역</b>을 뺀 것.
///
/// <para>금지 구역은 값으로 받는다(호출부가 셸의 실측 폭을 넣는다) — 이 구조체 자신은
/// 어떤 상수도 모른다. 호스트가 좌측 메뉴 · 이벤트 드로어의 <b>런타임 실폭</b>을 재서 넣을 수도,
/// 설계 상수를 넣을 수도 있다 — 이 타입은 그 선택에 관여하지 않는다.</para>
/// </summary>
public readonly record struct SurfaceArea(double Width, double Height, double LeftInset, double RightInset)
{
    public static SurfaceArea Of(double width, double height, double leftInset = 0, double rightInset = 0)
        => new(Math.Max(0, width), Math.Max(0, height), Math.Max(0, leftInset), Math.Max(0, rightInset));

    /// <summary>표면의 왼쪽 위 모서리가 들어갈 수 있는 가장 왼쪽.</summary>
    public double MinX => LeftInset;

    /// <summary>오른쪽 끝 — 머리의 최소 폭은 남겨 둔다.</summary>
    public double MaxX => Math.Max(LeftInset, Width - RightInset - SurfaceMath.MinVisibleHeaderWidth);

    public double MinY => 0;

    /// <summary>아래 끝 — 머리 한 줄은 남겨 둔다.</summary>
    public double MaxY => Math.Max(0, Height - SurfaceMath.MinVisibleHeaderHeight);

    /// <summary>표면이 커질 수 있는 최대 폭(금지 구역을 뺀 나머지).</summary>
    public double UsableWidth => Math.Max(SurfaceMath.AbsoluteMinWidth, Width - LeftInset - RightInset);

    public double UsableHeight => Math.Max(SurfaceMath.AbsoluteMinHeight, Height);

    public bool IsDegenerate => Width <= 0 || Height <= 0;
}

/// <summary>어느 모서리를 끄는가. 이동은 <see cref="None"/>.</summary>
[Flags]
public enum SurfaceEdge
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right,
}

/// <summary>
/// 표면 이동 · 크기 조절의 산술. <b>WPF 를 모른다</b> — 그래서 헤드리스로 잠근다
/// (드래그 제스처 자체는 UIA 로 단언할 수 없다는 규칙의 반대편: 판정은 전부 여기로 뺀다).
/// </summary>
public static class SurfaceMath
{
    /// <summary>머리가 이만큼은 늘 자리 안에 남는다(설계 L779 "헤더는 항상 최소 120×28").</summary>
    public const double MinVisibleHeaderWidth = 120;

    public const double MinVisibleHeaderHeight = 28;

    /// <summary>어떤 콘솔도 이보다 작아지지 않는다(설계 와이어프레임의 그립 최소 190 을 폭 하한으로 삼는다).</summary>
    public const double AbsoluteMinWidth = 190;

    public const double AbsoluteMinHeight = 120;

    /// <summary>화살표 한 번에 움직이는 거리. 키보드로도 드래그와 같은 일을 할 수 있어야 한다.</summary>
    public const double KeyboardStep = 1;

    /// <summary>수정자를 누른 화살표 한 번.</summary>
    public const double KeyboardCoarseStep = 10;

    #region - 이동 -
    /// <summary>끌어서 옮긴다 — 결과는 늘 자리 안으로 잘린다.</summary>
    public static SurfaceBounds Move(SurfaceBounds bounds, double deltaX, double deltaY, SurfaceArea area)
        => Clamp(bounds with { X = bounds.X + deltaX, Y = bounds.Y + deltaY }, area);

    /// <summary>
    /// 자리 안으로 민다. <b>크기는 건드리지 않는다</b> — 이동 중에 창이 줄어들면 손이 잡고 있는 지점이 달아난다.
    /// </summary>
    public static SurfaceBounds Clamp(SurfaceBounds bounds, SurfaceArea area)
    {
        if (!bounds.IsUsable || area.IsDegenerate) return bounds;

        var x = Math.Clamp(bounds.X, area.MinX, Math.Max(area.MinX, area.MaxX));
        var y = Math.Clamp(bounds.Y, area.MinY, Math.Max(area.MinY, area.MaxY));
        return bounds with { X = x, Y = y };
    }
    #endregion

    #region - 크기 조절 -
    /// <summary>
    /// 모서리를 끈다. 왼쪽 · 위쪽을 끌면 <b>반대쪽 모서리가 제자리에 있어야</b> 하므로 위치와 크기가 같이 바뀐다.
    /// </summary>
    /// <param name="minWidth">그 콘솔이 선언한 최소 폭(없으면 <see cref="AbsoluteMinWidth"/>).</param>
    public static SurfaceBounds Resize(SurfaceBounds bounds, SurfaceEdge edge,
                                       double deltaX, double deltaY, SurfaceArea area,
                                       double minWidth = AbsoluteMinWidth, double minHeight = AbsoluteMinHeight)
    {
        if (edge == SurfaceEdge.None || !bounds.IsUsable) return bounds;

        minWidth = Math.Max(AbsoluteMinWidth, double.IsFinite(minWidth) ? minWidth : AbsoluteMinWidth);
        minHeight = Math.Max(AbsoluteMinHeight, double.IsFinite(minHeight) ? minHeight : AbsoluteMinHeight);

        var left = bounds.X;
        var top = bounds.Y;
        var right = bounds.Right;
        var bottom = bounds.Bottom;

        if (edge.HasFlag(SurfaceEdge.Left)) left = Math.Min(bounds.X + deltaX, right - minWidth);
        if (edge.HasFlag(SurfaceEdge.Right)) right = Math.Max(bounds.Right + deltaX, left + minWidth);
        if (edge.HasFlag(SurfaceEdge.Top)) top = Math.Min(bounds.Y + deltaY, bottom - minHeight);
        if (edge.HasFlag(SurfaceEdge.Bottom)) bottom = Math.Max(bounds.Bottom + deltaY, top + minHeight);

        // 금지 구역을 넘어 자라지 않는다. 끌고 있는 모서리만 되민다 — 반대쪽은 제자리를 지킨다.
        if (!area.IsDegenerate)
        {
            if (edge.HasFlag(SurfaceEdge.Left)) left = Math.Max(left, area.MinX);
            if (edge.HasFlag(SurfaceEdge.Top)) top = Math.Max(top, 0);
            if (edge.HasFlag(SurfaceEdge.Right)) right = Math.Min(right, area.Width - area.RightInset);
            if (edge.HasFlag(SurfaceEdge.Bottom)) bottom = Math.Min(bottom, area.Height);

            // 되밀다가 최소 크기를 깨면 최소 크기가 이긴다(잘려 보이는 편이 사라지는 것보다 낫다).
            if (right - left < minWidth)
            {
                if (edge.HasFlag(SurfaceEdge.Left)) left = right - minWidth;
                else right = left + minWidth;
            }
            if (bottom - top < minHeight)
            {
                if (edge.HasFlag(SurfaceEdge.Top)) top = bottom - minHeight;
                else bottom = top + minHeight;
            }
        }

        return new SurfaceBounds(left, top, right - left, bottom - top);
    }
    #endregion

    #region - 기억해 둔 자리 되살리기 -
    /// <summary>머리가 자리 안에 남아 있는가 — 아니면 사람이 찾을 수 없다.</summary>
    public static bool IsReachable(SurfaceBounds bounds, SurfaceArea area)
    {
        if (!bounds.IsUsable || area.IsDegenerate) return false;

        var headRight = bounds.X + Math.Min(bounds.Width, MinVisibleHeaderWidth);
        var headBottom = bounds.Y + Math.Min(bounds.Height, MinVisibleHeaderHeight);

        return headRight > area.MinX
            && bounds.X < area.Width - area.RightInset
            && headBottom > 0
            && bounds.Y < area.Height;
    }

    /// <summary>
    /// 기억해 둔 자리를 지금 셸에 맞춘다.
    ///
    /// <para><b>왜 필요한가</b>: 표면 좌표는 셸 안쪽 기준이라 모니터가 빠지거나 DPI 가 바뀌거나 창을 줄이면
    /// <b>자리의 크기가 바뀐다</b>. 어제 1920 폭 셸에서 x=1500 에 둔 창은 오늘 1280 셸에서 화면 밖이다.
    /// 되살릴 수 없으면 기본 자리로 돌린다 — "안 보이는데 열려 있다"는 상태를 만들지 않는다.</para>
    /// </summary>
    public static SurfaceBounds Recover(SurfaceBounds remembered, SurfaceArea area, SurfaceBounds fallback)
    {
        if (area.IsDegenerate) return remembered.IsUsable ? remembered : fallback;
        if (!remembered.IsUsable) return Clamp(FitSize(fallback, area), area);

        var fitted = FitSize(remembered, area);
        var clamped = Clamp(fitted, area);

        return IsReachable(clamped, area) ? clamped : Clamp(FitSize(fallback, area), area);
    }

    /// <summary>자리보다 큰 표면은 자리에 맞춰 줄인다(최소 크기까지만).</summary>
    public static SurfaceBounds FitSize(SurfaceBounds bounds, SurfaceArea area)
    {
        if (area.IsDegenerate || !bounds.IsUsable) return bounds;

        var width = Math.Clamp(bounds.Width, AbsoluteMinWidth, Math.Max(AbsoluteMinWidth, area.UsableWidth));
        var height = Math.Clamp(bounds.Height, AbsoluteMinHeight, Math.Max(AbsoluteMinHeight, area.UsableHeight));
        return bounds with { Width = width, Height = height };
    }

    /// <summary>처음 열릴 때의 자리 — 쓸 수 있는 자리의 한가운데.</summary>
    public static SurfaceBounds Center(double width, double height, SurfaceArea area)
    {
        var fitted = FitSize(new SurfaceBounds(0, 0, width, height), area);
        var x = area.MinX + (Math.Max(0, area.UsableWidth - fitted.Width) / 2);
        var y = Math.Max(0, area.Height - fitted.Height) / 2;
        return Clamp(fitted with { X = x, Y = y }, area);
    }
    #endregion

    #region - 키보드 -
    /// <summary>화살표로 옮긴다 — 드래그와 <b>같은 일</b>을 한다(접근성 · 자동화가 좌표 클릭에 묶이지 않게).</summary>
    public static SurfaceBounds KeyboardMove(SurfaceBounds bounds, int dirX, int dirY, bool coarse, SurfaceArea area)
    {
        var step = coarse ? KeyboardCoarseStep : KeyboardStep;
        return Move(bounds, dirX * step, dirY * step, area);
    }

    /// <summary>화살표로 크기를 바꾼다 — 오른쪽 · 아래 모서리를 끈 것과 같다.</summary>
    public static SurfaceBounds KeyboardResize(SurfaceBounds bounds, int dirX, int dirY, bool coarse,
                                               SurfaceArea area, double minWidth = AbsoluteMinWidth,
                                               double minHeight = AbsoluteMinHeight)
    {
        var step = coarse ? KeyboardCoarseStep : KeyboardStep;
        return Resize(bounds, SurfaceEdge.BottomRight, dirX * step, dirY * step, area, minWidth, minHeight);
    }
    #endregion
}
