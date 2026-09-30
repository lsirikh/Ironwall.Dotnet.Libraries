using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// "지금 열려 있어야 하는 것"의 기록(스레드 안전). 명령은 먼저 여기에 반영되고, 호스트가 살아 있으면 곧바로 보내며,
/// 호스트가 재시작되면 이 목록을 그대로 다시 보낸다(FR-25 복원). 그래서 대기열이 넘쳐 버린 명령도 복원 때 회복된다.
/// </summary>
public sealed class HostSessionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OpenOverlayStream> _overlays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OpenEventWindow> _windows = new(StringComparer.Ordinal);
    private readonly List<string> _windowOrder = new();

    public void SetOverlay(OpenOverlayStream message)
    {
        lock (_gate) { _overlays[message.StreamId] = message; }
    }

    public bool RemoveOverlay(string streamId)
    {
        lock (_gate) { return _overlays.Remove(streamId); }
    }

    public void SetWindow(OpenEventWindow message)
    {
        lock (_gate)
        {
            if (!_windows.ContainsKey(message.EventKey)) _windowOrder.Add(message.EventKey);
            _windows[message.EventKey] = message;
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

    public int OverlayCount { get { lock (_gate) { return _overlays.Count; } } }
    public int WindowCount { get { lock (_gate) { return _windows.Count; } } }

    /// <summary>복원 순서: 창(열린 순서대로) → 오버레이.</summary>
    public (OpenEventWindow[] Windows, OpenOverlayStream[] Overlays) Snapshot()
    {
        lock (_gate)
        {
            var windows = _windowOrder.Select(k => _windows[k]).ToArray();
            return (windows, _overlays.Values.ToArray());
        }
    }
}
