using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

/// <summary>
/// 이벤트 창 타일용 — 생산자 스레드에서 받은 프레임을 보관했다가 UI 스레드에서 한 번에 그린다.
/// 그리기 예약은 하나로 합친다(프레임이 몰려도 디스패처 대기열이 불어나지 않게).
/// UI 스레드에서 만들어야 한다.
/// </summary>
internal sealed class BitmapFrameSink : IFrameSink, IDisposable
{
    private readonly object _gate = new();
    private readonly Dispatcher _dispatcher;
    private readonly byte[] _pending;
    private bool _scheduled;
    private bool _disposed;
    private long _scheduledTicks; // 계측: 그리기 예약 시각(디스패처 넘김 대기)

    public BitmapFrameSink(int width, int height, Dispatcher dispatcher)
    {
        Width = width;
        Height = height;
        _dispatcher = dispatcher;
        _pending = new byte[width * height * 4];
        Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
    }

    public int Width { get; }
    public int Height { get; }
    public WriteableBitmap Bitmap { get; }

    public void Write(IntPtr bgra, int stride)
    {
        bool schedule;
        bool trace = StreamTrace.IsOn;
        long started = trace ? Stopwatch.GetTimestamp() : 0;
        lock (_gate)
        {
            if (_disposed) return;
            int row = Width * 4;
            for (int y = 0; y < Height; y++)
                Marshal.Copy(bgra + y * stride, _pending, y * row, row);
            schedule = !_scheduled;
            _scheduled = true;
            if (trace && schedule) _scheduledTicks = Stopwatch.GetTimestamp();
        }
        if (trace) StreamTrace.SinkWrite(Stopwatch.GetTimestamp() - started, coalesced: !schedule);
        if (schedule) _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Flush));
    }

    private void Flush()
    {
        lock (_gate)
        {
            _scheduled = false;
            if (_disposed) return;
            long started = StreamTrace.IsOn ? Stopwatch.GetTimestamp() : 0;
            Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), _pending, Width * 4, 0);
            if (started != 0) StreamTrace.Rendered(started - _scheduledTicks, Stopwatch.GetTimestamp() - started);
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; }
    }
}
