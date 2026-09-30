using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

/// <summary>오버레이용 — GIS 가 만든 공유 메모리에 프레임을 쓴다(seqlock 이중 버퍼).</summary>
internal sealed class SharedMemoryFrameSink : IFrameSink, IDisposable
{
    private readonly SharedFrameView _view;

    public SharedMemoryFrameSink(SharedFrameView view) => _view = view;

    public int Width => _view.Layout.Width;
    public int Height => _view.Layout.Height;

    public void Write(IntPtr bgra, int stride) => _view.WriteFrame(bgra, stride);

    public void Dispose() => _view.Dispose();
}
