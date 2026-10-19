using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

/****************************************************************************
   Purpose      : 2D 지도의 3D 영역 표시(지도 3D 시나리오 S2 · S3, R4) — 구운 영역 테두리 + 왼쪽 위 모서리 "3D" 배지.
   Note         : ★ 편집 마커가 아니다 — GMapMarker 를 직접 파생하고 IEditableMarker 를 구현하지 않는다(DB 저장 · 선택 · 러버밴드 · 3D 오버레이 다리 대상 아님,
                    LocateRingMarker 와 같은 부류). 새 드래그를 붙이지 않는다.
                  ★ 테두리는 입력을 받지 않는다(밑의 심볼 · 지도 팬을 가리지 않게). 배지만 툴팁 · 우클릭 메뉴를 받는다 —
                    SetEditMode 가 모든 마커 Shape 의 IsHitTestVisible 을 덮어쓰므로 Shape 캔버스는 늘 true 로 강제한다.
                  ★ 색은 토큰을 매번 다시 읽는다(SetResourceReference) — 테마 전환을 따라간다.
                  ★ 크기는 줌이 바뀔 때 Refresh 로 다시 잰다(위치는 왼쪽 위 = 북서 모서리라 팬은 GMap 이 옮긴다).
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class Map3DAreaMarker : GMapMarker
{
    /// <summary>강조 고리(LocateRingMarker) 바로 밑 — 배지가 심볼에 가려지지 않게. 테두리는 입력을 받지 않으므로 위에 있어도 클릭을 막지 않는다.</summary>
    public const int AreaZIndex = int.MaxValue - 17;

    private readonly Polygon _outline;
    private readonly Border _badge;

    /// <summary>위치 = 영역 왼쪽 위 모서리(옛 영역 = bbox 북서, 회전 영역 = 사각형 로컬 왼쪽 위).</summary>
    public Map3DAreaMarker(Map3DAreaItem item) : base(TopLeftOf(item))
    {
        Item = item;

        // 테두리 = 네 모서리 다각형(2026-10-19) — 회전 영역은 기울어진 네모, 지도가 돌아가 있어도 모서리마다 화면에 투영해 맞는다
        _outline = new Polygon { StrokeThickness = 1.5, Fill = null, IsHitTestVisible = false };
        _outline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "PrimaryBrush");

        var text = new TextBlock { Text = "3D", FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 1, 6, 2) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "SurfaceBrush");
        AutomationProperties.SetAutomationId(text, $"GMaps.Map3D.AreaBadge.{item.Id}");   // peer 가 실재하는 TextBlock 에
        AutomationProperties.SetName(text, $"3D 영역 {item.Title}");

        _badge = new Border
        {
            Child = text,
            CornerRadius = new CornerRadius(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            ToolTip = item.ToolTip,
        };
        _badge.SetResourceReference(Border.BackgroundProperty, "PrimaryBrush");
        _badge.MouseRightButtonDown += OnBadgeRightButtonDown;

        var canvas = new AlwaysHitCanvas { SnapsToDevicePixels = true };
        canvas.Children.Add(_outline);
        canvas.Children.Add(_badge);
        Shape = canvas;
        ZIndex = AreaZIndex;
    }

    public Map3DAreaItem Item { get; }

    /// <summary>줌 · 표시 여부 반영 — 보일 때만 네 모서리를 화면에 투영해(마커 자리 기준) 테두리를 맞춘다.</summary>
    public void Refresh(GMapControl map, bool visible)
    {
        if (Shape is not UIElement shape) return;
        shape.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible) return;
        var origin = map.FromLatLngToLocal(Position);
        var points = new PointCollection();
        foreach (var (lat, lon) in Item.Area.Corners)
        {
            var p = map.FromLatLngToLocal(new PointLatLng(lat, lon));
            points.Add(new Point(p.X - origin.X, p.Y - origin.Y));
        }
        _outline.Points = points;
    }

    private static PointLatLng TopLeftOf(Map3DAreaItem item)
        => item.Area.Corners is { Count: 4 } c ? new PointLatLng(c[0].Lat, c[0].Lon) : new PointLatLng(item.Bbox[3], item.Bbox[0]);

    private void OnBadgeRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // 지도 우클릭(마커 · 이미지 메뉴)으로 올라가지 않게
        var menu = new ContextMenu { PlacementTarget = _badge };
        foreach (var action in Item.Actions)
            menu.Items.Add(new MenuItem { Header = action.Header, Command = action.Command });
        menu.IsOpen = true;
    }

    /// <summary>IsHitTestVisible 을 늘 true 로 강제한 캔버스(배경 없음 = 빈 곳은 투과, 배지만 입력을 받는다).</summary>
    private sealed class AlwaysHitCanvas : Canvas
    {
        static AlwaysHitCanvas()
            => IsHitTestVisibleProperty.OverrideMetadata(typeof(AlwaysHitCanvas), new UIPropertyMetadata(true, null, (_, _) => true));
    }
}
