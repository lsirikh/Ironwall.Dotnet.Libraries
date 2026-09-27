using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 화면 밖 렌더 호스트 — Application 을 전용 STA 스레드 하나에 상주시키고 모든 시험을 그 디스패처에서 돌린다.
/// </summary>
/// <remarks>
/// <para>왜 Application 인가: 뷰가 <c>{StaticResource Console.Button}</c> 처럼 앱 사전의 키를 찾는다 — 파서는
/// <c>Application.Current.Resources</c> 로만 폴백한다(project memory offscreen_panel_render_needs_application ①).</para>
/// <para>병합 순서는 호스트 App.xaml 그대로(MahApps → BundledTheme → MD3 기본값 → 개별 MD → Theme.Current) — tools/shared/HostResources.*.xaml 과 같다.
/// 순서가 다르면 호스트에 없는 결함을 만들거나 있는 결함을 숨긴다(D-28).</para>
/// <para>Application 은 프로세스당 1개라 시험마다 스레드를 만들지 않는다(두 번째 생성 불가).</para>
/// </remarks>
internal static class AppHost
{
    private const string DarkTokens = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";
    private static readonly Lazy<Dispatcher> _dispatcher = new(Start);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var merged = app.Resources.MergedDictionaries;
                merged.Add(Dict("pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml"));
                merged.Add(Dict("pack://application:,,,/MahApps.Metro;component/Styles/Fonts.xaml"));
                merged.Add(Dict("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml"));
                merged.Add(new BundledTheme { BaseTheme = BaseTheme.Light, PrimaryColor = MaterialDesignColors.PrimaryColor.Teal, SecondaryColor = MaterialDesignColors.SecondaryColor.Amber });
                merged.Add(Dict("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml"));
                merged.Add(Dict("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.ProgressBar.xaml"));
                merged.Add(Dict("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Flipper.xaml"));
                merged.Add(Dict("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Card.xaml"));
                merged.Add(Dict("pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Theme.Current.xaml"));
                // 호스트 Bootstrapper 와 같이 — 한글은 띄어쓰기에서만 줄바꿈(UIA 이름에 U+2060 이 섞인다 → SelfService.Name 이 걷는다)
                Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Install();
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }
            if (failure is null) Dispatcher.Run();
        })
        { IsBackground = true, Name = "Accounts.ViewTests.AppHost" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (failure is not null) throw new InvalidOperationException("AppHost 기동 실패", failure);
        return dispatcher!;

        static ResourceDictionary Dict(string uri) => new() { Source = new Uri(uri, UriKind.Absolute) };
    }

    /// <summary>시험 본문을 Application 스레드에서 실행한다(예외는 호출 스레드로 전달, 90 s 제한).</summary>
    internal static void Run(Action action)
    {
        Exception? failure = null;
        var op = _dispatcher.Value.InvokeAsync(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        Assert.True(op.Wait(TimeSpan.FromSeconds(90)) == DispatcherOperationStatus.Completed, "AppHost 제한 시간 초과");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>현재 디스패처를 <paramref name="priority"/> 까지 돌린다(Loaded · 바인딩 · 레이아웃).</summary>
    internal static void Pump(DispatcherPriority priority = DispatcherPriority.ContextIdle)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>화면 밖 창에 띄운다 — 사용자의 화면에 아무것도 나타나지 않는다.</summary>
    internal static Window Show(FrameworkElement view)
    {
        var host = new System.Windows.Controls.Border { Padding = new Thickness(24), Child = view };
        host.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "BgBrush");
        // 내용 크기로 — 틀은 자리가 무한이면 규격 폭(S 400 · M 560)과 규격 최대 높이를 그대로 쓴다(셸 표면 층 — Canvas 안 — 과 같은 조건).
        var window = new Window
        {
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = host,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
        };
        window.Show();
        Pump(DispatcherPriority.Loaded);
        Pump();
        return window;
    }

    /// <summary>라이트 ↔ 다크 — 호스트 ThemeService 와 같은 세 가지(토큰 · MD 바탕 · MahApps 크롬)를 같이 바꾼다.</summary>
    internal static void SetDark(bool dark)
    {
        var app = Application.Current;
        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d.Source?.OriginalString == DarkTokens);
        if (dark && existing is null) merged.Add(new ResourceDictionary { Source = new Uri(DarkTokens, UriKind.Absolute) });
        if (!dark && existing is not null) merged.Remove(existing);
        foreach (var bundled in merged.OfType<BundledTheme>()) bundled.BaseTheme = dark ? BaseTheme.Dark : BaseTheme.Light;
        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(app, dark ? "Dark.Cyan" : "Light.Blue");
        Pump();
    }

    /// <summary>창 내용을 PNG 로 — 바탕을 먼저 깔고 시각 트리를 직접 그린다.</summary>
    internal static void Save(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
        if (width <= 0 || height <= 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
