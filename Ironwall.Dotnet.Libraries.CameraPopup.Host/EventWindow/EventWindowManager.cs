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
        private readonly DispatcherTimer _ticker;
        private EventWindowView? _view;
        private bool _closed;

        public Session(EventWindowManager owner, OpenEventWindow msg)
        {
            _owner = owner;
            ViewModel = new EventWindowViewModel(msg, owner._controls, owner._clock, owner._send, owner._log);
            ViewModel.CloseRequested += reason => Close(reason, ViewModel.Message.ReturnHomeOnClose);
            ViewModel.TileRemoved += StopStream;
            _ticker = new DispatcherTimer(TickInterval, DispatcherPriority.Background, (_, _) => Tick(), owner._dispatcher);
        }

        public EventWindowViewModel ViewModel { get; }
        public string EventKey => ViewModel.EventKey;

        public void Show()
        {
            foreach (var tile in ViewModel.CameraTiles.ToArray())
            {
                tile.RetryRequested += RestartStream;
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
            try { ViewModel.Tick(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _owner._log.Warn($"event window tick failed {EventKey}: {ex.Message}");
            }
        }

        // ───────── 타일 스트림(FR-26: 실패는 그 타일만) ─────────

        private (int Width, int Height) TileSize()
        {
            var w = ViewModel.Message.Window;
            int width = Math.Clamp(w.Width / Math.Max(1, ViewModel.BaseColumns), 64, 1920) & ~1;
            int height = Math.Clamp(Math.Max(1, w.Height - 64) / Math.Max(1, ViewModel.BaseRows), 48, 1080) & ~1;
            return (width, height);
        }

        private void StartStream(TileViewModel tile)
        {
            if (_closed || tile.Camera is null) return;
            var (width, height) = TileSize();
            string streamId = $"{EventKey}#{tile.CameraId}";
            var stream = new TileStream(streamId);
            _streams[tile] = stream;
            try
            {
                var sink = new BitmapFrameSink(width, height, _owner._dispatcher);
                stream.Sink = sink;
                tile.Video = sink.Bitmap;
                var producer = _owner._producers.Create(tile.Camera.Provider, width, height, streamId, tile.Camera.CameraId);
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
                tile.SetStreamState(state, detail);
            });
        }

        private void RestartStream(TileViewModel tile)
        {
            if (_closed) return;
            _owner._log.Info($"tile retry {EventKey}#{tile.CameraId}");
            StopStream(tile);
            StartStream(tile);
        }

        private void StopStream(TileViewModel tile)
        {
            if (!_streams.Remove(tile, out var stream)) return;
            stream.Generation++;
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
            foreach (var stream in _streams.Values.ToArray()) DisposeInBackground(stream);
            _streams.Clear();
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
            public BitmapFrameSink? Sink { get; set; }
            public IFrameProducer? Producer { get; set; }
        }
    }
}
