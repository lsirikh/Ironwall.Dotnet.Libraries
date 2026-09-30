using System.Windows;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 관리(UI 스레드 전용). 이벤트 1건 = 창 1개 — 같은 키가 다시 오면 앞으로 가져온다(FR-09).
/// 헤드리스 모드에서도 창 상태(VM) · 타일 스트림 · 타이머는 그대로 돌고 창(뷰)만 만들지 않는다 — 생존 시험이 실제 경로를 탄다.
/// 창 수 한도 · 오래된 창 정리는 GIS 쪽 창 관리자 몫 — 호스트는 받은 명령만 따른다.
/// 창 하나의 실패(뷰 생성 · 스트림 · PTZ)는 그 창 · 그 타일에만 머문다(FR-26).
/// </summary>
internal sealed class EventWindowManager
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Dispatcher _dispatcher;
    private readonly FrameProducerFactory _producers;
    private readonly ITileCameraControlFactory _controls;
    private readonly IHostClock _clock;
    private readonly Action<IIpcMessage> _send;
    private readonly HostLog _log;
    private readonly bool _headless;

    public EventWindowManager(Dispatcher dispatcher, FrameProducerFactory producers, Action<IIpcMessage> send, HostLog log, bool headless)
        : this(dispatcher, producers, new TileCameraControlFactory(producers.Cameras), SystemHostClock.Instance, send, log, headless)
    {
    }

    internal EventWindowManager(Dispatcher dispatcher, FrameProducerFactory producers, ITileCameraControlFactory controls, IHostClock clock,
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
        Count = _sessions.Count;
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
        Count = _sessions.Count;
        _log.Info($"event window closed {session.EventKey} reason={reason}");
        _send(new WindowClosed { EventKey = session.EventKey, Reason = reason });
    }

    /// <summary>창 하나: VM + (헤드리스가 아니면) 뷰 + 타일별 영상 생산자 + 주기 타이머.</summary>
    private sealed class Session
    {
        private readonly EventWindowManager _owner;
        private readonly Dictionary<TileViewModel, TileStream> _streams = new();
        private readonly Dictionary<TileViewModel, TileRetrySchedule> _retries = new();
        private readonly DispatcherTimer _ticker;
        private EventWindowView? _view;
        private bool _closed;

        public Session(EventWindowManager owner, OpenEventWindow msg)
        {
            _owner = owner;
            ViewModel = new EventWindowViewModel(msg, owner._controls, owner._clock, owner._send, owner._log);
            ViewModel.CloseRequested += reason => Close(reason, ViewModel.Message.ReturnHomeOnClose);
            ViewModel.TileRemoved += tile =>
            {
                _retries.Remove(tile);
                StopStream(tile);
            };
            ViewModel.TileSizeChanged += OnTileSizeChanged;
            _ticker = new DispatcherTimer(TickInterval, DispatcherPriority.Background, (_, _) => Tick(), owner._dispatcher);
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
            _ticker.Start();
        }

        private void Tick()
        {
            if (_closed) return;
            try
            {
                ViewModel.Tick();
                RunDueRetries();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Warn($"event window tick failed {EventKey}: {ex.Message}");
            }
        }

        /// <summary>실패한 타일 자동 재시도(간격은 <see cref="StreamRetryBackoff"/>) — 때가 된 타일만 새 생산자로 다시 연다.</summary>
        private void RunDueRetries()
        {
            if (_retries.Count == 0) return;
            var now = _owner._clock.UtcNow;
            foreach (var (tile, schedule) in _retries.ToArray())
            {
                if (!schedule.TryTake(now, out int attempt)) continue;
                _owner._log.Info($"tile auto retry {EventKey}#{tile.CameraId} attempt={attempt}");
                tile.SetStreamState(StreamState.Opening, "retry");
                StopStream(tile);
                StartStream(tile, attempt);
            }
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

        private void StartStream(TileViewModel tile, int attempt = 0, bool priority = false)
        {
            if (_closed || tile.Camera is null) return;
            var (width, height) = TileSize(tile);
            string streamId = $"{EventKey}#{tile.CameraId}";
            var stream = new TileStream(streamId) { Width = width, Height = height };
            _streams[tile] = stream;
            Interlocked.Increment(ref _owner._streamCount);
            try
            {
                var sink = new BitmapFrameSink(width, height, _owner._dispatcher);
                stream.Sink = sink;
                // 다시 여는 동안(재시도 · 크게 보기)에는 앞 영상의 마지막 장면을 그대로 두고, 새 영상이 나오면 바꾼다.
                if (tile.Video is null) tile.Video = sink.Bitmap;
                var producer = _owner._producers.Create(tile.Camera.Provider, width, height, streamId, tile.Camera.CameraId, priority, attempt);
                stream.Producer = producer;
                int generation = stream.Generation;
                producer.Start(sink, (state, detail) => OnStreamState(tile, stream, generation, state, detail));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Error($"tile stream start failed {streamId}: {ex.GetType().Name} {ex.Message}");
                OnStreamState(tile, stream, stream.Generation, StreamState.Failed, "start-failed");
            }
        }

        private void OnStreamState(TileViewModel tile, TileStream stream, int generation, StreamState state, string? detail)
        {
            // 생산자 스레드에서 온다.
            _owner._send(new StreamStateChanged { StreamId = stream.StreamId, EventKey = EventKey, State = state, Detail = detail });
            _owner._dispatcher.BeginInvoke(() =>
            {
                if (_closed || stream.Generation != generation || !_streams.TryGetValue(tile, out var current) || !ReferenceEquals(current, stream)) return;
                if (state == StreamState.Playing && stream.Sink is { } sink && !ReferenceEquals(tile.Video, sink.Bitmap)) tile.Video = sink.Bitmap;
                tile.SetStreamState(state, detail);
                if (!_retries.TryGetValue(tile, out var schedule)) _retries[tile] = schedule = new TileRetrySchedule();
                if (schedule.OnState(state, detail, _owner._clock.UtcNow))
                    _owner._log.Info($"tile {stream.StreamId} {state} {detail} — retry #{schedule.Failures} at {schedule.DueAt:HH:mm:ss}");
            });
        }

        /// <summary>사람이 "다시 시도"를 눌렀다 — 횟수를 되돌리고 줄 앞에서 바로 연다.</summary>
        private void ManualRetry(TileViewModel tile)
        {
            if (_closed) return;
            _owner._log.Info($"tile retry {EventKey}#{tile.CameraId}");
            if (_retries.TryGetValue(tile, out var schedule)) schedule.Reset();
            StopStream(tile);
            StartStream(tile, attempt: 0, priority: true);
        }

        /// <summary>
        /// 크게 보기 켬 · 끔 — 그릴 크기가 "큰 상자 ↔ 작은 상자"를 넘나들 때만 다시 연다(큰 상자 = 메인 스트림 · 하드웨어 디코딩,
        /// 작은 상자 = 서브 스트림). 같은 등급이면 그대로 둔다(끊김 없이 늘려 그린다).
        /// </summary>
        private void OnTileSizeChanged(TileViewModel tile)
        {
            if (_closed || !_streams.TryGetValue(tile, out var stream)) return;
            var (width, height) = TileSize(tile);
            if (DecodeProfile.IsLarge(width, height) == DecodeProfile.IsLarge(stream.Width, stream.Height)) return;
            _owner._log.Info($"tile resize {stream.StreamId} {stream.Width}x{stream.Height} → {width}x{height}");
            if (_retries.TryGetValue(tile, out var schedule)) schedule.Reset();
            tile.SetStreamState(StreamState.Opening, "resize");
            StopStream(tile);
            StartStream(tile, attempt: 0, priority: true);
        }

        private void StopStream(TileViewModel tile)
        {
            if (!_streams.Remove(tile, out var stream)) return;
            stream.Generation++;
            Interlocked.Decrement(ref _owner._streamCount);
            DisposeInBackground(stream);
        }

        private void DisposeInBackground(TileStream stream)
        {
            stream.Sink?.Dispose();
            var producer = stream.Producer;
            if (producer is null) return;
            var log = _owner._log;
            _ = Task.Run(() =>
            {
                try { producer.Dispose(); }
                catch (Exception ex) { log.Warn($"tile producer dispose failed {stream.StreamId}: {ex.Message}"); }
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
            _closed = true;
            _ticker.Stop();
            foreach (var stream in _streams.Values.ToArray())
            {
                Interlocked.Decrement(ref _owner._streamCount);
                DisposeInBackground(stream);
            }
            _streams.Clear();
            _retries.Clear();
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

        private sealed class TileStream
        {
            public TileStream(string streamId) => StreamId = streamId;

            public string StreamId { get; }
            public int Generation { get; set; }
            public int Width { get; init; }
            public int Height { get; init; }
            public BitmapFrameSink? Sink { get; set; }
            public IFrameProducer? Producer { get; set; }
        }
    }
}
