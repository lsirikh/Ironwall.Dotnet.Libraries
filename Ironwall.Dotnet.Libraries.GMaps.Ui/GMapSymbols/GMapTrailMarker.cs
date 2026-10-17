using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      : 추적 이동경로 트레일 마커 (점선·시간경과 페이드)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 한 트랙의 최근 이동경로를 표시하는 트레일 마커.
/// <para><b>GMapMarkerLineControl 상속 금지</b>(매 팬/줌 new PointCollection·무디바운스 안티패턴 회피).
/// 단일 Canvas + 복수 <see cref="Line"/> 세그먼트, 세그먼트별 Opacity 선형감쇠(최신 1.0 → 오래됨 옅음).</para>
/// <para><b>투영 최적화(FR-P2-08)</b>: anchor(최신점) 기준 상대좌표라 <b>팬에는 재계산 불필요</b>(전체가 동일 이동),
/// <b>줌 변경 시에만</b> <see cref="Rebuild"/>(매니저가 줌 이벤트 1회 구독해 일괄 호출).</para>
/// <para>ZIndex=1990(타겟 마커 2000 바로 아래). 모든 호출은 UI Dispatcher에서만(매니저 보장).</para>
/// </summary>
public sealed class GMapTrailMarker : GMapMarker
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly List<PointLatLng> _points = new();
    private SolidColorBrush _stroke;

    public int CameraId { get; }
    public string TrackId { get; }
    public int Count => _points.Count;

    public GMapTrailMarker(int cameraId, string trackId, EnumColorType color) : base(default)
    {
        CameraId = cameraId;
        TrackId = trackId;
        _stroke = BrushFor(color);
        Shape = _canvas;
        Offset = new Point(0, 0);   // Canvas 원점 = anchor 스크린 좌표
        ZIndex = 1990;
    }

    /// <summary>새 점 추가(FIFO 상한). anchor를 최신점으로 갱신.</summary>
    public void AddPoint(PointLatLng p, int maxPoints)
    {
        _points.Add(p);
        while (_points.Count > Math.Max(2, maxPoints))
            _points.RemoveAt(0);
        Position = p;   // anchor = 최신점
    }

    /// <summary>세그먼트 분절(NATS gap) — 큐/캔버스 비움.</summary>
    public void ResetSegment()
    {
        _points.Clear();
        _canvas.Children.Clear();
    }

    public void SetColor(EnumColorType color) => _stroke = BrushFor(color);

    /// <summary>현재 줌 기준으로 세그먼트 재투영(줌 변경·점 추가 시). 팬에는 호출 불필요.</summary>
    public void Rebuild(GMapControl map)
    {
        _canvas.Children.Clear();
        int n = _points.Count;
        if (n < 2) return;

        var anchor = map.FromLatLngToLocal(Position);   // 최신점 스크린
        for (int i = 0; i < n - 1; i++)
        {
            var a = map.FromLatLngToLocal(_points[i]);
            var b = map.FromLatLngToLocal(_points[i + 1]);
            _canvas.Children.Add(new Line
            {
                X1 = a.X - anchor.X, Y1 = a.Y - anchor.Y,
                X2 = b.X - anchor.X, Y2 = b.Y - anchor.Y,
                Stroke = _stroke,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Opacity = Services.Tracking.TrackingMath.TrailFadeOpacity(i, n),
                IsHitTestVisible = false,
            });
        }
    }

    private static SolidColorBrush BrushFor(EnumColorType color)
    {
        var b = color switch
        {
            EnumColorType.Green => new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
            EnumColorType.Orange => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)),
            EnumColorType.Red => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7)),
        };
        b.Freeze();
        return b;
    }
}
