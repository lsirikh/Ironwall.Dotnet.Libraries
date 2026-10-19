using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 「건물 높이」 판 ±1층 · ↑/↓ 키 경계(영역 C 전수 시나리오 C-56 · C-59, 2026-10-19) — 순수 함수 <see cref="Map3DBuilding"/>.
/// 1층(층고)보다 낮은 건물에서 −1층이 높이를 올리지 않는지, 200층 상한을 넘는 높은 건물에서 +1층이 높이를 내리지 않는지 본다.
/// </summary>
public class Map3DBuildingStepEdgeTests
{
    // ── C-59(D1) 1층보다 낮은 건물 −1층 · ↓ ──

    [Theory]
    [InlineData(2.0, 3.0)]
    [InlineData(1.2, 3.0)]
    [InlineData(1.0, 3.0)]
    [InlineData(2.9, 3.0)]
    [InlineData(3.9, 4.0)]
    [InlineData(1.5, 2.0)]
    public void should_not_raise_height_when_minus_one_floor_on_building_lower_than_one_floor(double height, double floorHeight)
    {
        Assert.Equal(height, Map3DBuilding.StepFloors(height, floorHeight, -1));
        Assert.Equal(height, Map3DBuilding.KeyStep(height, floorHeight, up: false, shift: false));
    }

    [Theory]
    [InlineData(4.4, 3.0, 3.0)]     // 1층(반올림) − 1 = 1층 → 3.0 m(지금보다 낮다 — 내림 그대로)
    [InlineData(12.0, 3.0, 9.0)]
    [InlineData(3.0, 3.0, 3.0)]     // 1층에서 −1 = 그대로
    public void should_keep_lowering_by_floor_when_result_is_not_higher(double height, double floorHeight, double expected)
        => Assert.Equal(expected, Map3DBuilding.StepFloors(height, floorHeight, -1));

    // ── C-59 짝: 200층 상한을 넘는 높은 건물 +1층 · ↑ ──

    [Theory]
    [InlineData(500.0, 2.0)]        // 250층 → 상한 200층 → +1 도 200층 = 400 m(내려가면 안 된다)
    [InlineData(600.0, 2.0)]
    [InlineData(450.0, 2.0)]
    public void should_not_lower_height_when_plus_one_floor_on_building_above_floor_limit(double height, double floorHeight)
    {
        Assert.True(Map3DBuilding.StepFloors(height, floorHeight, +1) >= height, $"+1층이 높이를 내렸다: {height} → {Map3DBuilding.StepFloors(height, floorHeight, +1)}");
        Assert.True(Map3DBuilding.KeyStep(height, floorHeight, up: true, shift: false) >= height, "↑ 가 높이를 내렸다");
    }

    // ── C-56 층고 10 × 200층 = 2000 m → 600 m 로 안내 없이 자름(지금 동작 기록 — 결정 필요) ──

    [Fact]
    public void should_clamp_to_600_meters_without_error_when_floors_times_floor_height_exceeds_max()
    {
        Assert.True(Map3DBuilding.TryParseFloors("200", out var floors, out var error));
        Assert.Null(error);
        Assert.Equal(600.0, Map3DBuilding.HeightFromFloors(floors, 10.0));
        Assert.Equal(60, Map3DBuilding.FloorsFromHeight(600.0, 10.0));   // 넣은 200층이 아니라 60층으로 보인다
    }
}
