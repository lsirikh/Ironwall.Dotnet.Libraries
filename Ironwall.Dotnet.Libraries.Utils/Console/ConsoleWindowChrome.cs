using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 콘솔 OS 창의 겉 — 토큰으로 칠한 제목 줄 · 창 단추 · 도킹 폭 보정 (window-design-inventory B2)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 콘솔이 <b>제 OS 창의 뿌리</b>일 때 그 창의 겉(제목 줄 · 최소화 · 최대화 · 닫기)을 테마 토큰으로 그린다.
/// 부대 편제 창만 칠하던 <c>UnitWindowChrome</c>(Devices.Ui) 을 커널로 옮긴 것이다 — 원본은 지웠다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 조립기 · 결선 · 맵핑 · 배정 · 서버 · 보고서 창은 본문이 커널 · 토큰인데 제목 줄만 OS 기본 크림색이라
/// 다크에서 두 창을 이어 붙인 것처럼 보였다(window-design-inventory #30~43).</para>
/// <para><b>어디서 입히나</b>: 창을 만드는 런처는 스무 곳이 넘고 Caliburn 이 창을 만든다 — 런처마다 부르면 빠뜨린다.
/// 그래서 <see cref="ConsoleShell"/> · <c>ConsoleDialogFrame</c> 이 생성자에서 <see cref="Enlist"/> 하고,
/// <b>제 창의 뿌리일 때만</b>(<see cref="IsWindowRoot"/>) 그 창에 입힌다. 뿌리가 커널 틀이 아닌 창 뷰는
/// <see cref="DressWindowProperty"/> 를 켜고, 직접 만드는 창(<c>ReportLargePreviewWindow</c>)은 <see cref="Apply(Window)"/> 를 부른다.</para>
/// <para><b>무엇을 건드리나</b>: 창의 <see cref="Control.Template"/> 과 <see cref="WindowChrome"/> 뿐이다. <see cref="ContentControl.Content"/>
/// (뷰)는 그대로다 — 뷰의 뿌리 판정 · Caliburn 결속 · 자동화 식별자가 바뀌지 않는다. OS 제목(<see cref="Window.Title"/>)도 그대로라
/// 작업 전환 · UIA 창 이름 · <c>WindowPattern.Close</c> · Alt+F4 · 끌어 옮기기 · 스냅 · 가장자리 크기 조절이 전부 OS 것 그대로 동작한다.</para>
/// <para><b>닫기</b>는 <see cref="Window.Close"/> 로 간다 — OS ✕ · Alt+F4 와 같은 <c>Closing</c> 을 지나므로
/// 뷰모델의 <c>CanCloseAsync</c> 확인(부대 편제 닫기 관문 등)이 그대로 걸린다. 뿌리가 다이얼로그 틀인 창은 틀이
/// <see cref="CloseRedirectProperty"/> 로 제 취소 길을 걸어 둔다(그 창에서 틀은 제 머리 · ✕ 를 감춘다).</para>
/// <para>색은 템플릿이 전부 <c>DynamicResource</c> 로 가져오고, DWM(창 테두리 · 다크 표지)은 토큰이 바뀔 때마다 <b>다시 찾아</b> 칠한다
/// — 한 번 찾아 쥐면 테마를 바꿔도 옛 색으로 굳는다(저장소 규칙).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public static class ConsoleWindowChrome
{
    /// <summary>제목 줄 높이(DIU). 커널 머리(40)보다 낮게 — 제목 줄은 창의 것이고 머리는 콘솔의 것이다.</summary>
    public const double CaptionHeight = 32;

    /// <summary>테두리 몫으로 볼 수 있는 최대 부족분(DIU) — 이보다 크게 모자라면 사용자가 좁힌 것으로 본다.</summary>
    public const double MaxChromeDeficit = 48;

    public const string CloseAutomationId = "Console.Window.Close";
    public const string MinimizeAutomationId = "Console.Window.Minimize";
    public const string MaximizeAutomationId = "Console.Window.Maximize";
    public const string CaptionAutomationId = "Console.Window.Caption";

    /// <summary>창 템플릿의 키 — <c>Themes/ConsoleWindow.xaml</c>(이 어셈블리의 테마 사전)에 있다.</summary>
    public static readonly ComponentResourceKey WindowTemplateKey = new(typeof(ConsoleWindowChrome), "WindowTemplate");

    #region - Commands (template-bound) -
    /// <summary>
    /// 제목 줄 ✕ — <see cref="Window.Close"/>. OS ✕ 와 같은 <c>Closing</c> 을 지난다.
    /// 창이 <see cref="CloseRedirectProperty"/> 를 쥐고 있으면(뿌리가 다이얼로그 틀인 창) 그리로 보낸다 — 틀의 취소 길 하나로.
    /// </summary>
    public static ICommand CloseCommand { get; } = new WindowCommand(w =>
    {
        if (GetCloseRedirect(w) is { } redirect) redirect();
        else w.Close();
    });

    public static readonly DependencyProperty CloseRedirectProperty = DependencyProperty.RegisterAttached(
        "CloseRedirect", typeof(Action), typeof(ConsoleWindowChrome), new PropertyMetadata(null));

    /// <summary>
    /// 제목 줄 ✕ 를 창 닫기 대신 이것으로 보낸다. 다이얼로그 틀이 제 창의 뿌리일 때 켠다 — 틀의 [취소] · ESC 와 <b>같은 길</b>
    /// (<c>SecondaryInvoked</c>)로 가야 뷰의 취소 처리가 한 번만 돌고 창이 두 번 닫히지 않는다. Alt+F4 · 작업 표시줄 닫기는 그대로 OS 길이다.
    /// </summary>
    public static Action? GetCloseRedirect(DependencyObject window) => (Action?)window.GetValue(CloseRedirectProperty);
    public static void SetCloseRedirect(DependencyObject window, Action? value) => window.SetValue(CloseRedirectProperty, value);

    public static ICommand MinimizeCommand { get; } = new WindowCommand(SystemCommands.MinimizeWindow);

    /// <summary>최대화 ↔ 이전 크기.</summary>
    public static ICommand MaximizeRestoreCommand { get; } = new WindowCommand(w =>
    {
        if (w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(w);
        else SystemCommands.MaximizeWindow(w);
    });

    private sealed class WindowCommand : ICommand
    {
        private readonly Action<Window> _run;
        public WindowCommand(Action<Window> run) => _run = run;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is Window;
        public void Execute(object? parameter) { if (parameter is Window window) _run(window); }
    }
    #endregion

    #region - Converters (template-bound) -
    /// <summary>최대화된 창은 화면 밖으로 테두리 몫만큼 넘친다 — 그만큼 안으로 들인다.</summary>
    public static IValueConverter MaximizedInset { get; } = new FuncConverter(v =>
        v is WindowState.Maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0));

    /// <summary>최소화 단추 — 크기를 못 바꾸는 창(<see cref="ResizeMode.NoResize"/>)에는 없다. OS 제목 줄과 같다.</summary>
    public static IValueConverter MinimizeVisibility { get; } = new FuncConverter(v =>
        v is ResizeMode mode && CaptionButtons(mode).Minimize ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>최대화 단추 — 크기를 바꿀 수 있는 창에만.</summary>
    public static IValueConverter MaximizeVisibility { get; } = new FuncConverter(v =>
        v is ResizeMode mode && CaptionButtons(mode).Maximize ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>최대화 단추의 두 모양 — 매개변수 <c>Maximized</c> 는 최대화일 때, 그 밖은 아닐 때 보인다.</summary>
    public static IValueConverter StateGlyphVisibility { get; } = new FuncConverter((v, p) =>
        (v is WindowState.Maximized) == string.Equals(p as string, "Maximized", StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>최대화 단추가 읽히는 이름 — 지금 누르면 무엇이 되는가.</summary>
    public static IValueConverter MaximizeName { get; } = new FuncConverter(v => v is WindowState.Maximized ? "이전 크기로" : "최대화");

    private sealed class FuncConverter : IValueConverter
    {
        private readonly Func<object?, object?, object> _convert;
        public FuncConverter(Func<object?, object> convert) => _convert = (v, _) => convert(v);
        public FuncConverter(Func<object?, object?, object> convert) => _convert = convert;
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => _convert(value, parameter);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }
    #endregion

    #region - Pure rules -
    /// <summary>창의 크기 방식이 어떤 단추를 두는가 — OS 기본 제목 줄과 같게.</summary>
    public static (bool Minimize, bool Maximize) CaptionButtons(ResizeMode mode) => mode switch
    {
        ResizeMode.NoResize => (false, false),
        ResizeMode.CanMinimize => (true, false),
        _ => (true, true),
    };

    /// <summary>
    /// 콘솔 실폭이 도킹 기준에 모자라는 만큼(테두리 몫) — 넓혀야 할 폭. 넓히지 않아야 하면 0.
    /// </summary>
    /// <param name="consoleWidth">콘솔(<see cref="ConsoleShell"/>)의 실제 폭.</param>
    /// <param name="windowWidth">창 폭(테두리 포함).</param>
    /// <param name="workAreaWidth">작업 영역 폭 — 넘치게 넓히지 않는다.</param>
    public static double DockingDeficit(double consoleWidth, double windowWidth, double workAreaWidth)
    {
        if (double.IsNaN(consoleWidth) || double.IsNaN(windowWidth) || consoleWidth <= 0 || windowWidth <= 0) return 0;
        var deficit = Math.Ceiling(ConsoleLayoutMath.DockedMinWidth - consoleWidth);
        if (deficit <= 0 || deficit > MaxChromeDeficit) return 0;
        return windowWidth + deficit <= workAreaWidth ? deficit : 0;
    }

    /// <summary>상대 휘도가 절반 아래면 어두운 바탕이다.</summary>
    public static bool IsDark(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 < 0.5;

    /// <summary>WPF 색 → Win32 COLORREF(0x00BBGGRR).</summary>
    public static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /// <summary>
    /// 이 창에 겉을 입혀도 되는가. <b>평범한 <see cref="Window"/></b> 만 — 파생 창(호스트 셸의 <c>MetroWindow</c> 등)은
    /// 제 템플릿을 가진다. 테두리 없는 창(<see cref="WindowStyle.None"/> · 투명 창)은 스스로 겉을 원하지 않은 것이다.
    /// </summary>
    public static bool CanDress(Window window)
        => window.GetType() == typeof(Window)
        && window.WindowStyle != WindowStyle.None
        && !window.AllowsTransparency;

    /// <summary>
    /// <paramref name="element"/> 가 창의 뿌리인가 — 창의 <see cref="ContentControl.Content"/> 에서 <c>Content</c> 만 따라 내려가 닿으면 뿌리다.
    /// Caliburn 창은 <c>Window.Content = 뷰(UserControl)</c>, 뷰의 <c>Content = 틀</c> 이다. 다른 칸 · 테두리 안에 얹힌 콘솔(미리보기 도구 ·
    /// 호스트 카드)은 뿌리가 아니다 — 남의 창을 건드리지 않는다.
    /// </summary>
    public static bool IsWindowRoot(Window window, DependencyObject element)
    {
        object? current = window.Content;
        for (var depth = 0; current is not null && depth < 8; depth++)
        {
            if (ReferenceEquals(current, element)) return true;
            current = (current as ContentControl)?.Content;
        }
        return false;
    }
    #endregion

    #region - Enlist -
    public static readonly DependencyProperty DressWindowProperty = DependencyProperty.RegisterAttached(
        "DressWindow", typeof(bool), typeof(ConsoleWindowChrome), new PropertyMetadata(false, OnDressWindowChanged));

    /// <summary>
    /// 뿌리가 커널 틀(<see cref="ConsoleShell"/> · <c>ConsoleDialogFrame</c>)이 아닌 창 뷰가 켠다 — 이 뷰가 제 창의 뿌리면 겉을 입힌다.
    /// </summary>
    public static bool GetDressWindow(DependencyObject d) => (bool)d.GetValue(DressWindowProperty);
    public static void SetDressWindow(DependencyObject d, bool value) => d.SetValue(DressWindowProperty, value);

    private static void OnDressWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && e.NewValue is true) Enlist(element);
    }

    private static readonly DependencyProperty IsEnlistedProperty = DependencyProperty.RegisterAttached(
        "IsEnlisted", typeof(bool), typeof(ConsoleWindowChrome), new PropertyMetadata(false));

    /// <summary>
    /// 이 요소가 <b>제 창의 뿌리가 되면</b> 그 창에 겉을 입힌다. 커널 틀이 생성자에서 부른다. 여러 번 불러도 한 번.
    /// </summary>
    /// <remarks>
    /// 창이 뜰 때(요소가 창의 <see cref="PresentationSource"/> 에 붙을 때) 판정한다. 입히기는 한 박자 미룬다(<see cref="DispatcherPriority.Send"/>) —
    /// 그 순간은 창 템플릿을 펼치는 측정 중이라 거기서 템플릿을 바꾸면 안 된다. Send 는 첫 그리기(Render) · <c>Loaded</c> 보다 앞이라
    /// 크림색 제목 줄이 한 번도 그려지지 않고, 뷰의 <c>Loaded</c> 도 두 번 오지 않는다.
    /// </remarks>
    public static void Enlist(UIElement element)
    {
        if ((bool)element.GetValue(IsEnlistedProperty)) return;
        element.SetValue(IsEnlistedProperty, true);
        PresentationSource.AddSourceChangedHandler(element, OnEnlistedSourceChanged);
    }

    /// <summary>이 요소가 <see cref="Enlist"/> 됐는가(커널 틀은 생성자에서 스스로 한다).</summary>
    public static bool IsEnlisted(UIElement element) => (bool)element.GetValue(IsEnlistedProperty);

    private static void OnEnlistedSourceChanged(object sender, SourceChangedEventArgs e)
    {
        if (e.NewSource is null || sender is not DependencyObject element) return;
        if (Window.GetWindow(element) is not { } window || !ShouldDress(window, element)) return;

        window.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => TryDress(element)));
    }

    /// <summary>
    /// <paramref name="element"/> 가 담긴 창에 겉을 입힌다 — 평범한 창이고(<see cref="CanDress"/>) 이 요소가 그 창의 뿌리일 때만
    /// (<see cref="IsWindowRoot"/>). 입혔으면 <c>true</c>. 창이 뜰 때 <see cref="Enlist"/> 가 부르는 판정 그대로다.
    /// </summary>
    public static bool TryDress(DependencyObject element)
        => Window.GetWindow(element) is { } window && ShouldDress(window, element) && Apply(window);

    private static bool ShouldDress(Window window, DependencyObject element)
        => !GetIsApplied(window) && CanDress(window) && IsWindowRoot(window, element);
    #endregion

    #region - Apply -
    private static readonly DependencyProperty IsAppliedProperty = DependencyProperty.RegisterAttached(
        "IsApplied", typeof(bool), typeof(ConsoleWindowChrome), new PropertyMetadata(false));

    /// <summary>이 창에 콘솔 겉이 입혀졌는가.</summary>
    public static bool GetIsApplied(DependencyObject window) => (bool)window.GetValue(IsAppliedProperty);

    /// <summary>
    /// 창에 콘솔 겉을 입힌다. 이미 입혔으면 아무것도 하지 않는다. 템플릿을 못 찾으면(테마 사전 없음) 입히지 않고 <c>false</c>.
    /// 창이 뜨기 전에 불러도 된다(직접 만드는 창의 생성자).
    /// </summary>
    public static bool Apply(Window window)
    {
        if (GetIsApplied(window)) return false;
        if (FindTemplate(window) is not { } template) return false;

        window.SetValue(IsAppliedProperty, true);

        var resizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = CaptionHeight,
            ResizeBorderThickness = resizable ? SystemParameters.WindowResizeBorderThickness : new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });
        window.Template = template;

        // 바탕을 창이 정하지 않았으면 토큰으로 — 뷰가 칠하지 않은 틈이 흰색으로 비치지 않게(다크).
        if (window.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
            window.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");

        // DWM(창 테두리 · 다크 표지)은 토큰이 바뀔 때마다 다시 칠한다 — 이 두 속성이 토큰을 따라 바뀌며 알려 준다.
        window.SetResourceReference(CaptionTokenProperty, "SurfaceAltBrush");
        window.SetResourceReference(BorderTokenProperty, "BorderBrush");
        window.SourceInitialized += (_, _) => PaintDwm(window);
        PaintDwm(window);
        return true;
    }

    /// <summary>
    /// 창 템플릿을 찾는다 — 먼저 키로(호스트가 같은 키로 바꿔 끼울 수 있게, 보통은 이 어셈블리의 테마 사전에서 풀린다),
    /// 못 찾으면 사전 파일을 직접 읽는다. 테마 사전 조회는 앱 · 창이 덜 갖춰진 곳(헤드리스 시험 · 미리보기 도구)에서
    /// 조용히 비는 일이 있다(ConsoleShellTests 실측). 템플릿은 스레드에 묶이므로 쥐어 두지 않고 매번 찾는다.
    /// </summary>
    private static ControlTemplate? FindTemplate(Window window)
    {
        if (window.TryFindResource(WindowTemplateKey) is ControlTemplate found) return found;
        try
        {
            var dictionary = (ResourceDictionary)Application.LoadComponent(
                new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/ConsoleWindow.xaml", UriKind.Relative));
            return dictionary[WindowTemplateKey] as ControlTemplate;
        }
        catch (IOException) { return null; }              // 사전이 없다 — 겉 없이 OS 제목 줄 그대로 쓴다
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>
    /// 콘솔 창에 겉을 입히고, 콘솔(<see cref="ConsoleShell"/>) 실폭이 도킹 기준에 테두리 몫만큼 모자라면 그만큼 넓힌다 —
    /// 1280 창이 테두리 때문에 서랍(상세 숨김)으로 판정되던 것(visual-review #38). 사용자가 일부러 좁힌 창은 건드리지 않는다.
    /// </summary>
    public static void Apply(Window window, FrameworkElement console)
    {
        Apply(window);
        if (window.WindowState != WindowState.Normal) return;
        var extra = DockingDeficit(console.ActualWidth, window.ActualWidth, SystemParameters.WorkArea.Width);
        if (extra > 0) window.Width = window.ActualWidth + extra;
    }
    #endregion

    #region - DWM -
    public static readonly DependencyProperty CaptionTokenProperty = DependencyProperty.RegisterAttached(
        "CaptionToken", typeof(object), typeof(ConsoleWindowChrome), new PropertyMetadata(null, OnTokenChanged));

    /// <summary>제목 줄 토큰(<c>SurfaceAltBrush</c>)을 따라가는 거울 — 테마가 바뀌면 값이 바뀌어 DWM 을 다시 칠하게 한다.</summary>
    public static object? GetCaptionToken(DependencyObject d) => d.GetValue(CaptionTokenProperty);

    public static readonly DependencyProperty BorderTokenProperty = DependencyProperty.RegisterAttached(
        "BorderToken", typeof(object), typeof(ConsoleWindowChrome), new PropertyMetadata(null, OnTokenChanged));

    public static object? GetBorderToken(DependencyObject d) => d.GetValue(BorderTokenProperty);

    private static void OnTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window) PaintDwm(window);
    }

    /// <summary>
    /// DWM 에 다크 표지(시스템 메뉴 · 창 그림자)와 창 테두리 색을 알린다 — 제목 줄은 이미 WPF 가 토큰으로 그린다.
    /// 창 핸들이 없으면 생긴 뒤에(<see cref="Window.SourceInitialized"/>) 칠한다. 지원하지 않는 OS 에서는 조용히 아무것도 하지 않는다.
    /// </summary>
    private static void PaintDwm(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        // 매번 다시 찾는다 — 쥐고 있으면 테마를 바꿔도 옛 색으로 굳는다.
        var caption = (window.TryFindResource("SurfaceAltBrush") as SolidColorBrush)?.Color;
        var border = (window.TryFindResource("BorderBrush") as SolidColorBrush)?.Color;
        if (caption is not { } back) return;

        try
        {
            var dark = IsDark(back) ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            if (border is { } edge)
            {
                var colorRef = ToColorRef(edge);
                _ = DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));   // Windows 11 22000+, 그 전은 무시
            }
        }
        catch (DllNotFoundException) { }          // DWM 이 없는 환경(서버 코어 등) — 그래도 창은 그대로 쓸 수 있다
        catch (EntryPointNotFoundException) { }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    #endregion
}
