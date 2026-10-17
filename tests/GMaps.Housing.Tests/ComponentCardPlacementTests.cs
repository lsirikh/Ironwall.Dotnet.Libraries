using System.Windows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 조립 카드(L3) 자리 · 상세 보기 부품 탭 규칙 — component-display-unify FR-05 · FR-06 · FR-08(순수 함수).
/// </summary>
public class ComponentCardPlacementTests
{
    private static readonly Size Card = new(320, 200);
    private static readonly Size Viewport = new(1200, 800);

    [Fact]
    public void should_place_card_right_of_icon_and_center_vertically_when_there_is_room()
    {
        var p = ComponentCardPlacement.Place(new Point(400, 400), 20, Card, Viewport);
        Assert.Equal(400 + 20 + ComponentCardPlacement.Gap, p.X);
        Assert.Equal(300, p.Y);
    }

    [Fact]
    public void should_flip_card_to_left_when_right_edge_would_overflow()
    {
        var p = ComponentCardPlacement.Place(new Point(1100, 400), 20, Card, Viewport);
        Assert.Equal(1100 - 20 - ComponentCardPlacement.Gap - Card.Width, p.X);
    }

    [Theory]
    [InlineData(10, 8)]       // 위로 넘치면 위 여백에 붙인다
    [InlineData(790, 592)]    // 아래로 넘치면 아래 여백에 붙인다(800 − 8 − 200)
    public void should_clamp_card_inside_map_vertically_when_icon_is_near_edge(double iconY, double expectedTop)
        => Assert.Equal(expectedTop, ComponentCardPlacement.Place(new Point(400, iconY), 20, Card, Viewport).Y);

    [Fact]
    public void should_keep_card_inside_map_when_neither_side_fits()
    {
        var p = ComponentCardPlacement.Place(new Point(200, 400), 20, Card, new Size(400, 800));
        Assert.InRange(p.X, ComponentCardPlacement.Margin, 400 - ComponentCardPlacement.Margin - Card.Width);
    }

    [Fact]
    public void should_add_components_tab_before_symbol_only_when_device_has_component_axes()
    {
        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Components, SymbolDetailTab.Symbol },
            SymbolDetailRules.VisibleTabs(EnumDeviceType.IpCamera, hasComponentAxes: true));
        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Symbol },
            SymbolDetailRules.VisibleTabs(EnumDeviceType.IpCamera, hasComponentAxes: false));   // 6.3 무회귀
        Assert.False(SymbolDetailRules.IsTabEnabled(SymbolDetailTab.Components, hasDevice: false));
    }
}
