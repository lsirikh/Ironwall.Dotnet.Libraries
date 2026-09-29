using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 억제 서랍 [단발] / [주간 반복] 라디오 — 실제 XAML 바인딩으로 VM 이 주간 반복으로 바뀌고 <b>그대로 남는가</b>.
/// </summary>
/// <remarks>
/// 헤디드 r10~r17 SC-SUP-007c · 008 · 006 이 "[주간 반복]이 켜지지 않았다(요약 '→ 단발 · 10-02 03:00 ~ 11-01 03:00')" 로 SKIP 됐다.
/// 요약의 끝이 시작 +30일 — IsWeekly 세터가 참으로 한 번 돌았다(단발 기본 끝은 +1시간)는 뜻이고, 그 뒤 누군가 거짓으로 되돌렸다.
/// 되돌릴 수 있는 것은 두 라디오뿐이다. 원인은 <c>GroupName="SuppressionRecurrence"</c>: WPF 는 같은 GroupName 을 앱 전역에서 모으고
/// 같은 시각 루트끼리 서로 끈다. 이벤트 창을 닫을 때마다(Screen close → 뷰 캐시 비움) 새 EventDashboardView 가 생기고, 옛 뷰는
/// 뷰모델 이벤트 구독으로 살아 남아 <b>같은 서랍 뷰모델</b>에 붙어 있다 — 창에서 떨어진 옛 사본들은 루트가 null 로 서로 한 무리가 되어,
/// 참이 퍼지는 순간 서로의 [주간 반복] 을 끄고 그 TwoWay 바인딩이 IsWeekly=false 를 써 넣었다. 사람이 눌러도 똑같다.
/// 한 사본만 있는 미리보기 · 헤드리스(실제 뷰 + 실제 UIA 클라이언트 Select)에서는 멀쩡했다 — 그래서 사본을 세워 재현한다.
/// </remarks>
public class SuppressionRecurrenceRadioTests
{
    private const string OneShotId = "Console.Suppression.Form.Recurrence.OneShot";
    private const string WeeklyId = "Console.Suppression.Form.Recurrence.Weekly";

    private static string DrawerXamlPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Consoles", "SuppressionDrawerView.xaml");

    /// <summary>실제 XAML 에서 반복 라디오 두 개를 담은 StackPanel 을 떼어 낸다(스타일만 뺀다 — 바인딩 · GroupName 은 그대로).</summary>
    private static string ExtractRecurrenceRow()
    {
        var xaml = File.ReadAllText(DrawerXamlPath());
        var at = xaml.IndexOf($"AutomationProperties.AutomationId=\"{OneShotId}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{OneShotId} 를 XAML 에서 찾지 못했다");
        var open = xaml.LastIndexOf("<StackPanel", at, StringComparison.Ordinal);
        var close = xaml.IndexOf("</StackPanel>", at, StringComparison.Ordinal);
        var fragment = xaml[open..(close + "</StackPanel>".Length)];
        Assert.Contains(WeeklyId, fragment);
        fragment = Regex.Replace(fragment, @"\sStyle=""\{[^""]*\}""", "");
        return fragment.Insert("<StackPanel".Length,
            " xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'");
    }

    private static T RunSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var t = new Thread(() => { try { result = body(); } catch (Exception ex) { failure = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        Assert.True(t.Join(TimeSpan.FromSeconds(20)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure != null) throw failure;
        return result;
    }

    private static SuppressionDrawerViewModel NewDrawer()
    {
        var drawer = new SuppressionDrawerViewModel(new FakeClock(new DateTime(2026, 9, 30, 10, 0, 0)), null, null,
            (_, _) => Task.FromResult(new SuppressionSaveOutcome(true, "저장했습니다.", null)), () => true);
        drawer.OpenNew();
        return drawer;
    }

    /// <summary>반복 줄 사본 하나 — 서랍 뷰모델에 붙인다.</summary>
    private static Panel NewRow(SuppressionDrawerViewModel drawer)
    {
        var row = (Panel)XamlReader.Parse(ExtractRecurrenceRow());
        row.DataContext = drawer;
        return row;
    }

    private static RadioButton Radio(Panel row, string id)
    {
        foreach (var child in row.Children)
            if (child is RadioButton rb && AutomationProperties.GetAutomationId(rb) == id) return rb;
        throw new InvalidOperationException(id);
    }

    private static Window Show(UIElement content)
    {
        var w = new Window
        {
            Content = content, Width = 400, Height = 200, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None,
        };
        w.Show();
        w.UpdateLayout();
        return w;
    }

    /// <summary>[주간 반복] 을 자동화(UIA SelectionItem)로 고른다 — 헤디드와 같은 경로(RadioButtonAutomationPeer.Select).</summary>
    private static void SelectWeekly(Panel row)
        => ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(Radio(row, WeeklyId))
            .GetPattern(PatternInterface.SelectionItem)).Select();

    private static string Describe(SuppressionDrawerViewModel drawer)
        => $"IsWeekly={drawer.IsWeekly} · 요약 '{drawer.RecapText}' · 끝 '{drawer.WindowEndText}'";

    [Fact]
    public void should_switch_drawer_to_weekly_when_the_weekly_radio_is_selected_through_automation()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var (isWeekly, isChecked, text) = RunSta(() =>
        {
            var drawer = NewDrawer();
            var row = NewRow(drawer);
            var w = Show(row);
            SelectWeekly(row);
            var r = (drawer.IsWeekly, Radio(row, WeeklyId).IsChecked == true, Describe(drawer));
            w.Close();
            return r;
        });

        Assert.True(isWeekly, text);
        Assert.True(isChecked, text);
    }

    [Fact]
    public void should_keep_weekly_when_old_detached_drawer_copies_still_share_the_viewmodel()
    {
        // 이벤트 창을 두 번 닫았다 연 셈 — 창에서 떨어졌지만 살아 있는 옛 서랍 두 벌이 같은 뷰모델에 붙어 있다.
        PlatformProvider.Current = new DefaultPlatformProvider();
        var (isWeekly, liveChecked, text) = RunSta(() =>
        {
            var drawer = NewDrawer();
            var oldA = NewRow(drawer);
            var oldB = NewRow(drawer);
            var live = NewRow(drawer);
            var w = Show(live);
            SelectWeekly(live);
            var r = (drawer.IsWeekly, Radio(live, WeeklyId).IsChecked == true, Describe(drawer));
            w.Close();
            GC.KeepAlive(oldA);
            GC.KeepAlive(oldB);
            return r;
        });

        Assert.True(isWeekly, $"옛 사본이 [주간 반복] 을 되돌렸다 — {text}");
        Assert.True(liveChecked, text);
    }

    [Fact]
    public void should_keep_weekly_when_two_drawer_copies_share_the_viewmodel_in_one_window()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        var (isWeekly, text) = RunSta(() =>
        {
            var drawer = NewDrawer();
            var first = NewRow(drawer);
            var second = NewRow(drawer);
            var host = new StackPanel();
            host.Children.Add(first);
            host.Children.Add(second);
            var w = Show(host);
            SelectWeekly(first);
            var r = (drawer.IsWeekly, Describe(drawer));
            w.Close();
            return r;
        });

        Assert.True(isWeekly, $"같은 창의 다른 사본이 [주간 반복] 을 되돌렸다 — {text}");
    }

    [Fact]
    public void should_return_to_one_shot_when_the_one_shot_radio_is_selected_after_weekly()
    {
        // 묶음을 부모로 바꾼 뒤에도 두 라디오는 서로를 끈다(같은 StackPanel).
        PlatformProvider.Current = new DefaultPlatformProvider();
        var (isWeekly, weeklyChecked, text) = RunSta(() =>
        {
            var drawer = NewDrawer();
            var row = NewRow(drawer);
            var w = Show(row);
            SelectWeekly(row);
            ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(Radio(row, OneShotId))
                .GetPattern(PatternInterface.SelectionItem)).Select();
            var r = (drawer.IsWeekly, Radio(row, WeeklyId).IsChecked == true, Describe(drawer));
            w.Close();
            return r;
        });

        Assert.False(isWeekly, text);
        Assert.False(weeklyChecked, text);
    }
}
