using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 둥근 틀(<see cref="FrameBorder"/>) — 테두리가 자식 위에 그려지고, 자식은 안쪽 둥근 모양으로 잘리며, 배치는 <see cref="Border"/> 와 같다
/// (2026-10-01 "창 레이아웃의 모서리가 지워진 것 같다").
/// </summary>
[Collection(WpfFocusCollection.Name)]
public class FrameBorderTests
{
    // ─────────────── 기하(순수 함수) ───────────────

    [Fact]
    public void should_draw_the_stroke_half_a_thickness_inside_with_the_border_radius_when_the_path_is_built()
    {
        var (rect, radius) = FrameBorderGeometry.StrokePath(new Size(400, 200), 1, 10);

        Assert.Equal(new Rect(0.5, 0.5, 399, 199), rect);
        Assert.Equal(10, radius);   // Border 의 고른 테두리와 같은 모양
    }

    [Fact]
    public void should_clip_the_child_to_the_inner_radius_when_a_radius_is_given()
    {
        var clip = (RectangleGeometry)FrameBorderGeometry.ChildClip(new Size(398, 198), 10, 1, new Thickness(0))!;

        Assert.Equal(new Rect(0, 0, 398, 198), clip.Rect);
        Assert.Equal(9.5, clip.RadiusX);   // 테두리 안쪽선(10 − 0.5)
    }

    [Theory]
    [InlineData(6, 1, 6, 0)]       // 안쪽 여백이 반지름을 다 먹었다 — 자를 필요 없다
    [InlineData(0, 1, 0, 0)]       // 네모 틀
    public void should_not_clip_the_child_when_the_inner_radius_is_gone(double radius, double thickness, double padding, int _)
        => Assert.Null(FrameBorderGeometry.ChildClip(new Size(100, 100), radius, thickness, new Thickness(padding)));

    [Fact]
    public void should_not_clip_when_the_child_size_is_unknown()
        => Assert.Null(FrameBorderGeometry.ChildClip(new Size(0, 50), 10, 1, new Thickness(0)));

    [Fact]
    public void should_cap_the_radius_at_half_the_short_side_when_the_frame_is_small()
        => Assert.Equal(10, FrameBorderGeometry.OuterRadius(new Size(20, 80), 50));

    // ─────────────── 실제 그리기 ───────────────

    [Fact]
    public void should_draw_the_stroke_over_a_square_child_at_every_corner()
    {
        // 네모난 자식이 틀을 가득 채워도(콘솔 바탕 · 머리 띠) 네 모서리 호 위에 테두리색이 남는다.
        var corners = OnSta(() =>
        {
            var frame = new FrameBorder
            {
                Width = 200,
                Height = 120,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                Child = new Border { Background = Brushes.Blue },
            };
            var root = new Grid { Width = 240, Height = 160, Background = Brushes.Black, Children = { frame } };
            var window = Show(root);
            try { return ArcPixels(root, frame, 10); }
            finally { window.Close(); }
        });

        foreach (var c in corners)
            Assert.True(c.R > 200 && c.B < 120, $"모서리 호 위 픽셀 {c} — 테두리(빨강)가 자식(파랑)에 덮였다");
    }

    [Fact]
    public void should_keep_the_child_inside_the_rounded_corner_when_it_is_square()
    {
        // 호 바깥(모서리 꼭짓점 바로 안쪽 1px) — 예전에는 네모난 자식의 파랑이 비쳤다.
        var tip = OnSta(() =>
        {
            var frame = new FrameBorder
            {
                Width = 200, Height = 120, BorderBrush = Brushes.Red, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Child = new Border { Background = Brushes.Blue },
            };
            var root = new Grid { Width = 240, Height = 160, Background = Brushes.Black, Children = { frame } };
            var window = Show(root);
            try { return Pixel(root, frame, new Point(2, 2)); }
            finally { window.Close(); }
        });

        Assert.True(tip.B < 40, $"모서리 꼭짓점 안쪽 {tip} — 자식이 둥근 모서리 밖으로 비친다");
    }

    [Fact]
    public void should_lay_out_the_child_exactly_like_a_border_when_the_frame_replaces_it()
    {
        var (frameChild, borderChild) = OnSta(() =>
        {
            Rect Place(Border host)
            {
                var child = new Border();
                host.Width = 300; host.Height = 200; host.BorderThickness = new Thickness(1); host.Padding = new Thickness(4, 2, 4, 2);
                host.CornerRadius = new CornerRadius(8); host.Child = child;
                var window = Show(new Grid { Children = { host } });
                try { return child.TransformToAncestor(host).TransformBounds(new Rect(child.RenderSize)); }
                finally { window.Close(); }
            }
            return (Place(new FrameBorder()), Place(new Border()));
        });

        Assert.Equal(borderChild, frameChild);
    }

    [Fact]
    public void should_draw_like_a_border_when_the_thickness_is_not_uniform()
    {
        // 고르지 않은 두께(머리 띠의 아래 선 같은 것)는 Border 그리기 그대로 — 위에 덧그리지 않는다.
        var bottom = OnSta(() =>
        {
            var frame = new FrameBorder
            {
                Width = 200, Height = 40, BorderBrush = Brushes.Red, BorderThickness = new Thickness(0, 0, 0, 1),
                Background = Brushes.White,
            };
            var root = new Grid { Width = 200, Height = 40, Children = { frame } };
            var window = Show(root);
            try { return (Pixel(root, frame, new Point(100, 39.5)), Pixel(root, frame, new Point(100, 0.5))); }
            finally { window.Close(); }
        });

        Assert.True(bottom.Item1.R > 200 && bottom.Item1.G < 60, $"아래 선 {bottom.Item1}");
        Assert.Equal(Colors.White, bottom.Item2);
    }

    #region - Fixtures -
    private static Window Show(FrameworkElement root)
    {
        var window = new Window
        {
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            UseLayoutRounding = true,
        };
        window.Show();
        Pump(DispatcherPriority.ContextIdle);
        return window;
    }

    /// <summary>네 모서리 호의 45° 지점 근처(±1px)에서 가장 빨간 픽셀.</summary>
    private static Color[] ArcPixels(FrameworkElement root, FrameworkElement frame, double radius)
    {
        var box = frame.TransformToAncestor(root).TransformBounds(new Rect(frame.RenderSize));
        var off = (radius * (1 - Math.Sqrt(0.5))) + 0.35;
        var points = new[]
        {
            new Point(box.Left + off, box.Top + off), new Point(box.Right - off, box.Top + off),
            new Point(box.Left + off, box.Bottom - off), new Point(box.Right - off, box.Bottom - off),
        };
        var bitmap = Render(root);
        return points.Select(p =>
        {
            var best = Colors.Black;
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    var c = At(bitmap, (int)p.X + dx, (int)p.Y + dy);
                    if (c.R - c.B > best.R - best.B) best = c;
                }
            return best;
        }).ToArray();
    }

    private static Color Pixel(FrameworkElement root, FrameworkElement frame, Point local)
    {
        var p = frame.TranslatePoint(local, root);
        return At(Render(root), (int)p.X, (int)p.Y);
    }

    private static BitmapSource Render(FrameworkElement root)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }

    private static Color At(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(Math.Clamp(x, 0, bitmap.PixelWidth - 1), Math.Clamp(y, 0, bitmap.PixelHeight - 1), 1, 1), pixel, 4, 0);
        return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
    }

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("STA body failed", failure);
        return result;
    }
    #endregion
}
