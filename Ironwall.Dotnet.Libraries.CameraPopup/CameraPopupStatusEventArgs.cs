using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>호스트가 보낸 상태 메시지(StreamStateChanged · WindowOpened · WindowClosed · HostError).</summary>
public sealed class CameraPopupStatusEventArgs : EventArgs
{
    public CameraPopupStatusEventArgs(IIpcMessage message) => Message = message;

    public IIpcMessage Message { get; }
}
