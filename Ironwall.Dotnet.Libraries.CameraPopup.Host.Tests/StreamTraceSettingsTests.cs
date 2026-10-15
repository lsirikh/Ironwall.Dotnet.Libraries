using Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>스트림 단계 계측 플래그(IRONWALL_STREAM_TRACE · appsettings "CameraHostStreamTrace")의 해석 — 기본 꺼짐.</summary>
public class StreamTraceSettingsTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData(" ON ", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void should_turn_on_only_for_truthy_values_when_environment_variable_is_read(string? value, bool expected)
        => Assert.Equal(expected, StreamTrace.ReadEnabled(value));

    [Theory]
    [InlineData("{ \"CameraHostStreamTrace\": true }", true)]
    [InlineData("{ \"AppSettings\": { \"CameraHostStreamTrace\": true } }", true)]
    [InlineData("{ \"AppSettings\": { \"CameraHostStreamTrace\": \"1\" } }", true)]
    [InlineData("{ \"AppSettings\": { \"CameraHostStreamTrace\": false } }", false)]
    [InlineData("{ \"AppSettings\": { \"CameraPopup\": { \"Mode\": \"Self\" } } }", false)]
    [InlineData("{ // 주석\n \"CameraHostStreamTrace\": true, }", true)]
    [InlineData("not json", false)]
    [InlineData("[]", false)]
    public void should_read_flag_from_root_or_app_settings_when_appsettings_json_is_parsed(string json, bool expected)
        => Assert.Equal(expected, StreamTrace.ReadSettingsJson(json));

    [Fact]
    public void should_stay_off_and_register_nothing_when_flag_is_not_set()
    {
        // 시험 프로세스는 Start 를 부르지 않는다 — 생산자 등록은 null(계측 비용 없음)이어야 한다.
        Assert.False(StreamTrace.IsOn);
        Assert.Null(StreamTrace.Register("x", () => null));
    }
}
