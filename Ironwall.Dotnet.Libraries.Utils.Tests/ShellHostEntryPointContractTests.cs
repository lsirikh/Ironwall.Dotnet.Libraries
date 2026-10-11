using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.5(AUX) 중 W7 몫의 정적(소스) 계약 —
               AUX-030(닿지 않는 옛 화면 4개: 진입점 0) · AUX-031(옛 억제창 → 이벤트 창 억제 레일로 열림) ·
               AUX-002(WINDY 의 ✕ · Esc → [닫기] → 패널 닫기 배선 — 커널 거동은 ShellKernelScenarioTests).
   Notes     : 창 · 네트워크 없음. 이 저장소(라이브러리)와 형제 체크아웃인 호스트 저장소(Dotnet.Monitoring.Solution, 읽기만)의
               제품 소스(.cs · .xaml)를 훑는다 — 시험 · 도구 · 문서 · 빌드 산출물은 뺀다. 호스트 저장소가 없으면 실패로 알린다
               (조용히 통과하지 않는다).
****************************************************************************/
public class ShellHostEntryPointContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    /// <summary>호스트 저장소 뿌리 — 라이브러리 뿌리(워크트리면 그 위들)에서 위로 올라가며 형제 'Dotnet.Monitoring.Solution' 을 찾는다.</summary>
    private static string HostRoot()
    {
        for (var dir = new DirectoryInfo(RepoRoot()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Dotnet.Monitoring.Solution");
            if (File.Exists(Path.Combine(candidate, "Dotnet.Monitoring.Solution", "Bootstrapper.cs"))) return candidate;
        }
        throw new Xunit.Sdk.XunitException("호스트 저장소(Dotnet.Monitoring.Solution)를 라이브러리 저장소 위쪽 형제 폴더에서 찾지 못했다 — 이 시험은 호스트 소스가 필요하다.");
    }

    private static string HostApp() => Path.Combine(HostRoot(), "Dotnet.Monitoring.Solution");

    private static readonly string[] ExcludedSegments =
    {
        "bin", "obj", "Tests", "tests", "tools", "docs", "third_party", "scripts", ".git", ".claude", ".harness", ".harness-worktrees", ".playwright-mcp", "GMap.NET",
    };

    /// <summary>제품 소스 — 시험 프로젝트(이름이 .Tests · .UiTests 로 끝나거나 Tests 폴더) · 도구 · 문서 · 산출물 제외.</summary>
    private static IReadOnlyList<(string Path, string Text)> ProductSources(string root)
    {
        var list = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) continue;
            var rel = Path.GetRelativePath(root, file);
            var parts = rel.Split(Path.DirectorySeparatorChar);
            if (parts.Take(parts.Length - 1).Any(p => ExcludedSegments.Contains(p, StringComparer.Ordinal)
                                                    || p.EndsWith(".Tests", StringComparison.Ordinal) || p.EndsWith(".UiTests", StringComparison.Ordinal)
                                                    || p.EndsWith(".ViewTests", StringComparison.Ordinal))) continue;
            if (file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add((file, File.ReadAllText(file)));
        }
        return list;
    }

    private static readonly Lazy<IReadOnlyList<(string Path, string Text)>> Library = new(() => ProductSources(RepoRoot()));
    private static readonly Lazy<IReadOnlyList<(string Path, string Text)>> Host = new(() => ProductSources(HostApp()));

    private static IEnumerable<(string Path, string Text)> Everything => Library.Value.Concat(Host.Value);

    /// <summary>주석을 걷은 글 — 설명 주석 속 이름은 진입점이 아니다(// · /* */ · XML 주석 · XAML 주석).</summary>
    private static string Code(string path, string text)
    {
        if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) return Regex.Replace(text, "<!--.*?-->", "", RegexOptions.Singleline);
        var noBlock = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"//[^\n]*", "");
    }

    private static List<string> Hits(IEnumerable<(string Path, string Text)> files, string pattern, Func<string, bool>? skipFile = null)
        => files.Where(f => skipFile is null || !skipFile(f.Path))
                .SelectMany(f => Regex.Matches(Code(f.Path, f.Text), pattern).Select(m => $"{Path.GetFileName(f.Path)}: {m.Value}"))
                .ToList();

    private static bool IsFile(string path, params string[] names) => names.Any(n => string.Equals(Path.GetFileName(path), n, StringComparison.OrdinalIgnoreCase));

    private static string MethodBody(string text, string signaturePattern)
    {
        var m = Regex.Match(text, signaturePattern + @"[^{]*\{(?<b>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(m.Success, $"'{signaturePattern}' 본문을 찾지 못했다");
        return m.Groups["b"].Value;
    }

    // ── AUX-030: 닿지 않는 옛 화면 — 진입점 0 ───────────────────────────────────────────────────
    [Trait("Scenario", "AUX-030")]
    [Fact]
    public void should_find_no_entry_point_to_the_old_about_setup_view()
    {
        Assert.True(Library.Value.Count > 500 && Host.Value.Count > 100, $"훑은 소스가 너무 적다(라이브러리 {Library.Value.Count} · 호스트 {Host.Value.Count})");
        var own = new[] { "AboutSetupView.xaml", "AboutSetupView.xaml.cs" };

        // 화면(뷰) 자체를 부르는 곳 없음
        Assert.Empty(Hits(Everything, @"\bAboutSetupView\b", p => IsFile(p, own)));
        // 그 화면을 여는 메시지를 보내는 곳 없음(받는 처리기만 남아 있다)
        Assert.Empty(Hits(Everything, @"new\s+OpenAboutSetupPanelMessageModel\b"));
        // 뷰모델을 셸에 올리는 곳은 그 받는 처리기 하나뿐 — 보내는 곳이 없으니 닿지 않는다
        var activations = Hits(Everything, @"ActivateItemAsync\(\s*IoC\.Get<AboutSetupViewModel>\(\)");
        Assert.Equal(new[] { "ConductorControlViewModel.cs: ActivateItemAsync(IoC.Get<AboutSetupViewModel>()" }, activations);
        var conductor = File.ReadAllText(Path.Combine(HostApp(), "ViewModels", "Conductors", "ConductorControlViewModel.cs"));
        Assert.Contains("IoC.Get<AboutSetupViewModel>", MethodBody(conductor, @"public async Task HandleAsync\(OpenAboutSetupPanelMessageModel message"));
        // XAML 이 그 뷰모델을 화면으로 그리는 곳 없음(설정 콘솔은 IAboutSettingsPort 데이터 창구로만 쓴다)
        Assert.Empty(Hits(Everything, @"View\.Model=""\{Binding\s+AboutSetupViewModel\}""|x:Name=""AboutSetupViewModel"""));
    }

    [Trait("Scenario", "AUX-030")]
    [Fact]
    public void should_find_no_entry_point_to_the_old_account_manager_panel_view()
    {
        var own = new[] { "AccountManagerPanelView.xaml", "AccountManagerPanelView.xaml.cs" };

        Assert.Empty(Hits(Everything, @"\bAccountManagerPanelView\b", p => IsFile(p, own)));
        // 뷰모델은 계정 콘솔의 '사용자' 목록 데이터로만 쓰인다 — 그 자체를 화면으로 올리거나 그리는 곳 없음
        Assert.Empty(Hits(Everything, @"ActivateItemAsync\([^)]*AccountManagerPanelViewModel|IoC\.Get<AccountManagerPanelViewModel>"));
        Assert.Empty(Hits(Everything, @"\{Binding\s+AccountManagerPanelViewModel\}|x:Name=""AccountManagerPanelViewModel"""));
        // 옛 메뉴 메시지는 콘솔로 돌려 보낸다
        var conductor = File.ReadAllText(Path.Combine(HostApp(), "ViewModels", "Conductors", "ConductorControlViewModel.cs"));
        var body = Code("x.cs", MethodBody(conductor, @"public async Task HandleAsync\(OpenAccountManagerPanelMessageModel message"));
        Assert.Contains("IoC.Get<AccountConsolePanelViewModel>()", body);
        Assert.DoesNotContain("AccountManagerPanelViewModel", body);
    }

    [Trait("Scenario", "AUX-030")]
    [Fact]
    public void should_find_no_entry_point_to_the_old_sound_setting_view()
    {
        var own = new[] { "SoundSettingView.xaml", "SoundSettingView.xaml.cs", "SoundSettingViewModel.cs" };

        Assert.Empty(Hits(Everything, @"\bSoundSettingView\b", p => IsFile(p, own)));
        // 뷰모델은 모듈 등록 한 줄뿐 — 꺼내 쓰거나(IoC · 생성자 주입) 올리는 곳 없음
        var uses = Hits(Everything, @"\bSoundSettingViewModel\b", p => IsFile(p, own));
        Assert.Equal(new[] { "SoundUiModule.cs: SoundSettingViewModel" }, uses);
    }

    [Trait("Scenario", "AUX-030")]
    [Fact]
    public void should_find_no_entry_point_to_the_old_open_vca_panel_handler()
    {
        // 좌측 메뉴 뷰모델에 처리기 정의 한 줄만 있다 — 메뉴 XAML(Tag · cal:Message.Attach) · 멤버 분기(RunMember) 어디서도 부르지 않는다
        var all = Hits(Everything, @"\bOpenVcaPanelViewModel\b");
        Assert.Equal(new[] { "LeftMenuSectionViewModel.cs: OpenVcaPanelViewModel" }, all);
        var menuVm = File.ReadAllText(Path.Combine(HostApp(), "ViewModels", "Sections", "LeftMenuSectionViewModel.cs"));
        Assert.Matches(@"public async void OpenVcaPanelViewModel\(object sender, MouseButtonEventArgs e\)", menuVm);
        Assert.DoesNotContain("OpenVcaPanelViewModel", MethodBody(menuVm, @"private void RunMember\(string tag\)"));
    }

    // ── AUX-031: 옛 억제창 — 좌측 행은 이벤트 창(억제 스케줄 레일)으로 연다 ─────────────────────────────
    [Trait("Scenario", "AUX-031")]
    [Fact]
    public void should_open_the_events_console_on_the_suppression_rail_from_the_old_suppression_row_and_keep_no_old_window()
    {
        var host = HostApp();
        var menuXaml = File.ReadAllText(Path.Combine(host, "Views", "Sections", "LeftMenuSectionView.xaml"));
        var menuVm = File.ReadAllText(Path.Combine(host, "ViewModels", "Sections", "LeftMenuSectionViewModel.cs"));
        var conductor = File.ReadAllText(Path.Combine(host, "ViewModels", "Conductors", "ConductorControlViewModel.cs"));

        // 좌측 행은 그대로 있다(자동화 id 불변)
        Assert.Contains("AutomationProperties.AutomationId=\"Shell.LeftMenu.OpenEventSuppressionPanelViewModel\"", menuXaml);
        Assert.Contains("Tag=\"OpenEventSuppressionPanelViewModel\"", menuXaml);
        Assert.Contains("case nameof(OpenEventSuppressionPanelViewModel): OpenEventSuppressionPanelViewModel(this, null); break;", menuVm);
        Assert.Contains("new OpenEventSuppressionPanelMessageModel()", MethodBody(menuVm, @"public async void OpenEventSuppressionPanelViewModel\("));

        // 받는 쪽 — 이벤트 창을 올리고(막히면 멈춤) 억제 스케줄 레일로 옮긴다. 옛 억제창은 열지 않는다
        var body = MethodBody(conductor, @"public async Task HandleAsync\(OpenEventSuppressionPanelMessageModel message");
        Assert.Contains("if (!CanOpenPanel(\"events\")) return;", body);
        Assert.Contains("var events = IoC.Get<EventPanelViewModel>();", body);
        Assert.Contains("if (!ReferenceEquals(conductorShell.ActiveItem, events)) return;", body);
        Assert.Contains("await events.DashboardViewModel.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);", body);
        Assert.DoesNotContain("EventSuppressionSchedulePanel", Code("x.cs", body));

        // 그 레일은 라이브러리 이벤트 콘솔에 실재한다("억제 스케줄")
        var dashboard = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Events.Ui", "ViewModels", "Dashboards", "EventDashboardViewModel.cs"));
        Assert.Contains("public const string SuppressionRailKey = \"sup\";", dashboard);
        Assert.Contains("new ConsoleRailEntry(SuppressionRailKey, \"억제 스케줄\"", dashboard);

        // 옛 억제창(라이브러리 Events.Ui 의 뷰 · 뷰모델)은 남아 있지만 닿지 않는다 — DI 등록 한 줄뿐, 올리거나 꺼내 쓰는 곳 없음.
        // 그 창의 패널 id(Events.SuppressionSchedule.Panel)는 그 옛 뷰에만 있다 → 이 창을 찾는 옛 시험은 낡았다.
        var oldWindow = new[] { "EventSuppressionSchedulePanelView.xaml", "EventSuppressionSchedulePanelView.xaml.cs", "EventSuppressionSchedulePanelViewModel.cs" };
        Assert.Equal(new[] { "EventUiModule.cs: EventSuppressionSchedulePanelViewModel" },
                     Hits(Everything, @"\bEventSuppressionSchedulePanelView(Model)?\b", p => IsFile(p, oldWindow)));
        Assert.Equal(new[] { "EventSuppressionSchedulePanelView.xaml: \"Events.SuppressionSchedule.Panel\"" },
                     Hits(Everything, @"""Events\.SuppressionSchedule\.Panel"""));
    }

    // ── AUX-002: WINDY 닫기 배선(호스트) — ✕ · Esc → [닫기] Click → ClickCancel → 패널 닫기 ─────────────
    [Trait("Scenario", "AUX-002")]
    [Fact]
    public void should_wire_the_windy_header_x_and_escape_to_the_close_button_that_closes_the_panel()
    {
        var host = HostApp();
        var xaml = File.ReadAllText(Path.Combine(host, "Views", "Panels", "WindyPanelView.xaml"));
        var code = File.ReadAllText(Path.Combine(host, "Views", "Panels", "WindyPanelView.xaml.cs"));
        var forwarder = File.ReadAllText(Path.Combine(host, "Views", "PopupDialogs", "Common", "PopupActionForwarder.cs"));
        var vm = File.ReadAllText(Path.Combine(host, "ViewModels", "Panels", "WindyPanelViewModel.cs"));

        var frame = Regex.Match(Code("v.xaml", xaml), @"<dlg:ConsoleDialogFrame\b(?<a>[^>]*)>", RegexOptions.Singleline);
        Assert.True(frame.Success, "WindyPanelView 에 커널 다이얼로그 틀이 없다");
        var attrs = frame.Groups["a"].Value;
        foreach (var expected in new[] { "DialogKey=\"Shell.Windy\"", "HelpKey=\"Host.Windy.Mode\"", "SecondaryInvoked=\"OnSecondaryInvoked\"", "Size=\"Small\"", "PrimaryText=\"\"", "SecondaryText=\"\"" })
            Assert.Contains(expected, attrs);
        Assert.Matches(@"<Button\s[^>]*x:Name=""ClickCancel""[^>]*cal:Message\.Attach=""ClickCancel""", Code("v.xaml", xaml));
        Assert.Contains("private void OnSecondaryInvoked(object sender, RoutedEventArgs e) => PopupActionForwarder.Invoke(ClickCancel);", code);
        Assert.Contains("action.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, action));", forwarder);
        Assert.Contains("new ClosePanelMessageModel()", MethodBody(vm, @"public async void ClickCancel\(\)"));
    }
}
