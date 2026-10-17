using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 카메라 회전요청 body 빌더(<see cref="CameraAimRequestBuilder"/>) 단위 테스트.
/// </summary>
public class CameraAimRequestBuilderTests
{
    private const double CamLat = 37.5665;
    private const double CamLng = 126.9780;

    [Fact]
    public void should_build_dto_when_inputs_valid()
    {
        var dto = CameraAimRequestBuilder.Build(7, CamLat, CamLng, 37.5670, 126.9785, "tester");

        Assert.NotNull(dto);
        Assert.Equal(7, dto!.CameraId);
        Assert.Equal(37.5670, dto.Latitude);
        Assert.Equal(126.9785, dto.Longitude);
        Assert.Equal(CamLat, dto.CameraLatitude);
        Assert.Equal(CamLng, dto.CameraLongitude);
        Assert.Equal("tester", dto.RequestedBy);
        Assert.True(dto.DistanceM > 0d);
        Assert.InRange(dto.BearingDeg, 0d, 360d);
    }

    [Fact]
    public void should_compute_distance_matching_haversine()
    {
        double tLat = 37.5670, tLng = 126.9785;
        var dto = CameraAimRequestBuilder.Build(1, CamLat, CamLng, tLat, tLng, "x");

        double expected = System.Math.Round(TrackingMath.HaversineMeters(CamLat, CamLng, tLat, tLng), 2);
        Assert.Equal(expected, dto!.DistanceM);
    }

    [Fact]
    public void should_compute_bearing_near_north_when_target_due_north()
    {
        var dto = CameraAimRequestBuilder.Build(1, CamLat, CamLng, CamLat + 0.001, CamLng, "x");

        Assert.NotNull(dto);
        Assert.True(dto!.BearingDeg < 1d || dto.BearingDeg > 359d);   // 정북 ≈ 0°
    }

    [Fact]
    public void should_return_null_when_camera_id_invalid()
    {
        Assert.Null(CameraAimRequestBuilder.Build(0, CamLat, CamLng, 37.567, 126.978, "x"));
        Assert.Null(CameraAimRequestBuilder.Build(-3, CamLat, CamLng, 37.567, 126.978, "x"));
    }

    [Fact]
    public void should_return_null_when_camera_coords_invalid()
    {
        Assert.Null(CameraAimRequestBuilder.Build(1, double.NaN, CamLng, 37.567, 126.978, "x"));
        Assert.Null(CameraAimRequestBuilder.Build(1, 95d, CamLng, 37.567, 126.978, "x"));
    }

    [Fact]
    public void should_return_null_when_target_coords_invalid()
    {
        Assert.Null(CameraAimRequestBuilder.Build(1, CamLat, CamLng, double.NaN, 126.978, "x"));
        Assert.Null(CameraAimRequestBuilder.Build(1, CamLat, CamLng, 37.567, 200d, "x"));
    }

    [Fact]
    public void should_default_requested_by_to_empty_when_null()
    {
        var dto = CameraAimRequestBuilder.Build(1, CamLat, CamLng, 37.567, 126.978, null);

        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto!.RequestedBy);
    }
}
