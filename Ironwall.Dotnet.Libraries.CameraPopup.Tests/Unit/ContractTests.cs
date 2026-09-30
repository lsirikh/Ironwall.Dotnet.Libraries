using System.Buffers.Binary;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

public class ContractTests
{
    [Fact]
    public void should_round_trip_every_registered_message_when_serialized()
    {
        IIpcMessage[] messages =
        {
            new Hello { Token = PipeNaming.CreateToken(), ClientProcessId = 42 },
            new HelloAck { HostProcessId = 7, HostVersion = "1.0" },
            new Heartbeat { Sequence = 9 },
            new HeartbeatAck { Sequence = 9, PrivateBytes = 123, OpenStreams = 1, OpenWindows = 2 },
            new OpenOverlayStream { StreamId = "s1", Camera = new CameraRef { CameraId = "c1", Name = "정문" }, Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://h/1" }, Width = 320, Height = 180, SharedMemoryName = "n" },
            new CloseStream { StreamId = "s1" },
            FullOpenEventWindow(),
            new CloseEventWindow { EventKey = "detection-e1", Reason = EventWindowCloseReason.Evicted, ReturnHome = true },
            new BringToFront { EventKey = "detection-e1" },
            new SetTheme { Theme = "Light" },
            new PtzCommand { CameraId = "c1", Operation = PtzOperation.ContinuousMove, Pan = 0.5, Tilt = -0.25 },
            new PtzFocusCommand { CameraId = "c1", Direction = -1, Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 8080 } },
            new CameraRequest
            {
                RequestId = "r1", Kind = CameraRequestKind.SetPreset, CameraId = "c1", PresetName = "정문", PresetToken = "p1",
                IrCutFilter = "OFF", AutoFocus = true, TimeoutMs = 3000,
                Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80, Username = "u", Password = "p", PreferSubStream = false, FallbackUri = "rtsp://10.0.0.5/s" },
            },
            new CameraResponse
            {
                RequestId = "r1", Kind = CameraRequestKind.GetPresets, CameraId = "c1", Success = true, PtzCapable = true, ImagingCapable = true,
                Presets = new List<CameraPreset> { new() { Token = "p1", Name = "정문" }, new() { Token = "p2" } }, IrCutFilter = "AUTO", AutoFocus = true,
            },
            new DebugCommand { Kind = DebugCommandKind.NativeAccessViolation },
            new StreamStateChanged { StreamId = "s1", State = StreamState.Failed, Detail = "open-timeout" },
            new WindowOpened { EventKey = "e1", Reused = true },
            new WindowClosed { EventKey = "e1", Reason = EventWindowCloseReason.User },
            new WindowMoved { EventKey = "e1", X = -1200, Y = 40 },
            new TileClosed { EventKey = "e1", CameraId = "c2" },
            new PinChanged { EventKey = "e1", Pinned = true },
            new HostError { Code = "x", Scope = "c1" },
        };
        Assert.Equal(MessageRegistry.Names.Count, messages.Length);

        foreach (var original in messages)
        {
            var bytes = IpcSerializer.Serialize(original, 77);
            var status = IpcSerializer.TryDeserialize(bytes, out var decoded, out var id, out var type);

            Assert.Equal(DecodeStatus.Ok, status);
            Assert.Equal(77, id);
            Assert.Equal(MessageRegistry.NameOf(original.GetType()), type);
            Assert.IsType(original.GetType(), decoded);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original, original.GetType(), IpcSerializer.Options),
                         System.Text.Json.JsonSerializer.Serialize(decoded, decoded!.GetType(), IpcSerializer.Options));
        }
    }

    [Fact]
    public void should_write_enums_as_strings_when_serialized()
    {
        var json = System.Text.Encoding.UTF8.GetString(IpcSerializer.Serialize(new PtzCommand { CameraId = "c", Operation = PtzOperation.Stop }, 1));
        Assert.Contains("\"operation\":\"Stop\"", json);
        Assert.Contains("\"type\":\"Ptz\"", json);
        Assert.Contains("\"v\":2", json);
    }

    [Theory]
    [InlineData("{\"v\":2,\"type\":\"FutureThing\",\"id\":3,\"body\":{}}", DecodeStatus.UnknownType)]
    [InlineData("{\"v\":1,\"type\":\"OpenEventWindow\",\"id\":3,\"body\":{}}", DecodeStatus.IncompatibleVersion)]
    [InlineData("{\"v\":99,\"type\":\"Hello\",\"id\":3,\"body\":{}}", DecodeStatus.IncompatibleVersion)]
    [InlineData("{\"type\":\"Hello\",\"body\":{}}", DecodeStatus.Malformed)]
    [InlineData("not json", DecodeStatus.Malformed)]
    [InlineData("[1,2]", DecodeStatus.Malformed)]
    [InlineData("{\"v\":2,\"type\":\"Hello\",\"id\":3,\"body\":5}", DecodeStatus.Malformed)]
    public void should_report_status_without_throwing_when_envelope_is_unusual(string json, DecodeStatus expected)
    {
        var status = IpcSerializer.TryDeserialize(System.Text.Encoding.UTF8.GetBytes(json), out var message, out _, out _);

        Assert.Equal(expected, status);
        Assert.Null(message);
    }

    [Fact]
    public async Task should_round_trip_frames_when_written_back_to_back()
    {
        using var stream = new MemoryStream();
        await FrameCodec.WriteFrameAsync(stream, new byte[] { 1, 2, 3 }, CancellationToken.None);
        await FrameCodec.WriteFrameAsync(stream, new byte[] { 9 }, CancellationToken.None);
        stream.Position = 0;

        Assert.Equal(new byte[] { 1, 2, 3 }, await FrameCodec.ReadFrameAsync(stream, CancellationToken.None));
        Assert.Equal(new byte[] { 9 }, await FrameCodec.ReadFrameAsync(stream, CancellationToken.None));
        Assert.Null(await FrameCodec.ReadFrameAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FrameCodec.MaxPayloadBytes + 1)]
    public async Task should_reject_frame_when_length_is_out_of_range(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(() => FrameCodec.ReadFrameAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task should_throw_end_of_stream_when_frame_is_truncated()
    {
        var frame = FrameCodec.Encode(new byte[] { 1, 2, 3, 4 });
        using var stream = new MemoryStream(frame, 0, frame.Length - 1);

        await Assert.ThrowsAsync<EndOfStreamException>(() => FrameCodec.ReadFrameAsync(stream, CancellationToken.None));
    }

    [Fact]
    public void should_build_and_match_pipe_name_when_token_is_valid()
    {
        var token = PipeNaming.CreateToken();
        var name = PipeNaming.Build(1234, token);

        Assert.True(PipeNaming.IsValidToken(token));
        Assert.Equal($"ironwall-camhost-1234-{token}", name);
        Assert.True(PipeNaming.Matches(name, 1234, token));
        Assert.False(PipeNaming.Matches(name, 1235, token));
        Assert.False(PipeNaming.Matches(name, 1234, PipeNaming.CreateToken()));
        Assert.NotEqual(token, PipeNaming.CreateToken());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    public void should_reject_token_when_format_is_wrong(string? token)
    {
        Assert.False(PipeNaming.IsValidToken(token));
        Assert.False(PipeNaming.TokensEqual(token, PipeNaming.CreateToken()));
    }

    [Fact]
    public void should_round_trip_launch_arguments_when_parsed()
    {
        var token = PipeNaming.CreateToken();
        var original = new HostLaunchArguments
        {
            ParentProcessId = 55,
            PipeName = PipeNaming.Build(55, token),
            Token = token,
            MemoryLimitMb = 900,
            Headless = true,
            DebugCommands = true,
            LogDirectory = @"C:\로그 폴더\x",
        };

        Assert.True(HostLaunchArguments.TryParse(original.ToArgumentList(), out var parsed, out var error), error);
        Assert.Equal(original.ParentProcessId, parsed!.ParentProcessId);
        Assert.Equal(original.PipeName, parsed.PipeName);
        Assert.Equal(original.Token, parsed.Token);
        Assert.Equal(900, parsed.MemoryLimitMb);
        Assert.True(parsed.Headless);
        Assert.True(parsed.DebugCommands);
        Assert.Equal(original.LogDirectory, parsed.LogDirectory);
    }

    [Fact]
    public void should_reject_launch_arguments_when_pipe_does_not_match_pid_and_token()
    {
        var token = PipeNaming.CreateToken();
        var args = new[] { "--parent-pid", "55", "--pipe", PipeNaming.Build(56, token), "--token", token };

        Assert.False(HostLaunchArguments.TryParse(args, out _, out var error));
        Assert.Contains("--pipe", error);
        Assert.False(HostLaunchArguments.TryParse(new[] { "--bogus" }, out _, out _));
        Assert.False(HostLaunchArguments.TryParse(Array.Empty<string>(), out _, out _));
    }

    [Theory]
    [InlineData(null, 1, 1, 1)]
    [InlineData(null, 4, 2, 2)]
    [InlineData(null, 5, 3, 2)]
    [InlineData("3x2", 5, 3, 2)]
    [InlineData("6x1", 6, 6, 1)]
    [InlineData("1x2", 4, 2, 2)]
    [InlineData("junk", 2, 2, 1)]
    [InlineData("4x4", 3, 2, 2)]
    [InlineData(null, 9, 3, 2)]
    public void should_resolve_grid_when_layout_and_count_given(string? layout, int count, int columns, int rows)
    {
        Assert.Equal((columns, rows), TileGrid.Resolve(layout, count));
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 1)]
    [InlineData(0, 0, 2, 2, 1)]
    [InlineData(0, 0, 4, 2, 2)]
    [InlineData(3, 2, 5, 3, 2)] // 5대 3×2 — 한 칸 빈다
    [InlineData(2, 3, 5, 2, 3)]
    [InlineData(1, 6, 6, 1, 6)]
    [InlineData(2, 2, 6, 3, 2)] // 못 담으면 기본값
    [InlineData(4, 4, 3, 3, 1)] // 6칸 초과 → 기본값
    [InlineData(1, 1, 9, 3, 2)] // 6대로 자른다
    public void should_resolve_grid_when_columns_rows_and_count_given(int columns, int rows, int count, int expectedColumns, int expectedRows)
    {
        Assert.Equal((expectedColumns, expectedRows), TileGrid.Resolve(columns, rows, count));
    }

    [Fact]
    public void should_offer_fr10_grids_when_camera_count_given()
    {
        Assert.Equal(new[] { (1, 1) }, TileGrid.AllowedFor(1));
        Assert.Equal(new[] { (2, 2), (4, 1), (1, 4) }, TileGrid.AllowedFor(4));
        Assert.Equal(new[] { (3, 2), (2, 3) }, TileGrid.AllowedFor(5));
        Assert.Equal(new[] { (3, 2), (2, 3), (6, 1), (1, 6) }, TileGrid.AllowedFor(6));
        Assert.Equal(TileGrid.AllowedFor(6), TileGrid.AllowedFor(40));
    }

    [Theory]
    [InlineData(EventWindowKind.Detection, "1234", "detection-1234")]
    [InlineData(EventWindowKind.Malfunction, "a-b", "malfunction-a-b")]
    public void should_round_trip_event_key_when_built(EventWindowKind kind, string id, string expected)
    {
        var key = EventKeys.Build(kind, id);

        Assert.Equal(expected, key);
        Assert.True(EventKeys.TryParse(key, out var k, out var parsedId));
        Assert.Equal((kind, id), (k, parsedId));
        Assert.False(EventKeys.TryParse("other-1", out _, out _));
        Assert.False(EventKeys.TryParse("detection-", out _, out _));
    }

    [Fact]
    public void should_carry_every_event_window_field_when_open_message_serialized()
    {
        var bytes = IpcSerializer.Serialize(FullOpenEventWindow(), 5);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(bytes, out var decoded, out _, out _));
        var m = Assert.IsType<OpenEventWindow>(decoded);
        Assert.Equal("detection-e1", m.EventKey);
        Assert.DoesNotContain("\"eventKey\"", json); // 파생값은 보내지 않는다
        Assert.Contains("\"kind\":\"Detection\"", json);
        Assert.Equal("구역-07", m.Header.ZoneName);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 9, 41, 7, TimeSpan.FromHours(9)), m.Header.OccurredAt);
        var cam = m.Cameras[0];
        Assert.Equal(("c1", true, false, "P2", "HOME", 3), (cam.CameraId, cam.IsPtz, cam.PtzAllowed, cam.TargetPresetToken, cam.HomePresetToken, cam.DelaySeconds));
        Assert.Equal("secret", cam.Provider.Password); // 계정은 파이프로는 간다(로그에서만 가린다)
        Assert.DoesNotContain("secret", cam.ToString());
        Assert.Equal((3, 2, 1), (m.GridColumns, m.GridRows, m.ExtraCameraCount));
        Assert.Equal((-1920, 0, 1920, 1080), (m.MonitorBounds.X, m.MonitorBounds.Y, m.MonitorBounds.Width, m.MonitorBounds.Height));
        Assert.Equal(1.5, m.DpiScale);
        Assert.Equal((-1800, 60, 1200, 700), (m.Window.X, m.Window.Y, m.Window.Width, m.Window.Height));
        Assert.Equal((true, 45, true, false, true, "Light"), (m.AlwaysOnTop, m.TimerCloseSeconds, m.ReturnHomeOnClose, m.CloseOnActionReport, m.Pinned, m.Theme));
    }

    private static OpenEventWindow FullOpenEventWindow() => new()
    {
        Kind = EventWindowKind.Detection,
        EventId = "e1",
        Header = new EventWindowHeader { KindLabel = "탐지", ZoneName = "구역-07", DeviceName = "펜스 센서 #104", EventTypeText = "침입", OccurredAt = new DateTimeOffset(2026, 9, 30, 9, 41, 7, TimeSpan.FromHours(9)) },
        Cameras =
        {
            new EventWindowCamera { CameraId = "c1", Name = "외곽 PTZ-3", IsPtz = true, PtzAllowed = false, TargetPresetToken = "P2", TargetPresetName = "P2", HomePresetToken = "HOME", DelaySeconds = 3,
                Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Uri = "http://10.0.0.5/onvif/device_service", Username = "admin", Password = "secret", ProfileToken = "Profile_1" } },
            new EventWindowCamera { CameraId = "c2", Name = "정문 고정-1", Provider = new VideoProviderInfo { Kind = VideoProviderKind.Rtsp, Uri = "rtsp://10.0.0.6/s1" } },
        },
        GridColumns = 3,
        GridRows = 2,
        ExtraCameraCount = 1,
        MonitorBounds = new PixelRect { X = -1920, Y = 0, Width = 1920, Height = 1080 },
        MonitorWorkArea = new PixelRect { X = -1920, Y = 0, Width = 1920, Height = 1040 },
        DpiScale = 1.5,
        MonitorDeviceName = @"\\.\DISPLAY2",
        Window = new PixelRect { X = -1800, Y = 60, Width = 1200, Height = 700 },
        AlwaysOnTop = true,
        TimerCloseSeconds = 45,
        CloseOnActionReport = false,
        ReturnHomeOnClose = true,
        Pinned = true,
        Theme = "Light",
    };


    [Fact]
    public void should_compute_layout_offsets_when_size_is_valid()
    {
        var layout = SharedFrameLayout.Create(320, 180);

        Assert.Equal(1280, layout.Stride);
        Assert.Equal(1280L * 180, layout.SlotBytes);
        Assert.Equal(64 + 2L * 1280 * 180, layout.TotalBytes);
        Assert.Equal(64 + layout.SlotBytes, layout.SlotOffset(1));
        Assert.Equal(64, layout.SlotOffset(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SharedFrameLayout.Create(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SharedFrameLayout.Create(4000, 10));
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, true)]
    [InlineData(5, 7, false)]
    [InlineData(0, 0, false)]
    public void should_judge_copy_consistency_when_writer_advanced(long published, long writingAfter, bool expected)
    {
        Assert.Equal(expected, SharedFrameLayout.IsCopyConsistent(published, writingAfter));
    }

    [Theory]
    [InlineData("rtsp://admin:secret@10.0.0.5:554/s1", "rtsp://***@10.0.0.5:554/s1")]
    [InlineData("rtsp://10.0.0.5/s1", "rtsp://10.0.0.5/s1")]
    [InlineData("rtsp://admin:se@cret@10.0.0.5/s1", "rtsp://***@10.0.0.5/s1")]
    [InlineData("rtsp://10.0.0.5/path@x?q=a@b", "rtsp://10.0.0.5/path@x?q=a@b")]
    [InlineData(null, "-")]
    public void should_redact_credentials_when_provider_is_logged(string? uri, string expected)
    {
        Assert.Equal(expected, VideoProviderInfo.RedactUri(uri));
        var text = new VideoProviderInfo { Uri = uri, FallbackUri = uri, Username = "admin", Password = "secret" }.ToString();
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("cret", text);
    }
}
