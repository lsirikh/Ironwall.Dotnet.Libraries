using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// U-12 — "버튼이나 필터쪽에 Select 할때마다 버튼 크기들이 바뀌는 케이스" 재발 방지 계약.
/// 커널 칩(Console.Chip/Console.Tab/Console.Chip.ListBoxItem/Console.RadioButton)을 <b>실제로 그려서</b>
/// IsChecked/IsSelected 가 켜진 채로 만든 것과 꺼진 채로 만든 것의 <see cref="UIElement.DesiredSize"/> 가
/// 완전히 같은지를 잰다. 문서·정적 파싱(ConsoleStyleContractTests)만으론 "폭이 실제로 흔들리는가"를
/// 확인할 수 없다 — FontWeight 를 얹고 고스트로 예약하지 않는 회귀는 이 테스트가 아니면 안 잡힌다.
/// </summary>
/// <remarks>
/// WPF 요소는 STA 스레드에서만 만들 수 있어 테스트마다 전용 STA 스레드를 띄운다(Utils.Tests
/// DialogFrameMeasureTests 와 같은 패턴). Application/pack URI 없이 <see cref="XamlReader"/> 로
/// Styles.Console.xaml 원문을 직접 파싱한다 — 색 토큰(DynamicResource)은 못 찾아도 레이아웃과는
/// 무관하다(Measure 는 Padding·BorderThickness·FontWeight·Text 길이만 본다).
/// <para>
/// ⚠ IsChecked/IsSelected 는 <b>생성 시점에 같이 준다</b>(초기화 후 값만 바꾸지 않는다) — 값을 바꾼
/// 뒤 <c>InvalidateMeasure()+Measure()</c> 를 불러도, Dispatcher 레이아웃 펌프 없이는 트리거가 건드린
/// "깊이 있는" 템플릿 이름 부품(Label 등)의 무효화가 조상(Border·Grid·컨트롤 자신)까지 안 올라간다
/// (실측 2026-09-23: Label.FontWeight 는 Normal→SemiBold 로 실제로 바뀌는데 ToggleButton.DesiredSize 는
/// 그대로였다 — LayoutManager 펌프가 없어 위임 전파가 멈춘 탓). 매번 새 인스턴스를 생성 시점 상태로
/// 만들면 이 문제를 원천적으로 피한다.
/// </para>
/// </remarks>
public class ConsoleChipSizeContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static ResourceDictionary LoadConsoleStyles()
    {
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml");
        var xaml = File.ReadAllText(path);

        // Console.DateTimePicker(mah:DateTimePicker) 는 이 테스트에 필요 없고, XamlReader.Parse 가
        // 로컬 clr-namespace 로 선언된 MahApps 타입을 pack URI 없이는 못 찾는다. 파일의 마지막
        // Style 이라 그 시작부터 통째로 잘라내고 사전을 바로 닫는다(중첩 Style.Resources 가 있어
        // 첫 "</Style>" 로는 짝을 못 맞춘다).
        var start = xaml.IndexOf("<Style x:Key=\"Console.DateTimePicker\"", StringComparison.Ordinal);
        if (start >= 0)
            xaml = xaml[..start] + "</ResourceDictionary>";

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

    private static Size MeasureToggle(string styleKey, string content, bool isChecked)
    {
        var dict = LoadConsoleStyles();
        var style = (Style)dict[styleKey];
        var tb = new ToggleButton { Style = style, Content = content, IsChecked = isChecked };
        tb.ApplyTemplate();
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return tb.DesiredSize;
    }

    [Theory]
    [InlineData("Console.Chip", "오늘")]
    [InlineData("Console.Chip", "직접")]
    [InlineData("Console.Chip", "Custom")]
    [InlineData("Console.Tab", "탐지")]
    public void should_keep_the_same_desired_size_when_a_chip_is_checked(string styleKey, string content)
    {
        var unchecked_ = OnSta(() => MeasureToggle(styleKey, content, isChecked: false));
        var checked_ = OnSta(() => MeasureToggle(styleKey, content, isChecked: true));

        Assert.Equal(unchecked_.Width, checked_.Width, 3);
        Assert.Equal(unchecked_.Height, checked_.Height, 3);
    }

    [Fact]
    public void should_keep_the_same_desired_size_when_a_listboxitem_chip_is_selected()
    {
        Size Measure(bool selected) => OnSta(() =>
        {
            var dict = LoadConsoleStyles();
            var style = (Style)dict["Console.Chip.ListBoxItem"];
            var item = new ListBoxItem { Style = style, Content = "카메라", IsSelected = selected };
            item.ApplyTemplate();
            item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return item.DesiredSize;
        });

        var before = Measure(false);
        var after = Measure(true);

        Assert.Equal(before.Width, after.Width, 3);
        Assert.Equal(before.Height, after.Height, 3);
    }

    [Fact]
    public void should_keep_the_same_desired_size_when_a_radiobutton_is_checked()
    {
        Size Measure(bool isChecked) => OnSta(() =>
        {
            var dict = LoadConsoleStyles();
            var style = (Style)dict["Console.RadioButton"];
            var rb = new RadioButton { Style = style, Content = "장비", GroupName = "T", IsChecked = isChecked };
            rb.ApplyTemplate();
            rb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return rb.DesiredSize;
        });

        var before = Measure(false);
        var after = Measure(true);

        Assert.Equal(before.Width, after.Width, 3);
        Assert.Equal(before.Height, after.Height, 3);
    }

    /// <summary>
    /// 회귀 가드 — 예약 없이 IsChecked 에서 FontWeight 만 얹으면(예전 로컬 ConsoleChip 이 그랬듯)
    /// <see cref="should_keep_the_same_desired_size_when_a_chip_is_checked"/> 가 실패해야 한다는 것
    /// 자체가 계약이다. <b>2026-09-23 실증</b>: Console.Chip 템플릿에서 "Ghost" TextBlock 을 지우고
    /// 위 이론 테스트를 돌리면 Console.Chip/"오늘"·"직접"·"Custom" 세 케이스 모두 Width 불일치로
    /// 실패한다. Ghost 를 되돌리면 다시 통과한다.
    /// </summary>
    [Fact]
    public void should_reserve_bold_width_via_a_hidden_ghost_label_in_the_chip_template()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        var chipBlock = xaml[xaml.IndexOf("x:Key=\"Console.Chip\"", StringComparison.Ordinal)..xaml.IndexOf("x:Key=\"Console.Chip.ListBoxItem\"", StringComparison.Ordinal)];

        Assert.Contains("x:Name=\"Ghost\"", chipBlock);
        Assert.Contains("FontWeight=\"SemiBold\"", chipBlock);
        // 선택 테두리는 실제 BorderThickness 를 바꾸지 않는다 — 레이아웃 밖 오버레이(SelectedRing)로만 그린다.
        Assert.DoesNotContain("Property=\"BorderThickness\"", chipBlock);
    }
}
