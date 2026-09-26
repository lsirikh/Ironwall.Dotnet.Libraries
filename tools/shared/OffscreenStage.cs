using System.Windows;

namespace PreviewTools.Shared;

/// <summary>
/// 미리보기 창을 <b>보이지 않게</b> 띄운다 — 화면 밖(-20000, -20000)에 두고 활성화 · 작업 표시줄 표시를 끈다.
/// </summary>
/// <remarks>
/// <para>왜 Show() 는 그대로 하나: 창이 떠 있어야(HWND + 레이아웃 · 렌더 패스) 콘솔이 실제 폭으로 재고
/// 배치된다. 스냅숏은 RenderTargetBitmap 으로 시각 트리를 직접 그리므로 화면 위 픽셀이 필요 없다.</para>
/// <para>사용자가 이 PC 에서 일하는 중에 창이 튀어나오지 않게 한다(2026-09-26 요청). 팝업(드롭다운 등)은
/// 모니터 안쪽으로 밀려 나오므로, 팝업을 여는 스냅숏 모드는 이 보호 밖이다 — 그런 모드는 따로 적는다.</para>
/// </remarks>
public static class OffscreenStage
{
    public const double Offset = -20000;

    public static T Hide<T>(T window) where T : Window
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = Offset;
        window.Top = Offset;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        ApplyApiNames(window);
        StartGuard();
        return window;
    }

    /// <summary>
    /// <c>--api-names</c> 를 받았을 때만 API 필드명 캡션 · 절 축 이름을 켠다(커널 <c>ConsoleField.ShowApiNames</c>, 상속).
    /// 기본은 꺼짐 — 운영자 화면과 같은 모습을 그려야 잘림 감사가 실창과 같은 조건이 된다.
    /// </summary>
    public static void ApplyApiNames(Window window)
    {
        if (Environment.GetCommandLineArgs().Contains("--api-names"))
            Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleField.SetShowApiNames(window, true);
    }

    private static System.Windows.Threading.DispatcherTimer? _guard;
    private static readonly System.Text.StringBuilder GuardLog = new();

    /// <summary>
    /// 스냅숏 실행 중에는 25ms 마다 화면 위에 뜬 창(팝업 포함)을 찾아 곧바로 숨긴다 — 흐름이 팝업을 열지 않는 것이
    /// 1차 방어이고, 이것은 그 약속이 어긋났을 때를 위한 뒷받침이다. 찾은 것은 <see cref="GuardOffscreen"/> 가 기록한다.
    /// </summary>
    private static void StartGuard()
    {
        if (_guard is not null || !Environment.GetCommandLineArgs().Contains("--snapshot")) return;
        _guard = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(25),
        };
        _guard.Tick += (_, _) => HideOnScreen("timer", GuardLog);
        _guard.Start();
    }

    /// <summary>
    /// <c>--surface 1280x760</c> — 실제 앱의 표면(틀 안쪽) 크기 그대로 콘솔을 못 박는다. 창 크기와 상관없이
    /// 뷰가 그 크기로 재고 배치되므로(서랍/도킹 판정 · 열 폭 · 툴바 예산) 실창 캡처와 같은 조건이 된다.
    /// </summary>
    public static bool ApplySurface(string[] args, FrameworkElement view, Window window)
    {
        var at = System.Array.IndexOf(args, "--surface");
        if (at < 0 || at + 1 >= args.Length) return false;

        var parts = args[at + 1].Split('x', 'X');
        if (parts.Length != 2
            || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
            return false;

        view.Width = width;
        view.Height = height;
        view.HorizontalAlignment = HorizontalAlignment.Left;
        view.VerticalAlignment = VerticalAlignment.Top;
        window.Width = width + 80;
        window.Height = height + 100;
        return true;
    }

    /// <summary>
    /// 좁은 폭을 흉내 낸다. <c>--surface</c> 로 콘솔 폭을 못 박았으면 창 폭을 바꿔도 콘솔이 안 줄어든다 —
    /// 그때는 콘솔(표면) 폭을 <paramref name="width"/> 로 바꾸고, 아니면 예전처럼 창 폭을 바꾼다.
    /// </summary>
    public static void SetWidth(Window window, FrameworkElement? view, double width)
    {
        if (view is not null && !double.IsNaN(view.Width))
        {
            view.Width = width;
            window.Width = width + 80;
            return;
        }
        window.Width = width;
    }

    /// <summary>화면 밖이 아닌 곳에 떠 있는 이 프로세스의 창(팝업 포함)을 찾아 <c>offscreen-guard.txt</c> 에 적는다.</summary>
    /// <remarks>팝업(드롭다운 · 툴팁)은 화면 밖 창을 따라가지 않고 모니터 안쪽으로 밀려 나온다 — 감사 중에 그런 것이
    /// 하나라도 뜨면 "창이 보이지 않는다" 약속이 깨진 것이다. 발견하면 즉시 숨긴다(SW_HIDE).</remarks>
    public static void GuardOffscreen(string directory, string frame)
    {
        var lines = new System.Text.StringBuilder();
        HideOnScreen(frame, lines);
        lock (GuardLog)
        {
            lines.Append(GuardLog);
            GuardLog.Clear();
        }
        var windows = string.Join(" ", System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>()
            .Where(s => s.Handle != IntPtr.Zero && GetWindowRect(s.Handle, out _))
            .Select(s => { GetWindowRect(s.Handle, out var r); return $"{s.RootVisual?.GetType().Name}({r.Left},{r.Top})vis={IsWindowVisible(s.Handle)}"; }));
        System.IO.File.AppendAllText(System.IO.Path.Combine(directory, "offscreen-guard.txt"),
            lines.Length > 0 ? lines.ToString() : $"{frame}: ok {windows}{Environment.NewLine}");
    }

    private static void HideOnScreen(string frame, System.Text.StringBuilder log)
    {
        var screen = new NativeRect { Left = GetSystemMetrics(76), Top = GetSystemMetrics(77) };
        screen.Right = screen.Left + GetSystemMetrics(78);
        screen.Bottom = screen.Top + GetSystemMetrics(79);

        foreach (var source in System.Windows.PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>())
        {
            var handle = source.Handle;
            if (handle == IntPtr.Zero || !IsWindowVisible(handle) || !GetWindowRect(handle, out var r)) continue;
            var onScreen = r.Right > screen.Left && r.Left < screen.Right && r.Bottom > screen.Top && r.Top < screen.Bottom;
            if (!onScreen) continue;
            ShowWindow(handle, 0);
            lock (log) log.AppendLine($"{frame}: ON-SCREEN {source.RootVisual?.GetType().Name} rect=({r.Left},{r.Top})-({r.Right},{r.Bottom}) -> hidden");
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
}
