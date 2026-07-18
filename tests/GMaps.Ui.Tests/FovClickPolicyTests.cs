using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : FovClickPolicy 회귀 테스트 — 카메라 클릭 FOV 켜기 정책
                  (PRD: GMap_PidsCamera_FOV_Toggle_Persistence FR-03)
   Created By   : GHLee
   Created On   : 7/19/2026
****************************************************************************/
public class FovClickPolicyTests
{
    [Fact]
    public void should_turn_on_when_fov_currently_off()
    {
        // Arrange / Act / Assert
        Assert.True(FovClickPolicy.ShouldTurnOnFov(false));
    }

    [Fact]
    public void should_not_turn_on_when_fov_already_on()
    {
        // 이미 켜져 있으면 클릭은 무동작 — 반복 클릭 desync 방지
        Assert.False(FovClickPolicy.ShouldTurnOnFov(true));
    }
}
