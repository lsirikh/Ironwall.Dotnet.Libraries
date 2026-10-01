using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 둥근 틀 — 테두리를 내용 '위'에 그리고, 내용을 안쪽 둥근 모양으로 자른다
   Created By   : Claude (frame-corners, 2026-10-01 "창 레이아웃의 모서리가 지워진 것 같다")
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="Border"/> 와 같은 자리 · 같은 속성으로 쓰되, <b>테두리를 자식보다 위에</b> 그리는 틀.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: <see cref="Border"/> 는 테두리를 자기 <c>OnRender</c> 에서 그리고 자식은 그 위에 그려진다. 그래서 둥근 틀 안의
/// 머리 띠 · 버튼 줄 · 콘솔 바탕이 테두리 안쪽 반 픽셀(앤티에일리어스)을 덮어 칠하고, 네모난 자식이면 호를 통째로 덮는다 —
/// 모서리 테두리가 지워진 것처럼 보였다(화면 밖 렌더 실측: 호 위 테두리색 도달률 다이얼로그 0.52~0.81 · 콘솔 표면 0.49~0.74).</para>
/// <para><b>어떻게</b>: ① 바탕은 이 요소가 그린다(테두리 선의 중심 경로까지 — 틈이 없다) ② 자식 ③ 테두리는 맨 위의 그림 층(<see cref="DrawingVisual"/>)에 그린다.
/// ④ <see cref="ClipChild"/> 면 자식을 테두리 안쪽선 반지름(r − 두께/2 − 안쪽 여백)으로 자른다 — 네모난 자식이 모서리 밖으로 비치지 않는다.
/// 배치(Measure/Arrange)는 <see cref="Border"/> 그대로라 자리는 1px 도 바뀌지 않는다.</para>
/// <para>두께 · 반지름이 고르지 않으면(예: <c>0,0,0,1</c> · <c>9,9,0,0</c>) <see cref="Border"/> 의 그리기를 그대로 쓴다 — 이 틀은 고른 둥근 틀 전용이다.</para>
/// <para>색은 <see cref="Border.BorderBrush"/> · <see cref="Border.Background"/> 를 그대로 쓴다 — <c>DynamicResource</c> 가 바뀌면 다시 그린다(캐싱하지 않는다).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public class FrameBorder : Border
{
    private readonly DrawingVisual _stroke = new();

    public FrameBorder()
    {
        AddVisualChild(_stroke);
    }

    public static readonly DependencyProperty ClipChildProperty = DependencyProperty.Register(
        nameof(ClipChild), typeof(bool), typeof(FrameBorder),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>자식을 테두리 안쪽 둥근 모양으로 자르는가(기본 참).</summary>
    public bool ClipChild { get => (bool)GetValue(ClipChildProperty); set => SetValue(ClipChildProperty, value); }

    #region - 시각 자식(테두리 층은 맨 끝 = 맨 위) -
    protected override int VisualChildrenCount => base.VisualChildrenCount + 1;

    protected override Visual GetVisualChild(int index)
    {
        var count = base.VisualChildrenCount;
        if (index < count) return base.GetVisualChild(index);
        if (index == count) return _stroke;
        throw new ArgumentOutOfRangeException(nameof(index));
    }
    #endregion

    /// <summary>고른 두께 · 고른 반지름인가 — 아니면 <see cref="Border"/> 의 그리기를 쓴다.</summary>
    private bool IsUniform(out double thickness, out double radius)
    {
        var t = BorderThickness;
        var c = CornerRadius;
        thickness = t.Left;
        radius = c.TopLeft;
        return t.Left == t.Top && t.Left == t.Right && t.Left == t.Bottom
            && c.TopLeft == c.TopRight && c.TopLeft == c.BottomLeft && c.TopLeft == c.BottomRight;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!IsUniform(out var thickness, out var radius))
        {
            ClearStroke();
            base.OnRender(dc);
            return;
        }

        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) { ClearStroke(); return; }

        // 바탕은 테두리 선의 중심 경로까지 — 테두리 바깥 반쪽 아래는 틀 바깥이 비친다(Border 와 같은 모양, 선이 바탕에 묻히지 않는다).
        var (path, pathRadius) = FrameBorderGeometry.StrokePath(size, thickness, radius);
        if (Background is { } background)
            dc.DrawRoundedRectangle(background, null, path, pathRadius, pathRadius);

        using var strokeDc = _stroke.RenderOpen();
        if (BorderBrush is { } brush && thickness > 0)
            strokeDc.DrawRoundedRectangle(null, new Pen(brush, thickness), path, pathRadius, pathRadius);
    }

    private void ClearStroke()
    {
        using var _ = _stroke.RenderOpen();
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var result = base.ArrangeOverride(arrangeSize);

        if (Child is { } child)
        {
            var clip = ClipChild && IsUniform(out var thickness, out var radius)
                ? FrameBorderGeometry.ChildClip(child.RenderSize, radius, thickness, Padding)
                : null;
            if (clip is null) child.ClearValue(ClipProperty);
            else child.Clip = clip;
        }
        return result;
    }
}

/// <summary><see cref="FrameBorder"/> 의 기하 — 순수 함수(창 없이 시험한다).</summary>
public static class FrameBorderGeometry
{
    /// <summary>바깥 반지름 — 짧은 변의 반을 넘지 않는다.</summary>
    public static double OuterRadius(Size size, double radius)
        => Math.Max(0, Math.Min(radius, Math.Min(size.Width, size.Height) / 2));

    /// <summary>
    /// 테두리 선의 중심 경로 — 두께의 반만큼 안쪽 사각형에 반지름 그대로(<see cref="Border"/> 의 고른 테두리와 같은 모양 —
    /// 바꿔 끼워도 호의 굵기 · 위치가 달라지지 않는다).
    /// </summary>
    public static (Rect Rect, double Radius) StrokePath(Size size, double thickness, double radius)
    {
        var half = thickness / 2;
        var rect = new Rect(half, half, Math.Max(0, size.Width - thickness), Math.Max(0, size.Height - thickness));
        return (rect, OuterRadius(size, radius));
    }

    /// <summary>
    /// 자식 자르기 — 자식 좌표(0,0 ~ 자식 크기)의 둥근 사각형, 반지름 = 테두리 안쪽선의 반지름(반지름 − 두께/2) − 안쪽 여백.
    /// 반지름이 0 이하거나 크기를 모르면 <c>null</c>(자르지 않는다).
    /// </summary>
    public static Geometry? ChildClip(Size childSize, double radius, double thickness, Thickness padding)
    {
        if (!double.IsFinite(childSize.Width) || !double.IsFinite(childSize.Height) || childSize.Width <= 0 || childSize.Height <= 0) return null;
        var inset = (thickness / 2) + Math.Min(Math.Min(padding.Left, padding.Top), Math.Min(padding.Right, padding.Bottom));
        var r = Math.Min(radius - inset, Math.Min(childSize.Width, childSize.Height) / 2);
        if (!double.IsFinite(r) || r <= 0) return null;

        var geometry = new RectangleGeometry(new Rect(childSize), r, r);
        geometry.Freeze();
        return geometry;
    }
}
