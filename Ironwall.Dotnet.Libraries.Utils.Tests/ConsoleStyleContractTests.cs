using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 콘솔 커널 ↔ 테마 사전 사이의 계약을 글자로 지킨다(vq-theme K-01 · K-02 · K-03 · K-09 · K-10 · D-17 · D-44).
/// </summary>
/// <remarks>
/// 왜 파일을 직접 읽는가 — 커널의 기본 템플릿(<c>Utils/Themes/Generic.xaml</c>)은 <b>테마 사전</b>이라
/// 앱 스코프의 <c>StaticResource</c> 로 닿지 않는다(2026-09-21 실측: 조용히 <c>UnsetValue</c>).
/// 그래서 버튼 · 검색 상자 같은 공용 키는 Theme 쪽 <c>Styles.Console.xaml</c> 에 두고 커널은
/// <c>DynamicResource</c> 로 가져다 쓴다. 이 배선은 런타임에만 풀리므로 <b>깨져도 빌드가 통과한다</b> —
/// 여기서 문자열로 잡지 않으면 아무도 못 잡는다. Utils.Tests 는 Theme 을 참조하지 않으므로 경로로 읽는다.
/// </remarks>
public class ConsoleStyleContractTests
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static string Kernel() => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Utils", "Themes", "Generic.xaml"));

    private static string ThemeFile(string name)
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", name));

    private static ISet<string> Keys(string xaml)
        => XDocument.Parse(xaml).Descendants()
            .Select(e => e.Attribute(XamlNs + "Key")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!)
            .ToHashSet(StringComparer.Ordinal);

    private static ISet<string> DynamicRefs(string xaml, string prefix)
        => Regex.Matches(xaml, @"\{DynamicResource\s+([A-Za-z_][A-Za-z0-9_.]*)\s*\}")
            .Select(m => m.Groups[1].Value)
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    #region - K-02 promotion wiring -
    [Fact]
    public void should_define_every_console_style_the_kernel_pulls_from_app_scope()
    {
        // Arrange
        var referenced = DynamicRefs(Kernel(), "Console.");
        var defined = Keys(ThemeFile("Styles.Console.xaml"));

        // Act
        var missing = referenced.Where(k => !defined.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        // Assert — 하나라도 빠지면 그 컨트롤은 런타임에 조용히 MDIX 기본값으로 떨어진다
        Assert.True(missing.Count == 0, $"Styles.Console.xaml 에 없는 키: {string.Join(", ", missing)}");
        Assert.NotEmpty(referenced);   // 공허한 통과 방지
    }

    [Theory]
    [InlineData("Console.Button")]
    [InlineData("Console.Button.Primary")]
    [InlineData("Console.Button.Ghost")]
    [InlineData("Console.Button.Mini")]
    [InlineData("Console.Button.Icon")]
    [InlineData("Console.Button.Icon.OnPrimary")]
    [InlineData("Console.Button.Danger")]
    [InlineData("Console.SearchBox")]
    [InlineData("Console.CheckBox")]
    [InlineData("Console.RadioButton")]
    [InlineData("Console.Chip")]
    [InlineData("Console.Chip.ListBoxItem")]
    [InlineData("Console.Tab")]
    [InlineData("Console.ToggleSwitch")]
    [InlineData("Console.ScrollBar")]
    [InlineData("Console.ScrollViewer")]
    [InlineData("Console.Pill")]
    public void should_expose_the_console_key_at_application_scope(string key)
        => Assert.Contains(key, Keys(ThemeFile("Styles.Console.xaml")));

    [Fact]
    public void should_not_define_the_same_console_key_in_both_dictionaries()
    {
        // Arrange — 같은 키가 두 병합 사전에 있으면 어느 쪽이 이기는지 자리마다 달라져 조용히 어긋난다
        var kernel = Keys(Kernel());
        var theme = Keys(ThemeFile("Styles.Console.xaml"));

        // Act
        var both = kernel.Intersect(theme, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

        // Assert
        Assert.True(both.Count == 0, $"두 사전에 같이 있는 키: {string.Join(", ", both)}");
    }

    #region - K-15 no implicit MD3 button-family chrome in product XAML -
    // U-12 코디네이터 발견(D-28 preview-fidelity, 커밋 ae967783) — 미리보기 도구가 호스트 App.xaml 이
    // 병합하는 MaterialDesign3.Defaults.xaml 까지 그대로 병합하도록 고쳤더니, Style 없는 Button 계열이
    // 앱 스코프의 MD3 암시(implicit) 스타일(틸 채움)로 떨어지는 게 드러났다 — 커널 자신은 "암시 스타일 0"
    // 원칙(Styles.Console.xaml 머리말)을 지키지만, 그걸 쓰는 창들이 Style 을 안 주면 소용없다.
    // 아래 정규식은 <Button>·<ToggleButton>·<RadioButton>·<RepeatButton> "요소"만 잡는다 — WPF 속성-요소
    // 구문(<Button.Content>, <Button.Template> 등)은 태그 이름 뒤에 '.' 이 오므로 lookahead 로 제외한다.
    private static readonly Regex ButtonFamilyTag = new(
        @"<(?:\w+:)?(Button|ToggleButton|RadioButton|RepeatButton)(?=[\s/>])((?:[^<>]|\n)*?)(/?)>",
        RegexOptions.Compiled);

    // 제네릭 허용 목록 — "진짜 예외"만. 이유 없이 추가하지 않는다.
    // Generic.xaml 의 달력 이전/다음/머리 버튼은 Console.DateTimeRangeField 절 소속(D-30 소유) —
    // 이 스킬 세션은 그 구획을 건드리지 않는다(코디네이션 경계). 커밋 시점 기준 미해결로 남긴다.
    private static readonly HashSet<string> ButtonFamilyStyleAllowList = new(StringComparer.Ordinal)
    {
        "Ironwall.Dotnet.Libraries.Utils/Themes/Generic.xaml:PART_PreviousButton",
        "Ironwall.Dotnet.Libraries.Utils/Themes/Generic.xaml:PART_HeaderButton",
        "Ironwall.Dotnet.Libraries.Utils/Themes/Generic.xaml:PART_NextButton",
    };

    [Fact]
    public void should_give_every_product_button_family_element_an_explicit_style_or_template()
    {
        // Arrange
        var offenders = new List<string>();
        var root = RepoRoot();
        var projects = new[]
        {
            "Ironwall.Dotnet.Libraries.Events.Ui",
            "Ironwall.Dotnet.Libraries.Devices.Ui",
            "Ironwall.Dotnet.Libraries.Accounts.Ui",
            "Ironwall.Dotnet.Libraries.Reports.Ui",
            // U-13 — 소리 설정의 폴더 · 재생 단추 4개가 Style 없이 MD3 암시 스타일(틸 채움 48×32)로 그려졌다.
            "Ironwall.Dotnet.Libraries.Sounds.Ui",
            "Ironwall.Dotnet.Libraries.Gateway",
            "Ironwall.Dotnet.Libraries.Utils",
        };

        // Act
        foreach (var project in projects)
        {
            var dir = Path.Combine(root, project);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")) continue;

                var text = File.ReadAllText(file);
                foreach (Match m in ButtonFamilyTag.Matches(text))
                {
                    var attrs = m.Groups[2].Value;
                    // TargetType 이 있으면 Style/ControlTemplate 정의 자체(사용처가 아니다) — 건너뛴다.
                    if (attrs.Contains("TargetType", StringComparison.Ordinal)) continue;
                    // Style= 또는(자기 완결 템플릿을 로컬로 바로 거는) Template= 둘 중 하나면 명시된 것으로 친다.
                    if (attrs.Contains("Style=", StringComparison.Ordinal) || attrs.Contains("Template=", StringComparison.Ordinal)) continue;

                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var nameMatch = Regex.Match(attrs, "x:Name=\"([^\"]+)\"");
                    if (nameMatch.Success && ButtonFamilyStyleAllowList.Contains($"{relative}:{nameMatch.Groups[1].Value}")) continue;

                    var lineNo = text[..m.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{relative}:{lineNo}");
                }
            }
        }

        // Assert — 하나라도 있으면 그 요소는 런타임에 MD3 암시 스타일(틸 채움)로 떨어진다(U-12 실측 계열)
        Assert.True(offenders.Count == 0, $"Style/Template 이 없는 버튼 계열: {string.Join("; ", offenders)}");
    }
    #endregion

    #region - U-14 one button language in the console UI projects -
    // U-14 — 같은 화면에 MD3 틸 Flat/Outlined 버튼과 커널 Console.Button* 이 섞여 "버튼 언어가 둘" 이었다
    // (예: 부대 콘솔 상세의 [최상위로] 틸 글자 · [옮기기] 틸 윤곽 옆에 커널 [되돌리기]/[적용]).
    // 아래 프로젝트들의 XAML 은 MaterialDesign*Button 계열 스타일 키를 참조하지 않는다 — 커널 키만 쓴다.
    // 처음엔 장비 · 이벤트 두 곳이었고, U-13 마무리로 계정 · 보고서 · 소리 · 게이트웨이를 더했다.
    // 주석 처리된 옛 버튼도 센다 — 되살리는 순간 MD 버튼이 돌아오므로 주석 안의 키도 커널 키로 바꿔 두었다.
    // IconButtonStyle 도 금지한다: 라이브러리 Resources.xaml 은 앱에 병합되지 않아, 뷰가 그 키를 부르면
    // 런타임에는 호스트 앱의 사본(BasedOn MaterialDesignIconButton — 48 원형 · 물결)으로 풀린다.
    private static readonly Regex MaterialDesignButtonStyleRef = new(
        @"\{(?:StaticResource|DynamicResource)\s+(MaterialDesign\w*Button|IconButtonStyle)\s*\}",
        RegexOptions.Compiled);

    // "상대경로:키" → 남겨 두는 이유. 이유 없이 추가하지 않는다(지금은 비어 있다 — 전부 옮겼다).
    // 단, 라이브러리 Resources.xaml 의 IconButtonStyle '정의' 자체는 x:Key 라 이 정규식에 걸리지 않는다.
    private static readonly Dictionary<string, string> MaterialDesignButtonAllowList = new(StringComparer.Ordinal)
    {
    };

    internal static IReadOnlyList<string> FindMaterialDesignButtonRefs(string root, IEnumerable<string> projects)
    {
        var offenders = new List<string>();
        foreach (var project in projects)
        {
            var dir = Path.Combine(root, project);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

                var text = File.ReadAllText(file);
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                foreach (Match m in MaterialDesignButtonStyleRef.Matches(text))
                {
                    if (MaterialDesignButtonAllowList.ContainsKey($"{relative}:{m.Groups[1].Value}")) continue;
                    var lineNo = text[..m.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{relative}:{lineNo} {m.Groups[1].Value}");
                }
            }
        }
        return offenders;
    }

    internal static readonly string[] OneButtonLanguageProjects =
    {
        "Ironwall.Dotnet.Libraries.Devices.Ui",
        "Ironwall.Dotnet.Libraries.Events.Ui",
        "Ironwall.Dotnet.Libraries.Accounts.Ui",
        "Ironwall.Dotnet.Libraries.Reports.Ui",
        "Ironwall.Dotnet.Libraries.Sounds.Ui",
        "Ironwall.Dotnet.Libraries.Gateway",
    };

    [Fact]
    public void should_not_reference_materialdesign_button_styles_when_view_is_in_a_console_ui_project()
    {
        // Arrange
        var projects = OneButtonLanguageProjects;

        // Act
        var offenders = FindMaterialDesignButtonRefs(RepoRoot(), projects);

        // Assert — 하나라도 있으면 그 버튼은 런타임에 MD3 틸 chrome 으로 그려져 커널 버튼과 섞인다
        Assert.True(offenders.Count == 0, $"MaterialDesign 버튼 스타일 참조: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void should_justify_every_materialdesign_button_allow_list_entry()
    {
        // 허용 목록 항목은 반드시 이유를 적는다 — 빈 이유로 조용히 예외를 늘리지 못하게
        Assert.All(MaterialDesignButtonAllowList, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
    }

    [Theory]
    [InlineData("Console.Button.Icon", "Console.Button.Ghost")]
    [InlineData("Console.Button.Icon.OnPrimary", "Console.Button.Primary")]
    [InlineData("Console.Button.Danger", "Console.Button")]
    public void should_inherit_the_kernel_button_template_when_style_is_a_button_variant(string key, string basedOn)
    {
        // Arrange — 변형은 치수 · 색 토큰만 바꾸고 템플릿(두 겹 포커스 고리 · 꺼짐 트리거)은 물려받는다.
        // 자기 Template 을 가지면 K-01/K-10 계약을 따로 다시 지켜야 하고, 그러다 어긋난다.
        var block = StyleBlock(ThemeFile("Styles.Console.xaml"), key);

        // Assert
        Assert.Contains($"BasedOn=\"{{StaticResource {basedOn}}}\"", block);
        Assert.DoesNotContain("Property=\"Template\"", block);
        Assert.DoesNotContain("Opacity", block);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{6}", block);   // 색은 토큰(DynamicResource)으로만
    }

    [Fact]
    public void should_keep_the_primary_font_weight_when_primary_button_is_disabled()
    {
        // Arrange + Act — U-14: 꺼질 때 굵기를 내리면 켜짐 ↔ 꺼짐마다 버튼 폭이 흔들린다
        var disabled = DisabledTrigger(StyleBlock(ThemeFile("Styles.Console.xaml"), "Console.Button.Primary"));

        // Assert
        Assert.DoesNotContain("FontWeight", disabled);
    }

    [Fact]
    public void should_not_reintroduce_a_local_danger_button_copy_when_the_kernel_has_one()
    {
        // Arrange — U-13: 계정(Acc.Btn.Danger) · 보고서(Local.Button.Mini.Danger) 의 로컬 사본을 커널
        // Console.Button.Danger 로 옮기고 지웠다. 같은 이름이 x:Key 로 다시 나타나면 사본이 되살아난 것이다.
        var staleKeys = new[] { "Acc.Btn.Danger", "Local.Button.Mini.Danger" };
        var offenders = new List<string>();
        var root = RepoRoot();

        // Act
        foreach (var project in OneButtonLanguageProjects)
        {
            var dir = Path.Combine(root, project);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                var text = File.ReadAllText(file);
                foreach (var key in staleKeys)
                    if (text.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal))
                        offenders.Add($"{Path.GetRelativePath(root, file)}: x:Key=\"{key}\"");
            }
        }

        // Assert
        Assert.True(offenders.Count == 0, $"로컬 위험 버튼 사본이 되살아났다: {string.Join("; ", offenders)}");
    }
    #endregion

    [Fact]
    public void should_not_reintroduce_a_local_chip_style_copy_after_migration()
    {
        // Arrange — U-12/U-13: EventDashboardView 의 ConsoleChip/ConsoleTab, ByComponentView 의
        // ByComponent.Chip 은 커널 Console.Chip/Console.Tab/Console.Chip.ListBoxItem 로 이관·삭제했다.
        // 이 이름들이 x:Key 로 다시 나타나면 로컬 사본이 되살아난 것이다(고스트 예약 없는 옛 버그 재발).
        var offenders = new List<string>();
        var staleKeys = new[] { "ConsoleChip", "ConsoleTab", "ByComponent.Chip" };
        var root = Path.Combine(RepoRoot());
        foreach (var project in new[] { "Ironwall.Dotnet.Libraries.Events.Ui", "Ironwall.Dotnet.Libraries.Devices.Ui" })
        {
            var dir = Path.Combine(root, project);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                var text = File.ReadAllText(file);
                foreach (var key in staleKeys)
                    if (text.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal))
                        offenders.Add($"{file}: x:Key=\"{key}\"");
            }
        }

        // Assert
        Assert.True(offenders.Count == 0, $"로컬 칩 사본이 되살아났다: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void should_not_reference_console_styles_with_staticresource_inside_the_kernel()
    {
        // Arrange + Act — 테마 사전 안의 StaticResource 는 Application.Resources 를 보지 못한다(실측)
        var bad = Regex.Matches(Kernel(), @"\{StaticResource\s+(Console\.(?:Button|SearchBox|CheckBox|ToggleSwitch|ScrollBar|ScrollViewer|Pill)[A-Za-z0-9_.]*)\s*\}")
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        // Assert
        Assert.True(bad.Count == 0, $"커널이 StaticResource 로 부르는 공용 키(런타임에 UnsetValue): {string.Join(", ", bad)}");
    }
    #endregion

    #region - K-01 / K-09 disabled is a colour shift, not a fade -
    [Theory]
    [InlineData("Console.Button")]
    [InlineData("Console.Button.Primary")]
    [InlineData("Console.CheckBox")]
    [InlineData("Console.ToggleSwitch")]
    public void should_not_express_disabled_as_opacity_when_style_is_a_console_control(string key)
    {
        // Arrange — 꺼짐을 Opacity 로 표현하면 글자와 판이 함께 옅어져 대비가 오히려 무너진다(측정 2.05~2.89)
        var block = StyleBlock(ThemeFile("Styles.Console.xaml"), key);
        var disabled = DisabledTrigger(block);

        // Assert
        Assert.DoesNotContain("Opacity", disabled);
        Assert.Contains("TextMutedBrush", disabled);
    }

    [Fact]
    public void should_drop_the_filled_look_when_primary_button_is_disabled()
    {
        // Arrange + Act
        var disabled = DisabledTrigger(StyleBlock(ThemeFile("Styles.Console.xaml"), "Console.Button.Primary"));

        // Assert — 꺼진 Primary 가 켜진 것과 같은 채움이면 '이 창의 할 일' 로 오독된다
        Assert.Contains("SurfaceSunkenBrush", disabled);
        Assert.DoesNotContain("PrimaryBrush", disabled);
    }

    [Fact]
    public void should_add_a_shape_cue_when_checkbox_is_disabled_and_unchecked()
    {
        // Arrange + Act — 꺼짐과 '안 눌림' 을 색이 아니라 형태로 가른다
        var block = StyleBlock(ThemeFile("Styles.Console.xaml"), "Console.CheckBox");

        // Assert
        Assert.Contains("x:Name=\"Slash\"", block);
        Assert.Contains("<MultiTrigger>", block);
    }
    #endregion

    #region - K-10 two-tone focus ring -
    [Theory]
    [InlineData("Console.Button")]
    [InlineData("Console.Button.Primary")]
    [InlineData("Console.SearchBox")]
    [InlineData("Console.CheckBox")]
    [InlineData("Console.ToggleSwitch")]
    public void should_draw_a_two_tone_focus_ring_when_style_is_a_console_control(string key)
    {
        // Arrange — 라이트에서 FocusRingBrush 와 PrimaryBrush 는 완전히 같은 색(1.00:1)이다.
        // 한 겹 고리는 채운 버튼 위에서 사라지므로 안쪽 1px OnPrimary 를 덧대 경계를 만든다.
        var block = StyleBlock(ThemeFile("Styles.Console.xaml"), key);

        // Assert
        Assert.Contains("x:Name=\"FocusOuter\"", block);
        Assert.Contains("x:Name=\"FocusInner\"", block);
        Assert.Contains("FocusRingBrush", block);
        Assert.Contains("OnPrimaryBrush", block);
    }
    #endregion

    #region - K-03 token contrast -
    public static IEnumerable<object[]> StructuralPairs => new[]
    {
        //           theme,          line,            plate,               최소 비율
        new object[] { "Light", "BorderBrush",  "SurfaceBrush",     3.0 },
        new object[] { "Light", "BorderBrush",  "SurfaceSunkenBrush", 3.0 },
        new object[] { "Light", "DividerBrush", "SurfaceAltBrush",  3.0 },
        new object[] { "Dark",  "BorderBrush",  "SurfaceBrush",     3.0 },
        new object[] { "Dark",  "BorderBrush",  "SurfaceSunkenBrush", 3.0 },
        new object[] { "Dark",  "DividerBrush", "SurfaceBrush",     3.0 },
    };

    [Theory]
    [MemberData(nameof(StructuralPairs))]
    public void should_meet_non_text_contrast_when_line_is_structural(string theme, string line, string plate, double floor)
    {
        // Arrange
        var tokens = ThemeFile($"Tokens.{theme}.xaml");

        // Act
        var ratio = Contrast(Token(tokens, line), Token(tokens, plate));

        // Assert — WCAG 1.4.11 비텍스트 대비. 옛 값은 1.13~2.45 였다.
        Assert.True(ratio >= floor, $"{theme} {line}/{plate} = {ratio:0.00} (< {floor})");
    }

    [Theory]
    [InlineData("Light", "SurfaceAltBrush")]
    [InlineData("Dark", "SurfaceAltBrush")]
    public void should_stay_softer_than_structural_lines_when_line_is_a_table_row(string theme, string plate)
    {
        // Arrange — 행 구분선까지 3:1 로 올리면 목록이 스프레드시트가 된다. 선택 표시가 뜻을 나르므로 약해도 된다.
        var tokens = ThemeFile($"Tokens.{theme}.xaml");

        // Act
        var row = Contrast(Token(tokens, "RowLineBrush"), Token(tokens, plate));
        var divider = Contrast(Token(tokens, "DividerBrush"), Token(tokens, plate));

        // Assert
        Assert.InRange(row, 1.7, 2.6);
        Assert.True(divider > row, $"{theme}: 구조선({divider:0.00})이 행선({row:0.00})보다 진해야 한다");
    }

    [Fact]
    public void should_keep_the_same_token_keyset_when_themes_differ()
    {
        // Arrange + Act — 한쪽에만 있는 키는 테마 전환 때 조용한 기본값이 된다
        var light = Keys(ThemeFile("Tokens.Light.xaml"));
        var dark = Keys(ThemeFile("Tokens.Dark.xaml"));

        // Assert
        Assert.Empty(light.Except(dark, StringComparer.Ordinal).Concat(dark.Except(light, StringComparer.Ordinal)));
        Assert.Contains("RowLineBrush", light);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void should_tint_the_unapplied_bar_strongly_enough_to_see(string theme)
    {
        // Arrange — 0x22(라이트) 는 흰 바탕과 1.18:1 이라 "변경 N건 미적용" 이 보이지 않았다
        var argb = Token(ThemeFile($"Tokens.{theme}.xaml"), "TintWarningBrush", alpha: true);

        // Act
        var alpha = Convert.ToInt32(argb.Substring(1, 2), 16);

        // Assert
        Assert.True(alpha >= 0x50, $"{theme} TintWarningBrush alpha = 0x{alpha:X2} (< 0x50)");
    }
    #endregion

    #region - D-17 / D-44 kernel typography and spacing -
    [Fact]
    public void should_wrap_with_overflow_so_korean_lines_keep_no_orphan_syllable()
    {
        // Arrange + Act — TextWrapping="Wrap" 은 한 음절만 남는 줄을 만든다(실측: 12px 셋째 줄에 한 음절)
        var kernel = Kernel();

        // Assert
        Assert.DoesNotContain("TextWrapping=\"Wrap\"", kernel);
        Assert.Contains("TextWrapping=\"WrapWithOverflow\"", kernel);
    }

    [Fact]
    public void should_use_an_eight_pixel_gap_between_kernel_buttons()
    {
        // Arrange — 집 규칙은 8px 다. 커널이 6 이면 창도 6 을 따라 한다.
        var kernel = Kernel();

        // Act
        var six = Regex.Matches(kernel, "Margin=\"6,0,0,0\"").Count;
        var eight = Regex.Matches(kernel, "Margin=\"8,0,0,0\"").Count;

        // Assert
        Assert.Equal(0, six);
        Assert.True(eight >= 4, $"8px 간격이 {eight}곳 — 툴바 3 + 적용 막대 1 이상이어야 한다");
    }

    [Fact]
    public void should_let_the_apply_bar_message_wrap_instead_of_cutting_it()
    {
        // Arrange + Act — 한 줄 + 줄임표였을 때 "적용하거나 되돌린 뒤 이동하세요" 가 매번 잘렸다
        var footer = Between(Kernel(), "x:Name=\"Msg\"", "/>");

        // Assert
        Assert.Contains("TextWrapping=\"WrapWithOverflow\"", footer);
    }

    [Fact]
    public void should_keep_the_search_box_from_being_squeezed_by_a_long_extra()
    {
        // Arrange + Act — 옛 DockPanel(LastChildFill) 에서 검색은 '남는 자리' 라 MinWidth 아래로 눌렸다.
        // 두 줄 구조(D-2026-09-27-71c353): 검색은 고정 폭(코드가 180~240 을 건다)이고 가운데 칸 MinWidth 도 코드가 그 폭에 맞춘다.
        var toolbar = Between(Kernel(), "c:ConsoleToolbar}\">", "───────── 상세 호스트");

        // Assert
        Assert.Contains("Width=\"{x:Static c:ConsoleLayoutMath.ToolbarSearchPreferredWidth}\"", toolbar);
        Assert.DoesNotContain("<DockPanel LastChildFill=\"True\">", toolbar);
    }

    [Fact]
    public void should_put_the_filters_on_their_own_row_below_the_action_row()
    {
        // Arrange + Act — 필터 칩이 첫째 줄(검색 · 동작)과 폭을 나눠 먹던 한 줄 구조를 되살리지 않는다
        var toolbar = Between(Kernel(), "c:ConsoleToolbar}\">", "───────── 상세 호스트");
        var actionRowEnd = toolbar.IndexOf("x:Name=\"PART_Columns\"", StringComparison.Ordinal);
        var filters = toolbar.IndexOf("Content=\"{TemplateBinding Filters}\"", StringComparison.Ordinal);

        // Assert — 필터 자리는 첫째 줄의 마지막 부품([열 n/m])보다 뒤, 둘째 줄에만 있다
        Assert.True(actionRowEnd > 0 && filters > actionRowEnd, "필터 자리가 첫째 줄 안에 있다");
        Assert.Equal(1, CountOf(toolbar, "{TemplateBinding Filters}"));
        Assert.Contains("MinHeight\" Value=\"{x:Static c:ConsoleLayoutMath.FilterRowMinHeight}\"", toolbar);
        Assert.Contains("Binding HasFilterRow", toolbar);
    }

    private static int CountOf(string text, string token)
    {
        var count = 0;
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0; i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal)) count++;
        return count;
    }
    #endregion

    #region - Helpers -
    private static string StyleBlock(string xaml, string key)
    {
        var start = xaml.IndexOf($"x:Key=\"{key}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{key} 정의를 찾지 못했다");
        var end = xaml.IndexOf("\n\t</Style>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"{key} 블록의 끝을 찾지 못했다");
        return xaml[start..end];
    }

    private static string DisabledTrigger(string styleBlock)
    {
        var at = styleBlock.IndexOf("Property=\"IsEnabled\" Value=\"False\"", StringComparison.Ordinal);
        Assert.True(at >= 0, "IsEnabled=False 트리거가 없다");
        var end = styleBlock.IndexOf("</Trigger>", at, StringComparison.Ordinal);
        return styleBlock[at..end];
    }

    private static string Between(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(a >= 0, $"'{from}' 를 찾지 못했다");
        var b = text.IndexOf(to, a, StringComparison.Ordinal);
        return text[a..(b < 0 ? text.Length : b)];
    }

    private static string Token(string tokensXaml, string key, bool alpha = false)
    {
        var m = Regex.Match(tokensXaml, $"x:Key=\"{Regex.Escape(key)}\"\\s+Color=\"(#[0-9A-Fa-f]{{6,8}})\"");
        Assert.True(m.Success, $"{key} 토큰을 찾지 못했다");
        var value = m.Groups[1].Value;
        return alpha ? value : (value.Length == 9 ? "#" + value[3..] : value);
    }

    private static double Linear(int c)
    {
        var v = c / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static double Luminance(string hex)
        => 0.2126 * Linear(Convert.ToInt32(hex.Substring(1, 2), 16))
         + 0.7152 * Linear(Convert.ToInt32(hex.Substring(3, 2), 16))
         + 0.0722 * Linear(Convert.ToInt32(hex.Substring(5, 2), 16));

    private static double Contrast(string a, string b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
    #endregion
}
