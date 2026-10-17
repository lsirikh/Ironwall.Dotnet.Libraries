using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 없이 움직이는 시험 무늬를 그린다(25fps). 생존 시험 · 진단용.
/// 무늬: 스트림별 배경색 + 가로로 움직이는 흰 막대 + 왼쪽 위 32칸 이진 프레임 번호(칸 8px).
/// </summary>
internal sealed class TestPatternProducer : IFrameProducer
{
    private const int FrameIntervalMs = 40;
    private readonly object _gate = new();
    private readonly uint _background;
    private Timer? _timer;
    private IFrameSink? _sink;
    private Action<StreamState, string?>? _onState;
    private IntPtr _buffer;
    private long _frame;
    private bool _disposed;

    public TestPatternProducer(string seed)
    {
        int h = StringComparer.Ordinal.GetHashCode(seed ?? string.Empty);
        _background = 0xFF000000u | (uint)((h & 0x3F3F3F) + 0x202020);
    }

    public void Start(IFrameSink sink, Action<StreamState, string?> onState)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _sink = sink;
            _onState = onState;
            _buffer = Marshal.AllocHGlobal(sink.Width * sink.Height * 4);
            _timer = new Timer(Tick, null, 0, FrameIntervalMs);
        }
    }

    private unsafe void Tick(object? state)
    {
        if (!Monitor.TryEnter(_gate)) return; // 앞 프레임이 아직이면 건너뛴다
        try
        {
            if (_disposed || _sink is null) return;
            int w = _sink.Width, h = _sink.Height;
            long n = ++_frame;
            uint* px = (uint*)_buffer;
            new Span<uint>(px, w * h).Fill(_background);
            int barX = (int)((n * 8) % Math.Max(1, w));
            int barW = Math.Min(16, w - barX);
            for (int y = 0; y < h; y++)
                new Span<uint>(px + (long)y * w + barX, barW).Fill(0xFFFFFFFFu);
            for (int bit = 0; bit < 32; bit++)
            {
                uint color = ((n >> bit) & 1) != 0 ? 0xFF00FF00u : 0xFF000000u;
                int bx = bit * 8;
                if (bx + 8 > w || 8 > h) break;
                for (int y = 0; y < 8; y++)
                    new Span<uint>(px + (long)y * w + bx, 8).Fill(color);
            }
            _sink.Write(_buffer, w * 4);
            if (n == 1) _onState?.Invoke(StreamState.Playing, "test-pattern");
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    public void Dispose()
    {
        Timer? timer;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            timer = _timer;
            _timer = null;
        }
        if (timer is not null)
        {
            using var done = new ManualResetEvent(false);
            if (timer.Dispose(done)) done.WaitOne(TimeSpan.FromSeconds(1));
        }
        lock (_gate)
        {
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }
        }
    }
}
