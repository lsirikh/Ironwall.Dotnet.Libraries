using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using LibVLCSharp.Shared;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 영상 콜백으로 요청 크기(상자 · 타일 픽셀)의 BGRA 프레임을 만들어 싱크에 쓴다. 창 · GPU 공유 없음.
/// <para><b>비율 유지(T-02)</b>: 포맷 콜백에서 원본 크기를 받아 요청 상자 안에 맞는 크기로 디코딩시키고(<see cref="VideoFit"/>),
/// 남는 곳은 검은 띠로 채운 상자 크기 프레임을 낸다 — 옛 플레이어의 Stretch=Uniform 과 같은 모양. 싱크 크기는 그대로다.</para>
/// 스테이징 버퍼 3개를 돌려 쓰며 lock → display 순으로 식별자를 넘긴다. 콜백 안의 예외는 네이티브 경계를 넘으면
/// 프로세스가 죽으므로 모두 잡는다. 열기 제한 시간(FR-26) 안에 첫 프레임이 없으면 Failed.
/// </summary>
internal sealed class LibVlcFrameProducer : IFrameProducer
{
    private const int StagingCount = 3;
    private const int DefaultOpenTimeoutMs = 10_000;
    private const uint Rv32 = 0x32335652; // "RV32" little-endian

    private readonly VideoProviderInfo _provider;
    private readonly int _width;
    private readonly int _height;
    private readonly HostLog _log;
    private readonly string _name;
    private readonly long _openRequestedTicks;
    private readonly IntPtr[] _staging = new IntPtr[StagingCount];
    private readonly object _gate = new();

    // 네이티브가 들고 있는 대리자 — GC 가 거두지 않게 필드로 붙잡는다.
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;
    private readonly MediaPlayer.LibVLCVideoFormatCb _formatCb;
    private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;

    private MediaPlayer? _player;
    private Media? _media;
    private IFrameSink? _sink;
    private Action<StreamState, string?>? _onState;
    private Timer? _openTimer;
    private int _lockCounter;
    private long _frames;
    private volatile bool _stopped;

    // 포맷 콜백이 정한 디코딩 크기 · 상자 안 자리(vout 스레드에서만 쓰고 읽는다).
    private int _fitWidth;
    private int _fitHeight;
    private int _offsetX;
    private int _offsetY;
    private IntPtr _compose;
    private bool _composeDirty;

    /// <param name="openRequestedTicks">열기 요청 시각(<see cref="Environment.TickCount64"/>) — 첫 프레임까지 걸린 시간 기록용. 0 이면 생성 시각.</param>
    public LibVlcFrameProducer(VideoProviderInfo provider, int width, int height, string name, HostLog log, long openRequestedTicks = 0)
    {
        _provider = provider;
        _width = width;
        _height = height;
        _name = name;
        _log = log;
        _openRequestedTicks = openRequestedTicks > 0 ? openRequestedTicks : Environment.TickCount64;
        _lockCb = OnLock;
        _displayCb = OnDisplay;
        _formatCb = OnFormat;
        _cleanupCb = OnCleanup;
    }

    public void Start(IFrameSink sink, Action<StreamState, string?> onState)
    {
        _sink = sink;
        _onState = onState;
        // 초기화 · Play 는 배경 스레드에서 — 파이프 처리 줄을 붙잡지 않는다.
        _ = Task.Run(StartCore);
    }

    private void StartCore()
    {
        lock (_gate)
        {
            StartLocked();
        }
    }

    private void StartLocked()
    {
        try
        {
            if (_stopped) return;
            if (string.IsNullOrWhiteSpace(_provider.Uri))
            {
                Report(StreamState.Failed, "empty-uri");
                return;
            }

            var libVlc = LibVlcRuntime.Get(_log);
            _compose = Marshal.AllocHGlobal(_width * _height * 4);

            _media = _provider.Kind == VideoProviderKind.File
                ? new Media(libVlc, _provider.Uri!, FromType.FromPath)
                : new Media(libVlc, new Uri(_provider.Uri!));
            if (_provider.Kind == VideoProviderKind.File)
            {
                _media.AddOption(":image-duration=-1");
                _media.AddOption(":input-repeat=65535");
            }
            else
            {
                _media.AddOption(":rtsp-tcp");
                _media.AddOption(":network-caching=300");
                if (!string.IsNullOrEmpty(_provider.Username)) _media.AddOption($":rtsp-user={_provider.Username}");
                if (!string.IsNullOrEmpty(_provider.Password)) _media.AddOption($":rtsp-pwd={_provider.Password}");
            }

            _player = new MediaPlayer(_media) { EnableHardwareDecoding = false };
            _player.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
            _player.SetVideoCallbacks(_lockCb, null, _displayCb);
            _player.EncounteredError += (_, _) => Report(StreamState.Failed, "libvlc-error");
            _player.EndReached += (_, _) => Report(StreamState.Stalled, "end-reached");

            int timeout = _provider.OpenTimeoutMs > 0 ? _provider.OpenTimeoutMs : DefaultOpenTimeoutMs;
            _openTimer = new Timer(_ =>
            {
                if (Interlocked.Read(ref _frames) == 0 && !_stopped) Report(StreamState.Failed, "open-timeout");
            }, null, timeout, Timeout.Infinite);

            if (_stopped) return;
            if (!_player.Play()) Report(StreamState.Failed, "play-failed");
            _log.Info($"libvlc play {_name} {_provider} box={_width}x{_height}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"libvlc start failed {_name}: {ex.GetType().Name} {VideoProviderInfo.RedactUri(ex.Message)}");
            Report(StreamState.Failed, "libvlc-start-failed");
        }
    }

    /// <summary>원본 크기가 정해지면(또는 바뀌면) 불린다 — 상자에 맞춘 디코딩 크기 · 스테이징을 정한다.</summary>
    private uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        try
        {
            var (fw, fh) = VideoFit.Fit((int)width, (int)height, _width, _height);
            Marshal.WriteInt32(chroma, unchecked((int)Rv32));
            width = (uint)fw;
            height = (uint)fh;
            pitches = (uint)(fw * 4);
            lines = (uint)fh;
            FreeStaging();
            for (int i = 0; i < StagingCount; i++) _staging[i] = Marshal.AllocHGlobal(fw * fh * 4);
            _fitWidth = fw;
            _fitHeight = fh;
            _offsetX = (_width - fw) / 2;
            _offsetY = (_height - fh) / 2;
            _composeDirty = true;
            _log.Info($"libvlc format {_name} source→fit {fw}x{fh} in box {_width}x{_height}");
            return StagingCount;
        }
        catch (Exception ex)
        {
            _log.Warn($"format callback failed {_name}: {ex.Message}");
            return 0;
        }
    }

    private void OnCleanup(ref IntPtr opaque)
    {
        try { FreeStaging(); }
        catch (Exception ex) { _log.Warn($"cleanup callback failed {_name}: {ex.Message}"); }
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        try
        {
            int index = (int)((uint)Interlocked.Increment(ref _lockCounter) % StagingCount);
            Marshal.WriteIntPtr(planes, _staging[index]);
            return new IntPtr(index + 1); // 0 이 아닌 식별자
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private unsafe void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        try
        {
            if (_stopped) return;
            int index = picture.ToInt32() - 1;
            if (index < 0 || index >= StagingCount || _staging[index] == IntPtr.Zero) return;
            var sink = _sink;
            if (sink is null) return;

            if (_fitWidth == _width && _fitHeight == _height)
            {
                sink.Write(_staging[index], _width * 4);
            }
            else
            {
                // 상자 크기 프레임 = 검은 바탕 + 가운데 영상(비율 유지).
                var compose = (byte*)_compose;
                if (_composeDirty)
                {
                    new Span<uint>(compose, _width * _height).Fill(0xFF000000u);
                    _composeDirty = false;
                }
                var src = (byte*)_staging[index];
                int rowBytes = _fitWidth * 4;
                for (int y = 0; y < _fitHeight; y++)
                {
                    Buffer.MemoryCopy(src + (long)y * rowBytes,
                        compose + ((long)(y + _offsetY) * _width + _offsetX) * 4,
                        rowBytes, rowBytes);
                }
                sink.Write(_compose, _width * 4);
            }

            if (Interlocked.Increment(ref _frames) == 1)
            {
                _log.Info($"libvlc first frame {_name} after {Environment.TickCount64 - _openRequestedTicks} ms");
                Report(StreamState.Playing, null);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"display callback failed {_name}: {ex.Message}");
        }
    }

    private void FreeStaging()
    {
        for (int i = 0; i < StagingCount; i++)
        {
            if (_staging[i] != IntPtr.Zero) { Marshal.FreeHGlobal(_staging[i]); _staging[i] = IntPtr.Zero; }
        }
    }

    private void Report(StreamState state, string? detail)
    {
        try { _onState?.Invoke(state, detail); }
        catch (Exception ex) { _log.Warn($"state report failed {_name}: {ex.Message}"); }
    }

    public void Dispose()
    {
        MediaPlayer? player;
        Media? media;
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _openTimer?.Dispose();
            player = _player;
            media = _media;
        }
        // Stop 은 LibVLC 스레드와 합류하느라 막힐 수 있다 → 배경에서, 제한 시간 뒤에는 버퍼를 풀지 않고 포기(누수 < 충돌).
        var stop = Task.Run(() =>
        {
            player?.Stop();
            player?.Dispose();
            media?.Dispose();
        });
        if (stop.Wait(TimeSpan.FromSeconds(3)))
        {
            FreeStaging();
            if (_compose != IntPtr.Zero) { Marshal.FreeHGlobal(_compose); _compose = IntPtr.Zero; }
        }
        else
        {
            _log.Warn($"libvlc stop timed out {_name} — staging buffers leaked on purpose");
        }
    }
}

/// <summary>비율 유지 맞춤(순수) — 원본을 상자 안에 가장 크게. 짝수로 내린다(일부 크로마 변환기가 홀수 폭을 싫어한다).</summary>
internal static class VideoFit
{
    public static (int Width, int Height) Fit(int sourceWidth, int sourceHeight, int boxWidth, int boxHeight)
    {
        if (boxWidth <= 0 || boxHeight <= 0) return (Math.Max(2, boxWidth), Math.Max(2, boxHeight));
        if (sourceWidth <= 0 || sourceHeight <= 0) return (boxWidth, boxHeight);
        double scale = Math.Min(boxWidth / (double)sourceWidth, boxHeight / (double)sourceHeight);
        int w = Math.Clamp((int)Math.Floor(sourceWidth * scale), 2, boxWidth) & ~1;
        int h = Math.Clamp((int)Math.Floor(sourceHeight * scale), 2, boxHeight) & ~1;
        // 맞춘 크기가 상자와 1~2 px 차이면 상자 크기로(띠가 한두 줄 생기는 것 방지 — 짝수 내림 오차).
        if (boxWidth - w <= 2 && (boxWidth & 1) == 0) w = boxWidth;
        if (boxHeight - h <= 2 && (boxHeight & 1) == 0) h = boxHeight;
        return (Math.Max(2, w), Math.Max(2, h));
    }
}
