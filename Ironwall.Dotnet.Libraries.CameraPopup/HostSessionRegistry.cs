using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// "지금 열려 있어야 하는 것"의 기록(스레드 안전). 명령은 먼저 여기에 반영되고, 호스트가 살아 있으면 곧바로 보내며,
/// 호스트가 재시작되면 이 목록을 그대로 다시 보낸다(FR-25 복원). 그래서 대기열이 넘쳐 버린 명령도 복원 때 회복된다.
/// 호스트가 알린 사람 조작(📌 · 창 이동 · 타일 닫기)도 창별로 덧씌워 두었다가 복원 메시지에 반영한다.
/// </summary>
public sealed class HostSessionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OpenOverlayStream> _overlays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WindowEntry> _windows = new(StringComparer.Ordinal);
    private readonly List<string> _windowOrder = new();

    public void SetOverlay(OpenOverlayStream message)
    {
        lock (_gate) { _overlays[message.StreamId] = message; }
    }

    public bool RemoveOverlay(string streamId)
    {
        lock (_gate) { return _overlays.Remove(streamId); }
    }

    /// <summary>창 등록. 같은 키가 이미 있으면 덧씌운 사람 조작은 유지하고 원본만 바꾼다.</summary>
    public void SetWindow(OpenEventWindow message)
    {
        lock (_gate)
        {
            if (_windows.TryGetValue(message.EventKey, out var entry))
            {
                entry.Message = message;
                return;
            }
            _windowOrder.Add(message.EventKey);
            _windows[message.EventKey] = new WindowEntry(message);
        }
    }

    public bool RemoveWindow(string eventKey)
    {
        lock (_gate)
        {
            _windowOrder.Remove(eventKey);
            return _windows.Remove(eventKey);
        }
    }

    public bool HasWindow(string eventKey)
    {
        lock (_gate) { return _windows.ContainsKey(eventKey); }
    }

    /// <summary>호스트 알림(📌 · 이동 · 타일 닫기)을 창 기록에 덧씌운다. 모르는 창이면 false.</summary>
    public bool ApplyHostNotice(IIpcMessage notice)
    {
        lock (_gate)
        {
            switch (notice)
            {
                case PinChanged pin when _windows.TryGetValue(pin.EventKey, out var e1):
                    e1.Pinned = pin.Pinned;
                    return true;
                case WindowMoved moved when _windows.TryGetValue(moved.EventKey, out var e2):
                    e2.MovedTo = (moved.X, moved.Y);
                    return true;
                case TileClosed tile when _windows.TryGetValue(tile.EventKey, out var e3):
                    e3.ClosedCameras.Add(tile.CameraId);
                    return true;
                default:
                    return false;
            }
        }
    }

    public int OverlayCount { get { lock (_gate) { return _overlays.Count; } } }
    public int WindowCount { get { lock (_gate) { return _windows.Count; } } }

    /// <summary>복원 순서: 창(열린 순서대로, 사람 조작 반영) → 오버레이.</summary>
    public (OpenEventWindow[] Windows, OpenOverlayStream[] Overlays) Snapshot()
    {
        lock (_gate)
        {
            var windows = _windowOrder.Select(k => _windows[k].Effective()).ToArray();
            return (windows, _overlays.Values.ToArray());
        }
    }

    private sealed class WindowEntry
    {
        public WindowEntry(OpenEventWindow message) => Message = message;

        public OpenEventWindow Message { get; set; }
        public bool? Pinned { get; set; }
        public (int X, int Y)? MovedTo { get; set; }
        public HashSet<string> ClosedCameras { get; } = new(StringComparer.Ordinal);

        /// <summary>덧씌운 것이 없으면 원본 그대로.</summary>
        public OpenEventWindow Effective()
        {
            if (Pinned is null && MovedTo is null && ClosedCameras.Count == 0) return Message;
            var m = Message;
            var window = MovedTo is { } p
                ? new PixelRect { X = p.X, Y = p.Y, Width = m.Window.Width, Height = m.Window.Height }
                : m.Window;
            return new OpenEventWindow
            {
                Kind = m.Kind,
                EventId = m.EventId,
                Header = m.Header,
                Cameras = m.Cameras.Where(c => !ClosedCameras.Contains(c.CameraId)).ToList(),
                GridColumns = m.GridColumns,
                GridRows = m.GridRows,
                ExtraCameraCount = m.ExtraCameraCount,
                MonitorBounds = m.MonitorBounds,
                MonitorWorkArea = m.MonitorWorkArea,
                DpiScale = m.DpiScale,
                MonitorDeviceName = m.MonitorDeviceName,
                Window = window,
                AlwaysOnTop = m.AlwaysOnTop,
                TimerCloseSeconds = m.TimerCloseSeconds,
                CloseOnActionReport = m.CloseOnActionReport,
                ReturnHomeOnClose = m.ReturnHomeOnClose,
                Pinned = Pinned ?? m.Pinned,
                Theme = m.Theme,
                Title = m.Title,
            };
        }
    }
}
