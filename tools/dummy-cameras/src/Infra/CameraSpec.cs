namespace DummyCameras.Infra;

/// <summary>
/// Per-camera fault injection. Mutable at runtime (control endpoint) - fields are read on every request.
/// </summary>
public sealed class CameraFaults
{
    private volatile int _slowOnvifMs;
    private volatile bool _noOnvifReply;
    private volatile bool _authFail;
    private volatile bool _httpDigest;
    private volatile bool _wsSecurityOnly;
    private volatile bool _corruptStream;

    /// <summary>Delay every ONVIF reply by this many ms (FR-26 · K5).</summary>
    public int SlowOnvifMs { get => _slowOnvifMs; set => _slowOnvifMs = Math.Max(0, value); }

    /// <summary>Read the request, never answer (connection held until the client gives up).</summary>
    public bool NoOnvifReply { get => _noOnvifReply; set => _noOnvifReply = value; }

    /// <summary>Reject every authenticated ONVIF call (ter:NotAuthorized) and RTSP read (401).</summary>
    public bool AuthFail { get => _authFail; set => _authFail = value; }

    /// <summary>Require HTTP Digest (401 challenge) on the ONVIF endpoints in addition to WS-UsernameToken.</summary>
    public bool HttpDigest { get => _httpDigest; set => _httpDigest = value; }

    /// <summary>
    /// Strict WS-Security camera: every op except GetSystemDateAndTime (GetCapabilities · GetDeviceInformation · Media · PTZ)
    /// needs a valid WS-UsernameToken; HTTP Digest is neither offered nor accepted, a missing token is ter:NotAuthorized.
    /// Proves a client really sends the SOAP header instead of riding on PRE_AUTH ops or the HTTP Digest fallback.
    /// </summary>
    public bool WsSecurityOnly { get => _wsSecurityOnly; set => _wsSecurityOnly = value; }

    /// <summary>Kill the RTSP publisher this many seconds after start (camera "power off"). null = never.</summary>
    public int? KillStreamAfterSec { get; set; }

    /// <summary>Publish with a bit-flipping bitstream filter (decoder errors · artefacts).</summary>
    public bool CorruptStream { get => _corruptStream; set => _corruptStream = value; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (SlowOnvifMs > 0) parts.Add($"slow-onvif={SlowOnvifMs}ms");
        if (NoOnvifReply) parts.Add("no-onvif-reply");
        if (AuthFail) parts.Add("auth-fail");
        if (HttpDigest) parts.Add("http-digest");
        if (WsSecurityOnly) parts.Add("ws-security-only");
        if (KillStreamAfterSec is int k) parts.Add($"kill-stream-after={k}s");
        if (CorruptStream) parts.Add("corrupt-stream");
        return parts.Count == 0 ? "none" : string.Join(' ', parts);
    }
}

/// <summary>One dummy camera (static description + mutable faults).</summary>
public sealed class CameraSpec
{
    public required int Index { get; init; }
    public required bool IsPtz { get; init; }

    /// <summary>ONVIF HTTP port. 0 = pick a free port (tests); the real port is published by the responder.</summary>
    public int OnvifPort { get; set; }

    public string Host { get; init; } = "127.0.0.1";
    public int RtspPort { get; init; } = 8554;

    public string Id => $"cam{Index}";
    public string Name => $"DUMMY-CAM{Index}";
    public string MainPath => Id;
    public string SubPath => $"{Id}_sub";

    /// <summary>Main stream resolution advertised in GetProfiles (and produced when transcoding).</summary>
    public int MainWidth { get; set; } = 1280;
    public int MainHeight { get; set; } = 720;

    /// <summary>A second, lower profile exists (transcode mode only - copy mode cannot scale).</summary>
    public bool HasSub { get; set; } = true;
    public int SubWidth { get; set; } = 640;
    public int SubHeight { get; set; } = 360;
    public int Fps { get; set; } = 15;

    /// <summary>User video file (null = ffmpeg testsrc2 with a name overlay).</summary>
    public string? VideoFile { get; set; }
    public double VideoOffsetSec { get; set; }

    /// <summary>true = <c>-c copy</c> (no overlay, no sub stream).</summary>
    public bool CopyVideo { get; set; }

    public CameraFaults Faults { get; } = new();

    public string DeviceServiceUrl => $"http://{Host}:{OnvifPort}/onvif/device_service";
    public string MediaServiceUrl => $"http://{Host}:{OnvifPort}/onvif/media_service";
    public string PtzServiceUrl => $"http://{Host}:{OnvifPort}/onvif/ptz_service";
    public string MainRtspUrl => $"rtsp://{Host}:{RtspPort}/{MainPath}";
    public string SubRtspUrl => $"rtsp://{Host}:{RtspPort}/{(HasSub ? SubPath : MainPath)}";

    /// <summary>RTSP paths this camera publishes.</summary>
    public IEnumerable<string> Paths => HasSub ? new[] { MainPath, SubPath } : new[] { MainPath };
}
