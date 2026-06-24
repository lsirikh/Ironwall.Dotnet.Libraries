using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// IMPL-12/13/14 검증 — 공용 스타일셋(Styles.Controls/Containers/Nav)이 참조하는
/// 테마 토큰 키가 모두 Tokens(Light/Shared/Converters)에 정의돼 있는지 확인(RISK-03 린터 적용).
/// MD BasedOn(StaticResource MaterialDesign*)·MahApps* 키는 외부 어셈블리 소유 → 검사 제외.
/// </summary>
public class StyleTokenReferenceTests
{
    private static string ThemesDir([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(thisFile)!, "..",
            "Ironwall.Dotnet.Libraries.Theme", "Themes"));

    private static string Read(string fileName)
        => File.ReadAllText(Path.Combine(ThemesDir(), fileName));

    private static bool IsExternalKey(string key)
        => key.StartsWith("MaterialDesign", StringComparison.Ordinal)
        || key.StartsWith("MahApps", StringComparison.Ordinal);

    public static IEnumerable<object[]> StyleFiles => new[]
    {
        new object[] { "Styles.Controls.xaml" },
        new object[] { "Styles.Containers.xaml" },
        new object[] { "Styles.Nav.xaml" },
    };

    [Theory]
    [MemberData(nameof(StyleFiles))]
    public void should_define_all_referenced_tokens_when_style_file_uses_dynamicresource(string styleFile)
    {
        // Arrange — 정의 집합 = Light + Shared + Converters + 해당 스타일 파일 자체 키(intra-file BasedOn)
        var styleXaml = Read(styleFile);
        var defined = ThemeKeyLinter.ExtractDefinedKeys(Read("Tokens.Light.xaml"));
        foreach (var k in ThemeKeyLinter.ExtractDefinedKeys(Read("Tokens.Shared.xaml")))
            defined.Add(k);
        foreach (var k in ThemeKeyLinter.ExtractDefinedKeys(Read("Converters.xaml")))
            defined.Add(k);
        foreach (var k in ThemeKeyLinter.ExtractDefinedKeys(styleXaml))   // 파일 내 정의 스타일(예: StatusBadgeBase)
            defined.Add(k);

        // 참조 키에서 외부(MD/MahApps BasedOn) 키 제외 → 토큰 + 자체 스타일 참조만
        var tokenRefs = ThemeKeyLinter.ExtractReferencedKeys(styleXaml)
                                      .Where(k => !IsExternalKey(k));

        // Act
        var undefined = ThemeKeyLinter.FindUndefinedReferences(tokenRefs, defined);

        // Assert — 미정의 토큰 참조 0 (오타/누락 시 fail)
        Assert.True(undefined.Count == 0,
            $"{styleFile}: 정의되지 않은 토큰 참조 → {string.Join(", ", undefined)}");
    }

    [Fact]
    public void should_reference_at_least_one_token_when_styles_loaded()
    {
        // Arrange + Act — 스타일셋이 실제로 토큰을 소비하는지(공허한 통과 방지)
        var refs = ThemeKeyLinter.ExtractReferencedKeys(Read("Styles.Controls.xaml"))
                                 .Where(k => !IsExternalKey(k))
                                 .ToList();

        // Assert
        Assert.Contains("PrimaryBrush", refs);
        Assert.Contains("UiFont", refs);
    }
}
