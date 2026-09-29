using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 간격 표 · 한도 표(wiring-fence-view PRD v0.4 §1-C) — 펜스센서 현장 간격(2~4m · 0.5m 단위 · 기준 3m)과
/// 제어기 한 대의 기준 길이 · 대수(경고만 · 섞이면 알림만).
/// </summary>
public class WiringTablesTests
{
    private static EnumDeviceType[] Many(EnumDeviceType type, int count) => Enumerable.Repeat(type, count).ToArray();

    #region - Spacing table -
    [Fact]
    public void should_use_three_metres_for_fence_when_the_default_table_is_used()
    {
        // Arrange
        var table = WiringSpacingTable.Default;

        // Act · Assert
        Assert.Equal(3.0, table.FenceMetres);
        Assert.Equal(3.0, table.SpacingOf(EnumDeviceType.Fence));
        Assert.Equal(20.0, table.SpacingOf(EnumDeviceType.Multi));
        Assert.Equal(6.0, table.SpacingOf(EnumDeviceType.SmartSensor2));
        Assert.Equal(25.0, table.SpacingOf(EnumDeviceType.Underground));
        Assert.Equal(6.0, table.SpacingOf(EnumDeviceType.NONE));
    }

    [Fact]
    public void should_take_the_smaller_spacing_when_two_kinds_are_neighbours()
    {
        // Act · Assert — 펜스센서가 복합센서 사이를 채운다(FR-17)
        Assert.Equal(3.0, WiringSpacingTable.Default.GapBetween(EnumDeviceType.Multi, EnumDeviceType.Fence));
        Assert.Equal(6.0, WiringSpacingTable.Default.GapBetween(EnumDeviceType.SmartSensor2, EnumDeviceType.Underground));
    }

    [Theory]
    [InlineData(2.5, 2.5)]
    [InlineData(2.3, 2.5)]       // 0.5m 로 맞춘다
    [InlineData(2.2, 2.0)]
    [InlineData(3.75, 4.0)]
    [InlineData(1.0, 2.0)]       // 2m 아래는 2m
    [InlineData(9.0, 4.0)]       // 4m 위는 4m
    [InlineData(double.NaN, 3.0)]
    [InlineData(double.PositiveInfinity, 3.0)]
    public void should_clamp_and_snap_the_fence_spacing_when_changed(double metres, double expected)
        => Assert.Equal(expected, WiringSpacingTable.Default.WithFence(metres).FenceMetres);

    [Fact]
    public void should_offer_two_to_four_metres_by_half_when_listing_fence_choices()
        => Assert.Equal(new[] { 2.0, 2.5, 3.0, 3.5, 4.0 }, WiringSpacingTable.FenceChoices.ToArray());

    [Fact]
    public void should_keep_other_kinds_when_the_fence_spacing_changes()
    {
        // Act
        var table = WiringSpacingTable.Default.WithFence(2.5);

        // Assert
        Assert.Equal(2.5, table.SpacingOf(EnumDeviceType.Fence));
        Assert.Equal(20.0, table.SpacingOf(EnumDeviceType.Multi));
    }
    #endregion

    #region - Limit table -
    [Theory]
    [InlineData(new[] { EnumDeviceType.SmartSensor2 }, WiringFamily.Smart)]
    [InlineData(new[] { EnumDeviceType.Multi, EnumDeviceType.Fence, EnumDeviceType.Underground }, WiringFamily.Perimeter)]
    [InlineData(new[] { EnumDeviceType.SmartSensor2, EnumDeviceType.Fence }, WiringFamily.Mixed)]
    [InlineData(new[] { EnumDeviceType.NONE }, WiringFamily.None)]
    [InlineData(new EnumDeviceType[0], WiringFamily.None)]
    public void should_pick_the_family_when_sensor_types_are_given(EnumDeviceType[] types, WiringFamily expected)
        => Assert.Equal(expected, WiringLimitTable.FamilyOf(types));

    [Fact]
    public void should_not_warn_when_a_smart_ring_is_at_the_limit()
        => Assert.Empty(WiringLimitTable.Default.Warnings(Many(EnumDeviceType.SmartSensor2, 34), 200));

    [Fact]
    public void should_warn_when_a_smart_ring_has_more_than_34_sensors()
    {
        // Act
        var warnings = WiringLimitTable.Default.Warnings(Many(EnumDeviceType.SmartSensor2, 35), 150);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Contains("35대", warning);
        Assert.Contains("34대", warning);
    }

    [Fact]
    public void should_warn_when_a_smart_ring_is_longer_than_200_metres()
    {
        // Act
        var warnings = WiringLimitTable.Default.Warnings(Many(EnumDeviceType.SmartSensor2, 10), 201);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Contains("200m", warning);
    }

    [Fact]
    public void should_warn_when_a_perimeter_controller_has_more_than_25_multi_sensors()
    {
        // Act
        var warnings = WiringLimitTable.Default.Warnings(Many(EnumDeviceType.Multi, 26), 400);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Contains("복합센서가 26대", warning);
    }

    [Fact]
    public void should_warn_when_a_perimeter_controller_has_more_than_200_fence_sensors()
    {
        // Act
        var warnings = WiringLimitTable.Default.Warnings(Many(EnumDeviceType.Fence, 201), 480);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Contains("펜스센서가 201대", warning);
    }

    [Fact]
    public void should_warn_when_a_perimeter_chain_is_longer_than_500_metres()
    {
        // Act
        var warnings = WiringLimitTable.Default.Warnings(Many(EnumDeviceType.Fence, 10), 501);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Contains("500m", warning);
    }

    [Fact]
    public void should_not_warn_when_a_perimeter_controller_is_at_every_limit()
    {
        // Arrange
        var types = Many(EnumDeviceType.Multi, 25).Concat(Many(EnumDeviceType.Fence, 200)).ToArray();

        // Act · Assert
        Assert.Empty(WiringLimitTable.Default.Warnings(types, 500));
    }

    [Fact]
    public void should_not_warn_when_the_families_are_mixed()
    {
        // Arrange — 섞였을 때의 기준은 확인 중(O-11) — 숫자를 지어내지 않는다
        var types = Many(EnumDeviceType.SmartSensor2, 40).Concat(Many(EnumDeviceType.Fence, 300)).ToArray();

        // Act · Assert
        Assert.Empty(WiringLimitTable.Default.Warnings(types, 2000));
        Assert.Null(WiringLimitTable.Default.ReferenceLength(WiringFamily.Mixed));
        Assert.Equal(200, WiringLimitTable.Default.ReferenceLength(WiringFamily.Smart));
        Assert.Equal(500, WiringLimitTable.Default.ReferenceLength(WiringFamily.Perimeter));
    }
    #endregion
}
