using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>
/// 그림 좌표 ↔ 세계(m) — <b>순수</b>(sensor-mount-preset FR-08 · FR-10). 그림마다 두 축: 앞 = (가로, 높이) · 위 = (가로, 펜스와 거리 · 밖이 위) · 옆 = (펜스와 거리 · 밖이 왼쪽, 높이).
/// 3D 미리보기(<paramref name="oblique"/>)는 앞 그림에 깊이를 비스듬히 더한 투영(결선맵 2.5D 와 같은 방식 · 편집 없음).
/// </summary>
public readonly record struct MountBayProjection(MountView View, double Width, double Height, FenceSampleBay Bay, bool Oblique = false)
{
    public const double PAD = 28;

    /// <summary>그림에 담을 세계 범위 — 가로(m) · 세로(m).</summary>
    private (double U0, double U1, double V0, double V1) Range => View switch
    {
        MountView.Top => (-0.4, Bay.PanelWidthM + 0.4, -1.6, 1.6),
        MountView.Side => (-1.6, 1.6, -0.6, Bay.TopLimitM + 0.3),
        _ => (-0.4, Bay.PanelWidthM + 0.4, -0.6, Bay.TopLimitM + 0.3),
    };

    /// <summary>1 m 가 몇 화소인가(가로 · 세로 같은 배율).</summary>
    public double Scale
    {
        get
        {
            var (u0, u1, v0, v1) = Range;
            return Math.Max(1e-3, Math.Min((Width - 2 * PAD) / (u1 - u0), (Height - 2 * PAD) / (v1 - v0)));
        }
    }

    /// <summary>세계(가로 · 높이 · 펜스와 거리) → 그림 점.</summary>
    public Point ToScreen(double alongM, double heightM, double depthM)
    {
        var (u0, _, v0, v1) = Range;
        var s = Scale;
        var (u, v) = View switch
        {
            MountView.Top => (alongM, depthM),                        // 밖(−)이 위
            MountView.Side => (depthM, heightM),                      // 밖(−)이 왼쪽
            _ => (alongM, heightM),
        };
        var x = PAD + (u - u0) * s;
        var y = View == MountView.Top ? PAD + (v + 1.6) * s : Height - PAD - (v - v0) * s;
        if (Oblique && View == MountView.Front) { x += depthM * s * 0.45; y -= depthM * s * 0.30; }
        return new Point(x, y);
    }

    /// <summary>그림 점 → 그 그림의 두 축(m) — 앞 (가로, 높이) · 위 (가로, 펜스와 거리) · 옆 (펜스와 거리, 높이).</summary>
    public (double U, double V) FromScreen(Point p)
    {
        var (u0, _, v0, _) = Range;
        var s = Scale;
        var u = (p.X - PAD) / s + u0;
        return View switch
        {
            MountView.Top => (u, (p.Y - PAD) / s - 1.6),
            MountView.Side => (u, (Height - PAD - p.Y) / s + v0),
            _ => (u, (Height - PAD - p.Y) / s + v0),
        };
    }
}

/// <summary>
/// 담장 한 칸 그림(W2) — 위 · 앞 · 옆 · 3D 미리보기 중 하나. 붙는 점(회색 점) · 붙을 점(빨강 + 고리) · 흐린 미리보기 · 원래 자리(옅게) · 방향 손잡이.
/// 끌기는 캡처 방식(데드존 8 · 아이콘을 잡아야 시작 · Esc 원위치 · Alt 자유 위치 · drag-first-ux), 키보드는 Tab · 화살표.
/// </summary>
public sealed class MountBayCanvas : FrameworkElement
{
    private const double DEADZONE = 8.0;

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(MountPresetEditorViewModel), typeof(MountBayCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEditorChanged));

    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
        nameof(View), typeof(MountView), typeof(MountBayCanvas), new FrameworkPropertyMetadata(MountView.Front, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPreview3DProperty = DependencyProperty.Register(
        nameof(IsPreview3D), typeof(bool), typeof(MountBayCanvas), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public MountPresetEditorViewModel? Editor { get => (MountPresetEditorViewModel?)GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public MountView View { get => (MountView)GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <summary>3D 미리보기(편집 없음 · 결선맵과 같은 계산).</summary>
    public bool IsPreview3D { get => (bool)GetValue(IsPreview3DProperty); set => SetValue(IsPreview3DProperty, value); }

    private Point? _pressed;
    private bool _dragging;
    private bool _rotating;

    public MountBayCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (MountBayCanvas)d;
        if (e.OldValue is MountPresetEditorViewModel old) old.PropertyChanged -= canvas.OnEditorPropertyChanged;
        if (e.NewValue is MountPresetEditorViewModel now) now.PropertyChanged += canvas.OnEditorPropertyChanged;
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    private MountBayProjection Projection(MountPresetEditorViewModel vm)
        => new(IsPreview3D ? MountView.Front : View, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight), vm.Bay, IsPreview3D);

    private Brush Res(string key, Color fallback) => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    #region - Render -
    protected override void OnRender(DrawingContext dc)
    {
        if (Editor is not { } vm) return;
        var p = Projection(vm);
        var bay = vm.Bay;
        var ink = Res("TextSecondaryBrush", Colors.Gray);
        var post = Res("TextTertiaryBrush", Colors.DimGray);
        var accent = Res("PrimaryBrush", Colors.SteelBlue);
        var ground = new Pen(Res("BorderBrush", Colors.Gray), 1);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // 땅 · 펜스 선
        if (View != MountView.Top || IsPreview3D)
            dc.DrawLine(ground, p.ToScreen(View == MountView.Side ? 0 : -0.4, 0, View == MountView.Side ? 1.6 : 0), p.ToScreen(View == MountView.Side ? 0 : bay.PanelWidthM + 0.4, 0, View == MountView.Side ? -1.6 : 0));
        var postPen = new Pen(post, 4);
        var railPen = new Pen(post, 2);
        switch (IsPreview3D ? MountView.Front : View)
        {
            case MountView.Front:
                if (!bay.IsWall)
                {
                    dc.DrawLine(postPen, p.ToScreen(0, 0, 0), p.ToScreen(0, bay.PostTopM, 0));
                    dc.DrawLine(postPen, p.ToScreen(bay.PanelWidthM, 0, 0), p.ToScreen(bay.PanelWidthM, bay.PostTopM, 0));
                    dc.DrawLine(railPen, p.ToScreen(0, bay.FenceHeightM, 0), p.ToScreen(bay.PanelWidthM, bay.FenceHeightM, 0));
                    var mesh = new Pen(ink, 0.5) { DashStyle = DashStyles.Dot };
                    for (var x = 0.25; x < bay.PanelWidthM; x += 0.25) dc.DrawLine(mesh, p.ToScreen(x, 0.05, 0), p.ToScreen(x, bay.FenceHeightM, 0));
                }
                else dc.DrawRectangle(null, railPen, new Rect(p.ToScreen(0, bay.FenceHeightM, 0), p.ToScreen(bay.PanelWidthM, 0, 0)));
                if (bay.HasRazor)
                {
                    var coil = new Pen(ink, 1);
                    var r = bay.CoilRadiusM * p.Scale;
                    for (var x = r * 0.6 / p.Scale; x < bay.PanelWidthM; x += bay.CoilRadiusM * 0.7)
                        dc.DrawEllipse(null, coil, p.ToScreen(x, bay.CoilCenterM, 0), r * 0.45, r);
                }
                break;
            case MountView.Top:
                dc.DrawLine(railPen, p.ToScreen(0, 0, 0), p.ToScreen(bay.PanelWidthM, 0, 0));
                if (!bay.IsWall)
                {
                    dc.DrawRectangle(post, null, Centered(p.ToScreen(0, 0, 0), 8));
                    dc.DrawRectangle(post, null, Centered(p.ToScreen(bay.PanelWidthM, 0, 0), 8));
                }
                Label(dc, "밖", p.ToScreen(-0.3, 0, -1.4), ink);
                Label(dc, "안", p.ToScreen(-0.3, 0, 1.3), ink);
                break;
            default:
                dc.DrawLine(postPen, p.ToScreen(0, 0, 0), p.ToScreen(0, bay.IsWall ? bay.FenceHeightM : bay.PostTopM, 0));
                if (bay.HasRazor) dc.DrawEllipse(null, new Pen(ink, 1), p.ToScreen(0, bay.CoilCenterM, 0), bay.CoilRadiusM * p.Scale, bay.CoilRadiusM * p.Scale);
                Label(dc, "밖", p.ToScreen(0, bay.TopLimitM + 0.1, -1.4), ink);
                Label(dc, "안", p.ToScreen(0, bay.TopLimitM + 0.1, 1.3), ink);
                break;
        }

        // 붙는 점 — 허용 = 반투명 회색(다크 흰) 원, 허용 안 됨 = 흐린 작은 점, 붙을 점 = 반투명 빨강 + 고리(모양으로도 구분)
        if (!IsPreview3D)
        {
            var dark = (Res("BgBrush", Colors.White) as SolidColorBrush)?.Color is { } bg && bg.R + bg.G + bg.B < 300;
            var snap = new SolidColorBrush(dark ? Color.FromArgb(82, 255, 255, 255) : Color.FromArgb(97, 94, 107, 121));
            var snapLine = new Pen(new SolidColorBrush(dark ? Color.FromArgb(128, 255, 255, 255) : Color.FromArgb(140, 94, 107, 121)), 0.8);
            var target = new SolidColorBrush(dark ? Color.FromArgb(115, 255, 90, 82) : Color.FromArgb(107, 214, 40, 40));
            var ring = new Pen(new SolidColorBrush(dark ? Color.FromArgb(230, 255, 90, 82) : Color.FromArgb(217, 214, 40, 40)), 2);
            foreach (var item in vm.SnapPoints)
            {
                var c = p.ToScreen(item.Point.AlongRatio * bay.PanelWidthM, item.Point.HeightM, item.Point.DepthM);
                if (item.IsTarget && vm.IsDragging) { dc.DrawEllipse(target, ring, c, 12, 12); continue; }
                if (item.IsAllowed) dc.DrawEllipse(snap, snapLine, c, 5.5, 5.5);
                else dc.DrawEllipse(null, new Pen(snap, 1), c, 2.5, 2.5);
            }
        }

        // 원래 자리(옅게) · 센서(끄는 중이면 흐린 미리보기)
        var body = Res("StatusSuccessBrush", Color.FromRgb(62, 127, 92));
        if (vm.OriginalPose is { } o) DrawSensor(dc, p, vm, o, body, 0.25, accent, false);
        DrawSensor(dc, p, vm, vm.Pose, body, vm.IsDragging ? 0.55 : 1.0, accent, !IsPreview3D);

        if (vm.AttachText is { Length: > 0 } attach && !IsPreview3D) Label(dc, attach, new Point(8, 6), accent);
    }

    /// <summary>센서 아이콘 — 모델 실제 치수(과장 3배)로, 모양마다 다르게. 고른 센서 = 테두리 + 손잡이(모양).</summary>
    private void DrawSensor(DrawingContext dc, MountBayProjection p, MountPresetEditorViewModel vm, SensorMountPose pose, Brush body, double opacity, Brush accent, bool handles)
    {
        var c = p.ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
        var size = vm.Model.SizeMm;
        var view = IsPreview3D ? MountView.Front : View;
        var w = Math.Clamp((view == MountView.Side ? size.L : size.W) / 1000 * p.Scale * 3, 8, 60);
        var h = Math.Clamp((view == MountView.Top ? size.L : size.H) / 1000 * p.Scale * 3, 8, 60);
        dc.PushOpacity(opacity);
        switch (vm.Model.Shape)
        {
            case SensorShape.Pir:
                dc.DrawEllipse(body, null, c, w / 2, w / 2);
                break;
            case SensorShape.Optical:
                dc.DrawLine(new Pen(body, 4), new Point(c.X - 40, c.Y), new Point(c.X + 40, c.Y));
                break;
            case SensorShape.Underground:
                dc.DrawRectangle(body, null, new Rect(c.X - 3, c.Y - 4, 6, Math.Max(18, h)));
                break;
            default:
                dc.DrawRoundedRectangle(body, null, new Rect(c.X - w / 2, c.Y - h / 2, w, h), 3, 3);
                if (vm.Model.Shape is SensorShape.Smart or SensorShape.Smart2)
                    dc.DrawRectangle(body, null, new Rect(c.X - w / 2 - 3, c.Y - h / 2 - 4, w + 6, 4));     // 후드
                break;
        }
        dc.Pop();
        if (!handles) return;
        dc.DrawRectangle(null, new Pen(accent, 2), new Rect(c.X - w / 2 - 5, c.Y - h / 2 - 5, w + 10, h + 10));
        if (View == MountView.Top)
        {
            var end = HandleEnd(p, pose);
            dc.DrawLine(new Pen(accent, 2.2), c, end);
            dc.DrawEllipse(accent, null, end, 5, 5);
            Label(dc, $"{pose.YawDeg:0}°", new Point(end.X + 6, end.Y - 8), accent);
        }
        else if (View == MountView.Side)
        {
            var end = TiltEnd(p, pose);
            dc.DrawLine(new Pen(accent, 2.2), c, end);
            dc.DrawEllipse(accent, null, end, 4.5, 4.5);
        }
    }

    private static Point HandleEnd(MountBayProjection p, SensorMountPose pose)
    {
        var c = p.ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
        var rad = pose.YawDeg * Math.PI / 180;
        return new Point(c.X + Math.Cos(rad) * 46, c.Y - Math.Sin(rad) * 46);          // 바깥쪽(+90°)이 위
    }

    private static Point TiltEnd(MountBayProjection p, SensorMountPose pose)
    {
        var c = p.ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
        var rad = pose.TiltDeg * Math.PI / 180;
        var outward = pose.YawDeg >= 0 ? -1 : 1;                                           // 밖을 보면 왼쪽으로
        return new Point(c.X + outward * Math.Cos(rad) * 40, c.Y - Math.Sin(rad) * 40);
    }

    private static Rect Centered(Point c, double size) => new(c.X - size / 2, c.Y - size / 2, size, size);

    private void Label(DrawingContext dc, string text, Point at, Brush brush)
        => dc.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Malgun Gothic"), 11, brush,
                                         VisualTreeHelper.GetDpi(this).PixelsPerDip), at);
    #endregion

    #region - Mouse (캡처 드래그 · 데드존 8 · 아이콘/손잡이에서만 시작) -
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (e.ChangedButton != MouseButton.Left || Editor is not { } vm || IsPreview3D) return;
        Focus();
        var p = Projection(vm);
        var at = e.GetPosition(this);
        var pose = vm.Pose;
        var c = p.ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
        var handle = View == MountView.Top ? HandleEnd(p, pose) : View == MountView.Side ? TiltEnd(p, pose) : (Point?)null;
        if (handle is { } h && (h - at).Length <= 9) { _rotating = true; CaptureMouse(); e.Handled = true; return; }
        if ((c - at).Length <= 22) { _pressed = at; CaptureMouse(); e.Handled = true; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Editor is not { } vm) return;
        var p = Projection(vm);
        var at = e.GetPosition(this);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (_rotating)
        {
            var pose = vm.Pose;
            var c = p.ToScreen(pose.AlongM, pose.HeightM, pose.DepthM);
            if (View == MountView.Top) vm.RotateHandle(at.X - c.X, c.Y - at.Y, alt);
            else vm.TiltHandle(c.X - at.X, c.Y - at.Y, alt);
            return;
        }
        if (_pressed is not { } start) return;
        if (!_dragging)
        {
            var d = at - start;
            if (d.X * d.X + d.Y * d.Y < DEADZONE * DEADZONE) return;
            _dragging = true;
            vm.BeginDrag();
        }
        var (u, v) = p.FromScreen(at);
        vm.DragTo(View, u, v, alt);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        FinishDrag(commit: true);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        FinishDrag(commit: true);
    }

    /// <summary>끝맺음 하나(drag-first-ux) — ①플래그 ②시각 ③캡처 해제 ④확정 통지.</summary>
    private void FinishDrag(bool commit)
    {
        var wasDragging = _dragging;
        _pressed = null;
        _dragging = false;
        _rotating = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) Editor?.FinishDrag(commit);
    }
    #endregion

    #region - Keyboard (Tab · 화살표 · Esc) -
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (Editor is not { } vm || IsPreview3D) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (key)
        {
            case Key.Escape:
                if (_dragging) { _dragging = false; _pressed = null; if (IsMouseCaptured) ReleaseMouseCapture(); vm.CancelDrag(); e.Handled = true; }
                break;
            case Key.Tab:
                vm.NextSnapPoint(shift);
                e.Handled = true;
                break;
            case Key.Left: vm.Nudge(-1, 0, shift); e.Handled = true; break;
            case Key.Right: vm.Nudge(1, 0, shift); e.Handled = true; break;
            case Key.Up: vm.Nudge(0, 1, shift); e.Handled = true; break;
            case Key.Down: vm.Nudge(0, -1, shift); e.Handled = true; break;
        }
    }
    #endregion
}
