using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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

    #region - Harness -
    private sealed class RailOwner : INotifyPropertyChanged
    {
        private readonly Owner? _mode;
        private string _key = "a";

        public RailOwner(Owner? mode) => _mode = mode;

        public List<ConsoleRailEntry> Entries { get; } = new() { new("a", "A"), new("b", "B"), new("c", "C") };
        public List<string> Writes { get; } = new();
        public string Key => _key;

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
                }
            }
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
