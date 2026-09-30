using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Wpf;

/// <summary>
/// 호스트가 공유 메모리로 넘긴 프레임을 그리는 WPF 표시기(견본 — 지도 오버레이 이관은 뒤 태스크).
/// 화면 갱신 틱(<see cref="CompositionTarget.Rendering"/>)마다 순번만 읽고, 바뀌었을 때만
/// <see cref="WriteableBitmap.BackBuffer"/> 로 바로 복사한다(중간 배열 없음).
/// 그리기 중 어떤 예외도 GIS 로 번지지 않는다 — 연속 실패가 쌓이면 스스로 갱신을 멈춘다.
/// <para>화면 갱신 구독은 약한 참조다(L6) — <see cref="CompositionTarget.Rendering"/> 은 정적 이벤트라 직접 구독하면
/// Unloaded 없이 떨어진 표시기(창이 Unloaded 없이 닫힘 등)를 영원히 붙잡는다. 시각 부모에서 떨어질 때도 구독을 푼다.</para>
/// </summary>
public class HostedVideoView : Image
{
    private const int MaxConsecutiveFailures = 20;

    public static readonly DependencyProperty FrameSourceProperty = DependencyProperty.Register(
        nameof(FrameSource), typeof(IFrameSource), typeof(HostedVideoView),
        new PropertyMetadata(null, (d, _) => ((HostedVideoView)d).OnFrameSourceChanged()));

    private WriteableBitmap? _bitmap;
    private long _lastSequence;
    private bool _hooked;
    private int _failures;

    public HostedVideoView()
    {
        Stretch = Stretch.Uniform;
        Loaded += (_, _) => UpdateHook();
        Unloaded += (_, _) => Unhook();
    }

    public IFrameSource? FrameSource
    {
        get => (IFrameSource?)GetValue(FrameSourceProperty);
        set => SetValue(FrameSourceProperty, value);
    }

    /// <summary>화면 갱신 구독 중인지(진단 · 시험용).</summary>
    internal bool IsHooked => _hooked;

    /// <summary>지금까지 그린 프레임 수(진단 · 시험용).</summary>
    public long FramesRendered { get; private set; }

    private void OnFrameSourceChanged()
    {
        _bitmap = null;
        _lastSequence = 0;
        _failures = 0;
        Source = null;
        UpdateHook();
    }

    private void UpdateHook()
    {
        if (FrameSource is not null && IsLoaded) Hook();
        else Unhook();
    }

    internal void Hook()
    {
        if (_hooked) return;
        RenderingRelay.Add(this);
        _hooked = true;
    }

    internal void Unhook()
    {
        if (!_hooked) return;
        RenderingRelay.Remove(this);
        _hooked = false;
    }

    /// <summary>시각 부모에서 떨어지면(Unloaded 가 오지 않는 경우 포함) 바로 구독을 푼다. 다시 붙으면 Loaded 가 되살린다.</summary>
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        if (VisualParent is null) Unhook();
        else UpdateHook();
    }

    /// <summary>
    /// 스레드(디스패처)마다 <see cref="CompositionTarget.Rendering"/> 을 한 번만 구독하고, 표시기는 약한 참조로 든다.
    /// 수거된 표시기는 다음 틱에 빠지고, 목록이 비면 구독도 푼다.
    /// </summary>
    private static class RenderingRelay
    {
        [ThreadStatic] private static List<WeakReference<HostedVideoView>>? _views;

        public static void Add(HostedVideoView view)
        {
            var views = _views;
            if (views is null)
            {
                views = _views = new List<WeakReference<HostedVideoView>>();
                CompositionTarget.Rendering += OnRendering;
            }
            views.Add(new WeakReference<HostedVideoView>(view));
        }

        public static void Remove(HostedVideoView view)
        {
            var views = _views;
            if (views is null) return;
            views.RemoveAll(w => !w.TryGetTarget(out var v) || ReferenceEquals(v, view));
            if (views.Count == 0) Detach();
        }

        private static void Detach()
        {
            CompositionTarget.Rendering -= OnRendering;
            _views = null;
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            var views = _views;
            if (views is null) return;
            foreach (var weak in views.ToArray())
            {
                if (weak.TryGetTarget(out var view)) view.RenderLatest();
                else views.Remove(weak);
            }
            if (views.Count == 0 && ReferenceEquals(views, _views)) Detach();
        }
    }

    /// <summary>최신 프레임을 한 번 그린다(UI 스레드). 새 프레임을 그렸으면 true.</summary>
    public bool RenderLatest()
    {
        try
        {
            var source = FrameSource;
            if (source is null) return false;
            long sequence = source.PublishedSequence;
            if (sequence <= 0 || sequence == _lastSequence) return false;

            if (_bitmap is null || _bitmap.PixelWidth != source.Width || _bitmap.PixelHeight != source.Height)
            {
                _bitmap = new WriteableBitmap(source.Width, source.Height, 96, 96, PixelFormats.Bgr32, null);
                Source = _bitmap;
            }

            bool copied;
            long got;
            _bitmap.Lock();
            try
            {
                copied = source.TryCopyLatest(_bitmap.BackBuffer, _bitmap.BackBufferStride,
                    (long)_bitmap.BackBufferStride * _bitmap.PixelHeight, out got);
                if (copied) _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
            }
            finally
            {
                _bitmap.Unlock();
            }
            if (!copied) return false;
            _lastSequence = got;
            _failures = 0;
            FramesRendered++;
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[CameraPopup] HostedVideoView render failed: {ex.GetType().Name} {ex.Message}");
            if (++_failures >= MaxConsecutiveFailures) Unhook();
            return false;
        }
    }
}
