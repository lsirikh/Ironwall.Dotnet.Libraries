using Caliburn.Micro;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>세 방향 보기(sensor-mount-preset FR-08) — 위에서 · 앞에서 · 옆에서 본 모습.</summary>
public enum MountView
{
    /// <summary>위에서 본 모습 — 가로 · 펜스와 거리 · 바라보는 방향.</summary>
    Top = 0,
    /// <summary>앞에서 본 모습 — 가로 · 높이(붙는 점).</summary>
    Front = 1,
    /// <summary>옆에서 본 모습(펜스 줄 방향) — 펜스와 거리 · 높이 · 위아래 각도.</summary>
    Side = 2,
}

/// <summary>그림 위 붙는 점 하나 — 허용(진하게) · 붙을 점(빨강 + 고리).</summary>
public sealed record MountSnapItem(SensorSnapPoint Point, bool IsAllowed, bool IsTarget);

/// <summary>
/// 위치 프리셋 편집기(W2 · sensor-mount-preset FR-04 ~ FR-12) — 담장 한 칸 샘플 위에서 끌기 · 키보드 · 수치 칸으로 자리 · 방향을 정한다.
/// 그림(세 방향 + 3D 미리보기)은 모두 <see cref="Pose"/>(= <see cref="SensorMountResolver"/>, 결선맵과 같은 계산)를 그린다.
/// </summary>
public sealed class MountPresetEditorViewModel : Screen
{
    /// <summary>위 · 옆 그림에서 이 거리(m) 안이면 붙는 점에 붙는다(앞 그림은 늘 가장 가까운 점).</summary>
    public const double SNAP_RADIUS_M = 0.25;

    private readonly ISensorPresetRepository _repository;
    private readonly IReadOnlyList<SensorPresetUsage> _usages;
    private readonly Func<PresetSaveConfirmViewModel, Task<PresetSaveChoice>>? _confirm;
    private FenceSampleBay _bay;
    private SensorMountPreset _draft;
    private SensorMountPreset _saved;
    private SensorMountPreset? _ghost;
    private SensorMountPreset? _beforeDrag;
    private SensorSnapPoint? _target;
    private bool _isDragging;
    private string _statusText = string.Empty;
    private string? _editHeight, _editAlong, _editDepth, _editYaw, _editTilt, _editBracket;

    /// <param name="usages">모든 결선의 쓰임(저장 전 확인 · "센서 N대가 사용").</param>
    /// <param name="confirm">W5 저장 확인을 띄우는 쪽 — 없으면 쓰는 센서가 있는 프리셋은 저장하지 않는다(안전 쪽).</param>
    public MountPresetEditorViewModel(ISensorPresetRepository repository, SensorModel model, SensorMountPreset preset, FenceSampleBay? sample = null,
                                      IReadOnlyList<SensorPresetUsage>? usages = null, Func<PresetSaveConfirmViewModel, Task<PresetSaveChoice>>? confirm = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _saved = preset ?? throw new ArgumentNullException(nameof(preset));
        _draft = preset;
        _usages = usages ?? Array.Empty<SensorPresetUsage>();
        _confirm = confirm;
        _bay = (sample ?? new FenceSampleBay(preset.PanelWidthM, preset.FenceHeightM, preset.SampleHasRazor)).Normalized();
        DisplayName = $"위치 프리셋 편집 · {model.Name}";
        // 저장된 자리가 허공이면(옛 값 · 손으로 고친 값) 열 때 가장 가까운 물체로 보정해 "바뀜" 으로 보인다 — 저장소엔 [저장] 때만 쓴다(FR-22)
        var (fixedDraft, att) = SensorMountResolver.Normalize(preset, _bay);
        if (att.Clamped)
        {
            _draft = fixedDraft;
            _corrected = true;
            _statusText = $"저장된 자리가 물체에 닿지 않아 고쳤습니다 — {att.Reason}(저장하면 반영)";
        }
    }

    /// <summary>열 때 허공 자리를 보정했나(저장 전까지 "바뀜" 하나로 센다).</summary>
    private bool _corrected;

    /// <summary>자유 위치 결과를 붙임 까닭과 함께 받는다 — 허공이라 끌어 붙였으면 상태 줄에 알린다(FR-22).</summary>
    private SensorMountPreset Attached((SensorMountPreset Preset, SensorMountAttachment Attachment) moved)
    {
        if (moved.Attachment is { Clamped: true, Reason: { } reason }) StatusText = reason;
        return moved.Preset;
    }

    #region - Facts -
    public SensorModel Model { get; }

    public bool CanWrite => _repository.CanWrite;

    /// <summary>편집 중인 프리셋(저장 전).</summary>
    public SensorMountPreset Draft => _draft;

    /// <summary>마지막 저장 값([되돌리기]).</summary>
    public SensorMountPreset Saved => _saved;

    /// <summary>샘플 칸(칸 폭 · 펜스 높이 · 철조망 · 담).</summary>
    public FenceSampleBay Bay => _bay;

    /// <summary>지금 그릴 자세 — 끄는 중이면 흐린 미리보기 자세.</summary>
    public SensorMountPose Pose => SensorMountResolver.Resolve(_ghost ?? _draft, null, _bay);

    /// <summary>원래 자리(끄는 중 옅게 그린다).</summary>
    public SensorMountPose? OriginalPose => _isDragging && _beforeDrag is { } b ? SensorMountResolver.Resolve(b, null, _bay) : null;

    public bool IsDragging => _isDragging;

    /// <summary>붙을 점(끄는 중) — 빨강 + 고리.</summary>
    public SensorSnapPoint? TargetPoint => _target;

    /// <summary>"기둥 B · 중간 · 1.8 m 에 붙음" — 끄는 중 붙을 때만.</summary>
    public string AttachText => _isDragging && _target is { } t ? t.AttachText : string.Empty;

    /// <summary>칸의 붙는 점(허용 · 붙을 점 표시).</summary>
    public IReadOnlyList<MountSnapItem> SnapPoints
        => SensorSnapPoints.For(_bay).Select(p => new MountSnapItem(p, Model.Allows(p.Kind), _target?.Name == p.Name || (!_isDragging && _draft.SnapPoint == p.Name))).ToList();

    /// <summary>"센서 4대가 사용" — 쓰는 센서가 없으면 빈 글자.</summary>
    public int UsageCount => _saved.Id > 0 ? SensorPresetUsageMath.CountMount(_usages, _saved.Id) : 0;
    public string UsageText => UsageCount > 0 ? $"센서 {UsageCount}대가 사용" : string.Empty;

    /// <summary>붙은 점 이름(쉬운 말) · 자유 위치.</summary>
    public string SnapText => SensorSnapPoints.Find(_bay, _draft.SnapPoint)?.Label ?? "자유 위치";

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    #endregion

    #region - Sample (FR-04) -
    public double PanelWidthM { get => _bay.PanelWidthM; set => SetBay(_bay with { PanelWidthM = value }); }
    public double FenceHeightM { get => _bay.FenceHeightM; set => SetBay(_bay with { FenceHeightM = value }); }
    public bool HasRazor { get => _bay.HasRazor; set => SetBay(_bay with { HasRazor = value }); }
    public bool IsWall { get => _bay.IsWall; set => SetBay(_bay with { IsWall = value }); }

    private void SetBay(FenceSampleBay bay)
    {
        _bay = bay.Normalized();
        _draft = _draft with { PanelWidthM = _bay.PanelWidthM, FenceHeightM = _bay.FenceHeightM, SampleHasRazor = _bay.HasRazor };
        RaiseAll();
    }
    #endregion

    #region - Drag (FR-06) -
    /// <summary>아이콘을 잡아 데드존을 넘었다 — 원래 자리를 기억한다.</summary>
    public void BeginDrag()
    {
        if (_isDragging) return;
        _beforeDrag = _draft;
        _isDragging = true;
        _ghost = _draft;
        _target = null;
        RaiseAll();
    }

    /// <summary>
    /// 끄는 중 커서 — 그 그림의 두 축(m). 앞 그림은 가장 가까운 허용 점에, 위 · 옆 그림은 <see cref="SNAP_RADIUS_M"/> 안의 점에 붙는다.
    /// <paramref name="free"/>(Alt) 면 붙지 않고 1 cm 단위 자유 위치. 그 그림에 보이는 축만 바꾼다.
    /// </summary>
    public void DragTo(MountView view, double u, double v, bool free)
    {
        if (!_isDragging) BeginDrag();
        var current = SensorMountResolver.Resolve(_beforeDrag ?? _draft, null, _bay);
        var (along, height, depth) = view switch
        {
            MountView.Front => (u, v, current.DepthM),
            MountView.Top => (u, current.HeightM, v),
            _ => (current.AlongM, v, u),
        };
        _target = free ? null : SnapCandidate(view, along, height, depth);
        _ghost = _target is { } t
            ? SensorMountResolver.SnapTo(_draft, t, _bay)
            : Attached(SensorMountResolver.MoveFreeAttached(_draft, _bay, along, height, depth));
        RaiseAll();
    }

    private SensorSnapPoint? SnapCandidate(MountView view, double along, double height, double depth)
    {
        if (view == MountView.Front) return SensorSnapPoints.Nearest(_bay, Model, along, height, depth);
        SensorSnapPoint? best = null;
        var bestD = SNAP_RADIUS_M * SNAP_RADIUS_M;
        foreach (var p in SensorSnapPoints.Allowed(_bay, Model))
        {
            var (pu, pv, cu, cv) = view == MountView.Top
                ? (p.AlongRatio * _bay.PanelWidthM, p.DepthM, along, depth)
                : (p.DepthM, p.HeightM, depth, height);
            var d = (pu - cu) * (pu - cu) + (pv - cv) * (pv - cv);
            if (d <= bestD) { bestD = d; best = p; }
        }
        return best;
    }

    /// <summary>놓기(확정) 또는 취소 — <see cref="CancelDrag"/> 와 같은 끝맺음 하나.</summary>
    public void FinishDrag(bool commit)
    {
        if (!_isDragging) return;
        var next = commit ? _ghost ?? _draft : _beforeDrag ?? _draft;
        _isDragging = false;
        _ghost = null;
        _target = null;
        _beforeDrag = null;
        _draft = next;
        StatusText = commit ? $"{SnapText} · {Pose.HeightM.ToString("0.00", CultureInfo.InvariantCulture)} m" : "원래 자리로 돌렸습니다.";
        RaiseAll();
    }

    /// <summary>Esc — 끄는 중일 때만 소비한다(참 = 소비).</summary>
    public bool CancelDrag()
    {
        if (!_isDragging) return false;
        FinishDrag(commit: false);
        return true;
    }
    #endregion

    #region - Keyboard (FR-07) -
    /// <summary>Tab · Shift+Tab — 다음 · 이전 붙는 점.</summary>
    public void NextSnapPoint(bool backwards = false)
    {
        if (SensorSnapPoints.Next(_bay, Model, _draft.SnapPoint, backwards) is not { } next) return;
        _draft = SensorMountResolver.SnapTo(_draft, next, _bay);
        StatusText = next.AttachText;
        RaiseAll();
    }

    /// <summary>화살표 — 앞에서 본 모습 기준 10 cm(<paramref name="fine"/> = Shift 면 1 cm).</summary>
    public void Nudge(int dx, int dy, bool fine)
    {
        var pose = SensorMountResolver.Resolve(_draft, null, _bay);
        var step = fine ? SensorMountResolver.FINE_NUDGE_M : SensorMountResolver.NUDGE_M;
        _draft = Attached(SensorMountResolver.MoveFreeAttached(_draft, _bay, pose.AlongM + dx * step, pose.HeightM + dy * step, pose.DepthM));
        RaiseAll();
    }
    #endregion

    #region - Direction (FR-09) -
    /// <summary>위에서 본 손잡이를 돌린다 — 손잡이 끝이 센서에서 (펜스 따라, 바깥쪽) 방향. 15° 단위(<paramref name="fine"/> = Alt 면 1°).</summary>
    public void RotateHandle(double dxAlong, double dyOutward, bool fine)
    {
        if (Math.Abs(dxAlong) < 1e-9 && Math.Abs(dyOutward) < 1e-9) return;
        _draft = _draft with { YawDeg = SensorMountResolver.YawFromHandle(dxAlong, dyOutward, fine) };
        RaiseAll();
    }

    /// <summary>옆에서 본 손잡이 — 위아래 각도(아래로 −). 15° 단위(Alt = 1°).</summary>
    public void TiltHandle(double dDepth, double dHeight, bool fine)
    {
        if (Math.Abs(dDepth) < 1e-9 && Math.Abs(dHeight) < 1e-9) return;
        var deg = Math.Atan2(dHeight, Math.Abs(dDepth)) * 180 / Math.PI;
        var step = fine ? 1 : SensorMountResolver.YAW_STEP_DEG;
        _draft = _draft with { TiltDeg = Math.Clamp(Math.Round(deg / step) * step, -90, 90) };
        RaiseAll();
    }

    /// <summary>[펜스 따라 보기] (0°).</summary>
    public void FaceAlong() => SetYaw(SensorMountResolver.YAW_ALONG);
    /// <summary>[바깥쪽 정면 보기] (90°).</summary>
    public void FaceOutward() => SetYaw(SensorMountResolver.YAW_OUTWARD);
    /// <summary>[안쪽 정면 보기] (−90°).</summary>
    public void FaceInward() => SetYaw(SensorMountResolver.YAW_INWARD);

    private void SetYaw(double yaw)
    {
        _draft = _draft with { YawDeg = SensorMountResolver.NormalizeYaw(yaw) };
        RaiseAll();
    }
    #endregion

    #region - Numeric fields (FR-07) -
    public string AlongPercentText { get => _editAlong ?? (Pose.AlongRatio * 100).ToString("0", CultureInfo.InvariantCulture); set { _editAlong = value; NotifyOfPropertyChange(); } }
    public string HeightText { get => _editHeight ?? Pose.HeightM.ToString("0.00", CultureInfo.InvariantCulture); set { _editHeight = value; NotifyOfPropertyChange(); } }
    public string DepthText { get => _editDepth ?? Pose.DepthM.ToString("0.00", CultureInfo.InvariantCulture); set { _editDepth = value; NotifyOfPropertyChange(); } }
    public string YawText { get => _editYaw ?? Pose.YawDeg.ToString("0", CultureInfo.InvariantCulture); set { _editYaw = value; NotifyOfPropertyChange(); } }
    public string TiltText { get => _editTilt ?? Pose.TiltDeg.ToString("0", CultureInfo.InvariantCulture); set { _editTilt = value; NotifyOfPropertyChange(); } }
    public string BracketText { get => _editBracket ?? Pose.BracketM.ToString("0.00", CultureInfo.InvariantCulture); set { _editBracket = value; NotifyOfPropertyChange(); } }

    /// <summary>"펜스에서 떨어진 거리 −0.30 m (밖)".</summary>
    public string DepthSideText => Pose.DepthM switch { < -0.005 => "밖", > 0.005 => "안", _ => "펜스 선" };

    /// <summary>"바깥쪽 정면" 같은 방향 이름.</summary>
    public string FacingText => SensorMountResolver.FacingText(Pose.YawDeg);

    /// <summary>수치 칸 적용(Enter · 칸을 떠날 때) — 읽을 수 없으면 거짓 + 안내.</summary>
    public bool ApplyFields()
    {
        var ok = true;
        double? Read(ref string? text, string what)
        {
            var t = text;
            text = null;
            if (t is null) return null;
            if (double.TryParse(t.Trim().Replace("%", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) return v;
            ok = false;
            StatusText = $"{what} 칸은 숫자로 적어 주세요.";
            return null;
        }
        var along = Read(ref _editAlong, "칸 안 가로");
        var height = Read(ref _editHeight, "높이");
        var depth = Read(ref _editDepth, "펜스에서 떨어진 거리");
        var yaw = Read(ref _editYaw, "바라보는 방향");
        var tilt = Read(ref _editTilt, "위아래 각도");
        var bracket = Read(ref _editBracket, "브래킷");
        if (along is not null || height is not null || depth is not null)
        {
            // 브래킷을 같이 고쳤으면 먼저 적는다 — 펜스에서 떨어진 거리는 브래킷 길이 안에서만(FR-22)
            if (bracket is { } b0) _draft = _draft with { BracketM = Math.Clamp(b0, 0, SensorMountResolver.MAX_BRACKET_M) };
            _draft = Attached(SensorMountResolver.MoveFreeAttached(_draft, _bay, along is { } a ? a / 100 * _bay.PanelWidthM : null, height, depth));
        }
        if (yaw is { } y) _draft = _draft with { YawDeg = SensorMountResolver.NormalizeYaw(y) };
        if (tilt is { } tt) _draft = _draft with { TiltDeg = Math.Clamp(tt, -90, 90) };
        if (bracket is { } b)
        {
            _draft = _draft with { BracketM = Math.Clamp(b, 0, SensorMountResolver.MAX_BRACKET_M) };
            // 브래킷을 줄이면 펜스와 거리도 그 안으로(허공 금지)
            var (fixedDraft, att) = SensorMountResolver.Normalize(_draft, _bay);
            if (att.Clamped) { _draft = fixedDraft; StatusText = att.Reason ?? StatusText; }
        }
        RaiseAll();
        return ok;
    }
    #endregion

    #region - Name · save (FR-11 · FR-12) -
    private string? _name;

    /// <summary>이름 — 비우면 자리를 말하는 제안("기둥 중간 1.8 m · 밖향").</summary>
    public string Name { get => _name ?? _draft.Name; set { _name = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ChangeCount)); NotifyOfPropertyChange(nameof(ChangeText)); } }

    public string SuggestedName => SensorMountResolver.SuggestName(_draft, _bay);

    /// <summary>[이름 제안] — 자리를 말하는 이름으로.</summary>
    public void UseSuggestedName() => Name = SuggestedName;

    /// <summary>바뀐 칸 수("바뀜 n") — 마지막 저장 값과 비교.</summary>
    public int ChangeCount
    {
        get
        {
            var a = SensorMountResolver.Resolve(_saved, null, _bay);
            var b = SensorMountResolver.Resolve(_draft, null, _bay);
            var n = 0;
            if (a.Anchor != b.Anchor || _saved.SnapPoint != _draft.SnapPoint) n++;
            if (Math.Abs(a.AlongM - b.AlongM) >= 0.005) n++;
            if (Math.Abs(a.HeightM - b.HeightM) >= 0.005) n++;
            if (Math.Abs(a.DepthM - b.DepthM) >= 0.005) n++;
            if (Math.Abs(a.YawDeg - b.YawDeg) >= 0.5) n++;
            if (Math.Abs(a.TiltDeg - b.TiltDeg) >= 0.5) n++;
            if (Math.Abs(a.BracketM - b.BracketM) >= 0.005) n++;
            if (_name is not null && _name != _saved.Name) n++;
            if (n == 0 && _corrected && !_draft.SamePlace(_saved)) n++;               // 열 때 보정한 자리(저장하면 반영)
            return n;
        }
    }

    public string ChangeText => ChangeCount > 0 ? $"바뀜 {ChangeCount}" : string.Empty;

    /// <summary>[되돌리기] — 마지막 저장 값.</summary>
    public void Revert()
    {
        if (_isDragging) FinishDrag(false);
        _draft = _saved;
        _name = null;
        _editAlong = _editHeight = _editDepth = _editYaw = _editTilt = _editBracket = null;
        StatusText = "마지막 저장 값으로 되돌렸습니다.";
        RaiseAll();
    }

    /// <summary>저장할 값 — 날 값도 물체에 붙은 값으로 맞춘다(허공 값은 저장되지 않는다 · FR-22).</summary>
    private SensorMountPreset Outgoing()
    {
        var (fixedDraft, att) = SensorMountResolver.Normalize(_draft, _bay);
        return (att.Clamped ? fixedDraft : _draft) with
        {
            Name = string.IsNullOrWhiteSpace(Name) ? SuggestedName : Name.Trim(), PanelWidthM = _bay.PanelWidthM, FenceHeightM = _bay.FenceHeightM, SampleHasRazor = _bay.HasRazor,
        };
    }

    /// <summary>
    /// [저장] — 쓰는 센서가 있고 자리가 바뀌면 W5 확인(취소 · 새 프리셋으로 저장 · 저장). 실패하면 화면 값 그대로 · 거짓(NFR-05).
    /// </summary>
    public async Task<bool> SaveAsync()
    {
        if (!CanWrite) { StatusText = "이 저장소는 읽기 전용입니다."; return false; }
        var outgoing = Outgoing();
        if (_saved.Id > 0 && UsageCount > 0 && !outgoing.SamePlace(_saved))
        {
            var dialog = new PresetSaveConfirmViewModel(_saved.Name, SensorPresetUsageMath.UsingMount(_usages, _saved.Id));
            var choice = _confirm is null ? PresetSaveChoice.Cancel : await _confirm(dialog).ConfigureAwait(true);
            if (choice == PresetSaveChoice.Cancel) { StatusText = "저장하지 않았습니다."; return false; }
            if (choice == PresetSaveChoice.SaveAsNew) return await SaveAsNewAsync().ConfigureAwait(true) is not null;
        }
        return await WriteAsync(outgoing).ConfigureAwait(true) is not null;
    }

    /// <summary>[새 프리셋으로 저장] — 쓰던 센서는 그대로 두고 새 프리셋을 만든다.</summary>
    public async Task<SensorMountPreset?> SaveAsNewAsync()
    {
        if (!CanWrite) { StatusText = "이 저장소는 읽기 전용입니다."; return null; }
        var outgoing = Outgoing();
        var name = outgoing.Name == _saved.Name ? $"{outgoing.Name} (새)" : outgoing.Name;
        return await WriteAsync(outgoing with { Id = 0, IsDefault = false, Name = name }).ConfigureAwait(true);
    }

    private async Task<SensorMountPreset?> WriteAsync(SensorMountPreset outgoing)
    {
        try
        {
            var saved = await _repository.SaveMountPresetAsync(outgoing).ConfigureAwait(true);
            _saved = saved;
            _draft = saved;
            _name = null;
            StatusText = $"저장했습니다 — {saved.Name}";
            RaiseAll();
            return saved;
        }
        catch (Exception ex)
        {
            StatusText = $"저장하지 못했습니다 — 화면 값은 그대로입니다. 다시 저장해 주세요. ({ex.Message})";
            return null;
        }
    }

    /// <summary>[닫기].</summary>
    public Task CloseAsync() => TryCloseAsync(ChangeCount == 0);
    #endregion

    private void RaiseAll() => Refresh();
}
