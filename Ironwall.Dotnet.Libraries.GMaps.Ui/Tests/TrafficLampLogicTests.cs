using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : 신호등 점등 규칙 SSOT 검증 (map-topbar-trafficlight FR-A — 상단바 이관판).
                  구 Events.Ui TrafficLightStateTests의 상태 매트릭스를 승계 —
                  미초기화 게이트 / green 불변식 / 단독·동시 점등 / 0 복귀.
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
public class TrafficLampLogicTests
{
    [Fact]
    public void should_keep_all_lamps_off_when_counts_not_ready()
    {
        // 미초기화(첫 EQM 집계 전) = 데이터 없음 — 건수와 무관하게 전체 소등, 초록 점등 금지
        Assert.False(TrafficLampLogic.FaultOn(false, 3));
        Assert.False(TrafficLampLogic.DetectionOn(false, 2));
        Assert.False(TrafficLampLogic.GreenOn(false, 0, 0));
    }

    [Fact]
    public void should_turn_on_green_only_when_ready_and_both_zero()
    {
        Assert.True(TrafficLampLogic.GreenOn(true, 0, 0));
        Assert.False(TrafficLampLogic.FaultOn(true, 0));
        Assert.False(TrafficLampLogic.DetectionOn(true, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void should_turn_on_fault_lamp_only_when_fault_positive_and_detection_zero(int fault)
    {
        Assert.True(TrafficLampLogic.FaultOn(true, fault));
        Assert.False(TrafficLampLogic.DetectionOn(true, 0));
        Assert.False(TrafficLampLogic.GreenOn(true, 0, fault));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void should_turn_on_detection_lamp_only_when_detection_positive_and_fault_zero(int detection)
    {
        Assert.True(TrafficLampLogic.DetectionOn(true, detection));
        Assert.False(TrafficLampLogic.FaultOn(true, 0));
        Assert.False(TrafficLampLogic.GreenOn(true, detection, 0));
    }

    [Fact]
    public void should_allow_simultaneous_lamps_when_both_counts_positive()
    {
        // 설비 상태등 의미론 — 동시 점등 허용, green 불변식으로 초록은 구조적 소등
        Assert.True(TrafficLampLogic.FaultOn(true, 3));
        Assert.True(TrafficLampLogic.DetectionOn(true, 2));
        Assert.False(TrafficLampLogic.GreenOn(true, 2, 3));
    }

    [Fact]
    public void should_turn_green_back_on_when_counts_return_to_zero()
    {
        Assert.False(TrafficLampLogic.GreenOn(true, 2, 3));
        Assert.True(TrafficLampLogic.GreenOn(true, 0, 0));
    }
}
