using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 칩 묶음(<see cref="ConsoleChipGroup"/>)이 칩을 보조 기술(UIA)에 <b>칩 그대로</b>(선택 패턴까지) 내놓는가 —
/// 특히 창을 닫았다 다시 열 때처럼 <b>같은 항목</b>으로 다시 붙은 뒤에도.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 SC-EVT-013 · SC-EVT-014 · SC-SUP-003 — 이벤트 콘솔의 기간 칩 · 억제 상태 칩이 화면에는 있는데 자동화가
/// 한 번도 찾지 못했다("기간 목록 항목 [오늘(자식 0), 24시간(자식 0) …]"). 평범한 <see cref="ItemsControl"/> 은 칩마다 항목 peer 를
/// 두고 그것을 데이터 항목으로 재사용하는데, 뷰가 떨어졌다가 같은 항목으로 다시 붙으면 재사용된 항목 peer 가 옛 칩
/// (<c>{DisconnectedItem}</c>)을 쥔 채 남는다 — 옛 칩은 보이지 않아 기본 보기에서 빠진다.
/// 이 시험은 진짜 UIA 클라이언트(프로세스 안, 화면 밖 창)로 그 길을 그대로 밟는다.
/// </remarks>
public class ConsoleChipGroupAutomationTests
{
    private const string ChipTemplate =
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
        + "<RadioButton Content='{Binding Name}' GroupName='TestChips' IsChecked='{Binding IsSelected, Mode=OneWay}' "
        + "AutomationProperties.AutomationId='{Binding Name, StringFormat=Test.Chip.{0}}' />"
        + "</DataTemplate>";

    public sealed class Option
    {
        public Option(string name, bool isSelected = false) { Name = name; IsSelected = isSelected; }
        public string Name { get; }
        public bool IsSelected { get; }
        public override string ToString() => Name;
    }

    private sealed record Reading(bool Found, bool HasSelectionItem, string ControlType);

    [Fact]
    public void should_expose_each_chip_with_its_selection_pattern_when_the_group_is_read_by_automation()
    {
        // Arrange + Act
        var (first, _, _) = ReadChips(reattach: false);

        // Assert — 칩이 묶음의 자식으로 곧바로(항목 peer 층 없이) 나오고 선택 패턴을 가진다
        Assert.All(new[] { "a", "b", "c" }, name =>
        {
            Assert.True(first[name].Found, $"Test.Chip.{name} 가 기본 보기에 없다");
            Assert.True(first[name].HasSelectionItem, $"Test.Chip.{name} 에 선택 패턴이 없다");
            Assert.Equal("ControlType.RadioButton", first[name].ControlType);
        });
    }

    [Fact]
    public void should_still_expose_the_chips_when_the_group_is_detached_and_reattached_with_the_same_items()
    {
        // Arrange + Act — 떼었다가(항목을 비운 채) 다시 붙이고 같은 항목을 다시 준다(창을 닫았다 다시 연 길)
        var (_, again, selectedByAutomation) = ReadChips(reattach: true);

        // Assert — 옛 칩이 아니라 새 칩이 나오고, 자동화의 선택이 실제 칩에 닿는다
        Assert.All(new[] { "a", "b", "c" }, name =>
        {
            Assert.True(again[name].Found, $"다시 붙인 뒤 Test.Chip.{name} 가 기본 보기에 없다(옛 칩을 쥔 항목 peer)");
            Assert.True(again[name].HasSelectionItem, $"다시 붙인 뒤 Test.Chip.{name} 에 선택 패턴이 없다");
        });
        Assert.True(selectedByAutomation, "UIA Select() 가 화면의 칩(b)을 켜지 못했다");
    }

    [Fact]
    public void should_report_a_group_rather_than_a_list_when_the_group_peer_is_created()
    {
        var (type, className) = OnSta(() =>
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(new ConsoleChipGroup())!;
            return (peer.GetAutomationControlType(), peer.GetClassName());
        });

        Assert.Equal(AutomationControlType.Group, type);
        Assert.Equal(nameof(ConsoleChipGroup), className);
    }

    /// <summary>화면 밖 창에 칩 묶음을 띄우고, 다른 스레드의 UIA 클라이언트로 읽는다(선택적으로 떼었다 다시 붙인 뒤).</summary>
    private static (Dictionary<string, Reading> First, Dictionary<string, Reading> Again, bool SelectedByAutomation) ReadChips(bool reattach)
    {
        var first = new Dictionary<string, Reading>();
        var again = new Dictionary<string, Reading>();
        var selected = false;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var items = new[] { new Option("a", isSelected: true), new Option("b"), new Option("c") };
                var group = new ConsoleChipGroup { ItemTemplate = (DataTemplate)XamlReader.Parse(ChipTemplate), ItemsSource = items };
                AutomationProperties.SetAutomationId(group, "Test.Chips");
                var host = new StackPanel();
                host.Children.Add(group);
                var window = new Window
                {
                    Content = host, Width = 400, Height = 120, Left = -20000, Top = -20000,
                    ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
                };
                window.Show();
                var hwnd = new WindowInteropHelper(window).Handle;
                var dispatcher = Dispatcher.CurrentDispatcher;

                _ = Task.Run(() =>
                {
                    try
                    {
                        var top = AutomationElement.FromHandle(hwnd);
                        Read(top, first);
                        if (reattach)
                        {
                            dispatcher.Invoke(() => host.Children.Remove(group));
                            Thread.Sleep(300);
                            dispatcher.Invoke(() => { group.ItemsSource = null; host.Children.Add(group); group.ItemsSource = items; });
                            Read(top, again);

                            var b = Find(top, "Test.Chip.b");
                            if (b?.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern) == true)
                            {
                                ((SelectionItemPattern)pattern).Select();
                                selected = dispatcher.Invoke(() => group.ItemContainerGenerator.ContainerFromItem(items[1]) is ContentPresenter cp
                                                                   && FirstRadio(cp)?.IsChecked == true);
                            }
                        }
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { dispatcher.InvokeShutdown(); }
                });
                Dispatcher.Run();
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return (first, again, selected);
    }

    private static void Read(AutomationElement top, Dictionary<string, Reading> into)
    {
        // 레이아웃 · UIA 트리 갱신이 한 바퀴 돌 때까지 — 찾을 때까지 짧게 다시 본다(최대 3초)
        foreach (var name in new[] { "a", "b", "c" })
        {
            AutomationElement? chip = null;
            for (var i = 0; i < 30 && chip is null; i++)
            {
                chip = Find(top, $"Test.Chip.{name}");
                if (chip is null) Thread.Sleep(100);
            }
            into[name] = chip is null
                ? new Reading(false, false, "")
                : new Reading(true, chip.TryGetCurrentPattern(SelectionItemPattern.Pattern, out _), chip.Current.ControlType.ProgrammaticName);
        }
    }

    private static AutomationElement? Find(AutomationElement top, string id)
        => top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    private static RadioButton? FirstRadio(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is RadioButton radio) return radio;
            if (FirstRadio(child) is { } nested) return nested;
        }
        return null;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
}
