using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      : 사용률(0~100) → 도넛 호 Geometry (map-topbar-trafficlight FR-B1 보완).
                  MD 원형 ProgressBar 템플릿 의존을 제거하고, 회색 트랙 Ellipse와
                  기하학적으로 정확히 동심·동두께인 호를 직접 그린다 — 12시 시작, 시계방향.
                  (사용자 피드백: 트랙 원으로 원래 형상을 항상 짐작 가능하게)
   Note         : 0%/NaN = Geometry.Empty(트랙만 노출) · ≥100% = 완전 원(EllipseGeometry).
                  Diameter/Thickness는 트랙 Ellipse의 Width·StrokeThickness와 반드시 일치
                  (WPF Ellipse는 스트로크 절반을 안쪽으로 접어 반경 = (D-T)/2 — 동일 수식).
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
public class PercentToDonutArcConverter : IValueConverter
{
    /// <summary>도넛 외경(px) — 트랙 Ellipse Width/Height와 일치시킬 것.</summary>
    public double Diameter { get; set; } = 26d;

    /// <summary>선 두께(px) — 트랙 StrokeThickness와 일치시킬 것.</summary>
    public double Thickness { get; set; } = 3d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct;
        try { pct = System.Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        catch { return Geometry.Empty; }
        if (double.IsNaN(pct) || pct <= 0d) return Geometry.Empty;

        double c = Diameter / 2d;
        double r = (Diameter - Thickness) / 2d;

        if (pct >= 100d)
        {
            var full = new EllipseGeometry(new Point(c, c), r, r);
            full.Freeze();
            return full;
        }

        double angle = pct * 3.6d;
        double rad = (angle - 90d) * Math.PI / 180d;
        var start = new Point(c, c - r);                                    // 12시 방향
        var end = new Point(c + r * Math.Cos(rad), c + r * Math.Sin(rad));  // 시계방향 종점

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0d, angle > 180d, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
