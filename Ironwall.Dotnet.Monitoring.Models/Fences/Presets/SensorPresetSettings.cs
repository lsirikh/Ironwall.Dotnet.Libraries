using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 설정 <c>"SensorPresets": { "Source": "GisDb" | "GopApi" }</c> 읽기(sensor-mount-preset §5.3 · D5). 키가 없거나 읽지 못하면 <see cref="SensorPresetSource.GisDb"/> —
/// 설치본 appsettings 에 새 키가 없어도 동작이 바뀌지 않게. 루트 <c>SensorPresets</c> 와 <c>AppSettings.SensorPresets</c> 둘 다 본다(Symbol3D 선례).
/// </summary>
public static class SensorPresetSettings
{
    public const string SECTION = "SensorPresets";
    public const string KEY_SOURCE = "Source";

    private static readonly Lazy<SensorPresetSource> _current = new(() => ReadFile(Ironwall.Dotnet.Libraries.Base.Services.AppSettingsLocation.AppSettingsReadPath).Source, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>이 프로세스의 출처(앱 폴더 Config\appsettings.json, 없으면 옛 루트 파일 · 한 번만 읽는다).</summary>
    public static SensorPresetSource Current => _current.Value;

    /// <summary>파일에서 읽는다 — 파일이 없거나 깨졌으면 GisDb 와 까닭(<c>Error</c>).</summary>
    public static (SensorPresetSource Source, string? Error) ReadFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return (SensorPresetSource.GisDb, $"설정 파일 없음: {path}");
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            return (SensorPresetSource.GisDb, ex.Message);
        }
    }

    /// <summary>
    /// JSON 글자에서 읽는다(순수) — 대소문자 무시 · 모르는 값이면 GisDb 와 까닭. <c>Memory</c> 는 시험용으로만 받는다.
    /// </summary>
    public static (SensorPresetSource Source, string? Error) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (SensorPresetSource.GisDb, null);
        JObject root;
        try { root = JObject.Parse(json); }
        catch (Exception ex) { return (SensorPresetSource.GisDb, $"appsettings 를 읽지 못했습니다: {ex.Message}"); }

        var section = Find(root, SECTION) ?? (Find(root, "AppSettings") is JObject app ? Find(app, SECTION) : null);
        if (section is not JObject s || Find(s, KEY_SOURCE) is not JToken token || token.Type == JTokenType.Null) return (SensorPresetSource.GisDb, null);
        var text = token.Type == JTokenType.String ? ((string?)token ?? string.Empty).Trim() : token.ToString().Trim();
        foreach (var source in (SensorPresetSource[])Enum.GetValues(typeof(SensorPresetSource)))
            if (string.Equals(source.ToString(), text, StringComparison.OrdinalIgnoreCase)) return (source, null);
        return (SensorPresetSource.GisDb, $"SensorPresets.Source 값 \"{text}\" 을 모릅니다 — GisDb 로 엽니다.");
    }

    private static JToken? Find(JObject o, string name)
    {
        foreach (var p in o.Properties())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }
}

/// <summary>
/// 설정 출처로 저장소를 고른다(sensor-mount-preset §5.3) — 화면 · 결선맵은 고른 결과의 <see cref="ISensorPresetRepository.Source"/> · <see cref="ISensorPresetRepository.CanWrite"/> 만 본다.
/// </summary>
public static class SensorPresetRepositoryFactory
{
    /// <param name="source">설정 출처.</param>
    /// <param name="gisDb">GIS 로컬 DB 저장소를 만드는 쪽(GMaps.Db 모듈) — GisDb 일 때만 부른다.</param>
    public static ISensorPresetRepository Create(SensorPresetSource source, Func<ISensorPresetRepository> gisDb)
    {
        ArgumentNullException.ThrowIfNull(gisDb);
        return source switch
        {
            // GOP API 는 아직 자리만(모든 호출 NotSupported) — 읽기는 회사 기본 7종으로 대신 보이고, 편집은 잠긴다(W1 "GOP API(읽기 전용)").
            SensorPresetSource.GopApi => new ReadOnlySensorPresetRepository(new GopApiSensorPresetRepository(), new InMemorySensorPresetRepository()),
            SensorPresetSource.Memory => new InMemorySensorPresetRepository(),
            _ => gisDb(),
        };
    }
}

/// <summary>
/// 읽기 전용 저장소 — 출처는 <paramref name="primary"/> 의 것, 쓰기는 늘 거부(<see cref="CanWrite"/> = 거짓 · <see cref="NotSupportedException"/>).
/// 읽기가 아직 없는 출처(NotSupported)면 <paramref name="readFallback"/> 에서 읽는다.
/// </summary>
public sealed class ReadOnlySensorPresetRepository : ISensorPresetRepository
{
    private const string MESSAGE = "이 프리셋 저장소는 읽기 전용입니다 — 편집은 설정 SensorPresets.Source 를 GisDb 로 둔 뒤에.";
    private readonly ISensorPresetRepository _primary;
    private readonly ISensorPresetRepository? _fallback;

    public ReadOnlySensorPresetRepository(ISensorPresetRepository primary, ISensorPresetRepository? readFallback = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = readFallback;
    }

    public SensorPresetSource Source => _primary.Source;
    public bool CanWrite => false;

    public Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default) => Read(r => r.GetModelsAsync(ct));
    public Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default) => Read(r => r.GetMountPresetsAsync(modelId, ct));
    public Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default) => Read(r => r.GetAttributePresetsAsync(modelId, ct));

    public Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);
    public Task DeletePresetAsync(PresetKey key, CancellationToken ct = default) => throw new NotSupportedException(MESSAGE);

    private async Task<T> Read<T>(Func<ISensorPresetRepository, Task<T>> read)
    {
        try { return await read(_primary).ConfigureAwait(false); }
        catch (NotSupportedException) when (_fallback is not null) { return await read(_fallback).ConfigureAwait(false); }
    }
}
