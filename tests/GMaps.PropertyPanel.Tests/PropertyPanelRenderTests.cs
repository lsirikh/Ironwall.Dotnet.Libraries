using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 속성창 오프스크린 렌더 — 사용자 지시(2026-09-08): 철망 간격·센서 부착·높이 절은 **마커 속성 맨 밑**(단계별 순서: BASIC → COLOR → … → 특화 절 → 3D 철망). 스크롤 끝에서 온전히 보이고 라이트/다크 모두 읽혀야 한다.
/// SYMBOL3D_ARTIFACTS 가 있으면 PNG 도 남긴다(육안 검토).
/// </summary>
public class PropertyPanelRenderTests
{
    // 토큰은 패널 로컬(요소 수준)에 둔다 — Application 수준 사전 교체는 Window 트리만 무효화하므로(오프스크린 요소엔 미전파) 다크 교체 렌더가 안 먹는다(실측: 라이트/다크 PNG 해시 동일).
    private static ResourceDictionary Tokens(string name) => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };
    private static ResourceDictionary Theme() => new()
    {
        MergedDictionaries =
        {
            Tokens("Shared"), Tokens("Light"),
            new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupPropertyStyle.xaml", UriKind.Relative) },
        }
    };

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is FrameworkElement fe) yield return fe;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    [Fact]
    public void should_place_fence_section_last_and_fully_visible_at_scroll_end() => AppHost.Run(() =>
    {
        var model = new PidsGroupSymbolModel { Title = "구역-검증", Latitude = 37.5, Longitude = 127.0, PostSpacingM = null, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0), new(37.501, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        var panel = new GMapPropertyPidsGroupControl();
        panel.Resources.MergedDictionaries.Add(Theme());
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyPidsGroupControl)];
        panel.Is3DFeatureEnabled = true;   // 플래그 OFF 앱에서는 절 자체가 숨겨진다
        panel.SelectedMarker = marker;
        AppHost.Layout(panel, 300, 520);

        var byId = new Dictionary<string, FrameworkElement>();
        foreach (var e in Descendants(panel)) { var id = AutomationProperties.GetAutomationId(e); if (!string.IsNullOrEmpty(id)) byId.TryAdd(id, e); }
        foreach (var id in new[] { "GMaps.Property.FenceSection", "GMaps.Property.Render3D", "GMaps.Property.FenceMode", "GMaps.Property.PostSpacing", "GMaps.Property.PostSpacingPreset3", "GMaps.Property.FenceHeight", "GMaps.Property.FenceSummary" })
        {
            Assert.True(byId.ContainsKey(id), $"{id} 가 시각 트리에 없다");
            Assert.Equal(Visibility.Visible, byId[id].Visibility);
            Assert.True(byId[id].ActualWidth > 0 && byId[id].ActualHeight > 0, $"{id} 크기 0");
        }
        var section = byId["GMaps.Property.FenceSection"];
        // ① 맨 밑: 특화 StackPanel 의 마지막 자식이고, 장비 그룹 절·BASIC 라벨보다 아래
        var host = (StackPanel)VisualTreeHelper.GetParent(section);
        Assert.Same(section, host.Children[host.Children.Count - 1]);
        double Y(FrameworkElement e) => e.TransformToAncestor(panel).Transform(new Point(0, 0)).Y;
        var basic = Descendants(panel).OfType<TextBlock>().First(t => t.Text == "BASIC");
        var deviceGroup = Descendants(panel).OfType<TextBlock>().First(t => t.Text == "장비 그룹");
        Assert.True(Y(section) > Y(deviceGroup) && Y(section) > Y(basic), $"3D 철망 절({Y(section):F0})이 장비 그룹({Y(deviceGroup):F0})/BASIC({Y(basic):F0}) 위에 있다");
        // ② 스크롤 끝에서 온전히 보인다(패널 MaxHeight 520 뷰포트)
        var scroller = Descendants(panel).OfType<ScrollViewer>().First(v => v.IsAncestorOf(section));
        scroller.ScrollToEnd(); panel.UpdateLayout();
        double top = Y(section), bottom = top + section.ActualHeight;
        Assert.True(top >= 0 && bottom <= 520 + 0.5, $"스크롤 끝에서 3D 철망 절이 잘린다: top={top:F0} bottom={bottom:F0}");
        Assert.True(scroller.ScrollableHeight > 0, "패널이 스크롤되지 않는다(맨 밑 계약을 검증할 수 없음)");
        // ③ 값: 전역 기본 3.0 · 상속 표시 · 요약
        Assert.Equal(3.0, panel.PostSpacingM); Assert.True(panel.IsPostSpacingInherited);
        Assert.Contains("기둥", panel.FenceLayoutSummary);

        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output == null) return;
        AppHost.SaveImage(panel, 300, 520, Path.Combine(output, "pidsgroup-property-panel.png"));
        // 다크 토큰으로 교체 렌더(DynamicResource 재해석) — 라이트/다크 둘 다 육안 검토 대상(drag-first-ux 규칙: 색이 아니라 형태로 구분)
        var theme = panel.Resources.MergedDictionaries[0];
        var light = theme.MergedDictionaries[1];
        theme.MergedDictionaries[1] = Tokens("Dark");
        try { panel.UpdateLayout(); AppHost.SaveImage(panel, 300, 520, Path.Combine(output, "pidsgroup-property-panel-dark.png")); }
        finally { theme.MergedDictionaries[1] = light; }
    });

    [Fact]
    public void should_hide_fence_block_when_symbol3d_disabled_or_group_editing() => AppHost.Run(() =>
    {
        var model = new PidsGroupSymbolModel { Latitude = 37.5, Longitude = 127.0 };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        var panel = new GMapPropertyPidsGroupControl();
        panel.Resources.MergedDictionaries.Add(Theme());
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyPidsGroupControl)];
        panel.Is3DFeatureEnabled = false;
        panel.SelectedMarker = marker;
        AppHost.Layout(panel, 300, 520);
        var section = Descendants(panel).First(e => AutomationProperties.GetAutomationId(e) == "GMaps.Property.FenceSection");
        Assert.Equal(Visibility.Collapsed, section.Visibility);
        panel.Is3DFeatureEnabled = true; panel.IsGroupEditing = true; AppHost.Layout(panel, 300, 520);
        Assert.Equal(Visibility.Collapsed, section.Visibility);
    });
}
