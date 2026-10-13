using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>프리셋 저장소 출처(설정 <c>"SensorPresets": { "Source": "GisDb" }</c>).</summary>
public enum SensorPresetSource
{
    /// <summary>GIS 로컬 DB(<c>gmap_tiles_db</c>).</summary>
    GisDb = 0,
    /// <summary>GOP API — 나중(지금은 자리만 · 읽기 전용).</summary>
    GopApi = 1,
    /// <summary>메모리(DB 없음 · 시험).</summary>
    Memory = 2,
}

/// <summary>
/// 센서 모델 · 위치 프리셋 · 속성 프리셋 저장소(sensor-mount-preset §5.3) — 화면 · 결선맵은 이것만 안다.
/// </summary>
public interface ISensorPresetRepository
{
    SensorPresetSource Source { get; }

    /// <summary>쓸 수 있나 — GopApi 는 처음엔 읽기 전용.</summary>
    bool CanWrite { get; }

    Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default);
    Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default);

    /// <summary>모델 저장(새로면 id 0 → 저장소가 매긴다). 기본 모델은 숨기기 · 이름만 바뀐다.</summary>
    Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default);

    /// <summary>위치 프리셋 저장 — ★기본이면 같은 모델의 다른 ★기본을 내린다.</summary>
    Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default);

    Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default);

    /// <summary>지우기 — 기본 모델은 지우지 않는다(<see cref="InvalidOperationException"/>). 쓰는 센서 확인은 부르는 쪽(<see cref="SensorPresetRules"/>).</summary>
    Task DeletePresetAsync(PresetKey key, CancellationToken ct = default);
}

/// <summary>
/// 저장소 공통 규칙(구현 · 화면이 같이 쓴다) — ★기본 하나 · 기본 모델 보호 · 새 id.
/// </summary>
public static class SensorPresetRules
{
    /// <summary>모델당 ★기본 하나 — <paramref name="saved"/> 가 기본이면 나머지를 내리고, 기본이 하나도 없으면 첫 것을 기본으로.</summary>
    public static IReadOnlyList<T> OneDefault<T>(IReadOnlyList<T> sameModel, T saved, Func<T, bool> isDefault, Func<T, bool, T> withDefault, Func<T, int> idOf)
    {
        var list = sameModel.Where(p => idOf(p) != idOf(saved)).ToList();
        if (isDefault(saved)) list = list.Select(p => isDefault(p) ? withDefault(p, false) : p).ToList();
        list.Add(saved);
        if (!list.Any(isDefault) && list.Count > 0) list[0] = withDefault(list[0], true);
        return list.OrderBy(idOf).ToList();
    }

    /// <summary>모델을 지울 수 있나 — 기본 모델 불가 · 쓰는 센서가 있으면 불가.</summary>
    public static bool CanDeleteModel(SensorModel model, int usingSensors) => model is not null && !model.IsBuiltin && usingSensors == 0;

    /// <summary>프리셋을 지울 수 있나 — 쓰는 센서 0대일 때만.</summary>
    public static bool CanDeletePreset(int usingSensors) => usingSensors == 0;
}

/// <summary>
/// 메모리 저장소 — DB 가 없을 때의 대체 · 시험용 가짜. 처음엔 회사 기본 7종(<see cref="SensorPresetCatalog"/>)이 들어 있다.
/// </summary>
public sealed class InMemorySensorPresetRepository : ISensorPresetRepository
{
    private readonly object _gate = new();
    private readonly List<SensorModel> _models;
    private readonly List<SensorMountPreset> _mounts;
    private readonly List<SensorAttributePreset> _attrs;

    public InMemorySensorPresetRepository(bool seed = true)
    {
        _models = seed ? SensorPresetCatalog.Models.ToList() : new List<SensorModel>();
        _mounts = seed ? SensorPresetCatalog.MountPresets.ToList() : new List<SensorMountPreset>();
        _attrs = seed ? SensorPresetCatalog.AttributePresets.ToList() : new List<SensorAttributePreset>();
    }

    public SensorPresetSource Source => SensorPresetSource.Memory;
    public bool CanWrite => true;

    /// <summary>다음 저장이 실패하게(시험 — 실패 복구 NFR-05).</summary>
    public bool FailNextSave { get; set; }

    /// <summary>저장 횟수(시험).</summary>
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default)
    { lock (_gate) return Task.FromResult<IReadOnlyList<SensorModel>>(_models.OrderBy(m => m.Id).ToList()); }

    public Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default)
    { lock (_gate) return Task.FromResult<IReadOnlyList<SensorMountPreset>>(_mounts.Where(p => p.ModelId == modelId).OrderBy(p => p.Id).ToList()); }

    public Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default)
    { lock (_gate) return Task.FromResult<IReadOnlyList<SensorAttributePreset>>(_attrs.Where(p => p.ModelId == modelId).OrderBy(p => p.Id).ToList()); }

    public Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_gate)
        {
            ThrowIfFail();
            var saved = model.Id > 0 ? model : model with { Id = _models.Select(m => m.Id).DefaultIfEmpty(0).Max() + 1 };
            var old = _models.FindIndex(m => m.Id == saved.Id);
            if (old >= 0 && _models[old].IsBuiltin) saved = _models[old] with { IsHidden = saved.IsHidden, Name = saved.Name };   // 기본 모델은 숨기기 · 이름만
            saved = saved with { UpdatedAt = DateTime.Now };
            if (old >= 0) _models[old] = saved; else _models.Add(saved);
            return Task.FromResult(saved);
        }
    }

    public Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_gate)
        {
            ThrowIfFail();
            var saved = (preset.Id > 0 ? preset : preset with { Id = _mounts.Select(p => p.Id).DefaultIfEmpty(0).Max() + 1 }) with { UpdatedAt = DateTime.Now };
            var merged = SensorPresetRules.OneDefault(_mounts.Where(p => p.ModelId == saved.ModelId).ToList(), saved, p => p.IsDefault, (p, d) => p with { IsDefault = d }, p => p.Id);
            _mounts.RemoveAll(p => p.ModelId == saved.ModelId);
            _mounts.AddRange(merged);
            return Task.FromResult(merged.First(p => p.Id == saved.Id));
        }
    }

    public Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_gate)
        {
            ThrowIfFail();
            var saved = (preset.Id > 0 ? preset : preset with { Id = _attrs.Select(p => p.Id).DefaultIfEmpty(0).Max() + 1 }) with { UpdatedAt = DateTime.Now };
            var merged = SensorPresetRules.OneDefault(_attrs.Where(p => p.ModelId == saved.ModelId).ToList(), saved, p => p.IsDefault, (p, d) => p with { IsDefault = d }, p => p.Id);
            _attrs.RemoveAll(p => p.ModelId == saved.ModelId);
            _attrs.AddRange(merged);
            return Task.FromResult(merged.First(p => p.Id == saved.Id));
        }
    }

    public Task DeletePresetAsync(PresetKey key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            ThrowIfFail();
            switch (key.Kind)
            {
                case PresetKind.Model:
                    if (_models.Any(m => m.Id == key.Id && m.IsBuiltin)) throw new InvalidOperationException("회사 기본 모델은 지울 수 없습니다 — 숨기기만 됩니다.");
                    _models.RemoveAll(m => m.Id == key.Id);
                    _mounts.RemoveAll(p => p.ModelId == key.Id);
                    _attrs.RemoveAll(p => p.ModelId == key.Id);
                    break;
                case PresetKind.Mount:
                    _mounts.RemoveAll(p => p.Id == key.Id);
                    break;
                default:
                    _attrs.RemoveAll(p => p.Id == key.Id);
                    break;
            }
            return Task.CompletedTask;
        }
    }

    private void ThrowIfFail()
    {
        if (FailNextSave) { FailNextSave = false; throw new InvalidOperationException("저장 실패(시험)"); }
        SaveCount++;
    }
}

/// <summary>
/// GOP API 저장소 자리(sensor-mount-preset D5) — 서버가 경로를 내면 같은 인터페이스로 끼운다. 지금은 모든 호출이 <see cref="NotSupportedException"/>.
/// </summary>
public sealed class GopApiSensorPresetRepository : ISensorPresetRepository
{
    private const string MESSAGE = "GOP API 프리셋 저장소는 아직 없습니다 — 설정 SensorPresets.Source 를 GisDb 로 두세요.";

    public SensorPresetSource Source => SensorPresetSource.GopApi;
    public bool CanWrite => false;

    public Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task DeletePresetAsync(PresetKey key, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
}
