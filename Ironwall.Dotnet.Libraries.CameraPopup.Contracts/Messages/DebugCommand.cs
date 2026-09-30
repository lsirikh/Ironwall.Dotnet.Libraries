using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>디버그 전용(호스트가 <c>--debug-commands</c> 로 시작됐을 때만 처리). 생존 시험용.</summary>
public sealed class DebugCommand : IIpcMessage
{
    public DebugCommandKind Kind { get; init; }
}
