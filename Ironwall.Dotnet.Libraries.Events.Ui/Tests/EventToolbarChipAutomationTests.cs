using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 이벤트 콘솔 툴바의 칩 — 기간 칩(오늘 · 24시간 · 7일 · 직접)과 억제 상태 칩 — 이 보조 기술(UIA)에 <b>칩 그대로</b>
/// 선택 패턴까지 나오는가. 둘 다 커널 칩 묶음(<see cref="ConsoleChipGroup"/>)이어야 한다.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 SC-EVT-013 · SC-EVT-014 · SC-SUP-003 — 칩이 화면에는 있는데 자동화가 찾지 못했다("항목 peer 는 있는데 자식 0").
/// 평범한 ItemsControl 의 항목 peer 가 콘솔을 닫았다 다시 열 때 옛 칩을 쥔 채 남는 것이 원인이다 — 그 길 자체는 커널 시험
/// (Utils.Tests <c>ConsoleChipGroupAutomationTests</c>)이 진짜 UIA 클라이언트로 밟는다. 여기서는 실제 XAML 의 두 묶음이
/// 그 커널 묶음이고, 칩마다 선택 패턴이 닿는지를 본다(스타일 · 클릭 처리기 · 보임 바인딩만 뺀다 — peer 모양과 무관).
/// </remarks>
public class EventToolbarChipAutomationTests
{
    private static string DashboardXamlPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Dashboards", "EventDashboardView.xaml");

    /// <summary>AutomationId 로 칩 묶음 요소 하나를 통째로 떼어 낸다.</summary>
    private static string ExtractChipGroup(string automationId)
    {
        var xaml = File.ReadAllText(DashboardXamlPath());
        var at = xaml.IndexOf($"AutomationProperties.AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{automationId} 를 XAML 에서 찾지 못했다");
        var open = xaml.LastIndexOf('<', at);
        var tag = Regex.Match(xaml[(open + 1)..], @"^[\w:.]+").Value;
        Assert.Equal("c:ConsoleChipGroup", tag);      // 평범한 ItemsControl 로 되돌리면 여기서 먼저 걸린다
        var close = xaml.IndexOf($"</{tag}>", at, StringComparison.Ordinal);
        var fragment = xaml[open..(close + tag.Length + 3)];
        fragment = Regex.Replace(fragment, @"\s(Style|Visibility)=""\{[^""]*\}""", "");
        fragment = Regex.Replace(fragment, @"\sClick=""[^""]*""", "");
        return fragment.Insert(tag.Length + 1,
            " xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'"
            + " xmlns:c='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils'");
    }

    public sealed class SuppressionHolder
    {
        public SuppressionHolder(IReadOnlyList<SuppressionFilterOption> filters) => Filters = filters;
        public IReadOnlyList<SuppressionFilterOption> Filters { get; }
    }

    public sealed class Context
    {
        public IReadOnlyList<EventPeriodOption> PeriodOptions { get; init; } = Array.Empty<EventPeriodOption>();
        public SuppressionHolder? Suppression { get; init; }
    }

    [Fact]
    public void should_expose_each_period_chip_with_a_selection_pattern_when_the_toolbar_is_read_by_automation()
    {
        var periods = new[] { "오늘", "24시간", "7일", "직접" };
        var context = new Context { PeriodOptions = periods.Select(p => new EventPeriodOption(p)).ToList() };

        var chips = ChipPeers(ExtractChipGroup("Console.Events.Toolbar.Period"), context);

        Assert.All(periods, p => Assert.True(chips.TryGetValue($"Console.Events.Period.{p}", out var selectable) && selectable,
            $"Console.Events.Period.{p} — {(chips.ContainsKey($"Console.Events.Period.{p}") ? "선택 패턴 없음" : "묶음의 자식이 아님")} · 본 것 [{string.Join(", ", chips.Keys)}]"));
    }

    [Fact]
    public void should_expose_each_suppression_status_chip_with_a_selection_pattern_when_the_toolbar_is_read_by_automation()
    {
        var keys = new[] { "all", "suppressing", "active", "pending", "terminal" };
        var context = new Context { Suppression = new SuppressionHolder(keys.Select(k => new SuppressionFilterOption(k, k)).ToList()) };

        var chips = ChipPeers(ExtractChipGroup("Console.Suppression.Toolbar.Filter"), context);

        Assert.All(keys, k => Assert.True(chips.TryGetValue($"Console.Suppression.Filter.{k}", out var selectable) && selectable,
            $"Console.Suppression.Filter.{k} — 본 것 [{string.Join(", ", chips.Keys)}]"));
    }

    /// <summary>묶음을 화면 밖 창에 띄우고, 묶음 peer 의 <b>직속 자식</b>(항목 peer 층 없이)의 id → 선택 패턴 유무를 모은다.</summary>
    private static Dictionary<string, bool> ChipPeers(string fragment, Context context)
    {
        var found = new Dictionary<string, bool>();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var group = (FrameworkElement)XamlReader.Parse(fragment);
                Assert.IsType<ConsoleChipGroup>(group);
                group.DataContext = context;
                var window = new Window
                {
                    Content = group, Width = 520, Height = 120, Left = -20000, Top = -20000,
                    ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
                };
                window.Show();
                window.UpdateLayout();
                var peer = UIElementAutomationPeer.CreatePeerForElement(group)!;
                foreach (var child in peer.GetChildren() ?? new List<AutomationPeer>())
                    found[child.GetAutomationId()] = child.GetPattern(PatternInterface.SelectionItem) != null;
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return found;
    }
}
