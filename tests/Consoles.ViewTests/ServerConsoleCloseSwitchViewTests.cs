using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 서버 콘솔 닫기 확인이 <b>실제 확인 창</b>(창 관리자 · ConfirmPromptView · 중첩 메시지 루프)으로 뜰 때 —
/// 좌측 메뉴 전환(메시지 → 패널 층 전환 → 닫기 판정)이 [아니오] 면 멈추고, 다시 눌러 [예] 면 그대로 이어지는가.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 SC-SHL-066: [아니오] 뒤 다시 로그아웃 → [예] 에서 서버 창이 남고 로그아웃 카드가 안 떴다.
/// 호스트 결선(PanelShellViewModel.ActivateItemAsync → PanelLeaveRule.ShouldBlockAsync → CanCloseAsync)을 같은 모양으로 세우고
/// 호스트 메뉴처럼 <c>PublishOnUIThreadAsync</c> 로 연다. 확인 창의 단추는 UIA 와 같은 길(ButtonAutomationPeer Invoke)로 누른다.
/// 확인 창은 화면 밖에 활성화 없이 띄운다(헤디드 실행 중인 데스크톱을 건드리지 않는다).
/// </remarks>
public class ServerConsoleCloseSwitchViewTests
{
    [Fact]
    public void should_continue_to_the_next_card_when_the_close_prompt_is_declined_then_accepted() => AppHost.Run(() =>
    {
        PlatformProvider.Current = new XamlPlatformProvider();   // 호스트와 같이 — 창 닫기 동작이 실제 창을 닫는다
        if (!AssemblySource.Instance.Contains(typeof(ConfirmPromptView).Assembly)) AssemblySource.Instance.Add(typeof(ConfirmPromptView).Assembly);
        var answers = new Queue<bool>(new[] { false, true });
        var asked = new List<string>();
        using var answerer = new PromptAnswerer(answers, asked);

        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);
        var console = BuildConsole(events, new ServerConsoleDialogs(new OffscreenWindowManager(), ServiceMock().Object, Clock()));
        var shell = new LeaveGuardedShell();
        var logout = new Screen { DisplayName = "로그아웃" };
        var menu = new MenuHandler(shell, logout);
        events.SubscribeOnUIThread(menu);

        RailProbe.Wait(((IActivate)shell).ActivateAsync());
        RailProbe.Wait(shell.ActivateItemAsync(console));
        console.OnRowsSelected(new List<object> { console.Rows[0] });
        console.BeginEdit();
        console.NameText = "고친 이름";
        AppHost.Pump();
        Assert.True(console.Detail.IsDirty);

        // 첫 번째 로그아웃 → [아니오] → 서버 창 유지
        RailProbe.Wait(events.PublishOnUIThreadAsync(new OpenLogoutCard()));
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Single(asked);
        Assert.Same(console, shell.ActiveItem);

        // 두 번째 로그아웃 → [예] → 서버 창을 버리고 로그아웃 카드로
        RailProbe.Wait(events.PublishOnUIThreadAsync(new OpenLogoutCard()));
        AppHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(2, asked.Count);
        Assert.Same(logout, shell.ActiveItem);
        Assert.False(console.IsActive);
        Assert.Empty(answers);
    });

    #region - 호스트와 같은 모양의 결선 -
    /// <summary>호스트 PanelShellViewModel 의 전환 규칙(PanelLeaveRule.ShouldBlockAsync)과 같다 — 떠날 창의 닫기 판정을 먼저 묻는다.</summary>
    private sealed class LeaveGuardedShell : Conductor<Screen>.Collection.OneActive
    {
        public override async Task ActivateItemAsync(Screen item, CancellationToken cancellationToken = default)
        {
            if (ActiveItem is not null && item is not null && !ReferenceEquals(ActiveItem, item)
                && ActiveItem is IGuardClose closable && !await closable.CanCloseAsync(cancellationToken).ConfigureAwait(true))
                return;
            await base.ActivateItemAsync(item!, cancellationToken);
        }
    }

    private sealed class OpenLogoutCard { }

    /// <summary>호스트 ConductorControlViewModel.HandleAsync(OpenLogoutPanelMessageModel) 와 같다.</summary>
    private sealed class MenuHandler : IHandle<OpenLogoutCard>
    {
        private readonly LeaveGuardedShell _shell;
        private readonly Screen _card;
        public MenuHandler(LeaveGuardedShell shell, Screen card) { _shell = shell; _card = card; }
        public Task HandleAsync(OpenLogoutCard message, CancellationToken cancellationToken) => _shell.ActivateItemAsync(_card, cancellationToken);
    }

    /// <summary>확인 창을 화면 밖 · 비활성으로 띄운다 — 그 밖에는 창 관리자 그대로(모달 · 중첩 메시지 루프).</summary>
    private sealed class OffscreenWindowManager : WindowManager
    {
        protected override Window EnsureWindow(object model, object view, bool isDialog)
        {
            var window = base.EnsureWindow(model, view, isDialog);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            // 모달이어도 전경을 빼앗지 않는다 — 같은 데스크톱에서 헤디드 시험이 돌고 있을 수 있다.
            window.SourceInitialized += (_, _) =>
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                SetWindowLongPtr(handle, GwlExStyle, (IntPtr)(GetWindowLongPtr(handle, GwlExStyle).ToInt64() | WsExNoActivate | WsExToolWindow));
            };
            return window;
        }

        private const int GwlExStyle = -20;
        private const long WsExNoActivate = 0x08000000;
        private const long WsExToolWindow = 0x00000080;

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }

    /// <summary>뜬 확인 창을 찾아 UIA 와 같은 길(ButtonAutomationPeer Invoke)로 [예] · [아니오] 를 누른다.</summary>
    private sealed class PromptAnswerer : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly HashSet<Window> _answered = new();

        public PromptAnswerer(Queue<bool> answers, List<string> asked)
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) =>
            {
                var prompt = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsLoaded && w.DataContext is ConfirmPromptViewModel && !_answered.Contains(w));
                if (prompt is null || answers.Count == 0) return;
                var id = answers.Peek() ? "Devices.Assembly.Confirm.Accept" : "Devices.Assembly.Confirm.Cancel";
                var button = RailProbe.Find<Button>(prompt, b => AutomationProperties.GetAutomationId(b) == id);
                if (button is null) return;   // 템플릿이 아직이면 다음 박자에
                answers.Dequeue();
                _answered.Add(prompt);
                asked.Add(((ConfirmPromptViewModel)prompt.DataContext).Message);
                ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
            }, Dispatcher.CurrentDispatcher);
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();
    }
    #endregion

    #region - 서버 콘솔 -
    private static ServerMonitorViewModel BuildConsole(IEventAggregator events, IServerConsoleDialogs dialogs)
    {
        var console = new ServerMonitorViewModel(events, new MockLogService(), ServiceMock().Object, new DeviceProvider(), Clock(),
            new Lazy<IServerConsoleDialogs>(() => dialogs));
        return console;
    }

    private static Mock<IServerConsoleService> ServiceMock()
    {
        var servers = new List<ServerAxisView> { Entry(1, "프록시 1", EnumServerType.PROXY) };
        var service = new Mock<IServerConsoleService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        service.SetupGet(s => s.Contract).Returns(EnumServerContract.V8_0);
        service.SetupGet(s => s.IsUnitEra).Returns(true);
        service.SetupGet(s => s.IsAxisEra).Returns(true);
        service.Setup(s => s.LoadAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ServerLoadResult(servers, Array.Empty<ServerUnitOption>(), Array.Empty<ServerCategoryOption>(), false, null));
        service.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((int id, CancellationToken _) => servers.FirstOrDefault(s => s.Id == id));
        service.Setup(s => s.MetricHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<ServerMetricDto>?)Array.Empty<ServerMetricDto>());
        service.Setup(s => s.GetDeviceServerMapAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<int, int?>());
        return service;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        clock.SetupGet(c => c.Now).Returns(Now.ToLocalTime());
        return clock.Object;
    }

    private static readonly DateTime Now = new(2026, 9, 28, 0, 5, 0, DateTimeKind.Utc);

    private static ServerAxisView Entry(int id, string name, EnumServerType type) => new()
    {
        Id = id,
        TypeServer = type.ToString(),
        Name = name,
        IsEnable = true,
        UnitId = 4,
        Status = "NORMAL",
        HasStatusKey = true,
        StatusObservedAt = "2026-09-28T00:03:30+00:00",
        HasStatusObservedAtKey = true,
        IpAddress = $"10.0.0.{id}",
        Port = 8000 + id,
        Hostname = $"host-{id}",
        UserName = "admin",
        HasConnectionSection = true,
        HasConfigSection = true,
        CreatedAt = "2026-01-01T00:00:00+00:00",
        UpdatedAt = "2026-09-28T00:03:30+00:00",
    };
    #endregion
}
