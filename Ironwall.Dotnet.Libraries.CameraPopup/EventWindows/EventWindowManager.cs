using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using ContractRect = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages.PixelRect;
using ScreenRect = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.PixelRect;

namespace Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;

/****************************************************************************
   Purpose      : GIS 쪽 이벤트 창 관리자 (PRD camera-popup-modes FR-09 · 11 · 12 · 15 · 27 · 28 · NFR-02)
                  - 이벤트 키 → 창, 같은 이벤트는 앞으로만
                  - 동시 창 한도(설정 1~10) → 고정 안 된 가장 오래된 창 정리(Evicted)
                  - 대상 모니터 작업영역 안 계단 자리(T-03 순수 함수) · 옮긴 창은 줄에서 빠짐 · 닫힌 자리는 비지만 다시 늘어놓지 않음
                  - 모니터 없음 · 화면 밖 → 주 모니터 안쪽 + 안내 한 번
   Created By   : Claude (T-04/T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 상태는 자기 잠금 안의 장부(<see cref="EventWindowSlots"/>)뿐이고, 호스트 명령은 <b>잠금 밖에서</b> 감시자에 보내고 잊는다.
/// 감시자(<see cref="ICameraPopupHost"/>)는 기다리지 않는 창구라 NATS 처리 줄 · UI 스레드를 붙잡지 않는다(FR-28).
/// 호스트가 알려 주는 닫힘 · 이동 · 📌 은 <see cref="ICameraPopupHost.StatusReceived"/> 로 받아 장부에 반영한다.
/// </summary>
public sealed class EventWindowManager : IEventWindowManager, IDisposable
{
    /// <summary>모니터를 하나도 못 읽었을 때 쓰는 가상 작업영역(물리 픽셀).</summary>
    internal static readonly ScreenRect FallbackWorkArea = new(0, 0, 1920, 1080);

    private readonly object _gate = new();
    private readonly EventWindowSlots _slots = new();
    private readonly ICameraPopupHost _host;
    private readonly ICameraPopupSettingsSource _settings;
    private readonly IDisplayMonitorProvider _monitors;
    private readonly ILogService? _log;
    private string? _lastNotice;
    private int _disposed;

    public EventWindowManager(ICameraPopupHost host, ICameraPopupSettingsSource settings, IDisplayMonitorProvider monitors, ILogService? log = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        _log = log;
        _host.StatusReceived += OnHostStatus;
    }

    public event EventHandler<string>? NoticeRaised;

    public int OpenCount
    {
        get { lock (_gate) return _slots.Count; }
    }

    public CameraPopupSettings CurrentSettings => ReadSettings();

    public bool IsOpen(string eventKey)
    {
        if (string.IsNullOrEmpty(eventKey)) return false;
        lock (_gate) return _slots.Contains(eventKey);
    }

    /// <summary>시험 · 진단 — 키의 계단 번호(옮겨졌거나 모르면 null).</summary>
    internal int? CascadeIndexOf(string eventKey)
    {
        lock (_gate) return _slots.CascadeIndexOf(eventKey);
    }

    // ───────────────────────── 열기 ─────────────────────────

    public EventWindowOpenResult Open(EventWindowRequest request)
    {
        string? reserved = null;   // 장부에 올렸지만 아직 명령을 못 보낸 키 — 실패하면 되돌린다
        try
        {
            if (Volatile.Read(ref _disposed) == 1 || request is null || string.IsNullOrWhiteSpace(request.EventId))
                return EventWindowOpenResult.Failed;

            var key = request.EventKey;
            if (IsOpen(key))
            {
                _host.BringEventWindowToFront(key);
                return EventWindowOpenResult.BroughtToFront;
            }

            var settings = ReadSettings();
            if (settings.Mode != CameraPopupMode.Self) return EventWindowOpenResult.Disabled;
            if (request.Cameras is null || request.Cameras.Count == 0) return EventWindowOpenResult.NoCameras;

            // 모니터 조회(Win32)는 잠금 밖에서 — 빠르지만 잠금 안에서 외부 호출을 하지 않는다.
            var area = ResolveArea(settings);

            IReadOnlyList<string> evicted = Array.Empty<string>();
            ScreenRect rect = default;
            EventWindowOpenResult outcome;
            lock (_gate)
            {
                if (_slots.Contains(key))
                {
                    outcome = EventWindowOpenResult.BroughtToFront;   // 잠금 사이에 같은 이벤트가 먼저 열렸다
                }
                else
                {
                    evicted = _slots.MakeRoom(settings.MaxOpenWindows);
                    if (!_slots.HasRoom(settings.MaxOpenWindows))
                    {
                        outcome = EventWindowOpenResult.AllPinned;
                    }
                    else
                    {
                        var index = _slots.Add(key);
                        reserved = key;
                        rect = CameraPopupPlacement.CascadeAt(area.WorkArea, settings.FirstWindowX, settings.FirstWindowY,
                                                              settings.WindowWidth, settings.WindowHeight, settings.CascadeStepPx, index);
                        outcome = EventWindowOpenResult.Opened;
                    }
                }
            }

            // 명령은 잠금 밖에서 — 뺀 창을 먼저 닫고 새 창을 연다.
            foreach (var old in evicted)
            {
                _log?.Info($"[EventWindow] 동시 창 한도 {settings.MaxOpenWindows} — 오래된 창 정리: {old}");
                _host.CloseEventWindow(old, EventWindowCloseReason.Evicted, settings.ReturnHomePresetOnClose);
            }

            switch (outcome)
            {
                case EventWindowOpenResult.BroughtToFront:
                    _host.BringEventWindowToFront(key);
                    return outcome;
                case EventWindowOpenResult.AllPinned:
                    _log?.Warning($"[EventWindow] 열린 창 {settings.MaxOpenWindows}개가 전부 고정이라 새 창을 열지 않음: {key}");
                    RaiseNotice($"이벤트 창 {settings.MaxOpenWindows}개가 모두 고정되어 새 창을 열지 않았습니다 — 고정을 풀면 다음 이벤트부터 열립니다.");
                    return outcome;
            }

            _host.OpenEventWindow(BuildMessage(request, settings, area, rect));
            reserved = null;
            ReportPlacement(area);
            return EventWindowOpenResult.Opened;
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] Open 실패 {request?.EventKey}: {ex.GetType().Name} {ex.Message}");
            if (reserved is not null)
            {
                try { lock (_gate) _slots.Remove(reserved); }
                catch (Exception rollback) { _log?.Warning($"[EventWindow] 장부 되돌리기 실패: {rollback.Message}"); }
            }
            return EventWindowOpenResult.Failed;
        }
    }

    public bool BringToFront(EventWindowKind kind, string eventId)
    {
        try
        {
            var key = EventKeys.Build(kind, eventId);
            if (!IsOpen(key)) return false;
            _host.BringEventWindowToFront(key);
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] BringToFront 실패: {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }

    // ───────────────────────── 닫기 ─────────────────────────

    public bool CloseForActionReport(EventWindowKind kind, string eventId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventId)) return false;
            var key = EventKeys.Build(kind, eventId);
            var settings = ReadSettings();
            lock (_gate)
            {
                if (!_slots.Contains(key)) return false;
                if (!settings.CloseOnActionReport) return false;
                if (_slots.IsPinned(key))
                {
                    _log?.Info($"[EventWindow] 조치보고 — 📌 고정 창이라 닫지 않음: {key}");
                    return false;
                }
                _slots.Remove(key);
            }
            _host.CloseEventWindow(key, EventWindowCloseReason.ActionReported, settings.ReturnHomePresetOnClose);
            _log?.Info($"[EventWindow] 조치보고로 닫음: {key}");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] CloseForActionReport 실패: {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }

    // ───────────────────────── 호스트 알림 ─────────────────────────

    private void OnHostStatus(object? sender, CameraPopupStatusEventArgs e)
    {
        try
        {
            switch (e.Message)
            {
                case WindowClosed closed:
                    lock (_gate) _slots.Remove(closed.EventKey);   // 자리는 비지만 남은 창은 그대로(FR-12)
                    break;
                case WindowMoved moved:
                    lock (_gate) _slots.LeaveCascade(moved.EventKey);   // 사람이 옮긴 창은 계단 줄에서 빠진다
                    break;
                case PinChanged pin:
                    lock (_gate) _slots.SetPinned(pin.EventKey, pin.Pinned);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventWindow] 호스트 알림 처리 실패: {ex.GetType().Name} {ex.Message}");
        }
    }

    // ───────────────────────── 배치 ─────────────────────────

    /// <summary>창을 놓을 모니터 · 작업영역과 안내 사유.</summary>
    internal readonly record struct PlacementArea(DisplayMonitorInfo? Monitor, ScreenRect WorkArea, MonitorMatch Match, bool Clamped);

    private PlacementArea ResolveArea(CameraPopupSettings settings)
    {
        IReadOnlyList<DisplayMonitorInfo>? monitors;
        try { monitors = _monitors.GetMonitors(); }
        catch (Exception ex)
        {
            _log?.Warning($"[EventWindow] 모니터 조회 실패: {ex.GetType().Name} {ex.Message}");
            monitors = null;
        }
        return ResolveArea(monitors, settings);
    }

    /// <summary>순수 — 모니터 찾기 + 첫 위치가 화면 밖인지.</summary>
    internal static PlacementArea ResolveArea(IReadOnlyList<DisplayMonitorInfo>? monitors, CameraPopupSettings settings)
    {
        var resolution = CameraPopupPlacement.ResolveMonitor(monitors, settings.TargetMonitorId);
        var workArea = resolution.Monitor?.WorkArea is { IsEmpty: false } w ? w : FallbackWorkArea;
        var clamp = CameraPopupPlacement.ClampRelative(workArea, settings.FirstWindowX, settings.FirstWindowY,
                                                      settings.WindowWidth, settings.WindowHeight);
        return new PlacementArea(resolution.Monitor, workArea, resolution.Match, clamp.Clamped);
    }

    private void ReportPlacement(PlacementArea area)
    {
        string? notice = area.Match switch
        {
            MonitorMatch.FallbackPrimary => "이벤트 창 대상 모니터를 찾지 못해 주 모니터에 띄웁니다. 설정 콘솔에서 모니터를 다시 고르세요.",
            MonitorMatch.NoMonitors => "모니터 정보를 읽지 못해 이벤트 창을 기본 위치에 띄웁니다.",
            MonitorMatch.ResolutionChanged => "대상 모니터의 해상도가 바뀌어 이벤트 창 위치를 화면 안쪽으로 맞췄습니다.",
            _ => area.Clamped ? "이벤트 창 첫 위치 · 크기가 화면을 벗어나 화면 안쪽으로 맞췄습니다." : null,
        };
        if (notice is null)
        {
            Volatile.Write(ref _lastNotice, null);   // 정상으로 돌아오면 다음 이상을 다시 한 번 알린다
            return;
        }
        if (Interlocked.Exchange(ref _lastNotice, notice) == notice) return;   // 같은 안내는 한 번만
        _log?.Warning($"[EventWindow] {notice}");
        RaiseNotice(notice);
    }

    private void RaiseNotice(string text)
    {
        var handlers = NoticeRaised;
        if (handlers is null) return;
        foreach (EventHandler<string> h in handlers.GetInvocationList())
        {
            try { h(this, text); }
            catch (Exception ex) { _log?.Error($"[EventWindow] NoticeRaised 구독자 실패: {ex.Message}"); }
        }
    }

    // ───────────────────────── 메시지 ─────────────────────────

    /// <summary>순수 — 요청 + 설정 + 자리 → 호스트 계약(물리 픽셀).</summary>
    internal static OpenEventWindow BuildMessage(EventWindowRequest request, CameraPopupSettings settings, PlacementArea area, ScreenRect rect)
    {
        var cameras = request.Cameras.Take(TileGrid.MaxCameras).ToList();
        // 설정 격자는 "창당 카메라 수" 기준 — 실제 카메라가 적으면 그 수에 맞는 격자로 스냅(FR-10).
        var layout = CameraPopupGridLayouts.Snap(cameras.Count, settings.GridLayout);
        var monitor = area.Monitor;
        return new OpenEventWindow
        {
            Kind = request.Kind,
            EventId = request.EventId,
            Header = request.Header ?? new EventWindowHeader(),
            Cameras = cameras,
            GridColumns = layout.Columns,
            GridRows = layout.Rows,
            ExtraCameraCount = Math.Max(0, request.ExtraCameraCount),
            MonitorBounds = ToContract(monitor?.Bounds ?? area.WorkArea),
            MonitorWorkArea = ToContract(area.WorkArea),
            DpiScale = monitor is { Dpi: > 0 } m ? m.Dpi / 96d : 1.0,
            MonitorDeviceName = monitor?.DeviceName,
            Window = ToContract(rect),
            AlwaysOnTop = settings.AlwaysOnTop,
            TimerCloseSeconds = settings.CloseByTimer ? settings.CloseTimerSeconds : 0,
            CloseOnActionReport = settings.CloseOnActionReport,
            ReturnHomeOnClose = settings.ReturnHomePresetOnClose,
            Pinned = false,
            Title = request.Title,
        };
    }

    private static ContractRect ToContract(ScreenRect r) => new() { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };

    private CameraPopupSettings ReadSettings()
    {
        try { return (_settings.Current ?? new CameraPopupSettings { Mode = CameraPopupMode.None }).Normalize(); }
        catch (Exception ex)
        {
            _log?.Warning($"[EventWindow] 설정 읽기 실패 — 이벤트 창 끔: {ex.GetType().Name} {ex.Message}");
            return new CameraPopupSettings { Mode = CameraPopupMode.None };
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _host.StatusReceived -= OnHostStatus; }
        catch (Exception ex) { _log?.Warning($"[EventWindow] 구독 해제 실패: {ex.Message}"); }
    }
}
