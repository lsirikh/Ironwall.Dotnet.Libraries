using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 타일 카메라 → 제어. 시험 무늬 제공자는 <see cref="SimulatedCameraControl"/>(가짜 PTZ · 프리셋),
/// 카메라 제공자(ONVIF · RTSP 주소 · 외부 VMS)는 호스트 제공자 창구가 있으면 <see cref="ProviderTileCameraControl"/>(T-02 — 실제 ONVIF PTZ),
/// 창구가 없으면(시험 · 제공자 초기화 실패) <see cref="UnavailableCameraControl"/>.
/// </summary>
internal sealed class TileCameraControlFactory : ITileCameraControlFactory
{
    public const string OnvifPendingReason = "PTZ 제공자 연결 전(T-02)";
    public const string RtspNoPtzReason = "영상 주소(RTSP) 제공자는 PTZ 없음";
    public const string FileNoPtzReason = "파일 제공자는 PTZ 없음";

    private readonly HostCameraServices? _cameras;

    public TileCameraControlFactory(HostCameraServices? cameras = null) => _cameras = cameras;

    public ITileCameraControl Create(EventWindowCamera camera)
        => camera.Provider.Kind switch
        {
            VideoProviderKind.TestPattern => new SimulatedCameraControl(),
            VideoProviderKind.Onvif or VideoProviderKind.Rtsp or VideoProviderKind.ExternalVms when _cameras is not null
                => new ProviderTileCameraControl(_cameras, camera.CameraId, camera.Provider),
            VideoProviderKind.Onvif => new UnavailableCameraControl(OnvifPendingReason),
            VideoProviderKind.Rtsp => new UnavailableCameraControl(RtspNoPtzReason),
            _ => new UnavailableCameraControl(FileNoPtzReason),
        };
}
