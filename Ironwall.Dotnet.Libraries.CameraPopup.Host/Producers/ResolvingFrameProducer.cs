using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 제공자로 재생 주소를 먼저 얻고(ONVIF GetStreamUri · 저장 주소 + 계정) LibVLC 생산자에 넘긴다(FR-17/18).
/// 상태: <c>Opening "resolving"</c> → (<c>Opening "connecting"</c> → <c>Playing</c>) 또는 <c>Failed "resolve-failed" · "not-supported" · "timeout"</c>.
/// 실패는 이 스트림에만 보고한다(FR-26). 주소 조회는 배경에서 — 파이프 줄을 붙잡지 않는다.
/// </summary>
internal sealed class ResolvingFrameProducer : IFrameProducer
{
    public const string DetailResolving = "resolving";
    public const string DetailConnecting = "connecting";

    private readonly HostCameraServices _cameras;
    private readonly VideoProviderInfo _provider;
    private readonly string _cameraId;
    private readonly int _width;
    private readonly int _height;
    private readonly string _name;
    private readonly HostLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly StreamOpenGate? _openGate;
    private readonly bool _priority;
    private readonly int _attempt;
    private IFrameProducer? _inner;
    private bool _disposed;

    public ResolvingFrameProducer(HostCameraServices cameras, string cameraId, VideoProviderInfo provider, int width, int height, string name, HostLog log,
        StreamOpenGate? openGate = null, bool priority = false, int attempt = 0)
    {
        _openGate = openGate;
        _priority = priority;
        _attempt = attempt;
        _cameras = cameras;
        _cameraId = string.IsNullOrWhiteSpace(cameraId) ? name : cameraId;
        _provider = provider;
        _width = width;
        _height = height;
        _name = name;
        _log = log;
    }

    public void Start(IFrameSink sink, Action<StreamState, string?> onState)
    {
        _ = Task.Run(() => StartAsync(sink, onState));
    }

    private async Task StartAsync(IFrameSink sink, Action<StreamState, string?> onState)
    {
        try
        {
            onState(StreamState.Opening, DetailResolving);
            var started = Environment.TickCount64;
            // 상자 크기를 실어 보낸다 — ONVIF 제공자가 "이 크기를 덮는 가장 낮은 해상도" 프로필을 고른다(작은 타일 = 서브).
            var resolved = await _cameras.ResolveStreamAsync(_cameraId, _provider.WithTarget(_width, _height), _cts.Token).ConfigureAwait(false);
            if (_cts.IsCancellationRequested) return;
            if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Uri))
            {
                onState(StreamState.Failed, resolved.ErrorCode ?? CameraErrorCodes.ResolveFailed);
                return;
            }

            var playable = new VideoProviderInfo
            {
                Kind = VideoProviderKind.Rtsp,
                Uri = resolved.Uri,
                Username = _provider.Username,
                Password = _provider.Password,
                OpenTimeoutMs = _provider.OpenTimeoutMs,
            };
            IFrameProducer inner;
            lock (_gate)
            {
                if (_disposed) return;
                inner = new LibVlcFrameProducer(playable, _width, _height, _name, _log, started, _openGate, _priority, _attempt);
                _inner = inner;
            }
            onState(StreamState.Opening, DetailConnecting);
            inner.Start(sink, onState);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"resolving producer {_name} failed: {ex.GetType().Name} {HostLogService.Mask(ex.Message)}");
            try { onState(StreamState.Failed, CameraErrorCodes.ResolveFailed); } catch { /* 보고 실패는 무시 */ }
        }
    }

    public void Dispose()
    {
        IFrameProducer? inner;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            inner = _inner;
        }
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        inner?.Dispose();
    }
}
