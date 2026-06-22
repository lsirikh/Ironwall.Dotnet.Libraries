using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// Geolocation(Heading/Altitude) 모델↔DTO 왕복 매핑 + DTO 직렬화 회귀 가드.
/// PRD DevicePropertyPanel_Layout_Redesign v1.1 P2 — BLOCKER-1(저장 시 Heading 미전송) 핫픽스 보호.
/// </summary>
public class GeolocationMappingTests
{
    private static ControllerDeviceModel MakeModel(double lat, double lng, double? heading, double? altitude, string? location = "테스트위치")
        => new()
        {
            DeviceNumber = 1,
            DeviceName = "제어기_01",
            Version = "v1.0",
            IpAddress = "192.168.0.10",
            Port = 9000,
            Location = location,
            Latitude = lat,
            Longitude = lng,
            Heading = heading,
            Altitude = altitude,
            IsEnable = true,
        };

    [Fact]
    public void should_preserve_heading_and_altitude_when_round_tripped()
    {
        var model = MakeModel(37.5, 127.0, heading: 45.0, altitude: 100.0);

        var result = model.ToControllerDeviceDto().ToControllerDeviceModel();

        Assert.Equal(45.0, result.Heading);
        Assert.Equal(100.0, result.Altitude);
        Assert.Equal(37.5, result.Latitude);
        Assert.Equal(127.0, result.Longitude);
    }

    [Fact]
    public void should_preserve_null_heading_and_altitude_when_round_tripped()
    {
        var model = MakeModel(37.5, 127.0, heading: null, altitude: null);

        var result = model.ToControllerDeviceDto().ToControllerDeviceModel();

        Assert.Null(result.Heading);
        Assert.Null(result.Altitude);
    }

    [Fact]
    public void should_write_heading_and_altitude_into_dto_geolocation()
    {
        // BLOCKER-1: 이전엔 MapGeolocationToDto가 Heading/Altitude를 쓰지 않아 저장해도 미반영
        var model = MakeModel(37.5, 127.0, heading: 45.0, altitude: 100.0);

        var dto = model.ToControllerDeviceDto();

        Assert.NotNull(dto.Geolocation);
        Assert.Equal(45.0, dto.Geolocation!.Heading);
        Assert.Equal(100.0, dto.Geolocation.Altitude);
    }

    [Fact]
    public void should_create_geolocation_when_only_bearing_set_at_zero_coords()
    {
        // 가드 확장: Lat/Lng=0 + Location=null 이어도 Heading 있으면 geolocation 생성
        var model = MakeModel(0, 0, heading: 90.0, altitude: null, location: null);

        var dto = model.ToControllerDeviceDto();

        Assert.NotNull(dto.Geolocation);
        Assert.Equal(90.0, dto.Geolocation!.Heading);
    }

    [Fact]
    public void should_not_create_geolocation_when_no_location_no_bearing_no_altitude()
    {
        var model = MakeModel(0, 0, heading: null, altitude: null, location: null);

        var dto = model.ToControllerDeviceDto();

        Assert.Null(dto.Geolocation);
    }

    [Fact]
    public void should_omit_heading_and_altitude_from_json_when_null()
    {
        var json = JsonConvert.SerializeObject(new GeolocationDto { Latitude = 1, Longitude = 2, Heading = null, Altitude = null });

        Assert.DoesNotContain("heading", json);
        Assert.DoesNotContain("altitude", json);
    }

    [Fact]
    public void should_include_heading_and_altitude_in_json_when_set()
    {
        var json = JsonConvert.SerializeObject(new GeolocationDto { Latitude = 1, Longitude = 2, Heading = 45.0, Altitude = 100.0 });

        Assert.Contains("\"heading\"", json);
        Assert.Contains("\"altitude\"", json);
    }
}
