using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 기상 모드 표시 사전(GOP API 8.0.15 대조 G15) — 서버 EnumWeatherMode 7종 중 HEAT_HAZE 가 빠져 "알 수 없음" 으로 보이던 것.
/// </summary>
public class WeatherModeHeatHazeTests
{
    [Fact]
    public void should_show_heat_haze_in_korean_when_server_sends_heat_haze()
        => Assert.Equal("아지랑이", DeviceEnumDisplay.FixedKorean(DeviceEnumDisplay.WeatherModes, "HEAT_HAZE"));

    [Theory]
    [InlineData("NORMAL")]
    [InlineData("FOG")]
    [InlineData("SEA_FOG")]
    [InlineData("YELLOW_DUST")]
    [InlineData("RAIN")]
    [InlineData("SNOW")]
    [InlineData("HEAT_HAZE")]
    public void should_name_every_server_weather_mode(string code)
        => Assert.NotEqual(DeviceEnumDisplay.UnknownValue, DeviceEnumDisplay.FixedKorean(DeviceEnumDisplay.WeatherModes, code));
}
