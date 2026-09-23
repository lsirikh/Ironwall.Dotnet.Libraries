using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : DeviceEnumDisplay — 장비 콘솔 enum·코드값 한글 표시 단일 정본 회귀 가드
                  (device-console enum-korean-consistency)
   Created By   : GHLee
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// raw enum 이름(<c>ACTIVATED</c>·<c>IpCamera</c>·<c>Fence</c> …)이 화면에 새지 않는지 —
/// 매핑이 있으면 한글로, 없으면 원문을 그대로 보존하는지를 검증한다.
/// </summary>
public class DeviceEnumDisplayTests
{
    [Theory]
    [InlineData(EnumDeviceStatus.ACTIVATED, "운영")]
    [InlineData(EnumDeviceStatus.ERROR, "오류")]
    [InlineData(EnumDeviceStatus.DEACTIVATED, "중지")]
    public void should_map_korean_when_status_is_known(EnumDeviceStatus status, string korean)
        => Assert.Equal(korean, DeviceEnumDisplay.StatusKorean(status));

    [Fact]
    public void should_preserve_original_when_status_is_unknown()
    {
        var unknown = (EnumDeviceStatus)999;
        Assert.Equal(unknown.ToString(), DeviceEnumDisplay.StatusKorean(unknown));
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "제어기")]
    [InlineData(EnumDeviceCategory.Sensor, "센서")]
    [InlineData(EnumDeviceCategory.Camera, "카메라")]
    [InlineData(EnumDeviceCategory.Speaker, "스피커")]
    [InlineData(EnumDeviceCategory.Enclosure, "함체")]
    [InlineData(EnumDeviceCategory.Lamp, "경광등")]
    [InlineData(EnumDeviceCategory.Gate, "통문")]
    public void should_map_korean_when_category_is_known(EnumDeviceCategory category, string korean)
        => Assert.Equal(korean, DeviceEnumDisplay.CategoryKorean(category));

    [Theory]
    [InlineData("OK", "정상")]
    [InlineData("ok", "정상")]        // 대소문자 무관
    [InlineData("DEGRADED", "주의")]
    [InlineData("FAULT", "고장")]
    [InlineData("UNKNOWN", "미확인")]
    public void should_map_korean_when_component_health_is_known(string health, string korean)
        => Assert.Equal(korean, DeviceEnumDisplay.ComponentHealthKorean(health));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void should_return_unknown_text_when_component_health_is_empty(string? health)
        => Assert.Equal("미확인", DeviceEnumDisplay.ComponentHealthKorean(health));

    [Fact]
    public void should_preserve_original_when_component_health_is_not_in_vocabulary()
        => Assert.Equal("WEIRD", DeviceEnumDisplay.ComponentHealthKorean("WEIRD"));

    [Fact]
    public void should_build_bilingual_display_when_korean_differs_from_code()
        => Assert.Equal("운영 (ACTIVATED)", DeviceEnumDisplay.Bilingual("운영", "ACTIVATED"));

    [Fact]
    public void should_hide_code_when_korean_equals_code()
        => Assert.Equal("전체", DeviceEnumDisplay.Bilingual("전체", "전체"));

    [Fact]
    public void should_map_known_enum_types_through_ui_korean_map()
    {
        Assert.Equal("카메라", DeviceEnumDisplay.KoreanOf(EnumDeviceType.IpCamera));
        Assert.Equal("PTZ", DeviceEnumDisplay.KoreanOf(EnumCameraType.PTZ));
        Assert.Equal("ONVIF", DeviceEnumDisplay.KoreanOf(EnumCameraMode.ONVIF));
    }

    [Fact]
    public void should_build_enum_bilingual_display_for_clr_enum_combo()
        => Assert.Equal("카메라 (IpCamera)", DeviceEnumDisplay.EnumBilingual(EnumDeviceType.IpCamera));

    [Theory]
    [InlineData("Fence", "펜스센서 (Fence)")]
    [InlineData("PIR", "PIR센서 (PIR)")]
    [InlineData("Underground", "지중센서 (Underground)")]
    [InlineData("fence", "펜스센서 (fence)")]   // 대소문자 무관 매칭, 원문 대소문자는 보존
    public void should_map_legacy_sensor_type_code_to_korean(string code, string expected)
        => Assert.Equal(expected, DeviceEnumDisplay.SensorTypeBilingual(code));

    /// <summary>v7.0+ 카탈로그 코드처럼 enum 이 모르는 값은 지어내지 않고 원문 그대로 보인다.</summary>
    [Fact]
    public void should_preserve_unknown_sensor_type_code_verbatim()
        => Assert.Equal("DOOR_SENSOR_X1", DeviceEnumDisplay.SensorTypeBilingual("DOOR_SENSOR_X1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void should_return_empty_when_sensor_type_code_is_empty(string? code)
        => Assert.Equal(string.Empty, DeviceEnumDisplay.SensorTypeBilingual(code));

    [Theory]
    [InlineData("펜스센서 (Fence)", "Fence")]
    [InlineData("PIR센서 (PIR)", "PIR")]
    [InlineData("전체 (전체)", "전체")]
    public void should_extract_code_when_display_is_bilingual(string display, string expectedCode)
        => Assert.Equal(expectedCode, DeviceEnumDisplay.ExtractSensorTypeCode(display));

    /// <summary>사람이 병기 형식 없이 코드를 직접 타이핑했으면(카탈로그 코드 등) 그 글자를 그대로 코드로 받는다.</summary>
    [Theory]
    [InlineData("DOOR_SENSOR_X1")]
    [InlineData("Fence")]
    public void should_treat_plain_input_as_code_when_display_has_no_bilingual_suffix(string typed)
        => Assert.Equal(typed, DeviceEnumDisplay.ExtractSensorTypeCode(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void should_return_empty_when_extracting_code_from_empty_display(string? display)
        => Assert.Equal(string.Empty, DeviceEnumDisplay.ExtractSensorTypeCode(display));

    /// <summary>병기 문자열을 그대로 왕복해도 코드가 보존된다 — Bilingual → ExtractSensorTypeCode 는 항등이어야 한다.</summary>
    [Theory]
    [InlineData("Fence")]
    [InlineData("PIR")]
    [InlineData("Underground")]
    [InlineData("DOOR_SENSOR_X1")]
    public void should_round_trip_code_through_bilingual_and_extract(string code)
        => Assert.Equal(code, DeviceEnumDisplay.ExtractSensorTypeCode(DeviceEnumDisplay.SensorTypeBilingual(code)));
}
