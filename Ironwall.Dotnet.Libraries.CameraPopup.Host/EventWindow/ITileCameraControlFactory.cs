using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>타일 카메라 → 제어 객체. 창이 닫히면 창이 받은 제어 객체를 전부 Dispose 한다.</summary>
internal interface ITileCameraControlFactory
{
    ITileCameraControl Create(EventWindowCamera camera);
}
