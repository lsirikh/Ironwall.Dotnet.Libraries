using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 세 칸 바닥 띠(레일 바닥 · 목록 상태 줄 · 상세 적용 막대)의 윗선이 한 줄에 서는가 — 커널이 <b>강제</b>한다.
/// 2026-09-30 GIS 실창 020(서버 콘솔): 레일 · 상태 띠가 내용대로 128 까지 자라고 상세 막대는 53 이라 윗선이 75px 어긋났다.
/// 예전 규칙은 "띠마다 최소 53" 뿐이었다. 이제 셸이 보이는 띠 내용 중 가장 큰 높이를 모아 세 띠에 똑같이 건다.
/// 실제 커널 템플릿(Utils/Themes/Generic.xaml, ThemeInfo 로 풀림)을 화면 밖 창에 띄워 잰다 — 띠 등록이 Loaded 에서 일어나기 때문이다.
/// </summary>
[Collection(WpfFocusCollection.Name)]   // 실제 창(화면 밖)을 띄운다 — Application 을 만들지 않는다(커널 스타일은 테마 사전에서 풀린다)
public class ConsoleFooterBandAlignmentTests
{
    private const double Width = 1400, Height = 700;

    [Theory]
    [InlineData(20, 20)]      // 셋 다 53 보다 낮다 → 53
    [InlineData(70, 20)]      // 레일 바닥이 크다(두 줄 안) → 세 띠 모두 레일 높이로
    [InlineData(20, 60)]      // 상태 줄이 크다(계정 콘솔 건수 + 권한 그룹 칩 줄, 실측 60) → 세 띠 모두 상태 줄 높이로
    [InlineData(20, 106)]     // 두 줄 경계 — 아직 띠다
    public void should_align_the_three_band_tops_when_band_contents_differ_in_height(double railContent, double statusContent)
    {
        var (tops, heights, expected) = OnSta(() =>
        {
            var (window, shell, rail, status) = Show(railContent, statusContent);
            try
            {
                var bands = shell.FooterBands.Where(b => b.IsVisible).ToList();
                return (bands.Select(b => Top(b, shell)).ToList(),
                        bands.Select(b => b.ActualHeight).ToList(),
                        Expected(rail, status));
            }
            finally { window.Close(); }
        });

        Assert.Equal(3, tops.Count);                                   // 레일 바닥 · 상태 줄 · 상세 막대
        Assert.True(tops.Max() - tops.Min() <= 1, $"윗선 {string.Join(" / ", tops)}");
        Assert.All(heights, h => Assert.InRange(h, expected - 1, expected + 1));
    }

    [Fact]
    public void should_shrink_back_to_the_floor_when_the_tall_band_content_shrinks()
    {
        // 한 번 자란 높이에 갇히지 않는다 — 띠 자신이 아니라 띠 <b>내용</b>의 원하는 높이로 재기 때문이다.
        var (grown, shrunk) = OnSta(() =>
        {
            var (window, shell, rail, _) = Show(70, 20);
            try
            {
                var before = ConsoleShell.GetFooterBandHeight(shell);
                rail.Height = 20;
                Pump();
                return (before, ConsoleShell.GetFooterBandHeight(shell));
            }
            finally { window.Close(); }
        });

        Assert.True(grown > ConsoleLayoutMath.FooterBandHeight);
        Assert.Equal(ConsoleLayoutMath.FooterBandHeight, shrunk);
    }

    [Fact]
    public void should_leave_a_working_panel_alone_when_a_band_content_is_taller_than_two_bands()
    {
        // 이벤트 콘솔 조치 트레이(상태 줄 약 220)를 따라 레일 바닥 · 상세 막대가 빈 판으로 자라지 않는다 — 그 띠만 제 높이로 선다.
        var (heights, statusHeight) = OnSta(() =>
        {
            var (window, shell, _, status) = Show(20, 220);
            try
            {
                var statusBand = shell.FooterBands.Single(b => b is ContentPresenter);
                return (shell.FooterBands.Where(b => b is not ContentPresenter).Select(b => b.ActualHeight).ToList(), statusBand.ActualHeight);
            }
            finally { window.Close(); }
        });

        Assert.All(heights, h => Assert.Equal(ConsoleLayoutMath.FooterBandHeight, h, 0.5));
        Assert.Equal(220, statusHeight, 0.5);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(53, true)]
    [InlineData(106, true)]
    [InlineData(107, false)]
    public void should_treat_only_up_to_two_band_heights_as_a_band_when_deciding_alignment(double content, bool expected)
        => Assert.Equal(expected, ConsoleLayoutMath.IsAlignableFooterBand(content));

    [Fact]
    public void should_not_open_an_empty_status_band_when_the_status_content_is_collapsed()
    {
        // 상태 줄을 접은 화면(계정 세션 설정)은 빈 띠를 두지 않는다 — 공통 높이를 걸어도 그 칸은 0 이어야 한다.
        var (statusHeight, others) = OnSta(() =>
        {
            var (window, shell, _, status) = Show(20, 20);
            try
            {
                status.Visibility = Visibility.Collapsed;
                Pump();
                var statusBand = shell.FooterBands.Single(b => b is ContentPresenter);
                return (statusBand.ActualHeight, shell.FooterBands.Where(b => b is not ContentPresenter).Select(b => b.ActualHeight).ToList());
            }
            finally { window.Close(); }
        });

        Assert.Equal(0, statusHeight);
        Assert.All(others, h => Assert.Equal(ConsoleLayoutMath.FooterBandHeight, h, 0.5));
    }

    [Theory]
    [InlineData(0, 53)]
    [InlineData(52.4, 53)]
    [InlineData(53.2, 54)]
    [InlineData(127.6, 128)]
    [InlineData(double.NaN, 53)]
    public void should_round_up_to_a_whole_pixel_but_never_below_the_floor_when_resolving_the_band(double tallest, double expected)
        => Assert.Equal(expected, ConsoleLayoutMath.AlignedFooterBandHeight(tallest));

    #region - 도우미 -
    private static double Expected(FrameworkElement rail, FrameworkElement status)
        // 레일 FooterHost = 위아래 여백 8+8 + 윗선 1 · 상태 줄 = 내용 그대로 · 상세 막대 = 여백 10+10 + 윗선 1 + 단추(32)
        => ConsoleLayoutMath.AlignedFooterBandHeight(new[] { rail.Height + 17, status.Height, 53 }.Where(ConsoleLayoutMath.IsAlignableFooterBand).Max());

    private static (Window Window, ConsoleShell Shell, FrameworkElement RailContent, FrameworkElement StatusContent) Show(double railContent, double statusContent)
    {
        var rail = new Border { Height = railContent };
        var status = new Border { Height = statusContent };
        var consoleRail = new ConsoleRail { ConsoleKey = "Test", Footer = rail };
        consoleRail.Items.Add(new ConsoleRailEntry("all", "전체", null));
        var shell = new ConsoleShell
        {
            ConsoleKey = "Test",
            Width = Width,
            Height = Height,
            Rail = consoleRail,
            StatusBar = status,
            Detail = new ConsoleDetailHost { ConsoleKey = "Test", FooterText = "선택 대기", ShowButtons = true },
        };
        var window = new Window
        {
            Content = shell,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        Pump();
        Pump();
        if (!window.IsVisible) throw new InvalidOperationException("시험 창이 뜨지 않았다(Application 이 이미 종료 중이면 Show 가 조용히 무시된다)");
        return (window, shell, rail, status);
    }

    private static double Top(FrameworkElement band, FrameworkElement root) => band.TranslatePoint(new Point(0, 0), root).Y;

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
    #endregion
}
