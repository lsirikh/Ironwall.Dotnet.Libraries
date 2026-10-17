using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD FR-06 "TiltCos 단독 변화 필터"(플랜 IMPL-D2) — <see cref="MapViewportSnapshot.IsTiltCosOnlyChangeFrom"/>.
/// GMapMarkerPidsControl 스냅샷 홉은 TiltCos 만 바뀐 스냅샷에 UpdateFOVPath 를 생략한다(슬라이더 드래그 중 카메라 N개 × 프레임 재계산 방지).
/// PointLatLng(GMap.NET) 의존이라 GMaps.Ui.Tests(net8.0) 가 아닌 이 프로젝트에 둔다. STA 불요.
/// </summary>
public class ViewportSnapshotTiltFilterTests
{
    private static MapViewportSnapshot Snap(double tiltCos = 1.0, long revision = 1, double zoom = 18, double bearing = 0,
        double width = 640, double height = 480, double digital = 1.0, double lat = 37.5, double lng = 127.0)
        => new(new PointLatLng(lat, lng), bearing, zoom, width, height, digital, tiltCos, revision);

    [Fact]
    public void should_detect_tilt_only_change_when_only_tilt_cos_and_revision_differ()
    {
        var prev = Snap(tiltCos: 1.0, revision: 7);
        var next = Snap(tiltCos: 0.94, revision: 8);
        Assert.True(next.IsTiltCosOnlyChangeFrom(prev));
    }

    [Fact]
    public void should_not_filter_when_previous_is_null()
        => Assert.False(Snap(tiltCos: 0.94).IsTiltCosOnlyChangeFrom(null));   // 첫 수신(구독 replay)은 반드시 처리

    [Fact]
    public void should_not_filter_when_tilt_cos_is_unchanged()
        => Assert.False(Snap(tiltCos: 0.94, revision: 2).IsTiltCosOnlyChangeFrom(Snap(tiltCos: 0.94, revision: 1)));

    [Theory]
    [InlineData("zoom")]
    [InlineData("bearing")]
    [InlineData("size")]
    [InlineData("digital")]
    [InlineData("center")]
    public void should_not_filter_when_another_field_changed_with_tilt_cos(string field)
    {
        var prev = Snap(tiltCos: 1.0, revision: 1);
        var next = field switch
        {
            "zoom" => Snap(tiltCos: 0.9, revision: 2, zoom: 18.5),
            "bearing" => Snap(tiltCos: 0.9, revision: 2, bearing: 45),
            "size" => Snap(tiltCos: 0.9, revision: 2, height: 550),
            "digital" => Snap(tiltCos: 0.9, revision: 2, digital: 1.5),
            _ => Snap(tiltCos: 0.9, revision: 2, lat: 37.6),
        };
        Assert.False(next.IsTiltCosOnlyChangeFrom(prev));
    }

    [Fact]
    public void should_default_tilt_cos_to_one_when_legacy_constructor_used()
    {
        var legacy = new MapViewportSnapshot(new PointLatLng(37.5, 127), 0, 18, 640, 480, 1.0, 1);
        Assert.Equal(MapViewportSnapshot.DefaultTiltCos, legacy.TiltCos);
        Assert.Equal(1.0, legacy.TiltCos);
    }
}
