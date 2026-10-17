using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 이벤트 창이 열렸다(또는 기존 창을 앞으로 가져왔다).</summary>
public sealed class WindowOpened : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public bool Reused { get; init; }
}
