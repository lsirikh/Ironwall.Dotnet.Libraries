using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>3D 나가기(2026-10-18) — 영역 전체 시점(home) 메시지. 옛 줌 이탈 규칙(ShouldLeave3D · ExitCamera(bool,…)) 시험은 그 코드와 함께 지웠다(2026-10-19 사용자 결정).</summary>
public class Map3DExitTests
{
    [Fact]
    public void should_send_home_type_with_bearing_pitch_animate_when_asking_whole_area_view()
    {
        var o = JObject.Parse(Map3DMath.HomeMessageJson(30.0, 60.0, animate: true));
        Assert.Equal("home", o.Value<string>("type"));
        Assert.Equal(30.0, o.Value<double>("bearing"));
        Assert.Equal(60.0, o.Value<double>("pitch"));
        Assert.True(o.Value<bool>("animate"));
    }
}
