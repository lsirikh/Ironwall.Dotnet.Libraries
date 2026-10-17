using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD FR-14(정책 (i): 피치 35° 고정 + 지도 틸트 승계) — FenceMath.cs 주석이 약속한
/// <c>HousingMath.Pitch == FenceMath.PitchDeg</c> 동일성 단언. 두 값이 갈라지면 하우징(마커 박스)과 철망(Canvas 폴리라인)의
/// 지면 단축비(sin35≈0.574)가 달라져 링·기둥 접지가 어긋난다. 순수 상수 비교라 STA 불요.
/// 이 프로젝트에 둔 이유: HousingMath 는 HousingJoint(HousingMeshBuilder.cs, Media3D 의존)를 참조해 GMaps.Ui.Tests(net8.0) 에 링크 불가.
/// </summary>
public class HousingFencePitchTests
{
    [Fact]
    public void should_share_pitch_between_housing_and_fence()
    {
        // SIM-C003/C010 — 피치 SSOT: 두 상수가 같아야 틸트 승계 후에도 링:지도지면 상대비(0.574)가 하우징·철망에서 동일하게 유지된다.
        Assert.Equal(35.0, HousingMath.Pitch);
        Assert.Equal(HousingMath.Pitch, FenceMath.PitchDeg);
        Assert.Equal(HousingMath.SinPitch, FenceMath.SinPitch, 12);
        Assert.Equal(HousingMath.CosPitch, FenceMath.CosPitch, 12);
        Assert.Equal(0.5736, HousingMath.SinPitch, 4);   // 링:지도지면 상대비
        Assert.Equal(1.0 / HousingMath.SinPitch, FenceMath.GroundStretch, 12);
    }
}
