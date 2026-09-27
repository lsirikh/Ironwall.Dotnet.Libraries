using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 레일 전환이 미적용 변경으로 막혔을 때 <b>화면의 레일 선택도</b> 원래 레일로 돌아와야 한다.
/// </summary>
/// <remarks>
/// 뷰모델 쪽 차단은 <see cref="AccountConsoleRailTests"/> 가 본다. 이 시험은 <b>화면 결선</b>까지 — 실제 <see cref="ConsoleRail"/> 을
/// TwoWay 로 묶고 항목을 자동화 Select(마우스 · UIA 와 같은 SelectionChange 길)로 고른 뒤, 막힌 전환이 화면 선택까지 되돌리는지 본다.
/// 2026-09-27 헤디드 SC-ACC-006(사용자 레일 선택=False · 권한 레일 선택=True)을 가르려고 세웠다 — 이 시험은 수정 없이 통과한다.
/// 즉 막혔을 때는 화면도 돌아온다. 그 회차의 전환은 막히지 않은 것(미적용 칸이 없던 것 — 시험 입력 경합)으로 판정했다.
/// </remarks>
public class AccountConsoleRailRevertTests
{
    [Fact]
    public async Task should_put_the_rail_selection_back_on_screen_when_the_switch_is_blocked_by_unapplied_changes()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(new[] { ConsoleFixtures.User(1, "op1", "김운영") });
        await console.ActivateForTestAsync();
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        Assert.True(console.OnUsersSelected(new List<object> { row }));
        console.Form.Fields.Single(f => f.Key == "name").Text = "고친 이름";
        Assert.True(console.DetailIsDirty);

        var shown = OnSta(() =>
        {
            // 콘솔 뷰와 같은 결선 — ConsoleRail(ListBox) 의 SelectedItem 을 SelectedRail 에 TwoWay 로 묶는다.
            var rail = new ConsoleRail { ItemsSource = console.RailEntries, ConsoleKey = "Accounts" };
            rail.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(console.SelectedRail)) { Source = console, Mode = BindingMode.TwoWay });
            var window = new Window
            {
                Content = rail, Width = 300, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
            };
            window.Show();
            Pump(DispatcherPriority.Loaded);

            // 사람이 '권한 설정' 레일 항목을 고른 것과 같은 길 — 항목 컨테이너의 자동화 Select(마우스 클릭 · UIA 가 타는 SelectionChange).
            // SelectedItem 을 코드로 바로 넣으면 Selector 의 선택 변경 구간을 거치지 않아 결함이 재현되지 않는다.
            var target = console.RailEntries.Single(e => e.Key == AccountConsoleKeys.Permissions);
            var container = (ListBoxItem)rail.ItemContainerGenerator.ContainerFromItem(target);
            var peer = (System.Windows.Automation.Provider.ISelectionItemProvider)
                System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(rail).GetChildren()
                    .OfType<System.Windows.Automation.Peers.ListBoxItemAutomationPeer>()
                    .First(p => ReferenceEquals(p.Item, target))
                    .GetPattern(System.Windows.Automation.Peers.PatternInterface.SelectionItem);
            Assert.NotNull(container);
            peer.Select();
            Pump(DispatcherPriority.ContextIdle);

            var key = (rail.SelectedItem as ConsoleRailEntry)?.Key;
            window.Close();
            return key;
        });

        Assert.True(console.IsUsersRail, "뷰모델은 사용자 레일에 남아야 한다");
        Assert.Equal(AccountConsoleKeys.Users, shown);
    }

    private static T? OnSta<T>(Func<T> body)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new System.Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
