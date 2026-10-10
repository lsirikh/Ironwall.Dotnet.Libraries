using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Utils.Perf;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 호스트 알림 → 계측(PerfTrace) — 카탈로그 PERF-003 · 004 · 012 · 013
                  - WindowOpened → popup_opened
                  - 이벤트 창 타일의 첫 StreamStateChanged(Playing) → first_frame(호스트가 실어 보낸 FirstFrameMs 가 있으면 extra.host_ms)
                  - HeartbeatAck → host_heartbeat(1초에 한 번 — UiStallMs · PrivateBytes · 열린 창/스트림 수)
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 감시자의 받기 스레드에서 불린다 — 짧은 일만 하고 던지지 않는다. 계측이 꺼져 있으면 아무것도 하지 않는다.
/// 첫 영상은 (이벤트 키, 스트림) 마다 한 번만 찍는다(옛 호스트는 <see cref="StreamStateChanged.FirstFrameMs"/> 를 보내지 않으므로 첫 Playing 으로 가른다).
/// </summary>
internal sealed class HostPerfRecorder
{
    /// <summary>기억할 첫 영상 (키, 스트림) 수 — 넘으면 비운다(오래 켠 계측에서 무한히 자라지 않게).</summary>
    internal const int MaxRemembered = 4096;

    private static readonly long HeartbeatIntervalMs = 1000;

    private readonly Func<long> _clockMs;
    private readonly HashSet<string> _firstFrames = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _lastHeartbeatMs = long.MinValue;

    public HostPerfRecorder() : this(() => Environment.TickCount64) { }

    internal HostPerfRecorder(Func<long> clockMs) => _clockMs = clockMs;

    /// <summary>지금 호스트 pid(하트비트 줄에 싣는다).</summary>
    public int? HostProcessId { get; set; }

    public void OnHostMessage(IIpcMessage message)
    {
        if (!PerfTrace.IsEnabled || message is null) return;
        try
        {
            switch (message)
            {
                case WindowOpened opened when EventKeys.TryParse(opened.EventKey, out var kind, out var id):
                    PerfTrace.Mark(PerfStages.PopupOpened, KindName(kind), id,
                                   PerfTrace.Extra(("key", opened.EventKey), ("reused", opened.Reused)));
                    break;
                case StreamStateChanged s when s.State == StreamState.Playing && s.EventKey is not null
                                               && EventKeys.TryParse(s.EventKey, out var kind, out var id):
                    if (!FirstTime(s.EventKey + "|" + s.StreamId)) break;
                    PerfTrace.Mark(PerfStages.FirstFrame, KindName(kind), id,
                                   PerfTrace.Extra(("key", s.EventKey), ("stream", s.StreamId), ("host_ms", s.FirstFrameMs)));
                    break;
                case HeartbeatAck ack:
                    long now = _clockMs();
                    lock (_gate)
                    {
                        if (_lastHeartbeatMs != long.MinValue && now - _lastHeartbeatMs < HeartbeatIntervalMs) break;
                        _lastHeartbeatMs = now;
                    }
                    PerfTrace.Mark(PerfStages.HostHeartbeat, PerfKinds.CameraHost, null, PerfTrace.Extra(
                        ("ui_stall_ms", ack.UiStallMs), ("private_bytes", ack.PrivateBytes),
                        ("open_windows", ack.OpenWindows), ("open_streams", ack.OpenStreams), ("pid", HostProcessId)));
                    break;
            }
        }
        catch
        {
            // 계측은 받기 줄을 깨지 않는다.
        }
    }

    private bool FirstTime(string key)
    {
        lock (_gate)
        {
            if (_firstFrames.Count >= MaxRemembered) _firstFrames.Clear();
            return _firstFrames.Add(key);
        }
    }

    private static string KindName(EventWindowKind kind) => kind == EventWindowKind.Malfunction ? PerfKinds.Malfunction : PerfKinds.Detection;
}
