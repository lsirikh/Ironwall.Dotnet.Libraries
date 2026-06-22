using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Hub;

/// <summary>
/// 카메라 스트림에 대한 단일 사용자의 점유권(Lease).
/// DisposeAsync() 호출 시 자동으로 Hub에 RefCount 감소를 위임한다.
/// </summary>
public interface ICameraStreamLease : IAsyncDisposable
{
    string LeaseId { get; }
    string CameraId { get; }
    LeaseRequest Request { get; }
    string RelayUrl { get; }

    /// <summary>
    /// 공유 프레임 BitmapSource. Hub가 VLC 콜백으로 채운 WriteableBitmap을 가리킨다.
    /// SharedFrameBuffer 기반 Entry에서만 non-null. 메타데이터-only Entry(테스트용)는 null 반환.
    /// </summary>
    BitmapSource? Frame { get; }

    event EventHandler<StreamHealthEventArgs>? HealthChanged;
}
