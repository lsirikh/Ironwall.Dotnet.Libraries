using System.Collections.ObjectModel;
using System.Globalization;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 한 장의 상태(UI 스레드 전용, 창 없이도 동작 — 헤드리스 호스트 · 시험).
/// 머리(배지 · 구역 · 장비 · 종류 · 시각 · 카메라 수 · 📌) · 타일 격자 · 꼬리(닫힐 조건 · 남은 시간 · +N) ·
/// 타이머 닫기(만지면 다시 셈) · 타일 순서 바꾸기 · 크게 보기 · 타일 닫기(FR-09~16).
/// 사람 조작(📌 · 창 이동 · 타일 닫기)은 GIS 에 알린다 — GIS 는 재시작 복원에 반영한다.
/// </summary>
internal sealed class EventWindowViewModel : ObservableObject
{
    private readonly IHostClock _clock;
    private readonly Action<IIpcMessage> _send;
    private readonly HostLog? _log;
    private readonly List<ITileCameraControl> _controls = new();
    private DateTime _lastInteraction;
    private bool _isPinned;
    private int? _remainingSeconds;
    private TileViewModel? _selectedTile;
    private TileViewModel? _enlargedTile;
    private bool _closeRequested;
    private bool _started;

    public EventWindowViewModel(OpenEventWindow message, ITileCameraControlFactory controls, IHostClock clock, Action<IIpcMessage> send, HostLog? log)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        _clock = clock;
        _send = send;
        _log = log;
        _isPinned = message.Pinned;
        _lastInteraction = clock.UtcNow;

        var cameras = message.Cameras.Take(TileGrid.MaxCameras).ToList();
        (BaseColumns, BaseRows) = TileGrid.Resolve(message.GridColumns, message.GridRows, cameras.Count);
        foreach (var camera in cameras)
        {
            ITileCameraControl control;
            try { control = controls.Create(camera); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // 제어 객체 하나 실패는 그 타일만 PTZ 불가로(FR-26).
                _log?.Warn($"tile control create failed {camera.CameraId}: {ex.Message}");
                control = new UnavailableCameraControl("PTZ 제공자 오류");
            }
            _controls.Add(control);
            Tiles.Add(TileViewModel.ForCamera(EventKey, camera, control, clock, NoteInteraction, log));
        }
        FillEmptySlots();
    }

    public OpenEventWindow Message { get; }
    public string EventKey => Message.EventKey;
    public string AutomationId => $"CameraPopup.EventWindow.{EventKey}";

    // ───────── 머리(FR-16) ─────────

    public bool IsDetection => Message.Kind == EventWindowKind.Detection;

    public string BadgeText
        => !string.IsNullOrWhiteSpace(Message.Header.KindLabel) ? Message.Header.KindLabel!
           : IsDetection ? "탐지" : "장애";

    /// <summary>"구역-07 · 펜스 센서 #104 · 침입"(빈 칸은 뺀다).</summary>
    public string HeaderText
    {
        get
        {
            var h = Message.Header;
            var parts = new[] { h.ZoneName, h.DeviceName, h.EventTypeText }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            return parts.Length == 0 ? $"이벤트 {Message.EventId}" : string.Join(" · ", parts);
        }
    }

    public string TimeText
        => Message.Header.OccurredAt is { } at ? at.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty;

    public string CameraCountText => string.Create(CultureInfo.InvariantCulture, $"카메라 {CameraTiles.Count()}대");

    public string WindowTitle
        => !string.IsNullOrWhiteSpace(Message.Title) ? Message.Title! : $"[{BadgeText}] {HeaderText} {TimeText}".TrimEnd();

    public bool IsPinned
    {
        get => _isPinned;
        // 양방향 바인딩(📌 ToggleButton) — UIA Toggle() 도 이 경로로 들어온다.
        set => SetPinned(value);
    }

    public void SetPinned(bool pinned)
    {
        if (!Set(ref _isPinned, pinned, nameof(IsPinned))) return;
        NoteInteraction();
        _send(new PinChanged { EventKey = EventKey, Pinned = pinned });
        RaiseFooter();
    }

    public void TogglePin() => SetPinned(!_isPinned);

    /// <summary>📌 고정 창이 막는 자동 닫기 사유(순수) — 조치보고 · 오래된 창 정리. 사람(User) · 타이머는 막지 않는다.</summary>
    public static bool IsBlockedByPin(bool pinned, EventWindowCloseReason reason)
        => pinned && reason is EventWindowCloseReason.ActionReported or EventWindowCloseReason.Evicted;

    /// <summary>
    /// GIS 닫기 명령을 받을지(FR-15, 호스트가 📌 권위자). 사람이 📌 를 누른 직후 GIS 가 <see cref="PinChanged"/> 를 받기 전에
    /// 보낸 조치보고 · 정리 닫기는 무시하고 <see cref="PinChanged"/>(고정)를 다시 보내 GIS 상태를 맞춘다.
    /// </summary>
    public bool TryAcceptCloseCommand(EventWindowCloseReason reason)
    {
        if (!IsBlockedByPin(_isPinned, reason)) return true;
        _send(new PinChanged { EventKey = EventKey, Pinned = true });
        return false;
    }

    // ───────── 타일 격자(FR-10) ─────────

    public ObservableCollection<TileViewModel> Tiles { get; } = new();
    public IEnumerable<TileViewModel> CameraTiles => Tiles.Where(t => t.IsCamera);

    public int BaseColumns { get; }
    public int BaseRows { get; }
    public int Columns => _enlargedTile is null ? BaseColumns : 1;
    public int Rows => _enlargedTile is null ? BaseRows : 1;

    private void FillEmptySlots()
    {
        int slot = 0;
        while (Tiles.Count < BaseColumns * BaseRows) Tiles.Add(TileViewModel.Empty(EventKey, slot++, _clock));
    }

    public TileViewModel? SelectedTile
    {
        get => _selectedTile;
        private set
        {
            var old = _selectedTile;
            if (!Set(ref _selectedTile, value)) return;
            if (old is not null) old.IsSelected = false;
            if (value is not null) value.IsSelected = true;
        }
    }

    public void Select(TileViewModel? tile)
    {
        if (tile is not null && !tile.IsCamera) tile = null;
        NoteInteraction();
        SelectedTile = tile;
    }

    public TileViewModel? EnlargedTile => _enlargedTile;

    /// <summary>타일이 그려지는 크기가 바뀌었다(크게 보기 켬 · 끔) — 창 세션이 그 크기에 맞는 스트림(메인 · 서브)으로 다시 연다.</summary>
    public event Action<TileViewModel>? TileSizeChanged;

    /// <summary>"이 카메라만 크게" 토글 — 격자를 1×1 로 바꾸고 나머지 타일을 숨긴다(스트림은 계속).</summary>
    public void ToggleEnlarge(TileViewModel tile)
    {
        if (!tile.IsCamera) return;
        NoteInteraction();
        var previous = _enlargedTile;
        _enlargedTile = ReferenceEquals(_enlargedTile, tile) ? null : tile;
        foreach (var t in Tiles)
        {
            t.IsEnlarged = ReferenceEquals(t, _enlargedTile);
            t.IsHidden = _enlargedTile is not null && !t.IsEnlarged;
        }
        Raise(nameof(EnlargedTile));
        Raise(nameof(Columns));
        Raise(nameof(Rows));
        if (previous is not null) TileSizeChanged?.Invoke(previous);
        if (_enlargedTile is not null && !ReferenceEquals(_enlargedTile, previous)) TileSizeChanged?.Invoke(_enlargedTile);
    }

    /// <summary>사람이 타일을 옮겼다(끌기 확정). 삽입 위치는 카메라 타일 목록 기준(<see cref="TileReorder"/>).</summary>
    public bool MoveTile(TileViewModel tile, int insertIndex)
    {
        var cams = CameraTiles.ToList();
        int from = cams.IndexOf(tile);
        if (!TileReorder.IsMove(from, insertIndex, cams.Count)) return false;
        NoteInteraction();
        int to = TileReorder.FinalIndex(from, insertIndex);
        // 카메라 타일은 항상 목록 앞쪽에 연속 — 컬렉션 인덱스 = 카메라 인덱스.
        Tiles.Move(from, to);
        return true;
    }

    /// <summary>키보드 폴백(Alt+←/→).</summary>
    public bool MoveTileBy(TileViewModel tile, int delta)
    {
        var cams = CameraTiles.ToList();
        int insert = TileReorder.KeyboardInsertIndex(cams.IndexOf(tile), delta, cams.Count);
        return insert >= 0 && MoveTile(tile, insert);
    }

    public IReadOnlyList<string> CameraOrder => CameraTiles.Select(t => t.CameraId).ToArray();

    /// <summary>창 세션이 스트림 · 제어를 정리하도록.</summary>
    public event Action<TileViewModel>? TileRemoved;

    /// <summary>"이 타일 닫기" — 창은 그대로, 빈 칸이 뒤에 채워진다. GIS 에 <see cref="TileClosed"/>.</summary>
    public void CloseTile(TileViewModel tile)
    {
        if (!tile.IsCamera || !Tiles.Contains(tile)) return;
        NoteInteraction();
        if (ReferenceEquals(_enlargedTile, tile)) ToggleEnlarge(tile);
        if (ReferenceEquals(_selectedTile, tile)) SelectedTile = null;
        Tiles.Remove(tile);
        FillEmptySlots();
        _send(new TileClosed { EventKey = EventKey, CameraId = tile.CameraId });
        Raise(nameof(CameraCountText));
        TileRemoved?.Invoke(tile);
        _ = tile.StopIfMovingAsync();   // 누르고 있는 채로 타일이 닫혀도 정지는 나간다
        if (tile.Control is { } control) DisposeControl(control);
    }

    // ───────── 꼬리 · 닫기 조건(FR-15) ─────────

    /// <summary>"조치보고 오면 닫힘" · "📌 고정 — 자동으로 닫지 않음" · "직접 닫을 때까지".</summary>
    public string CloseConditionsText
    {
        get
        {
            if (_isPinned) return "📌 고정 — 자동으로 닫지 않음";
            if (Message.CloseOnActionReport) return "조치보고 오면 닫힘";
            return Message.TimerCloseSeconds > 0 ? "시간이 되면 닫힘" : "직접 닫을 때까지";
        }
    }

    public bool IsTimerActive => !_isPinned && Message.TimerCloseSeconds > 0;

    /// <summary>남은 초(타이머가 꺼져 있거나 📌 이면 null).</summary>
    public int? RemainingSeconds
    {
        get => _remainingSeconds;
        private set
        {
            if (Set(ref _remainingSeconds, value)) Raise(nameof(TimerText));
        }
    }

    public string? TimerText => IsTimerActive && _remainingSeconds is { } s ? $"타이머 {s}초" : null;

    public string? ExtraText
        => Message.ExtraCameraCount > 0
            ? string.Create(CultureInfo.InvariantCulture,
                $"매핑 카메라 {Message.Cameras.Count + Message.ExtraCameraCount}대 중 {Math.Min(Message.Cameras.Count, TileGrid.MaxCameras)}대 표시 · +{Message.ExtraCameraCount}")
            : null;

    private void RaiseFooter()
    {
        Raise(nameof(CloseConditionsText));
        Raise(nameof(IsTimerActive));
        UpdateRemaining();
        Raise(nameof(TimerText));
    }

    /// <summary>창을 만졌다(마우스 · 키 · PTZ · 메뉴) — 타이머를 다시 센다.</summary>
    public void NoteInteraction()
    {
        _lastInteraction = _clock.UtcNow;
        UpdateRemaining();
    }

    private void UpdateRemaining()
    {
        if (!IsTimerActive)
        {
            RemainingSeconds = null;
            return;
        }
        var left = TimeSpan.FromSeconds(Message.TimerCloseSeconds) - (_clock.UtcNow - _lastInteraction);
        RemainingSeconds = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
    }

    /// <summary>창이 닫혀야 한다(✕ · 타이머). 창 세션이 받아 실제로 닫는다 — 한 번만 올린다.</summary>
    public event Action<EventWindowCloseReason>? CloseRequested;

    public void RequestClose(EventWindowCloseReason reason)
    {
        if (_closeRequested) return;
        _closeRequested = true;
        CloseRequested?.Invoke(reason);
    }

    /// <summary>열림 직후 한 번 — 자동 프리셋 이동 · 타이머 시작.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _lastInteraction = _clock.UtcNow;
        foreach (var tile in CameraTiles) tile.BeginAutoMove();
        UpdateRemaining();
    }

    /// <summary>주기 호출(창 세션의 타이머) — delay 표시 · 남은 시간 · 타이머 닫기.</summary>
    public void Tick()
    {
        foreach (var tile in CameraTiles) tile.Tick();
        UpdateRemaining();
        if (IsTimerActive && _remainingSeconds == 0) RequestClose(EventWindowCloseReason.Timer);
    }

    /// <summary>사람이 창 머리를 끌어 옮겼다(물리 픽셀) → 계단 줄에서 빠진다.</summary>
    public void NotifyUserMoved(int x, int y)
    {
        NoteInteraction();
        _send(new WindowMoved { EventKey = EventKey, X = x, Y = y });
    }

    /// <summary>닫기 마무리 — 필요하면 자동 이동한 PTZ 를 복귀시키고(시간 제한) 제어 객체를 정리한다. 던지지 않는다.</summary>
    public async Task ShutdownAsync(bool returnHome)
    {
        var tiles = CameraTiles.ToArray();
        foreach (var tile in tiles)
        {
            // 누르고 있는 채로 창이 닫혀도(타이머 · 조치보고 · 사람) 정지는 나간다. 기다리지 않는다 — 실패해도 카메라는 2초 뒤 스스로 멈춘다.
            try { _ = tile.StopIfMovingAsync(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { _log?.Warn($"stop-on-close failed {tile.CameraId}: {ex.Message}"); }
        }
        if (returnHome)
        {
            try
            {
                await Task.WhenAll(tiles.Select(t => t.ReturnHomeOnCloseAsync())).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log?.Warn($"return-home failed {EventKey}: {ex.Message}");
            }
        }
        ITileCameraControl[] controls;
        lock (_controls) { controls = _controls.ToArray(); }
        foreach (var control in controls) DisposeControl(control);
    }

    private void DisposeControl(ITileCameraControl control)
    {
        lock (_controls)
        {
            if (!_controls.Remove(control)) return;
        }
        try { control.Dispose(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.Warn($"tile control dispose failed: {ex.Message}");
        }
    }
}
