using System.Globalization;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 타일 한 칸(UI 스레드 전용). 빈 칸(<see cref="IsEmpty"/>)도 같은 타입 — 격자 모양을 유지한다.
/// PTZ 호출은 전부 시간 제한(FR-26) · 예외 경계 — 실패는 이 타일의 알림 한 줄로만 드러난다.
/// </summary>
internal sealed class TileViewModel : ObservableObject
{
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    private readonly ITileCameraControl? _control;
    private readonly IHostClock _clock;
    private readonly Action _onInteraction;
    private readonly HostLog? _log;
    private readonly string _eventKey;

    private StreamState _streamState = StreamState.Opening;
    private string? _streamDetail;
    private bool _isPadVisible;
    private bool _isSelected;
    private bool _isEnlarged;
    private bool _isHidden;
    private ImageSource? _video;
    private DateTime? _delayUntil;
    private string? _delayText;
    private string? _notice;
    private IReadOnlyList<TilePreset> _presets = Array.Empty<TilePreset>();
    private string? _presetsStatus;

    private TileViewModel(string eventKey, EventWindowCamera? camera, ITileCameraControl? control, IHostClock clock, Action onInteraction, HostLog? log, int emptySlot)
    {
        _eventKey = eventKey;
        Camera = camera;
        _control = control;
        _clock = clock;
        _onInteraction = onInteraction;
        _log = log;
        EmptySlot = emptySlot;
        PtzDisabledReason = camera is null
            ? null
            : PtzAvailability.DisabledReason(camera.IsPtz, camera.PtzAllowed, control?.PtzUnavailableReason);
    }

    public static TileViewModel ForCamera(string eventKey, EventWindowCamera camera, ITileCameraControl control, IHostClock clock, Action onInteraction, HostLog? log)
        => new(eventKey, camera, control, clock, onInteraction, log, -1);

    public static TileViewModel Empty(string eventKey, int slot, IHostClock clock)
        => new(eventKey, null, null, clock, () => { }, null, slot);

    public EventWindowCamera? Camera { get; }
    public ITileCameraControl? Control => _control;
    public bool IsEmpty => Camera is null;
    public bool IsCamera => Camera is not null;
    public int EmptySlot { get; }

    public string CameraId => Camera?.CameraId ?? string.Empty;
    public string Name => Camera is null ? string.Empty : (string.IsNullOrWhiteSpace(Camera.Name) ? Camera.CameraId : Camera.Name!);

    /// <summary><c>CameraPopup.EventWindow.{eventKey}.Tile.{cameraId}</c>(빈 칸은 <c>…Tile.Empty.{n}</c>).</summary>
    public string AutomationId => IsEmpty
        ? $"CameraPopup.EventWindow.{_eventKey}.Tile.Empty.{EmptySlot}"
        : $"CameraPopup.EventWindow.{_eventKey}.Tile.{CameraId}";

    public string TargetPresetLabel
        => Camera is null ? string.Empty
           : !string.IsNullOrWhiteSpace(Camera.TargetPresetName) ? Camera.TargetPresetName!
           : Camera.TargetPresetToken ?? string.Empty;

    /// <summary>"PTZ · P2" · "PTZ" · "고정".</summary>
    public string TagText
        => Camera is null ? string.Empty
           : !Camera.IsPtz ? "고정"
           : string.IsNullOrEmpty(TargetPresetLabel) ? "PTZ" : $"PTZ · {TargetPresetLabel}";

    // ───────── 스트림 상태(FR-26) ─────────

    public StreamState StreamState
    {
        get => _streamState;
        private set
        {
            if (!Set(ref _streamState, value)) return;
            Raise(nameof(StateText));
            Raise(nameof(IsRetryVisible));
            Raise(nameof(IsFailed));
        }
    }

    public string? StreamDetail
    {
        get => _streamDetail;
        private set => Set(ref _streamDetail, value);
    }

    public string StateText => StateTextOf(_streamState);

    public bool IsFailed => _streamState is StreamState.Failed or StreamState.Stalled;
    public bool IsRetryVisible => IsCamera && IsFailed;

    /// <summary>상태 글자(순수) — 색이 아니라 글자로 구분한다.</summary>
    public static string StateTextOf(StreamState state) => state switch
    {
        StreamState.Opening => "연결 중",
        StreamState.Playing => "재생",
        StreamState.Stalled => "멈춤",
        StreamState.Failed => "연결 안 됨",
        _ => "닫힘",
    };

    public void SetStreamState(StreamState state, string? detail)
    {
        StreamState = state;
        StreamDetail = detail;
    }

    /// <summary>"다시 시도" — 창 세션이 이 타일의 생산자만 새로 만든다.</summary>
    public event Action<TileViewModel>? RetryRequested;

    public void RequestRetry()
    {
        if (!IsCamera) return;
        _onInteraction();
        SetStreamState(StreamState.Opening, "retry");
        RetryRequested?.Invoke(this);
    }

    public ImageSource? Video
    {
        get => _video;
        set => Set(ref _video, value);
    }

    // ───────── 선택 · 크게 · 숨김 ─────────

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public bool IsEnlarged
    {
        get => _isEnlarged;
        set => Set(ref _isEnlarged, value);
    }

    /// <summary>다른 타일을 "이 카메라만 크게" 한 동안 숨는다.</summary>
    public bool IsHidden
    {
        get => _isHidden;
        set => Set(ref _isHidden, value);
    }

    // ───────── PTZ(FR-13/14) ─────────

    /// <summary>PTZ 항목 비활성 이유(고정 · 권한 · 제공자). null 이면 가능.</summary>
    public string? PtzDisabledReason { get; }
    public bool IsPtzEnabled => IsCamera && PtzDisabledReason is null;

    public bool IsPadVisible
    {
        get => _isPadVisible;
        private set => Set(ref _isPadVisible, value);
    }

    public void TogglePad()
    {
        if (!IsPtzEnabled) return;
        _onInteraction();
        IsPadVisible = !IsPadVisible;
    }

    public void HidePad() => IsPadVisible = false;

    /// <summary>창이 뜰 때 대상 프리셋으로 보냈는가(닫을 때 복귀 대상).</summary>
    public bool AutoMoveIssued { get; private set; }

    public string? DelayText
    {
        get => _delayText;
        private set
        {
            if (Set(ref _delayText, value)) Raise(nameof(IsDelayVisible));
        }
    }

    public bool IsDelayVisible => _delayText is not null;

    /// <summary>
    /// 창 열림 — PTZ 카메라에 대상 프리셋이 있고 제공자가 PTZ 를 하면 이동을 보내고,
    /// delay 동안 "P2 로 이동 중 · N초"(FR-13). 자동 이동은 사람 조작 권한과 무관한 시스템 동작이다(매핑 설정).
    /// </summary>
    public void BeginAutoMove()
    {
        var cam = Camera;
        if (cam is null || _control is null || !cam.IsPtz || string.IsNullOrWhiteSpace(cam.TargetPresetToken)) return;
        if (_control.PtzUnavailableReason is not null) return;
        AutoMoveIssued = true;
        if (cam.DelaySeconds > 0)
        {
            _delayUntil = _clock.UtcNow.AddSeconds(cam.DelaySeconds);
            UpdateDelayText();
        }
        _ = RunPtzAsync(ct => _control.GotoPresetAsync(cam.TargetPresetToken!, ct), $"{TargetPresetLabel} 이동 실패", cancelsAutoMove: false, countsAsInteraction: false);
    }

    /// <summary>사람이 PTZ 를 만졌다 — 자동 이동 표시를 끝내고 사람 조작을 따른다.</summary>
    public void CancelAutoMove()
    {
        _delayUntil = null;
        DelayText = null;
    }

    public void Tick() => UpdateDelayText();

    private void UpdateDelayText()
    {
        if (_delayUntil is not { } until) return;
        var left = until - _clock.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            CancelAutoMove();
            return;
        }
        int seconds = (int)Math.Ceiling(left.TotalSeconds);
        DelayText = string.Create(CultureInfo.InvariantCulture, $"{TargetPresetLabel} 로 이동 중 · {seconds}초");
    }

    public Task PtzMoveAsync(double pan, double tilt, double zoom)
        => RunPtzAsync(ct => _control!.ContinuousMoveAsync(pan, tilt, zoom, ct), "PTZ 이동 실패");

    public Task PtzStopAsync()
        => RunPtzAsync(async ct => { await _control!.StopAsync(ct).ConfigureAwait(true); return true; }, "PTZ 정지 실패");

    public Task GotoPresetAsync(string token)
        => RunPtzAsync(ct => _control!.GotoPresetAsync(token, ct), "프리셋 이동 실패");

    public Task GotoHomeAsync()
        => RunPtzAsync(ct => _control!.GotoHomeAsync(Camera?.HomePresetToken, ct), "복귀 프리셋 실패");

    /// <summary>창을 닫을 때 — 자동 이동한 타일만 복귀(FR-15). 사람 조작 여부와 무관하게 카메라는 제자리가 아니다.</summary>
    public async Task ReturnHomeOnCloseAsync()
    {
        if (!AutoMoveIssued || _control is null || _control.PtzUnavailableReason is not null) return;
        using var cts = new CancellationTokenSource(CallTimeout);
        try
        {
            await _control.GotoHomeAsync(Camera?.HomePresetToken, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.Warn($"return-home failed {CameraId}: {ex.GetType().Name} {ex.Message}");
        }
    }

    public IReadOnlyList<TilePreset> Presets
    {
        get => _presets;
        private set => Set(ref _presets, value);
    }

    /// <summary>"불러오는 중" · "프리셋 없음" · "불러오기 실패" · null(목록 있음).</summary>
    public string? PresetsStatus
    {
        get => _presetsStatus;
        private set => Set(ref _presetsStatus, value);
    }

    public async Task LoadPresetsAsync()
    {
        if (!IsPtzEnabled || _control is null) return;
        PresetsStatus = "불러오는 중…";
        using var cts = new CancellationTokenSource(CallTimeout);
        try
        {
            var list = await _control.GetPresetsAsync(cts.Token).ConfigureAwait(true);
            Presets = list ?? Array.Empty<TilePreset>();
            PresetsStatus = Presets.Count == 0 ? "프리셋 없음" : null;
        }
        catch (OperationCanceledException)
        {
            PresetsStatus = "불러오기 실패(응답 없음)";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.Warn($"presets failed {CameraId}: {ex.GetType().Name} {ex.Message}");
            PresetsStatus = "불러오기 실패";
        }
    }

    /// <summary>타일 알림 한 줄(PTZ 실패 등). 다음 조작이 성공하면 지운다.</summary>
    public string? Notice
    {
        get => _notice;
        private set
        {
            if (!Set(ref _notice, value)) return;
            Raise(nameof(IsNoticeVisible));
            Raise(nameof(NoticeText));
        }
    }

    public bool IsNoticeVisible => _notice is not null;

    /// <summary>화면 · UIA 가 읽는 알림 글 전체("⚠ P2 이동 실패"). 알림이 없으면 빈 글.</summary>
    public string NoticeText => _notice is null ? string.Empty : "⚠ " + _notice;

    private async Task RunPtzAsync(Func<CancellationToken, Task<bool>> call, string failure, bool cancelsAutoMove = true, bool countsAsInteraction = true)
    {
        if (_control is null || (!IsPtzEnabled && cancelsAutoMove)) return;
        if (countsAsInteraction) _onInteraction();
        if (cancelsAutoMove) CancelAutoMove();
        using var cts = new CancellationTokenSource(CallTimeout);
        try
        {
            bool ok = await call(cts.Token).ConfigureAwait(true);
            Notice = ok ? null : failure;
        }
        catch (OperationCanceledException)
        {
            Notice = $"{failure} · 응답 없음({CallTimeout.TotalSeconds:0}초)";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.Warn($"ptz call failed {CameraId}: {ex.GetType().Name} {ex.Message}");
            Notice = failure;
        }
    }
}
