using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Accounts.Ui.ViewTests;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 블록 — 배치 미리보기 표지('↘ 다음 창 +24 px')가 칸보다 길어 잘리던 결함 (호스트 보고 R2 · SET-058)
   Created By   : Claude (t-20261011-111636-d46d)
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 미리보기 안 한 줄 글(줄바꿈 · 말줄임 없음)이 제 칸보다 길지 않은지 — 창을 띄우지 않고(부모 없는 렌더) 잰다.
/// </summary>
/// <remarks>
/// <para><b>왜 문화권을 둘 다 재나</b>: '↘' 처럼 기본 글꼴(MaterialDesignFont 등 라틴 글꼴)에 없는 글리프는 대체 글꼴을 언어(xml:lang)에 따라 고른다 —
/// 실측(10.5pt, Roboto) en-US 7.70 · ko-KR 9.98 DIU. TextBlock 은 제 Language(기본 en-US)로 칸을 재고, 호스트 감사(FormattedText)는
/// CurrentUICulture(ko-KR)로 재서 78 칸에 80.6 이 필요하다고 나왔다. 앱은 설정에 따라 Language 를 바꾸므로 어느 쪽이든 잘리지 않아야 한다.</para>
/// <para>배율 125 · 150 % 는 뿌리 DPI 를 바꿔 레이아웃 반올림까지 같은 조건으로 잰다(허용 0.5 DIU = 반올림 한 칸 미만).</para>
/// </remarks>
public class CameraPopupCascadeCaptionViewTests
{
    private const string PreviewId = "Settings.CameraPopup.Event.Preview";
    private const double Tolerance = 0.5;
    private static readonly string[] Cultures = { "en-US", "ko-KR" };

    [Theory]
    [InlineData(false, 1.0)]
    [InlineData(false, 1.25)]
    [InlineData(false, 1.5)]
    [InlineData(true, 1.0)]
    [InlineData(true, 1.25)]
    [InlineData(true, 1.5)]
    [Trait("Scenario", "SET-058")]
    public void should_fit_every_single_line_preview_text_in_its_box_in_both_themes_and_scales(bool dark, double scale) => AppHost.Run(() =>
    {
        var problems = new List<string>();
        try
        {
            AppHost.SetDark(dark);
            foreach (var language in Cultures)
            foreach (var step in new[] { 24, 200, 0 })
                problems.AddRange(Audit(language, step, scale));
        }
        finally
        {
            AppHost.SetDark(false);
        }

        Assert.True(problems.Count == 0, $"{(dark ? "다크" : "라이트")} {scale:P0}: " + string.Join(" / ", problems));
    });

    [Theory]
    [InlineData(24, true, "다음 창 +24 px")]
    [InlineData(0, false, "간격 0 — 모든 창이 한 자리")]
    [Trait("Scenario", "SET-058")]
    public void should_draw_the_cascade_arrow_as_an_icon_and_keep_the_full_caption_for_automation(int step, bool arrow, string text) => AppHost.Run(() =>
    {
        var (view, host) = Render(NewViewModel(step), "en-US", 1.0);

        var caption = Descendants<StackPanel>(view).Single(p => AutomationProperties.GetAutomationId(p) == "Settings.CameraPopup.Event.CascadeCaption");
        var icon = Descendants<MaterialDesignThemes.Wpf.PackIcon>(caption).Single();
        var label = Descendants<TextBlock>(caption).Single(t => !string.IsNullOrEmpty(t.Text));

        Assert.Equal(MaterialDesignThemes.Wpf.PackIconKind.ArrowBottomRight, icon.Kind);
        Assert.Equal(arrow, Shown(icon, host));
        Assert.Equal(text, label.Text);                                                         // 글자에는 화살표 글리프가 없다
        Assert.Equal(((CameraPopupSettingsViewModel)view.DataContext).CascadeCaption, AutomationProperties.GetName(caption));   // 읽기 이름은 화살표까지 온전히
    });

    private static (CameraPopupSettingsView View, Border Host) Render(CameraPopupSettingsViewModel vm, string language, double scale)
    {
        var view = new CameraPopupSettingsView { DataContext = vm };
        // 호스트 셸과 같은 조건 — 셸 창이 글꼴을 MaterialDesignFont 로 깔고, 앱이 Language 를 설정 문화권으로 바꾼다
        var host = new Border { Child = view, UseLayoutRounding = true, Language = XmlLanguage.GetLanguage(language) };
        host.SetResourceReference(TextElement.FontFamilyProperty, "MaterialDesignFont");
        VisualTreeHelper.SetRootDpi(host, new DpiScale(scale, scale));
        Layout(host);
        AppHost.Pump();
        Layout(host);
        return (view, host);
    }

    private static IEnumerable<string> Audit(string language, int step, double scale)
    {
        var (view, host) = Render(NewViewModel(step), language, scale);
        var preview = Descendants<Border>(view).Single(b => AutomationProperties.GetAutomationId(b) == PreviewId);
        var texts = Descendants<TextBlock>(preview)
            .Where(t => Shown(t, host) && !string.IsNullOrWhiteSpace(t.Text)
                        && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None)
            .ToList();
        if (texts.Count == 0) yield return $"[{language} · 간격 {step}] 미리보기에 잴 글이 없다";

        foreach (var text in texts)
        {
            var need = Cultures.Append(CultureInfo.CurrentUICulture.Name).Max(c => Need(text, c, scale));
            if (need > text.ActualWidth + Tolerance)
                yield return $"[{language} · 간격 {step}] '{text.Text}' 필요 {need:0.##} > 칸 {text.ActualWidth:0.##}";
        }
    }

    private static double Need(TextBlock text, string culture, double scale)
    {
        var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        var formatted = new FormattedText(text.Text, new CultureInfo(culture), text.FlowDirection, typeface, text.FontSize, Brushes.Black, scale);
        return formatted.WidthIncludingTrailingWhitespace + text.Padding.Left + text.Padding.Right;
    }

    /// <summary>부모 없는 렌더에는 PresentationSource 가 없어 IsVisible 이 늘 false — 뿌리까지 Visibility 를 본다.</summary>
    private static bool Shown(DependencyObject element, DependencyObject root)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement e && e.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(current, root)) return true;
        }
        return true;
    }

    private static void Layout(FrameworkElement root)
    {
        root.Measure(new Size(1200, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
    }

    private static CameraPopupSettingsViewModel NewViewModel(int step)
    {
        var port = new Mock<ICameraPopupSettingsPort>();
        port.Setup(p => p.LoadCameraPopup()).Returns(new CameraPopupSettings { CascadeStepPx = step }.Normalize());
        port.SetupGet(p => p.CameraPopupClientId).Returns("gis-test");
        var monitors = new Mock<IDisplayMonitorProvider>();
        monitors.Setup(m => m.GetMonitors()).Returns(new List<DisplayMonitorInfo>
        {
            new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
        });
        return new CameraPopupSettingsViewModel(port.Object, monitors.Object);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }
}
