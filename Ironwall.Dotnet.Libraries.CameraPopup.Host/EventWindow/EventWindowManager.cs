using System.Windows;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 관리. 이벤트 1건 = 창 1개 — 같은 키가 다시 오면 앞으로 가져온다(FR-09).
/// 헤드리스 모드에서도 창 상태(VM) · 타일 스트림 · 타이머는 그대로 돌고 창(뷰)만 만들지 않는다 — 생존 시험이 실제 경로를 탄다.
/// 창 수 한도 · 오래된 창 정리는 GIS 쪽 창 관리자 몫 — 호스트는 받은 명령만 따른다.
/// 창 하나의 실패(뷰 생성 · 스트림 · PTZ)는 그 창 · 그 타일에만 머문다(FR-26).
/// <para><b>스레드(FR-40)</b>: 창 열기 · 닫기 · 앞으로 · 테마(<see cref="Open"/> · <see cref="Close"/> · <see cref="BringToFront"/> ·
/// <see cref="SetTheme"/>)는 UI 스레드에서 부른다. <b>타일 복구는 UI 스레드를 타지 않는다</b> — 재시도 일정(실패 → 2·4·8·16·30초)은
/// 생산자 스레드가 적고, 스레드 풀 타이머가 때가 된 타일의 생산자를 새로 만든다(타일 비트맵 싱크는 다시 쓴다). UI 에는 타일 상태 글자 ·
/// 남은 시간 같은 최소 갱신만 Normal 우선순위로 넣는다. UI 가 그리기 · 입력으로 바쁘거나 잠깐 멈춰도 영상은 제때 다시 붙는다
/// (K7 실측: Background 타이머가 10분 밀려 끊긴 타일 24개가 돌아오지 않았다).</para>
/// </summary>
internal sealed class EventWindowManager : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>타일 상태 · 주기 갱신을 UI 에 넣는 우선순위 — Background 이하로 내리지 않는다(밀리면 화면이 옛 상태로 남는다).</summary>
    internal const DispatcherPriority UiUpdatePriority = DispatcherPriority.Normal;

    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Dispatcher _dispatcher;
    private readonly IFrameProducerFactory _producers;
    private readonly ITileCameraControlFactory _controls;
    private readonly IHostClock _clock;
    private readonly Action<IIpcMessage> _send;
    private readonly HostLog _log;
    private readonly bool _headless;

    public EventWindowManager(Dispatcher dispatcher, FrameProducerFactory producers, Action<IIpcMessage> send, HostLog log, bool headless)
        : this(dispatcher, producers, new TileCameraControlFactory(producers.Cameras), SystemHostClock.Instance, send, log, headless)
    {
    }

    internal EventWindowManager(Dispatcher dispatcher, IFrameProducerFactory producers, ITileCameraControlFactory controls, IHostClock clock,
        Action<IIpcMessage> send, HostLog log, bool headless)
    {
        _dispatcher = dispatcher;
        _producers = producers;
        _controls = controls;
        _clock = clock;
        _send = send;
        _log = log;
        _headless = headless;
    }

    /// <summary>열린 창 수 — 어느 스레드에서 읽어도 대략값이면 된다(심박 보고용).</summary>
    public int Count { get; private set; }

    private int _streamCount;

    /// <summary>열려 있는 타일 스트림 수(메모리 한도 · 심박 보고용). 어느 스레드에서나 읽는다.</summary>
    public int StreamCount => Volatile.Read(ref _streamCount);

    // ───────── 주기 일(스레드 풀) ─────────

    private Session[] _snapshot = Array.Empty<Session>();   // 타이머 스레드가 읽는 창 목록(UI 스레드가 바꿔 끼운다)
    private Timer? _timer;
    private int _timerBusy;
    private int _uiTickPending;
    private volatile bool _disposed;

    private void SessionsChanged()
    {
        Count = _sessions.Count;
        Volatile.Write(ref _snapshot, _sessions.Values.ToArray());
        if (_timer is null && !_disposed && _sessions.Count > 0)
            _timer = new Timer(_ => OnTimer(), null, TickInterval, TickInterval);
    }

    /// <summary>
    /// 0.25초마다(스레드 풀) — 때가 된 타일 재시도를 여기서 바로 돌리고, 화면 갱신(남은 시간 · 타이머 닫기)만 UI 에 하나 넣는다
    /// (앞 것이 아직이면 넣지 않는다 — 디스패처 줄이 불어나지 않게).
    /// </summary>
    private void OnTimer()
    {
        if (_disposed || Interlocked.Exchange(ref _timerBusy, 1) == 1) return;
        try
        {
            var sessions = Volatile.Read(ref _snapshot);
            foreach (var session in sessions) session.RunDueRetries();
            if (sessions.Length > 0 && Interlocked.Exchange(ref _uiTickPending, 1) == 0)
                _dispatcher.BeginInvoke(UiUpdatePriority, new Action(UiTick));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Warn($"event window timer failed: {ex.GetType().Name} {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _timerBusy, 0);
        }
    }

    private void UiTick()
    {
        Volatile.Write(ref _uiTickPending, 0);
        foreach (var session in _sessions.Values.ToArray()) session.UiTick();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
    }

    internal EventWindowViewModel? Find(string eventKey) => _sessions.TryGetValue(eventKey, out var s) ? s.ViewModel : null;

    public void Open(OpenEventWindow msg)
    {
        if (string.IsNullOrWhiteSpace(msg.EventId))
        {
            _send(new HostError { Code = "bad-message", Message = "empty eventId" });
            return;
        }
        if (!string.IsNullOrWhiteSpace(msg.Theme)) SetTheme(msg.Theme!);
        if (_sessions.TryGetValue(msg.EventKey, out var existing))
        {
            existing.BringToFront();
            _send(new WindowOpened { EventKey = msg.EventKey, Reused = true });
            return;
        }

        var session = new Session(this, msg);
        _sessions[msg.EventKey] = session;
        SessionsChanged();
        try
        {
            session.Show();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 창 하나 실패는 그 창만(FR-26) — 정리하고 GIS 에 알린다. 호스트는 산다.
            _log.Error($"event window open failed {msg.EventKey}: {ex}");
            _send(new HostError { Code = "window-open-failed", Scope = msg.EventKey, Message = ex.GetType().Name });
            session.Close(EventWindowCloseReason.User, returnHome: false);
            return;
        }
        _log.Info($"event window open {msg.EventKey} cameras={msg.Cameras.Count} grid={session.ViewModel.BaseColumns}x{session.ViewModel.BaseRows} headless={_headless}");
        _send(new WindowOpened { EventKey = msg.EventKey });
    }

    public void Close(string eventKey, EventWindowCloseReason reason, bool returnHome)
    {
        if (!_sessions.TryGetValue(eventKey, out var session)) return;
        if (!session.ViewModel.TryAcceptCloseCommand(reason))
        {
            _log.Info($"close {eventKey} reason={reason} ignored — pinned (PinChanged re-sent)");
            return;
        }
        session.Close(reason, returnHome);
    }

    public void BringToFront(string eventKey)
    {
        if (_sessions.TryGetValue(eventKey, out var session)) session.BringToFront();
    }

    public void SetTheme(string theme)
    {
        HostTheme.Apply(_headless ? null : Application.Current, theme);
        _log.Info($"theme {HostTheme.Current}");
    }

    private void OnSessionClosed(Session session, EventWindowCloseReason reason)
    {
        if (_sessions.TryGetValue(session.EventKey, out var current) && ReferenceEquals(current, session))
            _sessions.Remove(session.EventKey);
        SessionsChanged();
        _log.Info($"event window closed {session.EventKey} reason={reason}");
        _send(new WindowClosed { EventKey = session.EventKey, Reason = reason });
    }

    /// <summary>
    /// 창 하나: VM + (헤드리스가 아니면) 뷰 + 타일별 영상 생산자. 뷰 · VM 은 UI 스레드, 타일 스트림 표(<c>_streams</c> · <c>_retries</c>)는
    /// <c>_gate</c> 로 지켜 UI · 타이머 · 생산자 스레드가 함께 쓴다. 잠금 안에서는 밖의 코드를 부르지 않는다(디스패처에 넣기만 한다).
    /// </summary>
    private sealed class Session
    {
        private readonly EventWindowManager _owner;
        private readonly object _gate = new();
        private readonly Dictionary<TileViewModel, TileStream> _streams = new();
        private readonly Dictionary<TileViewModel, TileRetrySchedule> _retries = new();
        private EventWindowView? _view;
        private volatile bool _closed;

        public Session(EventWindowManager owner, OpenEventWindow msg)
        {
            _owner = owner;
            ViewModel = new EventWindowViewModel(msg, owner._controls, owner._clock, owner._send, owner._log);
            ViewModel.CloseRequested += reason => Close(reason, ViewModel.Message.ReturnHomeOnClose);
            ViewModel.TileRemoved += tile =>
            {
                lock (_gate) { _retries.Remove(tile); }
                StopStream(tile);
            };
            ViewModel.TileSizeChanged += OnTileSizeChanged;
        }

        public EventWindowViewModel ViewModel { get; }
        public string EventKey => ViewModel.EventKey;

        public void Show()
        {
            foreach (var tile in ViewModel.CameraTiles.ToArray())
            {
                tile.RetryRequested += ManualRetry;
                StartStream(tile);
            }
            if (!_owner._headless)
            {
                HostTheme.EnsureLoaded(Application.Current);
                _view = new EventWindowView(ViewModel);
                _view.Closed += (_, _) => Finish(EventWindowCloseReason.User);
                _view.Show();
            }
            ViewModel.Start();
        }

        /// <summary>UI 스레드 — delay 표시 · 남은 시간 · 타이머 닫기.</summary>
        public void UiTick()
        {
            if (_closed) return;
            try
            {
                ViewModel.Tick();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Warn($"event window tick failed {EventKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// 실패한 타일 자동 재시도(간격은 <see cref="StreamRetryBackoff"/>) — 때가 된 타일만 새 생산자로 다시 연다.
        /// <b>스레드 풀 타이머에서 돈다</b>(UI 를 기다리지 않는다, FR-40).
        /// </summary>
        public void RunDueRetries()
        {
            List<(TileViewModel Tile, TileStream Stream, int Attempt)>? due = null;
            lock (_gate)
            {
                if (_closed || _retries.Count == 0) return;
                var now = _owner._clock.UtcNow;
                foreach (var (tile, schedule) in _retries)
                {
                    if (!schedule.TryTake(now, out int attempt)) continue;
                    if (_streams.TryGetValue(tile, out var stream)) (due ??= new()).Add((tile, stream, attempt));
                }
            }
            if (due is null) return;
            foreach (var (tile, stream, attempt) in due)
            {
                try
                {
                    _owner._log.Info($"tile auto retry {stream.StreamId} attempt={attempt}");
                    if (stream.Sink is null)
                    {
                        // 비트맵을 못 만든 타일 — 비트맵은 UI 스레드에서만 만든다.
                        PostUi(() =>
                        {
                            if (_closed) return;
                            tile.SetStreamState(StreamState.Opening, "retry");
                            StopStream(tile);
                            StartStream(tile, attempt);
                        });
                        continue;
                    }
                    StartProducer(tile, stream, attempt, priority: false, openingDetail: "retry");
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    _owner._log.Warn($"tile auto retry failed {stream.StreamId}: {ex.GetType().Name} {ex.Message}");
                }
            }
        }

        private void PostUi(Action action)
        {
            try { _owner._dispatcher.BeginInvoke(UiUpdatePriority, action); }
            catch (InvalidOperationException) { /* 디스패처가 내려가는 중 */ }
        }

        // ───────── 타일 스트림(FR-26: 실패는 그 타일만) ─────────

        /// <summary>타일이 그려지는 크기(물리 픽셀) — 격자 한 칸, "이 카메라만 크게"면 창 전체.</summary>
        private (int Width, int Height) TileSize(TileViewModel tile)
        {
            var w = ViewModel.Message.Window;
            int columns = tile.IsEnlarged ? 1 : Math.Max(1, ViewModel.BaseColumns);
            int rows = tile.IsEnlarged ? 1 : Math.Max(1, ViewModel.BaseRows);
            int width = Math.Clamp(w.Width / columns, 64, 1920) & ~1;
            int height = Math.Clamp(Math.Max(1, w.Height - 64) / rows, 48, 1080) & ~1;
            return (width, height);
        }

        /// <summary>UI 스레드 — 타일 비트맵 싱크를 만들고 첫 생산자를 붙인다.</summary>
        private void StartStream(TileViewModel tile, int attempt = 0, bool priority = false)
        {
            if (_closed || tile.Camera is null) return;
            var (width, height) = TileSize(tile);
            var stream = new TileStream($"{EventKey}#{tile.CameraId}", tile.Camera) { Width = width, Height = height };
            lock (_gate) { _streams[tile] = stream; }
            Interlocked.Increment(ref _owner._streamCount);
            try
            {
                var sink = new BitmapFrameSink(width, height, _owner._dispatcher);
                stream.Sink = sink;
                // 다시 여는 동안(크게 보기)에는 앞 영상의 마지막 장면을 그대로 두고, 새 영상이 나오면 바꾼다.
                if (tile.Video is null) tile.Video = sink.Bitmap;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Error($"tile stream start failed {stream.StreamId}: {ex.GetType().Name} {ex.Message}");
                OnStreamState(tile, stream, stream.Generation, StreamState.Failed, "start-failed");
                return;
            }
            StartProducer(tile, stream, attempt, priority, openingDetail: null);
        }

        /// <summary>
        /// 이 타일 스트림에 새 생산자를 붙인다(앞 생산자는 배경에서 버린다). <b>어느 스레드에서나</b> — 싱크(비트맵)는 그대로 쓴다.
        /// <paramref name="openingDetail"/> 을 주면 화면 상태를 "연결 중"으로 돌린다(새 생산자의 상태보다 먼저 줄 선다).
        /// </summary>
        private void StartProducer(TileViewModel tile, TileStream stream, int attempt, bool priority, string? openingDetail)
        {
            IFrameProducer? old;
            GatedFrameSink output;
            int generation;
            lock (_gate)
            {
                if (_closed || stream.Sink is null || !IsCurrent(tile, stream)) return;
                generation = ++stream.Generation;
                stream.Output?.Detach();
                stream.Output = output = new GatedFrameSink(stream.Sink);
                old = stream.Producer;
                stream.Producer = null;
                if (openingDetail is not null) PostTileState(tile, stream, generation, StreamState.Opening, openingDetail);
            }
            DisposeInBackground(old, stream.StreamId);
            try
            {
                var camera = stream.Camera;
                var producer = _owner._producers.Create(camera.Provider, stream.Width, stream.Height, stream.StreamId, camera.CameraId, priority, attempt);
                bool keep;
                lock (_gate)
                {
                    keep = !_closed && stream.Generation == generation && IsCurrent(tile, stream);
                    if (keep) stream.Producer = producer;
                }
                if (!keep)
                {
                    DisposeInBackground(producer, stream.StreamId);
                    return;
                }
                // 잠금 밖에서 시작한다 — 그 사이 닫혀 이미 버려진 생산자의 Start 는 아무 일도 하지 않는다(생산자 계약).
                producer.Start(output, (state, detail) => OnStreamState(tile, stream, generation, state, detail));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Error($"tile stream start failed {stream.StreamId}: {ex.GetType().Name} {ex.Message}");
                OnStreamState(tile, stream, generation, StreamState.Failed, "start-failed");
            }
        }

        /// <summary><c>_gate</c> 안에서.</summary>
        private bool IsCurrent(TileViewModel tile, TileStream stream)
            => _streams.TryGetValue(tile, out var current) && ReferenceEquals(current, stream);

        /// <summary>
        /// 생산자 스레드에서 온다. 재시도 일정은 <b>여기서 바로</b> 적는다(UI 를 기다리지 않는다) — 화면에는 상태 글자만 넣는다.
        /// </summary>
        private void OnStreamState(TileViewModel tile, TileStream stream, int generation, StreamState state, string? detail)
        {
            _owner._send(new StreamStateChanged { StreamId = stream.StreamId, EventKey = EventKey, State = state, Detail = detail });
            bool scheduled;
            int failures;
            DateTime? dueAt;
            lock (_gate)
            {
                if (_closed || stream.Generation != generation || !IsCurrent(tile, stream)) return;
                if (!_retries.TryGetValue(tile, out var schedule)) _retries[tile] = schedule = new TileRetrySchedule();
                scheduled = schedule.OnState(state, detail, _owner._clock.UtcNow);
                failures = schedule.Failures;
                dueAt = schedule.DueAt;
                PostTileState(tile, stream, generation, state, detail);
            }
            if (scheduled)
                _owner._log.Info($"tile {stream.StreamId} {state} {detail} — retry #{failures} at {dueAt:HH:mm:ss}");
        }

        /// <summary>
        /// 타일 상태 글자 · 영상 비트맵을 UI 에 넣는다(Normal). <c>_gate</c> 안에서 부른다 — 넣는 순서 = 상태가 생긴 순서.
        /// UI 에서 돌 때 그 사이 다른 생산자로 바뀌었으면 버린다.
        /// </summary>
        private void PostTileState(TileViewModel tile, TileStream stream, int generation, StreamState state, string? detail)
            => PostUi(() =>
            {
                lock (_gate)
                {
                    if (_closed || stream.Generation != generation || !IsCurrent(tile, stream)) return;
                }
                if (state == StreamState.Playing && stream.Sink is { } sink && !ReferenceEquals(tile.Video, sink.Bitmap)) tile.Video = sink.Bitmap;
                tile.SetStreamState(state, detail);
            });

        /// <summary>사람이 "다시 시도"를 눌렀다 — 횟수를 되돌리고 줄 앞에서 바로 연다.</summary>
        private void ManualRetry(TileViewModel tile)
        {
            if (_closed) return;
            _owner._log.Info($"tile retry {EventKey}#{tile.CameraId}");
            TileStream? stream;
            lock (_gate)
            {
                if (_retries.TryGetValue(tile, out var schedule)) schedule.Reset();
                _streams.TryGetValue(tile, out stream);
            }
            if (stream?.Sink is not null)
            {
                StartProducer(tile, stream, attempt: 0, priority: true, openingDetail: null);
                return;
            }
            StopStream(tile);
            StartStream(tile, attempt: 0, priority: true);
        }

        /// <summary>
        /// 크게 보기 켬 · 끔 — 그릴 크기가 "큰 상자 ↔ 작은 상자"를 넘나들 때만 다시 연다(큰 상자 = 메인 스트림 · 하드웨어 디코딩,
        /// 작은 상자 = 서브 스트림). 같은 등급이면 그대로 둔다(끊김 없이 늘려 그린다).
        /// </summary>
        private void OnTileSizeChanged(TileViewModel tile)
        {
            if (_closed) return;
            TileStream? stream;
            lock (_gate) { _streams.TryGetValue(tile, out stream); }
            if (stream is null) return;
            var (width, height) = TileSize(tile);
            if (DecodeProfile.IsLarge(width, height) == DecodeProfile.IsLarge(stream.Width, stream.Height)) return;
            _owner._log.Info($"tile resize {stream.StreamId} {stream.Width}x{stream.Height} → {width}x{height}");
            lock (_gate)
            {
                if (_retries.TryGetValue(tile, out var schedule)) schedule.Reset();
            }
            tile.SetStreamState(StreamState.Opening, "resize");
            StopStream(tile);
            StartStream(tile, attempt: 0, priority: true);
        }

        private void StopStream(TileViewModel tile)
        {
            TileStream? stream;
            IFrameProducer? producer;
            lock (_gate)
            {
                if (!_streams.Remove(tile, out stream)) return;
                producer = Retire(stream);
            }
            Interlocked.Decrement(ref _owner._streamCount);
            stream.Sink?.Dispose();
            DisposeInBackground(producer, stream.StreamId);
        }

        /// <summary><c>_gate</c> 안에서 — 스트림을 물린다(늦은 상태 · 프레임은 버려진다). 버릴 생산자를 돌려준다.</summary>
        private static IFrameProducer? Retire(TileStream stream)
        {
            stream.Generation++;
            stream.Output?.Detach();
            stream.Output = null;
            var producer = stream.Producer;
            stream.Producer = null;
            return producer;
        }

        /// <summary>생산자 정리는 배경에서 — LibVLC Stop 이 부른 스레드(UI · 타이머)를 붙잡지 않게.</summary>
        private void DisposeInBackground(IFrameProducer? producer, string streamId)
        {
            if (producer is null) return;
            var log = _owner._log;
            _ = Task.Run(() =>
            {
                try { producer.Dispose(); }
                catch (Exception ex) { log.Warn($"tile producer dispose failed {streamId}: {ex.Message}"); }
            });
        }

        // ───────── 앞으로 · 닫기 ─────────

        public void BringToFront()
        {
            ViewModel.NoteInteraction();
            try { _view?.BringToFront(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Warn($"bring to front failed {EventKey}: {ex.Message}");
            }
        }

        public void Close(EventWindowCloseReason reason, bool returnHome)
        {
            if (_closed) return;
            var view = _view;
            _view = null;
            Finish(reason, returnHome);
            try { view?.CloseBySession(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Warn($"view close failed {EventKey}: {ex.Message}");
            }
        }

        private void Finish(EventWindowCloseReason reason, bool returnHome = false)
        {
            if (_closed) return;
            List<(TileStream Stream, IFrameProducer? Producer)> retired;
            lock (_gate)
            {
                _closed = true;
                retired = _streams.Values.Select(stream => (stream, Retire(stream))).ToList();
                _streams.Clear();
                _retries.Clear();
            }
            foreach (var (stream, producer) in retired)
            {
                Interlocked.Decrement(ref _owner._streamCount);
                stream.Sink?.Dispose();
                DisposeInBackground(producer, stream.StreamId);
            }
            var vm = ViewModel;
            var log = _owner._log;
            // 복귀 프리셋(시간 제한) · 제어 정리는 배경에서 — UI 를 붙잡지 않는다.
            _ = Task.Run(async () =>
            {
                try { await vm.ShutdownAsync(returnHome).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warn($"shutdown failed {vm.EventKey}: {ex.Message}"); }
            });
            _owner.OnSessionClosed(this, reason);
        }

        /// <summary>타일 하나의 스트림 — 비트맵 싱크(UI 스레드가 만든다)는 그대로 두고 생산자만 갈아 끼운다. 바뀌는 칸은 <c>_gate</c> 안에서.</summary>
        private sealed class TileStream
        {
            public TileStream(string streamId, EventWindowCamera camera)
            {
                StreamId = streamId;
                Camera = camera;
            }

            public string StreamId { get; }
            public EventWindowCamera Camera { get; }
            public int Generation { get; set; }
            public int Width { get; init; }
            public int Height { get; init; }
            public BitmapFrameSink? Sink { get; set; }
            public GatedFrameSink? Output { get; set; }
            public IFrameProducer? Producer { get; set; }
        }
    }
}
