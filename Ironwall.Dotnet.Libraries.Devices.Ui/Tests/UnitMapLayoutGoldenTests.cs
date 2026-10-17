using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-67 (FR-06 · FR-07 · NFR-11 · ISSUE-8 · 48) — 자동 배치 결과를 <c>layout_version = 1</c> 의 골든 파일에 못 박는다.
/// 공유 배치 Δ 는 자동 위치로부터의 차이라, 자동 배치가 한 칸이라도 바뀌면 모든 운영자의 Δ 가 엉뚱한 곳을 가리킨다(D-6).
/// 시나리오: SIM-L050 · SIM-L057~058 · SIM-P048 · SIM-L001~049(회귀).
/// </summary>
/// <remarks>
/// 골든은 <c>Tests/Golden/UnitMapLayout.v1.golden.json</c> — 시험이 스스로 다시 쓰는 경로는 없다.
/// 이 파일을 바꾸는 것은 <see cref="UnitMapLayout.LayoutVersion"/> 을 올리고 PRD §6 판 올림 절차를 따르는 일이다.
/// </remarks>
public class UnitMapLayoutGoldenTests
{
    private const string GoldenRelative = "Tests/Golden/UnitMapLayout.v1.golden.json";
    private const string ChangedMessage = "자동 배치 결과가 바뀌었습니다 — layout_version 을 올리고 PRD §6 판 올림 절차를 따르세요";

    /// <summary>시험 어셈블리 위치에서 위로 올라가며 프로젝트 폴더의 골든을 찾는다. 못 찾으면 Skip 이 아니라 실패.</summary>
    private static string GoldenPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, GoldenRelative);
            if (File.Exists(candidate)) return candidate;
            var nested = Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.Devices.Ui", GoldenRelative);
            if (File.Exists(nested)) return nested;
        }
        throw new Xunit.Sdk.XunitException($"골든 파일({GoldenRelative})을 찾지 못했습니다 — 골든 없이 배치 판을 지킬 수 없습니다.");
    }

    private static (int LayoutVersion, Dictionary<string, List<(int Id, double X, double Y)>> Trees) ReadGolden()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(GoldenPath()));
        var root = doc.RootElement;
        var trees = new Dictionary<string, List<(int, double, double)>>();
        foreach (var tree in root.GetProperty("trees").EnumerateObject())
            trees[tree.Name] = tree.Value.EnumerateArray()
                                   .Select(e => (e.GetProperty("id").GetInt32(), e.GetProperty("x").GetDouble(), e.GetProperty("y").GetDouble()))
                                   .ToList();
        return (root.GetProperty("layout_version").GetInt32(), trees);
    }

    [Theory]
    [InlineData("standard200")]
    [InlineData("multiRootWithOrphan")]
    public void should_match_golden_coordinates_when_layout_version_1(string treeName)
    {
        // Arrange
        var (layoutVersion, trees) = ReadGolden();
        var fixture = treeName == "standard200" ? UnitMapTestData.Standard200() : UnitMapTestData.MultiRootWithOrphan();

        // Act
        var positions = UnitMapLayout.Compute(fixture.Tree).Positions;

        // Assert — 판 번호 · 부대 수 · 모든 좌표(허용 오차 0)
        Assert.Equal(1, UnitMapLayout.LayoutVersion);
        Assert.True(layoutVersion == UnitMapLayout.LayoutVersion, ChangedMessage);
        var golden = trees[treeName];
        Assert.True(golden.Count == positions.Count, $"{ChangedMessage} (부대 수 {golden.Count} ≠ {positions.Count})");
        foreach (var (id, x, y) in golden)
        {
            Assert.True(positions.TryGetValue(id, out var actual), $"{ChangedMessage} (부대 {id} 없음)");
            Assert.True(actual.X == x && actual.Y == y, $"{ChangedMessage} (부대 {id}: 골든 {x},{y} ≠ 지금 {actual.X},{actual.Y})");
        }
    }

    [Fact]
    public void should_order_roots_like_unit_tree_when_multiple_roots()
    {
        // ISSUE-48 — 뿌리 왼→오 순서 = 편제 트리(UnitTreeBuilder: 제대 순위 → 코드)의 뿌리 순서. 규칙 사본이 아니라 트리 결과와 대조한다.
        var fixture = UnitMapTestData.MultiRootWithOrphan();
        var positions = UnitMapLayout.Compute(fixture.Tree).Positions;

        var treeRoots = fixture.Tree.Ordered.Where(n => n.ParentId is null).Select(n => n.Id).ToList();
        var screenRoots = treeRoots.OrderBy(id => positions[id].X).ToList();

        Assert.Equal(treeRoots, screenRoots);
        Assert.Equal(new[] { "d01", "c09", "unit001" }, treeRoots.Select(id => fixture.Tree.Find(id)!.Code));
    }
}
