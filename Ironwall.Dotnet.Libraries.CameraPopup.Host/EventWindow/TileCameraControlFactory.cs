using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 얇은 어댑터(T-05 임시). 시험 무늬 제공자는 <see cref="SimulatedCameraControl"/>(가짜 PTZ · 프리셋),
/// 그 밖은 <see cref="UnavailableCameraControl"/> — ONVIF PTZ 는 T-02a 제공자가 호스트에 들어오면 여기서 잇는다.
/// </summary>
internal sealed class TileCameraControlFactory : ITileCameraControlFactory
{
    public const string OnvifPendingReason = "PTZ 제공자 연결 전(T-02)";
    public const string RtspNoPtzReason = "영상 주소(RTSP) 제공자는 PTZ 없음";
    public const string FileNoPtzReason = "파일 제공자는 PTZ 없음";

    public ITileCameraControl Create(EventWindowCamera camera)
        => camera.Provider.Kind switch
        {
            VideoProviderKind.TestPattern => new SimulatedCameraControl(),
            VideoProviderKind.Onvif => new UnavailableCameraControl(OnvifPendingReason),
            VideoProviderKind.Rtsp => new UnavailableCameraControl(RtspNoPtzReason),
            _ => new UnavailableCameraControl(FileNoPtzReason),
        };
}
