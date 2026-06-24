using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// TEST-LINT — RISK-03 린터 자체 검증 + 실제 Tokens.Light/Dark 파리티 게이트.
/// </summary>
public class ThemeKeyLinterTests
{
    // 테스트 소스 위치 기준으로 실제 토큰 XAML 경로 해소(빌드 머신).
    private static string ThemesDir([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(thisFile)!, "..",
            "Ironwall.Dotnet.Libraries.Theme", "Themes"));

    private static string ReadToken(string fileName)
        => File.ReadAllText(Path.Combine(ThemesDir(), fileName));

    [Fact]
    public void should_pass_when_all_keys_defined_in_both_themes()
    {
        // Arrange — 실제 출하 토큰 dict
        var light = ThemeKeyLinter.ExtractDefinedKeys(ReadToken("Tokens.Light.xaml"));
        var dark = ThemeKeyLinter.ExtractDefinedKeys(ReadToken("Tokens.Dark.xaml"));

        // Act
        var gaps = ThemeKeyLinter.FindParityGaps(light, dark);

        // Assert — Light/Dark 키셋 완전 일치(파리티 갭 0)
        Assert.True(light.Count > 0, "Light 토큰 키가 추출돼야 한다");
        Assert.Empty(gaps);
    }

    [Fact]
    public void should_fail_when_referenced_key_undefined()
    {
        // Arrange
        var defined = ThemeKeyLinter.ExtractDefinedKeys(ReadToken("Tokens.Light.xaml"));
        var referenced = new[] { "PrimaryBrush", "TotallyMissingBrush" };

        // Act
        var undefined = ThemeKeyLinter.FindUndefinedReferences(referenced, defined);

        // Assert — 미정의 키만 보고
        Assert.Contains("TotallyMissingBrush", undefined);
        Assert.DoesNotContain("PrimaryBrush", undefined);
    }

    [Fact]
    public void should_report_empty_when_all_references_defined()
    {
        // Arrange
        var defined = ThemeKeyLinter.ExtractDefinedKeys(ReadToken("Tokens.Dark.xaml"));
        var referenced = new[] { "PrimaryBrush", "BgBrush", "StatusCriticalBrush" };

        // Act
        var undefined = ThemeKeyLinter.FindUndefinedReferences(referenced, defined);

        // Assert
        Assert.Empty(undefined);
    }

    [Fact]
    public void should_extract_referenced_keys_when_dynamic_and_static()
    {
        // Arrange
        const string xaml =
            "<Grid Background=\"{DynamicResource BgBrush}\" Foreground=\"{StaticResource TextPrimaryBrush}\" />";

        // Act
        var refs = ThemeKeyLinter.ExtractReferencedKeys(xaml);

        // Assert
        Assert.Contains("BgBrush", refs);
        Assert.Contains("TextPrimaryBrush", refs);
        Assert.Equal(2, refs.Count);
    }
}
