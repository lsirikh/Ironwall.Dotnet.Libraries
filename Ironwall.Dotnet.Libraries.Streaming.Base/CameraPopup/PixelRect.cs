namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 정수 픽셀 사각형 — 모니터 · 작업 영역 · 창 자리 (WPF 무관)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>물리 픽셀 사각형. <see cref="Right"/> · <see cref="Bottom"/> 은 배타 경계(Win32 RECT 와 같다).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary><paramref name="inner"/> 가 통째로 이 안에 있는가.</summary>
    public bool Contains(PixelRect inner)
        => inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;

    public PixelRect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };
}
