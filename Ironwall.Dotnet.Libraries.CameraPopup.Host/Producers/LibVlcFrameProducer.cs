using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using LibVLCSharp.Shared;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 영상 콜백(<c>SetVideoFormat("RV32")</c> + <c>SetVideoCallbacks</c>)으로 요청 크기의 BGRA 프레임을 받아
/// 싱크에 쓴다. 창 · GPU 공유 없음. 스테이징 버퍼 3개를 돌려 쓰며 lock → display 순으로 식별자를 넘긴다.
/// 콜백 안의 예외는 네이티브 경계를 넘으면 프로세스가 죽으므로 모두 잡는다.
/// 열기 제한 시간(FR-26) 안에 첫 프레임이 없으면 Failed.
/// </summary>
internal sealed class LibVlcFrameProducer : IFrameProducer
{
    private const int StagingCount = 3;
    private const int DefaultOpenTimeoutMs = 10_000;

    private readonly VideoProviderInfo _provider;
    private readonly int _width;
    private readonly int _height;
    private readonly HostLog _log;
    private readonly string _name;
    private readonly IntPtr[] _staging = new IntPtr[StagingCount];
    private readonly object _gate = new();

    // 네이티브가 들고 있는 대리자 — GC 가 거두지 않게 필드로 붙잡는다.
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;

    private MediaPlayer? _player;
    private Media? _media;
    private IFrameSink? _sink;
    private Action<StreamState, string?>? _onState;
    private Timer? _openTimer;
    private int _lockCounter;
    private long _frames;
    private volatile bool _stopped;

    public LibVlcFrameProducer(VideoProviderInfo provider, int width, int height, string name, HostLog log)
    {
        _provider = provider;
        _width = width;
        _height = height;
        _name = name;
        _log = log;
        _lockCb = OnLock;
        _displayCb = OnDisplay;
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
            if (_provider.Kind == VideoProviderKind.Onvif)
            {
                Report(StreamState.Failed, "onvif-provider-not-implemented");
                return;
            }
            if (string.IsNullOrWhiteSpace(_provider.Uri))
            {
                Report(StreamState.Failed, "empty-uri");
                return;
            }

            var libVlc = LibVlcRuntime.Get(_log);
            for (int i = 0; i < StagingCount; i++) _staging[i] = Marshal.AllocHGlobal(_width * _height * 4);

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
            _player.SetVideoFormat("RV32", (uint)_width, (uint)_height, (uint)(_width * 4));
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
            _log.Info($"libvlc play {_name} {_provider} {_width}x{_height}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error($"libvlc start failed {_name}: {ex.GetType().Name} {ex.Message}");
            Report(StreamState.Failed, "libvlc-start-failed");
        }
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

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        try
        {
            if (_stopped) return;
            int index = picture.ToInt32() - 1;
            if (index < 0 || index >= StagingCount) return;
            _sink?.Write(_staging[index], _width * 4);
            if (Interlocked.Increment(ref _frames) == 1) Report(StreamState.Playing, null);
        }
        catch (Exception ex)
        {
            _log.Warn($"display callback failed {_name}: {ex.Message}");
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
            for (int i = 0; i < StagingCount; i++)
            {
                if (_staging[i] != IntPtr.Zero) { Marshal.FreeHGlobal(_staging[i]); _staging[i] = IntPtr.Zero; }
            }
        }
        else
        {
            _log.Warn($"libvlc stop timed out {_name} — staging buffers leaked on purpose");
        }
    }
}
