using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>map-tilt-25d PRD FR-13 — Tier 0 강등의 순수 판정(분석 D4: 기존 Tier 코드는 전량 주석이라 신규 구현).
/// <c>RenderCapability.Tier</c> 원시값은 상위 워드가 tier 레벨(0/1/2), 하위 워드는 예약 비트.
/// WPF 래퍼(<c>RenderTierProbe.Wpf.cs</c>)는 헤드리스에서 링크하지 않는다 — V-04(RDP 실측)는 실기 항목.</summary>
public class RenderTierProbeTests
{
    [Theory]
    [InlineData(0x00000, true)]    // SIM-C008: Tier0(RDP) → φ 강제 0 사유
    [InlineData(0x10000, false)]   // SIM-T1048: t=1 부분 가속 → 강등 없음
    [InlineData(0x20000, false)]   // 전체 하드웨어 가속
    public void should_detect_software_tier_when_high_word_is_zero(int renderCapabilityTier, bool expected)
        => Assert.Equal(expected, RenderTierProbe.IsSoftwareTier(renderCapabilityTier));

    [Theory]
    [InlineData(0x0000FFFF, true)]   // 하위 워드(예약 비트)만 차 있어도 tier 레벨은 0
    [InlineData(0x0001ABCD, false)]  // 하위 워드 무시하고 상위 워드 1
    public void should_ignore_low_word_when_judging_tier(int renderCapabilityTier, bool expected)
        => Assert.Equal(expected, RenderTierProbe.IsSoftwareTier(renderCapabilityTier));

    [Theory]
    [InlineData(0x00000, 0)]
    [InlineData(0x10000, 1)]
    [InlineData(0x20000, 2)]
    public void should_extract_tier_level_when_raw_value_given(int renderCapabilityTier, int expected)
        => Assert.Equal(expected, RenderTierProbe.TierLevel(renderCapabilityTier));

    [Fact]
    public void should_format_log_with_hex_tier_and_software_flag_when_tier0()
        // VER-04 실기 로그 grep 키 "[Tilt] tier=" — 형식 고정
        => Assert.Equal("[Tilt] tier=0x0 software=True", RenderTierProbe.FormatLog(0x00000));

    [Fact]
    public void should_format_log_with_hex_tier_and_software_flag_when_tier2()
        => Assert.Equal("[Tilt] tier=0x20000 software=False", RenderTierProbe.FormatLog(0x20000));
}
