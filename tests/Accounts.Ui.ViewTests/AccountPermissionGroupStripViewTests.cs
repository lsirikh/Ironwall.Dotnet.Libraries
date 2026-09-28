using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 계정 콘솔 <b>실제 뷰</b> — 권한 설정의 '권한 그룹' 칩 줄이 좁은 폭에서 잘려도 [⌄ 더 보기 +N]로 나머지 칩에 닿는다.
/// </summary>
/// <remarks>
/// 2026-09-28 사용자 보고: "권한 필터에서 상단 필터쪽 거기가 짤린다. 스크롤을 넣던지 아니면 아래 꺽쇠로 더 보기 같은 것을 만들어줘야될거 같아".
/// 종전 칩 줄은 가로 스크롤 막대를 숨긴 ScrollViewer + 가로 휠 처리기라, 잘린 칩으로 갈 길이 화면에 없었다.
/// 커널 <see cref="ConsoleChipStrip"/> 으로 바꾸고, 진짜 UIA 클라이언트(프로세스 안, 다른 스레드)로 [더 보기]를 토글해 모든 그룹 칩이
/// 화면에 서는지 본다.
/// </remarks>
public class AccountPermissionGroupStripViewTests
{
    private const string StripId = "Console.Accounts.GroupStrip";
    private const string MoreId = StripId + ".More";

    [Fact]
    public void should_show_the_more_toggle_and_reach_every_group_chip_by_automation_when_the_group_strip_overflows() => AppHost.Run(() =>
    {
        var groups = Groups(14);
        var (view, window, console) = HostPermissions(groups, width: 1100);
        try
        {
            // Arrange — 접힌 한 줄: [더 보기 +N]이 있고 일부 칩은 가려져 있다
            var toggle = Find<ConsoleChipStripToggle>(view, t => AutomationProperties.GetAutomationId(t) == MoreId);
            Assert.True(toggle is { IsVisible: true }, "좁은 폭에서 [더 보기]가 보여야 한다");
            var strip = Find<ConsoleChipStrip>(view, s => AutomationProperties.GetAutomationId(s) == StripId)!;
            Assert.True(strip.HiddenCount > 0, "14개 그룹이면 일부는 가려져야 한다");
            Assert.Equal(ConsoleChipStripToggle.MoreText(strip.HiddenCount), toggle!.Text);
            var collapsedHeight = strip.ActualHeight;
            var hiddenBefore = strip.HiddenCount;

            // Act — 진짜 UIA 클라이언트로: 이름을 읽고, 토글하고, 모든 칩을 찾는다
            var hwnd = new WindowInteropHelper(window).Handle;
            var reading = RunUia(() =>
            {
                var top = AutomationElement.FromHandle(hwnd);
                var more = Wait(() => top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, MoreId)));
                var collapsedName = more?.Current.Name;
                var zeroBefore = groups.Count(g => ChipRect(top, g.Id) is { } r && (r.IsEmpty || r.Width <= 0));
                ((TogglePattern)more!.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
                Thread.Sleep(200);
                var widths = groups.ToDictionary(g => g.Id, g => Wait(() => ChipRect(top, g.Id) is { IsEmpty: false, Width: > 0 } r ? r : (Rect?)null));
                var expandedName = more.Current.Name;
                return (collapsedName, zeroBefore, widths, expandedName);
            });

            // Assert
            Assert.Equal(ConsoleChipStripToggle.MoreAutomationName(hiddenBefore), reading.collapsedName);
            Assert.True(reading.zeroBefore > 0, "접힌 줄에서는 가려진 칩(크기 0)이 있어야 한다");
            var missing = reading.widths.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
            Assert.True(missing.Count == 0, "펼친 뒤 화면에 서지 않은 그룹 칩: " + string.Join(",", missing));
            Assert.True(strip.IsExpanded, "UIA 토글이 줄을 펼쳐야 한다");
            Assert.Equal(ConsoleChipStripToggle.CollapseText, reading.expandedName);
            Assert.True(strip.ActualHeight > collapsedHeight + 10, $"펼치면 아래 내용을 밀어 내야 한다({collapsedHeight} → {strip.ActualHeight})");
            Assert.True(strip.ActualHeight <= strip.MaxExpandedHeight + 0.5, $"14개여도 펼친 높이는 {strip.MaxExpandedHeight} 에서 멈춘다(표 · [접기]가 화면에 남게) — {strip.ActualHeight}");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_keep_the_selected_group_chip_visible_when_it_would_fall_behind_the_more_toggle() => AppHost.Run(() =>
    {
        var groups = Groups(14);
        var (view, window, console) = HostPermissions(groups, width: 1100);
        try
        {
            // Arrange — 맨 뒤 그룹을 고른다(접힌 줄이라면 가려질 차례)
            var last = console.Matrix.Groups[^1];
            console.Matrix.SelectedGroup = last;
            AppHost.Pump(DispatcherPriority.ApplicationIdle);

            // Assert
            var strip = Find<ConsoleChipStrip>(view, s => AutomationProperties.GetAutomationId(s) == StripId)!;
            Assert.False(strip.IsExpanded);
            Assert.True(strip.HiddenCount > 0);
            var chip = Find<RadioButton>(view, r => AutomationProperties.GetAutomationId(r) == $"Console.Accounts.GroupRow.{last.GroupId}")!;
            Assert.True(chip.IsChecked == true);
            var origin = chip.TranslatePoint(new Point(0, 0), strip);
            Assert.True(chip.ActualWidth > 0 && origin.X + chip.ActualWidth <= strip.ActualWidth + 0.5,
                $"고른 칩이 접힌 줄 안에 보여야 한다(x={origin.X}, w={chip.ActualWidth}, 줄 폭={strip.ActualWidth})");
            var toggle = Find<ConsoleChipStripToggle>(view, t => AutomationProperties.GetAutomationId(t) == MoreId)!;
            Assert.True(toggle.TranslatePoint(new Point(0, 0), strip).X >= origin.X + chip.ActualWidth - 0.5, "고른 칩은 [더 보기] 앞에 선다");
        }
        finally { window.Close(); }
    });

    /// <summary>육안 검토용 — <c>B3_SNAPSHOT_DIR</c> 가 있을 때만 라이트 · 다크 × 접힘 · 펼침 PNG 를 남긴다(없으면 렌더만).</summary>
    [Fact]
    public void should_render_the_group_strip_collapsed_and_expanded_in_light_and_dark_when_snapshots_are_requested() => AppHost.Run(() =>
    {
        var directory = Environment.GetEnvironmentVariable("B3_SNAPSHOT_DIR");
        var (view, window, console) = HostPermissions(Groups(14), width: 1100);
        try
        {
            console.Matrix.SelectedGroup = console.Matrix.Groups[^1];
            var strip = Find<ConsoleChipStrip>(view, s => AutomationProperties.GetAutomationId(s) == StripId)!;
            foreach (var theme in new[] { "light", "dark" })
            {
                AppHost.SetDark(theme == "dark");
                foreach (var expanded in new[] { false, true })
                {
                    strip.IsExpanded = expanded;
                    AppHost.Pump(DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    Assert.True(strip.ActualHeight > 0);
                    if (directory is not null)
                        AppHost.Save(window, System.IO.Path.Combine(directory, $"{theme}-accounts-group-strip-{(expanded ? "expanded" : "collapsed")}.png"));
                }
            }
        }
        finally
        {
            AppHost.SetDark(false);
            window.Close();
        }
    });

    // ── 호스트와 같은 결선 ──

    private static UserGroupDto[] Groups(int count)
        => Enumerable.Range(1, count)
                     .Select(i => new UserGroupDto { Id = 100 + i, Name = $"LRT-UI-R2RPT-{190000 + i}", IsActive = true, Permissions = new PermissionsDto() })
                     .ToArray();

    private static (AccountConsolePanelView View, Window Window, TestAccountConsole Console) HostPermissions(IEnumerable<UserGroupDto> groups, double width)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        IoC.BuildUp = _ => { };
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.GetInstance = (_, _) => null!;

        var list = groups.ToList();
        var (console, _, _, _, _) = ConsoleFixtures.Build(new[] { ConsoleFixtures.User(1, "op1", "김운영") }, list);
        WaitTask(console.ActivateForTestAsync());

        var view = new AccountConsolePanelView { Width = width, Height = 800 };
        ((IViewAware)console).AttachView(view);
        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, console);
        AppHost.Pump(DispatcherPriority.Loaded);
        AppHost.Pump();

        var rail = Find<ConsoleRail>(view, r => AutomationProperties.GetAutomationId(r) == "Console.Accounts.Rail")!;
        var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
        var item = peer.GetChildren()!.OfType<ListBoxItemAutomationPeer>().First(p => (p.Item as ConsoleRailEntry)?.Key == AccountConsoleKeys.Permissions);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (console.Matrix.Groups.Count < list.Count && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.True(console.IsPermissionsRail);
        Assert.Equal(list.Count, console.Matrix.Groups.Count);
        return (view, window, console);
    }

    private static void WaitTask(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        task.GetAwaiter().GetResult();
    }

    /// <summary>UIA 클라이언트는 다른 스레드에서 — 앱 디스패처는 그동안 돌려 둔다(같은 스레드면 교착).</summary>
    private static T RunUia<T>(Func<T> body)
    {
        var task = Task.Run(body);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        Assert.True(task.IsCompleted, "UIA 클라이언트 제한 시간 초과");
        return task.GetAwaiter().GetResult();
    }

    private static Rect? ChipRect(AutomationElement top, int groupId)
        => top.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, $"Console.Accounts.GroupRow.{groupId}"))
              ?.Current.BoundingRectangle;

    /// <summary>레이아웃 · UIA 트리 갱신이 한 바퀴 돌 때까지 짧게 다시 본다(최대 3초).</summary>
    private static T? Wait<T>(Func<T?> probe)
    {
        for (var i = 0; i < 30; i++)
        {
            var value = probe();
            if (value is not null) return value;
            Thread.Sleep(100);
        }
        return default;
    }

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
