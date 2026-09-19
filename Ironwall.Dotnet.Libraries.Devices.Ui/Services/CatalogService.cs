using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Catalog;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/// <inheritdoc cref="ICatalogService"/>
/// <remarks>
/// <b>스레드</b>: 패널 VM(UI 스레드)과 NATS 콜백(<c>SYNC_CATALOG</c>)이 함께 부른다.
/// 적재된 카탈로그는 <b>불변 스냅샷</b>이고 참조 하나를 통째 바꾼다 — 읽는 쪽은 락 없이 일관된 판을 본다.
/// 진행 중 요청은 <see cref="_gate"/> 안에서 한 개로 합류시키고, 이벤트는 <b>락 밖에서</b> 발화한다.
/// </remarks>
public sealed class CatalogService : ICatalogService, IComponentCatalog
{
    #region - Ctors -
    public CatalogService(IDeviceApiService apiService, DeviceQueryPolicy? policy = null, ILogService? log = null)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _policy = policy ?? DeviceQueryPolicy.Resolve();
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public bool IsLoaded => _snapshot != null;

    public event EventHandler? CatalogChanged;

    public Task<bool> EnsureLoadedAsync(CancellationToken token = default)
        => IsLoaded ? Task.FromResult(true) : LoadAsync(force: false, token);

    public Task<bool> RefreshAsync(CancellationToken token = default)
        => LoadAsync(force: true, token);

    public CatalogTypeAxis? TypeAxis(EnumDeviceCategory category)
        => _snapshot != null && _snapshot.TypeAxes.TryGetValue(category, out var axis) ? axis : null;

    public IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category)
        => TypeAxis(category)?.Values ?? Array.Empty<CatalogOption>();

    public bool IsTypeAxisValue(EnumDeviceCategory category, string? code)
        => Find(TypeAxisValues(category), code) != null;

    public string TypeAxisLabel(EnumDeviceCategory category, string? code)
        => Find(TypeAxisValues(category), code)?.Label ?? code?.Trim() ?? string.Empty;

    public IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category)
        => _snapshot != null && _snapshot.ExtraAxes.TryGetValue(category, out var axes) ? axes : Array.Empty<CatalogExtraAxis>();

    public IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null)
    {
        if (_snapshot == null || string.IsNullOrWhiteSpace(name)) return Array.Empty<CatalogOption>();
        if (!_snapshot.Vocabularies.TryGetValue(name, out var entries)) return Array.Empty<CatalogOption>();

        return entries
            .Where(e => includeDeprecated || !e.Option.IsDeprecated)
            .Where(e => appliesTo == null || e.AppliesTo == null || e.AppliesTo.Contains(appliesTo.Value))
            .Select(e => e.Option)
            .ToArray();
    }

    public string LabelOf(string vocabularyName, string? code)
        => Find(Vocabulary(vocabularyName, includeDeprecated: true), code)?.Label ?? code?.Trim() ?? string.Empty;
    #endregion

    #region - Implementation of IComponentCatalog (조립기 팔레트) -
    // ICatalogService 를 넓히지 않고 두 번째 인터페이스로 붙인다 — 그 인터페이스에 멤버를 더하면
    // 목/페이크 구현이 한꺼번에 깨진다(실증된 함정). 적재는 하나다: IsLoaded · EnsureLoadedAsync ·
    // CatalogChanged 는 위쪽 구현을 그대로 쓴다(한 번 읽으면 두 인터페이스가 같이 산다).

    /// <inheritdoc/>
    public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false)
    {
        var snapshot = _snapshot;                                   // 지역으로 한 번만 집는다 — 중간에 판이 바뀌어도 일관
        if (snapshot == null) return Array.Empty<ComponentTypeInfo>();

        // 이미 가족 → 라벨 순으로 정렬된 목록이다(네트워크가 늘 맨 끝). 여기서는 거르기만 한다.
        return snapshot.ComponentTypes
            .Where(t => includeDeprecated || !t.IsDeprecated)
            .Where(t => t.AppliesToCategory(category))
            .ToArray();
    }

    /// <inheritdoc/>
    public ComponentTypeInfo? Find(string? code)
    {
        var snapshot = _snapshot;
        var text = code?.Trim();
        if (snapshot == null || string.IsNullOrEmpty(text)) return null;
        return snapshot.ComponentIndex.TryGetValue(text, out var info) ? info : null;
    }
    #endregion

    #region - Processes -
    private Task<bool> LoadAsync(bool force, CancellationToken token)
    {
        // 6.3 에는 /api/devices/spec 이 없다 — 부르지 않는다(404 를 받아 "실패"로 기록하는 것조차 소음이다).
        if (!_policy.IsAxisContract) return Task.FromResult(false);

        TaskCompletionSource<bool> completion;
        lock (_gate)
        {
            // 진행 중 요청이 있으면 거기에 합류한다 — 7개 패널이 동시에 열려도 서버에는 한 번만 간다.
            if (_inFlight != null) return _inFlight;
            if (!force && IsLoaded) return Task.FromResult(true);

            // 합류 지점(_inFlight)을 요청 시작 "전에" 세운다. 요청 Task 자체를 넣으면, 요청이 동기로 끝나는 경우
            // finally 의 해제가 이 대입보다 먼저 일어나 완료된 Task 가 _inFlight 에 영구히 남는다(이후 Refresh 가 영영 안 나감).
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _inFlight = completion.Task;
        }

        _ = RunFetchAsync(completion, token);   // 락 밖에서 시작
        return completion.Task;
    }

    private async Task RunFetchAsync(TaskCompletionSource<bool> completion, CancellationToken token)
    {
        try
        {
            var loaded = await FetchAsync(token).ConfigureAwait(false);
            lock (_gate) { _inFlight = null; }
            if (loaded) CatalogChanged?.Invoke(this, EventArgs.Empty);   // 락 밖 — 구독자가 다시 이 서비스를 불러도 재진입 데드락이 없다
            completion.TrySetResult(loaded);
        }
        catch (OperationCanceledException)
        {
            lock (_gate) { _inFlight = null; }
            completion.TrySetCanceled(token);
        }
        catch (Exception ex)
        {
            // 구독자 핸들러의 예외 등 — 적재 자체는 끝났으므로 호출자를 던져 죽이지 않는다.
            lock (_gate) { _inFlight = null; }
            _log?.Error($"[Catalog] 적재 후처리 예외 — {ex.Message}");
            completion.TrySetResult(IsLoaded);
        }
    }

    private async Task<bool> FetchAsync(CancellationToken token)
    {
        var loaded = false;
        try
        {
            var response = await _apiService.GetDeviceSpecCatalogAsync(includeInactive: false, token).ConfigureAwait(false);
            if (response.Success && response.Data != null)
            {
                _snapshot = Snapshot.From(response.Data);   // 참조 하나 교체 — 읽는 쪽은 옛 판이든 새 판이든 일관된 판을 본다
                loaded = true;
                _log?.Info($"[Catalog] 적재 — 카테고리 {_snapshot.TypeAxes.Count} · 어휘 {_snapshot.Vocabularies.Count}");
            }
            else
            {
                // 옛 카탈로그는 버리지 않는다 — 어휘가 조금 낡은 편이 콤보가 비는 편보다 낫다.
                _log?.Warning($"[Catalog] 적재 실패 — {response.Error?.Code}: {response.Error?.Message ?? response.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error($"[Catalog] 적재 예외 — {ex.Message}");
        }

        return loaded;
    }

    private static CatalogOption? Find(IReadOnlyList<CatalogOption> options, string? code)
    {
        var text = code?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return options.FirstOrDefault(o => string.Equals(o.Code, text, StringComparison.OrdinalIgnoreCase));
    }
    #endregion

    #region - Snapshot -
    private sealed record VocabularyEntry(CatalogOption Option, IReadOnlySet<EnumDeviceCategory>? AppliesTo);

    /// <summary>적재된 카탈로그의 불변 사본 — DTO(가변)를 그대로 들고 있지 않는다.</summary>
    private sealed class Snapshot
    {
        public required IReadOnlyDictionary<EnumDeviceCategory, CatalogTypeAxis> TypeAxes { get; init; }
        public required IReadOnlyDictionary<EnumDeviceCategory, IReadOnlyList<CatalogExtraAxis>> ExtraAxes { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyList<VocabularyEntry>> Vocabularies { get; init; }

        /// <summary>
        /// 조립기 팔레트용 부품 유형 — <c>component_type</c> 어휘의 <c>definition</c> 까지 읽은 것.
        /// <b>가족 → 라벨</b> 순으로 미리 정렬해 둔다(네트워크가 늘 맨 끝). 폐기된 것도 담는다(거르기는 질의 때).
        /// </summary>
        public required IReadOnlyList<ComponentTypeInfo> ComponentTypes { get; init; }

        /// <summary>코드(대소문자 무시) → 부품 유형. 폐기된 것도 찾힌다.</summary>
        public required IReadOnlyDictionary<string, ComponentTypeInfo> ComponentIndex { get; init; }

        public static Snapshot From(DeviceSpecCatalogDto dto)
        {
            var typeAxes = new Dictionary<EnumDeviceCategory, CatalogTypeAxis>();
            var extraAxes = new Dictionary<EnumDeviceCategory, IReadOnlyList<CatalogExtraAxis>>();

            foreach (var spec in dto.Categories ?? Enumerable.Empty<CategorySpecDto>())
            {
                var category = DeviceTypeResolver.ParseCategory(spec?.CategoryDevice);
                if (spec == null || category == EnumDeviceCategory.None) continue;   // 모르는 카테고리는 건너뛴다(서버가 8번째를 늘려도 죽지 않는다)

                if (spec.TypeAxis != null)
                {
                    typeAxes[category] = new CatalogTypeAxis(
                        Field: spec.TypeAxis.Field ?? string.Empty,
                        RequiredOnCreate: spec.TypeAxis.RequiredOnCreate ?? false,
                        UnknownCode: NullIfEmpty(spec.TypeAxis.UnknownCode),
                        DefaultCode: NullIfEmpty(spec.TypeAxis.Default),
                        Values: ToOptions(spec.TypeAxis.Values));
                }

                extraAxes[category] = (spec.ExtraAxes ?? new List<ExtraAxisSpecDto>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Field))
                    .Select(a => new CatalogExtraAxis(a.Field!, NullIfEmpty(a.Label) ?? a.Field!, NullIfEmpty(a.Default), ToOptions(a.Values)))
                    .ToArray();
            }

            var vocabularies = new Dictionary<string, IReadOnlyList<VocabularyEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, entries) in dto.Vocabularies ?? new Dictionary<string, List<VocabularyEntryDto>>())
            {
                vocabularies[name] = (entries ?? new List<VocabularyEntryDto>())
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
                    .Select(e => new VocabularyEntry(
                        new CatalogOption(e.Code, NullIfEmpty(e.Label) ?? e.Code, IsDeprecated: !string.IsNullOrWhiteSpace(e.DeprecatedAt)),
                        ToCategories(e.AppliesTo)))
                    .ToArray();
            }

            // 엄격 어휘는 {code,label} 뿐이다(폐기 표지·적용 범위 없음). 같은 이름이 열린 어휘에 있으면 그쪽이 더 풍부하므로 덮지 않는다.
            foreach (var (name, entries) in dto.StrictVocabularies ?? new Dictionary<string, List<EnumEntryDto>>())
            {
                if (vocabularies.ContainsKey(name)) continue;
                vocabularies[name] = ToOptions(entries).Select(o => new VocabularyEntry(o, null)).ToArray();
            }

            var components = ReadComponentTypes(dto);
            var componentIndex = new Dictionary<string, ComponentTypeInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in components) componentIndex[info.Code] = info;

            return new Snapshot
            {
                TypeAxes = typeAxes,
                ExtraAxes = extraAxes,
                Vocabularies = vocabularies,
                ComponentTypes = components,
                ComponentIndex = componentIndex,
            };
        }

        /// <summary>
        /// <c>component_type</c> 어휘 → 조립기 팔레트 목록. <b>가족 → 라벨 → 코드</b> 순으로 굳힌다 —
        /// 정렬을 여기서 한 번만 하고, 질의(<c>ComponentTypes</c>)는 거르기만 한다.
        /// </summary>
        /// <remarks>
        /// 라벨 비교는 <see cref="StringComparer.Ordinal"/> 이다 — 한글 음절은 코드포인트 순이 곧 가나다 순이고,
        /// 문화권에 따라 팔레트 차례가 달라지면 화면 회귀 단언이 기계마다 갈린다.
        /// </remarks>
        private static IReadOnlyList<ComponentTypeInfo> ReadComponentTypes(DeviceSpecCatalogDto dto)
        {
            var entries = dto.FindVocabulary(DeviceSpecCatalogDto.VOCAB_COMPONENT_TYPE);
            if (entries == null || entries.Count == 0) return Array.Empty<ComponentTypeInfo>();

            return entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
                .Select(ComponentCatalogReader.Read)
                .GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())                                   // 같은 코드가 두 번 오면 앞의 것(서버 순서)을 쓴다
                .OrderBy(t => ComponentFamilyRules.PaletteOrder(t.Family))
                .ThenBy(t => t.Label, StringComparer.Ordinal)
                .ThenBy(t => t.Code, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<CatalogOption> ToOptions(IEnumerable<EnumEntryDto>? entries)
            => (entries ?? Enumerable.Empty<EnumEntryDto>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
                .Select(e => new CatalogOption(e.Code, NullIfEmpty(e.Label) ?? e.Code))
                .ToArray();

        // applies_to 가 없거나 비면 "제한 없음"(null). 모르는 카테고리 이름은 버린다.
        private static IReadOnlySet<EnumDeviceCategory>? ToCategories(IEnumerable<string>? names)
        {
            if (names == null) return null;
            var set = names.Select(DeviceTypeResolver.ParseCategory).Where(c => c != EnumDeviceCategory.None).ToHashSet();
            return set.Count == 0 ? null : set;
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    #endregion

    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly DeviceQueryPolicy _policy;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private Task<bool>? _inFlight;
    private volatile Snapshot? _snapshot;
    #endregion
}
