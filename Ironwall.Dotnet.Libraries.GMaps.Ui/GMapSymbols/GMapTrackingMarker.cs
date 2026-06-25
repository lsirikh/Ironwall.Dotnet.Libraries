using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      : 추적 타겟 오버레이 마커 (transient — 편집/영속 없음)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// AiAnalysis TRACKING_STATUS 타겟을 지도에 표시하는 경량 마커.
/// <para>심볼/장비 마커(<c>GMapBaseMarker&lt;ISymbolModel&gt;</c>)와 달리 <b>편집·영속·속성패널이 없는 transient</b>
/// 마커라 ISymbolModel 결합을 피하고 <see cref="GMapMarker"/>를 직접 파생한다.</para>
/// <para>비주얼: 위협색 원 + 타입 이니셜 + 방향 화살표(bearing) + track_id 라벨. 위협색은 severity hue-lock(테마 불변).</para>
/// <para>스레드: 생성·갱신은 모두 UI Dispatcher에서만 호출(매니저가 보장).</para>
/// </summary>
public sealed class GMapTrackingMarker : GMapMarker
{
    private const double Box = 40d;     // 마커 캔버스 크기
    private const double DotSize = 18d;

    private readonly Ellipse _dot;
    private readonly TextBlock _glyph;
    private readonly TextBlock _label;
    private readonly Polygon _arrow;
    private readonly RotateTransform _arrowRotate;

    public int CameraId { get; }
    public string TrackId { get; }

    public GMapTrackingMarker(int cameraId, string trackId, PointLatLng position) : base(position)
    {
        CameraId = cameraId;
        TrackId = trackId;

        var canvas = new Canvas { Width = Box, Height = Box, IsHitTestVisible = false };

        // 방향 화살표 (북=0°, 시계방향). 중심(20,20) 기준 회전.
        _arrowRotate = new RotateTransform(0d, Box / 2, Box / 2);
        _arrow = new Polygon
        {
            Points = new PointCollection { new(Box / 2, 1), new(Box / 2 - 5, 13), new(Box / 2 + 5, 13) },
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            StrokeThickness = 0.5,
            RenderTransform = _arrowRotate,
            Visibility = Visibility.Collapsed,
        };
        canvas.Children.Add(_arrow);

        // 위협색 원
        _dot = new Ellipse
        {
            Width = DotSize,
            Height = DotSize,
            Fill = BrushFor(EnumColorType.Gray),
            Stroke = Brushes.White,
            StrokeThickness = 2,
        };
        Canvas.SetLeft(_dot, (Box - DotSize) / 2);
        Canvas.SetTop(_dot, (Box - DotSize) / 2);
        canvas.Children.Add(_dot);

        // 타입 이니셜
        _glyph = new TextBlock
        {
            Text = "?",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Width = DotSize,
            TextAlignment = TextAlignment.Center,
        };
        Canvas.SetLeft(_glyph, (Box - DotSize) / 2);
        Canvas.SetTop(_glyph, (Box - DotSize) / 2 + 1);
        canvas.Children.Add(_glyph);

        // track_id 라벨
        _label = new TextBlock
        {
            Text = trackId,
            Foreground = Brushes.White,
            FontSize = 8,
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            Padding = new Thickness(2, 0, 2, 0),
            TextAlignment = TextAlignment.Center,
        };
        Canvas.SetTop(_label, Box - 9);
        Canvas.SetLeft(_label, 0);
        _label.Width = Box;
        canvas.Children.Add(_label);

        Shape = canvas;
        Offset = new Point(-Box / 2, -Box / 2);   // 중심 정렬
    }

    /// <summary>위협색·타입·라벨·방향을 갱신(위치는 매니저가 <see cref="GMapMarker.Position"/> 직접 설정).</summary>
    public void Update(EnumColorType threatColor, EnumTargetType type, double bearing, bool showArrow)
    {
        var brush = BrushFor(threatColor);
        _dot.Fill = brush;
        _arrow.Fill = brush;
        _glyph.Text = GlyphFor(type);
        _arrowRotate.Angle = bearing;
        _arrow.Visibility = showArrow ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>lost/idle 등 흐릿 처리(제거 전 페이드 표현).</summary>
    public void SetDimmed(bool dimmed) => ((Canvas)Shape).Opacity = dimmed ? 0.45 : 1.0;

    private static string GlyphFor(EnumTargetType type) => type switch
    {
        EnumTargetType.Person => "P",
        EnumTargetType.Vehicle => "V",
        EnumTargetType.Animal => "A",
        _ => "?",
    };

    // severity hue-lock — 의미색은 테마 불변(직접 brush). NORMAL/CAUTION/THREAT/Unknown.
    private static SolidColorBrush BrushFor(EnumColorType color)
    {
        var b = color switch
        {
            EnumColorType.Green => new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
            EnumColorType.Orange => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)),
            EnumColorType.Red => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
        };
        b.Freeze();
        return b;
    }
}
