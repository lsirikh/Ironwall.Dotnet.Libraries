using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>내보내기 묶음 — 프리셋은 모델 id 대신 모델 code 로 잇는다(현장마다 id 가 다르다).</summary>
public sealed record SensorPresetBundle
{
    public const int SCHEMA = 1;

    [JsonProperty("schema")] public int Schema { get; init; } = SCHEMA;
    [JsonProperty("exported_at")] public DateTime ExportedAt { get; init; }
    [JsonProperty("models")] public IReadOnlyList<SensorModel> Models { get; init; } = Array.Empty<SensorModel>();
    [JsonProperty("mount_presets")] public IReadOnlyList<BundledMount> MountPresets { get; init; } = Array.Empty<BundledMount>();
    [JsonProperty("attribute_presets")] public IReadOnlyList<BundledAttribute> AttributePresets { get; init; } = Array.Empty<BundledAttribute>();

    public sealed record BundledMount([property: JsonProperty("model_code")] string ModelCode, [property: JsonProperty("preset")] SensorMountPreset Preset);
    public sealed record BundledAttribute([property: JsonProperty("model_code")] string ModelCode, [property: JsonProperty("preset")] SensorAttributePreset Preset);
}

/// <summary>가져오기 결과 — 더한 · 바꾼 · 건너뛴 수.</summary>
public sealed record SensorPresetImportResult(int ModelsAdded, int PresetsAdded, int PresetsUpdated, int Skipped)
{
    public string Summary => $"모델 {ModelsAdded}개 추가 · 프리셋 {PresetsAdded}개 추가 · {PresetsUpdated}개 바뀜" + (Skipped > 0 ? $" · {Skipped}개 건너뜀" : string.Empty);
}

/// <summary>
/// JSON 내보내기 · 가져오기(sensor-mount-preset D5 · FR-20) — 여러 PC(현장마다 GIS DB 가 따로)를 맞춘다.
/// 맞춤: 모델 = code, 프리셋 = (모델 code, 이름). 회사 기본 모델은 덮지 않고(숨김도 그대로) 프리셋만 합친다.
/// </summary>
public static class SensorPresetExchange
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Formatting = Formatting.Indented,
    };

    public static async Task<string> ExportAsync(ISensorPresetRepository repository, DateTime? now = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var models = await repository.GetModelsAsync(ct).ConfigureAwait(false);
        var mounts = new List<SensorPresetBundle.BundledMount>();
        var attrs = new List<SensorPresetBundle.BundledAttribute>();
        foreach (var m in models)
        {
            mounts.AddRange((await repository.GetMountPresetsAsync(m.Id, ct).ConfigureAwait(false)).Select(p => new SensorPresetBundle.BundledMount(m.Code, p)));
            attrs.AddRange((await repository.GetAttributePresetsAsync(m.Id, ct).ConfigureAwait(false)).Select(p => new SensorPresetBundle.BundledAttribute(m.Code, p)));
        }
        return Serialize(new SensorPresetBundle { ExportedAt = now ?? DateTime.Now, Models = models, MountPresets = mounts, AttributePresets = attrs });
    }

    public static string Serialize(SensorPresetBundle bundle) => JsonConvert.SerializeObject(bundle ?? throw new ArgumentNullException(nameof(bundle)), Settings);

    /// <summary>읽을 수 없으면 <c>null</c>.</summary>
    public static SensorPresetBundle? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonConvert.DeserializeObject<SensorPresetBundle>(json, Settings); }
        catch (JsonException) { return null; }
    }

    public static async Task<SensorPresetImportResult> ImportAsync(ISensorPresetRepository repository, SensorPresetBundle bundle, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(bundle);
        if (!repository.CanWrite) throw new InvalidOperationException("이 저장소는 읽기 전용입니다.");
        int modelsAdded = 0, added = 0, updated = 0, skipped = 0;
        var existing = (await repository.GetModelsAsync(ct).ConfigureAwait(false)).ToList();
        var idOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in existing) idOf[m.Code] = m.Id;

        foreach (var m in bundle.Models ?? Array.Empty<SensorModel>())
        {
            if (m is null || string.IsNullOrWhiteSpace(m.Code)) { skipped++; continue; }
            if (idOf.TryGetValue(m.Code, out var id))
            {
                var mine = existing.First(e => e.Id == id);
                if (!mine.IsBuiltin) await repository.SaveModelAsync(m with { Id = id, IsBuiltin = false }, ct).ConfigureAwait(false);
                continue;
            }
            var saved = await repository.SaveModelAsync(m with { Id = 0, IsBuiltin = false }, ct).ConfigureAwait(false);
            idOf[m.Code] = saved.Id;
            modelsAdded++;
        }

        foreach (var group in (bundle.MountPresets ?? Array.Empty<SensorPresetBundle.BundledMount>()).Where(b => b?.Preset is not null).GroupBy(b => b.ModelCode))
        {
            if (!idOf.TryGetValue(group.Key ?? string.Empty, out var modelId)) { skipped += group.Count(); continue; }
            var mine = await repository.GetMountPresetsAsync(modelId, ct).ConfigureAwait(false);
            foreach (var b in group)
            {
                var same = mine.FirstOrDefault(p => p.Name == b.Preset.Name);
                await repository.SaveMountPresetAsync(b.Preset with { Id = same?.Id ?? 0, ModelId = modelId }, ct).ConfigureAwait(false);
                if (same is null) added++; else updated++;
            }
        }

        foreach (var group in (bundle.AttributePresets ?? Array.Empty<SensorPresetBundle.BundledAttribute>()).Where(b => b?.Preset is not null).GroupBy(b => b.ModelCode))
        {
            if (!idOf.TryGetValue(group.Key ?? string.Empty, out var modelId)) { skipped += group.Count(); continue; }
            var mine = await repository.GetAttributePresetsAsync(modelId, ct).ConfigureAwait(false);
            foreach (var b in group)
            {
                var same = mine.FirstOrDefault(p => p.Name == b.Preset.Name);
                await repository.SaveAttributePresetAsync(b.Preset with { Id = same?.Id ?? 0, ModelId = modelId }, ct).ConfigureAwait(false);
                if (same is null) added++; else updated++;
            }
        }
        return new SensorPresetImportResult(modelsAdded, added, updated, skipped);
    }
}
