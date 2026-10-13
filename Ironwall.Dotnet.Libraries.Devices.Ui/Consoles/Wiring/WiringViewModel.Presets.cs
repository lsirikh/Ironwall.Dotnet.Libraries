using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 결선맵 "설치 위치" 절 · [위치 프리셋 적용…](sensor-mount-preset W4 · FR-15 ~ FR-17 · FR-19).
/// 센서는 "위치 프리셋 + 따로 고친 값" 으로 서고, 편집은 펜스 구성 편집 한 걸음(되돌리기 · 저장하기 흐름 그대로)이다.
/// 저장소가 붙지 않으면(GMaps.Db 모듈 없음 · 읽기 실패) 절은 숨고 9자리 편집이 그대로 남는다.
/// </summary>
public sealed partial class WiringViewModel
{
    private ISensorPresetRepository? _presetRepo;
    private ISensorPresetUsageSource? _presetUsage;
    private Func<Task>? _openPresetManager;
    private IReadOnlyList<SensorModel> _presetModels = Array.Empty<SensorModel>();
    private readonly Dictionary<int, IReadOnlyList<SensorMountPreset>> _mountPresets = new();
    private readonly Dictionary<int, IReadOnlyList<SensorAttributePreset>> _attrPresets = new();
    private string? _editPresetHeight;
    private string? _editPresetYaw;

    #region - Attach · load -
    /// <summary>프리셋 저장소를 붙인다(결선 창을 여는 쪽 — 없으면 절이 숨는다). <paramref name="openManager"/> = [프리셋 관리](센서 모델 창).</summary>
    public void AttachSensorPresets(ISensorPresetRepository repository, ISensorPresetUsageSource? usage = null, Func<Task>? openManager = null)
    {
        _presetRepo = repository ?? throw new ArgumentNullException(nameof(repository));
        _presetUsage = usage;
        _openPresetManager = openManager;
    }

    /// <summary>모델 · 프리셋을 읽는다. 실패하면 절을 숨기고 거짓(창은 그대로 · 로그 한 줄).</summary>
    public async Task<bool> LoadSensorPresetsAsync(CancellationToken ct = default)
    {
        if (_presetRepo is null) return false;
        try
        {
            var models = await _presetRepo.GetModelsAsync(ct).ConfigureAwait(true);
            _mountPresets.Clear();
            _attrPresets.Clear();
            foreach (var m in models)
            {
                _mountPresets[m.Id] = await _presetRepo.GetMountPresetsAsync(m.Id, ct).ConfigureAwait(true);
                _attrPresets[m.Id] = await _presetRepo.GetAttributePresetsAsync(m.Id, ct).ConfigureAwait(true);
            }
            _presetModels = models;
            RaisePresetPane();
            FenceChanged?.Invoke(this, EventArgs.Empty);                       // 그림이 프리셋 자세 · 모델 모양으로 다시 선다
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _presetModels = Array.Empty<SensorModel>();
            _log?.Warning($"[Wiring] 센서 프리셋을 읽지 못했습니다 — 9자리 편집으로 엽니다: {ex.Message}");
            RaisePresetPane();
            return false;
        }
    }

    /// <summary>프리셋을 쓸 수 있나(저장소 · 모델을 읽었다).</summary>
    public bool HasSensorPresets => _presetModels.Count > 0;
    #endregion

    #region - Lookups -
    private SensorMountPreset? MountPresetById(int id)
        => _mountPresets.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);

    private EnumDeviceType TypeOfKey(int key)
        => _board.Find(key) is { } row ? WiringTopology.ParseSensorType(row.Facts.TypeText) : EnumDeviceType.NONE;

    private int ModelIdOfKey(int key) => WiringPresetPlan.ModelIdOf(_board.FenceLayout.MountOf(key), TypeOfKey(key), _presetModels);

    /// <summary>
    /// 펜스 보기가 그릴 자세 — 사용자가 고른 위치 프리셋으로 서는 센서만(FR-17 · <see cref="SensorMountSpec.IsPresetChosen"/>).
    /// 저장 때 이관만 된 자리는 <c>null</c> — 9자리 계산(같은 자리 벌리기 · 줄)으로 그려 기존 키 · 끌기와 어긋나지 않게(2026-10-13 실창 회귀).
    /// </summary>
    public SensorMountPose? FencePoseOf(int key)
        => _board.FenceLayout.MountOf(key) is { IsPresetChosen: true } ? PresetPoseOf(key) : null;

    /// <summary>설치 위치 절이 보여 줄 자세 — 이관된 자리도(프리셋 + 차이 = 9자리와 같은 곳).</summary>
    private SensorMountPose? PresetPoseOf(int key)
        => HasSensorPresets ? WiringPresetPlan.PoseOf(_board.FenceLayout.MountOf(key), _board.FenceLayout.Panels, MountPresetById) : null;

    /// <summary>센서의 모델 모양(FR-18) — 모델을 모르면 <c>null</c>(장비 종류 모양).</summary>
    private SensorShape? PresetShapeOf(int key)
    {
        if (!HasSensorPresets) return null;
        var id = ModelIdOfKey(key);
        return _presetModels.FirstOrDefault(m => m.Id == id)?.Shape;
    }

    private IReadOnlyList<int> PresetTargets() => MountTargets();

    /// <summary>
    /// 위치 프리셋 편집 한 걸음 — 이 안에서는 9자리 칸이 같이 바뀌어도 프리셋을 놓지 않는다(<see cref="WiringBoard.KeepMountPresets"/>).
    /// </summary>
    private bool EditPresetFence(Func<WiringFenceLayout, WiringFenceLayout> edit, IReadOnlyList<int>? tieOrder = null)
    {
        _board.KeepMountPresets = true;
        try { return EditFence(edit, tieOrder); }
        finally { _board.KeepMountPresets = false; }
    }
    #endregion

    #region - Install-location section (W4 · FR-15) -
    /// <summary>"설치 위치" 프리셋 줄을 보일까 — 프리셋을 읽었고 체인 센서를 골랐을 때.</summary>
    public bool HasPresetRow => HasSensorPresets && HasMountRow;

    /// <summary>모델 목록(숨긴 모델 제외 — 지금 고른 센서의 모델은 숨겼어도 남긴다).</summary>
    public IReadOnlyList<SensorModel> PresetModelChoices
        => _presetModels.Where(m => !m.IsHidden || m.Id == SelectedPresetModel?.Id).ToList();

    /// <summary>고른 센서의 모델(모두 같을 때).</summary>
    public SensorModel? SelectedPresetModel
    {
        get
        {
            var ids = PresetTargets().Select(ModelIdOfKey).Distinct().ToList();
            return ids.Count == 1 ? _presetModels.FirstOrDefault(m => m.Id == ids[0]) : null;
        }
        set
        {
            if (value is null || value.Id == SelectedPresetModel?.Id) return;
            var preset = DefaultMountPreset(value.Id);
            var targets = PresetTargets();
            var ok = EditPresetFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => !targets.Contains(p.Key) ? p.Value
                : preset is null ? p.Value with { ModelId = value.Id, MountPresetId = null, MountOverride = null }
                : WiringPresetPlan.Apply(p.Value, preset, l.Panels))));
            StatusText = ok ? $"모델 — {targets.Count}대: {value.Name}" : "바뀐 것이 없습니다.";
            RaisePresetPane();
        }
    }

    /// <summary>모델의 위치 프리셋(★기본 먼저).</summary>
    public IReadOnlyList<SensorMountPreset> PresetMountChoices
        => SelectedPresetModel is { } m && _mountPresets.TryGetValue(m.Id, out var list) ? list.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id).ToList() : Array.Empty<SensorMountPreset>();

    private SensorMountPreset? DefaultMountPreset(int modelId)
        => _mountPresets.TryGetValue(modelId, out var list) ? list.FirstOrDefault(p => p.IsDefault) ?? list.FirstOrDefault() : null;

    /// <summary>고른 센서의 위치 프리셋(모두 같을 때) — 고르면 그 자리로 선다(따로 고친 값은 지운다 · 되돌리기 한 걸음).</summary>
    public SensorMountPreset? SelectedMountPreset
    {
        get
        {
            var ids = PresetTargets().Select(k => _board.FenceLayout.MountOf(k)?.MountPresetId).Distinct().ToList();
            return ids is { Count: 1 } && ids[0] is { } id ? MountPresetById(id) : null;
        }
        set
        {
            // 콤보가 목록을 다시 받으며 같은 값을 되써도 적용하지 않는다 — 이관된 자리가 소리 없이 "고른 프리셋" 이 되어 펜스 보기 그림 · 끌기가 바뀌지 않게(2026-10-13 실창 회귀)
            if (value is null || value.Id == SelectedMountPreset?.Id) return;
            ApplyMountPreset(MountPresetApplyScope.Selected, new Dictionary<int, SensorMountPreset> { [value.ModelId] = value });
        }
    }

    /// <summary>프리셋과 다른 값이 있는가(주황 칸 · 칩 "바뀜").</summary>
    public bool HasMountOverride => PresetTargets().Any(k => _board.FenceLayout.MountOf(k)?.MountOverride is { IsEmpty: false });

    /// <summary>"프리셋에서 바뀜: 높이 +0.20".</summary>
    public string MountOverrideText
    {
        get
        {
            var targets = PresetTargets();
            if (targets.Count != 1 || _board.FenceLayout.MountOf(targets[0]) is not { MountPresetId: { } id } m) return string.Empty;
            var text = WiringPresetPlan.OverrideText(MountPresetById(id), m, _board.FenceLayout.Panels);
            // 저장된 값이 허공이었으면 보정 까닭을 같이(그림은 이미 붙은 자리 · 저장하면 반영 — FR-22)
            return WiringPresetPlan.CorrectionOf(m, MountPresetById(id), _board.FenceLayout.Panels) is { } fix && text.Length > 0
                ? $"{text} · 보정: {fix}(저장하면 반영)"
                : text;
        }
    }

    /// <summary>지금 자세(한 대일 때).</summary>
    private SensorMountPose? SelectedPose => PresetTargets() is { Count: 1 } one ? PresetPoseOf(one[0]) : null;

    /// <summary>높이 칸(m) — 프리셋 + 따로 고친 값. 여러 대면 모두 같을 때 그 값(실창 4회차 MNT-R28-425 — 여러 대를 고르면 칸이 비었다).</summary>
    public string PresetHeightText
    {
        get => _editPresetHeight ?? (PresetTargets().Select(k => PresetPoseOf(k)?.HeightM.ToString("0.00", CultureInfo.InvariantCulture)).Distinct().ToList() is [{ } one] ? one : string.Empty);
        set { _editPresetHeight = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>바라보는 방향 칸(°).</summary>
    public string PresetYawText
    {
        get => _editPresetYaw ?? (SelectedPose is { } p ? p.YawDeg.ToString("0", CultureInfo.InvariantCulture) : string.Empty);
        set { _editPresetYaw = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>높이 칸 적용(Enter · 칸을 떠날 때) — 따로 고친 값으로 남는다(기둥 위로 뜨거나 바닥을 뚫지 않게 범위 안으로).</summary>
    public bool ApplyPresetHeight()
    {
        var text = _editPresetHeight;
        _editPresetHeight = null;
        if (text is null) return false;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var h) || !double.IsFinite(h))
        {
            StatusText = "높이는 m 숫자로 적어 주세요.";
            NotifyOfPropertyChange(nameof(PresetHeightText));
            return false;
        }
        return EditOverride(pose => pose with { HeightM = h }, $"높이 {h:0.00} m");
    }

    /// <summary>방향 칸 적용 — 0 = 펜스 따라 · 90 = 바깥쪽 정면 · −90 = 안쪽 정면.</summary>
    public bool ApplyPresetYaw()
    {
        var text = _editPresetYaw;
        _editPresetYaw = null;
        if (text is null) return false;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) || !double.IsFinite(y))
        {
            StatusText = "바라보는 방향은 도(°) 숫자로 적어 주세요.";
            NotifyOfPropertyChange(nameof(PresetYawText));
            return false;
        }
        return EditOverride(pose => pose with { YawDeg = y }, $"바라보는 방향 {SensorMountResolver.NormalizeYaw(y):0}°");
    }

    private bool EditOverride(Func<SensorMountPose, SensorMountPose> change, string what)
    {
        var targets = PresetTargets();
        if (targets.Count == 0) return false;
        var why = AttachReasonOf(targets, change);
        var ok = EditPresetFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => targets.Contains(p.Key) && p.Value.MountPresetId is { } id && MountPresetById(id) is { } preset
                ? WiringPresetPlan.WithOverride(p.Value, preset, l.Panels, change) : p.Value)));
        StatusText = ok ? $"설치 위치 — {what} (프리셋에서 바뀜){why}" : why.Length > 0 ? $"바뀐 것이 없습니다{why}" : "바뀐 것이 없습니다.";
        RaisePresetPane();
        return ok;
    }

    /// <summary>
    /// 바꾼 자세가 허공이라 물체로 끌어 붙였으면 " — 기둥 높이를 넘어 기둥 꼭대기에 붙였습니다"(첫 대상 기준 · FR-22), 아니면 빈 글자.
    /// </summary>
    private string AttachReasonOf(IReadOnlyList<int> targets, Func<SensorMountPose, SensorMountPose> change)
    {
        foreach (var key in targets)
            if (_board.FenceLayout.MountOf(key) is { MountPresetId: { } id } m && MountPresetById(id) is { } preset
                && WiringPresetPlan.OverrideAttachment(m, preset, _board.FenceLayout.Panels, change) is { Clamped: true, Reason: { } reason })
                return $" — {reason}";
        return string.Empty;
    }

    /// <summary>[프리셋으로] — 따로 고친 값을 지운다(되돌리기 한 걸음).</summary>
    public bool ResetMountToPreset()
    {
        var targets = PresetTargets();
        var ok = EditPresetFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => targets.Contains(p.Key) && p.Value.MountPresetId is { } id && MountPresetById(id) is { } preset ? WiringPresetPlan.Apply(p.Value, preset, l.Panels) : p.Value)));
        StatusText = ok ? $"프리셋 자리로 되돌렸습니다 — {targets.Count}대" : "바뀐 것이 없습니다.";
        RaisePresetPane();
        return ok;
    }

    /// <summary>
    /// [이 값으로 새 프리셋] — 고른 센서(한 대) 의 지금 자세를 새 위치 프리셋으로 저장하고 그 센서를 새 프리셋에 잇는다. 저장 실패면 화면 값 유지(NFR-05).
    /// </summary>
    public async Task<SensorMountPreset?> SaveMountAsNewPresetAsync(string? name = null)
    {
        var targets = PresetTargets();
        if (_presetRepo is null || !_presetRepo.CanWrite || targets.Count != 1 || _board.FenceLayout.MountOf(targets[0]) is not { MountPresetId: { } id } mount
            || MountPresetById(id) is not { } basePreset || PresetPoseOf(targets[0]) is not { } pose) return null;
        var bay = FenceSampleBay.FromPanel(_board.FenceLayout.Panels[SensorMountMigration.BayIndexOf(mount, _board.FenceLayout.Panels.Count, basePreset.AlongRatio)]);
        var draft = basePreset with
        {
            Id = 0, IsDefault = false, SnapPoint = mount.MountOverride is { IsEmpty: false } ? null : basePreset.SnapPoint, Anchor = pose.Anchor,
            AlongRatio = pose.AlongRatio, HeightM = pose.HeightM, DepthM = pose.DepthM, YawDeg = pose.YawDeg, TiltDeg = pose.TiltDeg, BracketM = pose.BracketM,
            PanelWidthM = bay.PanelWidthM, FenceHeightM = bay.FenceHeightM, SampleHasRazor = bay.HasRazor,
        };
        draft = draft with { Name = string.IsNullOrWhiteSpace(name) ? SensorMountResolver.SuggestName(draft, bay) : name.Trim() };
        SensorMountPreset saved;
        try { saved = await _presetRepo.SaveMountPresetAsync(draft).ConfigureAwait(true); }
        catch (Exception ex)
        {
            StatusText = "새 프리셋을 저장하지 못했습니다 — 화면 값은 그대로입니다. 다시 저장해 주세요.";
            _log?.Warning($"[Wiring] 위치 프리셋 저장 실패: {ex.Message}");
            return null;
        }
        _mountPresets[saved.ModelId] = (_mountPresets.TryGetValue(saved.ModelId, out var list) ? list.Where(p => p.Id != saved.Id) : Enumerable.Empty<SensorMountPreset>())
            .Append(saved).OrderBy(p => p.Id).ToList();
        ApplyMountPreset(MountPresetApplyScope.Selected, new Dictionary<int, SensorMountPreset> { [saved.ModelId] = saved });
        StatusText = $"새 위치 프리셋 \"{saved.Name}\" 을 저장하고 적용했습니다.";
        return saved;
    }

    /// <summary>
    /// [위치 프리셋 적용…](FR-16) — 범위 안 센서에 모델별 프리셋. 되돌리기 한 걸음 · 저장은 결선맵 [저장하기] 하나로.
    /// </summary>
    public int ApplyMountPreset(MountPresetApplyScope scope, IReadOnlyDictionary<int, SensorMountPreset> presetByModel)
    {
        if (presetByModel is null || presetByModel.Count == 0) return 0;
        var targets = WiringPresetPlan.TargetsOf(scope, PresetTargets(), _board.Chain.Keys.Where(k => _board.FenceLayout.MountOf(k) is not null).ToList(), ModelIdOfKey);
        var applied = 0;
        var ok = EditPresetFence(l =>
        {
            var (mounts, count) = WiringPresetPlan.ApplyToTargets(l.Mounts, targets, presetByModel, ModelIdOfKey, l.Panels);
            applied = count;
            return l.WithMounts(mounts);
        });
        StatusText = ok ? $"위치 프리셋 적용 — {applied}대" : "바뀐 것이 없습니다.";
        RaisePresetPane();
        return ok ? applied : 0;
    }

    /// <summary>범위에 드는 모델(적용 창이 모델별 프리셋 콤보를 만든다).</summary>
    public IReadOnlyList<SensorModel> ApplyScopeModels(MountPresetApplyScope scope)
    {
        var targets = WiringPresetPlan.TargetsOf(scope, PresetTargets(), _board.Chain.Keys.Where(k => _board.FenceLayout.MountOf(k) is not null).ToList(), ModelIdOfKey);
        var ids = targets.Select(ModelIdOfKey).Distinct().ToHashSet();
        return _presetModels.Where(m => ids.Contains(m.Id)).ToList();
    }

    /// <summary>모델의 위치 프리셋 목록(적용 창).</summary>
    public IReadOnlyList<SensorMountPreset> MountPresetsOf(int modelId)
        => _mountPresets.TryGetValue(modelId, out var list) ? list : Array.Empty<SensorMountPreset>();

    /// <summary>모델의 속성 프리셋 목록.</summary>
    public IReadOnlyList<SensorAttributePreset> PresetAttributeChoices
        => SelectedPresetModel is { } m && _attrPresets.TryGetValue(m.Id, out var list) ? list : Array.Empty<SensorAttributePreset>();

    /// <summary>고른 센서의 속성 프리셋(없으면 모델 ★기본) — 고르면 센서에 적는다(GIS 안에서만 · D2).</summary>
    public SensorAttributePreset? SelectedAttributePreset
    {
        get
        {
            var targets = PresetTargets();
            if (targets.Count == 0 || SelectedPresetModel is not { } model || !_attrPresets.TryGetValue(model.Id, out var list)) return null;
            var ids = targets.Select(k => _board.FenceLayout.MountOf(k)?.AttrPresetId).Distinct().ToList();
            if (ids.Count != 1) return null;
            return ids[0] is { } id ? list.FirstOrDefault(p => p.Id == id) : list.FirstOrDefault(p => p.IsDefault);
        }
        set
        {
            if (value is null || value.Id == SelectedAttributePreset?.Id) return;                     // 같은 값 되쓰기는 무시(위와 같다)
            var targets = PresetTargets();
            var ok = EditPresetFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => targets.Contains(p.Key) ? p.Value with { AttrPresetId = value.Id, ModelId = value.ModelId } : p.Value)));
            StatusText = ok ? $"속성 프리셋 — {targets.Count}대: {value.Name}" : "바뀐 것이 없습니다.";
            RaisePresetPane();
        }
    }

    /// <summary>
    /// 탐지 거리(m · 속성 프리셋 "그림" 값) — 결선맵 탐지 범위가 읽는다(FR-14). 프리셋이 없으면 <c>null</c>(종류별 기본).
    /// </summary>
    public double? PresetRangeOf(int key)
    {
        if (!HasSensorPresets) return null;
        var modelId = ModelIdOfKey(key);
        if (!_attrPresets.TryGetValue(modelId, out var list)) return null;
        var id = _board.FenceLayout.MountOf(key)?.AttrPresetId;
        return (id is { } i ? list.FirstOrDefault(p => p.Id == i) : null)?.RangeM ?? list.FirstOrDefault(p => p.IsDefault)?.RangeM;
    }

    /// <summary>
    /// "권장 간격 넘음" 알림(FR-14) — 같은 모델 이웃 센서(체인 차례) 사이가 속성 프리셋 권장 간격보다 먼 쌍. 펜스 구성 · 프리셋이 없으면 빈 목록.
    /// </summary>
    public IReadOnlyList<(int Left, int Right, double Metres, double Limit)> PresetSpacingAlerts()
    {
        var result = new List<(int, int, double, double)>();
        if (!HasSensorPresets || !_board.FenceLayout.IsActive) return result;
        var geometry = _board.FenceLayout.Geometry;
        var keys = _board.Chain.Keys.Where(k => _board.FenceLayout.MountOf(k) is not null).ToList();
        double XOf(int k) => FencePoseOf(k) is { } pose
            ? geometry.Panels[FencePresetBayOf(k) ?? SensorMountMigration.BayIndexOf(_board.FenceLayout.MountOf(k)!, geometry.Panels.Count, pose.AlongRatio)] is var bay ? bay.StartM + bay.SpanM * pose.AlongRatio : 0
            : FenceLayoutMath.PointOf(_board.FenceLayout.MountOf(k)!, geometry).XM;
        foreach (var group in keys.GroupBy(ModelIdOfKey))
        {
            var spacing = _attrPresets.TryGetValue(group.Key, out var list) ? list.FirstOrDefault(p => p.IsDefault)?.SpacingM : null;
            if (spacing is not { } limit || limit <= 0) continue;
            var ordered = group.OrderBy(XOf).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var d = XOf(ordered[i]) - XOf(ordered[i - 1]);
                if (d > limit + 0.01) result.Add((ordered[i - 1], ordered[i], Math.Round(d, 2), limit));
            }
        }
        return result;
    }

    /// <summary>"권장 간격 넘음 2곳" — 없으면 빈 글자.</summary>
    public string PresetSpacingAlertText => PresetSpacingAlerts() is { Count: > 0 } a ? $"권장 간격 넘음 {a.Count}곳" : string.Empty;

    /// <summary>[프리셋 관리] — 센서 모델 창(W1). 닫힌 뒤 다시 읽는다.</summary>
    public async Task OpenPresetManagerAsync()
    {
        if (_openPresetManager is null) return;
        await _openPresetManager().ConfigureAwait(true);
        await LoadSensorPresetsAsync().ConfigureAwait(true);
    }

    public bool CanOpenPresetManager => _openPresetManager is not null;

    /// <summary>작은 창(적용 창)을 띄우는 쪽 — 결선 창을 여는 쪽이 준다. 없으면 [위치 프리셋 적용…] 은 고른 센서에 모델 ★기본을 바로 적용한다.</summary>
    public Func<object, Task<bool?>>? ShowPresetDialog { get; set; }

    /// <summary>[위치 프리셋 적용…](FR-16) — 범위 · 모델별 프리셋을 고르는 창. 적용한 센서 수.</summary>
    public async Task<int> OpenApplyPresetAsync()
    {
        if (!HasSensorPresets) return 0;
        var dialog = new SensorPresets.MountPresetApplyViewModel(this);
        if (ShowPresetDialog is null)
            return ApplyMountPreset(MountPresetApplyScope.Selected, dialog.Rows.Where(r => r.Selected is not null).ToDictionary(r => r.Model.Id, r => r.Selected!));
        await ShowPresetDialog(dialog).ConfigureAwait(true);
        return dialog.Applied;
    }

    /// <summary>지금 열린 제어기의 (저장 전) 쓰임 — 센서 모델 창 · 저장 확인 창의 셈에 덮는다.</summary>
    public IReadOnlyList<SensorPresetUsage> CurrentPresetUsages()
        => _board.FenceLayout.Mounts
            .Where(p => _board.Find(p.Key) is { Id: > 0 } && (p.Value.MountPresetId is not null || p.Value.ModelId is not null))
            .Select(p => new SensorPresetUsage(_fenceKey.Server, Controller.Id, _board.Find(p.Key)!.Id, p.Value.ModelId, p.Value.MountPresetId, p.Value.AttrPresetId,
                                              $"{Controller.Name} · {_board.Find(p.Key)!.Facts.Number}"))
            .ToList();

    /// <summary>저장할 때 옛 자리 이관(FR-19) — 프리셋을 읽었을 때만.</summary>
    private SensorMountSpec MigrateMountForSave(int key, SensorMountSpec mount)
    {
        if (!HasSensorPresets) return mount;
        // 허공 값(옛 값 · 손으로 고친 문서)은 붙은 자리로 고쳐 적는다 — 열 때는 그림만 보정하고, 저장할 때 반영(FR-22)
        if (mount.HasMountPreset) return WiringPresetPlan.NormalizeOverride(mount, MountPresetById(mount.MountPresetId!.Value), _board.FenceLayout.Panels);
        var modelId = ModelIdOfKey(key);
        return WiringPresetPlan.Migrate(mount, modelId, MountPresetsOf(modelId), _board.FenceLayout.Panels);
    }

    private void RaisePresetPane()
    {
        foreach (var name in new[]
        {
            nameof(HasSensorPresets), nameof(HasPresetRow), nameof(PresetModelChoices), nameof(SelectedPresetModel), nameof(PresetMountChoices),
            nameof(SelectedMountPreset), nameof(HasMountOverride), nameof(MountOverrideText), nameof(PresetHeightText), nameof(PresetYawText),
            nameof(PresetAttributeChoices), nameof(SelectedAttributePreset), nameof(PresetSpacingAlertText), nameof(CanOpenPresetManager),
            nameof(HasRangeSensors),                                            // 속성 프리셋 탐지 거리가 바뀌면 [탐지 범위] 를 켤 수 있는지도(MNT-R28-440)
        }) NotifyOfPropertyChange(name);
    }
    #endregion
}
