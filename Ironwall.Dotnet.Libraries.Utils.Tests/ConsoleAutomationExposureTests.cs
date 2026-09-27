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
