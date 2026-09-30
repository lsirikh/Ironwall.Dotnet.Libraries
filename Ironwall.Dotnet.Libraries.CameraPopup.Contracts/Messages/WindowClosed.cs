using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS: 이벤트 창이 닫혔다. 사용자 · 타이머로 닫힌 창은 GIS 가 재시작 복원 목록에서 뺀다.
/// </summary>
public sealed class WindowClosed : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;

    /// <summary>"user" · "timeout" · "command" 등.</summary>
    public string? Reason { get; init; }
}
