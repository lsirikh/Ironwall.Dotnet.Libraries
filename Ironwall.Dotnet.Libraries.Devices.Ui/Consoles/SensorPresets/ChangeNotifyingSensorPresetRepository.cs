using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>
/// 저장소를 감싸 쓰기(저장 · 지우기)가 끝날 때마다 <c>changed</c> 를 부른다 — 결선 창이 연 센서 모델 창(모달)에서 프리셋을 저장하면
/// 창을 닫기 전에도 결선 펜스 보기가 곧바로 따라 옮겨지게(실창 4회차 MNT-R28-455 · 440 — 창이 닫힌 뒤에야 다시 읽었다).
/// 쓰기가 실패하면 부르지 않는다(예외는 그대로 · 화면 값 유지 NFR-05).
/// </summary>
internal sealed class ChangeNotifyingSensorPresetRepository : ISensorPresetRepository
{
    private readonly ISensorPresetRepository _inner;
    private readonly Func<Task> _changed;

    public ChangeNotifyingSensorPresetRepository(ISensorPresetRepository inner, Func<Task> changed)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public SensorPresetSource Source => _inner.Source;
    public bool CanWrite => _inner.CanWrite;

    public Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default) => _inner.GetModelsAsync(ct);
    public Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default) => _inner.GetMountPresetsAsync(modelId, ct);
    public Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default) => _inner.GetAttributePresetsAsync(modelId, ct);

    public async Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default)
        => await Changed(await _inner.SaveModelAsync(model, ct).ConfigureAwait(true)).ConfigureAwait(true);

    public async Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default)
        => await Changed(await _inner.SaveMountPresetAsync(preset, ct).ConfigureAwait(true)).ConfigureAwait(true);

    public async Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default)
        => await Changed(await _inner.SaveAttributePresetAsync(preset, ct).ConfigureAwait(true)).ConfigureAwait(true);

    public async Task DeletePresetAsync(PresetKey key, CancellationToken ct = default)
    {
        await _inner.DeletePresetAsync(key, ct).ConfigureAwait(true);
        await _changed().ConfigureAwait(true);
    }

    private async Task<T> Changed<T>(T saved)
    {
        await _changed().ConfigureAwait(true);
        return saved;
    }
}
