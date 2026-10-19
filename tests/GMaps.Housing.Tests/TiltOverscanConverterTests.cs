using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 기울이기 제거(2026-10-19, 사용자 요청) — 예전엔 MainMap Height/Margin MultiBinding 컨버터(TiltOverscanConverter, map-tilt-25d FR-04)의
/// 값 변환을 검증했다. 그 바인딩과 함께 컨버터를 지웠으므로, 지금은 형식이 GMaps.Ui 어셈블리에 남아 있지 않음을 단언한다
/// (오버스캔 수식 TiltOverscanMath 는 팝업 좌표 · 보이는 범위가 쓰므로 남아 있고 GMaps.Ui.Tests/TiltOverscanMathTests 가 검증한다).
/// </summary>
public class TiltOverscanConverterTests
{
    [Fact]
    public void should_not_ship_tilt_overscan_converter_when_tilt_removed()
    {
        var asm = typeof(GMapCustomControl).Assembly;
        Assert.Null(asm.GetType("Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.TiltOverscanConverter"));
        Assert.NotNull(asm.GetType("Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.TiltOverscanMath"));
    }
}
