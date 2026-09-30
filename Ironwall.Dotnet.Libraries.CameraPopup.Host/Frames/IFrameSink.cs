namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

/// <summary>생산자가 만든 BGRA(32bpp) 프레임을 받는 곳 — 공유 메모리(오버레이) 또는 창 타일 비트맵.</summary>
internal interface IFrameSink
{
    int Width { get; }
    int Height { get; }

    /// <summary>생산자 스레드에서 불린다. 빨리 끝나야 한다.</summary>
    void Write(IntPtr bgra, int stride);
}
