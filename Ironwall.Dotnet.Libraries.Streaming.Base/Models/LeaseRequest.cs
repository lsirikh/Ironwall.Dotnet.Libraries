namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

/// <summary>
/// AcquireAsync 호출자가 제공하는 Lease 요청 컨텍스트.
/// </summary>
public sealed record LeaseRequest(
    string CameraId,
    string EventId,
    string SlotId,
    string RequesterTag);
