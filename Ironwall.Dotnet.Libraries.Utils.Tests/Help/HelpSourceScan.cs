using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// "?" 계약 시험의 원본 훑기 — 제품 프로젝트의 XAML · C# 글에서 도움말 키 · 설명 목록 키 · 설명형 문구를 찾는다(help-callout NFR-01 · NFR-02).
/// </summary>
/// <remarks>
/// 런타임에만 풀리는 배선(키 ↔ 목록)은 깨져도 빌드가 통과한다 — 그래서 글로 잡는다(ConsoleStyleContractTests 와 같은 이유).
/// <c>Consoles.ViewTests</c> 가 이 파일을 연결해 런타임 등록과 견준다(사본을 만들지 않는다).
/// </remarks>
internal static class HelpSourceScan
{
    /// <summary>훑지 않는 맨 위 폴더 — 벤더 · 도구 · 시험 · 문서.</summary>
    private static readonly HashSet<string> SkippedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "GMap.NET", "third_party", "tools", "tests", "docs", "scripts", ".git", ".claude", ".playwright-mcp",
    };

    private static readonly Regex XamlHelpKey = new(@"\bHelpKey\s*=\s*""([^""{][^""]*)""", RegexOptions.Compiled);
    private static readonly Regex CodeHelpKey = new(@"\bHelpKey\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex CatalogKey = new(@"(?:HelpEntry\.Create|new\s+HelpEntry)\(\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex StartTag = new(@"<([A-Za-z_][\w.:]*)((?:\s+[\w:.]+\s*=\s*""[^""]*"")*)\s*/?>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Attribute = new(@"([\w:.]+)\s*=\s*""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex BindingPath = new(@"^\{Binding\s+(?:Path\s*=\s*)?([^,\s}=]+)\s*(?:[,}])", RegexOptions.Compiled);

    /// <summary>설명형 긴 글 기준(글자 수) — PRD NFR-02 "긴 리터럴 TextBlock".</summary>
    public const int LongTextLength = 40;

    /// <summary>저장소 뿌리(이 파일 기준).</summary>
    public static string RepoRoot(string thisFile) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    /// <summary>제품 원본 파일(bin · obj · Tests 폴더 · 벤더 제외).</summary>
    public static IEnumerable<string> ProductFiles(string root, string pattern)
    {
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(dir);
            if (SkippedRoots.Contains(name) || name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
            {
                var sep = Path.DirectorySeparatorChar;
                if (file.Contains($"{sep}bin{sep}") || file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}Tests{sep}")) continue;
                yield return file;
            }
        }
    }

    public static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    /// <summary>화면이 거는 키(XAML 글자 값 · C# 대입) → 처음 본 자리.</summary>
    public static IReadOnlyDictionary<string, string> UsedKeys(string root)
    {
        var used = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in ProductFiles(root, "*.xaml"))
            foreach (Match m in XamlHelpKey.Matches(File.ReadAllText(file)))
                used.TryAdd(m.Groups[1].Value, Relative(root, file));
        foreach (var file in ProductFiles(root, "*.cs"))
            foreach (Match m in CodeHelpKey.Matches(File.ReadAllText(file)))
                used.TryAdd(m.Groups[1].Value, Relative(root, file));
        return used;
    }

    /// <summary>XAML 이 거는 키만(프로젝트 폴더 이름 → 키들).</summary>
    public static IReadOnlyList<(string Project, string Key)> XamlKeys(string root)
        => ProductFiles(root, "*.xaml")
            .SelectMany(file => XamlHelpKey.Matches(File.ReadAllText(file)).Select(m => (Project: Relative(root, file).Split('/')[0], Key: m.Groups[1].Value)))
            .ToList();

    /// <summary>설명 목록이 정의한 키 — (키, 자리). 같은 키가 두 번 나오면 둘 다 돌려준다.</summary>
    public static IReadOnlyList<(string Key, string File)> CatalogKeys(string root)
        => ProductFiles(root, "*.cs")
            .SelectMany(file => CatalogKey.Matches(File.ReadAllText(file)).Select(m => (m.Groups[1].Value, Relative(root, file))))
            .ToList();

    /// <summary>XAML 의 <c>HelpTip</c> 요소마다 지역 AutomationId · BodyAutomationId(옛 id 별칭).</summary>
    public static IReadOnlyList<(string File, string Attribute, string Value)> HelpTipAliases(string root)
    {
        var found = new List<(string, string, string)>();
        foreach (var file in ProductFiles(root, "*.xaml"))
        {
            foreach (Match tag in StartTag.Matches(File.ReadAllText(file)))
            {
                if (LocalName(tag.Groups[1].Value) != "HelpTip") continue;
                foreach (Match a in Attribute.Matches(tag.Groups[2].Value))
                    if (a.Groups[1].Value is "AutomationProperties.AutomationId" or "BodyAutomationId")
                        found.Add((Relative(root, file), a.Groups[1].Value, a.Groups[2].Value));
            }
        }
        return found;
    }

    /// <summary>
    /// 화면에 박힌 설명형 문구 — <c>*Hint</c> · <c>*Note</c> · <c>*Explain*</c> 바인딩, <c>Note=</c> · <c>Hint=</c> 글자 값, 40자 이상 TextBlock 글자.
    /// 한 줄 = <c>경로 \t 종류 \t 값</c>(줄 번호는 넣지 않는다 — 위에 줄이 늘어도 같은 항목이다).
    /// </summary>
    public static IReadOnlyList<string> ExplanatoryText(string root)
    {
        var lines = new List<string>();
        foreach (var file in ProductFiles(root, "*.xaml"))
        {
            var relative = Relative(root, file);
            foreach (Match tag in StartTag.Matches(File.ReadAllText(file)))
            {
                var element = LocalName(tag.Groups[1].Value);
                foreach (Match a in Attribute.Matches(tag.Groups[2].Value))
                {
                    var attribute = a.Groups[1].Value;
                    var raw = a.Groups[2].Value;
                    var name = LocalName(attribute);

                    if (raw.StartsWith("{Binding", StringComparison.Ordinal))
                    {
                        var path = BindingPath.Match(raw + (raw.EndsWith('}') ? "" : "}"));
                        if (path.Success && IsExplanatoryName(path.Groups[1].Value.Split('.').Last()))
                            lines.Add(Line(relative, "binding", $"{attribute}={path.Groups[1].Value}"));
                        continue;
                    }
                    if (raw.StartsWith("{", StringComparison.Ordinal)) continue;   // 다른 마크업 확장(TemplateBinding · 자원)

                    var text = Clean(WebUtility.HtmlDecode(raw));
                    if (text.Length == 0) continue;
                    if (name is "Note" or "Hint" || name.Contains("Explain", StringComparison.Ordinal))
                        lines.Add(Line(relative, "literal", $"{attribute}={text}"));
                    else if (element == "TextBlock" && attribute == "Text" && text.Length >= LongTextLength)
                        lines.Add(Line(relative, "text", text));
                }
            }
        }
        return lines;
    }

    private static bool IsExplanatoryName(string member)
    {
        var name = member.TrimEnd(']');
        var bracket = name.IndexOf('[');
        if (bracket >= 0) name = name[..bracket];
        return name.EndsWith("Hint", StringComparison.Ordinal) || name.EndsWith("Note", StringComparison.Ordinal) || name.Contains("Explain", StringComparison.Ordinal);
    }

    private static string LocalName(string qualified)
    {
        var colon = qualified.LastIndexOf(':');
        var local = colon >= 0 ? qualified[(colon + 1)..] : qualified;
        var dot = local.LastIndexOf('.');
        return dot >= 0 ? local[(dot + 1)..] : local;
    }

    private static string Clean(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string Line(string file, string kind, string value) => $"{file}\t{kind}\t{value}";
}
