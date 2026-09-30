using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Windows;

/// <summary>
/// 이벤트 창 관리(UI 스레드 전용). 이벤트 1건 = 창 1개 — 같은 키가 다시 오면 앞으로 가져온다(FR-09).
/// 헤드리스 모드에서는 창을 만들지 않고 상태만 추적한다(생존 시험의 복원 확인용).
/// 창 수 한도 · 오래된 창 정리는 GIS 쪽 창 관리자(T-04) 몫 — 호스트는 받은 명령만 따른다.
/// </summary>
internal sealed class EventWindowManager
{
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Dispatcher _dispatcher;
    private readonly FrameProducerFactory _factory;
    private readonly Action<IIpcMessage> _send;
    private readonly HostLog _log;
    private readonly bool _headless;

    public EventWindowManager(Dispatcher dispatcher, FrameProducerFactory factory, Action<IIpcMessage> send, HostLog log, bool headless)
    {
        _dispatcher = dispatcher;
        _factory = factory;
        _send = send;
        _log = log;
        _headless = headless;
    }

    /// <summary>열린 창 수 — 어느 스레드에서 읽어도 대략값이면 된다(심박 보고용).</summary>
    public int Count { get; private set; }

    public void Open(OpenEventWindow msg)
    {
        if (string.IsNullOrWhiteSpace(msg.EventKey))
        {
            _send(new HostError { Code = "bad-message", Message = "empty eventKey" });
            return;
        }
        if (_sessions.TryGetValue(msg.EventKey, out var existing))
        {
            existing.BringToFront();
            _send(new WindowOpened { EventKey = msg.EventKey, Reused = true });
            return;
        }

        var session = new Session(this, msg);
        _sessions[msg.EventKey] = session;
        Count = _sessions.Count;
        session.Show();
        _log.Info($"event window open {msg.EventKey} cameras={msg.Cameras.Count} headless={_headless}");
        _send(new WindowOpened { EventKey = msg.EventKey });
    }

    public void Close(string eventKey, string reason)
    {
        if (_sessions.TryGetValue(eventKey, out var session)) session.Close(reason);
    }

    private void OnSessionClosed(Session session, string reason)
    {
        if (_sessions.TryGetValue(session.EventKey, out var current) && ReferenceEquals(current, session))
            _sessions.Remove(session.EventKey);
        Count = _sessions.Count;
        _log.Info($"event window closed {session.EventKey} reason={reason}");
        _send(new WindowClosed { EventKey = session.EventKey, Reason = reason });
    }

    private sealed class Session
    {
        private readonly EventWindowManager _owner;
        private readonly OpenEventWindow _msg;
        private readonly List<IFrameProducer> _producers = new();
        private readonly List<BitmapFrameSink> _sinks = new();
        private EventWindow? _window;
        private DispatcherTimer? _timer;
        private string? _closeReason;
        private bool _closed;

        public Session(EventWindowManager owner, OpenEventWindow msg)
        {
            _owner = owner;
            _msg = msg;
        }

        public string EventKey => _msg.EventKey;

        public void Show()
        {
            var policy = _msg.ClosePolicy;
            if (!policy.Pinned && policy.TimeoutSeconds > 0)
            {
                _timer = new DispatcherTimer(TimeSpan.FromSeconds(policy.TimeoutSeconds), DispatcherPriority.Normal,
                    (_, _) => Close("timeout"), _owner._dispatcher);
                _timer.Start();
            }
            if (_owner._headless) return;

            _window = new EventWindow(_msg);
            _window.Closed += (_, _) => Finish(_closeReason ?? "user");
            var (columns, rows) = TileGrid.Resolve(_msg.Layout, _window.TileCount);
            int tileWidth = Math.Max(64, (int)(_window.Width / columns)) & ~1;
            int tileHeight = Math.Max(48, (int)(_window.Height / rows)) & ~1;
            for (int i = 0; i < _window.TileCount && i < _msg.Cameras.Count; i++)
            {
                int index = i;
                var camera = _msg.Cameras[i];
                var sink = new BitmapFrameSink(tileWidth, tileHeight, _owner._dispatcher);
                _sinks.Add(sink);
                _window.SetTileSource(index, sink.Bitmap);
                string streamId = $"{_msg.EventKey}#{index}";
                var producer = _owner._factory.Create(camera.Provider, tileWidth, tileHeight, streamId);
                _producers.Add(producer);
                producer.Start(sink, (state, detail) =>
                {
                    _owner._send(new StreamStateChanged { StreamId = streamId, EventKey = _msg.EventKey, State = state, Detail = detail });
                    _owner._dispatcher.BeginInvoke(() =>
                    {
                        if (!_closed) _window?.SetTileState(index, $"{camera.Camera.Name ?? camera.Camera.CameraId} · {state}");
                    });
                });
            }
            _window.Show();
        }

        public void BringToFront()
        {
            if (_window is null) return;
            if (_window.WindowState == System.Windows.WindowState.Minimized) _window.WindowState = System.Windows.WindowState.Normal;
            _window.Activate();
        }

        public void Close(string reason)
        {
            if (_closed) return;
            _closeReason = reason;
            if (_window is not null) _window.Close(); // Closed → Finish
            else Finish(reason);
        }

        private void Finish(string reason)
        {
            if (_closed) return;
            _closed = true;
            _timer?.Stop();
            foreach (var sink in _sinks) sink.Dispose();
            var producers = _producers.ToArray();
            _ = Task.Run(() =>
            {
                foreach (var p in producers)
                {
                    try { p.Dispose(); }
                    catch (Exception ex) { _owner._log.Warn($"tile producer dispose failed: {ex.Message}"); }
                }
            });
            _owner.OnSessionClosed(this, reason);
        }
    }
}
