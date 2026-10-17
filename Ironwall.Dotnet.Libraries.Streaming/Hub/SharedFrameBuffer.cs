using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Streaming.Hub;

/// <summary>
/// VLC Lock/Unlock/Display 콜백 기반 인-프로세스 프레임 공유 버퍼.
/// 1 MediaPlayer → 1 WriteableBitmap → N Image.Source 바인딩으로 N개 뷰어가 같은 프레임을 공유.
///
/// 핵심 설계:
/// - GCHandle.Pinned: VLC Lock 콜백이 반환하는 포인터는 GC 이동이 금지된 관리 byte[] 주소
/// - Coalescing sentinel: 25fps × N 카메라의 Dispatcher 홍수를 방지 (Interlocked.Exchange)
/// - Dispose 순서(C-03): _disposed=1 → Stop(호출자) → GCHandle.Free (내부)
/// </summary>
internal sealed class SharedFrameBuffer : IDisposable
{
    private byte[]? _pixelData;
    private GCHandle _gcHandle;
    private IntPtr _pinnedPtr;

    private volatile WriteableBitmap? _bitmap;
    private int _width;
    private int _height;
    private int _stride;

    // Coalescing sentinel: 0=idle, 1=pending
    private int _pendingFrame;

    // Dispose guard: 0=alive, 1=disposed
    private int _disposed;


    /// <summary>공유 BitmapSource. 첫 번째 OnVideoFormat 호출 후 non-null.</summary>
    public BitmapSource? Bitmap => _bitmap;

    /// <summary>
    /// OnVideoFormat에서 WriteableBitmap 생성 완료 시 발화.
    /// _bitmap이 non-null로 설정된 직후 호출되므로 구독자는 Bitmap != null을 보장받는다.
    /// CameraStreamEntry.CreateAsync가 구독하여 Lease 발급 타이밍을 결정한다.
    /// </summary>
    public event Action? FrameFormatReady;

    // ── VLC SetVideoFormat 콜백 ───────────────────────────────────────────────

    /// <summary>
    /// VLC가 스트림 해상도를 결정할 때 호출.
    /// 버퍼 할당 + WriteableBitmap 생성 (or 재생성 시 resize).
    /// </summary>
    public uint OnVideoFormat(
        ref IntPtr opaque,
        IntPtr chroma,
        ref uint width, ref uint height,
        ref uint pitches, ref uint lines)
    {
        if (_disposed == 1) return 0;

        // chroma = "RV32" (BGRA32) — VLC가 사용할 픽셀 포맷 지정
        unsafe
        {
            byte* p = (byte*)chroma;
            p[0] = (byte)'R'; p[1] = (byte)'V'; p[2] = (byte)'3'; p[3] = (byte)'2';
        }

        int w = (int)width, h = (int)height, stride = w * 4;

        if (_bitmap == null || _width != w || _height != h)
        {
            // 기존 버퍼 해제
            if (_gcHandle.IsAllocated) _gcHandle.Free();

            _width = w; _height = h; _stride = stride;
            _pixelData = new byte[stride * h];
            _gcHandle = GCHandle.Alloc(_pixelData, GCHandleType.Pinned);
            _pinnedPtr = _gcHandle.AddrOfPinnedObject();

            // WriteableBitmap은 반드시 UI 스레드에서 생성
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher?.CheckAccess() == true)
            {
                _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            }
            else
            {
                dispatcher?.Invoke(() =>
                    _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null));
            }
        }

        pitches = (uint)_stride;
        lines = (uint)_height;

        // WriteableBitmap 생성 완료(_bitmap != null 보장) 후 구독자에게 알림
        FrameFormatReady?.Invoke();

        return 1; // 1 plane
    }

    public void OnVideoCleanup(IntPtr opaque) { }

    /// <summary>테스트 전용 — FrameFormatReady 이벤트 직접 발화 (프로덕션 코드에서 호출 금지).</summary>
    internal void FireFrameFormatReadyForTest() => FrameFormatReady?.Invoke();

    // ── VLC Lock 콜백 ─────────────────────────────────────────────────────────

    /// <summary>VLC가 프레임을 쓸 준비가 됐을 때 호출 — pinned 포인터 반환.</summary>
    public IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        if (_disposed == 1 || _pinnedPtr == IntPtr.Zero)
            return IntPtr.Zero;

        Marshal.WriteIntPtr(planes, _pinnedPtr);
        return IntPtr.Zero; // picture identifier (unused for single-buffer)
    }

    // ── VLC Unlock 콜백 ──────────────────────────────────────────────────────

    /// <summary>no-op: single plane, 별도 release 불필요.</summary>
    public void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    // ── VLC Display 콜백 ─────────────────────────────────────────────────────

    /// <summary>
    /// VLC가 프레임을 표시할 준비가 됐을 때 호출.
    /// Coalescing sentinel로 Dispatcher 홍수를 방지한다.
    /// </summary>
    public void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        if (_disposed == 1) return;

        // 이미 pending이면 이번 프레임은 다음 Render 사이클에 포함됨 (최신 프레임 우선)
        if (Interlocked.Exchange(ref _pendingFrame, 1) == 0)
        {
            Application.Current?.Dispatcher.InvokeAsync(
                FlushToWriteableBitmap,
                DispatcherPriority.Render);
        }
    }

    // ── UI 스레드에서 실행되는 프레임 flush ──────────────────────────────────

    private void FlushToWriteableBitmap()
    {
        if (_disposed == 1 || _bitmap == null || _pinnedPtr == IntPtr.Zero)
        {
            Interlocked.Exchange(ref _pendingFrame, 0);
            return;
        }

        try
        {
            _bitmap.Lock();
            try
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)_pinnedPtr,
                        (void*)_bitmap.BackBuffer,
                        _stride * _height,
                        _stride * _height);

                    // VLC RV32 4번째 바이트(padding)가 0인 경우 alpha=0으로 투명하게 보임.
                    // Bgra32로 선언했으므로 alpha 채널을 0xFF로 강제 설정.
                    uint* pixels = (uint*)_bitmap.BackBuffer;
                    int count = _width * _height;
                    for (int i = 0; i < count; i++)
                        pixels[i] |= 0xFF000000u;
                }
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            }
            finally
            {
                _bitmap.Unlock();
            }
        }
        catch
        {
            // VLC 스레드에서 Dispose가 경쟁하는 경우 silent ignore
        }
        finally
        {
            Interlocked.Exchange(ref _pendingFrame, 0); // sentinel 해제
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 반드시 호출자가 SetVideoCallbacks(null) + Stop() 을 완료한 뒤 호출할 것 (C-03).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // GCHandle 해제 (Stop 완료 후에만 호출되므로 콜백 재진입 없음)
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();

        _pinnedPtr = IntPtr.Zero;
        _pixelData = null;
    }
}
