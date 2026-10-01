using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// "?" 계약(help-callout NFR-01 · NFR-02) — 화면이 거는 키는 설명 목록에 있고, 목록 항목은 어딘가에서 쓰이며(고아 0),
/// "?" 는 명시 템플릿 + <c>Help.{키}</c> id 를 갖고, 화면에 설명형 문구가 <b>새로</b> 쌓이지 않는다.
/// </summary>
/// <remarks>
/// <para>설명형 문구 허용 목록(<c>Help/explanatory-text-allowlist.txt</c>)은 H-1 시점 재고로 시작한다 — 지금은 통과하고,
/// H-2 ~ H-4 가 화면을 옮길 때마다 줄이 빠진다. <b>새 줄만</b> 실패다. 목록을 다시 찍으려면
/// <c>IRONWALL_HELP_ALLOWLIST_WRITE=1</c> 로 이 시험을 한 번 돌린다(줄이는 방향으로만 쓴다 — 늘리려면 이유를 적고 검토를 받는다).</para>
/// </remarks>
public class HelpContractTests
{
    /// <summary>옛 화면 id 를 잇는 별칭 — "파일:속성=값". 헤디드 시험이 옛 id 로 찾는 자리만. 이유 없이 늘리지 않는다.</summary>
    private static readonly HashSet<string> AliasAllowList = new(StringComparer.Ordinal)
    {
        // 결선 펜스 보기 [?] — 헤디드 SC-FEN-013(Dotnet.Monitoring.Solution UiTests R21)이 이 두 id 로 찾고 'Shift+끌기' 를 읽는다.
        "Ironwall.Dotnet.Libraries.Devices.Ui/Consoles/Wiring/Fence/FenceView.xaml:AutomationProperties.AutomationId=Devices.Wiring.Fence.Help",
        "Ironwall.Dotnet.Libraries.Devices.Ui/Consoles/Wiring/Fence/FenceView.xaml:BodyAutomationId=Devices.Wiring.Fence.HelpText",
    };

    private static string Root([CallerFilePath] string? thisFile = null) => HelpSourceScan.RepoRoot(thisFile!);

    private static string AllowListPath([CallerFilePath] string? thisFile = null)
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "explanatory-text-allowlist.txt");

    [Fact]
    public void should_find_every_help_key_used_by_a_screen_in_a_catalog()
    {
        // Arrange
        var root = Root();
        var defined = HelpSourceScan.CatalogKeys(root).Select(k => k.Key).ToHashSet(StringComparer.Ordinal);
        var used = HelpSourceScan.UsedKeys(root);

        // Act
        var missing = used.Where(u => !defined.Contains(u.Key)).Select(u => $"{u.Key} ({u.Value})").OrderBy(s => s, StringComparer.Ordinal).ToList();

        // Assert — 빠지면 그 "?" 는 꺼진 채로 뜬다(죽지는 않지만 설명이 사라진다)
        Assert.True(missing.Count == 0, $"설명 목록에 없는 키: {string.Join("; ", missing)}");
        Assert.NotEmpty(used);   // 공허한 통과 방지 — 시범 두 자리가 있다
    }

    [Fact]
    public void should_use_every_catalog_entry_somewhere_when_the_catalogs_are_scanned()
    {
        var root = Root();
        var used = HelpSourceScan.UsedKeys(root);

        var orphans = HelpSourceScan.CatalogKeys(root).Where(k => !used.ContainsKey(k.Key)).Select(k => $"{k.Key} ({k.File})").ToList();

        Assert.True(orphans.Count == 0, $"아무 화면도 걸지 않는 설명: {string.Join("; ", orphans)}");
    }

    [Fact]
    public void should_define_each_help_key_once_across_all_catalogs()
    {
        var duplicates = HelpSourceScan.CatalogKeys(Root()).GroupBy(k => k.Key).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(k => k.File))}").ToList();

        Assert.True(duplicates.Count == 0, $"두 번 정의된 키(나중 것이 조용히 이긴다): {string.Join("; ", duplicates)}");
    }

    [Fact]
    public void should_keep_local_ids_on_question_marks_only_for_listed_legacy_aliases()
    {
        var offenders = HelpSourceScan.HelpTipAliases(Root())
            .Select(a => $"{a.File}:{a.Attribute}={a.Value}")
            .Where(a => !AliasAllowList.Contains(a)).ToList();

        Assert.True(offenders.Count == 0, $"Help.{{키}} 대신 지역 id 를 건 \"?\": {string.Join("; ", offenders)}");
    }

    [Fact]
    public void should_give_the_question_mark_an_explicit_template_and_a_help_dot_key_automation_id()
    {
        // Arrange — 커널 기본 스타일(테마 사전)
        var xaml = File.ReadAllText(Path.Combine(Root(), "Ironwall.Dotnet.Libraries.Utils", "Themes", "Generic.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var style = XDocument.Parse(xaml).Descendants(p + "Style").Single(s => (string?)s.Attribute("TargetType") == "{x:Type c:HelpTip}");
        var setters = style.Elements(p + "Setter").ToDictionary(s => (string)s.Attribute("Property")!, s => s);

        // Assert — 버튼 계열은 명시 템플릿(MD3 암시 스타일로 떨어지지 않는다) · 자동화 id 는 Help.{키}
        Assert.True(setters.ContainsKey("Template"));
        Assert.Contains("StringFormat=Help.{0}", (string)setters["AutomationProperties.AutomationId"].Attribute("Value")!, StringComparison.Ordinal);
        Assert.Contains("PART_Callout", style.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void should_not_add_new_explanatory_text_to_screens_outside_the_allow_list()
    {
        // Arrange
        var current = HelpSourceScan.ExplanatoryText(Root());
        if (Environment.GetEnvironmentVariable("IRONWALL_HELP_ALLOWLIST_WRITE") == "1")
        {
            WriteAllowList(current);
            return;
        }
        var allowed = File.ReadAllLines(AllowListPath(), Encoding.UTF8)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .GroupBy(l => l, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Act — 다중집합 차(같은 글이 한 파일에 두 번이면 두 줄)
        var added = new List<string>();
        foreach (var group in current.GroupBy(l => l, StringComparer.Ordinal))
        {
            var extra = group.Count() - (allowed.TryGetValue(group.Key, out var n) ? n : 0);
            for (var i = 0; i < extra; i++) added.Add(group.Key.Replace('\t', ' '));
        }

        // Assert — 사용법 · 용어 · 동작 원리는 섹션 · 창 "?"(HelpCatalog)로. 잠금 사유는 ConsoleField.LockReason.
        Assert.True(added.Count == 0,
            $"새 설명형 문구 {added.Count}건 — \"?\" 로 옮기거나(HelpKey + 설명 목록) 상태 · 잠금 사유면 LockReason 을 쓰세요: {string.Join(" | ", added.Take(20))}");
    }

    [Fact]
    public void should_start_the_allow_list_from_the_current_inventory()
    {
        var lines = File.ReadAllLines(AllowListPath(), Encoding.UTF8).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(3, l.Split('\t').Length));
    }

    private static void WriteAllowList(IReadOnlyList<string> current)
    {
        var header = new[]
        {
            "# help-callout NFR-02 — 화면에 박힌 설명형 문구의 허용 목록(H-1 시점 재고). 형식: 경로<TAB>종류<TAB>값",
            "# 종류: binding = *Hint · *Note · *Explain* 바인딩 / literal = Note= · Hint= 글자 값 / text = 40자 이상 TextBlock 글자.",
            "# 줄이 빠지는 것은 좋다(H-2 ~ H-4 이전). 새 줄은 HelpContractTests 가 막는다 — 설명은 \"?\"(HelpCatalog)로.",
            "# 다시 찍기: IRONWALL_HELP_ALLOWLIST_WRITE=1 dotnet test --filter should_not_add_new_explanatory_text",
        };
        File.WriteAllLines(AllowListPath(), header.Concat(current.OrderBy(l => l, StringComparer.Ordinal)), new UTF8Encoding(true));
    }
}
