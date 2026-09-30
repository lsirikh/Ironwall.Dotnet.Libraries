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
            new OpenEventWindow { EventKey = "e1", Layout = "3x2", Cameras = { new EventWindowCamera { Camera = new CameraRef { CameraId = "c1" }, PresetToken = "P2", PresetDelaySeconds = 3 } }, ClosePolicy = new WindowClosePolicy { TimeoutSeconds = 30, Pinned = true } },
            new CloseEventWindow { EventKey = "e1", Reason = "action-report" },
            new PtzCommand { CameraId = "c1", Operation = PtzOperation.ContinuousMove, Pan = 0.5, Tilt = -0.25 },
            new DebugCommand { Kind = DebugCommandKind.NativeAccessViolation },
            new StreamStateChanged { StreamId = "s1", State = StreamState.Failed, Detail = "open-timeout" },
            new WindowOpened { EventKey = "e1", Reused = true },
            new WindowClosed { EventKey = "e1", Reason = "user" },
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
        Assert.Contains("\"v\":1", json);
    }

    [Theory]
    [InlineData("{\"v\":1,\"type\":\"FutureThing\",\"id\":3,\"body\":{}}", DecodeStatus.UnknownType)]
    [InlineData("{\"v\":99,\"type\":\"Hello\",\"id\":3,\"body\":{}}", DecodeStatus.IncompatibleVersion)]
    [InlineData("{\"type\":\"Hello\",\"body\":{}}", DecodeStatus.Malformed)]
    [InlineData("not json", DecodeStatus.Malformed)]
    [InlineData("[1,2]", DecodeStatus.Malformed)]
    [InlineData("{\"v\":1,\"type\":\"Hello\",\"id\":3,\"body\":5}", DecodeStatus.Malformed)]
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
    [InlineData(null, "-")]
    public void should_redact_credentials_when_provider_is_logged(string? uri, string expected)
    {
        Assert.Equal(expected, VideoProviderInfo.RedactUri(uri));
        var text = new VideoProviderInfo { Uri = uri, Username = "admin", Password = "secret" }.ToString();
        Assert.DoesNotContain("secret", text);
    }
}
