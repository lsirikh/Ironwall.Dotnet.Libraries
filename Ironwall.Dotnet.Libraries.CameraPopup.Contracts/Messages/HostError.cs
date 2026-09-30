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

    /// <summary>
    /// 호스트가 곧 스스로 내려간다는 예고(메모리 한도 — 종료 코드 20). 감시자는 이 예고를 받은 호스트의 종료를
    /// 종료 코드를 못 읽어도 <b>계획된 재시작</b>으로 센다(충돌 예산을 쓰지 않는다).
    /// </summary>
    public const string MemoryLimitCode = "memory-limit";
}
