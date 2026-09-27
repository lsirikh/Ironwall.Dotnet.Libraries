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
/// 억제 서랍의 요일 칩(월~일)이 보조 기술(UIA)에 <b>토글</b>로 나오는가.
/// </summary>
/// <remarks>
/// 2026-09-27 헤디드 SC-SUP-007 — 요일을 전부 끄라고 했는데 요약이 "→ 월~금 …" 그대로였다. 칩을 <c>ItemsControl</c> 안에
/// 직접 넣어 두었더니 WPF 가 각 칩을 항목 peer(ItemsControlItem)로 감싸 <c>AutomationId</c> 만 그 항목에 넘기고
/// <b>토글 패턴은 버렸다</b> — 자동화도 화면 읽기도 요일을 켜고 끌 수 없었다. 서버 쪽 검증("요일을 하나 이상")은 멀쩡하다.
/// 이 시험은 실제 XAML 에서 요일 묶음을 그대로 떼어 peer 트리를 만든다(스타일만 뺀다 — 템플릿은 peer 모양과 무관).
/// </remarks>
public class SuppressionDayChipAutomationTests
{
    private static string DrawerXamlPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Consoles", "SuppressionDrawerView.xaml");

    /// <summary>AutomationId 로 요소 하나를 통째로 떼어 낸다(같은 이름의 요소가 안에 겹치지 않는다는 전제).</summary>
    private static string ExtractElement(string xaml, string automationId)
    {
        var at = xaml.IndexOf($"AutomationProperties.AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{automationId} 를 XAML 에서 찾지 못했다");
        var open = xaml.LastIndexOf('<', at);
        var tag = Regex.Match(xaml[(open + 1)..], @"^[\w:.]+").Value;
        var close = xaml.IndexOf($"</{tag}>", at, StringComparison.Ordinal);
        Assert.True(close > at, $"</{tag}> 를 찾지 못했다");
        var fragment = xaml[open..(close + tag.Length + 3)];
        fragment = Regex.Replace(fragment, @"\s(Style|IsChecked|IsEnabled)=""\{[^""]*\}""", "");
        return fragment.Insert(tag.Length + 1,
            " xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'");
    }

    [Fact]
    public void should_expose_each_weekday_chip_as_a_toggle_when_the_drawer_days_are_read_by_automation()
    {
        var fragment = ExtractElement(File.ReadAllText(DrawerXamlPath()), "Console.Suppression.Form.Days");
        var found = new Dictionary<string, bool>();
        Exception? failure = null;
        var t = new Thread(() =>
        {
            try
            {
                var root = (UIElement)XamlReader.Parse(fragment);
                var w = new Window { Content = root, Width = 400, Height = 120, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
                w.Show();
                w.UpdateLayout();
                void Walk(AutomationPeer p)
                {
                    var id = p.GetAutomationId();
                    if (id.StartsWith("Console.Suppression.Form.Day.", StringComparison.Ordinal))
                        found[id] = p.GetPattern(PatternInterface.Toggle) != null;
                    foreach (var c in p.GetChildren() ?? new List<AutomationPeer>()) Walk(c);
                }
                Walk(UIElementAutomationPeer.CreatePeerForElement(w));
                w.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (failure != null) throw failure;

        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        Assert.All(days, d => Assert.True(found.TryGetValue($"Console.Suppression.Form.Day.{d}", out var toggle) && toggle,
            $"Day.{d} — 트리에 {(found.ContainsKey($"Console.Suppression.Form.Day.{d}") ? "있지만 토글 패턴 없음" : "없음")} · 본 것 [{string.Join(", ", found.Select(kv => $"{kv.Key}={kv.Value}"))}]"));
    }
}
