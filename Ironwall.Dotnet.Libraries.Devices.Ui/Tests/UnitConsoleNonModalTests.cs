using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 부대 관계도 TEST-34 — 비모달이 된 부대 콘솔 창에서도 닫기 가드(7a5dd396)와 창 수명이 그대로인가. <b>실제 창</b>(화면 밖 · 활성화 없음)을
/// Caliburn 창 관리자로 띄워 사람이 ✕ 를 누르는 길(<see cref="Window.Close"/> → 창 지휘자 → <c>CanCloseAsync</c>)을 그대로 지난다.
/// </summary>
/// <remarks>
/// <para>뷰는 가벼운 대역(<see cref="Grid"/>)이다 — 이 어셈블리의 시험은 Application 없이 돈다(부대 콘솔 실제 뷰는 앱 사전의 키를
/// StaticResource 로 찾는다 — 실제 뷰 · 제목 줄 겉은 <c>tests/Consoles.ViewTests</c> · <c>ConsoleWindowChromeShownTests</c> 몫).
/// 가드 · 창 지휘자 · 소유자 · 최소화 복원 · 로그아웃 닫기는 뷰와 무관하게 창 수준에서 일어나므로 대역으로 충분하다.</para>
/// <para>확인 창(모달)은 띄우지 않고 기록만 한다 — 모달 루프가 시험 스레드를 붙잡는다.</para>
/// </remarks>
[Collection("CaliburnIoC")]
public sealed class UnitConsoleNonModalTests
{
    #region - 비모달 · 소유자 (FR-43) -
    [Fact]
    public void should_leave_the_shell_usable_and_own_the_console_by_the_shell_when_opened()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            return (Visible: window.IsVisible, ShellEnabled: env.Shell.IsEnabled, Owner: env.ConsoleWindow.Owner, Topmost: env.ConsoleWindow.Topmost,
                    Active: env.Console!.IsActive);
        });

        Assert.True(result.Visible);                 // ShowWindowAsync 가 창이 떠 있는 채로 돌아왔다(비모달)
        Assert.True(result.ShellEnabled);            // 모달이면 셸이 막힌다(IsEnabled=false)
        Assert.True(result.Owner is not null);       // 셸 위에 뜬다
        Assert.False(result.Topmost);
        Assert.True(result.Active);
    }

    [Fact]
    public void should_restore_and_bring_back_a_minimized_console_when_opened_again()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            env.ConsoleWindow!.WindowState = WindowState.Minimized;
            Sta.Pump();
            env.Open();
            return (State: env.ConsoleWindow.WindowState, Windows: env.Windows.ShownWindows.Count);
        });

        Assert.Equal(WindowState.Normal, result.State);
        Assert.Equal(1, result.Windows);
    }
    #endregion

    #region - 닫기 가드 (7a5dd396) -
    [Fact]
    public void should_ask_and_keep_the_window_when_the_user_refuses_to_discard_an_unsaved_edit()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            env.MakeDirty();
            env.Windows.ConfirmAnswer = false;

            window.Close();                             // ✕
            Sta.Pump();

            return (Asked: env.Windows.Confirms, Visible: window.IsVisible, Active: env.Console!.IsActive);
        });

        Assert.Equal(1, result.Asked);
        Assert.True(result.Visible);                                 // 닫기 취소
        Assert.True(result.Active);
    }

    [Fact]
    public void should_close_and_release_the_instance_when_the_user_agrees_to_discard()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            var first = env.Console;
            env.MakeDirty();

            window.Close();
            Sta.PumpUntil(() => !window.IsVisible);
            var closed = !window.IsVisible;
            env.Open();                                              // 다음 열기는 새 인스턴스
            return (Asked: env.Windows.Confirms, Closed: closed, Fresh: !ReferenceEquals(first, env.Console), Windows: env.Windows.ShownWindows.Count);
        });

        Assert.Equal(1, result.Asked);
        Assert.True(result.Closed);
        Assert.True(result.Fresh);
        Assert.Equal(2, result.Windows);
    }

    [Fact]
    public void should_close_without_asking_when_nothing_is_pending()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            window.Close();
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, Visible: window.IsVisible);
        });

        Assert.Equal(0, result.Asked);
        Assert.False(result.Visible);
    }

    [Fact]
    public void should_cancel_a_drag_and_a_confirm_overlay_without_server_calls_before_the_guard_when_closed()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            var map = env.Console!.Map;
            map.CompleteDrag(new UnitMapDropRequest(27, 0, 0, 6, false));   // 27중대를 6중대 아래로 → 확인 오버레이(서버 0)
            var overlay = map.IsConfirming;
            map.BeginDrag(6);                                                // 오버레이 중 두 번째 끌기는 시작되지 않는다 — 끌기는 따로 본다

            window.Close();
            Sta.PumpUntil(() => !window.IsVisible);
            return (Overlay: overlay, StillConfirming: map.IsConfirming, Patches: env.Units.Patches, Closed: !window.IsVisible);
        });

        Assert.True(result.Overlay);
        Assert.False(result.StillConfirming);        // 서버 0 으로 거뒀다(SIM-F126)
        Assert.Equal(0, result.Patches);
        Assert.True(result.Closed);                  // 남은 것 없음 → 묻지 않고 닫힘
    }

    [Fact]
    public void should_cancel_a_running_drag_without_server_calls_when_closed()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            var map = env.Console!.Map;
            map.BeginDrag(27);
            var dragging = map.IsDragging;

            window.Close();
            Sta.PumpUntil(() => !window.IsVisible);
            return (Dragging: dragging, StillDragging: map.IsDragging, Patches: env.Units.Patches, Closed: !window.IsVisible);
        });

        Assert.True(result.Dragging);
        Assert.False(result.StillDragging);
        Assert.Equal(0, result.Patches);
        Assert.True(result.Closed);
    }
    #endregion

    #region - 실제 모달 확인 창 + UIA 닫기 (헤디드 4회차 SC-UNT-003 · SC-UNT-022.close) -
    /// <summary>
    /// UIA WindowPattern.Close 와 같은 길 — WPF 창의 WindowPattern 은 HWND 프록시가 내며 <c>WM_SYSCOMMAND(SC_CLOSE)</c> 를 보낸다
    /// (→ DefWindowProc → WM_CLOSE → <c>Closing</c> → 창 지휘자). 같은 스레드라 게시하고 다음 펌프에서 처리된다.
    /// </summary>
    private static void UiaClose(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Assert.NotEqual(IntPtr.Zero, hwnd);
        PostMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_CLOSE = 0xF060;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void should_keep_the_window_on_no_and_close_it_on_yes_when_closed_twice_through_uia_with_a_real_confirm()
    {
        var result = Sta.Run(env =>
        {
            env.Windows.RealDialogs = true;
            env.Open();
            var window = env.ConsoleWindow!;
            env.MakeDirty();

            env.Windows.ConfirmAnswer = false;
            UiaClose(window);                                             // ✕ → 확인 [아니오]
            Sta.PumpUntil(() => env.Windows.Confirms == 1 && env.Windows.Read.Count == 1);
            Sta.Pump();
            var aliveAfterNo = window.IsVisible;

            env.Windows.ConfirmAnswer = true;
            UiaClose(window);                                             // 다시 ✕ → [예]
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, AliveAfterNo: aliveAfterNo, ClosedAfterYes: !window.IsVisible,
                    Read: env.Windows.Read.ToList(), Released: !env.Console!.IsActive);
        });

        Assert.Equal(2, result.Asked);
        Assert.True(result.AliveAfterNo);
        Assert.True(result.ClosedAfterYes);                               // 헤디드 4회차: 닫히지 않았다
        Assert.True(result.Released);
        Assert.All(result.Read, r =>
        {
            Assert.Equal("부대 편제 닫기", r.Title);                       // UIA 로 읽히는 제목
            Assert.Contains("부대 정보 변경", r.Message);                  // UIA 로 읽히는 문장
        });
    }

    [Fact]
    public void should_close_through_uia_without_a_prompt_when_nothing_is_pending_with_real_dialogs()
    {
        var result = Sta.Run(env =>
        {
            env.Windows.RealDialogs = true;
            env.Open();
            var window = env.ConsoleWindow!;

            UiaClose(window);
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, Closed: !window.IsVisible);
        });

        Assert.Equal(0, result.Asked);
        Assert.True(result.Closed);
    }
    #endregion

    #region - 로그아웃 (FR-43 · ISSUE-45) -
    [Fact]
    public void should_close_the_real_window_without_asking_when_the_session_expires()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            env.MakeDirty();

            env.Session.Fire(EnumRevokeReason.TokenExpired);
            Sta.Wait(env.Launcher.SessionWork);
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, Visible: window.IsVisible);
        });

        Assert.Equal(0, result.Asked);
        Assert.False(result.Visible);
    }

    [Fact]
    public void should_drop_a_late_server_answer_and_read_again_when_the_console_is_reopened()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var first = env.Console!;
            env.Units.HoldPatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var move = first.MoveAsync(27, 6);                             // PATCH 응답 대기 중

            env.Session.Fire(EnumRevokeReason.SessionRevoked);             // 닫힘
            Sta.Wait(env.Launcher.SessionWork);
            env.Units.HoldPatch.SetResult();                               // 늦은 응답
            Sta.Wait(move);
            var readsBefore = env.Units.GraphReads;

            env.Open();
            return (NewInstance: !ReferenceEquals(first, env.Console), ReadAgain: env.Units.GraphReads > readsBefore,
                    FreshSelection: env.Console!.SelectedRow is null);
        });

        Assert.True(result.NewInstance);
        Assert.True(result.ReadAgain);               // 다음 열기는 편제를 새로 읽는다
        Assert.True(result.FreshSelection);          // 옛 창의 응답이 새 창을 건드리지 않는다
    }
    #endregion

    #region - 셸 종료 (V-17 헤드리스 부분) -
    /// <summary>
    /// V-17 헤드리스 관찰(2026-09-28): 소유자(셸)가 닫히면 WPF 가 소유 창을 <b>Closing 없이</b> 함께 없앤다 — 콘솔의 <c>CanCloseAsync</c> 는
    /// 불리지 않는다. 이 시험은 그 WPF 사실을 고정한다. 그래서 셸 종료에서 묻는 일은 창 수준이 아니라 호스트 셸의 종료 관문
    /// (<c>ShellExitGate</c> — U-27)이 셸을 닫기 <b>전에</b> <see cref="IGuardedWindowRegistry"/> 로 한다(위 「셸 종료 관문」 시험). 인스턴스는 놓는다.
    /// </summary>
    [Fact]
    public void should_close_with_the_shell_without_running_the_console_guard_and_release_the_instance()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            var console = env.Console!;
            env.MakeDirty();

            env.Shell.Close();                                               // 셸(소유자) 종료 — WPF 는 소유 창을 함께 닫는다
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, ConsoleVisible: window.IsVisible, ConsoleActive: console.IsActive);
        });

        Assert.Equal(0, result.Asked);               // 관찰: 가드는 불리지 않는다(V-17)
        Assert.False(result.ConsoleVisible);         // 콘솔은 셸과 함께 닫힌다(남아 떠돌지 않는다)
        Assert.False(result.ConsoleActive);          // 닫힘 비활성화 → 런처가 인스턴스를 놓는다
    }
    #endregion

    #region - 셸 종료 관문 (U-27) -
    // 호스트 셸은 종료 전에 이 목록의 창에 차례로 묻는다(ShellExitGate). 여기서는 런처 쪽 약속만 잠근다 —
    // 띄우면 올리고 · 닫히면 내리고 · 물으면 ✕ 와 같은 가드를 지나고 · 앞으로 가져오고 · 동의 뒤에는 다시 묻지 않고 닫는다.

    [Fact]
    public void should_register_the_open_console_for_the_shell_exit_and_release_it_when_closed()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var whileOpen = env.Registry.OpenWindows.Count;
            var window = env.ConsoleWindow!;
            window.Close();                                              // ✕ (남은 것 없음 → 묻지 않고 닫힌다)
            Sta.PumpUntil(() => !window.IsVisible);
            return (WhileOpen: whileOpen, AfterClose: env.Registry.OpenWindows.Count);
        });

        Assert.Equal(1, result.WhileOpen);
        Assert.Equal(0, result.AfterClose);
    }

    [Fact]
    public void should_register_the_console_only_once_when_opened_again()
    {
        var count = Sta.Run(env =>
        {
            env.Open();
            env.Open();                                                  // 이미 떠 있다 → 앞으로 가져올 뿐
            return env.Registry.OpenWindows.Count;
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void should_ask_the_console_guard_and_keep_the_window_when_the_shell_exit_asks_and_the_user_refuses()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            env.MakeDirty();
            env.Windows.ConfirmAnswer = false;

            var entry = env.Registry.OpenWindows.Single();
            var ask = entry.CanCloseAsync();
            Sta.Wait(ask);
            Sta.Pump();
            return (Answer: ask.Result, Asked: env.Windows.Confirms, Visible: window.IsVisible, Registered: env.Registry.OpenWindows.Count);
        });

        Assert.False(result.Answer);                 // 종료를 막는다
        Assert.Equal(1, result.Asked);               // ✕ 와 같은 확인을 한 번 묻는다
        Assert.True(result.Visible);                 // 창과 편집은 그대로
        Assert.Equal(1, result.Registered);
    }

    [Fact]
    public void should_agree_without_asking_when_the_shell_exit_asks_and_nothing_is_pending()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var ask = env.Registry.OpenWindows.Single().CanCloseAsync();
            Sta.Wait(ask);
            return (Answer: ask.Result, Asked: env.Windows.Confirms);
        });

        Assert.True(result.Answer);
        Assert.Equal(0, result.Asked);
    }

    [Fact]
    public void should_restore_a_minimized_console_when_the_shell_exit_brings_it_to_front()
    {
        var state = Sta.Run(env =>
        {
            env.Open();
            env.ConsoleWindow!.WindowState = WindowState.Minimized;
            Sta.Pump();
            env.Registry.OpenWindows.Single().BringToFront();
            Sta.Pump();
            return env.ConsoleWindow.WindowState;
        });

        Assert.Equal(WindowState.Normal, state);
    }

    [Fact]
    public void should_close_a_dirty_console_without_asking_and_release_it_when_the_shell_exit_closes_it()
    {
        var result = Sta.Run(env =>
        {
            env.Open();
            var window = env.ConsoleWindow!;
            var console = env.Console!;
            env.MakeDirty();

            Sta.Wait(env.Registry.OpenWindows.Single().CloseWithoutAskingAsync());
            Sta.PumpUntil(() => !window.IsVisible);
            return (Asked: env.Windows.Confirms, Visible: window.IsVisible, Active: console.IsActive, Registered: env.Registry.OpenWindows.Count);
        });

        Assert.Equal(0, result.Asked);               // 사람은 이미 답했다(또는 OS 가 끝내는 중) — 다시 묻지 않는다
        Assert.False(result.Visible);
        Assert.False(result.Active);
        Assert.Equal(0, result.Registered);
    }

    [Fact]
    public void should_release_the_console_from_the_shell_exit_when_the_session_expires()
    {
        var count = Sta.Run(env =>
        {
            env.Open();
            env.Session.Fire(EnumRevokeReason.TokenExpired);
            Sta.Wait(env.Launcher.SessionWork);
            Sta.Pump();
            return env.Registry.OpenWindows.Count;
        });

        Assert.Equal(0, count);
    }
    #endregion

    #region - Fixture -
    private sealed class Env
    {
        public Window Shell { get; }
        public HybridWindows Windows { get; } = new();
        public StubSession Session { get; } = new();
        public GraphStub Units { get; } = new();
        public UnitConsoleLauncher Launcher { get; }
        public EventAggregator Events { get; } = new();

        /// <summary>셸 종료 관문이 읽는 목록(U-27) — 런처가 콘솔을 올리고 내린다.</summary>
        public GuardedWindowRegistry Registry { get; } = new();

        public Env()
        {
            Shell = new Window
            {
                Title = "셸", Width = 400, Height = 300, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false,
            };
            Shell.Show();
            Launcher = new UnitConsoleLauncher(Windows, Units, new DeviceStub(), events: Events, session: Session,
                                               host: new WpfUnitConsoleWindowHost(() => Shell), prefs: () => null,
                                               guardedWindows: Registry);
        }

        public UnitConsoleViewModel? Console => Windows.ShownWindows.LastOrDefault();

        public Window? ConsoleWindow => Console is IViewAware aware && aware.GetView() is DependencyObject view ? Window.GetWindow(view) : null;

        /// <summary>띄운 콘솔 창들(닫힌 뒤에는 뷰모델이 뷰를 놓으므로 여기서 기억해 정리한다).</summary>
        public List<Window> Opened { get; } = new();

        public void Open()
        {
            Sta.Wait(Launcher.OpenAsync());
            Sta.Pump();
            if (ConsoleWindow is { } window && !Opened.Contains(window)) Opened.Add(window);
        }

        public void MakeDirty()
        {
            Sta.Wait(Console!.SelectByIdAsync(6));
            Console.Form.Description = "손댄 설명";
            Assert.True(Console.Detail.IsDirty);
        }
    }

    /// <summary>콘솔 창은 진짜 Caliburn 창 관리자로(화면 밖 · 활성화 없음), 확인 창은 기록만.</summary>
    private sealed class HybridWindows : IWindowManager
    {
        private readonly WindowManager _real = new();
        public List<UnitConsoleViewModel> ShownWindows { get; } = new();
        public bool ConfirmAnswer { get; set; } = true;
        public int Confirms { get; private set; }

        /// <summary>
        /// 켜면 확인 창을 <b>진짜 모달</b>(실제 <see cref="ConfirmPromptView"/>, 소유자 = 런처가 준 콘솔 창)로 띄우고, 모달 루프 안에서
        /// 사람처럼 단추를 자동화 Invoke 로 누른다(UIA InvokePattern 과 같은 길). 끄면 기록만 하고 곧바로 답한다.
        /// </summary>
        public bool RealDialogs { get; set; }

        /// <summary>진짜 모달에서 읽은 제목 · 문장(UIA 로 읽히는 그대로).</summary>
        public List<(string Title, string Message)> Read { get; } = new();

        public async Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            if (rootModel is not ConfirmPromptViewModel prompt) throw new InvalidOperationException("이 시험에서 다른 모달은 없다");
            Confirms++;
            if (!RealDialogs)
            {
                if (ConfirmAnswer) await prompt.AcceptAsync();
                return ConfirmAnswer;
            }

            var answer = ConfirmAnswer;
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += (_, _) =>
            {
                if (prompt.GetView() is not FrameworkElement view || Window.GetWindow(view) is not { IsLoaded: true } window) return;
                timer.Stop();
                Read.Add((ReadText(window, "Dialog.Devices.Assembly.Confirm.Title") ?? window.Title,
                          ReadText(window, "Devices.Assembly.Confirm.Message") ?? string.Empty));
                InvokeById(window, answer ? "Devices.Assembly.Confirm.Accept" : "Devices.Assembly.Confirm.Cancel");
            };
            timer.Start();

            var saved = ViewLocator.LocateForModel;
            ViewLocator.LocateForModel = (_, _, _) => new ConfirmPromptView();
            try { return await _real.ShowDialogAsync(rootModel, context, settings); }
            finally { ViewLocator.LocateForModel = saved; timer.Stop(); }
        }

        private static string? ReadText(DependencyObject root, string automationId)
        {
            var element = Find(root, automationId);
            if (element is null) return null;
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(element);
            return peer?.GetName();
        }

        private static void InvokeById(DependencyObject root, string automationId)
        {
            var element = Find(root, automationId) ?? throw new InvalidOperationException($"{automationId} 가 없다");
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(element)!;
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
        }

        private static UIElement? Find(DependencyObject root, string automationId)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is UIElement ui && System.Windows.Automation.AutomationProperties.GetAutomationId(ui) == automationId) return ui;
                if (Find(child, automationId) is { } found) return found;
            }
            return null;
        }

        public async Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            var offscreen = new Dictionary<string, object>(settings ?? new Dictionary<string, object>())
            {
                ["WindowStartupLocation"] = WindowStartupLocation.Manual,
                ["Left"] = -20000.0,
                ["Top"] = -20000.0,
                ["ShowActivated"] = false,
            };
            ShownWindows.Add((UnitConsoleViewModel)rootModel);
            var saved = ViewLocator.LocateForModel;
            ViewLocator.LocateForModel = (_, _, _) => new Grid();          // 가벼운 대역 뷰(실제 뷰는 앱 사전이 있어야 선다)
            try { await _real.ShowWindowAsync(rootModel, context, offscreen); }
            finally { ViewLocator.LocateForModel = saved; }
        }

        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
            => throw new InvalidOperationException("팝업은 없다");
    }

    private sealed class StubSession : ISessionLifecycle
    {
        public event System.Action<EnumRevokeReason>? ForceLogoutRequested;
        public event System.Action? LoginSucceeded;
        public void Fire(EnumRevokeReason reason) => ForceLogoutRequested?.Invoke(reason);
        public void ForceLogoutOnce(EnumRevokeReason reason) => Fire(reason);
        public void ResetForLogin() { }
        public void NotifyLoginSucceeded() => LoginSucceeded?.Invoke();
    }

    /// <summary>사단(1) 아래 6중대 · 27중대. PATCH 를 붙잡을 수 있다.</summary>
    private sealed class GraphStub : IUnitGraphApi
    {
        private readonly List<UnitListDto> _nodes = new()
        {
            new() { Id = 1, Code = "d01", Name = "사단", EchelonRaw = "Division", IsEnable = true },
            new() { Id = 6, Code = "c06", Name = "6중대", EchelonRaw = "Company", ParentId = 1, IsEnable = true },
            new() { Id = 27, Code = "c27", Name = "27중대", EchelonRaw = "Company", ParentId = 1, IsEnable = true },
        };

        public bool IsAvailable => true;
        public int GraphReads { get; private set; }
        public int Patches { get; private set; }
        public TaskCompletionSource? HoldPatch { get; set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            GraphReads++;
            return Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(new UnitGraphDto
            {
                Nodes = _nodes.Select(n => new UnitListDto { Id = n.Id, Code = n.Code, Name = n.Name, EchelonRaw = n.EchelonRaw, ParentId = n.ParentId, IsEnable = true }).ToList(),
                Edges = new UnitGraphEdgesDto
                {
                    Hierarchy = _nodes.Where(n => n.ParentId is int).Select(n => new List<int> { n.ParentId!.Value, n.Id }).ToList(),
                    Adjacency = new List<List<int>>(),
                },
            }));
        }

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            var node = _nodes.First(n => n.Id == unitId);
            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = node.Id, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId, IsEnable = true,
                AdjacentUnitIds = new List<int>(),
            }));
        }

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));

        public async Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
        {
            Patches++;
            if (HoldPatch is { } hold) await hold.Task.ConfigureAwait(true);
            return ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId });
        }

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto()));
    }

    private sealed class DeviceStub : IUnitDeviceApi
    {
        public bool IsAvailable => true;
        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default) => Task.FromResult(UnitDeviceLoadResult.Empty);
        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "ok"));
    }

    /// <summary>STA 스레드 하나에서 디스패처 동기화 문맥으로 본문을 돌린다(시험마다 새 스레드 · 창은 끝에 모두 닫는다).</summary>
    private static class Sta
    {
        public static T Run<T>(Func<Env, T> body)
        {
            T result = default!;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                var savedIoC = IoC.GetInstance;
                IoC.GetInstance = (_, _) => null!;                           // 권한 게이트 = 허용 폴백
                var savedPlatform = PlatformProvider.Current;
                PlatformProvider.Current = new XamlPlatformProvider();       // 호스트와 같은 창 닫기 길(TryCloseAsync → 창 닫기)
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                Env? env = null;
                try
                {
                    env = new Env();
                    result = body(env);
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    try
                    {
                        // 창을 남긴 채 디스패처를 끄면 다른 시험 스레드의 방송 메시지가 주인 없는 HWND 에 닿아 시험 호스트가 죽는다
                        // (HwndSubclass NRE → FailFast). 가드가 "닫지 않음" 을 고른 창도 있으니 수락으로 바꾸고 닫힐 때까지 돌린다.
                        if (env is not null)
                        {
                            env.Windows.ConfirmAnswer = true;
                            foreach (var window in env.Opened) window.Close();
                            Sta.PumpUntil(() => env.Opened.All(w => !w.IsVisible));
                            env.Shell.Close();
                            Sta.Pump();
                            env.Launcher.Dispose();
                        }
                    }
                    catch { /* 정리 실패는 결과에 영향이 없다 */ }
                    IoC.GetInstance = savedIoC;
                    PlatformProvider.Current = savedPlatform;
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        public static void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
        }

        /// <summary>창 지휘자의 닫기는 비동기다(Closing 취소 → 가드 → 다시 Close) — 조건이 설 때까지 돌린다(최대 5 s).</summary>
        public static void PumpUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline) Pump();
            Pump();
        }

        public static void Wait(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            if (!task.IsCompleted) throw new TimeoutException("작업이 끝나지 않았다");
            task.GetAwaiter().GetResult();
        }
    }
    #endregion
}
