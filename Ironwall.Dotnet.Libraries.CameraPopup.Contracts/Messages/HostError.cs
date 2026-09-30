using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 기능 단위 실패 안내(FR-27). 호스트는 살아 있다.</summary>
public sealed class HostError : IIpcMessage
{
    /// <summary>기계 판독용 코드(예 "ptz-not-implemented", "memory-limit", "bad-message").</summary>
    public string Code { get; init; } = string.Empty;
    public string? Message { get; init; }

    /// <summary>영향 범위(스트림 id · 이벤트 키 · 카메라 id).</summary>
    public string? Scope { get; init; }
}
