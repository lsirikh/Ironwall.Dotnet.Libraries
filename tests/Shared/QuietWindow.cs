using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Ironwall.Dotnet.Libraries.Tests.Shared;

/// <summary>
/// 시험 창을 사용자 화면 · 키보드 포커스와 떼어 놓는 공용 도우미 — 창을 띄우는 모든 시험이 이것으로 Show() 한다
/// (2026-10-21: 시험 창이 뜰 때마다 사용자의 포커스를 0.1초 간격으로 10~20번 뺏는 것이 실측됨).
/// </summary>
/// <remarks>
/// <para>창은 화면 밖 자리 · <c>ShowActivated=false</c> · <c>ShowInTaskbar=false</c> · <c>Topmost=false</c> 로 띄운다.
/// 그것만으로는 모자라다 — 시험의 <c>Focus()</c>(WPF 가 Win32 SetFocus 로 창을 활성화) · 제품 코드의 <c>Activate()</c> ·
/// 확인 대화상자 <c>ShowDialog()</c> 가 창을 활성화하고, 시험 프로세스가 전경 권한을 가진 순간(사용자가 한동안 입력하지 않았을 때 등)에는
/// 사용자 창의 포커스를 가져간다.</para>
/// <para>그래서 이 스레드에 스레드 한정 CBT 훅을 걸어 <b>이 스레드가 만든 모든 창의 활성화</b>(HCBT_ACTIVATE)를 거절한다 —
/// 시험이 직접 만든 창뿐 아니라 시험 중 제품 코드가 여는 창 · 대화상자도 막힌다. 다른 스레드 · 다른 프로세스에는 닿지 않는다.
/// 활성화가 막힌 창의 요소는 키보드 포커스를 받지 못한다(<c>Focus()</c> 가 false) — 논리 포커스(FocusManager)는 잡힌다.</para>
/// <para>같은 훅이 이 스레드가 만드는 팝업 창(Popup · ToolTip · 드롭다운 — WS_POPUP + WS_EX_NOACTIVATE)에 빈 창 영역을 건다.
/// WPF 팝업은 화면 밖 창에 딸려도 모니터 안쪽으로 붙잡혀 사용자 화면에 뜨기 때문이다(2026-10-21 실측: HelpTip 말풍선이 [-1920,1]).
/// 창 영역은 OS 화면 표시만 자른다 — WPF 의 IsOpen · 배치 · 자동화 · RenderTargetBitmap 은 그대로다.</para>
/// <para>키보드 포커스 자체가 시험 대상이면(Win32 포커스 = 창 활성화가 필요) 그 STA 스레드 첫 줄에서 <see cref="UseHiddenDesktop"/> 를 불러
/// 사용자에게 보이지 않는 데스크톱으로 옮긴다 — 거기서는 활성화를 막지 않는다. 부르는 곳마다 이유를 주석으로 남긴다.</para>
/// <para><c>WindowStyle</c> 은 바꾸지 않는다 — 틀 두께가 바뀌면 클라이언트 크기를 재는 시험의 조건이 달라진다.</para>
/// </remarks>
internal static class QuietWindow
{
    private const double Offscreen = -20000;

    /// <summary>비활성 · 화면 밖 창으로 띄운다. 이 창과 이 스레드의 다른 창은 활성화되지 않는다(키보드 포커스 없음).</summary>
    public static T Show<T>(T window) where T : Window
    {
        Prepare(window);
        window.Show();
        return window;
    }

    /// <summary>
    /// 이 STA 스레드를 사용자에게 보이지 않는 데스크톱(프로세스마다 하나)으로 옮긴다 — 그 데스크톱의 창은 활성화 · 키보드 포커스 ·
    /// 입력 라우팅이 그대로 되면서(이 스레드는 활성화를 막지 않는다) 사용자 데스크톱의 전경 · 화면에는 닿지 않는다.
    /// 스레드가 창을 하나라도 만들기 전, 스레드 첫 줄에서 부른다.
    /// </summary>
    /// <remarks>STA 초기화가 만든 COM 숨은 창이 있으면 SetThreadDesktop 이 거절된다(ERROR_BUSY) — COM 을 내렸다가 같은 STA 로 다시 올린다.
    /// 데스크톱은 프로세스가 끝나면 시스템이 함께 지운다.</remarks>
    public static void UseHiddenDesktop()
    {
        CoUninitialize();
        if (!SetThreadDesktop(HiddenDesktop.Value))
            throw new InvalidOperationException($"숨은 데스크톱으로 옮기지 못했다(오류 {Marshal.GetLastWin32Error()}) — 스레드가 이미 창을 만들었다");
        CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);
        _onHiddenDesktop = true;
    }

    /// <summary>
    /// UIA 클라이언트처럼 <b>다른 스레드</b>에서 숨은 데스크톱의 창을 다룰 때 — 그 일을 숨은 데스크톱에 둔 새 스레드에서 돌린다.
    /// 기본 데스크톱 스레드의 UIA 는 숨은 데스크톱 창의 WindowPattern 을 얻지 못한다(2026-10-21 실측: "지원되지 않는 Pattern").
    /// </summary>
    public static Task<T> RunOnHiddenDesktop<T>(Func<T> body)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (!SetThreadDesktop(HiddenDesktop.Value))
                    throw new InvalidOperationException($"숨은 데스크톱으로 옮기지 못했다(오류 {Marshal.GetLastWin32Error()})");
                done.SetResult(body());
            }
            catch (Exception ex) { done.SetException(ex); }
        }) { IsBackground = true };
        thread.Start();
        return done.Task;
    }

    public static Task RunOnHiddenDesktop(Action body) => RunOnHiddenDesktop(() => { body(); return true; });

    private static readonly Lazy<IntPtr> HiddenDesktop = new(() =>
        CreateDesktop($"ironwall-tests-{Environment.ProcessId}", IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero));

    /// <summary>이 스레드에 훅을 한 번만 건다.</summary>
    private static void GuardThread()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWindowsHookEx(WH_CBT, Hook, IntPtr.Zero, GetCurrentThreadId());
    }

    private static void Prepare(Window window)
    {
        GuardThread();
        // 시험이 일부러 정한 자리(배치 시험 등)는 그대로 둔다 — 정하지 않은 창만 화면 밖으로.
        if (double.IsNaN(window.Left) || double.IsNaN(window.Top))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = Offscreen;
            window.Top = Offscreen;
        }
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Topmost = false;
        // 숨은 데스크톱에서 TextBox 가 진짜 초점을 받으면 TSF(입력기)가 끝난 TextStore 를 불러 시험 호스트가 죽었다
        // (2026-10-21 전체 Utils 실행 2/2: TextStore.GetStatus → NullReferenceException → FailFast) — 닫기 전에 초점을 비워 TSF 가 문서를 놓게 한다.
        if (_onHiddenDesktop) window.Closing += (_, _) => Keyboard.ClearFocus();
    }

    private const int WH_CBT = 5;
    private const int HCBT_CREATEWND = 3;
    private const int HCBT_ACTIVATE = 5;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint GENERIC_ALL = 0x10000000;
    private const int COINIT_APARTMENTTHREADED = 2;

    // 스레드 한정 훅 — 스레드가 끝나면 시스템이 함께 푼다. 대리자는 GC 되지 않게 정적으로 붙잡는다.
    [ThreadStatic] private static IntPtr _hook;
    [ThreadStatic] private static bool _onHiddenDesktop;
    private static readonly HookProc Hook = (code, wParam, lParam) =>
    {
        if (code == HCBT_ACTIVATE && !_onHiddenDesktop) return (IntPtr)1;
        if (code == HCBT_CREATEWND)
        {
            var create = Marshal.PtrToStructure<CreateStruct>(Marshal.PtrToStructure<CbtCreateWnd>(lParam).lpcs);
            if ((create.style & WS_POPUP) != 0 && (create.dwExStyle & WS_EX_NOACTIVATE) != 0)
                SetWindowRgn(wParam, CreateRectRgn(0, 0, 0, 0), false);   // 영역은 창이 가져간다(지우지 않는다)
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    };

    [StructLayout(LayoutKind.Sequential)] private struct CbtCreateWnd { public IntPtr lpcs, hwndInsertAfter; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CreateStruct { public IntPtr lpCreateParams, hInstance, hMenu, hwndParent; public int cy, cx, y, x, style; public IntPtr lpszName, lpszClass; public int dwExStyle; }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice, IntPtr pDevmode, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetThreadDesktop(IntPtr hDesktop);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);
}
