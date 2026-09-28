using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 커널이 운영자에게 보이는 것을 보조 기술(UIA · 화면 읽기)에도 내놓는가.
/// 2026-09-27 헤디드 시험에서 레일 바닥 띠(요약 · 부대 필터)와 상세 바닥 문구("…을 저장했습니다")가 UIA 에 한 번도 나오지 않았다.
/// </summary>
[Collection(WpfApplicationCollection.Name)]
public class ConsoleAutomationExposureTests
{
    [Fact]
    public void should_expose_the_rail_footer_content_when_the_rail_has_a_footer()
    {
        var ids = OnSta(() =>
        {
            var rail = new ConsoleRail
            {
                Style = KernelStyle(typeof(ConsoleRail)),
                ConsoleKey = "Test",
                Footer = new CheckBox { Content = "예하 포함" },
            };
            AutomationProperties.SetAutomationId((CheckBox)rail.Footer, "Console.Test.Rail.Include");
            rail.Items.Add(new ConsoleRailEntry("all", "전체", null));
            Arrange(rail, 240, 600);

            var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
            return peer.GetChildren()?.Select(c => c.GetAutomationId()).ToList() ?? new List<string>();
        });

        Assert.Contains("Console.Test.Rail.Include", ids);
    }

    [Fact]
    public void should_expose_the_footer_by_its_automation_id_when_the_footer_root_is_a_panel_without_a_peer()
    {
        // 2026-09-28 헤디드 SC-KRN-001 — 이벤트 콘솔 바닥 띠(미조치 · 장애 진행)는 StackPanel 에 Console.Events.Rail.Footer 를 달았다.
        // StackPanel 은 제 peer 가 없어 그 id 가 UIA 에 한 번도 나오지 않았다(다른 콘솔은 TextBlock 한 개라 보였다).
        var (ids, footerChildren) = OnSta(() =>
        {
            var footer = new StackPanel();
            AutomationProperties.SetAutomationId(footer, "Console.Test.Rail.Footer");
            footer.Children.Add(new TextBlock { Text = "미조치 3건" });
            footer.Children.Add(new TextBlock { Text = "장애 진행 1건" });
            var rail = new ConsoleRail { Style = KernelStyle(typeof(ConsoleRail)), ConsoleKey = "Test", Footer = footer };
            rail.Items.Add(new ConsoleRailEntry("all", "전체", null));
            Arrange(rail, 240, 600);

            var children = UIElementAutomationPeer.CreatePeerForElement(rail)!.GetChildren() ?? new List<AutomationPeer>();
            var footerPeer = children.FirstOrDefault(c => c.GetAutomationId() == "Console.Test.Rail.Footer");
            var texts = footerPeer?.GetChildren()?.Select(c => c.GetName()).ToList() ?? new List<string>();
            return (children.Select(c => c.GetAutomationId()).ToList(), texts);
        });

        Assert.Contains("Console.Test.Rail.Footer", ids);
        Assert.Equal(new[] { "미조치 3건", "장애 진행 1건" }, footerChildren);   // 띠 안의 글은 그 아래에서 읽힌다
    }

    [Fact]
    public void should_not_expose_the_footer_group_when_the_footer_root_is_collapsed()
    {
        var ids = OnSta(() =>
        {
            var footer = new StackPanel { Visibility = Visibility.Collapsed };
            AutomationProperties.SetAutomationId(footer, "Console.Test.Rail.Footer");
            footer.Children.Add(new TextBlock { Text = "미조치 3건" });
            var rail = new ConsoleRail { Style = KernelStyle(typeof(ConsoleRail)), ConsoleKey = "Test", Footer = footer };
            rail.Items.Add(new ConsoleRailEntry("all", "전체", null));
            Arrange(rail, 240, 600);

            return UIElementAutomationPeer.CreatePeerForElement(rail)!.GetChildren()?.Select(c => c.GetAutomationId()).ToList() ?? new List<string>();
        });

        Assert.DoesNotContain("Console.Test.Rail.Footer", ids);   // 억제 레일처럼 띠를 접은 레일에서는 없는 것이 맞다
    }

    [Fact]
    public void should_put_template_text_in_the_control_view_when_it_is_a_console_text()
    {
        var (plain, console) = OnSta(() => (TemplatedTextIsControl(new FrameworkElementFactory(typeof(TextBlock))),
                                            TemplatedTextIsControl(new FrameworkElementFactory(typeof(ConsoleText)))));

        // WPF 기본: 템플릿 안 TextBlock 은 기본 보기에서 빠진다(바닥 문구가 안 보이던 이유)
        Assert.False(plain);
        Assert.True(console);
    }

    [Fact]
    public void should_draw_the_detail_footer_message_with_a_console_text_when_the_kernel_template_is_applied()
    {
        var isConsoleText = OnSta(() =>
        {
            var host = new ConsoleDetailHost { Style = KernelStyle(typeof(ConsoleDetailHost)), ConsoleKey = "Test" };
            Arrange(host, 420, 700);
            return host.Template.FindName("Msg", host) is ConsoleText;
        });

        Assert.True(isConsoleText);
    }

    [Fact]
    public void should_put_the_range_start_and_end_texts_in_the_control_view_when_the_range_field_is_templated()
    {
        // 2026-09-28 헤디드 SC-EVT-013 — 추이 끌기로 '직접' 기간이 되고 직접 지정 칸도 떴는데, 자동화는 그 칸
        // (Console.Events.Period.From/To)을 찾지 못했다: 두 글이 템플릿 안 TextBlock 이라 기본 보기에서 빠졌다.
        var controlView = OnSta(() =>
        {
            var field = new DateTimeRangeField
            {
                Style = KernelStyle(typeof(DateTimeRangeField)),
                FromAutomationId = "Test.Range.From",
                ToAutomationId = "Test.Range.To",
            };
            Arrange(field, 360, 40);

            // 두 글을 시각 트리에서 AutomationId 로 찾아 그 peer 가 기본 보기에 드는지 묻는다(범위 칸 자신은 peer 가 없다).
            var found = new Dictionary<string, bool>();
            void Walk(DependencyObject node)
            {
                if (node is UIElement element && AutomationProperties.GetAutomationId(element) is "Test.Range.From" or "Test.Range.To")
                    found[AutomationProperties.GetAutomationId(element)] = UIElementAutomationPeer.CreatePeerForElement(element)?.IsControlElement() == true;
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
            }
            Walk(field);
            return found;
        });

        Assert.True(controlView.TryGetValue("Test.Range.From", out var from) && from, "시작 글이 기본 보기에 없다");
        Assert.True(controlView.TryGetValue("Test.Range.To", out var to) && to, "끝 글이 기본 보기에 없다");
    }

    private static bool TemplatedTextIsControl(FrameworkElementFactory text)
    {
        text.Name = "T";
        text.SetValue(TextBlock.TextProperty, "설정을 저장했습니다.");
        var control = new ContentControl { Template = new ControlTemplate(typeof(ContentControl)) { VisualTree = text } };
        Arrange(control, 200, 40);
        var element = (UIElement)control.Template.FindName("T", control);
        return UIElementAutomationPeer.CreatePeerForElement(element)!.IsControlElement();
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.ApplyTemplate();
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static readonly object EnsureApplicationGate = new();

    private static Style KernelStyle(Type type)
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        return (Style)((ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative)))[type];
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
