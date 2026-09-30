using System.Text.Json.Nodes;
using DummyCameras.Infra;
using DummyCameras.MediaMtx;
using DummyCameras.Seed;
using DummyCameras.Streams;

namespace DummyCameras.Tests;

public class GeneratorTests
{
    private static CameraSpec Cam(int i, bool ptz = true) => new() { Index = i, IsPtz = ptz, OnvifPort = 8180 + i };

    [Fact]
    public void should_bind_loopback_and_withhold_read_grant_when_camera_has_auth_fail()
    {
        var c1 = Cam(1);
        var c2 = Cam(2);
        c2.Faults.AuthFail = true;

        var yml = MediaMtxConfig.Build(new MediaMtxConfig.Settings(8554, 18000, 9997, "dummy", "dummy1234", false), new[] { c1, c2 });

        Assert.Contains("rtspAddress: 127.0.0.1:8554", yml);
        Assert.Contains("apiAddress: 127.0.0.1:9997", yml);
        Assert.Contains("rtpAddress: 127.0.0.1:18000", yml);
        Assert.Contains("path: \"cam1\"", yml);
        Assert.Contains("path: \"cam1_sub\"", yml);
        Assert.DoesNotContain("path: \"cam2", yml);
        Assert.DoesNotContain("rtmp: true", yml);
    }

    [Fact]
    public void should_publish_main_and_sub_with_overlay_when_no_video_given()
    {
        var args = FfmpegArgs.Build(Cam(3), withOverlayFont: true);
        var joined = string.Join(' ', args);

        Assert.Contains("testsrc2=size=1280x720:rate=15", joined);
        Assert.Contains("textfile=cam3.txt", joined);
        Assert.Contains("rtsp://127.0.0.1:8554/cam3 ", joined + " ");
        Assert.Contains("rtsp://127.0.0.1:8554/cam3_sub", joined);
        Assert.DoesNotContain("noise=", joined);
        Assert.DoesNotContain(":\\", joined);   // no drive-colon paths inside the filtergraph
    }

    [Fact]
    public void should_copy_single_stream_and_add_noise_when_copy_mode_with_corrupt_stream()
    {
        var cam = Cam(4);
        cam.VideoFile = @"D:\clips\a.mp4";
        cam.VideoOffsetSec = 12.5;
        cam.CopyVideo = true;
        cam.HasSub = false;
        cam.Faults.CorruptStream = true;

        var args = FfmpegArgs.Build(cam, withOverlayFont: false);

        Assert.Equal(new[] { "-stream_loop", "-1", "-ss", "12.5", "-i", @"D:\clips\a.mp4" }, args.SkipWhile(a => a != "-stream_loop").Take(6));
        Assert.Contains("copy", args);
        Assert.Contains("noise=amount=2000", args);
        Assert.DoesNotContain("-filter_complex", args);
        Assert.Single(args, a => a.StartsWith("rtsp://", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--slow-onvif 5=3000,6", 5, 3000)]
    [InlineData("--kill-stream-after=7=30", 7, 30)]
    public void should_parse_per_camera_values_when_fault_flags_given(string argLine, int cam, int value)
    {
        var a = new Args(("run " + argLine).Split(' '));
        var key = argLine.TrimStart('-').Split(' ', '=')[0];

        var list = a.PerCamera(key, 8).ToList();

        Assert.Contains((cam, (int?)value), list);
    }

    [Fact]
    public void should_apply_faults_to_named_cameras_only_when_parsing_run_options()
    {
        var o = Cli.ParseFarm(new Args("run --auth-fail 6 --no-onvif-reply 5 --slow-onvif 4=1200 --http-digest 3".Split(' ')));
        var specs = Enumerable.Range(1, 8).Select(i => { var f = new CameraFaults(); if (o.FaultSetters.TryGetValue(i, out var s)) s(f); return f; }).ToList();

        Assert.True(specs[5].AuthFail);
        Assert.True(specs[4].NoOnvifReply);
        Assert.Equal(1200, specs[3].SlowOnvifMs);
        Assert.True(specs[2].HttpDigest);
        Assert.Equal("none", specs[0].ToString());
        Assert.Equal(8180, o.ControlPort);
    }
}

public class SeedTests
{
    private static SeedOptions Options(string manifest) => new() { Manifest = manifest };

    [Fact]
    public async Task should_plan_every_row_without_network_when_dry_run()
    {
        // Arrange
        var manifest = Path.Combine(TestDirs.NewOutDir("seed"), "m.json");
        var output = new StringWriter();
        using var api = new DryRunSeedApi(output);

        // Act
        var code = await new Seeder(Options(manifest), api, output).RunAsync(CancellationToken.None);

        // Assert — group · controller · sensor · 8 cameras · 4×5 presets · mapping (32 creates) + 8 mapping rows
        var text = output.ToString();
        Assert.Equal(0, code);
        Assert.Contains("40 rows would be created", text);
        Assert.False(File.Exists(manifest));                       // dry-run writes nothing
        Assert.DoesNotContain("dummy1234", text);                  // camera password masked
        Assert.Contains("\"password\":\"***\"", text);
        Assert.Contains("\"category_event_mapping\":\"SENSOR_WITH_CAMERA\"", text);
        Assert.Contains("\"ip_port\":8181", text);
        Assert.Contains("\"type_camera\":\"PTZ\"", text);
        Assert.Contains("\"type_camera\":\"FIXED\"", text);
        Assert.Contains("\"delay_time\":6", text);                 // cam4 = delay base 3 + 3
    }

    [Fact]
    public async Task should_add_existing_sensors_as_members_when_sensor_ids_given()
    {
        var output = new StringWriter();
        using var api = new DryRunSeedApi(output);
        var o = Options(Path.Combine(TestDirs.NewOutDir("seed"), "m.json"));
        o.ExistingSensorIds.Add(42);

        await new Seeder(o, api, output).RunAsync(CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("\"device_ids\":[42]", text);
        Assert.DoesNotContain("devices/sensors ", text);
        Assert.DoesNotContain("devices/controllers", text);
    }

    [Fact]
    public async Task should_delete_children_first_and_skip_rows_without_prefix_when_cleanup_applies()
    {
        // Arrange — manifest: group ← camera(7) ← preset(70) · camera(8) whose name was changed (recycled id)
        var manifest = Path.Combine(TestDirs.NewOutDir("clean"), "m.json");
        new SeedManifest
        {
            RunId = "t",
            Items =
            {
                new SeedItem("group", 1, null, "LRT-DUMMY"),
                new SeedItem("camera", 7, null, "LRT-DUMMY-CAM1"),
                new SeedItem("preset", 70, 7, "P1"),
                new SeedItem("camera", 8, null, "LRT-DUMMY-CAM2"),
            },
        }.Save(manifest);
        var api = new RecordingApi(get => get.Contains("cameras/8") ? "Real Gate Camera" : get.Contains("groups") ? "LRT-DUMMY" : "LRT-DUMMY-CAM1");
        var o = Options(manifest);
        o.CredentialFile = WriteCred();

        // Act
        var code = await new Cleaner(o, api, new StringWriter()).RunAsync(CancellationToken.None);

        // Assert
        var deletes = api.Calls.Where(c => c.StartsWith("DELETE", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { "DELETE devices/cameras/7/presets/70", "DELETE devices/cameras/7", "DELETE devices/groups/1" }, deletes);
        Assert.Equal(2, code);                                        // one row refused
        var left = SeedManifest.Load(manifest)!;
        Assert.Single(left.Items);
        Assert.Equal(8, left.Items[0].Id);
    }

    [Fact]
    public void should_read_only_id_and_pw_lines_when_reading_credential_file()
    {
        var path = WriteCred();
        var (id, pw) = InjectorCredential.Read(path);
        Assert.Equal("inj", id);
        Assert.Equal("inj-pass", pw);
    }

    private static string WriteCred()
    {
        var path = Path.Combine(TestDirs.NewOutDir("cred"), "c.txt");
        File.WriteAllLines(path, new[] { "admin_id=root", "admin_pw=never-use", "id=inj", "pw=inj-pass" });
        return path;
    }

    private sealed class RecordingApi : ISeedApi
    {
        private readonly Func<string, string> _nameFor;
        public RecordingApi(Func<string, string> nameFor) => _nameFor = nameFor;
        public List<string> Calls { get; } = new();
        public bool IsDryRun => false;

        public Task<ApiResult> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
        {
            Calls.Add($"{method.Method} {path}");
            if (path == "auth/login")
            {
                Assert.Equal("inj", (string?)body!["login_id"]);   // never the admin_* lines
                return Task.FromResult(new ApiResult(200, JsonNode.Parse("{\"data\":{\"access_token\":\"t\"}}")));
            }
            if (method == HttpMethod.Get)
            {
                var name = _nameFor(path);
                return Task.FromResult(new ApiResult(200, new JsonObject { ["data"] = new JsonObject { ["name"] = name, ["name_device"] = name, ["name_event"] = name } }));
            }
            return Task.FromResult(new ApiResult(200, null));
        }

        public void Dispose() { }
    }
}
