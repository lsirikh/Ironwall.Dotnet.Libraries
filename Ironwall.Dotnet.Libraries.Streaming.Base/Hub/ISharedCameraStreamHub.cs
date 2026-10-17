using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Hub;

/// <summary>
/// 카메라 단위 RefCount 기반 RTSP 스트림 공유 허브.
/// 동일 cameraId에 대한 AcquireAsync는 기존 연결을 재사용하고 RefCount를 증가시킨다.
/// </summary>
public interface ISharedCameraStreamHub : IAsyncDisposable
{
    Task<ICameraStreamLease> AcquireAsync(LeaseRequest request, RtspConnectionInfo info, CancellationToken ct = default);
    Task ReleaseAsync(string cameraId, string leaseId);
    int GetRefCount(string cameraId);
    IReadOnlyDictionary<string, int> GetAllRefCounts();
}
