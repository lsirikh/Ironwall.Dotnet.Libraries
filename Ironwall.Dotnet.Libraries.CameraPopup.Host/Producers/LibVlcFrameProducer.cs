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
/// <para><b>줄 서기 · 실패 뒤 정리(T-09 T7)</b>: 연결은 <see cref="StreamOpenGate"/> 자리를 얻은 뒤에 시작하고(한꺼번에 60개가 몰리지 않게),
/// 자리는 첫 프레임 · 실패 · 닫기 때 돌려준다. 실패(열기 시간 초과 · LibVLC 오류 · 끝)하면 <b>플레이어를 바로 멈춘다</b> —
/// 멈추지 않은 실패 플레이어는 RTSP 스레드가 계속 돌며 코어 하나씩을 태웠다(실측: 실패 29개 ≈ 바쁜 스레드 27개).
/// 다시 열기는 새 생산자로 한다(창 세션의 자동 재시도).</para>
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
    private readonly StreamOpenGate? _openGate;
    private readonly bool _priority;
    private readonly int _attempt;
    private readonly CancellationTokenSource _cts = new();
    private StreamOpenGate.Lease? _lease;
    private bool _tornDown;
    private int _failed;

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
    /// <param name="openGate">연결 줄(없으면 바로 연다 — 파일 · 시험).</param>
    /// <param name="priority">줄 앞에 선다(사람이 방금 연 오버레이).</param>
    /// <param name="attempt">몇 번째 재시도인가(0 = 첫 시도) — 재시도는 소프트웨어 디코딩으로(<see cref="DecodeProfile"/>).</param>
    public LibVlcFrameProducer(VideoProviderInfo provider, int width, int height, string name, HostLog log, long openRequestedTicks = 0,
        StreamOpenGate? openGate = null, bool priority = false, int attempt = 0)
    {
        _openGate = openGate;
        _priority = priority;
        _attempt = attempt;
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
        _ = Task.Run(StartAsync);
    }

    private async Task StartAsync()
    {
        try
        {
            if (_openGate is not null)
            {
                if (_openGate.Active >= _openGate.MaxConcurrent) Report(StreamState.Opening, DetailQueued);
                var lease = await _openGate.EnterAsync(_priority, _cts.Token).ConfigureAwait(false);
                bool keep;
                lock (_gate)
                {
                    keep = !_stopped;
                    if (keep) _lease = lease;
                }
                if (!keep)
                {
                    lease.Dispose();
                    return;
                }
            }
            lock (_gate)
            {
                StartLocked();
            }
        }
        catch (OperationCanceledException)
        {
            // 줄 서 있다가 닫혔다.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"libvlc start failed {_name}: {ex.GetType().Name} {VideoProviderInfo.RedactUri(ex.Message)}");
            Fail(StreamState.Failed, "libvlc-start-failed");
        }
    }

    /// <summary>스트림 상태 Detail — 연결 줄에서 차례를 기다리는 중.</summary>
    public const string DetailQueued = "queued";

    private void ReleaseLease()
    {
        StreamOpenGate.Lease? lease;
        lock (_gate)
        {
            lease = _lease;
            _lease = null;
        }
        lease?.Dispose();
    }

    /// <summary>실패를 한 번만 알리고, 자리를 돌려주고, 플레이어를 배경에서 멈춘다(실패한 플레이어가 CPU 를 태우지 않게).</summary>
    private void Fail(StreamState state, string detail)
    {
        if (_stopped || Interlocked.Exchange(ref _failed, 1) == 1) return;
        ReleaseLease();
        Report(state, detail);
        TearDown(wait: false);
    }

    private void StartLocked()
    {
        try
        {
            if (_stopped) return;
            if (string.IsNullOrWhiteSpace(_provider.Uri))
            {
                _ = Task.Run(() => Fail(StreamState.Failed, "empty-uri"));
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
                // RTSP over TCP(들어오는 포트를 열지 않는다) · 상자 크기에 맞춘 디코딩 옵션.
                foreach (var option in DecodeProfile.MediaOptions(_width, _height, _attempt)) _media.AddOption(option);
                if (!string.IsNullOrEmpty(_provider.Username)) _media.AddOption($":rtsp-user={_provider.Username}");
                if (!string.IsNullOrEmpty(_provider.Password)) _media.AddOption($":rtsp-pwd={_provider.Password}");
            }

            // 하드웨어 디코딩 여부는 미디어 옵션(:avcodec-hw)이 정한다 — 속성은 끈 채로 둔다(켜면 LibVLCSharp 가 옵션을 덮어쓴다).
            _player = new MediaPlayer(_media) { EnableHardwareDecoding = false };
            _player.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
            _player.SetVideoCallbacks(_lockCb, null, _displayCb);
            _player.EncounteredError += (_, _) => Fail(StreamState.Failed, "libvlc-error");
            _player.EndReached += (_, _) => Fail(StreamState.Stalled, "end-reached");

            int timeout = _provider.OpenTimeoutMs > 0 ? _provider.OpenTimeoutMs : DefaultOpenTimeoutMs;
            _openTimer = new Timer(_ =>
            {
                if (Interlocked.Read(ref _frames) == 0 && !_stopped) Fail(StreamState.Failed, "open-timeout");
            }, null, timeout, Timeout.Infinite);

            if (_stopped) return;
            if (!_player.Play())
            {
                // 잠금 안 — 멈추기는 배경에서(Fail 이 다시 잠금을 잡는다).
                _ = Task.Run(() => Fail(StreamState.Failed, "play-failed"));
                return;
            }
            _log.Info($"libvlc play {_name} {_provider} box={_width}x{_height} attempt={_attempt} hw={DecodeProfile.UsesHardware(_width, _height, _attempt)}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"libvlc start failed {_name}: {ex.GetType().Name} {VideoProviderInfo.RedactUri(ex.Message)}");
            _ = Task.Run(() => Fail(StreamState.Failed, "libvlc-start-failed"));
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
                ReleaseLease();
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
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        ReleaseLease();
        TearDown(wait: true);
    }

    /// <summary>
    /// 플레이어를 멈추고 버퍼를 푼다(한 번만). Stop 은 LibVLC 스레드와 합류하느라 막힐 수 있어 늘 배경에서 한다 —
    /// LibVLC 이벤트 스레드에서 직접 Stop 하면 교착이다. <paramref name="wait"/> 가 참이면 3초까지 기다린다(닫기).
    /// 제한 시간 안에 못 멈추면 버퍼는 멈춘 뒤에 풀린다(멈추지 않으면 풀지 않는다 — 누수 &lt; 충돌).
    /// </summary>
    private void TearDown(bool wait)
    {
        MediaPlayer? player;
        Media? media;
        lock (_gate)
        {
            _stopped = true;
            if (_tornDown) return;
            _tornDown = true;
            _openTimer?.Dispose();
            player = _player;
            media = _media;
            _player = null;
            _media = null;
        }
        var stop = Task.Run(() =>
        {
            player?.Stop();
            player?.Dispose();
            media?.Dispose();
        });
        var free = stop.ContinueWith(t =>
        {
            if (t.IsFaulted) _log.Warn($"libvlc stop failed {_name}: {t.Exception?.GetBaseException().Message}");
            FreeStaging();
            if (_compose != IntPtr.Zero) { Marshal.FreeHGlobal(_compose); _compose = IntPtr.Zero; }
        }, TaskScheduler.Default);
        if (wait && !free.Wait(TimeSpan.FromSeconds(3)))
            _log.Warn($"libvlc stop timed out {_name} — buffers are freed when it finishes");
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
