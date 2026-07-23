using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Controls;
/****************************************************************************
   Purpose      : 탐지 신호 시간축 차트 (Detection_Signal_History FR-13)
                  자체 OnRender 렌더러 — 외부 차트 패키지 없이 ≤500 포인트 렌더.
                  색상은 전부 Brush DP로 받는다(뷰에서 DynamicResource 토큰 바인딩 → 다크/라이트 대응).
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>차트 1포인트 — Payload에 원본 행 VM을 실어 클릭 시 그리드 행과 동기화한다.</summary>
public sealed record SignalChartPoint(DateTime Time, int Signal, bool IsActioned, string ResultText, object? Payload);

public class SignalChartControl : FrameworkElement
{
    #region - DependencyProperties -
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IReadOnlyList<SignalChartPoint>), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedPayloadProperty = DependencyProperty.Register(
        nameof(SelectedPayload), typeof(object), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PointClickedCommandProperty = DependencyProperty.Register(
        nameof(PointClickedCommand), typeof(ICommand), typeof(SignalChartControl), new PropertyMetadata(null));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.CadetBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PointBrushProperty = DependencyProperty.Register(
        nameof(PointBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.CadetBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnactionedBrushProperty = DependencyProperty.Register(
        nameof(UnactionedBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AxisBrushProperty = DependencyProperty.Register(
        nameof(AxisBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
        nameof(LabelBrush), typeof(Brush), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<SignalChartPoint>? ItemsSource
    {
        get => (IReadOnlyList<SignalChartPoint>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public object? SelectedPayload
    {
        get => GetValue(SelectedPayloadProperty);
        set => SetValue(SelectedPayloadProperty, value);
    }
    public ICommand? PointClickedCommand
    {
        get => (ICommand?)GetValue(PointClickedCommandProperty);
        set => SetValue(PointClickedCommandProperty, value);
    }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Brush PointBrush { get => (Brush)GetValue(PointBrushProperty); set => SetValue(PointBrushProperty, value); }
    public Brush UnactionedBrush { get => (Brush)GetValue(UnactionedBrushProperty); set => SetValue(UnactionedBrushProperty, value); }
    public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    #endregion

    #region - Ctors -
    public SignalChartControl()
    {
        // 배경 히트테스트 확보(빈 영역 MouseMove/클릭 수신) — 렌더에서 투명 사각형을 깐다.
        ClipToBounds = true;
        ToolTipService.SetInitialShowDelay(this, 150);
        ToolTipService.SetShowDuration(this, 8000);
    }
    #endregion

    #region - Layout constants -
    private const double MARGIN_LEFT = 52;
    private const double MARGIN_RIGHT = 14;
    private const double MARGIN_TOP = 14;
    private const double MARGIN_BOTTOM = 30;
    private const double POINT_RADIUS = 4.5;
    private const double HIT_RADIUS = 10;
    #endregion

    // 렌더 시점 화면 좌표 캐시 — 히트테스트/툴팁용
    private readonly List<(Point Screen, SignalChartPoint Data)> _hitPoints = new();

    #region - Render -
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _hitPoints.Clear();

        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 80 || h < 60) return;

        // 히트테스트용 투명 배경
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        double plotL = MARGIN_LEFT, plotT = MARGIN_TOP;
        double plotR = w - MARGIN_RIGHT, plotB = h - MARGIN_BOTTOM;
        var axisPen = new Pen(AxisBrush, 1);
        var gridPen = new Pen(GridBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) };
        axisPen.Freeze(); gridPen.Freeze();

        // 축
        dc.DrawLine(axisPen, new Point(plotL, plotB), new Point(plotR, plotB));
        dc.DrawLine(axisPen, new Point(plotL, plotT), new Point(plotL, plotB));

        var items = ItemsSource?.Where(p => p.Signal > 0).OrderBy(p => p.Time).ToList();
        if (items is not { Count: > 0 })
        {
            DrawLabel(dc, "표시할 신호 데이터가 없습니다", new Point((plotL + plotR) / 2, (plotT + plotB) / 2), 12, TextAlignment.Center);
            return;
        }

        // 스케일 — Y: 구간 최대값을 1-2-5 스텝으로 올림, X: 시간 범위(동일 시각 단건이면 ±30분 패딩)
        var tMin = items[0].Time;
        var tMax = items[^1].Time;
        if (tMax <= tMin) { tMin = tMin.AddMinutes(-30); tMax = tMax.AddMinutes(30); }
        double span = (tMax - tMin).TotalSeconds;
        int yMax = NiceCeiling(items.Max(p => p.Signal));

        double X(DateTime t) => plotL + (t - tMin).TotalSeconds / span * (plotR - plotL);
        double Y(int s) => plotB - Math.Clamp((double)s / yMax, 0, 1) * (plotB - plotT);

        // Y 격자 + 라벨 (4분할)
        for (int i = 1; i <= 4; i++)
        {
            int v = yMax * i / 4;
            double y = Y(v);
            dc.DrawLine(gridPen, new Point(plotL, y), new Point(plotR, y));
            DrawLabel(dc, v.ToString("N0"), new Point(plotL - 6, y - 7), 10, TextAlignment.Right);
        }
        DrawLabel(dc, "0", new Point(plotL - 6, plotB - 7), 10, TextAlignment.Right);

        // X 격자 + 라벨 (4분할) — 범위에 따라 포맷 선택
        string timeFormat = span <= 48 * 3600 ? "HH:mm" : "MM-dd";
        for (int i = 0; i <= 4; i++)
        {
            var t = tMin.AddSeconds(span * i / 4);
            double x = X(t);
            if (i > 0 && i < 4)
                dc.DrawLine(gridPen, new Point(x, plotT), new Point(x, plotB));
            DrawLabel(dc, t.ToString(timeFormat), new Point(x, plotB + 6), 10, TextAlignment.Center);
        }

        // 라인
        if (items.Count > 1)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(X(items[0].Time), Y(items[0].Signal)), false, false);
                foreach (var p in items.Skip(1))
                    ctx.LineTo(new Point(X(p.Time), Y(p.Signal)), true, true);
            }
            geo.Freeze();
            var linePen = new Pen(LineBrush, 2) { LineJoin = PenLineJoin.Round };
            linePen.Freeze();
            dc.DrawGeometry(null, linePen, geo);
        }

        // 포인트 (미조치=critical, 선택=외곽 강조)
        foreach (var p in items)
        {
            var center = new Point(X(p.Time), Y(p.Signal));
            _hitPoints.Add((center, p));
            var fill = p.IsActioned ? PointBrush : UnactionedBrush;
            bool selected = SelectedPayload != null && Equals(SelectedPayload, p.Payload);
            dc.DrawEllipse(fill, selected ? new Pen(LabelBrush, 2) : null, center, POINT_RADIUS, POINT_RADIUS);
        }
    }

    /// <summary>1-2-5 스텝 올림 (예: 3150 → 4000) — Y축 눈금이 읽기 좋은 값이 되도록.</summary>
    private static int NiceCeiling(int value)
    {
        if (value <= 4) return 4;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(value)));
        double f = value / exp;
        double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 4 ? 4 : f <= 5 ? 5 : 10;
        return (int)(nice * exp);
    }

    private void DrawLabel(DrawingContext dc, string text, Point anchor, double size, TextAlignment align)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), size, LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { TextAlignment = align };
        dc.DrawText(ft, anchor);
    }
    #endregion

    #region - Interaction (툴팁/클릭→행 동기화) -
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = FindNearest(e.GetPosition(this));
        if (hit is { } p)
        {
            Cursor = Cursors.Hand;
            ToolTip = $"{p.Time:MM-dd HH:mm:ss} · {p.ResultText}\n신호 {p.Signal:N0} · {(p.IsActioned ? "조치" : "미조치")}";
        }
        else
        {
            Cursor = Cursors.Arrow;
            ToolTip = null;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var hit = FindNearest(e.GetPosition(this));
        if (hit is not { } p) return;

        SelectedPayload = p.Payload;
        if (PointClickedCommand?.CanExecute(p.Payload) == true)
            PointClickedCommand.Execute(p.Payload);
        InvalidateVisual();
        e.Handled = true;
    }

    private SignalChartPoint? FindNearest(Point pos)
    {
        SignalChartPoint? best = null;
        double bestDist = HIT_RADIUS;
        foreach (var (screen, data) in _hitPoints)
        {
            double d = (screen - pos).Length;
            if (d < bestDist) { bestDist = d; best = data; }
        }
        return best;
    }
    #endregion
}
