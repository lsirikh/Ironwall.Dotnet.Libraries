using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>저장 확인(W5)의 고름.</summary>
public enum PresetSaveChoice
{
    /// <summary>[취소] — 기본 포커스(실수로 Enter 해도 아무 일 없음).</summary>
    Cancel = 0,
    SaveAsNew = 1,
    Save = 2,
}

/// <summary>
/// 프리셋 저장 확인(W5 · FR-12) — 쓰는 센서가 있을 때만 뜬다. "'이름' 을 쓰는 센서 N대가 새 자리로 옮겨집니다." + 센서 칩 + 따로 고친 값은 남는다는 말.
/// </summary>
public sealed class PresetSaveConfirmViewModel : Screen
{
    public PresetSaveConfirmViewModel(string presetName, IReadOnlyList<SensorPresetUsage> usages)
    {
        DisplayName = "프리셋 저장";
        Usages = usages ?? Array.Empty<SensorPresetUsage>();
        Message = SensorPresetUsageMath.MoveMessage(presetName ?? string.Empty, Usages.Count);
    }

    public IReadOnlyList<SensorPresetUsage> Usages { get; }
    public string Message { get; }
    public IReadOnlyList<string> Chips => Usages.Select(u => u.ChipText).ToList();
    public string Note => "따로 고친 값은 그대로 남습니다.";

    /// <summary>틀의 안내 줄(버튼 줄 왼쪽 · 저장의 결과) — W5 목업 문구.</summary>
    public string KeepText => Note;

    /// <summary>기본 단추는 [취소] — 포커스가 처음 가는 곳.</summary>
    public PresetSaveChoice DefaultChoice => PresetSaveChoice.Cancel;

    public PresetSaveChoice Choice { get; private set; } = PresetSaveChoice.Cancel;

    public Task CancelAsync() => Close(PresetSaveChoice.Cancel);
    public Task SaveAsNewAsync() => Close(PresetSaveChoice.SaveAsNew);
    public Task SaveAsync() => Close(PresetSaveChoice.Save);

    private async Task Close(PresetSaveChoice choice)
    {
        Choice = choice;
        await TryCloseAsync(choice != PresetSaveChoice.Cancel);
    }
}

/// <summary>모델 한 줄 — 범위 안 그 모델 센서에 줄 프리셋.</summary>
public sealed class MountPresetApplyRow : PropertyChangedBase
{
    private SensorMountPreset? _selected;

    public MountPresetApplyRow(SensorModel model, IReadOnlyList<SensorMountPreset> presets)
    {
        Model = model;
        Presets = presets.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id).ToList();
        _selected = Presets.FirstOrDefault();
    }

    public SensorModel Model { get; }
    public IReadOnlyList<SensorMountPreset> Presets { get; }
    public SensorMountPreset? Selected { get => _selected; set => Set(ref _selected, value); }
}

/// <summary>
/// [위치 프리셋 적용…](W4 주석 1 · FR-16) — 범위(고른 센서 / 같은 모델 전체 / 이 제어기 전체) + 모델별 프리셋.
/// </summary>
public sealed class MountPresetApplyViewModel : Screen
{
    private readonly WiringViewModel _wiring;
    private MountPresetApplyScope _scope;

    public MountPresetApplyViewModel(WiringViewModel wiring, MountPresetApplyScope scope = MountPresetApplyScope.Selected)
    {
        _wiring = wiring ?? throw new ArgumentNullException(nameof(wiring));
        DisplayName = "위치 프리셋 적용";
        _scope = scope;
        Rebuild();
    }

    public ObservableCollection<MountPresetApplyRow> Rows { get; } = new();

    public MountPresetApplyScope Scope
    {
        get => _scope;
        set { if (Set(ref _scope, value)) { Rebuild(); NotifyOfPropertyChange(nameof(IsSelectedScope)); NotifyOfPropertyChange(nameof(IsSameModelScope)); NotifyOfPropertyChange(nameof(IsControllerScope)); } }
    }

    public bool IsSelectedScope { get => _scope == MountPresetApplyScope.Selected; set { if (value) Scope = MountPresetApplyScope.Selected; } }
    public bool IsSameModelScope { get => _scope == MountPresetApplyScope.SameModel; set { if (value) Scope = MountPresetApplyScope.SameModel; } }
    public bool IsControllerScope { get => _scope == MountPresetApplyScope.Controller; set { if (value) Scope = MountPresetApplyScope.Controller; } }

    /// <summary>적용한 센서 수(닫힌 뒤 읽는다).</summary>
    public int Applied { get; private set; }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var m in _wiring.ApplyScopeModels(_scope)) Rows.Add(new MountPresetApplyRow(m, _wiring.MountPresetsOf(m.Id)));
    }

    /// <summary>[적용] — 되돌리기 한 걸음(결선맵 Undo).</summary>
    public async Task ApplyAsync()
    {
        var map = Rows.Where(r => r.Selected is not null).ToDictionary(r => r.Model.Id, r => r.Selected!);
        Applied = _wiring.ApplyMountPreset(_scope, map);
        await TryCloseAsync(Applied > 0);
    }

    public Task CancelAsync() => TryCloseAsync(false);
}

/// <summary>속성 한 줄(W3 표) — 항목 · 값 · 단위 · 쓰임.</summary>
public sealed class AttributeRowViewModel : PropertyChangedBase
{
    private string _key, _label, _value, _unit;
    private SensorAttributeUse _use;

    public AttributeRowViewModel(SensorAttributeValue value)
    {
        _key = value.Key; _label = value.Label; _value = value.Value; _unit = value.Unit; _use = value.Use;
    }

    public string Key { get => _key; set => Set(ref _key, value ?? string.Empty); }
    public string Label { get => _label; set => Set(ref _label, value ?? string.Empty); }
    public string Value { get => _value; set => Set(ref _value, value ?? string.Empty); }
    public string Unit { get => _unit; set => Set(ref _unit, value ?? string.Empty); }
    public SensorAttributeUse Use { get => _use; set { if (Set(ref _use, value)) NotifyOfPropertyChange(nameof(UseText)); } }

    /// <summary>"그림" · "알림" · "참고".</summary>
    public string UseText => UseName(_use);

    public static string UseName(SensorAttributeUse use) => use switch { SensorAttributeUse.Render => "그림", SensorAttributeUse.Alert => "알림", _ => "참고" };

    public static IReadOnlyList<SensorAttributeUse> Uses { get; } = new[] { SensorAttributeUse.Render, SensorAttributeUse.Alert, SensorAttributeUse.Reference };

    public SensorAttributeValue ToValue() => new(Key.Trim(), Label.Trim(), Value.Trim(), Unit.Trim(), _use);
}
