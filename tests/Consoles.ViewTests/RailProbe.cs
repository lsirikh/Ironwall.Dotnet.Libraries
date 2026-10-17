using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 콘솔 실제 뷰를 호스트와 같은 결선(View.SetModel · XamlPlatformProvider)으로 세우고, 레일을 사람과 같은 길(항목 자동화 Select —
/// 마우스 · UIA SelectionItemPattern 과 같은 SelectionChange)로 누른 뒤 목록의 세 가지 선택 상태를 읽는다.
/// </summary>
/// <remarks>
/// 막힌 레일 전환에서 WPF 는 SelectedItem 만 원래 레일로 되돌리고 SelectedItems · 항목 컨테이너는 누른 레일에 남긴다 —
/// SelectedItem 만 보는 시험은 결함을 못 잡는다(계정 콘솔 SC-ACC-006). 그래서 셋을 다 본다.
/// </remarks>
internal static class RailProbe
{
    /// <summary>뷰모델을 뷰에 붙이고 화면 밖 창에 띄운다. IoC 는 호출자가 먼저 준비한다.</summary>
    public static Window Host(object viewModel, FrameworkElement view)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        ((IViewAware)viewModel).AttachView(view);
        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, viewModel);
        AppHost.Pump(DispatcherPriority.Loaded);
        AppHost.Pump();
        return window;
    }

    /// <summary>시각 트리에서 AutomationId 로 레일을 찾는다.</summary>
    public static ConsoleRail Rail(DependencyObject root, string automationId)
    {
        var rail = Find<ConsoleRail>(root, r => System.Windows.Automation.AutomationProperties.GetAutomationId(r) == automationId);
        Assert.True(rail is not null, $"레일 {automationId} 이 화면에 없다");
        return rail!;
    }

    /// <summary>항목 컨테이너의 자동화 Select — 마우스 클릭 · UIA SelectionItemPattern 과 같은 SelectionChange 길.</summary>
    public static void Select(ConsoleRail rail, string key)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
        var item = peer.GetChildren()!.OfType<ListBoxItemAutomationPeer>()
                       .First(p => (p.Item as ConsoleRailEntry)?.Key == key);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();
    }

    /// <summary>
    /// 키보드로 한 칸 아래 — 항목에 키보드 초점을 두고 ↓ 를 입력 관리자(InputManager)에 넣는다. 실제 키 입력과 같이
    /// PreviewKeyDown → KeyDown 으로 올라가 ListBox 가 초점을 먼저 옮기고 그다음 고른다.
    /// </summary>
    public static void PressDownFrom(ConsoleRail rail, string key)
    {
        var from = Container(rail, key);
        Assert.True(from.Focus() && from.IsKeyboardFocused, $"'{key}' 항목에 키보드 초점을 둘 수 없다 · 초점={System.Windows.Input.Keyboard.FocusedElement}");
        var source = PresentationSource.FromVisual(rail)!;
        var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, System.Windows.Input.Key.Down)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
            Source = from,
        };
        System.Windows.Input.InputManager.Current.ProcessInput(args);
    }

    /// <summary>레일 순서에서 <paramref name="key"/> 바로 아래 항목의 키.</summary>
    public static string KeyAfter(ConsoleRail rail, string key)
    {
        var keys = rail.Items.OfType<ConsoleRailEntry>().Select(e => e.Key).ToList();
        return keys[keys.IndexOf(key) + 1];
    }

    public static ListBoxItem Container(ConsoleRail rail, string key)
    {
        var entry = rail.Items.OfType<ConsoleRailEntry>().Single(e => e.Key == key);
        return (ListBoxItem)rail.ItemContainerGenerator.ContainerFromItem(entry);
    }

    /// <summary>선택 변경을 차례로 적는다 — "+추가 -제거".</summary>
    public static List<string> RecordSelectionChanges(ConsoleRail rail)
    {
        var log = new List<string>();
        rail.SelectionChanged += (_, e) => log.Add($"+{Keys(e.AddedItems)} -{Keys(e.RemovedItems)}");
        return log;

        static string Keys(System.Collections.IList items) => string.Join(",", items.OfType<ConsoleRailEntry>().Select(e => e.Key));
    }

    /// <summary>키보드 초점이 <paramref name="key"/> 항목 컨테이너에 있는가.</summary>
    public static void AssertKeyboardFocusOn(ConsoleRail rail, string key)
        => Assert.True(Container(rail, key).IsKeyboardFocused,
            $"키보드 초점이 '{key}' 항목에 있어야 한다 · 초점={((System.Windows.Input.Keyboard.FocusedElement as FrameworkElement)?.DataContext is ConsoleRailEntry e ? e.Key : System.Windows.Input.Keyboard.FocusedElement?.ToString())}");

    public static string? SelectedKey(ConsoleRail rail) => (rail.SelectedItem as ConsoleRailEntry)?.Key;

    public static bool ItemSelected(ConsoleRail rail, string key)
    {
        var entry = rail.Items.OfType<ConsoleRailEntry>().Single(e => e.Key == key);
        return rail.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem { IsSelected: true };
    }

    /// <summary>
    /// 막힌 전환 뒤 목록이 <b>한 가지 상태</b>로 <paramref name="expected"/> 에 있는가 — SelectedItem · SelectedItems · 항목 컨테이너 모두.
    /// </summary>
    public static void AssertShows(ConsoleRail rail, string expected, string refused)
    {
        Assert.True(SelectedKey(rail) == expected, $"SelectedItem 이 {expected} 여야 한다 · " + State(rail));
        Assert.True(rail.SelectedItems.Count == 1 && (rail.SelectedItems[0] as ConsoleRailEntry)?.Key == expected,
            $"SelectedItems 가 [{expected}] 여야 한다 · " + State(rail));
        Assert.True(ItemSelected(rail, expected), $"화면의 '{expected}' 항목이 선택돼야 한다 · " + State(rail));
        Assert.False(ItemSelected(rail, refused), $"막힌 '{refused}' 항목은 선택으로 남으면 안 된다 · " + State(rail));
    }

    /// <summary>진단 — 목록의 SelectedItem · SelectedItems · 항목 컨테이너마다의 IsSelected.</summary>
    public static string State(ConsoleRail rail)
        => $"SelectedItem={SelectedKey(rail)} · SelectedItems=[{string.Join(",", rail.SelectedItems.OfType<ConsoleRailEntry>().Select(e => e.Key))}] · 컨테이너=["
           + string.Join(",", rail.Items.OfType<ConsoleRailEntry>().Select(e => $"{e.Key}:{(rail.ItemContainerGenerator.ContainerFromItem(e) as ListBoxItem)?.IsSelected}")) + "]";

    public static void Wait(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        task.GetAwaiter().GetResult();
    }

    public static T Wait<T>(Task<T> task)
    {
        Wait((Task)task);
        return task.Result;
    }

    /// <summary>Caliburn 정적 IoC — 시험마다 새로 준다(다른 시험이 남긴 델리게이트에 기대지 않는다).</summary>
    public static void UseIoC(Func<Type, object?> resolve)
    {
        IoC.GetInstance = (type, _) => resolve(type)!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
    }

    public static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
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
