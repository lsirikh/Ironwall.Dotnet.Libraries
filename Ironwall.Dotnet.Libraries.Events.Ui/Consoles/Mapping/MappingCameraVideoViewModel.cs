using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 · ROI 창의 카메라 실시간 영상 상자 상태
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>영상 상자에 보이는 상태.</summary>
public enum MappingVideoStatus
{
    /// <summary>열지 않음 · 닫힘.</summary>
    Idle,
    /// <summary>연결 중(주소 조회 · RTSP 연결 · 호스트 재시작).</summary>
    Connecting,
    /// <summary>재생.</summary>
    Playing,
    /// <summary>프레임이 멈췄다(호스트가 다시 잇는 중).</summary>
    Stalled,
    /// <summary>연결 안 됨 — [다시 시도].</summary>
    Failed,
    /// <summary>호스트 없음 · 일시 중지 — 영상 기능을 쓸 수 없다(GIS 는 정상).</summary>
    Unavailable,
}

/// <summary>
/// 카메라 한 대의 실시간 영상 상자(프리셋 관리 창 · ROI 설정 창이 하나씩 든다).
/// </summary>
/// <remarks>
/// <para>영상은 <see cref="IMappingCameraVideo"/>(카메라 팝업 호스트)가 열고, 뷰의 <c>HostedVideoView</c> 가 <see cref="FrameSource"/> 를 그린다.
/// 여기는 상태 문구 · 다시 열기 · 닫기만 한다 — 헤드리스로 시험한다.</para>
/// <para>어떤 실패도 창으로 번지지 않는다 — 상자 안 문구로만 남는다.</para>
/// </remarks>
public sealed class MappingCameraVideoViewModel : PropertyChangedBase, IDisposable
{
    /// <summary>크기 변화 뒤 다시 열기까지 기다리는 시간 — 해상도 칸을 한 글자씩 고칠 때 한 번만 연다.</summary>
    public static readonly TimeSpan ReopenDebounce = TimeSpan.FromMilliseconds(400);

    private readonly IMappingCameraVideo _video;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly CancellationTokenSource _lifetime = new();
    private IFrameSource? _source;
    private (int Width, int Height) _size;
    private string _failMessage = string.Empty;
    private bool _started;
    private bool _disposed;
    private int _version;
    private MappingVideoStatus _status;

    /// <summary>생성자.</summary>
    /// <param name="video">영상 창구.</param>
    /// <param name="cameraId">카메라 id.</param>
    /// <param name="streamId">스트림 id(창마다 다르게 — 같은 id 로 열면 서로 교체한다).</param>
    /// <param name="delay">다시 열기 대기(시험 주입). 기본 <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public MappingCameraVideoViewModel(IMappingCameraVideo video, int cameraId, string streamId,
                                       Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _video = video ?? throw new ArgumentNullException(nameof(video));
        CameraId = cameraId;
        StreamId = streamId;
        _delay = delay ?? ((t, ct) => Task.Delay(t, ct));
        _video.HostStateChanged += OnHostStateChanged;
    }

    #region - 상태 -
    /// <summary>카메라 id.</summary>
    public int CameraId { get; }

    /// <summary>스트림 id.</summary>
    public string StreamId { get; }

    /// <summary>호스트가 쓰는 프레임(뷰가 그린다). 다시 열 때마다 바뀐다.</summary>
    public IFrameSource? FrameSource
    {
        get => _source;
        private set
        {
            if (ReferenceEquals(_source, value)) return;
            _source = value;
            NotifyOfPropertyChange();
        }
    }

    /// <summary>지금 열린(또는 요청한) 크기 — 시험 · 진단용.</summary>
    public (int Width, int Height) OpenedSize => _size;

    /// <summary>화면 상태.</summary>
    public MappingVideoStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(StatusText));
            NotifyOfPropertyChange(nameof(HasStatusText));
            NotifyOfPropertyChange(nameof(Headline));
            NotifyOfPropertyChange(nameof(IsPlaying));
            NotifyOfPropertyChange(nameof(CanRetry));
        }
    }

    /// <summary>상자 가운데 글(재생 중이면 빈 문자열).</summary>
    public string StatusText => _status switch
    {
        MappingVideoStatus.Connecting => "영상 연결 중…",
        MappingVideoStatus.Stalled => "영상이 멈췄습니다 — 다시 잇는 중…",
        MappingVideoStatus.Failed => string.IsNullOrEmpty(_failMessage) ? "영상을 열지 못했습니다." : _failMessage,
        MappingVideoStatus.Unavailable => "영상 기능을 쓸 수 없습니다.",
        _ => string.Empty,
    };

    /// <summary>가운데 글을 보일까.</summary>
    public bool HasStatusText => StatusText.Length > 0;

    /// <summary>상태 줄 글 — 재생 중이면 "실시간 영상", 아니면 <see cref="StatusText"/>.</summary>
    public string Headline => _status == MappingVideoStatus.Playing ? "실시간 영상" : StatusText;

    /// <summary>재생 중.</summary>
    public bool IsPlaying => _status == MappingVideoStatus.Playing;

    /// <summary>[다시 시도] 를 보일까.</summary>
    public bool CanRetry => _status is MappingVideoStatus.Failed or MappingVideoStatus.Stalled;
    #endregion

    #region - 동작 -
    /// <summary>그 크기로 연다(처음 한 번). 뒤에 다른 크기로 부르면 <see cref="Resize"/> 와 같다.</summary>
    public void Start(int width, int height)
    {
        if (_disposed) return;
        if (_started) { Resize(width, height); return; }
        _started = true;
        _size = (Math.Max(16, width), Math.Max(16, height));
        Open();
    }

    /// <summary>
    /// 크기(비율)가 바뀌었다 — 잠깐 기다렸다가 새 크기로 다시 연다. 거의 같으면(2% 안) 그대로 둔다.
    /// </summary>
    /// <returns>다시 열기 대기 — 시험이 기다릴 때 쓴다.</returns>
    public Task Resize(int width, int height)
    {
        if (_disposed || width <= 0 || height <= 0) return Task.CompletedTask;
        if (!_started) { Start(width, height); return Task.CompletedTask; }
        var next = (Math.Max(16, width), Math.Max(16, height));
        if (Near(_size, next)) return Task.CompletedTask;
        _size = next;
        var version = Interlocked.Increment(ref _version);
        return ReopenLaterAsync(version);
    }

    /// <summary>[다시 시도] — 지금 크기로 다시 연다.</summary>
    public void Retry()
    {
        if (_disposed || !_started) return;
        Open();
    }

    private async Task ReopenLaterAsync(int version)
    {
        try { await _delay(ReopenDebounce, _lifetime.Token); }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        if (_disposed || version != Volatile.Read(ref _version)) return;
        Open();
    }

    private void Open()
    {
        Interlocked.Increment(ref _version);    // 대기 중인 다시 열기는 무효
        try
        {
            // 같은 스트림 id 로 열면 감시자가 이전 것을 닫고 교체한다 — 옛 원천은 구독만 푼다.
            var old = _source;
            if (old is not null) old.StateChanged -= OnSourceStateChanged;

            var result = _video.OpenVideo(CameraId, StreamId, _size.Width, _size.Height);
            if (result.IsSuccess && result.Value is { } source)
            {
                _failMessage = string.Empty;
                source.StateChanged += OnSourceStateChanged;
                FrameSource = source;
            }
            else
            {
                _failMessage = result.Message;
                FrameSource = null;
            }
        }
        catch (Exception)
        {
            _failMessage = "영상을 열지 못했습니다.";
            FrameSource = null;
        }
        RefreshStatus();
    }

    /// <summary>닫는다 — 스트림을 닫고(공유 메모리 해제) 구독을 푼다. 여러 번 불러도 된다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _lifetime.Cancel(); } catch (ObjectDisposedException) { }
        _video.HostStateChanged -= OnHostStateChanged;
        var source = _source;
        if (source is not null)
        {
            source.StateChanged -= OnSourceStateChanged;
            try { source.Dispose(); } catch (Exception) { /* 닫는 길 — 무해 */ }
        }
        FrameSource = null;
        Status = MappingVideoStatus.Idle;
    }
    #endregion

    #region - 판정 -
    /// <summary>호스트 · 스트림 상태 → 화면 상태(순수).</summary>
    public static MappingVideoStatus Compute(bool started, MappingVideoHostState host, bool opened, StreamState? stream)
    {
        if (!started) return MappingVideoStatus.Idle;
        if (host == MappingVideoHostState.Unavailable) return MappingVideoStatus.Unavailable;
        if (!opened) return MappingVideoStatus.Failed;
        if (host == MappingVideoHostState.Starting) return MappingVideoStatus.Connecting;
        return stream switch
        {
            null or StreamState.Opening => MappingVideoStatus.Connecting,
            StreamState.Playing => MappingVideoStatus.Playing,
            StreamState.Stalled => MappingVideoStatus.Stalled,
            StreamState.Closed => MappingVideoStatus.Idle,
            _ => MappingVideoStatus.Failed,
        };
    }

    private void RefreshStatus()
    {
        try
        {
            var source = _source;
            var status = Compute(_started && !_disposed, _video.HostState, source is not null, source?.State);
            if (status == MappingVideoStatus.Failed && source is not null && string.IsNullOrEmpty(_failMessage))
                _failMessage = source.StateDetail == CameraErrorCodes.NotSupported
                    ? "이 카메라는 영상을 지원하지 않습니다."
                    : "영상을 열지 못했습니다. 카메라 주소 · 계정을 확인하세요.";
            if (status != MappingVideoStatus.Failed && source is not null) _failMessage = string.Empty;
            Status = status;
            NotifyOfPropertyChange(nameof(StatusText));
            NotifyOfPropertyChange(nameof(HasStatusText));
            NotifyOfPropertyChange(nameof(Headline));
        }
        catch (Exception) { /* 상태 표시는 창을 흔들지 않는다 */ }
    }

    private void OnSourceStateChanged(object? sender, EventArgs e) => RefreshStatus();

    private void OnHostStateChanged(object? sender, EventArgs e) => RefreshStatus();

    private static bool Near((int Width, int Height) a, (int Width, int Height) b)
    {
        if (a == b) return true;
        var ra = (double)a.Width / a.Height;
        var rb = (double)b.Width / b.Height;
        return Math.Abs(ra / rb - 1) <= 0.02 && Math.Abs(a.Width - b.Width) <= 32;
    }
    #endregion
}
