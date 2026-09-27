using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// B2 — 콘솔 OS 창의 겉(<see cref="ConsoleWindowChrome"/>). 커널 틀이 <b>제 창의 뿌리일 때만</b> 그 창에 토큰 제목 줄을 입히고,
/// 닫기는 UIA 로 누를 수 있으며 OS 닫기 경로(<c>Closing</c>)를 그대로 지나고, 두 테마의 토큰을 다시 찾아 칠한다.
/// </summary>
/// <remarks>
/// <para>창을 <b>띄우지 않고</b> 판정한다. 이 시험 어셈블리는 다른 반이 <see cref="Application"/> 을 저마다의 STA 스레드에 만들고
/// 그 스레드가 끝나 버린다 — 그 뒤 다른 스레드의 <see cref="Window.Show"/> 는 창 원본(<see cref="PresentationSource"/>)을 만들지
/// 않는다(2026-09-27 실측: 핸들은 생겨도 RootVisual 이 붙지 않는다). 그래서 창이 뜰 때 <see cref="ConsoleWindowChrome.Enlist"/> 가
/// 부르는 판정(<see cref="ConsoleWindowChrome.TryDress"/>)을 직접 부르고, 커널 틀이 스스로 등록하는지는
/// <see cref="ConsoleWindowChrome.IsEnlisted"/> 로 본다. 템플릿은 <see cref="FrameworkElement.ApplyTemplate"/> + 측정으로 펼친다.</para>
/// </remarks>
[Collection(WpfApplicationCollection.Name)]
public class ConsoleWindowChromeTests
{
    private const double Width = 480;
    private const double Height = 300;

    #region - Pure rules -
    [Theory]
    [InlineData(ResizeMode.NoResize, false, false)]
    [InlineData(ResizeMode.CanMinimize, true, false)]
    [InlineData(ResizeMode.CanResize, true, true)]
    [InlineData(ResizeMode.CanResizeWithGrip, true, true)]
    public void should_offer_the_same_caption_buttons_as_the_os_when_the_resize_mode_is_given(ResizeMode mode, bool minimize, bool maximize)
    {
        // Act
        var buttons = ConsoleWindowChrome.CaptionButtons(mode);

        // Assert
        Assert.Equal(minimize, buttons.Minimize);
        Assert.Equal(maximize, buttons.Maximize);
    }

    [Theory]
    [InlineData(1264, 1280, 1920, 16)]
    [InlineData(1278, 1280, 1920, 2)]     // 커널 겉: 창 테두리 1+1 만 먹는다
    [InlineData(1280, 1296, 1920, 0)]
    [InlineData(1000, 1016, 1920, 0)]
    public void should_widen_only_by_the_border_share_when_the_console_misses_the_docking_width(double console, double window, double workArea, double expected)
        => Assert.Equal(expected, ConsoleWindowChrome.DockingDeficit(console, window, workArea));
    #endregion

    #region - Where it applies -
    [Fact]
    public void should_enlist_itself_when_a_kernel_frame_or_shell_is_created()
    {
        var result = OnSta(() => (
            Frame: ConsoleWindowChrome.IsEnlisted(new ConsoleDialogFrame()),
            Shell: ConsoleWindowChrome.IsEnlisted(new ConsoleShell()),
            PlainView: ConsoleWindowChrome.IsEnlisted(new UserControl())));

        Assert.True(result.Frame);
        Assert.True(result.Shell);
        Assert.False(result.PlainView);     // 평범한 뷰는 켜야(DressWindow) 등록된다
    }

    [Fact]
    public void should_dress_the_window_when_a_dialog_frame_is_its_root()
    {
        var result = OnSta(() =>
        {
            var (window, frame) = FrameWindow(ResizeMode.NoResize);
            var dressed = ConsoleWindowChrome.TryDress(frame);
            Layout(window);
            var chrome = WindowChrome.GetWindowChrome(window);
            return (
                Dressed: dressed,
                Applied: ConsoleWindowChrome.GetIsApplied(window),
                Chrome: chrome is null ? ((double, Thickness)?)null : (chrome.CaptionHeight, chrome.ResizeBorderThickness),
                CaptionTitle: Descendants<TextBlock>(window).Any(t => t.Text == window.Title),
                FrameStillContent: ReferenceEquals(((ContentControl)window.Content).Content, frame),
                FrameShown: Descendants<ConsoleDialogFrame>(window).Contains(frame),
                Again: ConsoleWindowChrome.TryDress(frame));
        });

        Assert.True(result.Dressed);
        Assert.True(result.Applied);
        Assert.NotNull(result.Chrome);
        Assert.Equal(ConsoleWindowChrome.CaptionHeight, result.Chrome!.Value.Item1);   // 제목 줄 끌기 = OS 창 옮기기 · 스냅
        Assert.Equal(new Thickness(0), result.Chrome.Value.Item2);                       // 크기를 못 바꾸는 창은 가장자리도 잡히지 않는다
        Assert.True(result.CaptionTitle);          // 제목 줄 글자 = 창의 한글 제목 그대로
        Assert.True(result.FrameStillContent);     // 뷰(Content)는 건드리지 않는다
        Assert.True(result.FrameShown);            // 새 템플릿 안에 뷰가 그대로 펼쳐진다
        Assert.False(result.Again);                // 여러 번 불러도 한 번
    }

    [Fact]
    public void should_dress_the_window_when_a_console_shell_is_its_root()
    {
        var result = OnSta(() =>
        {
            var shell = new ConsoleShell();
            var window = NewWindow(new UserControl { Content = shell }, ResizeMode.CanResize);
            return (Dressed: ConsoleWindowChrome.TryDress(shell), Resize: WindowChrome.GetWindowChrome(window)?.ResizeBorderThickness);
        });

        Assert.True(result.Dressed);
        Assert.Equal(SystemParameters.WindowResizeBorderThickness, result.Resize);   // 가장자리 크기 조절은 OS 것 그대로
    }

    [Fact]
    public void should_dress_the_window_when_a_plain_view_opts_in()
    {
        var result = OnSta(() =>
        {
            var view = new UserControl { Content = new TextBlock { Text = "결선" } };
            ConsoleWindowChrome.SetDressWindow(view, true);
            var window = NewWindow(view, ResizeMode.CanResize);
            return (Enlisted: ConsoleWindowChrome.IsEnlisted(view), Dressed: ConsoleWindowChrome.TryDress(view), Applied: ConsoleWindowChrome.GetIsApplied(window));
        });

        Assert.True(result.Enlisted);
        Assert.True(result.Dressed);
        Assert.True(result.Applied);
    }

    [Fact]
    public void should_leave_the_window_alone_when_the_frame_sits_inside_other_content()
    {
        var result = OnSta(() =>
        {
            // 미리보기 도구 · 호스트 카드처럼 다른 칸 안에 얹힌 틀 — 남의 창을 건드리지 않는다
            var frame = new ConsoleDialogFrame { Title = "확인" };
            var window = NewWindow(new Border { Child = frame }, ResizeMode.NoResize);
            return (Dressed: ConsoleWindowChrome.TryDress(frame), Applied: ConsoleWindowChrome.GetIsApplied(window), Template: window.Template);
        });

        Assert.False(result.Dressed);
        Assert.False(result.Applied);
    }

    [Fact]
    public void should_leave_the_window_alone_when_it_is_a_derived_or_chromeless_window()
    {
        var result = OnSta(() =>
        {
            var derivedFrame = new ConsoleDialogFrame();
            _ = new DerivedWindow { Content = new UserControl { Content = derivedFrame } };
            var chromelessFrame = new ConsoleDialogFrame();
            var chromeless = NewWindow(new UserControl { Content = chromelessFrame }, ResizeMode.NoResize);
            chromeless.WindowStyle = WindowStyle.None;
            return (Derived: ConsoleWindowChrome.TryDress(derivedFrame), Chromeless: ConsoleWindowChrome.TryDress(chromelessFrame));
        });

        Assert.False(result.Derived);       // 호스트 셸(MetroWindow) 같은 파생 창은 제 템플릿을 가진다
        Assert.False(result.Chromeless);    // 테두리 없는 창은 스스로 겉을 원하지 않았다
    }
    #endregion

    #region - Buttons and closing -
    [Theory]
    [InlineData(ResizeMode.NoResize, Visibility.Collapsed, Visibility.Collapsed)]
    [InlineData(ResizeMode.CanMinimize, Visibility.Visible, Visibility.Collapsed)]
    [InlineData(ResizeMode.CanResize, Visibility.Visible, Visibility.Visible)]
    public void should_expose_the_close_button_to_uia_when_the_window_is_dressed(ResizeMode mode, Visibility minimize, Visibility maximize)
    {
        var result = OnSta(() =>
        {
            var window = DressedFrameWindow(mode);
            var close = ById(window, ConsoleWindowChrome.CloseAutomationId);
            var peer = close is null ? null : UIElementAutomationPeer.CreatePeerForElement(close);
            return (
                PeerId: peer?.GetAutomationId(),
                PeerType: peer?.GetAutomationControlType(),
                PeerName: peer?.GetName(),
                HasInvoke: peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider,
                HitTestInChrome: close is not null && WindowChrome.GetIsHitTestVisibleInChrome(close),
                CloseFocusable: close?.Focusable ?? true,
                Minimize: ById(window, ConsoleWindowChrome.MinimizeAutomationId)?.Visibility,
                Maximize: ById(window, ConsoleWindowChrome.MaximizeAutomationId)?.Visibility);
        });

        // IsControlElement 는 요소의 IsVisible 을 따른다 — 띄우지 않은 창에서는 거짓이라 여기서 보지 않는다(뜬 창에서는 참).
        Assert.Equal(ConsoleWindowChrome.CloseAutomationId, result.PeerId);
        Assert.Equal(AutomationControlType.Button, result.PeerType);
        Assert.Equal("닫기", result.PeerName);
        Assert.True(result.HasInvoke);
        Assert.True(result.HitTestInChrome);     // 제목 줄(끌기 영역) 안에서도 눌린다
        Assert.False(result.CloseFocusable);     // 다이얼로그 틀의 첫 포커스(취소)를 빼앗지 않는다
        Assert.Equal(minimize, result.Minimize);
        Assert.Equal(maximize, result.Maximize);
    }

    [Fact]
    public void should_close_through_the_os_closing_path_when_the_caption_close_is_invoked()
    {
        var result = OnSta(() =>
        {
            var window = DressedFrameWindow(ResizeMode.NoResize);
            var closingSeen = 0;
            var closed = false;
            var refuse = true;
            // 뷰모델의 CanCloseAsync 관문(Caliburn WindowConductor)은 Closing 에서 막는다 — 같은 길을 지나는지 본다
            window.Closing += (_, e) => { closingSeen++; e.Cancel = refuse; };
            window.Closed += (_, _) => closed = true;

            Invoke(ById(window, ConsoleWindowChrome.CloseAutomationId)!);
            Settle();
            var closedAfterRefusal = closed;

            refuse = false;
            Invoke(ById(window, ConsoleWindowChrome.CloseAutomationId)!);
            Settle();
            return (closingSeen, closedAfterRefusal, closed);
        });

        Assert.Equal(2, result.closingSeen);
        Assert.False(result.closedAfterRefusal);   // 관문이 거절하면 닫히지 않는다(부대 편제 닫기 확인)
        Assert.True(result.closed);
    }
    #endregion

    #region - Tokens -
    [Fact]
    public void should_repaint_the_caption_from_tokens_when_the_theme_is_switched()
    {
        var result = OnSta(() =>
        {
            var light = Tokens("Tokens.Light.xaml");
            var dark = Tokens("Tokens.Dark.xaml");
            var (window, frame) = FrameWindow(ResizeMode.CanResize);
            window.Resources.MergedDictionaries.Add(light);
            ConsoleWindowChrome.TryDress(frame);
            Layout(window);
            var lightCaption = CaptionColor(window);
            var lightTitle = TitleColor(window);
            var lightBack = (window.Background as SolidColorBrush)?.Color;

            window.Resources.MergedDictionaries.Remove(light);
            window.Resources.MergedDictionaries.Add(dark);
            Layout(window);
            return (
                lightCaption, lightTitle, lightBack,
                darkCaption: CaptionColor(window), darkTitle: TitleColor(window), darkBack: (window.Background as SolidColorBrush)?.Color,
                tokenMirror: (ConsoleWindowChrome.GetCaptionToken(window) as SolidColorBrush)?.Color,
                expected: (
                    LightAlt: Brush(light, "SurfaceAltBrush"), DarkAlt: Brush(dark, "SurfaceAltBrush"),
                    LightText: Brush(light, "TextPrimaryBrush"), DarkText: Brush(dark, "TextPrimaryBrush"),
                    LightSurface: Brush(light, "SurfaceBrush"), DarkSurface: Brush(dark, "SurfaceBrush")));
        });

        Assert.NotNull(result.lightCaption);
        Assert.Equal(result.expected.LightAlt, result.lightCaption);
        Assert.Equal(result.expected.LightText, result.lightTitle);
        Assert.Equal(result.expected.LightSurface, result.lightBack);
        Assert.Equal(result.expected.DarkAlt, result.darkCaption);     // 한 번 찾아 쥐지 않는다 — 전환 뒤 새 색
        Assert.Equal(result.expected.DarkText, result.darkTitle);
        Assert.Equal(result.expected.DarkSurface, result.darkBack);
        Assert.Equal(result.expected.DarkAlt, result.tokenMirror);     // DWM 을 다시 칠하게 하는 거울도 따라왔다
        Assert.NotEqual(result.lightCaption, result.darkCaption);
    }

    [Theory]
    [InlineData("Tokens.Light.xaml")]
    [InlineData("Tokens.Dark.xaml")]
    public void should_define_every_token_the_window_chrome_pulls_when_the_theme_is_loaded(string tokenFile)
    {
        // Arrange — 창 겉이 부르는 토큰은 두 테마 모두에 있어야 한다(없으면 런타임에 조용히 투명해진다)
        var chrome = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Utils", "Themes", "ConsoleWindow.xaml"));
        var tokens = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", tokenFile));
        var used = Regex.Matches(chrome, @"\{DynamicResource\s+([A-Za-z0-9_.]+)\s*\}").Select(m => m.Groups[1].Value).Distinct().ToList();

        // Act
        var missing = used.Where(k => !tokens.Contains($"x:Key=\"{k}\"", StringComparison.Ordinal)).ToList();

        // Assert
        Assert.NotEmpty(used);
        Assert.True(missing.Count == 0, $"{tokenFile} 에 없는 토큰: {string.Join(", ", missing)}");
        Assert.DoesNotMatch(@"(Background|Foreground|Stroke|Fill|BorderBrush)=""#", chrome);   // 하드코딩 색 없음
        Assert.DoesNotContain("x:Name", chrome);                                                 // 배선은 명령 · 바인딩으로만
    }
    #endregion

    #region - Fixtures -
    private sealed class DerivedWindow : Window { }

    private static (Window Window, ConsoleDialogFrame Frame) FrameWindow(ResizeMode mode)
    {
        var frame = new ConsoleDialogFrame { Title = "확인", PrimaryText = "확인" };
        var window = NewWindow(new UserControl { Content = frame }, mode);
        return (window, frame);
    }

    private static Window DressedFrameWindow(ResizeMode mode)
    {
        var (window, frame) = FrameWindow(mode);
        var dressed = ConsoleWindowChrome.TryDress(frame);
        Assert.True(dressed, $"입히지 못했다 — getwin={Window.GetWindow(frame) is not null} can={ConsoleWindowChrome.CanDress(window)} root={ConsoleWindowChrome.IsWindowRoot(window, frame)} applied={ConsoleWindowChrome.GetIsApplied(window)} key={window.TryFindResource(ConsoleWindowChrome.WindowTemplateKey)?.GetType().Name}");
        Layout(window);
        return window;
    }

    private static Window NewWindow(object content, ResizeMode mode) => new()
    {
        Title = "결선 — 제어기 1",
        Content = content,
        Width = Width,
        Height = Height,
        ResizeMode = mode,
        ShowInTaskbar = false,
        ShowActivated = false,
    };

    /// <summary>띄우지 않은 창의 템플릿을 펼치고 잰다 — 제목 줄 · 단추 · 뷰가 시각 트리에 선다.</summary>
    private static void Layout(Window window)
    {
        window.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(window) == 0) return;
        var root = (UIElement)VisualTreeHelper.GetChild(window, 0);
        root.Measure(new Size(Width, Height));
        root.Arrange(new Rect(0, 0, Width, Height));
        root.UpdateLayout();
    }

    private static void Settle()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Invoke(Button button)
    {
        var peer = (ButtonAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private static Button? ById(DependencyObject root, string id)
        => Descendants<Button>(root).FirstOrDefault(b => AutomationProperties.GetAutomationId(b) == id);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static Color? CaptionColor(Window window)
        => (Descendants<Border>(window).FirstOrDefault(b => b.Height == ConsoleWindowChrome.CaptionHeight)?.Background as SolidColorBrush)?.Color;

    private static Color? TitleColor(Window window)
        => (Descendants<TextBlock>(window).FirstOrDefault(t => t.Text == window.Title)?.Foreground as SolidColorBrush)?.Color;

    private static Color? Brush(ResourceDictionary tokens, string key) => (tokens[key] as SolidColorBrush)?.Color;

    private static ResourceDictionary Tokens(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

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
