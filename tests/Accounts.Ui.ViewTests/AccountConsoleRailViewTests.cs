using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 계정 콘솔 <b>실제 뷰</b>에서 — 미적용 변경으로 레일 전환이 막히면 화면의 레일 선택도 '사용자'로 돌아와야 한다.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 2회차 SC-ACC-006: 직급 칸 미적용('LRT-BLK2') 중 '권한 설정' 레일을 누르자 뷰모델은 막았는데
/// (막대 '적용하거나 되돌린 뒤 이동하세요' · 칸 유지) 화면 레일은 '권한 설정'을 고른 채 남았다. 콘솔 뷰 · 커널 레일 · Caliburn 결선을
/// 호스트와 같은 길(View.SetModel · XamlPlatformProvider)로 세우고, 항목 자동화 Select(마우스 · UIA 와 같은 SelectionChange)로 누른다.
/// </remarks>
public class AccountConsoleRailViewTests
{
    [Fact]
    public void should_put_the_rail_selection_back_to_users_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
            Assert.True(console.OnUsersSelected(new List<object> { row }));
            console.Form.Fields.Single(f => f.Key == "position").Text = "LRT-BLK2";
            AppHost.Pump();
            Assert.True(console.DetailIsDirty);

            var rail = Find<ConsoleRail>(view, r => AutomationPropertiesId(r) == "Console.Accounts.Rail");
            Assert.NotNull(rail);
            Assert.Equal(AccountConsoleKeys.Users, (rail!.SelectedItem as ConsoleRailEntry)?.Key);

            SelectByAutomation(rail, AccountConsoleKeys.Permissions);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.IsUsersRail, "뷰모델은 막아야 한다");
            Assert.Equal("LRT-BLK2", console.Form.Fields.Single(f => f.Key == "position").Text);
            Assert.Equal(AccountConsoleKeys.Users, (rail.SelectedItem as ConsoleRailEntry)?.Key);
            Assert.True(ItemSelected(rail, AccountConsoleKeys.Users), "화면의 '사용자' 항목이 다시 선택돼야 한다 · " + State(rail));
            Assert.False(ItemSelected(rail, AccountConsoleKeys.Permissions), "막힌 '권한 설정' 항목은 선택으로 남으면 안 된다 · " + State(rail));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            var rail = Find<ConsoleRail>(view, r => AutomationPropertiesId(r) == "Console.Accounts.Rail")!;
            SelectByAutomation(rail, AccountConsoleKeys.Permissions);
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.IsPermissionsRail);
            Assert.Equal(AccountConsoleKeys.Permissions, (rail.SelectedItem as ConsoleRailEntry)?.Key);
        }
        finally { window.Close(); }
    });

    /// <summary>
    /// PRD FR-17 ⑤ — 그룹 배정 [적용] <b>뒤</b> 트레이의 [되돌리기]로 방금 옮긴 사용자를 이전 그룹으로 되돌린다.
    /// 2026-09-28 헤디드 SC-ACC-032: 적용이 끝나 대기 목록이 비면 트레이 전체가 접혀 [되돌리기]도 함께 사라졌다(되돌릴 길 없음).
    /// </summary>
    [Fact]
    public void should_keep_the_undo_button_visible_after_the_group_assignment_is_applied_in_the_real_console_view() => AppHost.Run(() =>
    {
        var groups = new[]
        {
            new Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.UserGroupDto { Id = 11, Name = "주간조", IsActive = true, Permissions = new Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.PermissionsDto() },
        };
        var (view, window, console) = HostConsole(groups);
        try
        {
            var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
            Assert.True(console.OnUsersSelected(new List<object> { row }));
            console.AssignSelectionToGroup(console.GroupChips.Single(c => c.Id == 11));
            AppHost.Pump();
            Assert.True(Button(view, "Console.Accounts.Draft.Apply")?.IsVisible, "적용 전 — 트레이가 보여야 한다");

            Wait(console.ApplyDraftAsync());
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.True(console.CanUndoDraft, "적용 뒤 되돌리기 장부가 있어야 한다");
            var undo = Button(view, "Console.Accounts.Draft.Undo");
            Assert.True(undo?.IsVisible == true, "적용 뒤 [되돌리기]가 화면에 보여야 한다(트레이가 함께 접히면 안 된다)");

            Wait(console.UndoDraftAsync());
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            Assert.False(console.CanUndoDraft);
            Assert.False(Button(view, "Console.Accounts.Draft.Undo")?.IsVisible == true, "되돌린 뒤에는 트레이가 접힌다");
        }
        finally { window.Close(); }
    });

    private static Button? Button(DependencyObject root, string id) => Find<Button>(root, b => AutomationPropertiesId(b) == id);

    // ── 호스트와 같은 결선 ──

    private static (AccountConsolePanelView View, Window Window, TestAccountConsole Console) HostConsole(
        IEnumerable<Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.UserGroupDto>? groups = null)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        IoC.BuildUp = _ => { };
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.GetInstance = (_, _) => null!;

        var (console, _, _, _, _) = ConsoleFixtures.Build(new[] { ConsoleFixtures.User(1, "op1", "김운영", position: "시험직급") }, groups);
        Wait(console.ActivateForTestAsync());

        var view = new AccountConsolePanelView { Width = 1400, Height = 900 };
        ((IViewAware)console).AttachView(view);
        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, console);
        AppHost.Pump(DispatcherPriority.Loaded);
        AppHost.Pump();
        return (view, window, console);
    }

    private static void Wait(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        task.GetAwaiter().GetResult();
    }

    /// <summary>항목 컨테이너의 자동화 Select — 마우스 클릭 · UIA SelectionItemPattern 과 같은 SelectionChange 길.</summary>
    private static void SelectByAutomation(ConsoleRail rail, string key)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
        var item = peer.GetChildren()!.OfType<ListBoxItemAutomationPeer>()
                       .First(p => (p.Item as ConsoleRailEntry)?.Key == key);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();
    }

    private static bool ItemSelected(ConsoleRail rail, string key)
    {
        var entry = rail.Items.OfType<ConsoleRailEntry>().Single(e => e.Key == key);
        return rail.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem { IsSelected: true };
    }

    /// <summary>진단 — 목록의 SelectedItem · SelectedItems · 항목 컨테이너마다의 IsSelected.</summary>
    private static string State(ConsoleRail rail)
        => $"SelectedItem={(rail.SelectedItem as ConsoleRailEntry)?.Key} · SelectedItems=[{string.Join(",", rail.SelectedItems.OfType<ConsoleRailEntry>().Select(e => e.Key))}] · 컨테이너=["
           + string.Join(",", rail.Items.OfType<ConsoleRailEntry>().Select(e => $"{e.Key}:{(rail.ItemContainerGenerator.ContainerFromItem(e) as ListBoxItem)?.IsSelected}")) + "]";

    private static string AutomationPropertiesId(DependencyObject d) => System.Windows.Automation.AutomationProperties.GetAutomationId(d);

    private static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }
}
