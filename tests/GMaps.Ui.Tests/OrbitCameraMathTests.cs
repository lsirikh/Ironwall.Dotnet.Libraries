using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 상세 창 3D 프리뷰 오빗 카메라 수식 — PRD symbol-detail-and-door-control FR-16/17.
/// 지도 심볼의 고정 피치 카메라와 <b>독립</b>이라는 것이 이 수식의 존재 이유다.
/// </summary>
public class OrbitCameraMathTests
{
    [Fact]
    public void should_rotate_in_eight_discrete_steps_per_turn()
    {
        // 45°씩 끊어 돈다 — 연속 회전이면 중간값이 나온다
        double perStep = OrbitCameraMath.AutoRotateSeconds / OrbitCameraMath.AutoRotateSteps;   // 2초
        Assert.Equal(0, OrbitCameraMath.AutoYawDeg(0), 6);
        Assert.Equal(0, OrbitCameraMath.AutoYawDeg(perStep * 0.99), 6);      // 아직 첫 칸
        Assert.Equal(45, OrbitCameraMath.AutoYawDeg(perStep), 6);
        Assert.Equal(90, OrbitCameraMath.AutoYawDeg(perStep * 2), 6);
        Assert.Equal(315, OrbitCameraMath.AutoYawDeg(perStep * 7), 6);
    }

    [Fact]
    public void should_wrap_after_a_full_turn()
    {
        double turn = OrbitCameraMath.AutoRotateSeconds;
        Assert.Equal(0, OrbitCameraMath.AutoYawDeg(turn), 6);
        Assert.Equal(45, OrbitCameraMath.AutoYawDeg(turn + turn / 8), 6);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void should_return_zero_for_invalid_elapsed(double elapsed)
        => Assert.Equal(0, OrbitCameraMath.AutoYawDeg(elapsed), 6);

    [Fact]
    public void should_apply_drag_to_yaw_and_pitch()
    {
        var (yaw, pitch) = OrbitCameraMath.ApplyDrag(0, 0, dxPixels: 100, dyPixels: 20);
        Assert.Equal(50, yaw, 6);      // 100px * 0.5 deg
        Assert.Equal(10, pitch, 6);
    }

    [Fact]
    public void should_clamp_pitch_to_avoid_flipping_over()
    {
        var (_, up) = OrbitCameraMath.ApplyDrag(0, 0, 0, 10_000);
        var (_, down) = OrbitCameraMath.ApplyDrag(0, 0, 0, -10_000);
        Assert.Equal(OrbitCameraMath.MaxPitchDeg, up, 6);
        Assert.Equal(OrbitCameraMath.MinPitchDeg, down, 6);
    }

    [Fact]
    public void should_wrap_yaw_instead_of_clamping()
    {
        // yaw 는 한 바퀴 돌아도 막히면 안 된다(무한 회전)
        var (yaw, _) = OrbitCameraMath.ApplyDrag(350, 0, dxPixels: 40, dyPixels: 0);   // +20 -> 370 -> 10
        Assert.Equal(10, yaw, 6);
        var (back, _) = OrbitCameraMath.ApplyDrag(10, 0, dxPixels: -40, dyPixels: 0);  // -20 -> -10 -> 350
        Assert.Equal(350, back, 6);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 1)]        // 정면: +Z
    [InlineData(90, 0, 1, 0, 0)]       // 오른쪽: +X
    [InlineData(180, 0, 0, 0, -1)]
    [InlineData(270, 0, -1, 0, 0)]
    public void should_place_camera_on_the_orbit(double yaw, double pitch, double x, double y, double z)
    {
        var (cx, cy, cz) = OrbitCameraMath.CameraPosition(yaw, pitch, distance: 1.0);
        Assert.Equal(x, cx, 6);
        Assert.Equal(y, cy, 6);
        Assert.Equal(z, cz, 6);
    }

    [Fact]
    public void should_raise_camera_when_pitch_increases()
    {
        var (_, low, _) = OrbitCameraMath.CameraPosition(0, 10, 10);
        var (_, high, _) = OrbitCameraMath.CameraPosition(0, 60, 10);
        Assert.True(high > low);
    }

    [Fact]
    public void should_keep_distance_constant_regardless_of_angles()
    {
        foreach (var (yaw, pitch) in new[] { (0.0, 0.0), (37.0, 22.0), (200.0, -45.0), (359.0, 80.0) })
        {
            var (x, y, z) = OrbitCameraMath.CameraPosition(yaw, pitch, 12.5);
            Assert.Equal(12.5, Math.Sqrt(x * x + y * y + z * z), 6);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(360, 0)]
    [InlineData(-10, 350)]
    [InlineData(720.5, 0.5)]
    [InlineData(double.NaN, 0)]
    public void should_normalize_angles(double input, double expected)
        => Assert.Equal(expected, OrbitCameraMath.Normalize360(input), 6);

    [Fact]
    public void should_reset_to_front_with_default_pitch()
    {
        var (yaw, pitch) = OrbitCameraMath.Front();
        Assert.Equal(0, yaw, 6);
        Assert.Equal(OrbitCameraMath.DefaultPitchDeg, pitch, 6);
    }
}
