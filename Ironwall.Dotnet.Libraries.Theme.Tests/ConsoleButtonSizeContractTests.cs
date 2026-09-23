using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// U-14 — "레이아웃 버튼 크기 일관성 · 정돈된 규격" 계약. 커널 버튼 가족(Console.Button · .Primary · .Ghost ·
/// .Danger · .Icon · .Icon.OnPrimary)을 <b>실제로 그려서</b> 잰다 — 정적 파싱(ConsoleStyleContractTests)만으론
/// "높이가 한 가족인가" · "켜짐 ↔ 꺼짐에서 폭이 흔들리는가" 를 확인할 수 없다.
/// </summary>
/// <remarks>
/// 측정 방식은 <see cref="ConsoleChipSizeContractTests"/> 와 같다(전용 STA 스레드 · XamlReader 로 원문 파싱 ·
/// 상태는 생성 시점에 준다). 색 토큰(DynamicResource)은 못 찾아도 치수와는 무관하다.
/// </remarks>
public class ConsoleButtonSizeContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static ResourceDictionary LoadConsoleStyles()
    {
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml");
        var xaml = File.ReadAllText(path);
        // mah:DateTimePicker 는 pack URI 없이 못 푼다 — 파일의 마지막 Style 이라 그 앞에서 사전을 닫는다(칩 테스트와 같다).
        var start = xaml.IndexOf("<Style x:Key=\"Console.DateTimePicker\"", StringComparison.Ordinal);
        if (start >= 0) xaml = xaml[..start] + "</ResourceDictionary>";
        return (ResourceDictionary)XamlReader.Parse(xaml);
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

    private static Size Measure(string styleKey, object content, bool isEnabled = true, double? width = null, double? height = null)
        => OnSta(() =>
        {
            var button = new Button { Style = (Style)LoadConsoleStyles()[styleKey], Content = content, IsEnabled = isEnabled };
            if (width is { } w) button.Width = w;
            if (height is { } h) button.Height = h;
            button.ApplyTemplate();
            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return button.DesiredSize;
        });

    [Theory]
    [InlineData("Console.Button")]
    [InlineData("Console.Button.Primary")]
    [InlineData("Console.Button.Ghost")]
    [InlineData("Console.Button.Danger")]
    [InlineData("Console.Button.Icon")]
    [InlineData("Console.Button.Icon.OnPrimary")]
    public void should_share_the_input_height_when_style_is_a_kernel_button(string styleKey)
    {
        // Act — 같은 줄에 놓이는 버튼은 높이가 하나여야 한다(ConsoleLayoutMath.InputHeight = 32)
        var size = Measure(styleKey, "적용");

        // Assert
        Assert.Equal(32, size.Height, 3);
    }

    [Theory]
    [InlineData("Console.Button.Icon")]
    [InlineData("Console.Button.Icon.OnPrimary")]
    public void should_be_a_square_of_the_input_height_when_style_is_an_icon_button(string styleKey)
    {
        // Act
        var size = Measure(styleKey, "✕");

        // Assert
        Assert.Equal(32, size.Width, 3);
        Assert.Equal(32, size.Height, 3);
    }

    [Fact]
    public void should_honor_a_local_small_size_when_icon_button_sits_inside_a_chip()
    {
        // Act — 칩 안의 ✕ 는 자리에서 18 로 줄인다. MinWidth 를 물려받으면(Ghost 56) WPF 는 MinWidth 가 Width 를 이겨 부푼다.
        var size = Measure("Console.Button.Icon", "✕", width: 18, height: 18);

        // Assert
        Assert.Equal(18, size.Width, 3);
        Assert.Equal(18, size.Height, 3);
    }

    [Theory]
    [InlineData("Console.Button", "조치보고 (12)")]
    [InlineData("Console.Button.Primary", "조치보고 (12)")]
    [InlineData("Console.Button.Primary", "프리셋으로 저장")]
    [InlineData("Console.Button.Danger", "선택 삭제 (3)")]
    public void should_keep_the_same_desired_size_when_a_button_is_disabled(string styleKey, string content)
    {
        // Act — 행을 고를 때마다 켜지고 꺼지는 버튼이 그때마다 넓어졌다 줄었다 하면 안 된다(U-14)
        var enabled = Measure(styleKey, content, isEnabled: true);
        var disabled = Measure(styleKey, content, isEnabled: false);

        // Assert
        Assert.Equal(enabled.Width, disabled.Width, 3);
        Assert.Equal(enabled.Height, disabled.Height, 3);
    }

    [Fact]
    public void should_align_content_left_when_a_list_button_asks_for_it()
    {
        // Arrange + Act — 목록형 버튼(프리셋 목록 · 그룹 체크 행)은 HorizontalContentAlignment 로 왼쪽 정렬을 요구한다.
        // 옛 템플릿은 Center 를 못박아 이 요구를 조용히 무시했다.
        var offset = OnSta(() =>
        {
            var label = new TextBlock { Text = "함체 · 표준 6부품" };
            var button = new Button
            {
                Style = (Style)LoadConsoleStyles()["Console.Button"],
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            button.ApplyTemplate();
            button.Measure(new Size(300, 32));
            button.Arrange(new Rect(0, 0, 300, 32));
            return label.TranslatePoint(new Point(0, 0), button).X;
        });

        // Assert — Padding 왼쪽(12) 에서 시작한다. 가운데 정렬이었다면 100 을 훌쩍 넘는다.
        Assert.Equal(12, offset, 3);
    }
}
