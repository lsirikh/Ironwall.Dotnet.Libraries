using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Help;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Tests;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 지도 화면 "?"(help-callout H-4) — 지도 XAML 이 거는 키가 런타임 등록(모듈 초기화)으로 실제 풀리고,
/// 화면에서 뺀 설명이 말풍선 본문에 남아 있으며(NFR-03 — 설명이 사라지지 않는다), 속성 패널에 "?" 가 실제로 뜨는지 본다.
/// </summary>
/// <remarks>글 훑기(<see cref="HelpSourceScan"/>)는 Utils.Tests 의 것을 연결해 쓴다(사본을 만들지 않는다).</remarks>
public class MapsHelpTests
{
    private const string GMapsProject = "Ironwall.Dotnet.Libraries.GMaps.Ui";

    private static string ThisFile([CallerFilePath] string? path = null) => path!;

    [Fact]
    public void should_resolve_every_map_screen_help_key_when_the_gmaps_assembly_has_loaded()
    {
        // Arrange — 지도 어셈블리가 쓰이기 시작했다(뷰가 뜨기 직전과 같은 조건)
        RuntimeHelpers.RunModuleConstructor(typeof(MapsHelp).Module.ModuleHandle);
        var keys = HelpSourceScan.XamlKeys(HelpSourceScan.RepoRoot(ThisFile()))
            .Where(k => k.Project == GMapsProject).Select(k => k.Key).Distinct().ToList();

        // Act
        var missing = keys.Where(k => !HelpCatalog.TryGet(k, out _)).ToList();

        // Assert
        Assert.NotEmpty(keys);
        Assert.True(missing.Count == 0, $"등록되지 않은 키: {string.Join(", ", missing)}");
        Assert.All(keys, k => Assert.StartsWith("Map.", k, StringComparison.Ordinal));
    }

    [Theory]
    // 지도 위 띠 · 툴팁에서 뺀 단축키 · 동작 원리 · 용어가 말풍선 본문에 남아 있다(NFR-03 · PRD §2 "옮긴다")
    [InlineData("Map.Measure.Tool", "Backspace")]
    [InlineData("Map.Measure.Tool", "Esc")]
    [InlineData("Map.Measure.Tool", "3개 이상")]
    [InlineData("Map.Toolbar.RotateTilt", "Shift+휠")]
    [InlineData("Map.Toolbar.RotateTilt", "Ctrl+Shift+↑/↓ = 1°")]
    [InlineData("Map.Toolbar.RotateTilt", "설정한 줌 이상에서만")]
    [InlineData("Map.Toolbar.RotateTilt", "MGRS")]
    [InlineData("Map.Toolbar.EditMode", "등록 · 심볼 · 선택 · 기준")]
    [InlineData("Map.EditStrip.Selection", "Shift+끌기")]
    [InlineData("Map.EditStrip.Selection", "Ctrl+V")]
    [InlineData("Map.Zoom.Steps", "소프트 확대")]
    [InlineData("Map.SymbolPalette.Placement", "Enter")]
    [InlineData("Map.SymbolPalette.Symbol3D", "다시 시작해야")]
    [InlineData("Map.LayerPanel.Layers", "묶음 전체")]
    [InlineData("Map.LayerPanel.Layers", "Alt+↑ · Alt+↓")]
    [InlineData("Map.Property.Label", "말줄임표")]
    [InlineData("Map.Property.Housing", "지금 실행 중에만")]
    [InlineData("Map.Property.Housing", "장비 연결은 그대로")]
    [InlineData("Map.Property.Door", "장비가 응답한 뒤")]
    [InlineData("Map.Property.Device", "서버에 저장")]
    [InlineData("Map.Property.Fence", "센서 장착")]
    [InlineData("Map.SymbolDetail.Window", "45°")]
    [InlineData("Map.SymbolDetail.Window", "부대 관계도")]
    [InlineData("Map.Anchor.Window", "정북")]
    [InlineData("Map.Anchor.Window", "완전히 막습니다")]
    [InlineData("Map.Tracking.Settings", "ttl_sec")]
    [InlineData("Map.Tracking.Settings", "[저장]")]
    [InlineData("Map.Playback.Window", "31일")]
    [InlineData("Map.Playback.Window", "[불러오기]")]
    public void should_keep_the_moved_explanation_in_the_callout_body_when_it_left_the_screen(string key, string phrase)
    {
        RuntimeHelpers.RunModuleConstructor(typeof(MapsHelp).Module.ModuleHandle);

        Assert.True(HelpCatalog.TryGet(key, out var entry), $"{key} 미등록");
        Assert.Contains(phrase, entry.ToPlainText(), StringComparison.Ordinal);
    }

    [Fact]
    public void should_keep_only_the_first_hud_line_and_drop_shortcut_lists_from_map_status_text()
    {
        // D-1 — 측정 띠는 지금 할 일 한 줄만(끝내기 · 지우기 · 취소 단축키는 "?")
        var hint = Ironwall.Dotnet.Libraries.GMaps.Ui.Services.MeasureController.InProgressHint;
        Assert.DoesNotContain("ESC", hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Backspace", hint, StringComparison.Ordinal);
        Assert.Contains("클릭", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void should_show_enabled_question_marks_and_drop_long_tooltips_in_the_pids_group_property_panel() => AppHost.Run(() =>
    {
        // Arrange — 철망 절이 보이는 상태(3D 기능 ON · 단일 편집)
        var model = new PidsGroupSymbolModel { Title = "구역-도움말", Latitude = 37.5, Longitude = 127.0, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        var panel = new GMapPropertyPidsGroupControl();
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupPropertyStyle.xaml", UriKind.Relative) },
            }
        });
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyPidsGroupControl)];
        panel.Is3DFeatureEnabled = true;
        panel.SelectedMarker = marker;

        // Act
        AppHost.Layout(panel, 300, 900);
        var all = Descendants(panel).ToList();
        var tips = all.OfType<HelpTip>().ToDictionary(t => t.HelpKey);

        // Assert — 라벨 절 · 철망 절의 "?" 가 Help.{키} 로 뜨고 눌린다(등록이 뷰보다 먼저 끝났다)
        foreach (var key in new[] { "Map.Property.Label", "Map.Property.Fence" })
        {
            Assert.True(tips.ContainsKey(key), $"{key} \"?\" 가 없다");
            Assert.Equal($"Help.{key}", AutomationProperties.GetAutomationId(tips[key]));
            Assert.True(tips[key].IsEnabled, $"{key} \"?\" 가 꺼져 있다(설명 목록에 없음)");
            Assert.True(tips[key].Visibility == Visibility.Visible && tips[key].ActualWidth > 0, $"{key} \"?\" 가 보이지 않는다");
        }

        // 툴팁엔 이름 · 범위만 — 저장 시점(150 ms) · 형태 정의 · 말줄임 설명은 "?" 로 옮겼다(FR-08)
        var toolTips = all.Select(e => e.ToolTip as string).Where(t => t is not null).ToList();
        Assert.DoesNotContain(toolTips, t => t!.Contains("150 ms", StringComparison.Ordinal));
        Assert.DoesNotContain(toolTips, t => t!.Contains("정확 간격", StringComparison.Ordinal));
        Assert.DoesNotContain(toolTips, t => t!.Contains("말줄임", StringComparison.Ordinal));
        Assert.Contains(toolTips, t => t!.Contains("1~10 m", StringComparison.Ordinal));   // 입력 범위는 남는다
    });

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
}
