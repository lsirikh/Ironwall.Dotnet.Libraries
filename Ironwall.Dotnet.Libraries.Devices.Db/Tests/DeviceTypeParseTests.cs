using Ironwall.Dotnet.Libraries.Devices.Db.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Tests;

/// <summary>
/// FR-06 — DB 행의 문자열 → enum 변환이 미지 어휘에 죽지 않는다.
/// 순수 함수 검증이라 DB 가 필요 없다(<see cref="DeviceDbFixture"/> 미사용).
/// </summary>
public class DeviceTypeParseTests
{
    [Theory]
    [InlineData("SmartController")]
    [InlineData("SPEED_DOME")]
    [InlineData("Sliding")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void should_fallback_to_none_when_device_type_text_is_unknown(string? text)
    {
        var result = DeviceTypeText.ParseTypeOrNone(text);

        Assert.Equal(EnumDeviceType.NONE, result);
    }

    [Theory]
    [InlineData("Controller", EnumDeviceType.Controller)]
    [InlineData("controller", EnumDeviceType.Controller)]
    [InlineData("GATE", EnumDeviceType.Gate)]
    [InlineData(" Lamp ", EnumDeviceType.Lamp)]
    public void should_parse_known_device_type_when_case_or_padding_differs(string text, EnumDeviceType expected)
    {
        var result = DeviceTypeText.ParseTypeOrNone(text);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("-1")]
    public void should_fallback_to_none_when_device_type_text_is_undefined_number(string text)
    {
        // Enum.TryParse 는 정의되지 않은 정수 문자열도 성공으로 돌려준다 — 그 값이 모델로 새면 안 된다.
        var result = DeviceTypeText.ParseTypeOrNone(text);

        Assert.Equal(EnumDeviceType.NONE, result);
    }

    [Theory]
    [InlineData("UNKNOWN_STATE")]
    [InlineData("")]
    [InlineData(null)]
    public void should_fallback_to_deactivated_when_status_text_is_unknown(string? text)
    {
        var result = DeviceTypeText.ParseStatusOrDeactivated(text);

        Assert.Equal(EnumDeviceStatus.DEACTIVATED, result);
    }

    [Theory]
    [InlineData("ACTIVATED", EnumDeviceStatus.ACTIVATED)]
    [InlineData("error", EnumDeviceStatus.ERROR)]
    public void should_parse_known_status_when_case_differs(string text, EnumDeviceStatus expected)
    {
        var result = DeviceTypeText.ParseStatusOrDeactivated(text);

        Assert.Equal(expected, result);
    }
}
