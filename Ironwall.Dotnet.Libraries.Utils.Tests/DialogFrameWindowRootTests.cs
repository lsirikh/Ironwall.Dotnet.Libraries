using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 창 안의 다이얼로그 틀 — 틀이 <b>제 OS 창의 뿌리</b>면 창 제목 줄이 이미 제목 · ✕ 를 그린다. 틀이 머리 · 바깥 바탕 ·
/// 카드 테두리를 또 그리면 한 창에 제목이 두 번, ✕ 가 두 개, 카드 둘레에 어두운 띠가 생긴다(2026-09-30 사용자 보고 "부대 편제 닫기").
/// </summary>
/// <remarks>
/// 실제 창을 화면 밖에 띄운다(<see cref="Window.Show"/>) — 판정은 틀이 창의 <see cref="PresentationSource"/> 에 붙을 때 일어나므로
/// 띄우는 길 그대로 본다. 키보드 초점 · 전경은 프로세스 전역이라 <see cref="WpfFocusCollection"/> 에 묶어 겹쳐 돌지 않게 한다.
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class DialogFrameWindowRootTests
{
    private const string FrameTitle = "부대 편제 닫기";
    private const string Body = "저장하지 않은 편제 변경 3건이 있습니다. 닫으면 변경이 사라집니다.";

    [Fact]
    public void should_hide_the_frame_header_and_backdrop_when_the_frame_is_the_root_of_a_dressed_window()
    {
        var result = OnSta(() =>
        {
            var (window, frame, _) = Show(ResizeMode.NoResize, height: 420);
            var snapshot = (
                Applied: ConsoleWindowChrome.GetIsApplied(window),
                Root: frame.IsWindowRoot,
                TitleVisible: Part(frame, ConsoleDialogFrame.PartTitle)?.IsVisible,
                CloseVisible: Part(frame, ConsoleDialogFrame.PartClose)?.IsVisible,
                Backdrop: Backdrop(frame),
                CardShadow: Descendants<Border>(frame).Any(b => b.Effect is not null),
                WindowTitle: window.Title,
                CaptionTitle: Descendants<TextBlock>(window).Any(t => t.Text == FrameTitle && t.FontSize == 12),
                FooterVisible: Part(frame, ConsoleDialogFrame.PartSecondary)?.IsVisible);
            window.Close();
            return snapshot;
        });

        Assert.True(result.Applied);                    // 창은 커널 겉을 입었다
        Assert.True(result.Root);
        Assert.False(result.TitleVisible);              // 제목은 창 제목 줄 하나뿐
        Assert.False(result.CloseVisible);              // ✕ 도 창 제목 줄 하나뿐
        Assert.True(IsClear(result.Backdrop));          // 카드 둘레의 어두운 바탕이 없다
        Assert.False(result.CardShadow);                // 창 안의 카드 그림자 · 테두리도 없다
        Assert.Equal(FrameTitle, result.WindowTitle);   // 창 제목 = 틀 제목
        Assert.True(result.CaptionTitle);               // 제목 줄에 그 글자가 보인다
        Assert.True(result.FooterVisible);              // 버튼 줄은 그대로
    }

    [Fact]
    public void should_route_the_caption_close_to_the_frame_cancel_exactly_once_when_the_frame_is_the_window_root()
    {
        var result = OnSta(() =>
        {
            var (window, frame, _) = Show(ResizeMode.NoResize, height: 420);
            var cancels = 0;
            var closes = 0;
            frame.SecondaryInvoked += (_, _) => { cancels++; window.Close(); };
            window.Closing += (_, _) => closes++;

            var caption = Descendants<Button>(window).First(b => AutomationProperties.GetAutomationId(b) == ConsoleWindowChrome.CloseAutomationId);
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(caption).GetPattern(PatternInterface.Invoke)).Invoke();
            Pump(DispatcherPriority.ContextIdle);
            return (cancels, closes, window.IsVisible);
        });

        Assert.Equal(1, result.cancels);    // 제목 줄 ✕ = 틀의 취소 길(버튼 · ESC 와 같은 하나)
        Assert.Equal(1, result.closes);     // 두 번 닫히지 않는다
        Assert.False(result.IsVisible);
    }

    [Fact]
    public void should_fit_the_window_height_to_the_content_when_the_frame_is_the_window_root()
    {
        var result = OnSta(() =>
        {
            var (window, frame, body) = Show(ResizeMode.CanResize, height: 420);
            var view = (FrameworkElement)window.Content;
            var footer = Footer(frame);
            var footerTop = footer.TranslatePoint(new Point(0, 0), view).Y;
            var footerBottom = footerTop + footer.ActualHeight;
            var bodyBottom = body.TranslatePoint(new Point(0, body.ActualHeight), view).Y;
            var diag = $"frame {frame.ActualHeight:0.0} body {body.ActualHeight:0.0} footer {footer.ActualHeight:0.0}";
            var snapshot = (WindowHeight: window.ActualHeight, ViewHeight: view.ActualHeight, footerBottom, Band: footerTop - bodyBottom - frame.BodyPadding.Bottom, Diag: diag);
            window.Close();
            return snapshot;
        });

        Assert.True(result.WindowHeight < 400, $"창 높이 {result.WindowHeight:0} — 런처가 준 420 에서 내용에 맞게 줄지 않았다");
        Assert.InRange(result.footerBottom, result.ViewHeight - 2, result.ViewHeight + 2);   // 버튼 줄이 창 바닥에 붙는다
        Assert.True(result.Band > -2 && result.Band < 2, $"본문과 버튼 줄 사이 빈 띠 {result.Band:0.0} — 창 {result.WindowHeight:0.0} view {result.ViewHeight:0.0} {result.Diag}");
    }

    [Fact]
    public void should_keep_the_header_and_backdrop_when_the_frame_sits_in_a_popup_layer()
    {
        var result = OnSta(() =>
        {
            // 호스트 팝업층 — 셸 안의 칸 위에 카드로 앉는다. 창의 뿌리가 아니다.
            var frame = NewFrame(out _);
            frame.Backdrop = Brushes.DarkSlateGray;
            var window = NewWindow(new Grid { Children = { frame } }, ResizeMode.CanResize, 420);
            window.Show();
            Pump(DispatcherPriority.ApplicationIdle);
            var snapshot = (
                Root: frame.IsWindowRoot,
                TitleVisible: Part(frame, ConsoleDialogFrame.PartTitle)?.IsVisible,
                CloseVisible: Part(frame, ConsoleDialogFrame.PartClose)?.IsVisible,
                Backdrop: Backdrop(frame),
                Height: window.ActualHeight);
            window.Close();
            return snapshot;
        });

        Assert.False(result.Root);
        Assert.True(result.TitleVisible);
        Assert.True(result.CloseVisible);
        Assert.Equal(Colors.DarkSlateGray, (result.Backdrop as SolidColorBrush)?.Color);
        Assert.Equal(420, result.Height, 0);      // 남의 창 크기를 건드리지 않는다
    }

    [Fact]
    public void should_keep_the_header_when_the_window_has_no_caption_of_its_own()
    {
        var result = OnSta(() =>
        {
            var frame = NewFrame(out _);
            var window = NewWindow(new UserControl { Content = frame }, ResizeMode.NoResize, 420);
            window.WindowStyle = WindowStyle.None;     // 제목 줄 없는 창 — 틀의 머리가 유일한 제목 · ✕ 다
            window.Show();
            Pump(DispatcherPriority.ApplicationIdle);
            var snapshot = (Title: Part(frame, ConsoleDialogFrame.PartTitle)?.IsVisible, Close: Part(frame, ConsoleDialogFrame.PartClose)?.IsVisible);
            window.Close();
            return snapshot;
        });

        Assert.True(result.Title);
        Assert.True(result.Close);
    }

    [Fact]
    public void should_keep_the_kind_line_as_the_first_body_line_when_the_frame_is_the_window_root()
    {
        var result = OnSta(() =>
        {
            var frame = NewFrame(out _);
            frame.Kind = "제1중대 · 편제 3건";
            var window = NewWindow(new UserControl { Content = frame }, ResizeMode.NoResize, 420);
            window.Show();
            Pump(DispatcherPriority.ApplicationIdle);
            var kind = Descendants<ConsoleDialogText>(frame).FirstOrDefault(t => t.Text == frame.Kind);
            var snapshot = (Kind: kind?.IsVisible, Title: Part(frame, ConsoleDialogFrame.PartTitle)?.IsVisible);
            window.Close();
            return snapshot;
        });

        Assert.True(result.Kind);        // 무엇에 대한 창인지는 남는다
        Assert.False(result.Title);
    }

    [Theory]
    [InlineData(DialogSize.Small, 111.4, 34, 1000, 146)]     // 내용만큼 — 올림
    [InlineData(DialogSize.Small, 900, 34, 1000, 594)]       // 규격 상한(S 560)에서 멈추고 몸통이 스크롤한다
    [InlineData(DialogSize.Large, 900, 34, 700, 700)]        // 작업 영역을 넘지 않는다
    public void should_fit_the_window_to_the_frame_within_the_limits_when_the_heights_are_known(DialogSize size, double desired, double chrome, double workArea, double expected)
        => Assert.Equal(expected, DialogSizeRules.FitWindowHeight(size, desired, chrome, workArea));

    [Theory]
    [InlineData(0, 34, 1000)]
    [InlineData(double.NaN, 34, 1000)]
    [InlineData(120, -1, 1000)]
    [InlineData(120, 34, double.PositiveInfinity)]
    public void should_leave_the_window_alone_when_a_height_is_unusable(double desired, double chrome, double workArea)
        => Assert.Null(DialogSizeRules.FitWindowHeight(DialogSize.Small, desired, chrome, workArea));

    #region - Fixtures -
    private static (Window Window, ConsoleDialogFrame Frame, FrameworkElement Body) Show(ResizeMode mode, double height)
    {
        var frame = NewFrame(out var body);
        var window = NewWindow(new UserControl { Content = frame }, mode, height);
        window.Title = "창 제목(런처)";
        window.Show();
        Pump(DispatcherPriority.ApplicationIdle);
        Pump(DispatcherPriority.ContextIdle);
        return (window, frame, body);
    }

    private static ConsoleDialogFrame NewFrame(out FrameworkElement body)
    {
        body = new TextBlock { Text = Body, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top };
        return new ConsoleDialogFrame
        {
            Title = FrameTitle,
            Size = DialogSize.Small,
            DialogKey = "Test.WindowRoot",
            PrimaryText = "닫기",
            SecondaryText = "취소",
            Backdrop = Brushes.Black,
            Content = body,
        };
    }

    private static Window NewWindow(object content, ResizeMode mode, double height) => new()
    {
        Title = "창",
        Content = content,
        Width = DialogSizeRules.WindowWidth(DialogSize.Small),
        Height = height,
        SizeToContent = SizeToContent.Manual,
        ResizeMode = mode,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -20000,
        Top = -20000,
        ShowInTaskbar = false,
        ShowActivated = false,
    };

    private static FrameworkElement? Part(ConsoleDialogFrame frame, string name) => frame.Template?.FindName(name, frame) as FrameworkElement;

    private static Brush? Backdrop(ConsoleDialogFrame frame)
        => VisualTreeHelper.GetChildrenCount(frame) > 0 ? (VisualTreeHelper.GetChild(frame, 0) as Panel)?.Background : null;

    private static bool IsClear(Brush? brush) => brush is null || brush is SolidColorBrush { Color.A: 0 };

    /// <summary>버튼 줄 — 보조 버튼을 품은, 위쪽 구분선 하나짜리 테두리.</summary>
    private static Border Footer(ConsoleDialogFrame frame)
    {
        DependencyObject? current = Part(frame, ConsoleDialogFrame.PartSecondary);
        while (current is not null && !(current is Border { BorderThickness: { Top: 1, Bottom: 0, Left: 0, Right: 0 } }))
            current = VisualTreeHelper.GetParent(current);
        return (Border)current!;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
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
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result;
    }
    #endregion
}
