using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 제공자 종류 → 생산자. 시험 무늬 · 파일은 바로, 카메라(ONVIF · RTSP 주소 · 외부 VMS)는 제공자로 주소를 얻은 뒤
/// LibVLC 로(<see cref="ResolvingFrameProducer"/>, T-02). 제공자 창구가 없으면(시험) RTSP 주소를 그대로 연다.
/// </summary>
internal sealed class FrameProducerFactory : IFrameProducerFactory
{
    private readonly HostLog _log;
    private readonly HostCameraServices? _cameras;

    private readonly StreamOpenGate? _openGate;

    public FrameProducerFactory(HostLog log, HostCameraServices? cameras = null, StreamOpenGate? openGate = null)
    {
        _log = log;
        _cameras = cameras;
        _openGate = openGate;
    }

    /// <summary>카메라 연결 줄(없으면 제한 없음 — 시험).</summary>
    public StreamOpenGate? OpenGate => _openGate;

    /// <summary>호스트 제공자 창구(없으면 null — 시험). 이벤트 창 타일 PTZ 어댑터도 이것을 쓴다.</summary>
    public HostCameraServices? Cameras => _cameras;

    /// <param name="cameraId">제공자 캐시 · PTZ 와 같은 카메라 키. 비우면 <paramref name="name"/>.</param>
    /// <param name="priority">연결 줄 앞에 선다(사람이 방금 연 오버레이 · "다시 시도").</param>
    /// <param name="attempt">몇 번째 재시도인가(0 = 첫 시도).</param>
    public IFrameProducer Create(VideoProviderInfo provider, int width, int height, string name, string? cameraId = null, bool priority = false, int attempt = 0)
        => provider.Kind switch
        {
            VideoProviderKind.TestPattern => new TestPatternProducer(name),
            VideoProviderKind.File => new LibVlcFrameProducer(provider, width, height, name, _log),
            _ when _cameras is not null => new ResolvingFrameProducer(_cameras, cameraId ?? name, provider, width, height, name, _log, _openGate, priority, attempt),
            _ => new LibVlcFrameProducer(provider, width, height, name, _log, 0, _openGate, priority, attempt),
        };
}
