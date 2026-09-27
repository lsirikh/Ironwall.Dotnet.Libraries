using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;

namespace Accounts.Ui.ViewTests;

/// <summary>뷰모델이 올린 메시지를 종류별로 센다 — 버튼 · ESC 가 뷰모델까지 닿았는지의 증거.</summary>
internal sealed class MessageRecorder :
    IHandle<ClosePanelMessageModel>,
    IHandle<CloseDialogMessageModel>,
    IHandle<OpenResetPasswordDialogMessageModel>,
    IHandle<OpenDeleteAccountDialogMessageModel>,
    IHandle<OpenConfirmPopupMessageModel>,
    IHandle<OpenInfoPopupMessageModel>
{
    public List<string> Seen { get; } = new();
    public int Count<T>() => Seen.Count(s => s == typeof(T).Name);

    private Task Add(object message) { Seen.Add(message.GetType().Name); return Task.CompletedTask; }
    public Task HandleAsync(ClosePanelMessageModel message, CancellationToken cancellationToken) => Add(message);
    public Task HandleAsync(CloseDialogMessageModel message, CancellationToken cancellationToken) => Add(message);
    public Task HandleAsync(OpenResetPasswordDialogMessageModel message, CancellationToken cancellationToken) => Add(message);
    public Task HandleAsync(OpenDeleteAccountDialogMessageModel message, CancellationToken cancellationToken) => Add(message);
    public Task HandleAsync(OpenConfirmPopupMessageModel message, CancellationToken cancellationToken) => Add(message);
    public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken) => Add(message);
}

/// <summary>한 창 — 뷰 · 뷰모델 · 메시지 기록 · 화면 밖 창.</summary>
internal sealed record Hosted(UserControl View, object ViewModel, MessageRecorder Messages, Window Window);

/// <summary>
/// B3 일곱 창의 뷰모델을 가짜 게이트웨이 위에 세우고, 호스트와 같은 길(Caliburn <c>View.SetModel</c>)로 뷰에 붙인다.
/// </summary>
internal static class SelfService
{
    public static AccountModel Sample() => new()
    {
        Id = 7,
        Username = "operator01",
        Name = "김관제",
        EmployeeNumber = "A-1024",
        Phone = "01012345678",
        EMail = "op01@example.com",
        Position = "주임",
        Department = "관제1팀",
        Role = EnumUserRole.USER,
        Used = EnumUsedType.USED,
    };

    private static readonly ILogService Log = Mock.Of<ILogService>();

    public static Mock<IProfileGateway> Profile(bool canSelfDelete = true)
    {
        var mock = new Mock<IProfileGateway>();
        mock.SetupGet(p => p.CanSelfDeleteAccount).Returns(canSelfDelete);
        mock.SetupGet(p => p.CanSelfEditEmployeeNumber).Returns(true);
        return mock;
    }

    /// <summary>뷰 이름 → (뷰, 뷰모델). 뷰모델 생성자는 호스트 DI 가 넘기는 것과 같은 모양.</summary>
    public static (UserControl View, object ViewModel) Build(string name, IEventAggregator events, bool canSelfDelete = true)
    {
        var login = new LoginViewModel(events, Log, Sample());
        return name switch
        {
            "Login" => (new LoginPanelView(), new LoginPanelViewModel(events, Log, new LoginViewModel(events, Log, new AccountModel()), new AccountProvider(), Mock.Of<IAuthGateway>())),
            "Logout" => (new LogoutPanelView(), new LogoutPanelViewModel(events, Log, login, Mock.Of<IAuthGateway>())),
            "MyPage" => (new MyPagePanelView(), new MyPagePanelViewModel(events, Log, login, Profile(canSelfDelete).Object, Mock.Of<IProfileImageService>())),
            "Editor" => (new EditorDialogView(), new EditorDialogViewModel(events, Log, new AccountViewModel(events, Log, Sample()),
                             Mock.Of<IUserDirectoryGateway>(), Mock.Of<ISessionConfigService>(), Mock.Of<IProfileImageService>(), Profile().Object)),
            "ResetPass" => (new ResetPassDialogView(), new ResetPassDialogViewModel(events, Log, login, Profile().Object)),
            "DeleteAccount" => (new DeleteAccountDialogView(), new DeleteAccountDialogViewModel(events, Log, login, new AccountProvider(), Mock.Of<IUserDirectoryGateway>(), Profile().Object)),
            "Register" => (new RegisterDialogView(), new RegisterDialogViewModel(events, Log, new RegisterViewModel(events, Log, new AccountModel()), new AccountProvider(), Mock.Of<IUserDirectoryGateway>(), Mock.Of<IProfileImageService>())),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    /// <summary>
    /// 화면 밖 창을 먼저 띄우고, 호스트 ContentControl(cal:View.Model="{Binding ActiveItem}")과 같은 길로 뷰모델을 붙인다 —
    /// Caliburn 이 이름 관례를 거는 순간(Bind)에는 뷰에 아직 템플릿이 없다(실앱과 같은 조건).
    /// </summary>
    public static Hosted Host(string name, bool canSelfDelete = true)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        // Task 를 돌려주는 동작은 Caliburn 이 코루틴으로 감싸며 IoC.BuildUp 을 부른다 — 호스트는 부트스트래퍼가 채운다.
        IoC.BuildUp = _ => { };
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.GetInstance = (_, _) => null!;
        var events = new EventAggregator();
        var recorder = new MessageRecorder();
        events.SubscribeOnPublishedThread(recorder);

        var (view, viewModel) = Build(name, events, canSelfDelete);
        ((IViewAware)viewModel).AttachView(view);   // ViewLocator 가 이 뷰를 돌려주게

        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, viewModel);
        AppHost.Pump(System.Windows.Threading.DispatcherPriority.Loaded);
        AppHost.Pump();
        return new Hosted(view, viewModel, recorder, window);
    }

    // ── 트리 ─────────────────────────────────────────────────────────

    public static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Visuals(child)) yield return d;
        }
    }

    /// <summary>UIA 이름 — 호스트 UiTests(ProbeSession.Name)처럼 한글 줄바꿈 결합자(U+2060, KoreanWordWrap)를 걷는다.</summary>
    public static string Name(AutomationPeer peer) => Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Strip(peer.GetName());

    /// <summary>UIA 기본 보기(Control view)에 나오는 peer 만 — 자동화 · 화면 읽기가 실제로 보는 것.</summary>
    public static IEnumerable<AutomationPeer> ControlPeers(UIElement root)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(root);
        return peer is null ? Enumerable.Empty<AutomationPeer>() : Walk(peer).Where(p => p.IsControlElement());

        static IEnumerable<AutomationPeer> Walk(AutomationPeer p)
        {
            var kids = p.GetChildren();
            if (kids is null) yield break;
            foreach (var k in kids)
            {
                yield return k;
                foreach (var d in Walk(k)) yield return d;
            }
        }
    }
}
