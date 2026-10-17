using Ironwall.Dotnet.Libraries.SystemResources.Models;
using Ironwall.Dotnet.Libraries.SystemResources.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.SystemResources.Tests;

/// <summary>히스테리시스 레벨 매핑(경계 깜빡임 방지) 검증.</summary>
public class HysteresisLevelTests
{
    [Fact]
    public void should_stay_normal_until_62()
    {
        var m = new HysteresisLevelMapper();
        Assert.Equal(ResourceLevel.Normal, m.Map(61));
        Assert.Equal(ResourceLevel.Warning, m.Map(62));
    }

    [Fact]
    public void should_return_to_normal_at_57()
    {
        var m = new HysteresisLevelMapper();
        m.Map(70);                                                  // → Warning
        Assert.Equal(ResourceLevel.Warning, m.Map(58));            // 57 초과 → 유지
        Assert.Equal(ResourceLevel.Normal, m.Map(57));            // 57 이하 → 복귀
    }

    [Fact]
    public void should_enter_critical_at_87_and_return_at_82()
    {
        var m = new HysteresisLevelMapper();
        m.Map(70);                                                  // → Warning
        Assert.Equal(ResourceLevel.Critical, m.Map(87));
        Assert.Equal(ResourceLevel.Critical, m.Map(83));          // 82 초과 → 유지
        Assert.Equal(ResourceLevel.Warning, m.Map(82));           // 82 이하 → 복귀
    }

    [Fact]
    public void should_not_flicker_between_59_and_61()
    {
        var m = new HysteresisLevelMapper();
        Assert.Equal(ResourceLevel.Normal, m.Map(59));
        Assert.Equal(ResourceLevel.Normal, m.Map(61));
        Assert.Equal(ResourceLevel.Normal, m.Map(59));
    }

    [Fact]
    public void should_jump_normal_to_critical_directly()
    {
        var m = new HysteresisLevelMapper();
        Assert.Equal(ResourceLevel.Critical, m.Map(95));
    }

    [Fact]
    public void should_reset_to_normal()
    {
        var m = new HysteresisLevelMapper();
        m.Map(95);                                                  // → Critical
        m.Reset();
        Assert.Equal(ResourceLevel.Normal, m.Map(61));            // 리셋 후 Normal 기준
    }
}
