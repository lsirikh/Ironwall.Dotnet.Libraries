using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 칩 줄(<see cref="ConsoleChipStrip"/>) 실제 컨트롤 — 화면 밖 창, 고정 폭, 폭 100 칩 N 개.
/// </summary>
/// <remarks>
/// 2026-09-28 사용자 보고: 계정 · 권한 → 권한 설정의 '권한 그룹' 칩 줄이 오른쪽에서 잘리는데 나머지로 갈 길이 보이지 않았다
/// (숨긴 가로 스크롤 + 가로 휠). 접힌 한 줄 + [⌄ 더 보기 +N] → 펼침 · 고른 칩 늘 보임 · 폭과 항목 변화에 다시 잼.
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class ConsoleChipStripTests
{
    private const string ChipTemplate =
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
        + "<RadioButton Width='100' Margin='0' Content='{Binding Name}' GroupName='StripChips' IsChecked='{Binding IsSelected, Mode=OneWay}' "
        + "AutomationProperties.AutomationId='{Binding Name, StringFormat=Test.Chip.{0}}' />"
        + "</DataTemplate>";

    public sealed class Option
    {
        public Option(string name, bool isSelected = false) { Name = name; IsSelected = isSelected; }
        public string Name { get; }
        public bool IsSelected { get; }
        public override string ToString() => Name;
    }

    [Fact]
    public void should_show_the_more_toggle_with_the_hidden_count_when_chips_overflow_a_fixed_width() => OnSta(() =>
    {
        // Arrange + Act — 폭 450 에 폭 100 칩 8 개
        using var hosted = Host(Options(8), width: 450);
        var toggle = hosted.Toggle!;

        // Assert
        Assert.Equal(Visibility.Visible, toggle.Visibility);
        Assert.True(hosted.Strip.HiddenCount > 0);
        Assert.Equal(8 - VisibleChips(hosted).Count, hosted.Strip.HiddenCount);
        Assert.Equal(ConsoleChipStripToggle.MoreText(hosted.Strip.HiddenCount), toggle.Text);
        Assert.Equal(ConsoleChipStripToggle.MoreAutomationName(hosted.Strip.HiddenCount), AutomationProperties.GetName(toggle));
        Assert.Equal("Test.Strip.More", AutomationProperties.GetAutomationId(toggle));
        Assert.False(toggle.IsChecked == true);

        // [더 보기]까지 한 줄 폭 안에 선다 · 판정은 순수 함수와 같다
        var right = toggle.TranslatePoint(new Point(toggle.ActualWidth, 0), hosted.Strip).X;
        Assert.True(right <= 450.5, $"[더 보기] 오른쪽 끝 {right} 가 줄 폭 450 을 넘었다");
        var expected = ConsoleChipStripMath.Fit(450, Enumerable.Repeat(100.0, 8).ToArray(), toggle.DesiredSize.Width, -1);
        Assert.Equal(expected.HiddenCount, hosted.Strip.HiddenCount);
    });

    [Fact]
    public void should_expand_to_show_every_chip_when_the_toggle_is_toggled_by_automation() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        var collapsedHeight = hosted.Strip.ActualHeight;

        // Act — UIA 토글 패턴(클릭 · Space · Enter 와 같은 길: IsChecked → IsExpanded)
        Toggle(hosted.Toggle!);
        hosted.Window.UpdateLayout();

        // Assert — 칩 8 개가 모두 제 크기로, 줄 폭 안에, 여러 줄로
        Assert.True(hosted.Strip.IsExpanded);
        Assert.Equal(0, hosted.Strip.HiddenCount);
        Assert.Equal(8, VisibleChips(hosted).Count);
        foreach (var chip in Chips(hosted))
        {
            var origin = chip.TranslatePoint(new Point(0, 0), hosted.Strip);
            Assert.True(origin.X >= -0.5 && origin.X + chip.ActualWidth <= 450.5, $"{chip.Content} 가 줄 폭 밖(x={origin.X})");
        }
        // 줄마다 높이가 다를 수 있다(기본 RadioButton 은 [더 보기]보다 낮다) — 높이가 늘고 칩이 두 줄 이상에 선다
        Assert.True(hosted.Strip.ActualHeight > collapsedHeight + 10, $"펼치면 아래로 밀어 낸다({collapsedHeight} → {hosted.Strip.ActualHeight})");
        var rows = Chips(hosted).Select(c => Math.Round(c.TranslatePoint(new Point(0, 0), hosted.Strip).Y)).Distinct().Count();
        Assert.True(rows >= 2, $"칩이 여러 줄로 흘러야 한다(줄 {rows})");
        Assert.Equal(ConsoleChipStripToggle.CollapseText, hosted.Toggle!.Text);
        Assert.Equal(ConsoleChipStripToggle.CollapseText, AutomationProperties.GetName(hosted.Toggle));

        // 다시 토글 — 접힌 한 줄로
        Toggle(hosted.Toggle);
        hosted.Window.UpdateLayout();
        Assert.False(hosted.Strip.IsExpanded);
        Assert.True(hosted.Strip.HiddenCount > 0);
        Assert.Equal(collapsedHeight, hosted.Strip.ActualHeight, 1);
    });

    [Fact]
    public void should_keep_the_selected_chip_visible_before_the_toggle_when_it_would_be_hidden() => OnSta(() =>
    {
        // Arrange — 맨 뒤 칩(h)을 고른 채
        var options = Options(8, selected: 7);
        using var hosted = Host(options, width: 450);

        // Assert
        var visible = VisibleChips(hosted);
        Assert.Contains(visible, c => (string)c.Content == "h");
        var h = visible.Single(c => (string)c.Content == "h");
        var hx = h.TranslatePoint(new Point(0, 0), hosted.Strip).X;
        var tx = hosted.Toggle!.TranslatePoint(new Point(0, 0), hosted.Strip).X;
        Assert.True(hx + h.ActualWidth <= tx + 0.5, "고른 칩은 [더 보기] 바로 앞에 선다");
        Assert.True(visible.All(c => c == h || c.TranslatePoint(new Point(0, 0), hosted.Strip).X < hx), "앞 칩들은 순서대로 그 앞에");
    });

    [Fact]
    public void should_bring_a_newly_selected_hidden_chip_into_the_collapsed_line_when_selection_changes() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        var g = Chips(hosted).Single(c => (string)c.Content == "g");
        Assert.DoesNotContain(g, VisibleChips(hosted));

        // Act — 자동화의 Select()(가려진 칩에도 닿는다)
        var peer = UIElementAutomationPeer.CreatePeerForElement(g)!;
        ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)!).Select();
        hosted.Window.UpdateLayout();

        // Assert
        Assert.Contains(g, VisibleChips(hosted));
        Assert.False(hosted.Strip.IsExpanded);
    });

    [Fact]
    public void should_expand_when_keyboard_focus_reaches_a_hidden_chip() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        var hidden = Chips(hosted).First(c => !VisibleChips(hosted).Contains(c));

        // Act — Tab · UIA SetFocus 가 가려진 칩에 닿은 것과 같은 이벤트
        hidden.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, hidden) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
        hosted.Window.UpdateLayout();

        // Assert
        Assert.True(hosted.Strip.IsExpanded, "보이지 않는 칩에 초점이 머물면 안 된다 — 펼쳐야 한다");
        Assert.Contains(hidden, VisibleChips(hosted));
    });

    [Fact]
    public void should_hide_the_toggle_when_the_strip_is_widened_to_fit_every_chip() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        Assert.Equal(Visibility.Visible, hosted.Toggle!.Visibility);

        // Act — 폭이 늘었다(부모 크기 변화 → 레이아웃이 다시 잰다)
        hosted.Frame.Width = 900;
        hosted.Window.UpdateLayout();

        // Assert
        Assert.Equal(Visibility.Collapsed, hosted.Toggle.Visibility);
        Assert.Equal(0, hosted.Strip.HiddenCount);
        Assert.Equal(8, VisibleChips(hosted).Count);

        // 다시 좁히면 되돌아온다
        hosted.Frame.Width = 450;
        hosted.Window.UpdateLayout();
        Assert.Equal(Visibility.Visible, hosted.Toggle.Visibility);
        Assert.True(hosted.Strip.HiddenCount > 0);
    });

    [Fact]
    public void should_show_the_toggle_when_added_items_start_to_overflow() => OnSta(() =>
    {
        var options = new ObservableCollection<Option>(Options(3));
        using var hosted = Host(options, width: 450);
        Assert.True(hosted.Toggle is null || hosted.Toggle.Visibility == Visibility.Collapsed, "3 개는 다 들어간다");

        // Act — 항목이 늘었다(CollectionChanged → 항목 생성기가 칸을 더한다)
        foreach (var name in new[] { "x", "y", "z" }) options.Add(new Option(name));
        hosted.Window.UpdateLayout();

        // Assert
        Assert.Equal(Visibility.Visible, hosted.Toggle!.Visibility);
        Assert.Equal(ConsoleChipStripToggle.MoreText(hosted.Strip.HiddenCount), hosted.Toggle.Text);
        Assert.True(hosted.Strip.HiddenCount > 0);
    });

    [Fact]
    public void should_expose_the_toggle_and_hidden_chips_to_automation_when_collapsed() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);

        var stripPeer = UIElementAutomationPeer.CreatePeerForElement(hosted.Strip)!;
        var children = ControlChildren(stripPeer);
        var togglePeer = UIElementAutomationPeer.CreatePeerForElement(hosted.Toggle!)!;

        // [더 보기]는 줄의 자식 · 토글 패턴 · 단추 — Tab 이 닿는다(칩 다음)
        Assert.Contains(togglePeer, children);
        Assert.Same(togglePeer, children[^1]);
        Assert.NotNull(togglePeer.GetPattern(PatternInterface.Toggle));
        Assert.Equal(AutomationControlType.Button, togglePeer.GetAutomationControlType());
        Assert.True(hosted.Toggle!.Focusable && hosted.Toggle.IsTabStop);
        Assert.Equal(nameof(ConsoleChipStrip), stripPeer.GetClassName());

        // 가려진 칩도 트리에 남되(자동화가 id 로 찾아 고를 수 있다) 경계는 0 — 좌표 클릭이 [더 보기]를 대신 누르지 않게
        var hidden = Chips(hosted).Where(c => !VisibleChips(hosted).Contains(c)).ToList();
        Assert.NotEmpty(hidden);
        foreach (var chip in hidden)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(chip)!;
            Assert.Contains(peer, children);
            var bounds = chip.TransformToAncestor(hosted.Strip).TransformBounds(new Rect(chip.RenderSize));
            Assert.True(bounds.Width <= 0.01 && bounds.Height <= 0.01, $"{chip.Content} 경계 {bounds}");
        }
    });

    [Fact]
    public void should_cap_the_expanded_height_and_scroll_inside_when_max_expanded_height_is_set() => OnSta(() =>
    {
        // Arrange — 폭 250(칩 2 개/줄) · 20 개 → 펼치면 10 줄이 넘는다
        using var hosted = Host(Options(20), width: 250);
        hosted.Strip.MaxExpandedHeight = 90;

        // Act
        hosted.Strip.IsExpanded = true;
        hosted.Window.UpdateLayout();

        // Assert — 줄 높이는 90 에서 멈추고 칩 칸 안에서 굴린다(아래 내용 · [⌃ 접기]가 화면 밖으로 가지 않게)
        Assert.True(hosted.Strip.ActualHeight <= 90.5, $"펼친 높이 {hosted.Strip.ActualHeight} 가 최대 90 을 넘었다");
        var scroll = FindFirst<ScrollViewer>(hosted.Strip)!;
        Assert.True(scroll.ScrollableHeight > 0, "넘친 칩은 칩 칸 안에서 굴려 닿아야 한다");
        Assert.Equal(0, hosted.Strip.HiddenCount);
    });

    [Fact]
    public void should_scroll_the_selected_chip_into_view_when_a_capped_strip_is_expanded() => OnSta(() =>
    {
        // Arrange — 20 개 중 맨 뒤(t)를 고른 채, 최대 높이 90
        using var hosted = Host(Options(20, selected: 19), width: 250);
        hosted.Strip.MaxExpandedHeight = 90;
        hosted.Window.UpdateLayout();

        // Act — 펼친 뒤 디스패처를 한 바퀴(Loaded) 돌린다
        hosted.Strip.IsExpanded = true;
        hosted.Window.UpdateLayout();
        PumpUntilIdle();

        // Assert — 고른 칩이 칩 칸의 보이는 자리 안에 있다
        var scroll = FindFirst<ScrollViewer>(hosted.Strip)!;
        var t = Chips(hosted).Single(c => (string)c.Content == "t");
        var top = t.TranslatePoint(new Point(0, 0), scroll).Y;
        Assert.True(scroll.VerticalOffset > 0, "맨 뒤 칩을 보이려면 굴려야 한다");
        Assert.True(top >= -0.5 && top + t.ActualHeight <= scroll.ViewportHeight + 0.5, $"고른 칩이 보이는 자리 밖(top={top}, viewport={scroll.ViewportHeight})");
    });

    [Fact]
    public void should_apply_the_first_line_height_to_the_first_line_only_when_expanded() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        hosted.Strip.FirstLineHeight = 60;
        hosted.Window.UpdateLayout();
        Assert.Equal(60, hosted.Strip.ActualHeight, 1);   // 접힌 줄 = 첫 줄

        hosted.Strip.IsExpanded = true;
        hosted.Window.UpdateLayout();

        var ys = Chips(hosted).Select(c => c.TranslatePoint(new Point(0, 0), hosted.Strip).Y).Distinct().OrderBy(y => y).ToList();
        var chipHeight = Chips(hosted)[0].ActualHeight;
        Assert.True(ys.Count >= 2);
        Assert.Equal((60 - chipHeight) / 2, ys[0], 1);                 // 첫 줄은 60 안에서 가운데
        Assert.Equal(60, ys[1], 1);                                    // 둘째 줄은 첫 줄 바로 아래, 칩 높이 그대로
    });

    [Fact]
    public void should_pass_the_mouse_wheel_to_the_parent_when_there_is_nothing_to_scroll() => OnSta(() =>
    {
        using var hosted = Host(Options(8), width: 450);
        var received = 0;
        hosted.Frame.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler((_, _) => received++), handledEventsToo: false);

        // Act — 접힌 줄 위 휠(ScrollViewer 는 굴릴 것이 없어도 휠을 삼킨다)
        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        hosted.Strip.RaiseEvent(args);

        // Assert
        Assert.True(args.Handled);
        Assert.Equal(1, received);
    });

    // ── 준비 ──

    /// <summary>UIA 기본 보기의 자식 — 컨트롤이 아닌 peer(템플릿 속 ScrollViewer 등)는 건너 그 자식을 본다.</summary>
    private static List<AutomationPeer> ControlChildren(AutomationPeer peer)
    {
        var result = new List<AutomationPeer>();
        foreach (var child in peer.GetChildren() ?? new List<AutomationPeer>())
        {
            if (child.IsControlElement()) result.Add(child);
            else result.AddRange(ControlChildren(child));
        }
        return result;
    }

    private static void PumpUntilIdle()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindFirst<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static Option[] Options(int count, int selected = -1)
        => Enumerable.Range(0, count).Select(i => new Option(((char)('a' + i)).ToString(), i == selected)).ToArray();

    private sealed class Hosted : IDisposable
    {
        public required Window Window { get; init; }
        public required Border Frame { get; init; }
        public required ConsoleChipStrip Strip { get; init; }
        public ConsoleChipStripToggle? Toggle => FindPanel(Strip)?.MoreToggle;
        public void Dispose() => Window.Close();
    }

    private static Hosted Host(IEnumerable<Option> items, double width)
    {
        var strip = new ConsoleChipStrip { ItemTemplate = (DataTemplate)XamlReader.Parse(ChipTemplate), ItemsSource = items, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetAutomationId(strip, "Test.Strip");
        var frame = new Border { Width = width, Child = strip, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window
        {
            Content = frame, Width = 1200, Height = 400, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
        };
        window.Show();
        window.UpdateLayout();
        return new Hosted { Window = window, Frame = frame, Strip = strip };
    }

    private static void Toggle(ToggleButton toggle)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle)!;
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
    }

    private static ConsoleChipStripPanel? FindPanel(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ConsoleChipStripPanel panel) return panel;
            if (FindPanel(child) is { } nested) return nested;
        }
        return null;
    }

    private static List<RadioButton> Chips(Hosted hosted)
    {
        var panel = FindPanel(hosted.Strip)!;
        return panel.Children.OfType<ContentPresenter>().Select(cp => (RadioButton)VisualTreeHelper.GetChild(cp, 0)).ToList();
    }

    /// <summary>화면에 제 크기로 선 칩(패널이 가리지 않은 칩).</summary>
    private static List<RadioButton> VisibleChips(Hosted hosted)
    {
        var panel = FindPanel(hosted.Strip)!;
        return Chips(hosted).Where((_, i) => !panel.IsChipHidden(i)).ToList();
    }

    private static void OnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
