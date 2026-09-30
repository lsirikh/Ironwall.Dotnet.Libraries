using DummyCameras.Ptz;

namespace DummyCameras.Tests;

public class PtzSimulatorTests
{
    private static readonly DateTime T0 = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private static PtzSimulator Create(double gotoSeconds = 3) => new(TimeSpan.FromSeconds(gotoSeconds), TimeSpan.FromSeconds(5));

    [Fact]
    public void should_integrate_velocity_over_time_when_continuous_move()
    {
        // Arrange
        var sim = Create();

        // Act — full-speed pan right for 1 s
        sim.ContinuousMove(1, 0, 0, TimeSpan.FromSeconds(2), T0);
        var s = sim.Status(T0.AddSeconds(1));

        // Assert
        Assert.Equal(PtzSimulator.PanTiltRatePerSec, s.Position.Pan, 6);
        Assert.True(s.PanTiltMoving);
        Assert.Equal("continuous", s.Motion);
    }

    [Fact]
    public void should_stop_by_itself_when_onvif_timeout_elapses()
    {
        var sim = Create();
        sim.ContinuousMove(0, 1, 0, TimeSpan.FromSeconds(2), T0);

        var s = sim.Status(T0.AddSeconds(5));

        Assert.False(s.IsMoving);
        Assert.Equal(2 * PtzSimulator.PanTiltRatePerSec, s.Position.Tilt, 6);   // integrated only until the 2 s timeout
    }

    [Fact]
    public void should_freeze_position_when_stop_arrives()
    {
        var sim = Create();
        sim.ContinuousMove(-1, 0, 1, TimeSpan.FromSeconds(2), T0);

        sim.Stop(true, true, T0.AddMilliseconds(400));
        var s = sim.Status(T0.AddSeconds(3));

        Assert.False(s.IsMoving);
        Assert.Equal(-0.4 * PtzSimulator.PanTiltRatePerSec, s.Position.Pan, 6);
        Assert.Equal(0.4 * PtzSimulator.ZoomRatePerSec, s.Position.Zoom, 6);
    }

    [Fact]
    public void should_stop_only_zoom_when_stop_names_zoom_axis()
    {
        var sim = Create();
        sim.ContinuousMove(1, 0, 1, TimeSpan.FromSeconds(2), T0);

        sim.Stop(panTilt: false, zoom: true, T0.AddMilliseconds(500));
        var s = sim.Status(T0.AddSeconds(1));

        Assert.True(s.PanTiltMoving);
        Assert.False(s.ZoomMoving);
    }

    [Fact]
    public void should_clamp_to_space_when_moving_past_the_edge()
    {
        var sim = Create();
        sim.ContinuousMove(1, 0, 0, TimeSpan.FromSeconds(10), T0);

        var s = sim.Status(T0.AddSeconds(9));

        Assert.Equal(1.0, s.Position.Pan, 6);
        Assert.False(s.PanTiltMoving);   // pinned at the limit = not moving
    }

    [Fact]
    public void should_be_moving_until_travel_time_then_arrive_when_goto_preset()
    {
        var sim = Create(gotoSeconds: 3);
        var p3 = sim.FindPreset("3")!;

        Assert.True(sim.GotoPreset("3", T0));
        var mid = sim.Status(T0.AddSeconds(1.5));
        var end = sim.Status(T0.AddSeconds(3.01));

        Assert.True(mid.IsMoving);
        Assert.Equal("goto", mid.Motion);
        Assert.Equal("3", mid.TargetPreset);
        Assert.Equal(T0.AddSeconds(3), mid.ArrivesAtUtc);
        Assert.Equal(p3.Position.Pan / 2, mid.Position.Pan, 6);
        Assert.False(end.IsMoving);
        Assert.Equal(p3.Position, end.Position);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("P2")]
    [InlineData("p2")]
    public void should_accept_token_or_name_when_goto_preset(string token)
    {
        var sim = Create(0);
        Assert.True(sim.GotoPreset(token, T0));
        Assert.Equal(sim.FindPreset("2")!.Position, sim.Status(T0).Position);
    }

    [Fact]
    public void should_reject_unknown_token_when_goto_preset()
    {
        var sim = Create();
        Assert.False(sim.GotoPreset("99", T0));
        Assert.False(sim.Status(T0).IsMoving);
    }

    [Fact]
    public void should_cancel_goto_when_stop_arrives()
    {
        var sim = Create(gotoSeconds: 4);
        sim.GotoPreset("4", T0);

        sim.Stop(true, true, T0.AddSeconds(1));
        var s = sim.Status(T0.AddSeconds(5));

        Assert.False(s.IsMoving);
        Assert.Equal(sim.FindPreset("4")!.Position.Pan / 4, s.Position.Pan, 6);
    }

    [Fact]
    public void should_let_a_human_move_win_when_continuous_move_interrupts_goto()
    {
        var sim = Create(gotoSeconds: 4);
        sim.GotoPreset("5", T0);

        sim.ContinuousMove(0, 1, 0, TimeSpan.FromSeconds(2), T0.AddSeconds(1));
        var s = sim.Status(T0.AddSeconds(1.5));

        Assert.Equal("continuous", s.Motion);
        Assert.Null(s.TargetPreset);
    }

    [Fact]
    public void should_store_current_position_when_set_preset_without_token()
    {
        var sim = Create(0);
        sim.AbsoluteMove(new PtzVector(0.25, -0.25, 0.5), T0);

        var token = sim.SetPreset(null, "Gate", T0);

        Assert.Equal(new PtzVector(0.25, -0.25, 0.5), sim.FindPreset(token)!.Position);
        Assert.True(sim.RemovePreset(token));
        Assert.Null(sim.FindPreset(token));
    }

    [Fact]
    public void should_travel_to_home_when_goto_home()
    {
        var sim = Create(gotoSeconds: 2);
        sim.GotoPreset("4", T0);
        sim.GotoHome(T0.AddSeconds(3));

        var mid = sim.Status(T0.AddSeconds(4));
        var end = sim.Status(T0.AddSeconds(5.1));

        Assert.Equal("home", mid.TargetPreset);
        Assert.Equal(sim.Home, end.Position);
    }
}
