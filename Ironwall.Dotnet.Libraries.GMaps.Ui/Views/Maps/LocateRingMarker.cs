using GMap.NET;
using GMap.NET.WindowsPresentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

/****************************************************************************
   Purpose      : [지도에서 보기] 강조 고리 — 심볼 위치에 그리는 비상호작용 마커 (unit-relationship-map FR-45)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 강조 고리 — <b>형태</b>로 구분한다(색이 아니라): 2.5 실선 고리 + 1px 점선 바깥 고리, 그 밑에 대비용 바탕 고리.
/// </summary>
/// <remarks>
/// <para><b>입력을 받지 않는다</b>(<see cref="UIElement.IsHitTestVisible"/> = false) — 밑의 심볼 클릭 · 우클릭 · 지도 팬을 가리지 않는다.
/// 새 드래그를 붙이지 않고 <c>AdornerManagerService</c> 도 쓰지 않는다(DF — 지도 위 새 드래그 금지 · 매니저 5분 타이머 전례).</para>
/// <para><b>편집 마커가 아니다</b> — <see cref="GMapMarker"/> 를 직접 파생하고 <c>IEditableMarker</c> 를 구현하지 않는다.
/// 지도의 마커 순회는 <c>Markers.OfType&lt;IEditableMarker&gt;()</c> 라 이 고리를 건드리지 않는다
/// (메모리 <c>symbolprovider_stalecache_markers_hardcast</c> — 추적 마커와 같은 부류).</para>
/// <para><b>색은 토큰을 매번 다시 읽는다</b>(<c>SetResourceReference</c>) — 테마를 바꾸면 따라 바뀐다. Frozen 브러시 없음.
/// 점선은 <c>{2,2}</c> — 그룹 선택 박스 <c>{4,3}</c> · 드로잉 미리보기 <c>{5,3}</c> 어휘와 겹치지 않는다.</para>
/// <para>생성 · 제거는 UI 스레드에서만.</para>
/// </remarks>
public sealed class LocateRingMarker : GMapMarker
{
    /// <summary>실선 고리 지름(DIU).</summary>
    public const double RingDiameter = 44;

    /// <summary>점선 바깥 고리 지름(DIU).</summary>
    public const double OuterDiameter = 58;

    /// <summary>다른 마커보다 위(고리가 심볼에 가려지지 않게). 입력을 받지 않으므로 위에 있어도 클릭을 막지 않는다.</summary>
    public const int RingZIndex = int.MaxValue - 16;

    /// <param name="deviceId">강조한 장비 서버 id(진단 · 시험).</param>
    /// <param name="position">심볼 위치.</param>
    public LocateRingMarker(int deviceId, PointLatLng position) : base(position)
    {
        DeviceId = deviceId;

        var canvas = new Canvas
        {
            Width = OuterDiameter,
            Height = OuterDiameter,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        // 대비용 바탕 — 위성 · 밝은 타일 어디서나 실선 고리가 묻히지 않게.
        var halo = Ring(RingDiameter, 5.0);
        halo.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "SurfaceBrush");
        halo.Opacity = 0.85;

        var ring = Ring(RingDiameter, 2.5);
        ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryBrush");

        var outer = Ring(OuterDiameter - 1, 1.0);
        outer.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryBrush");
        outer.StrokeDashArray = new DoubleCollection { 2, 2 };

        canvas.Children.Add(halo);
        canvas.Children.Add(ring);
        canvas.Children.Add(outer);

        Shape = canvas;
        Offset = new Point(-OuterDiameter / 2, -OuterDiameter / 2);   // 가운데가 심볼 좌표
        ZIndex = RingZIndex;
    }

    /// <summary>강조한 장비 서버 id.</summary>
    public int DeviceId { get; }

    private static Ellipse Ring(double diameter, double thickness)
    {
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            StrokeThickness = thickness,
            Fill = null,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(ellipse, (OuterDiameter - diameter) / 2);
        Canvas.SetTop(ellipse, (OuterDiameter - diameter) / 2);
        return ellipse;
    }
}
