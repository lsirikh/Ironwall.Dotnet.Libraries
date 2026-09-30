using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>위치 = 순서 = 번호 · 번호 대역(fence-wiring-editor FR-09 · FR-10 · NFR-01).</summary>
public class NumberingMathTests
{
    [Fact]
    public void should_number_seven_smart_and_five_fence_sensors_one_to_seven_and_101_to_105_when_the_tier4_preset_numbers_a_mixed_ring()
    {
        // Arrange — 4차: 판망 = 스마트, 윤형 = 펜스가 한 링에 섞였다(S F S S F F S F S S F S)
        var pattern = "SFSSFFSFSSFS";
        var chain = pattern.Select((c, i) => (Key: i + 1, Category: c == 'S' ? FenceSensorCategory.Smart : FenceSensorCategory.Fence)).ToList();

        // Act
        var numbers = NumberingMath.Assign(chain, NumberBandSet.Tier4);

        // Assert
        Assert.Equal(Enumerable.Range(1, 7), chain.Where(c => c.Category == FenceSensorCategory.Smart).Select(c => numbers[c.Key]));
        Assert.Equal(Enumerable.Range(101, 5), chain.Where(c => c.Category == FenceSensorCategory.Fence).Select(c => numbers[c.Key]));
    }

    [Fact]
    public void should_block_saving_when_a_category_has_more_sensors_than_its_band()
    {
        // 4차 스마트 대역 1~99 에 스마트 100대
        var chain = Enumerable.Range(1, 100).Select(k => (Key: k, Category: FenceSensorCategory.Smart)).ToList();
        var numbers = NumberingMath.Assign(chain, NumberBandSet.Tier4);
        var sensors = chain.Select(c => new NumberingSensor(c.Key, c.Category, numbers[c.Key], true, $"S{c.Key}")).ToList();

        var issues = NumberingMath.Validate(sensors, NumberBandSet.Tier4);

        var overflow = Assert.Single(issues, i => i.Kind == NumberingIssueKind.BandOverflow);
        Assert.True(overflow.BlocksSave);
        Assert.Equal(new[] { 100 }, overflow.Keys);                           // 넘친 센서
        Assert.Contains("99대까지", overflow.Message);
    }

    [Fact]
    public void should_block_saving_when_a_renumbered_sensor_collides_with_an_unplaced_sensor()
    {
        var bands = NumberBandSet.Tier3;
        var sensors = new List<NumberingSensor>
        {
            new(1, FenceSensorCategory.Smart, 1, true, "북측 1"),
            new(2, FenceSensorCategory.Smart, 2, true, "북측 2"),
            new(3, FenceSensorCategory.Smart, 2, false, "창고 예비"),              // 미배치 · 제 번호 그대로
        };

        var issues = NumberingMath.Validate(sensors, bands);

        var duplicate = Assert.Single(issues, i => i.Kind == NumberingIssueKind.Duplicate);
        Assert.True(duplicate.BlocksSave);
        Assert.Equal(new[] { 2, 3 }, duplicate.Keys);
    }

    [Fact]
    public void should_block_saving_when_a_number_goes_over_255()
    {
        var bands = new NumberBandSet(NumberBandSet.PRESET_CUSTOM, new[] { new NumberBand(FenceSensorCategory.Multi, 250, 300) });
        var chain = Enumerable.Range(1, 8).Select(k => (Key: k, Category: FenceSensorCategory.Multi)).ToList();
        var numbers = NumberingMath.Assign(chain, bands);
        var sensors = chain.Select(c => new NumberingSensor(c.Key, c.Category, numbers[c.Key], true, $"M{c.Key}")).ToList();

        var issues = NumberingMath.Validate(sensors, bands);

        var over = Assert.Single(issues, i => i.Kind == NumberingIssueKind.OverMax);
        Assert.True(over.BlocksSave);
        Assert.Equal(new[] { 7, 8 }, over.Keys);                                // 256 · 257
    }

    [Fact]
    public void should_only_warn_when_two_bands_overlap()
    {
        var bands = new NumberBandSet(NumberBandSet.PRESET_CUSTOM, new[]
        {
            new NumberBand(FenceSensorCategory.Smart, 1, 120),
            new NumberBand(FenceSensorCategory.Fence, 101, 199),
        });

        var issues = NumberingMath.Validate(new[] { new NumberingSensor(1, FenceSensorCategory.Smart, 1, true, "S") }, bands);

        var overlap = Assert.Single(issues);
        Assert.Equal(NumberingIssueKind.BandOverlap, overlap.Kind);
        Assert.False(overlap.BlocksSave);
    }

    [Fact]
    public void should_leave_numbers_of_a_category_without_a_band_untouched_and_say_so()
    {
        var chain = new[] { (Key: 1, Category: FenceSensorCategory.Smart), (Key: 2, Category: FenceSensorCategory.Underground) };

        var numbers = NumberingMath.Assign(chain, NumberBandSet.Tier4);
        var issues = NumberingMath.Validate(new[]
        {
            new NumberingSensor(1, FenceSensorCategory.Smart, numbers[1], true, "S"),
            new NumberingSensor(2, FenceSensorCategory.Underground, 501, true, "U"),
        }, NumberBandSet.Tier4);

        Assert.False(numbers.ContainsKey(2));
        var info = Assert.Single(issues);
        Assert.Equal(NumberingIssueKind.NoBand, info.Kind);
        Assert.False(info.BlocksSave);
        Assert.Contains(issues, i => i.Message.Contains("지진동"));
    }

    [Fact]
    public void should_do_nothing_when_no_band_set_is_chosen()
    {
        Assert.Empty(NumberingMath.Assign(new[] { (1, FenceSensorCategory.Smart) }, null));
        Assert.Empty(NumberingMath.Validate(new[] { new NumberingSensor(1, FenceSensorCategory.Smart, 1101, true, "S") }, null));
    }

    [Theory]
    [InlineData(EnumDeviceType.SmartSensor2, FenceSensorCategory.Smart)]
    [InlineData(EnumDeviceType.SmartMultisensor2, FenceSensorCategory.Smart)]
    [InlineData(EnumDeviceType.Fence, FenceSensorCategory.Fence)]
    [InlineData(EnumDeviceType.Multi, FenceSensorCategory.Multi)]
    [InlineData(EnumDeviceType.Underground, FenceSensorCategory.Underground)]
    [InlineData(EnumDeviceType.NONE, FenceSensorCategory.Other)]
    public void should_map_sensor_types_to_number_categories_when_categorised(EnumDeviceType type, FenceSensorCategory expected)
        => Assert.Equal(expected, NumberingMath.CategoryOf(type));

    [Fact]
    public void should_list_only_changed_numbers_in_order_when_the_change_table_is_built()
    {
        var changes = NumberingMath.Changes(new[] { (5, "북측 5구간", 105, 5), (6, "북측 6구간", 6, 6), (7, "북측 7구간", 3, 7) });

        Assert.Equal(new[] { "북측 5구간 105 → 5", "북측 7구간 3 → 7" }, changes.Select(c => c.Text));
    }

    [Fact]
    public void should_compare_band_sets_by_content_when_equal()
    {
        var copy = new NumberBandSet(NumberBandSet.PRESET_TIER4, NumberBandSet.Tier4.Bands.ToList());

        Assert.Equal(NumberBandSet.Tier4, copy);
        Assert.NotEqual(NumberBandSet.Tier4, NumberBandSet.Tier4.With(FenceSensorCategory.Fence, new NumberBand(FenceSensorCategory.Fence, 101, 150)));
    }
}
