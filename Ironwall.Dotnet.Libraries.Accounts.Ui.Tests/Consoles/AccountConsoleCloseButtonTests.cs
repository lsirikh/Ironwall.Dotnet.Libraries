using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 실창(GIS 호스트) 결함 D3 — 계정 · 권한 콘솔의 머리 ✕(<c>Console.Accounts.CloseButton</c>)를 눌러도 창이 닫히지 않았다
/// (run summary <c>d2.close-works@users</c> FAIL, 뒤이은 모든 닫기 재시도 · 왼쪽 메뉴 열기도 실패).
/// </summary>
/// <remarks>
/// <para><b>근거</b>: 앱 로그(log-2026-09-27.txt)에 계정 콘솔이 열린 00:58:34 이후 <c>ClosePanelMessageModel 의 HandleAsync</c>
/// 도, 강제 로그인 차단 줄도, 계정 콘솔 · 자식 패널의 <c>OnDeactivate</c> 도 한 줄도 없다 — 닫기 요청이 호스트에 <b>닿지 않았다</b>.
/// 같은 회차에서 ✕ 가 된 창(장비 · 이벤트)은 ✕ 가 <c>ConsoleShell</c> <b>밖</b>(호스트 뷰의 Grid)에 있고,
/// 보고서 · 조치 문구 · 서버 콘솔은 ✕ 를 코드 숨김 <c>Click</c> 으로 잇는다. 계정 콘솔만 ✕ 를
/// <c>ConsoleShell.HeaderContent</c> 안에 두고 <b>Caliburn x:Name 관례</b>(<c>x:Name="ClickClose"</c>)에만 기댄다.</para>
/// <para>Caliburn 은 뷰를 처음 붙일 때(<c>View.Model</c> → <c>ViewModelBinder.Bind</c>) 이름 붙은 요소를 찾는데, 그 순간
/// <see cref="ConsoleShell"/>(<see cref="ContentControl"/> 이 아닌 <see cref="Control"/>)은 아직 템플릿이 없어 시각 자식이 0 이고,
/// <c>HeaderContent</c> 라는 자기 속성 안은 들여다보지 않는다 → ✕ 에 액션이 안 붙는다 → 눌러도 <c>ClickClose</c> 가 안 불린다.</para>
/// <para>고침: <c>x:Name</c> 은 그대로 두고(Caliburn 바인딩 지시자 — 바꾸지 않는다) <c>cal:Message.Attach="ClickClose"</c> 를 더한다.
/// Message.Attach 는 누를 때 시각 트리를 따라 대상을 찾으므로 템플릿 시점과 무관하다. 관례는 이미 트리거가 있는 요소를 건너뛰므로
/// 두 번 불리지도 않는다.</para>
/// </remarks>
public class AccountConsoleCloseButtonTests
{
    /// <summary>✕ 가 부르는 메서드만 가진 뷰모델 — 몇 번 불렸는지 센다.</summary>
    public sealed class CloseProbeViewModel : Screen
    {
        public int CloseCount { get; private set; }
        public void ClickClose() => CloseCount++;
    }

    /// <summary>
    /// 실제 ConsoleShell 과 같은 꼴 — <c>HeaderContent</c> 를 템플릿의 ContentPresenter 로 내보낸다.
    /// 커널의 전체 템플릿(Generic.xaml)은 Application 을 세워야 읽히는데, 그러면 이 시험 프로젝트의 다른 시험이 기대는
    /// "Application.Current == null → DispatcherService 제자리 실행" 전제가 깨진다 — 그래서 머리 칸만 가진 최소 템플릿을 쓴다.
    /// </summary>
    private static ControlTemplate HeaderOnlyTemplate()
    {
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ConsoleShell.HeaderContentProperty));
        return new ControlTemplate(typeof(ConsoleShell)) { VisualTree = presenter };
    }

    /// <summary>
    /// Caliburn 이 콘솔을 여는 길 그대로 붙이고(<c>View.SetModel</c>: 뷰 찾기 → Bind → 내용 교체) 화면 밖 창에 띄운 뒤
    /// ✕ 의 Click 을 올린다. 반환값은 <c>ClickClose</c> 가 불린 횟수.
    /// </summary>
    private static int ClickCloseCountAfterClick(bool withMessageAttach, bool insideShellHeader = true)
    {
        var count = -1;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var button = new Button { Name = "ClickClose" };   // XAML 의 x:Name="ClickClose" 와 같다
                if (withMessageAttach) Message.SetAttach(button, "ClickClose");

                // 호스트 장비 · 이벤트 창처럼 ✕ 가 ConsoleShell 밖(평범한 패널)에 있는 경우와 견준다.
                var view = insideShellHeader
                    ? new UserControl { Content = new ConsoleShell { Template = HeaderOnlyTemplate(), HeaderContent = button } }
                    : new UserControl { Content = new Grid { Children = { button } } };
                var viewModel = new CloseProbeViewModel();
                ((IViewAware)viewModel).AttachView(view);   // ViewLocator 가 이 뷰를 돌려주게(타입 검색을 건너뛴다)

                var host = new ContentControl();
                var window = new Window
                {
                    Content = host, Width = 400, Height = 200, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
                };
                window.Show();

                View.SetModel(host, viewModel);   // 호스트의 ContentControl(cal:View.Model="{Binding ActiveItem}") 과 같은 길
                Pump(DispatcherPriority.Loaded);
                Pump(DispatcherPriority.ContextIdle);

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Pump(DispatcherPriority.ContextIdle);

                count = viewModel.CloseCount;
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw failure;
        return count;

        static void Pump(DispatcherPriority priority)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(priority, new System.Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    [Fact]
    public void should_not_invoke_click_close_when_the_button_relies_only_on_the_xname_convention_inside_the_console_shell_header()
    {
        // 근본 원인의 특성 시험 — 이 시험이 실패하면(=관례가 닿으면) Caliburn · ConsoleShell 의 동작이 바뀐 것이다.
        Assert.Equal(0, ClickCloseCountAfterClick(withMessageAttach: false));
    }

    [Fact]
    public void should_invoke_click_close_when_the_xname_button_sits_outside_the_console_shell()
    {
        // 대조군 — 같은 관례가 ConsoleShell 밖(장비 · 이벤트 호스트 뷰의 ✕ 자리)에서는 된다. 차이는 자리뿐이다.
        Assert.Equal(1, ClickCloseCountAfterClick(withMessageAttach: false, insideShellHeader: false));
    }

    [Fact]
    public void should_invoke_click_close_exactly_once_when_the_header_button_uses_message_attach()
    {
        Assert.Equal(1, ClickCloseCountAfterClick(withMessageAttach: true));
    }

    [Fact]
    public void should_invoke_click_close_only_once_when_the_xname_convention_can_also_see_a_message_attach_button()
    {
        // 다시 열 때(뷰 캐시 · 템플릿 적용 뒤 Bind) 관례가 ✕ 를 찾게 되어도 두 번 불리지 않아야 한다 —
        // 관례가 닿는 자리에 둘 다 달아 본다.
        Assert.Equal(1, ClickCloseCountAfterClick(withMessageAttach: true, insideShellHeader: false));
    }

    [Fact]
    public async Task should_close_the_account_console_through_the_panel_shell_when_close_panel_is_handled_after_an_apply()
    {
        // ✕ 가 다시 닿게 된 뒤의 나머지 길 — 실창 회차와 같은 순서(행 선택 → 상태 '미사용' → [적용])를 밟고
        // 호스트 ConductorControlViewModel.HandleAsync(ClosePanelMessageModel) 와 같은 세 줄로 닫는다.
        // Arrange
        var (console, _, _, _, events) = ConsoleFixtures.Build(new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영"),
            ConsoleFixtures.User(2, "op2", "이관제"),
        });
        var log = new SilentLog();
        var shell = new Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Conductors.ConductorOneViewModel(events, log);
        await ((IActivate)shell).ActivateAsync();
        await shell.ActivateItemAsync(console);
        console.OnUsersSelected(new List<object> { console.AccountManagerPanelViewModel.ViewModelProvider[0] });
        console.Form.Fields.Single(f => f.Key == "used").Text = "미사용";
        await console.ApplyAsync();

        // Act — ConductorControlViewModel.HandleAsync(ClosePanelMessageModel)
        shell.Items.Clear();
        await shell.DeactivateItemAsync(shell.ActiveItem, true);
        await ((IDeactivate)shell).DeactivateAsync(true);

        // Assert
        Assert.False(console.IsActive);
        Assert.Null(shell.ActiveItem);
        Assert.False(shell.IsVisible);
    }

    // ── 실제 뷰의 계약 ───────────────────────────────────────────────

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void should_wire_the_account_console_close_button_with_message_attach_when_it_sits_in_the_console_shell_header()
    {
        // Arrange
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "AccountConsolePanelView.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace cal = "http://caliburnmicro.com";

        // Act
        var close = XDocument.Load(path).Descendants()
            .Single(e => (string?)e.Attribute("AutomationProperties.AutomationId") == "Console.Accounts.CloseButton");

        // Assert
        Assert.Equal("ConsoleShell.HeaderContent", close.Parent?.Name.LocalName);   // 관례가 닿지 않는 자리에 있다
        Assert.Equal("ClickClose", (string?)close.Attribute(x + "Name"));           // x:Name 은 그대로(바꾸지 않는다)
        Assert.Equal("ClickClose", (string?)close.Attribute(cal + "Message.Attach"));
    }
}
