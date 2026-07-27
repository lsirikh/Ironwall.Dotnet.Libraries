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
                  X축=조회 구간(RangeStart/End) 기준 + 휠 줌·드래그 팬·더블클릭 리셋 (런타임 피드백 v1.3).
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
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewResetNeeded));

    /// <summary>조회 구간 시작 — X축 전체 범위 기준(데이터가 일부 구간에만 있어도 축은 조회 기간을 반영).</summary>
    public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(
        nameof(RangeStart), typeof(DateTime?), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewResetNeeded));

    /// <summary>조회 구간 끝.</summary>
    public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(
        nameof(RangeEnd), typeof(DateTime?), typeof(SignalChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewResetNeeded));

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
    public DateTime? RangeStart
    {
        get => (DateTime?)GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }
    public DateTime? RangeEnd
    {
        get => (DateTime?)GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
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

    // 데이터/조회 구간이 바뀌면 줌·팬 뷰를 전체로 리셋 (이전 뷰 잔존 방지)
    private static void OnViewResetNeeded(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SignalChartControl)d).ResetView();
    #endregion

    #region - Ctors -
    public SignalChartControl()
    {
        // 배경 히트테스트 확보(빈 영역 MouseMove/휠/드래그 수신) — 렌더에서 투명 사각형을 깐다.
        ClipToBounds = true;
        Focusable = true;
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
    private const double DRAG_THRESHOLD = 4;           // 이하 이동=클릭, 초과=팬
    private const double MIN_VIEW_SPAN_SECONDS = 60;   // 최소 줌 구간(1분)
    private const double ZOOM_STEP = 1.25;
    #endregion

    #region - Attributes -
    // 렌더 시점 화면 좌표 캐시 — 히트테스트/툴팁용
    private readonly List<(Point Screen, SignalChartPoint Data)> _hitPoints = new();

    // 줌/팬 뷰 윈도우 — null=전체(조회 구간). 렌더 시 실제 사용값/플롯 좌표를 캐시해 인터랙션 환산에 사용.
    private DateTime? _viewStart, _viewEnd;
    private DateTime _fullStart, _fullEnd;
    private DateTime _renderStart, _renderEnd;
    private double _plotL, _plotR;
    private bool _hasData;

    // 드래그 팬 상태
    private bool _dragActive, _panMoved;
    private Point _dragOrigin;
    private DateTime _dragViewStart, _dragViewEnd;

    // (code-review P1-2) 현재 hover 포인트 캐시 — 포인트가 바뀔 때만 ToolTip 갱신
    private SignalChartPoint? _hoverPoint;
    #endregion

    #region - Render -
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _hitPoints.Clear();
        _hasData = false;

        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 80 || h < 60) return;

        // 히트테스트용 투명 배경
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        double plotL = MARGIN_LEFT, plotT = MARGIN_TOP;
        double plotR = w - MARGIN_RIGHT, plotB = h - MARGIN_BOTTOM;
        _plotL = plotL; _plotR = plotR;
        var axisPen = new Pen(AxisBrush, 1);
        var gridPen = new Pen(GridBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) };
        axisPen.Freeze(); gridPen.Freeze();

        // 축
        dc.DrawLine(axisPen, new Point(plotL, plotB), new Point(plotR, plotB));
        dc.DrawLine(axisPen, new Point(plotL, plotT), new Point(plotL, plotB));

        var all = ItemsSource?.Where(p => p.Signal > 0).OrderBy(p => p.Time).ToList();
        if (all is not { Count: > 0 })
        {
            DrawLabel(dc, "표시할 신호 데이터가 없습니다", new Point((plotL + plotR) / 2, (plotT + plotB) / 2), 12, TextAlignment.Center);
            return;
        }
        _hasData = true;

        // 전체 범위 — 조회 구간(RangeStart/End) 우선, 없으면 데이터 범위 (X축이 조회 기간을 반영)
        var fullStart = RangeStart ?? all[0].Time;
        var fullEnd = RangeEnd ?? all[^1].Time;
        if (fullEnd <= fullStart) { fullStart = fullStart.AddMinutes(-30); fullEnd = fullEnd.AddMinutes(30); }
        // 전체 span이 최소 뷰 구간보다 짧으면 최소 1분으로 확장 — 줌/팬 Clamp(min>max) ArgumentException(크래시) 원천 차단.
        if ((fullEnd - fullStart).TotalSeconds < MIN_VIEW_SPAN_SECONDS)
            fullEnd = fullStart.AddSeconds(MIN_VIEW_SPAN_SECONDS);
        _fullStart = fullStart; _fullEnd = fullEnd;

        // 현재 뷰(줌/팬 윈도우) — 없으면 전체. 전체 범위 밖으로 벗어나 있으면 리셋.
        var tMin = _viewStart ?? fullStart;
        var tMax = _viewEnd ?? fullEnd;
        if (tMax <= tMin || tMin < fullStart.AddSeconds(-1) || tMax > fullEnd.AddSeconds(1))
        {
            tMin = fullStart; tMax = fullEnd;
            _viewStart = _viewEnd = null;
        }
        _renderStart = tMin; _renderEnd = tMax;
        double span = (tMax - tMin).TotalSeconds;

        double X(DateTime t) => plotL + (t - tMin).TotalSeconds / span * (plotR - plotL);

        // 뷰 내 포인트 + 경계 이웃(라인이 화면 밖에서 이어져 들어오도록)
        var items = FilterToView(all, tMin, tMax);

        // Y 스케일 — 뷰 내 최대(줌 시 재스케일), 뷰가 비면 전체 최대
        int yMax = NiceCeiling((items.Count > 0 ? items : all).Max(p => p.Signal));
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

        // X 격자 + 라벨 (4분할) — 뷰 범위 기준 포맷(48h 초과 시 날짜 병기)
        string timeFormat = span <= 48 * 3600 ? "HH:mm" : "MM-dd HH:mm";
        for (int i = 0; i <= 4; i++)
        {
            var t = tMin.AddSeconds(span * i / 4);
            double x = plotL + (plotR - plotL) * i / 4;
            if (i > 0 && i < 4)
                dc.DrawLine(gridPen, new Point(x, plotT), new Point(x, plotB));
            DrawLabel(dc, t.ToString(timeFormat), new Point(x, plotB + 6), 10, TextAlignment.Center);
        }

        if (items.Count == 0)
        {
            DrawLabel(dc, "이 구간에 신호가 없습니다 (휠=줌 · 드래그=이동 · 더블클릭=전체)",
                new Point((plotL + plotR) / 2, (plotT + plotB) / 2), 11, TextAlignment.Center);
            return;
        }

        // 플롯 영역 클리핑 — 경계 이웃 포인트가 축 밖으로 그려지지 않도록
        dc.PushClip(new RectangleGeometry(new Rect(plotL, plotT, plotR - plotL, plotB - plotT)));

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
            if (center.X >= plotL - 1 && center.X <= plotR + 1)
                _hitPoints.Add((center, p));
            var fill = p.IsActioned ? PointBrush : UnactionedBrush;
            bool selected = SelectedPayload != null && Equals(SelectedPayload, p.Payload);
            dc.DrawEllipse(fill, selected ? new Pen(LabelBrush, 2) : null, center, POINT_RADIUS, POINT_RADIUS);
        }

        dc.Pop();
    }

    /// <summary>뷰 구간 내 포인트 + 양쪽 경계 이웃 1개씩(라인 연속성).</summary>
    private static List<SignalChartPoint> FilterToView(List<SignalChartPoint> all, DateTime start, DateTime end)
    {
        var result = new List<SignalChartPoint>();
        for (int i = 0; i < all.Count; i++)
        {
            var t = all[i].Time;
            bool inside = t >= start && t <= end;
            bool leftNeighbor = t < start && i + 1 < all.Count && all[i + 1].Time >= start;
            bool rightNeighbor = t > end && i - 1 >= 0 && all[i - 1].Time <= end;
            if (inside || leftNeighbor || rightNeighbor)
                result.Add(all[i]);
        }
        return result;
    }

    /// <summary>1-2-5 스텝 올림 (예: 3150 → 4000) — Y축 눈금이 읽기 좋은 값이 되도록.</summary>
    private static int NiceCeiling(int value)
    {
        if (value <= 4) return 4;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(value)));
        double f = value / exp;
        double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 4 ? 4 : f <= 5 ? 5 : 10;
        return (int)Math.Min(nice * exp, int.MaxValue);
    }

    private void DrawLabel(DrawingContext dc, string text, Point anchor, double size, TextAlignment align)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), size, LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { TextAlignment = align };
        dc.DrawText(ft, anchor);
    }
    #endregion

    #region - Interaction (줌/팬/툴팁/클릭→행 동기화) -
    private void ResetView()
    {
        _viewStart = _viewEnd = null;
        InvalidateVisual();
    }

    private void SetView(DateTime start, DateTime end)
    {
        // 전체 범위로 클램프 — 범위 밖 팬/줌 방지, 최소 구간 1분
        double fullSpan = (_fullEnd - _fullStart).TotalSeconds;
        if (fullSpan < MIN_VIEW_SPAN_SECONDS) return;   // 방어: OnRender가 보장하나 stale 진입 시 Clamp(min>max) 크래시 회피
        double span = Math.Clamp((end - start).TotalSeconds, MIN_VIEW_SPAN_SECONDS, fullSpan);
        if (start < _fullStart) start = _fullStart;
        if (start.AddSeconds(span) > _fullEnd) start = _fullEnd.AddSeconds(-span);

        if (span >= fullSpan - 0.5)
        {
            _viewStart = _viewEnd = null;   // 전체와 같으면 리셋 상태
        }
        else
        {
            _viewStart = start;
            _viewEnd = start.AddSeconds(span);
        }
        InvalidateVisual();
    }

    private DateTime ScreenToTime(double x)
    {
        double ratio = Math.Clamp((x - _plotL) / Math.Max(1, _plotR - _plotL), 0, 1);
        return _renderStart.AddSeconds((_renderEnd - _renderStart).TotalSeconds * ratio);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_hasData) return;
        if ((_fullEnd - _fullStart).TotalSeconds < MIN_VIEW_SPAN_SECONDS) return;   // 방어: fullSpan<최소구간이면 줌 무력화(Clamp 크래시 회피)

        // 커서 시각 고정 줌 — 커서가 가리키는 시간이 화면상 같은 위치에 남도록
        var pos = e.GetPosition(this);
        var cursorTime = ScreenToTime(pos.X);
        double curSpan = (_renderEnd - _renderStart).TotalSeconds;
        double newSpan = e.Delta > 0 ? curSpan / ZOOM_STEP : curSpan * ZOOM_STEP;
        newSpan = Math.Clamp(newSpan, MIN_VIEW_SPAN_SECONDS, (_fullEnd - _fullStart).TotalSeconds);

        double ratio = Math.Clamp((pos.X - _plotL) / Math.Max(1, _plotR - _plotL), 0, 1);
        var newStart = cursorTime.AddSeconds(-newSpan * ratio);
        SetView(newStart, newStart.AddSeconds(newSpan));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_hasData) return;

        if (e.ClickCount == 2)   // 더블클릭=전체 보기
        {
            ResetView();
            e.Handled = true;
            return;
        }

        _dragActive = true;
        _panMoved = false;
        _dragOrigin = e.GetPosition(this);
        _dragViewStart = _renderStart;
        _dragViewEnd = _renderEnd;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);

        // 드래그 팬 — 임계(4px) 초과 이동 시 뷰 이동, 미만이면 클릭 후보 유지
        if (_dragActive)
        {
            double dx = pos.X - _dragOrigin.X;
            if (!_panMoved && Math.Abs(dx) < DRAG_THRESHOLD) return;
            _panMoved = true;
            Cursor = Cursors.ScrollWE;
            ToolTip = null;
            _hoverPoint = null;

            double secondsPerPixel = (_dragViewEnd - _dragViewStart).TotalSeconds / Math.Max(1, _plotR - _plotL);
            var shifted = _dragViewStart.AddSeconds(-dx * secondsPerPixel);
            SetView(shifted, shifted.AddSeconds((_dragViewEnd - _dragViewStart).TotalSeconds));
            return;
        }

        // (code-review P1-2) hover 포인트 변경 시에만 ToolTip 갱신
        var hit = FindNearest(pos);
        if (ReferenceEquals(hit, _hoverPoint)) return;
        _hoverPoint = hit;

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

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragActive) return;
        _dragActive = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;

        if (_panMoved)   // 팬 종료 — 클릭 아님
        {
            _panMoved = false;
            return;
        }

        // 이동 없는 클릭 = 포인트 선택 (code-review P1-3: 커맨드→VM→TwoWay 역류로 단일화, 커맨드 없을 때만 직접 세팅)
        var hit = FindNearest(e.GetPosition(this));
        if (hit is not { } p) return;
        if (PointClickedCommand?.CanExecute(p.Payload) == true)
            PointClickedCommand.Execute(p.Payload);
        else
            SelectedPayload = p.Payload;
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverPoint = null;
        if (!_dragActive) Cursor = Cursors.Arrow;
        ToolTip = null;
    }

    // MouseUp 없이 캡처가 상실되는 경로(Alt-Tab·창 비활성화·타 요소 캡처 탈취·드래그 중 다이얼로그 교체)에서
    // _dragActive 고착(버튼 안 눌러도 hover만으로 팬되는 오작동) 방지 — 팬 상태를 안전하게 종료.
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragActive = false;
        _panMoved = false;
        Cursor = Cursors.Arrow;
        ToolTip = null;
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
