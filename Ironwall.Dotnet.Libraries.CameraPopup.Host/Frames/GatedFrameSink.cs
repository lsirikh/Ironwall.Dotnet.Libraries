namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

/// <summary>
/// 생산자 하나에게 빌려주는 싱크 — 타일 비트맵 싱크는 그대로 두고 생산자만 바꿀 때(자동 재시도 · "다시 시도") 쓴다.
/// <see cref="Detach"/> 뒤에 온 프레임은 버린다 — 물러난 생산자의 늦은 프레임이 새 영상 위에 그려지지 않는다.
/// 비트맵(WriteableBitmap)은 UI 스레드에서만 만들 수 있어, 재시도가 UI 를 기다리지 않으려면 싱크를 다시 쓴다(FR-40).
/// </summary>
internal sealed class GatedFrameSink : IFrameSink
{
    private volatile IFrameSink? _target;

    public GatedFrameSink(IFrameSink target)
    {
        _target = target;
        Width = target.Width;
        Height = target.Height;
    }

    public int Width { get; }
    public int Height { get; }

    public void Write(IntPtr bgra, int stride) => _target?.Write(bgra, stride);

    public void Detach() => _target = null;
}
