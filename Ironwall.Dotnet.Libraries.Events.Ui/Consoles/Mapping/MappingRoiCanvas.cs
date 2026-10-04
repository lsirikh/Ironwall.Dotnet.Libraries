using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 ROI 그리기 캔버스 — 클릭=점, 첫 점=닫기, 꼭짓점 끌기
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// ROI 다각형을 그리는 캔버스. 마우스 좌표를 <b>0~1 로 바꿔</b> <see cref="MappingRoiEditorViewModel"/> 에 넘기기만 하고,
/// 판정(찍을 수 있나 · 닫을 수 있나 · 겹치나)은 전부 뷰모델이 한다.
/// </summary>
/// <remarks>
/// <para>조작: 빈 곳 클릭 = 점 추가(선이 이어진다) · 첫 점 클릭 · 더블클릭 · Enter = 닫기 ·
/// 우클릭 · Backspace = 마지막 점 지우기 · Esc = 그리던 선 버리기 · 닫은 뒤 꼭짓점 끌기 = 옮기기.</para>
/// <para>그림 칸은 기준 해상도 비율(<see cref="MappingRoiEditorViewModel.CanvasAspect"/>)로 가운데 맞춘다.
/// 색은 의존 속성으로 받아 뷰가 <c>DynamicResource</c> 토큰을 건다(테마 전환이 따라온다).</para>
/// </remarks>
public sealed class MappingRoiCanvas : FrameworkElement
{
    /// <summary>꼭짓점을 집는 거리(px).</summary>
    private const double HIT_RADIUS = 9;
    private const double VERTEX_RADIUS = 4.5;

    private Point? _hover;
    private int _dragIndex = -1;

    /// <summary>생성자.</summary>
    public MappingRoiCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
    }

    #region - 의존 속성 -
    /// <summary>그리기 판정을 맡은 뷰모델.</summary>
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(MappingRoiEditorViewModel), typeof(MappingRoiCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEditorChanged));

    /// <summary>그리기 판정을 맡은 뷰모델.</summary>
    public MappingRoiEditorViewModel? Editor
    {
        get => (MappingRoiEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>그림 칸 바탕.</summary>
    public static readonly DependencyProperty BackdropProperty = Brush(nameof(Backdrop), Brushes.Black);

    /// <summary>그림 칸 바탕.</summary>
    public Brush Backdrop { get => (Brush)GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }

    /// <summary>
    /// 그림 칸 바탕 영상(카메라 실시간 프레임). 뷰가 <c>HostedVideoView.Source</c>(WriteableBitmap)를 건다 —
    /// 비트맵은 자리에서 갱신되므로 프레임마다 다시 그리지 않아도 화면이 따라온다. 없으면 <see cref="Backdrop"/> 만.
    /// </summary>
    public static readonly DependencyProperty VideoImageProperty = DependencyProperty.Register(
        nameof(VideoImage), typeof(ImageSource), typeof(MappingRoiCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>그림 칸 바탕 영상.</summary>
    public ImageSource? VideoImage { get => (ImageSource?)GetValue(VideoImageProperty); set => SetValue(VideoImageProperty, value); }

    /// <summary>격자 · 테두리 색.</summary>
    public static readonly DependencyProperty GridBrushProperty = Brush(nameof(GridBrush), Brushes.Gray);

    /// <summary>격자 · 테두리 색.</summary>
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    /// <summary>지금 그리는 ROI 색.</summary>
    public static readonly DependencyProperty ShapeBrushProperty = Brush(nameof(ShapeBrush), Brushes.DodgerBlue);

    /// <summary>지금 그리는 ROI 색.</summary>
    public Brush ShapeBrush { get => (Brush)GetValue(ShapeBrushProperty); set => SetValue(ShapeBrushProperty, value); }

    /// <summary>같은 프리셋의 다른 ROI 색(흐리게).</summary>
    public static readonly DependencyProperty OtherBrushProperty = Brush(nameof(OtherBrush), Brushes.Silver);

    /// <summary>같은 프리셋의 다른 ROI 색(흐리게).</summary>
    public Brush OtherBrush { get => (Brush)GetValue(OtherBrushProperty); set => SetValue(OtherBrushProperty, value); }

    private static DependencyProperty Brush(string name, Brush fallback) => DependencyProperty.Register(
        name, typeof(Brush), typeof(MappingRoiCanvas),
        new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (MappingRoiCanvas)d;
        if (e.OldValue is MappingRoiEditorViewModel old)
        {
            old.ShapeChanged -= canvas.OnShapeChanged;
            old.PropertyChanged -= canvas.OnEditorPropertyChanged;
            old.Rois.CollectionChanged -= canvas.OnRoisChanged;
        }
        if (e.NewValue is MappingRoiEditorViewModel now)
        {
            now.ShapeChanged += canvas.OnShapeChanged;
            now.PropertyChanged += canvas.OnEditorPropertyChanged;
            now.Rois.CollectionChanged += canvas.OnRoisChanged;
        }
        canvas._dragIndex = -1;
    }

    private void OnShapeChanged(object? sender, EventArgs e) => Redraw();

    private void OnRoisChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MappingRoiEditorViewModel.SelectedRoi) or nameof(MappingRoiEditorViewModel.CanDraw)
            or nameof(MappingRoiEditorViewModel.CanMovePoints) or nameof(MappingRoiEditorViewModel.HasForm))
            Redraw();
    }

    private void Redraw()
    {
        if (Dispatcher.CheckAccess()) { InvalidateVisual(); UpdateCursor(); }
        else Dispatcher.BeginInvoke(new Action(() => { InvalidateVisual(); UpdateCursor(); }));
    }
    #endregion

    #region - 좌표 -
    /// <summary>기준 해상도 비율로 가운데 맞춘 그림 칸.</summary>
    private Rect Frame()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return Rect.Empty;
        var aspect = Editor?.CanvasAspect ?? 16.0 / 9.0;
        if (aspect <= 0 || double.IsNaN(aspect) || double.IsInfinity(aspect)) aspect = 16.0 / 9.0;
        double fw = w, fh = w / aspect;
        if (fh > h) { fh = h; fw = h * aspect; }
        return new Rect((w - fw) / 2, (h - fh) / 2, fw, fh);
    }

    private static Point ToScreen(Rect frame, MappingRoiPoint p) => new(frame.X + p.X * frame.Width, frame.Y + p.Y * frame.Height);

    private static (double X, double Y) ToNormal(Rect frame, Point p)
        => ((p.X - frame.X) / frame.Width, (p.Y - frame.Y) / frame.Height);

    /// <summary>화면 점에서 가장 가까운 꼭짓점(집는 거리 안) — 없으면 -1.</summary>
    private int HitVertex(Rect frame, Point at)
    {
        var editor = Editor;
        if (editor is null) return -1;
        int best = -1;
        double bestDistance = HIT_RADIUS;
        for (int i = 0; i < editor.Points.Count; i++)
        {
            var d = (ToScreen(frame, editor.Points[i]) - at).Length;
            if (d <= bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }
    #endregion

    #region - 마우스 · 키 -
    /// <inheritdoc/>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var editor = Editor;
        var frame = Frame();
        if (editor is null || frame.IsEmpty) return;
        var at = e.GetPosition(this);

        if (editor.CanMovePoints)
        {
            var hit = HitVertex(frame, at);
            if (hit >= 0)
            {
                _dragIndex = hit;
                CaptureMouse();
                e.Handled = true;
            }
            return;
        }

        // 첫 점을 다시 누르거나 더블클릭하면 닫는다.
        if (editor.CanClosePolygon && (e.ClickCount >= 2 || HitVertex(frame, at) == 0))
        {
            editor.ClosePolygon();
            e.Handled = true;
            return;
        }

        if (editor.CanDraw && frame.Contains(at))
        {
            var (x, y) = ToNormal(frame, at);
            editor.AddPoint(x, y);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var at = e.GetPosition(this);
        _hover = at;
        if (_dragIndex >= 0 && Editor is { } editor && e.LeftButton == MouseButtonState.Pressed)
        {
            var frame = Frame();
            if (!frame.IsEmpty)
            {
                var (x, y) = ToNormal(frame, at);
                editor.MovePoint(_dragIndex, x, y);
            }
        }
        InvalidateVisual();
        UpdateCursor();
    }

    /// <inheritdoc/>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
    }

    /// <inheritdoc/>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragIndex = -1;
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (Editor is { CanUndoPoint: true } editor)
        {
            editor.UndoPoint();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 캔버스에 초점이 있을 때의 키. 대화상자 틀이 <c>PreviewKeyDown</c> 에서 ESC 를 "닫기" 로 먹기 때문에
    /// <b>뷰가 틀보다 먼저</b>(제 <c>PreviewKeyDown</c>) 이것을 부른다.
    /// </summary>
    /// <returns>처리했으면 <c>true</c>(틀로 보내지 않는다).</returns>
    public bool HandleKey(Key key)
    {
        var editor = Editor;
        if (editor is null) return false;
        switch (key)
        {
            case Key.Enter when editor.CanClosePolygon:
                editor.ClosePolygon();
                return true;
            case Key.Back or Key.Delete when editor.CanUndoPoint:
                editor.UndoPoint();
                return true;
            case Key.Escape when !editor.IsClosed && editor.Points.Count > 0 && editor.CanEdit:
                // 그리던 선만 버린다 — 창 닫기로 새지 않는다.
                editor.CancelDrawing();
                return true;
            default:
                return false;
        }
    }

    private void EndDrag()
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private void UpdateCursor()
    {
        var editor = Editor;
        var frame = Frame();
        if (editor is null || frame.IsEmpty) { Cursor = null; return; }
        if (_dragIndex >= 0) { Cursor = Cursors.SizeAll; return; }
        if (_hover is Point at)
        {
            if (editor.CanMovePoints && HitVertex(frame, at) >= 0) { Cursor = Cursors.SizeAll; return; }
            if (editor.CanClosePolygon && HitVertex(frame, at) == 0) { Cursor = Cursors.Hand; return; }
        }
        Cursor = editor.CanDraw ? Cursors.Cross : null;
    }
    #endregion

    #region - 그리기 -
    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        var frame = Frame();
        if (frame.IsEmpty) return;

        dc.DrawRectangle(Backdrop, null, frame);
        // 영상 프레임은 이미 그림 칸 비율로 온다(호스트가 카메라 비율을 그 안에 맞춘다) — 칸에 그대로 채운다.
        if (VideoImage is { } video) dc.DrawImage(video, frame);
        DrawGrid(dc, frame);

        var editor = Editor;
        if (editor is null) return;

        // 같은 프리셋의 다른 ROI — 겹침을 보게 흐리게.
        var otherPen = MakePen(new Pen(OtherBrush, 1.2) { DashStyle = DashStyles.Dash });
        var otherFill = Faded(OtherBrush, 0.10);
        foreach (var roi in editor.Rois.Where(r => r.Id != editor.SelectedRoi?.Id || editor.IsNew))
        {
            if (roi.Points.Count < 2) continue;
            dc.DrawGeometry(otherFill, otherPen, Polygon(frame, roi.Points, closed: true));
        }

        if (!editor.HasForm) return;
        var points = editor.Points;
        if (points.Count == 0) { DrawPreview(dc, frame, editor); return; }

        var shapePen = MakePen(new Pen(ShapeBrush, 2) { LineJoin = PenLineJoin.Round });
        dc.DrawGeometry(editor.IsClosed ? Faded(ShapeBrush, 0.22) : null, shapePen, Polygon(frame, points, editor.IsClosed));
        DrawPreview(dc, frame, editor);

        // 꼭짓점 — 첫 점은 "여기를 누르면 닫힘" 이라 크게, 집을 수 있는 점은 테두리를 두껍게.
        int hovered = _hover is Point at ? HitVertex(frame, at) : -1;
        var vertexPen = MakePen(new Pen(ShapeBrush, 1.5));
        for (int i = 0; i < points.Count; i++)
        {
            var c = ToScreen(frame, points[i]);
            var closable = i == 0 && editor.CanClosePolygon;
            var r = closable ? VERTEX_RADIUS + 2.5 : i == hovered && editor.CanMovePoints ? VERTEX_RADIUS + 1.5 : VERTEX_RADIUS;
            dc.DrawEllipse(closable || i == hovered ? ShapeBrush : Backdrop, vertexPen, c, r, r);
        }
    }

    /// <summary>그리는 중이면 마지막 점에서 마우스까지 점선을 잇는다.</summary>
    private void DrawPreview(DrawingContext dc, Rect frame, MappingRoiEditorViewModel editor)
    {
        if (_hover is not Point at || !editor.CanDraw || editor.Points.Count == 0) return;
        var pen = MakePen(new Pen(ShapeBrush, 1.2) { DashStyle = DashStyles.Dot });
        var from = ToScreen(frame, editor.Points[^1]);
        var to = editor.CanClosePolygon && HitVertex(frame, at) == 0 ? ToScreen(frame, editor.Points[0]) : Clamp(frame, at);
        dc.DrawLine(pen, from, to);
    }

    private void DrawGrid(DrawingContext dc, Rect frame)
    {
        var line = MakePen(new Pen(Faded(GridBrush, 0.35), 1));
        for (int i = 1; i < 10; i++)
        {
            double x = frame.X + frame.Width * i / 10, y = frame.Y + frame.Height * i / 10;
            dc.DrawLine(line, new Point(x, frame.Top), new Point(x, frame.Bottom));
            dc.DrawLine(line, new Point(frame.Left, y), new Point(frame.Right, y));
        }
        dc.DrawRectangle(null, MakePen(new Pen(GridBrush, 1)), frame);
    }

    private static Geometry Polygon(Rect frame, System.Collections.Generic.IReadOnlyList<MappingRoiPoint> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToScreen(frame, points[0]), closed, closed);
            for (int i = 1; i < points.Count; i++) ctx.LineTo(ToScreen(frame, points[i]), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point Clamp(Rect frame, Point p)
        => new(Math.Min(Math.Max(p.X, frame.Left), frame.Right), Math.Min(Math.Max(p.Y, frame.Top), frame.Bottom));

    private static Brush Faded(Brush brush, double opacity)
    {
        var copy = brush.CloneCurrentValue();
        copy.Opacity = opacity;
        copy.Freeze();
        return copy;
    }

    /// <summary>
    /// 펜의 붓을 복제본으로 바꿔 얼린다 — 리소스 사전의 붓을 그대로 얼리면 그 토큰이 굳어 테마 전환이 막힌다.
    /// </summary>
    private static Pen MakePen(Pen pen)
    {
        if (pen.Brush is { IsFrozen: false } brush) pen.Brush = brush.CloneCurrentValue();
        pen.Freeze();
        return pen;
    }
    #endregion
}
