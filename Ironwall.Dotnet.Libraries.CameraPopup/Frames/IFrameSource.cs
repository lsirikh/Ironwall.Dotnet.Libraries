using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Frames;

/// <summary>
/// 호스트가 공유 메모리에 쓰는 오버레이 프레임을 읽는 쪽(GIS). 공유 메모리는 GIS 가 소유하므로
/// 호스트가 죽어도 이 객체는 유효하다(마지막 프레임이 남아 있고, 재시작 뒤 순번이 이어서 오른다).
/// 모든 멤버는 예외를 던지지 않는다. <see cref="IDisposable.Dispose"/> 는 오버레이 닫기와 같다.
/// </summary>
public interface IFrameSource : IDisposable
{
    string StreamId { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>BGRA 32bpp 행 간격(바이트) = Width × 4.</summary>
    int Stride { get; }

    /// <summary>마지막으로 다 쓴 프레임 순번(0 = 아직 없음, -1 = 닫힘). 값이 바뀌었을 때만 복사하면 된다(폴링).</summary>
    long PublishedSequence { get; }

    StreamState State { get; }
    string? StateDetail { get; }

    /// <summary>호스트가 보낸 스트림 상태가 바뀌면(배경 스레드에서) 알린다.</summary>
    event EventHandler? StateChanged;

    /// <summary>최신 프레임을 대상 버퍼(예: WriteableBitmap.BackBuffer)로 복사. 온전한 복사가 아니면 false.</summary>
    bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence);
}
