using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>모델 목록 한 줄 — "펜스센서 MEMS · 위치 3 · 380대".</summary>
public sealed record SensorModelRow(SensorModel Model, int MountPresetCount, int SensorCount)
{
    public string Name => Model.IsHidden ? $"{Model.Name} (숨김)" : Model.Name;
    public string CountText => $"위치 {MountPresetCount} · {SensorCount}대";
}

/// <summary>위치 프리셋 한 줄 — "★ 기둥 중간 1.8 m · 밖향 · 27대".</summary>
public sealed record MountPresetRow(SensorMountPreset Preset, int SensorCount)
{
    public string Name => (Preset.IsDefault ? "★ " : string.Empty) + Preset.Name;
    public string CountText => $"{SensorCount}대";
}

/// <summary>
/// 센서 모델 창(W1 · FR-01 ~ FR-03 · FR-13 · FR-20) — 모델 목록 · 위치 프리셋 · 속성 프리셋(W3) · 쓰는 센서 · 내보내기/가져오기.
/// 편집기(W2) · 저장 확인(W5) · 파일 고르기는 여는 쪽이 준 함수로 띄운다(창을 띄우지 않는 헤드리스 시험이 가짜로 바꾼다).
/// </summary>
public sealed class SensorModelsViewModel : Screen
{
    private readonly ISensorPresetRepository _repository;
    private readonly ISensorPresetUsageSource? _usageSource;
    private readonly Func<IReadOnlyList<SensorPresetUsage>>? _currentUsages;
    private IReadOnlyList<SensorPresetUsage> _usages = Array.Empty<SensorPresetUsage>();
    private readonly Dictionary<int, IReadOnlyList<SensorMountPreset>> _mounts = new();
    private readonly Dictionary<int, IReadOnlyList<SensorAttributePreset>> _attrs = new();
    private SensorModelRow? _selectedModel;
    private MountPresetRow? _selectedMount;
    private SensorAttributePreset? _selectedAttr;
    private string _statusText = string.Empty;
    private int _tab;

    /// <param name="currentUsages">지금 열린 결선(저장 전) 쓰임 — 저장된 쓰임 위에 덮는다.</param>
    public SensorModelsViewModel(ISensorPresetRepository repository, ISensorPresetUsageSource? usageSource = null, Func<IReadOnlyList<SensorPresetUsage>>? currentUsages = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _usageSource = usageSource;
        _currentUsages = currentUsages;
        DisplayName = "센서 모델";
    }

    /// <summary>편집기(W2)를 띄운다 — 참이면 저장했다.</summary>
    public Func<MountPresetEditorViewModel, Task<bool>>? OpenEditor { get; set; }

    /// <summary>저장 확인(W5)을 띄운다.</summary>
    public Func<PresetSaveConfirmViewModel, Task<PresetSaveChoice>>? Confirm { get; set; }

    /// <summary>샘플 칸(처음엔 이 현장 결선 값).</summary>
    public FenceSampleBay Sample { get; set; } = SensorPresetCatalog.DefaultBay;

    #region - Facts -
    public ObservableCollection<SensorModelRow> Models { get; } = new();
    public ObservableCollection<MountPresetRow> MountPresets { get; } = new();
    public ObservableCollection<SensorAttributePreset> AttributePresets { get; } = new();
    public ObservableCollection<AttributeRowViewModel> AttributeRows { get; } = new();
    public ObservableCollection<SensorPresetUsage> UsingSensors { get; } = new();

    /// <summary>"저장소: GIS DB" · "저장소: GOP API(읽기 전용)".</summary>
    public string SourceText => _repository.Source switch
    {
        SensorPresetSource.GopApi => "저장소: GOP API(읽기 전용)",
        SensorPresetSource.Memory => "저장소: 이 창만(DB 없음)",
        _ => "저장소: GIS DB",
    };

    /// <summary>편집 단추를 쓸 수 있나(GopApi 면 잠금).</summary>
    public bool CanEdit => _repository.CanWrite;

    /// <summary>"기본 7 · 추가 0".</summary>
    public string BuiltinText => $"기본 {Models.Count(m => m.Model.IsBuiltin)} · 추가 {Models.Count(m => !m.Model.IsBuiltin)}";

    /// <summary>상태줄 — "모델 7 · 위치 프리셋 12 · 속성 프리셋 9".</summary>
    public string SummaryText => $"모델 {Models.Count} · 위치 프리셋 {_mounts.Values.Sum(l => l.Count)} · 속성 프리셋 {_attrs.Values.Sum(l => l.Count)}";

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    /// <summary>모델 머리 — "장비 종류: 복합센서 · 모양: 브래킷 상자 91×108×70".</summary>
    public string ModelHeaderText => _selectedModel?.Model is { } m ? $"장비 종류: {m.DeviceType} · 모양: {SensorModel.ShapeText(m.Shape)} {m.SizeMm}" : string.Empty;

    /// <summary>탭 — 0 위치 프리셋 · 1 속성 프리셋 · 2 쓰는 센서.</summary>
    public int SelectedTab { get => _tab; set => Set(ref _tab, value); }

    public string MountTabText => $"위치 프리셋 {MountPresets.Count}";
    public string AttrTabText => $"속성 프리셋 {AttributePresets.Count}";
    public string UsingTabText => $"쓰는 센서 {UsingSensors.Count}";
    #endregion

    #region - Load -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadAsync(cancellationToken);
    }

    /// <summary>모델 · 프리셋 · 쓰임을 읽는다. 실패하면 안내 한 줄(창은 열린 채).</summary>
    public async Task<bool> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var models = await _repository.GetModelsAsync(ct).ConfigureAwait(true);
            _mounts.Clear();
            _attrs.Clear();
            foreach (var m in models)
            {
                _mounts[m.Id] = await _repository.GetMountPresetsAsync(m.Id, ct).ConfigureAwait(true);
                _attrs[m.Id] = await _repository.GetAttributePresetsAsync(m.Id, ct).ConfigureAwait(true);
            }
            var stored = _usageSource is null ? Array.Empty<SensorPresetUsage>() : await _usageSource.GetUsagesAsync(ct).ConfigureAwait(true);
            _usages = SensorPresetUsageMath.Overlay(stored, _currentUsages?.Invoke());
            var keep = _selectedModel?.Model.Id;
            Models.Clear();
            foreach (var m in models) Models.Add(new SensorModelRow(m, _mounts[m.Id].Count, SensorPresetUsageMath.CountModel(_usages, m.Id)));
            SelectedModel = Models.FirstOrDefault(r => r.Model.Id == keep) ?? Models.FirstOrDefault();
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"센서 모델을 읽지 못했습니다 — {ex.Message}";
            return false;
        }
    }
    #endregion

    #region - Selection -
    public SensorModelRow? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (!Set(ref _selectedModel, value)) return;
            MountPresets.Clear();
            AttributePresets.Clear();
            UsingSensors.Clear();
            if (value is { } row)
            {
                foreach (var p in MountsOf(row.Model.Id) ?? Array.Empty<SensorMountPreset>())
                    MountPresets.Add(new MountPresetRow(p, SensorPresetUsageMath.CountMount(_usages, p.Id)));
                foreach (var a in AttrsOf(row.Model.Id) ?? Array.Empty<SensorAttributePreset>()) AttributePresets.Add(a);
                foreach (var u in _usages.Where(u => u.ModelId == row.Model.Id)) UsingSensors.Add(u);
            }
            SelectedMountPreset = MountPresets.FirstOrDefault(r => r.Preset.IsDefault) ?? MountPresets.FirstOrDefault();
            SelectedAttributePreset = AttributePresets.FirstOrDefault(a => a.IsDefault) ?? AttributePresets.FirstOrDefault();
            foreach (var name in new[] { nameof(ModelHeaderText), nameof(MountTabText), nameof(AttrTabText), nameof(UsingTabText), nameof(CanDeleteModel), nameof(HideText) })
                NotifyOfPropertyChange(name);
        }
    }

    public MountPresetRow? SelectedMountPreset
    {
        get => _selectedMount;
        set { if (Set(ref _selectedMount, value)) NotifyOfPropertyChange(nameof(CanDeleteMountPreset)); }
    }

    public SensorAttributePreset? SelectedAttributePreset
    {
        get => _selectedAttr;
        set
        {
            if (!Set(ref _selectedAttr, value)) return;
            AttributeRows.Clear();
            foreach (var v in value?.Values ?? Array.Empty<SensorAttributeValue>()) AttributeRows.Add(new AttributeRowViewModel(v));
            NotifyOfPropertyChange(nameof(AttributeUsageText));
        }
    }

    /// <summary>"이 프리셋을 쓰는 센서 31대".</summary>
    public string AttributeUsageText
        => _selectedAttr is { } a ? $"이 프리셋을 쓰는 센서 {SensorPresetUsageMath.CountAttribute(_usages, a.Id) + (a.IsDefault ? _usages.Count(u => u.ModelId == a.ModelId && u.AttrPresetId is null) : 0)}대" : string.Empty;
    #endregion

    #region - Models (FR-01 · FR-02) -
    /// <summary>[+ 모델] — 이름 · 장비 종류 · 모양. 같은 이름이 있으면 거절.</summary>
    public async Task<SensorModel?> AddModelAsync(string name, EnumDeviceType type, SensorShape shape, params SensorAnchorKind[] kinds)
    {
        if (!CanEdit) return null;
        if (string.IsNullOrWhiteSpace(name)) { StatusText = "모델 이름을 적어 주세요."; return null; }
        if (Models.Any(m => string.Equals(m.Model.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase))) { StatusText = $"이미 있는 이름입니다 — {name}"; return null; }
        var code = $"USER_{Guid.NewGuid():N}"[..13].ToUpperInvariant();
        var model = new SensorModel { Code = code, Name = name.Trim(), DeviceType = type, Shape = shape, AllowedKinds = kinds.Length > 0 ? kinds : DefaultKinds(shape) };
        return await Guarded(async () =>
        {
            var saved = await _repository.SaveModelAsync(model).ConfigureAwait(true);
            // 새 모델도 ★기본 위치 · 속성 프리셋 하나로 시작한다(새 센서가 설 자리).
            var point = SensorSnapPoints.Allowed(Sample, saved).FirstOrDefault();
            if (point is not null)
            {
                var draft = SensorMountResolver.SnapTo(new SensorMountPreset { ModelId = saved.Id, IsDefault = true }, point, Sample);
                await _repository.SaveMountPresetAsync(draft with { Name = SensorMountResolver.SuggestName(draft, Sample) }).ConfigureAwait(true);
            }
            await _repository.SaveAttributePresetAsync(new SensorAttributePreset { ModelId = saved.Id, Name = "표준", IsDefault = true }).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            SelectedModel = Models.FirstOrDefault(r => r.Model.Id == saved.Id);
            StatusText = $"모델을 추가했습니다 — {saved.Name}";
            return saved;
        }).ConfigureAwait(true);
    }

    private static IReadOnlyList<SensorAnchorKind> DefaultKinds(SensorShape shape) => shape switch
    {
        SensorShape.Fence => new[] { SensorAnchorKind.Mesh, SensorAnchorKind.Rail, SensorAnchorKind.Razor },
        SensorShape.Underground => new[] { SensorAnchorKind.Ground },
        SensorShape.Optical => new[] { SensorAnchorKind.Mesh, SensorAnchorKind.Rail, SensorAnchorKind.Razor, SensorAnchorKind.Ground },
        _ => new[] { SensorAnchorKind.Post, SensorAnchorKind.Rail, SensorAnchorKind.Razor, SensorAnchorKind.Wall },
    };

    /// <summary>[숨기기] · [보이기] — 기본 모델도 된다.</summary>
    public async Task<bool> ToggleHiddenAsync()
    {
        if (!CanEdit || _selectedModel is not { } row) return false;
        return await Guarded(async () =>
        {
            await _repository.SaveModelAsync(row.Model with { IsHidden = !row.Model.IsHidden }).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            StatusText = row.Model.IsHidden ? $"보이게 했습니다 — {row.Model.Name}" : $"숨겼습니다 — {row.Model.Name}";
            return true;
        }).ConfigureAwait(true);
    }

    public string HideText => _selectedModel?.Model.IsHidden == true ? "보이기" : "숨기기";

    /// <summary>모델을 지울 수 있나 — 기본 모델 불가 · 쓰는 센서가 있으면 불가.</summary>
    public bool CanDeleteModel => CanEdit && _selectedModel is { } r && SensorPresetRules.CanDeleteModel(r.Model, r.SensorCount);

    public async Task<bool> DeleteModelAsync()
    {
        if (_selectedModel is not { } row) return false;
        if (row.Model.IsBuiltin) { StatusText = "회사 기본 모델은 지울 수 없습니다 — 숨기기만 됩니다."; return false; }
        if (row.SensorCount > 0) { StatusText = $"쓰는 센서가 {row.SensorCount}대 있어 지울 수 없습니다."; return false; }
        return await Guarded(async () =>
        {
            await _repository.DeletePresetAsync(new PresetKey(PresetKind.Model, row.Model.Id)).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            StatusText = $"지웠습니다 — {row.Model.Name}";
            return true;
        }).ConfigureAwait(true);
    }
    #endregion

    #region - Mount presets (W1 주석 4 · W2) -
    /// <summary>[편집] — 편집기(W2)를 띄우고 닫히면 다시 읽는다.</summary>
    public async Task<bool> EditMountPresetAsync()
    {
        if (_selectedModel is not { } row || _selectedMount is not { } preset || OpenEditor is null) return false;
        var editor = CreateEditor(row.Model, preset.Preset);
        var saved = await OpenEditor(editor).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
        return saved;
    }

    /// <summary>편집기 뷰모델(시험 · 창 여는 쪽이 같이 쓴다).</summary>
    public MountPresetEditorViewModel CreateEditor(SensorModel model, SensorMountPreset preset)
        => new(_repository, model, preset, Sample with { HasRazor = Sample.HasRazor || preset.SampleHasRazor }, _usages, Confirm);

    /// <summary>[새로] — 모델이 붙을 수 있는 첫 점에 선 새 프리셋.</summary>
    public async Task<SensorMountPreset?> NewMountPresetAsync()
    {
        if (!CanEdit || _selectedModel is not { } row) return null;
        var point = SensorSnapPoints.Allowed(Sample, row.Model).FirstOrDefault();
        var draft = point is null ? new SensorMountPreset { ModelId = row.Model.Id } : SensorMountResolver.SnapTo(new SensorMountPreset { ModelId = row.Model.Id }, point, Sample);
        return await SaveMount(draft with { Name = UniqueName(row.Model.Id, SensorMountResolver.SuggestName(draft, Sample)) }, "새 프리셋").ConfigureAwait(true);
    }

    /// <summary>[복제].</summary>
    public async Task<SensorMountPreset?> DuplicateMountPresetAsync()
    {
        if (!CanEdit || _selectedMount is not { } row) return null;
        return await SaveMount(row.Preset with { Id = 0, IsDefault = false, Name = UniqueName(row.Preset.ModelId, row.Preset.Name + " 복사") }, "복제").ConfigureAwait(true);
    }

    /// <summary>[★기본으로] — 모델당 하나.</summary>
    public async Task<bool> MakeMountDefaultAsync()
    {
        if (!CanEdit || _selectedMount is not { } row || row.Preset.IsDefault) return false;
        return await SaveMount(row.Preset with { IsDefault = true }, "★기본").ConfigureAwait(true) is not null;
    }

    public bool CanDeleteMountPreset => CanEdit && _selectedMount is { } r && SensorPresetRules.CanDeletePreset(r.SensorCount);

    /// <summary>[삭제] — 쓰는 센서 0대일 때만.</summary>
    public async Task<bool> DeleteMountPresetAsync()
    {
        if (_selectedMount is not { } row) return false;
        if (!SensorPresetRules.CanDeletePreset(row.SensorCount)) { StatusText = $"센서 {row.SensorCount}대가 쓰고 있어 지울 수 없습니다."; return false; }
        return await Guarded(async () =>
        {
            await _repository.DeletePresetAsync(new PresetKey(PresetKind.Mount, row.Preset.Id)).ConfigureAwait(true);
            // ★기본을 지웠으면 남은 첫 것을 기본으로(모델당 하나).
            if (row.Preset.IsDefault && (await _repository.GetMountPresetsAsync(row.Preset.ModelId).ConfigureAwait(true)).FirstOrDefault() is { } first)
                await _repository.SaveMountPresetAsync(first with { IsDefault = true }).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            StatusText = $"지웠습니다 — {row.Preset.Name}";
            return true;
        }).ConfigureAwait(true);
    }

    private async Task<SensorMountPreset?> SaveMount(SensorMountPreset preset, string what)
        => await Guarded(async () =>
        {
            var saved = await _repository.SaveMountPresetAsync(preset).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            SelectedMountPreset = MountPresets.FirstOrDefault(r => r.Preset.Id == saved.Id);
            StatusText = $"{what} — {saved.Name}";
            return saved;
        }).ConfigureAwait(true);

    private string UniqueName(int modelId, string name)
    {
        var names = (MountsOf(modelId) ??Array.Empty<SensorMountPreset>()).Select(p => p.Name).ToHashSet();
        if (!names.Contains(name)) return name;
        for (var i = 2; ; i++) if (!names.Contains($"{name} {i}")) return $"{name} {i}";
    }
    #endregion

    #region - Attribute presets (W3 · FR-13) -
    /// <summary>[+ 항목].</summary>
    public void AddAttributeRow() => AttributeRows.Add(new AttributeRowViewModel(new SensorAttributeValue($"item_{AttributeRows.Count + 1}", "새 항목", string.Empty, string.Empty, SensorAttributeUse.Reference)));

    public void RemoveAttributeRow(AttributeRowViewModel row) => AttributeRows.Remove(row);

    /// <summary>[저장] — 표의 줄을 지금 속성 프리셋에. 키가 비었거나 겹치면 거절.</summary>
    public async Task<bool> SaveAttributePresetAsync()
    {
        if (!CanEdit || _selectedAttr is not { } preset) return false;
        var values = AttributeRows.Select(r => r.ToValue()).ToList();
        if (values.Any(v => string.IsNullOrWhiteSpace(v.Key))) { StatusText = "항목 키가 빈 줄이 있습니다."; return false; }
        if (values.GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) { StatusText = "항목 키가 겹칩니다."; return false; }
        return await SaveAttr(preset with { Values = values }, "속성 프리셋 저장") is not null;
    }

    public async Task<SensorAttributePreset?> NewAttributePresetAsync()
    {
        if (!CanEdit || _selectedModel is not { } row) return null;
        return await SaveAttr(new SensorAttributePreset { ModelId = row.Model.Id, Name = "새 속성" }, "새 속성 프리셋").ConfigureAwait(true);
    }

    public async Task<SensorAttributePreset?> DuplicateAttributePresetAsync()
    {
        if (!CanEdit || _selectedAttr is not { } a) return null;
        return await SaveAttr(a with { Id = 0, IsDefault = false, Name = a.Name + " 복사" }, "복제").ConfigureAwait(true);
    }

    public async Task<bool> MakeAttributeDefaultAsync()
    {
        if (!CanEdit || _selectedAttr is not { } a || a.IsDefault) return false;
        return await SaveAttr(a with { IsDefault = true }, "★기본") is not null;
    }

    private async Task<SensorAttributePreset?> SaveAttr(SensorAttributePreset preset, string what)
        => await Guarded(async () =>
        {
            var saved = await _repository.SaveAttributePresetAsync(preset).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            SelectedAttributePreset = AttributePresets.FirstOrDefault(a => a.Id == saved.Id);
            StatusText = $"{what} — {saved.Name}";
            return saved;
        }).ConfigureAwait(true);
    #endregion

    #region - Export · import (FR-20) -
    /// <summary>[내보내기] — JSON 글자(파일 쓰기는 여는 쪽).</summary>
    public async Task<string?> ExportAsync()
        => await Guarded(async () =>
        {
            var json = await SensorPresetExchange.ExportAsync(_repository).ConfigureAwait(true);
            StatusText = "내보냈습니다.";
            return json;
        }).ConfigureAwait(true);

    /// <summary>[가져오기] — JSON 글자를 합친다.</summary>
    public async Task<SensorPresetImportResult?> ImportAsync(string json)
    {
        if (!CanEdit) return null;
        if (SensorPresetExchange.Parse(json) is not { } bundle) { StatusText = "읽을 수 없는 파일입니다."; return null; }
        return await Guarded(async () =>
        {
            var result = await SensorPresetExchange.ImportAsync(_repository, bundle).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            StatusText = $"가져왔습니다 — {result.Summary}";
            return result;
        }).ConfigureAwait(true);
    }
    #endregion

    /// <summary>저장소 호출 — 실패하면 화면 그대로 · 안내 한 줄(NFR-05).</summary>
    private async Task<T?> Guarded<T>(Func<Task<T>> work)
    {
        try { return await work().ConfigureAwait(true); }
        catch (Exception ex)
        {
            StatusText = $"저장소에 쓰지 못했습니다 — 화면 값은 그대로입니다. 다시 시도해 주세요. ({ex.Message})";
            return default;
        }
    }

    public Task CloseAsync() => TryCloseAsync(true);

    private IReadOnlyList<SensorMountPreset>? MountsOf(int modelId) => _mounts.TryGetValue(modelId, out var l) ? l : null;
    private IReadOnlyList<SensorAttributePreset>? AttrsOf(int modelId) => _attrs.TryGetValue(modelId, out var l) ? l : null;
}
