using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>
/// 반복 펼치기(device-assembly-preset FR-08) — 16채널을 16번 끌게 하지 않는다. 드래그의 폴백이 아니라 보완이다:
/// 위치가 뜻을 갖는 조작은 드래그가, 규칙적인 대량 생성은 이 창이 맡고, 결과는 같은 슬롯이라 이후는 다시 드래그다(AS L300-308).
/// </summary>
public sealed class RepeatExpandViewModel : Screen
{
    private readonly IReadOnlyCollection<string> _existingKeys;
    private PaletteItemViewModel? _selectedType;
    private string _count = "16";
    private string _startChannel = "1";
    private string _keyFormat = string.Empty;
    private bool _isKeyFormatEdited;

    public RepeatExpandViewModel(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys)
    {
        Types = palette ?? Array.Empty<PaletteItemViewModel>();
        _existingKeys = existingKeys ?? Array.Empty<string>();
        Preview = new ObservableCollection<RepeatPreviewRow>();
        DisplayName = "반복 펼치기";
        SelectedType = preselected ?? Types.FirstOrDefault();
    }

    public IReadOnlyList<PaletteItemViewModel> Types { get; }
    public ObservableCollection<RepeatPreviewRow> Preview { get; }

    public PaletteItemViewModel? SelectedType
    {
        get => _selectedType;
        set
        {
            if (ReferenceEquals(_selectedType, value)) return;
            _selectedType = value;
            // 손으로 고치기 전까지는 유형에 맞춰 key 규칙을 제안한다: CONTACT_INPUT → contact_{02d}
            if (!_isKeyFormatEdited && value is not null) _keyFormat = AssemblyKeyRules.BaseKeyFor(value.Code) + "_{02d}";
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(KeyFormat));
            Rebuild();
        }
    }

    public string Count { get => _count; set { _count = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string StartChannel { get => _startChannel; set { _startChannel = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }

    public string KeyFormat
    {
        get => _keyFormat;
        set { _keyFormat = value ?? string.Empty; _isKeyFormatEdited = true; NotifyOfPropertyChange(); Rebuild(); }
    }

    public string? Error { get; private set; }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public int ConflictCount => Preview.Count(r => r.IsConflict);
    public bool CanExpand => Spec is not null && !HasError && Preview.Count > 0 && ConflictCount == 0;

    /// <summary>확정된 규칙. 취소했으면 null.</summary>
    public RepeatExpandSpec? Result { get; private set; }

    private RepeatExpandSpec? Spec { get; set; }

    private void Rebuild()
    {
        Preview.Clear();
        Spec = null;
        Error = null;

        if (_selectedType is null) Error = "유형을 고른다";
        else if (!int.TryParse(_count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) Error = "개수는 숫자여야 한다";
        else if (!int.TryParse(_startChannel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)) Error = "시작 채널은 숫자여야 한다";
        else
        {
            var spec = new RepeatExpandSpec(_selectedType.Code, count, start, _keyFormat);
            Error = RepeatExpand.Validate(spec);
            if (Error is null)
            {
                Spec = spec;
                foreach (var row in RepeatExpand.Preview(spec, _existingKeys)) Preview.Add(row);
            }
        }

        NotifyOfPropertyChange(nameof(Error));
        NotifyOfPropertyChange(nameof(HasError));
        NotifyOfPropertyChange(nameof(ConflictCount));
        NotifyOfPropertyChange(nameof(CanExpand));
    }

    public Task ExpandAsync()
    {
        if (!CanExpand) return Task.CompletedTask;
        Result = Spec;
        return TryCloseAsync(true);
    }

    public Task CancelAsync() => TryCloseAsync(false);
}

/// <summary>글 한 줄을 묻는 작은 창(프리셋 이름).</summary>
public sealed class TextPromptViewModel : Screen
{
    private string _text;

    public TextPromptViewModel(string title, string label, string initial)
    {
        DisplayName = title;
        Label = label;
        _text = initial ?? string.Empty;
    }

    public string Label { get; }

    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanAccept)); }
    }

    public bool CanAccept => !string.IsNullOrWhiteSpace(_text);
    public string? Result { get; private set; }

    public Task AcceptAsync()
    {
        if (!CanAccept) return Task.CompletedTask;
        Result = _text.Trim();
        return TryCloseAsync(true);
    }

    public Task CancelAsync() => TryCloseAsync(false);
}

/// <summary>예 · 아니오를 묻는 작은 창. 호스트의 확인 팝업은 메시지로만 답을 돌려줘 조립기처럼 기다려야 하는 흐름에 맞지 않는다.</summary>
public sealed class ConfirmPromptViewModel : Screen
{
    public ConfirmPromptViewModel(string title, string message)
    {
        DisplayName = title;
        Message = message;
    }

    public string Message { get; }
    public bool Result { get; private set; }

    public Task AcceptAsync() { Result = true; return TryCloseAsync(true); }
    public Task CancelAsync() => TryCloseAsync(false);
}

/// <summary>
/// 프리셋 관리(device-assembly-preset FR-11) — 카테고리별 목록 · 이름 바꾸기 · 복제 · 삭제 · 내보내기/가져오기.
/// 프리셋은 이 컴퓨터의 파일에 산다(서버에 프리셋 리소스가 없다 — AS L208-209).
/// </summary>
public sealed class PresetManagerViewModel : Screen
{
    private readonly DevicePresetStore _store;
    private readonly IComponentCatalog _catalog;
    private readonly IAssemblyDialogs _dialogs;
    private EnumDeviceCategory _category;
    private DevicePreset? _selected;
    private string _message = string.Empty;

    public PresetManagerViewModel(DevicePresetStore store, IComponentCatalog catalog, IAssemblyDialogs dialogs, EnumDeviceCategory category)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _category = category;
        Categories = Lists.DeviceRailCounter.RailOrder;
        Presets = new ObservableCollection<DevicePreset>();
        DisplayName = "프리셋 관리";
        Refresh();
    }

    public IReadOnlyList<EnumDeviceCategory> Categories { get; }
    public ObservableCollection<DevicePreset> Presets { get; }
    public bool IsReadOnly => _store.IsReadOnly;
    public string? StoreMessage => _store.StateMessage;
    public bool HasStoreMessage => !string.IsNullOrEmpty(_store.StateMessage);

    public EnumDeviceCategory Category
    {
        get => _category;
        set { if (_category == value) return; _category = value; NotifyOfPropertyChange(); Refresh(); }
    }

    public DevicePreset? Selected
    {
        get => _selected;
        set { _selected = value; NotifyOfPropertyChange(); NotifySelection(); }
    }

    public string Message
    {
        get => _message;
        private set { _message = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public bool CanEditSelected => _selected is not null && !IsReadOnly;
    public bool HasSelected => _selected is not null;

    /// <summary>고른 프리셋의 부품 가운데 카탈로그에서 사라진 유형 — 말없이 빼지 않고 짚는다. 이 프리셋으로는 등록할 수 없다.</summary>
    public string? SelectedProblem
    {
        get
        {
            if (_selected is null || !_catalog.IsLoaded) return null;
            var missing = _selected.Components.Select(c => c.Type).Where(t => _catalog.Find(t) is null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return missing.Count == 0 ? null : $"카탈로그에 없는 유형이 들어 있다: {string.Join(", ", missing)} — 조립기에서 빼야 등록할 수 있다";
        }
    }

    public string SelectedSummary => _selected is null
        ? "프리셋을 고르세요"
        : $"부품 {_selected.Components.Count} · 재정의 {_selected.ComponentOverrides?.Count ?? 0} · 종류 {_selected.TypeAxisCode ?? "미지정"}" + (_selected.IsSeed ? " · 본보기" : string.Empty);

    /// <summary>조립기로 열어 달라 — 창을 여는 것은 부른 쪽(콘솔)의 몫이다.</summary>
    public event EventHandler<DevicePreset>? OpenInAssemblyRequested;

    public void OpenInAssembly()
    {
        if (_selected is not null) OpenInAssemblyRequested?.Invoke(this, _selected);
    }

    public async Task RenameAsync()
    {
        if (!CanEditSelected) return;
        var name = await _dialogs.AskTextAsync("이름 바꾸기", "프리셋 이름", _selected!.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        Report(_store.Rename(_selected.Id, name!), $"이름을 '{name}' (으)로 바꿨다", _selected.Id);
    }

    public void Duplicate()
    {
        if (!CanEditSelected) return;
        var result = _store.Duplicate(_selected!.Id, out var copy);
        Report(result, copy is null ? string.Empty : $"'{copy.Name}' 을(를) 만들었다", copy?.Id);
    }

    public async Task DeleteAsync()
    {
        if (!CanEditSelected) return;
        if (!await _dialogs.ConfirmAsync("프리셋 삭제", $"'{_selected!.Name}' 을(를) 지운다. 이 프리셋으로 이미 만든 장비에는 영향이 없다.")) return;
        Report(_store.Delete(_selected.Id), "지웠다", null);
    }

    public void Export(string filePath) => Report(_store.Export(filePath), $"내보냈다 — {filePath}", _selected?.Id);

    public void Import(string filePath)
    {
        var result = _store.Import(filePath);
        Report(result, result.Message ?? "가져왔다", _selected?.Id);
    }

    public Task CloseAsync() => TryCloseAsync(true);

    /// <summary>조립기에서 프리셋을 고치고 돌아왔다 — 목록을 다시 읽고 그것을 고른다.</summary>
    public void ReloadAndSelect(string presetId)
    {
        Refresh();
        Selected = Presets.FirstOrDefault(p => p.Id == presetId) ?? Presets.FirstOrDefault();
    }

    private void Report(PresetStoreResult result, string success, string? selectId)
    {
        Message = result.IsSuccess ? (string.IsNullOrEmpty(result.Message) ? success : result.Message!) : result.Message ?? "하지 못했다";
        if (!result.IsSuccess) return;
        Refresh();
        Selected = selectId is null ? Presets.FirstOrDefault() : Presets.FirstOrDefault(p => p.Id == selectId) ?? Presets.FirstOrDefault();
    }

    private void Refresh()
    {
        Presets.Clear();
        foreach (var preset in _store.ForCategory(_category)) Presets.Add(preset);
        if (_selected is null || Presets.All(p => p.Id != _selected.Id)) _selected = Presets.FirstOrDefault();
        NotifyOfPropertyChange(nameof(Selected));
        NotifySelection();
    }

    private void NotifySelection()
    {
        NotifyOfPropertyChange(nameof(CanEditSelected));
        NotifyOfPropertyChange(nameof(HasSelected));
        NotifyOfPropertyChange(nameof(SelectedProblem));
        NotifyOfPropertyChange(nameof(SelectedSummary));
    }
}
