using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 레일(<see cref="ConsoleRail"/>)이 묶인 값이 사용자의 선택을 <b>거절</b>하면 — 목록이 한 가지 상태(SelectedItem · SelectedItems ·
/// 항목 컨테이너)로 원래 항목에 돌아오는가.
/// </summary>
/// <remarks>
/// <para>WPF 는 TwoWay 갱신 뒤 원본을 <b>무조건 다시 읽는다</b>. 그 다시 읽기는 목록의 선택 변경이 아직 진행 중일 때 오므로
/// Selector 가 무시해, SelectedItem 만 원래 항목이 되고 SelectedItems · 컨테이너는 누른 항목에 남는다(실측: 거절 알림을 울리든
/// 안 울리든 · 한 박자 뒤 다시 울리든 같다 — 한 박자 뒤에는 SelectedItem 이 이미 같은 값이라 변경이 일어나지 않는다).</para>
/// <para>2026-09-28 계정 콘솔 SC-ACC-006 · 장비 콘솔 SC-DEV-008 — 화면은 막힌 레일을 고른 채 남고 콘솔은 앞 레일이었다.</para>
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class ConsoleRailRefusedSwitchTests
{
    public enum Owner
    {
        /// <summary>거절하고 곧바로 알린다(서버 · 부대 · 장비 · 이벤트 콘솔).</summary>
        RefuseAndNotify,
        /// <summary>거절하고 알리지 않는다.</summary>
        RefuseSilently,
        /// <summary>거절하고 곧바로 + 한 박자 뒤 알린다(옛 보고서 콘솔).</summary>
        RefuseAndNotifyTwice,
        /// <summary>받아들이되 <c>await Task.Yield()</c> 뒤에 키를 바꾼다 — 그동안 진행 중이라고 알린다.</summary>
        AcceptAfterYield,
        /// <summary>받아들이되 시험이 <c>Complete</c> 를 부를 때까지(긴 await) 끝나지 않는다 — 그동안 진행 중이라고 알린다.</summary>
        AcceptLater,
        /// <summary><see cref="AcceptAfterYield"/> 와 같되 진행 중이라고 알리지 않는다.</summary>
        AcceptAfterYieldUnsignalled,
    }

    [Theory]
    [InlineData(Owner.RefuseAndNotify)]
    [InlineData(Owner.RefuseSilently)]
    [InlineData(Owner.RefuseAndNotifyTwice)]
    public void should_show_only_the_original_item_as_selected_when_the_bound_value_refuses_the_pick(Owner owner)
    {
        var (atOnce, settled) = PickB(owner);

        Assert.Equal("a", settled.Owner);
        Assert.Equal("SelectedItem=a · SelectedItems=[a] · 컨테이너=[a:True,b:False,c:False]", settled.Rail);
        // 막는 순간(같은 입력 처리 안) 이미 한 상태여야 한다 — 한 박자 뒤 되돌리면 그 사이 화면이 한 번 그려진다.
        Assert.Equal("SelectedItem=a · SelectedItems=[a] · 컨테이너=[a:True,b:False,c:False]", atOnce);
    }

    [Fact]
    public void should_move_to_the_picked_item_when_the_bound_value_accepts_it()
    {
        var (atOnce, settled) = PickB(owner: null);

        Assert.Equal("b", settled.Owner);
        Assert.Equal("SelectedItem=b · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", settled.Rail);
        Assert.Equal("SelectedItem=b · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", atOnce);
    }

    [Fact]
    public void should_raise_one_selection_change_back_to_the_original_item_when_refused()
    {
        var changes = OnSta(() =>
        {
            var (rail, window, _) = Host(Owner.RefuseAndNotify);
            var log = new List<string>();
            rail.SelectionChanged += (_, e) => log.Add($"+{Keys(e.AddedItems)} -{Keys(e.RemovedItems)}");
            Select(rail, "b");
            Pump(DispatcherPriority.ContextIdle);
            window.Close();
            return log;
        });

        // 누른 변경(+b) 뒤 되돌림(+a) 한 번 — 처리기는 마지막에 원래 항목을 본다.
        Assert.Equal(new[] { "+b -a", "+a -b" }, changes);
    }

    [Fact]
    public void should_not_write_to_the_bound_value_again_when_putting_the_selection_back()
    {
        var writes = OnSta(() =>
        {
            var (rail, window, owner) = Host(Owner.RefuseAndNotify);
            Select(rail, "b");
            Pump(DispatcherPriority.ContextIdle);
            window.Close();
            return owner.Writes;
        });

        Assert.Equal(new[] { "b" }, writes);   // 사용자의 한 번뿐 — 되돌림이 원본에 null · a 를 쓰지 않는다
    }

    [Fact]
    public void should_keep_selection_and_keyboard_focus_on_the_original_item_when_down_is_refused()
    {
        // ListBox 의 ↓ 는 선택 변경 뒤 초점을 누른 항목에 한 번 더 둔다 — 선택 변경 안에서 초점을 옮기면 덮여 막힌 항목에 남았다.
        var (state, focus) = OnSta(() =>
        {
            var (rail, window, _) = Host(Owner.RefuseAndNotify);
            PressDown(rail, from: "a");
            Pump(DispatcherPriority.ContextIdle);
            var result = (State(rail), FocusedKey());
            window.Close();
            return result;
        });

        Assert.Equal("SelectedItem=a · SelectedItems=[a] · 컨테이너=[a:True,b:False,c:False]", state);
        Assert.Equal("a", focus);
    }

    [Fact]
    public void should_move_to_the_picked_item_without_passing_back_through_the_original_when_the_owner_accepts_after_a_yield()
    {
        // 적대 검토 M2 — 전환이 await 뒤에 키를 바꾸면(양보) 선택 변경 시점의 묶인 값은 아직 앞 항목이다.
        // 진행 중이라고 알리는 원본이면 레일은 되돌리지 않는다: 앞 항목을 거치는 중간 선택도, 앞 항목에 남는 초점도 없다.
        var (changes, atOnce, settled, focus) = OnSta(() =>
        {
            var (rail, window, owner) = Host(Owner.AcceptAfterYield);
            var log = Record(rail);
            PressDown(rail, from: "a");
            var now = State(rail);
            Pump(DispatcherPriority.ContextIdle);
            var result = (log, now, new Settled(owner.Key, State(rail)), FocusedKey());
            window.Close();
            return result;
        });

        Assert.Equal(new[] { "+b -a" }, changes);
        Assert.Equal("SelectedItem=a · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", atOnce);   // 화면은 누른 항목 — 원본만 아직 앞 값
        Assert.Equal("b", settled.Owner);
        Assert.Equal("SelectedItem=b · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", settled.Rail);
        Assert.Equal("b", focus);
    }

    [Fact]
    public void should_keep_the_picked_item_selected_across_renders_while_the_owner_switch_is_pending()
    {
        // 양보가 한 박자가 아니라 긴 await(I/O)면 그 사이 여러 번 그려진다 — 그동안 누른 항목에 머물고 끝나면 그대로 간다.
        var (during, settled, changes) = OnSta(() =>
        {
            var (rail, window, owner) = Host(Owner.AcceptLater);
            var log = Record(rail);
            Select(rail, "b");
            Pump(DispatcherPriority.ApplicationIdle);   // Render · Loaded · Input 을 모두 지난다
            var mid = State(rail);
            owner.Complete(accept: true);
            Pump(DispatcherPriority.ContextIdle);
            var result = (mid, new Settled(owner.Key, State(rail)), log);
            window.Close();
            return result;
        });

        Assert.Equal("SelectedItem=a · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", during);
        Assert.Equal("b", settled.Owner);
        Assert.Equal("SelectedItem=b · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", settled.Rail);
        Assert.Equal(new[] { "+b -a" }, changes);
    }

    [Fact]
    public void should_put_selection_and_keyboard_focus_back_on_the_original_item_when_the_pending_switch_is_finally_refused()
    {
        var (settled, focus) = OnSta(() =>
        {
            var (rail, window, owner) = Host(Owner.AcceptLater);
            PressDown(rail, from: "a");
            Pump(DispatcherPriority.ApplicationIdle);
            owner.Complete(accept: false);
            Pump(DispatcherPriority.ContextIdle);
            var result = (new Settled(owner.Key, State(rail)), FocusedKey());
            window.Close();
            return result;
        });

        Assert.Equal("a", settled.Owner);
        Assert.Equal("SelectedItem=a · SelectedItems=[a] · 컨테이너=[a:True,b:False,c:False]", settled.Rail);
        Assert.Equal("a", focus);
    }

    [Fact]
    public void should_end_on_the_picked_item_with_keyboard_focus_when_an_unsignalled_owner_accepts_after_a_yield()
    {
        // 진행 중이라고 알리지 않는 원본 — 선택 변경 시점에는 거절과 구별할 수 없어 한 번 되돌린다(그리기 전).
        // 늦은 수락이 오면 목록은 새 항목 한 상태로 끝나고, 초점을 앞 항목으로 빼앗지 않는다.
        var (settled, focus) = OnSta(() =>
        {
            var (rail, window, owner) = Host(Owner.AcceptAfterYieldUnsignalled);
            PressDown(rail, from: "a");
            Pump(DispatcherPriority.ContextIdle);
            var result = (new Settled(owner.Key, State(rail)), FocusedKey());
            window.Close();
            return result;
        });

        Assert.Equal("b", settled.Owner);
        Assert.Equal("SelectedItem=b · SelectedItems=[b] · 컨테이너=[a:False,b:True,c:False]", settled.Rail);
        Assert.Equal("b", focus);
    }

    #region - Harness -
    private sealed class RailOwner : IConsoleRailSwitchState
    {
        private readonly Owner? _mode;
        private string _key = "a";
        private string? _pendingKey;

        public RailOwner(Owner? mode) => _mode = mode;

        public List<ConsoleRailEntry> Entries { get; } = new() { new("a", "A"), new("b", "B"), new("c", "C") };
        public List<string> Writes { get; } = new();
        public string Key => _key;

        /// <summary>알리는 원본만 참을 보인다 — 알리지 않는 원본(AcceptAfterYieldUnsignalled)은 약속이 없는 것과 같다.</summary>
        public bool IsRailSwitchPending => _pendingKey is not null && _mode != Owner.AcceptAfterYieldUnsignalled;

        public ConsoleRailEntry? SelectedRail
        {
            get => Entries.FirstOrDefault(e => e.Key == _key);
            set
            {
                Writes.Add(value?.Key ?? "null");
                if (value is null || value.Key == _key) return;
                switch (_mode)
                {
                    case null: _key = value.Key; Raise(); return;
                    case Owner.RefuseAndNotify: Raise(); return;
                    case Owner.RefuseSilently: return;
                    case Owner.RefuseAndNotifyTwice:
                        Raise();
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(Raise));
                        return;
                    case Owner.AcceptAfterYield:
                    case Owner.AcceptAfterYieldUnsignalled:
                        _pendingKey = value.Key;
                        _ = AcceptAfterYieldAsync();
                        return;
                    case Owner.AcceptLater:
                        _pendingKey = value.Key;
                        return;
                }
            }
        }

        /// <summary>콘솔의 SwitchRailAsync 와 같은 꼴 — await(양보) 뒤에 키를 바꾸고 알린다.</summary>
        private async Task AcceptAfterYieldAsync()
        {
            await Task.Yield();
            Complete(accept: true);
        }

        /// <summary>진행 중인 전환을 끝낸다 — 받아들이면 새 키, 막으면 앞 키 그대로. 어느 쪽이든 알린다.</summary>
        public void Complete(bool accept)
        {
            if (accept && _pendingKey is not null) _key = _pendingKey;
            _pendingKey = null;
            Raise();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRail)));
    }

    private sealed record Settled(string Owner, string Rail);

    private static (string AtOnce, Settled Settled) PickB(Owner? owner) => OnSta(() =>
    {
        var (rail, window, source) = Host(owner);
        Select(rail, "b");
        var atOnce = State(rail);
        Pump(DispatcherPriority.ContextIdle);
        var settled = new Settled(source.Key, State(rail));
        window.Close();
        return (atOnce, settled);
    });

    private static (ConsoleRail Rail, Window Window, RailOwner Owner) Host(Owner? mode)
    {
        var owner = new RailOwner(mode);
        var rail = new ConsoleRail { ItemsSource = owner.Entries };
        rail.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(RailOwner.SelectedRail)) { Source = owner, Mode = BindingMode.TwoWay });
        var window = new Window
        {
            Content = rail, Width = 300, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
        };
        // 앱의 UI 스레드처럼 await 가 같은 디스패처로 돌아오게 한다(Task.Yield 뒤 계속이 디스패처 Normal 에 선다).
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        window.Show();
        Pump(DispatcherPriority.Loaded);
        owner.Writes.Clear();
        return (rail, window, owner);
    }

    /// <summary>항목 컨테이너의 자동화 Select — 마우스 클릭 · UIA SelectionItemPattern 과 같은 SelectionChange 길.</summary>
    private static void Select(ConsoleRail rail, string key)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
        var item = peer.GetChildren()!.OfType<ListBoxItemAutomationPeer>().First(p => (p.Item as ConsoleRailEntry)?.Key == key);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();
    }

    /// <summary>
    /// 키보드 ↓ — 항목에 키보드 초점을 두고 입력 관리자에 넣는다. 실제 키 입력처럼 PreviewKeyDown → KeyDown 으로 올라가
    /// ListBox 가 초점을 옮기고 → 고르고 → 초점을 한 번 더 둔다.
    /// </summary>
    private static void PressDown(ConsoleRail rail, string from)
    {
        var container = (ListBoxItem)rail.ItemContainerGenerator.ContainerFromItem(rail.Items.OfType<ConsoleRailEntry>().Single(e => e.Key == from));
        Assert.True(container.Focus() && container.IsKeyboardFocused, $"'{from}' 항목에 키보드 초점을 둘 수 없다");
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(rail)!, Environment.TickCount, Key.Down)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = container,
        };
        InputManager.Current.ProcessInput(args);
    }

    private static string? FocusedKey() => ((Keyboard.FocusedElement as FrameworkElement)?.DataContext as ConsoleRailEntry)?.Key;

    private static List<string> Record(ConsoleRail rail)
    {
        var log = new List<string>();
        rail.SelectionChanged += (_, e) => log.Add($"+{Keys(e.AddedItems)} -{Keys(e.RemovedItems)}");
        return log;
    }

    private static string Keys(System.Collections.IList items) => string.Join(",", items.OfType<ConsoleRailEntry>().Select(e => e.Key));

    private static string State(ConsoleRail rail)
        => $"SelectedItem={(rail.SelectedItem as ConsoleRailEntry)?.Key} · SelectedItems=[{Keys(rail.SelectedItems)}] · 컨테이너=["
           + string.Join(",", rail.Items.OfType<ConsoleRailEntry>().Select(e => $"{e.Key}:{(rail.ItemContainerGenerator.ContainerFromItem(e) as ListBoxItem)?.IsSelected}")) + "]";

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() => { try { result = body(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result;
    }
    #endregion
}

/// <summary>
/// 키보드 초점 · 마우스 캡처 · Win32 전경(foreground)은 프로세스 전역이다 — 화면 밖이라도 <c>Window.Show()</c> 로
/// 실제 OS 창을 띄우고 <c>Keyboard.Focus</c>/<c>Mouse.Capture</c>/실제 입력 라우팅에 기대는 시험 반들을 한 모음에 묶어
/// 서로 겹쳐 돌지 않게 한다. 겹치면 다른 반의 창이 초점 · 캡처를 가로채 간헐적으로 실패한다
/// (실측 2026-09-29: 전체 스위트에서만 616/617 — <see cref="ConsoleRailRefusedSwitchTests"/> 단독 5연속 11/11 은 항상 통과).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfFocusCollection
{
    public const string Name = "WpfFocus (real window · keyboard focus · mouse capture)";
}
