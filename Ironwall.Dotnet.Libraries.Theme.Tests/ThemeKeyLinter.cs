using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// RISK-03 — 참조-vs-정의 테마키 린터.
/// (1) Light/Dark 토큰 키 파리티: 한쪽에만 정의된 키 = silent default 위험.
/// (2) 참조 키 해소: 참조됐으나 어느 테마 dict에도 없는 키 = 빌드 fail 대상.
/// <para>Phase 1 = 린터 로직 + 자체 테스트(TEST-LINT). Phase 4에서 134 DynamicResource 전수(VER-04b)에 적용 + 빌드 게이트화.</para>
/// </summary>
public static class ThemeKeyLinter
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex ReferenceRx =
        new(@"\{\s*(?:Dynamic|Static)Resource\s+([A-Za-z_][A-Za-z0-9_.]*)\s*\}", RegexOptions.Compiled);

    /// <summary>ResourceDictionary XAML에서 정의된 모든 <c>x:Key</c> 추출.</summary>
    public static ISet<string> ExtractDefinedKeys(string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent);
        return doc.Descendants()
                  .Select(e => e.Attribute(XamlNs + "Key")?.Value)
                  .Where(v => !string.IsNullOrWhiteSpace(v))
                  .Select(v => v!)
                  .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>XAML에서 참조된 모든 <c>{DynamicResource X}</c> / <c>{StaticResource X}</c> 키 추출.</summary>
    public static ISet<string> ExtractReferencedKeys(string xamlContent)
        => ReferenceRx.Matches(xamlContent)
                      .Select(m => m.Groups[1].Value)
                      .ToHashSet(StringComparer.Ordinal);

    /// <summary>Light XOR Dark — 한쪽에만 정의된 키(파리티 갭). 비어야 정상.</summary>
    public static IReadOnlyList<string> FindParityGaps(ISet<string> lightKeys, ISet<string> darkKeys)
        => lightKeys.Except(darkKeys)
                    .Concat(darkKeys.Except(lightKeys))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToList();

    /// <summary>참조됐으나 정의 집합에 없는 키. 비어야 정상(빌드 fail 대상).</summary>
    public static IReadOnlyList<string> FindUndefinedReferences(
        IEnumerable<string> referencedKeys, ISet<string> definedKeys)
        => referencedKeys.Where(k => !definedKeys.Contains(k))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(k => k, StringComparer.Ordinal)
                         .ToList();
}
