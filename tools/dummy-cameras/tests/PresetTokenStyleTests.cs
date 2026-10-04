using System.Text.Json;
using DummyCameras.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;

namespace DummyCameras.Tests;

/// <summary>Preset token shapes of different camera models (numeric · named · padded · mixed) - simulator and CLI.</summary>
public class PresetTokenStyleTests
{
    private static readonly DateTime T0 = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);

    private static PtzSimulator Create(PresetTokenStyle style) => new(TimeSpan.Zero, TimeSpan.FromSeconds(5), tokenStyle: style);

    [Theory]
    [InlineData(PresetTokenStyle.Numeric, "1,2,3,4,5")]
    [InlineData(PresetTokenStyle.Named, "Preset001,Preset002,Preset003,Preset004,Preset005")]
    [InlineData(PresetTokenStyle.Padded, "001,002,003,004,005")]
    [InlineData(PresetTokenStyle.Mixed, "1,2,Preset003,004,5")]
    public void should_hand_out_tokens_in_camera_shape_when_style_given(PresetTokenStyle style, string expected)
    {
        var sim = Create(style);

        Assert.Equal(expected.Split(','), sim.Presets.Select(p => p.Token));
        Assert.Equal(new[] { "P1", "P2", "P3", "P4", "P5" }, sim.Presets.Select(p => p.Name));
        Assert.Equal(PtzSimulator.DefaultPresets().Select(p => p.Position), sim.Presets.Select(p => p.Position));   // same spots as numeric
    }

    [Theory]
    [InlineData(PresetTokenStyle.Named, "Preset003")]
    [InlineData(PresetTokenStyle.Padded, "003")]
    public void should_reject_plain_number_and_accept_own_token_when_tokens_are_not_numbers(PresetTokenStyle style, string own)
    {
        // A real camera with name-shaped / zero-padded tokens does not know "3" - the GIS sends preset_index as the token.
        var sim = Create(style);

        Assert.False(sim.GotoPreset("3", T0));
        Assert.True(sim.GotoPreset(own, T0));
        Assert.Equal(sim.FindPreset(own)!.Position, sim.Status(T0).Position);
    }

    [Fact]
    public void should_accept_numeric_tokens_only_where_camera_has_them_when_mixed()
    {
        var sim = Create(PresetTokenStyle.Mixed);

        Assert.True(sim.GotoPreset("2", T0));
        Assert.True(sim.GotoPreset("5", T0));
        Assert.False(sim.GotoPreset("3", T0));
        Assert.False(sim.GotoPreset("4", T0));
        Assert.True(sim.GotoPreset("004", T0));
    }

    [Theory]
    [InlineData(PresetTokenStyle.Numeric, "6")]
    [InlineData(PresetTokenStyle.Named, "Preset006")]
    [InlineData(PresetTokenStyle.Padded, "006")]
    [InlineData(PresetTokenStyle.Mixed, "6")]
    public void should_shape_new_token_like_camera_when_set_preset_without_token(PresetTokenStyle style, string expected)
    {
        var sim = Create(style);
        sim.AbsoluteMove(new PtzVector(0.1, 0.2, 0.3), T0);

        var token = sim.SetPreset(null, null, T0);

        Assert.Equal(expected, token);
        Assert.Equal("P6", sim.FindPreset(token)!.Name);
        Assert.Equal(new PtzVector(0.1, 0.2, 0.3), sim.FindPreset(token)!.Position);
    }

    [Fact]
    public void should_swap_presets_and_keep_position_when_reset_presets_to_another_style()
    {
        var sim = Create(PresetTokenStyle.Numeric);
        sim.AbsoluteMove(new PtzVector(0.3, 0.3, 0.3), T0);
        sim.SetPreset(null, "Gate", T0);

        sim.ResetPresets(PresetTokenStyle.Named, T0);

        Assert.Equal(PresetTokenStyle.Named, sim.TokenStyle);
        Assert.Equal(PtzSimulator.DefaultPresets(PresetTokenStyle.Named).Select(p => p.Token), sim.Presets.Select(p => p.Token));
        Assert.Equal(new PtzVector(0.3, 0.3, 0.3), sim.Status(T0).Position);
        Assert.Equal("Preset006", sim.SetPreset(null, null, T0));
    }

    [Fact]
    public void should_cancel_preset_goto_when_presets_are_replaced()
    {
        var sim = new PtzSimulator(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5));
        sim.GotoPreset("4", T0);

        sim.ResetPresets(PresetTokenStyle.Padded, T0.AddSeconds(1));
        var s = sim.Status(T0.AddSeconds(5));

        Assert.False(s.IsMoving);
        Assert.Equal(sim.FindPreset("004")!.Position.Pan / 4, s.Position.Pan, 6);
    }

    [Theory]
    [InlineData("named", 4)]
    [InlineData("Padded", 4)]
    public void should_apply_one_style_to_every_ptz_camera_when_no_camera_number_given(string style, int ptz)
    {
        var o = Cli.ParseFarm(new Args($"run --ptz {ptz} --preset-tokens {style}".Split(' ')));

        Assert.Equal(Enumerable.Range(1, ptz), o.PresetTokenStyles.Keys.OrderBy(k => k));
        Assert.All(o.PresetTokenStyles.Values, v => Assert.Equal(PresetTokens.Parse(style), v));
    }

    [Fact]
    public void should_set_style_per_camera_when_list_given()
    {
        var o = Cli.ParseFarm(new Args("run --preset-tokens 2=named,3=padded,4=mixed".Split(' ')));

        Assert.False(o.PresetTokenStyles.ContainsKey(1));
        Assert.Equal(PresetTokenStyle.Named, o.PresetTokenStyles[2]);
        Assert.Equal(PresetTokenStyle.Padded, o.PresetTokenStyles[3]);
        Assert.Equal(PresetTokenStyle.Mixed, o.PresetTokenStyles[4]);
    }

    [Theory]
    [InlineData("run --preset-tokens 6=named")]        // cam6 is a fixed camera (PTZ = 1..4)
    [InlineData("run --preset-tokens 2=weird")]
    [InlineData("run --preset-tokens")]
    public void should_refuse_when_preset_tokens_option_is_wrong(string line)
    {
        Assert.Throws<FormatException>(() => Cli.ParseFarm(new Args(line.Split(' '))));
    }

    [Theory]
    [InlineData(PresetTokenStyle.Numeric, "1,2,3,4,5")]
    [InlineData(PresetTokenStyle.Named, "")]
    [InlineData(PresetTokenStyle.Padded, "")]
    [InlineData(PresetTokenStyle.Mixed, "1,2,5")]
    public void should_leave_only_canonical_numbers_storable_for_gis_when_camera_shape_varies(PresetTokenStyle style, string storable)
    {
        // The GIS preset window (MappingCameraPreset.Index) may only store canonical positive integers as preset_index.
        var indexes = PtzSimulator.DefaultPresets(style).Select(p => new MappingCameraPreset(p.Token, p.Name).Index).Where(i => i is not null);

        Assert.Equal(storable.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse), indexes.Select(i => i!.Value));
    }
}

/// <summary>ONVIF-only farm: cam1 numeric · cam2 named · cam3 padded · cam4 mixed, read by OUR production PTZ client.</summary>
public sealed class PresetStyleFarmFixture : IAsyncLifetime
{
    public Farm Farm { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var o = new FarmOptions
        {
            Count = 4,
            PtzCount = 4,
            OnvifBasePort = 0,
            ControlPort = 0,
            EnableRtsp = false,
            GotoSeconds = 0.3,
            OutDir = TestDirs.NewOutDir("presets"),
        };
        o.PresetTokenStyles[2] = PresetTokenStyle.Named;
        o.PresetTokenStyles[3] = PresetTokenStyle.Padded;
        o.PresetTokenStyles[4] = PresetTokenStyle.Mixed;
        Farm = await Farm.StartAsync(o, TextWriter.Null, CancellationToken.None);
    }

    public async Task DisposeAsync() => await Farm.DisposeAsync();

    public VideoProviderInfo Info(int cam) => new()
    {
        Kind = VideoProviderKind.Onvif,
        Host = "127.0.0.1",
        Port = Farm.Find(cam)!.Spec.OnvifPort,
        Username = Farm.Options.User,
        Password = Farm.Options.Password,
    };
}

public class PresetStyleOnvifTests : IClassFixture<PresetStyleFarmFixture>
{
    private readonly PresetStyleFarmFixture _fx;

    public PresetStyleOnvifTests(PresetStyleFarmFixture fx) => _fx = fx;

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    [Theory]
    [InlineData(1, "1,2,3,4,5")]
    [InlineData(2, "Preset001,Preset002,Preset003,Preset004,Preset005")]
    [InlineData(3, "001,002,003,004,005")]
    [InlineData(4, "1,2,Preset003,004,5")]
    public async Task should_read_camera_shaped_tokens_through_onvif_when_get_presets(int cam, string expected)
    {
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        Assert.True((await ptz.PrepareAsync($"ps{cam}", _fx.Info(cam), Within(10_000))).PtzCapable);

        var presets = await ptz.GetPresetsAsync($"ps{cam}", Within(5_000));

        Assert.NotNull(presets);
        Assert.Equal(expected.Split(','), presets!.Select(p => p.Token));
        Assert.Equal(new[] { "P1", "P2", "P3", "P4", "P5" }, presets.Select(p => p.Name));
        ptz.Release($"ps{cam}");
    }

    [Fact]
    public async Task should_fail_index_token_and_reach_own_token_when_camera_uses_named_tokens()
    {
        // What an event does with a server preset_index on a named-token camera: GotoPreset("3") → ter:NoToken.
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        var sim = _fx.Farm.Find(2)!.Ptz!;
        sim.Reset();
        await ptz.PrepareAsync("pg2", _fx.Info(2), Within(10_000));

        var byIndex = await ptz.GotoPresetAsync("pg2", "3", Within(5_000));
        var byToken = await ptz.GotoPresetAsync("pg2", "Preset003", Within(5_000));
        var arrived = await Wait.UntilAsync(() => sim.Status(DateTime.UtcNow).Position == sim.FindPreset("Preset003")!.Position, TimeSpan.FromSeconds(3));

        Assert.False(byIndex);
        Assert.True(byToken);
        Assert.True(arrived);
        ptz.Release("pg2");
    }

    [Fact]
    public async Task should_switch_running_camera_shape_when_control_endpoint_asks()
    {
        using var http = new HttpClient();
        var url = $"http://127.0.0.1:{_fx.Farm.ControlPort}";
        var sim = _fx.Farm.Find(1)!.Ptz!;
        try
        {
            using var bad = await http.PostAsync($"{url}/cam/1/ptz/presets?style=nope", null);
            using var ok = await http.PostAsync($"{url}/cam/1/ptz/presets?style=padded", null);
            var status = JsonDocument.Parse(await http.GetStringAsync($"{url}/status")).RootElement
                .GetProperty("cameras")[0].GetProperty("ptz");

            Assert.Equal(400, (int)bad.StatusCode);
            Assert.True(ok.IsSuccessStatusCode);
            Assert.Equal("padded", status.GetProperty("presetTokens").GetString());
            Assert.Equal("001", status.GetProperty("presets")[0].GetProperty("token").GetString());

            var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
            await ptz.PrepareAsync("pc1", _fx.Info(1), Within(10_000));
            var presets = await ptz.GetPresetsAsync("pc1", Within(5_000));
            Assert.Equal("001", presets![0].Token);
            ptz.Release("pc1");
        }
        finally
        {
            sim.ResetPresets(PresetTokenStyle.Numeric, DateTime.UtcNow);
        }
    }
}
