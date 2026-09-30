using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 제공자 종류 → 생산자. 시험 무늬 · 파일은 바로, 카메라(ONVIF · RTSP 주소 · 외부 VMS)는 제공자로 주소를 얻은 뒤
/// LibVLC 로(<see cref="ResolvingFrameProducer"/>, T-02). 제공자 창구가 없으면(시험) RTSP 주소를 그대로 연다.
/// </summary>
internal sealed class FrameProducerFactory
{
    private readonly HostLog _log;
    private readonly HostCameraServices? _cameras;

    public FrameProducerFactory(HostLog log, HostCameraServices? cameras = null)
    {
        _log = log;
        _cameras = cameras;
    }

    /// <summary>호스트 제공자 창구(없으면 null — 시험). 이벤트 창 타일 PTZ 어댑터도 이것을 쓴다.</summary>
    public HostCameraServices? Cameras => _cameras;

    /// <param name="cameraId">제공자 캐시 · PTZ 와 같은 카메라 키. 비우면 <paramref name="name"/>.</param>
    public IFrameProducer Create(VideoProviderInfo provider, int width, int height, string name, string? cameraId = null)
        => provider.Kind switch
        {
            VideoProviderKind.TestPattern => new TestPatternProducer(name),
            VideoProviderKind.File => new LibVlcFrameProducer(provider, width, height, name, _log),
            _ when _cameras is not null => new ResolvingFrameProducer(_cameras, cameraId ?? name, provider, width, height, name, _log),
            _ => new LibVlcFrameProducer(provider, width, height, name, _log),
        };
}
