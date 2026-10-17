using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 완성도 수정 패스 — 운영자 화면에서 개발자용 글자를 걷고(API 필드명 · 절 축 이름), 창 제목을 한 번만 찍는다(X1).
/// 실제 커널 템플릿(Utils/Themes/Generic.xaml)을 물려 헤드리스로 확인한다(<see cref="ConsoleShellTests"/> 와 같은 방식).
/// </summary>
[Collection(WpfApplicationCollection.Name)]
public class ConsoleOperatorChromeTests
{
    private static readonly object EnsureApplicationGate = new();

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

    private static ResourceDictionary Kernel()
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        return (ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative));
    }

    private static void Layout(FrameworkElement root, double width = 900, double height = 700)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static Visibility ApiCaption(bool turnOn)
    {
        var kernel = Kernel();
        var field = new ConsoleField { Style = (Style)kernel[typeof(ConsoleField)], Header = "권한", ApiName = "users.role", Content = new TextBlock { Text = "관리자" } };
        var root = new StackPanel();
        if (turnOn) ConsoleField.SetShowApiNames(root, true);
        root.Children.Add(field);
        Layout(root);

        return ((FrameworkElement)field.Template.FindName("Api", field)).Visibility;
    }

    private static Visibility AxisCaption(bool turnOn)
    {
        var kernel = Kernel();
        var section = new ConsoleSection { Style = (Style)kernel[typeof(ConsoleSection)], Header = "연결", AxisName = "connection" };
        var root = new StackPanel();
        if (turnOn) ConsoleField.SetShowApiNames(root, true);
        root.Children.Add(section);
        Layout(root);

        return ((FrameworkElement)section.Template.FindName("Axis", section)).Visibility;
    }

    [Fact]
    public void should_hide_the_api_field_caption_when_nobody_turns_developer_captions_on()
        => Assert.Equal(Visibility.Collapsed, OnSta(() => ApiCaption(turnOn: false)));

    [Fact]
    public void should_show_the_api_field_caption_when_a_preview_tool_turns_captions_on_at_the_root()
        => Assert.Equal(Visibility.Visible, OnSta(() => ApiCaption(turnOn: true)));

    [Fact]
    public void should_hide_the_section_axis_name_when_nobody_turns_developer_captions_on()
        => Assert.Equal(Visibility.Collapsed, OnSta(() => AxisCaption(turnOn: false)));

    [Fact]
    public void should_show_the_section_axis_name_when_a_preview_tool_turns_captions_on_at_the_root()
        => Assert.Equal(Visibility.Visible, OnSta(() => AxisCaption(turnOn: true)));

    [Fact]
    public void should_default_to_hidden_and_flow_down_when_the_caption_switch_is_declared()
    {
        var metadata = (FrameworkPropertyMetadata)ConsoleField.ShowApiNamesProperty.GetMetadata(typeof(DependencyObject));

        Assert.False((bool)metadata.DefaultValue!);   // 운영자 화면(호스트 포함)이 기본
        Assert.True(metadata.Inherits);                // 창 뿌리 한 곳에서 켠다
    }

    [Fact]
    public void should_keep_the_caption_hidden_when_the_field_has_no_api_name_even_if_captions_are_on()
    {
        var visibility = OnSta(() =>
        {
            var kernel = Kernel();
            var field = new ConsoleField { Style = (Style)kernel[typeof(ConsoleField)], Header = "이름" };
            var root = new StackPanel();
            ConsoleField.SetShowApiNames(root, true);
            root.Children.Add(field);
            Layout(root);
            return ((FrameworkElement)field.Template.FindName("Api", field)).Visibility;
        });

        Assert.Equal(Visibility.Collapsed, visibility);
    }

    // ───────── X1 — 표면 틀의 손잡이가 콘솔 머리에 앉는다 ─────────

    private static TextBlock TitleBlock(DependencyObject root, string title)
    {
        TextBlock? found = null;
        void Walk(DependencyObject node)
        {
            if (found is not null) return;
            if (node is TextBlock tb && tb.Text == title) { found = tb; return; }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }
        Walk(root);
        return found ?? throw new InvalidOperationException($"'{title}' 제목을 못 찾았다");
    }

    private static double TitleLeft(double gripWidth)
    {
        var kernel = Kernel();
        var shell = new ConsoleShell { Style = (Style)kernel[typeof(ConsoleShell)], Title = "계정 · 권한" };
        var host = new Border { Child = shell };
        if (gripWidth > 0) SurfaceFrame.SetHeadGripWidth(host, gripWidth);
        Layout(host, 1280, 760);

        return TitleBlock(shell, "계정 · 권한").TranslatePoint(new Point(0, 0), shell).X;
    }

    [Fact]
    public void should_move_the_console_title_right_by_the_grip_width_when_the_surface_frame_merges_its_head()
    {
        var (plain, framed) = OnSta(() => (TitleLeft(0), TitleLeft(24)));

        Assert.Equal(16, plain, 1);          // 틀 밖(미리보기 · 별도 창)은 예전 그대로
        Assert.Equal(16 + 24, framed, 1);    // 틀 안에서는 손잡이 폭만큼 비킨다 — 손잡이가 제목을 덮지 않는다
    }

    [Fact]
    public void should_not_move_a_nested_console_title_when_the_outer_console_already_made_room()
    {
        var inner = OnSta(() =>
        {
            var kernel = Kernel();
            var nested = new ConsoleShell { Style = (Style)kernel[typeof(ConsoleShell)], Title = "안쪽" };
            var outer = new ConsoleShell { Style = (Style)kernel[typeof(ConsoleShell)], Title = "바깥", List = nested };
            var host = new Border { Child = outer };
            SurfaceFrame.SetHeadGripWidth(host, 24);
            Layout(host, 1400, 760);
            return SurfaceFrame.GetHeadGripWidth(nested);
        });

        Assert.Equal(0d, inner);
    }

    [Fact]
    public void should_default_the_grip_width_to_zero_and_inherit_when_declared()
    {
        var metadata = (FrameworkPropertyMetadata)SurfaceFrame.HeadGripWidthProperty.GetMetadata(typeof(DependencyObject));

        Assert.Equal(0d, (double)metadata.DefaultValue!);
        Assert.True(metadata.Inherits);
    }
}

/// <summary>
/// 한 AppDomain 에 <see cref="Application"/> 은 하나뿐이다 — 그것을 만드는 시험 반들을 한 모음에 넣어 차례로 돌린다
/// (병렬로 돌면 둘이 동시에 null 을 보고 둘 다 만들다 한쪽이 터진다 — 실측).
/// </summary>
[CollectionDefinition(Name)]
public sealed class WpfApplicationCollection
{
    public const string Name = "WpfApplication";
}
