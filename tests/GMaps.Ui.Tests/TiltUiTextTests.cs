using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 지도 기울이기 제거(2026-10-19, 사용자 요청) — 예전엔 MapViewModel.Tilt.cs 가 쓰던 표시 로직 TiltUiText(툴팁 사유 · 「기울임 N°」 배지 ·
/// 토글 잠금 · 각도 스텝)를 실코드 링크로 검증했다. 그 소비처(툴바 토글 · 배지 · VM 배선)가 모두 없어져 TiltUiText 도 지웠으므로,
/// 지금은 그 파일과 VM 파셜이 되살아나지 않았는지(링크 · 원본 모두 없음) 단언한다.
/// </summary>
public class TiltUiTextTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Theory]
    [InlineData("Utils/TiltUiText.cs")]
    [InlineData("ViewModels/Maps/MapViewModel.Tilt.cs")]
    [InlineData("Helpers/TiltOverscanConverter.cs")]
    public void should_have_no_tilt_ui_source_file_when_tilt_feature_removed(string relative)
        => Assert.False(File.Exists(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", relative)), $"{relative} 가 남아 있다");

    [Fact]
    public void should_not_link_tilt_ui_text_into_test_project_when_tilt_feature_removed()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "tests", "GMaps.Ui.Tests", "GMaps.Ui.Tests.csproj"));
        Assert.DoesNotContain("TiltUiText.cs", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(TiltUiTextTests).Assembly.GetTypes(), t => t.Name == "TiltUiText");
    }
}
