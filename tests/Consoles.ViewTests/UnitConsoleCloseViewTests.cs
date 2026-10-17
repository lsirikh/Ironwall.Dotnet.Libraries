using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 부대 콘솔 <b>실제 창</b>의 닫기 — 헤디드 4회차 SC-UNT-003 · SC-UNT-022.close(비모달 전환 뒤 UIA <c>WindowPattern.Close</c> 로
/// 창이 닫히지 않고 확인 창도 뜨지 않았다). 호스트와 같은 길로 세운다: 런처 → Caliburn 창 관리자 → 실제 <see cref="UnitConsoleView"/>
/// (커널 창 겉 포함) · 소유자 = 셸 창 · 확인 창 = 실제 <see cref="ConfirmPromptView"/> 모달. 닫기는 <b>UIA 클라이언트</b>의
/// <see cref="WindowPattern.Close"/> 를 다른 스레드에서 부른다(FlaUI <c>Window.Close()</c> 와 같은 길).
/// </summary>
public class UnitConsoleCloseViewTests
{
    [Fact]
    public void should_ask_keep_on_no_and_close_on_yes_when_the_real_console_window_is_closed_through_uia() => AppHost.Run(() =>
    {
        using var env = new CloseEnv();
        env.Open();
        env.MakeDirty();

        env.Windows.Answer = false;
        env.UiaClose();
        env.PumpUntil(() => env.Windows.Read.Count == 1, 8000);
        var askedFirst = env.Windows.Read.Count;
        env.PumpFor(300);
        var aliveAfterNo = env.ConsoleWindow.IsVisible;

        env.Windows.Answer = true;
        env.UiaClose();
        env.PumpUntil(() => !env.ConsoleWindow.IsVisible, 8000);

        Assert.True(askedFirst == 1, "첫 ✕ 에서 확인 창이 떠야 한다 — " + env.Trace());
        Assert.True(aliveAfterNo, "[아니오] 뒤 창은 남아야 한다");
        Assert.True(!env.ConsoleWindow.IsVisible, "[예] 뒤 창이 닫혀야 한다 — " + env.Trace());
        Assert.Equal(2, env.Windows.Read.Count);
        Assert.All(env.Windows.Read, r =>
        {
            Assert.Equal("부대 편제 닫기", r.Title);                          // UIA 로 읽히는 제목(창 이름)
            Assert.Contains("부대 정보 변경", r.Message);                     // UIA 로 읽히는 문장(Devices.Assembly.Confirm.Message)
        });
    });

    [Fact]
    public void should_close_without_a_prompt_when_the_real_console_window_is_closed_through_uia_with_nothing_pending() => AppHost.Run(() =>
    {
        using var env = new CloseEnv();
        env.Open();

        env.UiaClose();
        env.PumpUntil(() => !env.ConsoleWindow.IsVisible, 8000);

        Assert.True(!env.ConsoleWindow.IsVisible, "남은 것이 없으면 곧바로 닫혀야 한다 — " + env.Trace());
        Assert.Empty(env.Windows.Read);
    });

    /// <summary>
    /// 자동화가 창을 찾는 길의 근거(4회차 실패 분석) — 비모달 콘솔은 UIA 에서 <b>셸 창의 자식</b>이고 <c>IsModal=false</c> 다.
    /// 그래서 "최상위 창 + 그 모달 자식" 만 보는 찾기(FlaUI <c>ModalWindows</c>)에는 콘솔이 후보로 없고, 콘솔의 식별자를 품은 가장 작은 후보가
    /// <b>셸</b>이 된다 — 그 창에 <c>Close()</c> 를 부르면 콘솔이 아니라 셸을 닫으려 한다. 확인 창(모달)은 콘솔의 자식으로 뜬다.
    /// </summary>
    [Fact]
    public void should_place_the_non_modal_console_under_the_shell_and_the_modal_confirm_under_the_console_in_uia() => AppHost.Run(() =>
    {
        using var env = new CloseEnv();
        env.Open();
        var shell = new WindowInteropHelper(env.Shell).Handle;
        var console = new WindowInteropHelper(env.ConsoleWindow).Handle;

        var probe = Task.Run(() =>
        {
            var shellElement = AutomationElement.FromHandle(shell);
            var windows = shellElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window))
                                      .Cast<AutomationElement>().ToList();
            var consoleChild = windows.FirstOrDefault(w => w.Current.NativeWindowHandle == console.ToInt32());
            var modal = consoleChild is null ? (bool?)null : ((WindowPattern)consoleChild.GetCurrentPattern(WindowPattern.Pattern)).Current.IsModal;
            var modalChildren = shellElement.FindAll(TreeScope.Children, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                new PropertyCondition(WindowPattern.IsModalProperty, true))).Count;
            var rail = shellElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Console.Units.Rail"));
            return (UnderShell: consoleChild is not null, IsModal: modal, ModalChildren: modalChildren, ShellContainsConsoleIds: rail is not null);
        });
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!probe.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        var found = probe.GetAwaiter().GetResult();

        env.MakeDirty();
        env.Windows.Answer = false;
        env.UiaClose();
        env.PumpUntil(() => env.Windows.Placement.Count == 1, 8000);

        Assert.True(found.UnderShell, "콘솔 창은 UIA 에서 셸의 자식이다");
        Assert.False(found.IsModal);                                     // 모달 체인으로 찾는 자동화에는 보이지 않는다
        Assert.Equal(0, found.ModalChildren);
        Assert.True(found.ShellContainsConsoleIds);                      // 셸 창이 콘솔의 식별자를 품는다 → "가장 작은 후보" 가 셸
        Assert.Equal(("부대 편제", true), env.Windows.Placement.Single()); // 확인 창 = 콘솔의 모달 자식
    });

    #region - Fixture -
    private sealed class CloseEnv : IDisposable
    {
        private readonly Func<object, DependencyObject?, object?, UIElement> _savedLocator;
        private readonly Window _shell;
        public Window Shell => _shell;
        public OffscreenWindows Windows { get; } = new();
        public UnitConsoleLauncher Launcher { get; }
        public UnitConsoleViewModel Console => Windows.Consoles.Last();
        /// <summary>연 창(닫히면 뷰모델이 뷰를 놓으므로 열 때 쥔다).</summary>
        public Window ConsoleWindow { get; private set; } = null!;

        public CloseEnv()
        {
            var events = new EventAggregator();
            RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);
            PlatformProvider.Current = new XamlPlatformProvider();
            _savedLocator = ViewLocator.LocateForModel;
            ViewLocator.LocateForModel = (model, _, _) => model switch
            {
                UnitConsoleViewModel => new UnitConsoleView(),
                ConfirmPromptViewModel => new ConfirmPromptView(),
                _ => throw new InvalidOperationException(model.GetType().Name),
            };

            _shell = new Window
            {
                Title = "셸", Width = 600, Height = 400, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000,
            };
            _shell.Show();
            Launcher = new UnitConsoleLauncher(Windows, Units(), Devices(), events: events,
                                               host: new WpfUnitConsoleWindowHost(() => _shell), prefs: () => null);
        }

        public void Open()
        {
            RailProbe.Wait(Launcher.OpenAsync());
            AppHost.Pump(DispatcherPriority.Loaded);
            AppHost.Pump();
            ConsoleWindow = Window.GetWindow((DependencyObject)((IViewAware)Console).GetView())!;
            Assert.True(ConsoleWindow.IsVisible);
        }

        public void MakeDirty()
        {
            RailProbe.Wait(Console.SelectByIdAsync(6));
            Console.Form.Name = "6중대 (개편)";
            AppHost.Pump();
            Assert.True(Console.Detail.IsDirty);
        }

        /// <summary>UIA 클라이언트 WindowPattern.Close — 다른 스레드에서(같은 스레드면 UIA 가 자기 창에 묶인다). 부르는 동안 UI 는 돈다.</summary>
        public void UiaClose()
        {
            var hwnd = new WindowInteropHelper(ConsoleWindow).Handle;
            Exception? failure = null;
            var call = Task.Run(() =>
            {
                try
                {
                    var element = AutomationElement.FromHandle(hwnd);
                    ((WindowPattern)element.GetCurrentPattern(WindowPattern.Pattern)).Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!call.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
            Assert.True(call.IsCompleted, "UIA Close 가 돌아오지 않는다");
            if (failure is not null) throw new InvalidOperationException("UIA Close 실패", failure);
        }

        public void PumpUntil(Func<bool> done, int ms)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(ms);
            while (!done() && DateTime.UtcNow < deadline) PumpFor(20);
        }

        public void PumpFor(int ms)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(ms), DispatcherPriority.Background, (s, _) => { ((DispatcherTimer)s!).Stop(); frame.Continue = false; }, Dispatcher.CurrentDispatcher);
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        public string Trace() => $"묻기 {Windows.Read.Count} · 창 보임 {ConsoleWindow.IsVisible} · 사용 가능 {ConsoleWindow.IsEnabled} · 활성 VM {Console.IsActive}";

        public void Dispose()
        {
            try
            {
                Windows.Answer = true;
                if (ConsoleWindow is { IsVisible: true } w) w.Close();
                PumpFor(100);
                _shell.Close();
                Launcher.Dispose();
            }
            finally { ViewLocator.LocateForModel = _savedLocator; }
        }

        private static IUnitGraphApi Units()
        {
            var graph = new UnitGraphDto
            {
                Nodes = new List<UnitListDto>
                {
                    new() { Id = 1, Code = "d01", Name = "d01", EchelonRaw = "Division", IsEnable = true },
                    new() { Id = 3, Code = "b01", Name = "b01", EchelonRaw = "Battalion", ParentId = 1, IsEnable = true },
                    new() { Id = 6, Code = "c06", Name = "c06", EchelonRaw = "Company", ParentId = 3, IsEnable = true },
                },
                Edges = new UnitGraphEdgesDto
                {
                    Hierarchy = new List<List<int>> { new() { 1, 3 }, new() { 3, 6 } },
                    Adjacency = new List<List<int>>(),
                },
            };
            var units = new Mock<IUnitGraphApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
            units.SetupGet(u => u.IsAvailable).Returns(true);
            units.Setup(u => u.GetGraphAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => ApiResponse<UnitGraphDto>.CreateSuccess(graph));
            units.Setup(u => u.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((int id, CancellationToken _) =>
                 {
                     var node = graph.Nodes.First(n => n.Id == id);
                     return ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
                     {
                         Id = id, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId,
                         IsEnable = true, AdjacentUnitIds = new List<int>(),
                     });
                 });
            return units.Object;
        }

        private static IUnitDeviceApi Devices()
        {
            var devices = new Mock<IUnitDeviceApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
            devices.SetupGet(d => d.IsAvailable).Returns(true);
            devices.Setup(d => d.LoadAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UnitDeviceLoadResult(Array.Empty<UnitDeviceItem>(), Array.Empty<string>()));
            return devices.Object;
        }
    }

    /// <summary>진짜 Caliburn 창 관리자 — 콘솔 창만 화면 밖으로 옮기고(소유자는 그대로), 확인 창은 진짜 모달로 띄워 UIA 로 읽고 누른다.</summary>
    private sealed class OffscreenWindows : IWindowManager
    {
        private readonly WindowManager _real = new();
        public List<UnitConsoleViewModel> Consoles { get; } = new();
        public List<(string Title, string Message)> Read { get; } = new();

        /// <summary>확인 창의 UIA 부모 이름 · 그 창의 WindowPattern.IsModal — 자동화가 창을 찾는 길을 기록한다.</summary>
        public List<(string Parent, bool IsModal)> Placement { get; } = new();
        public bool Answer { get; set; } = true;

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Consoles.Add((UnitConsoleViewModel)rootModel);
            var offscreen = new Dictionary<string, object>(settings ?? new Dictionary<string, object>())
            {
                ["WindowStartupLocation"] = WindowStartupLocation.Manual,
                ["Left"] = -20000.0,
                ["Top"] = -20000.0,
                ["ShowActivated"] = false,
            };
            return _real.ShowWindowAsync(rootModel, context, offscreen);
        }

        public Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            var prompt = (ConfirmPromptViewModel)rootModel;
            var answer = Answer;
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += (_, _) =>
            {
                if (((IViewAware)prompt).GetView() is not DependencyObject view || Window.GetWindow(view) is not { IsLoaded: true } window) return;
                timer.Stop();
                var hwnd = new WindowInteropHelper(window).Handle;
                // 자동화가 읽는 그대로 — 다른 스레드의 UIA 클라이언트로 제목(창 이름) · 문장을 읽고 단추를 누른다.
                var result = Task.Run(() =>
                {
                    var root = AutomationElement.FromHandle(hwnd);
                    var message = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Devices.Assembly.Confirm.Message"));
                    var button = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty,
                                                answer ? "Devices.Assembly.Confirm.Accept" : "Devices.Assembly.Confirm.Cancel"));
                    var read = (root.Current.Name, message?.Current.Name ?? string.Empty);
                    var parent = TreeWalker.ControlViewWalker.GetParent(root);
                    Placement.Add((parent?.Current.Name ?? "", ((WindowPattern)root.GetCurrentPattern(WindowPattern.Pattern)).Current.IsModal));
                    ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    return read;
                });
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!result.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
                Read.Add(result.GetAwaiter().GetResult());
            };
            timer.Start();
            return _real.ShowDialogAsync(rootModel, context, settings);
        }

        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
            => throw new InvalidOperationException("팝업은 없다");
    }
    #endregion
}
