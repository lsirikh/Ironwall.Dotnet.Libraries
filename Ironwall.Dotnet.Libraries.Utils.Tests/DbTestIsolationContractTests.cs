using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// DB 시험이 라이브 로컬 DB(<c>monitor_db</c> — GIS 지도 심볼 · 결선 배치)를 건드리지 않는다는 계약을 글자로 지킨다.
/// </summary>
/// <remarks>
/// 2026-10-02 실측: 전체 헤드리스 회차에서 <c>Accounts.Db</c> 의 시험 픽스처가 <c>DbDatabase = "monitor_db"</c> 로 붙어
/// <c>DisposeAsync</c> 의 <c>DROP DATABASE</c> 로 지도 DB 를 통째로 지웠다(심볼 102개 · 결선 배치 소실).
/// 게다가 <c>IAsyncLifetime</c> 메서드에 <c>[Fact]</c> 가 붙어 정리 메서드가 "시험"으로 따로 돌았다(testing-patterns I-01).
/// 이 시험은 <c>*\Tests\*.cs</c> 의 DB 이름과 라이프사이클 어트리뷰트를 훑는다 — DB 없이 돈다.
/// </remarks>
public class DbTestIsolationContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static IEnumerable<string> TestSources()
        => Directory.EnumerateFiles(RepoRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void should_not_point_any_db_test_at_live_monitor_db_when_fixtures_declare_a_database()
    {
        var live = new Regex(@"^\s*DbDatabase\s*=\s*""monitor_db""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var offenders = TestSources().Where(f => live.IsMatch(File.ReadAllText(f))).Select(f => Path.GetRelativePath(RepoRoot(), f)).ToList();
        Assert.True(offenders.Count == 0, "라이브 지도 DB(monitor_db)를 쓰는 DB 시험: " + string.Join(", ", offenders));
    }

    [Fact]
    public void should_not_mark_async_lifetime_methods_as_facts_when_db_fixtures_are_declared()
    {
        var lifecycleFact = new Regex(@"\[Fact[^\]]*\]\s*(?://[^\n]*\n\s*)*public\s+async\s+Task\s+(InitializeAsync|DisposeAsync)\s*\(", RegexOptions.Multiline);
        var offenders = TestSources().Where(f => lifecycleFact.IsMatch(File.ReadAllText(f))).Select(f => Path.GetRelativePath(RepoRoot(), f)).ToList();
        Assert.True(offenders.Count == 0, "IAsyncLifetime 메서드에 [Fact]: " + string.Join(", ", offenders));
    }
}
