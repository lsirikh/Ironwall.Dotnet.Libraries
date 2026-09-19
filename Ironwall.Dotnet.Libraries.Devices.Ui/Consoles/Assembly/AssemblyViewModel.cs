using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>조립기가 사람에게 묻거나 다른 창을 여는 일 — 뷰모델이 창 관리자를 모르게 한다(헤드리스 테스트).</summary>
public interface IAssemblyDialogs
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> AskTextAsync(string title, string label, string initial);
    Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys);

    /// <summary>"프리셋으로 등록" 창을 연다. 등록된 장비의 Id(안 했으면 null).</summary>
    Task<int?> OpenRegisterAsync(DevicePreset preset);
}

/// <summary>
/// 부품 조립기(device-assembly-preset FR-01 ~ FR-08 · FR-15 · FR-16) — 팔레트 · 조립 보드 · 속성.
/// </summary>
/// <remarks>
/// <para><b>조립은 끝까지 Draft 다.</b> 끌어 놓기 · 펼치기 · 빼기 · 되돌리기는 서버를 부르지 않는다. 서버 호출은 마지막 한 번뿐이다 —
/// 새 장비면 POST 1건(등록 창), 기존 장비면 PATCH 1건. 그래서 끌어 놓기에 배치 엔드포인트가 필요 없다(AS L204-205).</para>
/// <para>싱글턴이 아니다 — 열 때마다 새로 만든다. 판정(끼울 자리 · key · 중복 · 미저장 계산)은 전부 <see cref="AssemblyBoard"/> 에 있다.</para>
/// </remarks>
public sealed class AssemblyViewModel : Screen, IDragDropHandler
{
    public const string BoardZoneKey = "assembly-board";
    public const string BinZoneKey = "assembly-bin";

    private readonly IComponentCatalog _catalog;
    private readonly DevicePresetStore _presets;
    private readonly ComponentApplyService? _applyService;
    private readonly IAssemblyDialogs _dialogs;

    private AssemblyBoard _board;
    private EnumDeviceCategory _category;
    private string _paletteSearch = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private BoardSlotViewModel? _inspected;
    private IReadOnlyList<ComponentDefinitionModel> _deviceBaseline = Array.Empty<ComponentDefinitionModel>();

    private AssemblyViewModel(AssemblyMode mode, EnumDeviceCategory category, IComponentCatalog catalog, DevicePresetStore presets, ComponentApplyService? applyService, IAssemblyDialogs dialogs)
    {
        Mode = mode;
        _category = category;
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));
        _applyService = applyService;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        Categories = DeviceRailCounter.RailOrder;
        Palette = new ObservableCollection<PaletteItemViewModel>();
        BoardItems = new ObservableCollection<BoardSlotViewModel>();
        OverrideRows = new ObservableCollection<OverrideRowViewModel>();
        CategoryPresets = new ObservableCollection<DevicePreset>();

        _board = NewBoard(category);
        AddFromPaletteCommand = new DelegateCommand<PaletteItemViewModel>(AddFromPalette);
        DisplayName = "부품 조립기";
    }

    #region - Factories -
    public static AssemblyViewModel Compose(EnumDeviceCategory category, IComponentCatalog catalog, DevicePresetStore presets, IAssemblyDialogs dialogs)
        => new(AssemblyMode.Compose, category, catalog, presets, null, dialogs);

    public static AssemblyViewModel ForPreset(DevicePreset preset, IComponentCatalog catalog, DevicePresetStore presets, IAssemblyDialogs dialogs)
    {
        var vm = new AssemblyViewModel(AssemblyMode.EditPreset, preset.Category, catalog, presets, null, dialogs) { EditingPreset = preset };
        vm._board.Load(preset.Components, preset.ComponentOverrides);
        return vm;
    }

    public static AssemblyViewModel ForDevice(IBaseDeviceModel device, EnumDeviceCategory category, IComponentCatalog catalog, DevicePresetStore presets, ComponentApplyService applyService, IAssemblyDialogs dialogs)
    {
        var vm = new AssemblyViewModel(AssemblyMode.EditDevice, category, catalog, presets, applyService ?? throw new ArgumentNullException(nameof(applyService)), dialogs) { EditingDevice = device };
        var components = device.Axes?.HardwareSpec?.Components?.ToList() ?? new List<ComponentDefinitionModel>();
        vm._deviceBaseline = components;
        vm._board.Load(components, device.Axes?.DeviceConfig?.ComponentOverrides);
        return vm;
    }
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _catalog.CatalogChanged += OnCatalogChanged;
        await _catalog.EnsureLoadedAsync(cancellationToken);
        RebuildPalette();
        RebuildBoardItems();
        RefreshPresets();
        RefreshAll();
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _catalog.CatalogChanged -= OnCatalogChanged;
        if (close)
        {
            foreach (var item in _itemsBySlot.Values) item.Detach();
            _itemsBySlot.Clear();
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    /// <summary>미저장 변경이 있으면 닫기 전에 묻는다.</summary>
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_board.IsDirty || _closeWithoutAsking) return true;
        return await _dialogs.ConfirmAsync("조립기 닫기", $"미저장 변경 {_board.UnsavedChangeCount}건이 있다. 버리고 닫을까?");
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => Execute.BeginOnUIThread(() =>
    {
        RebuildPalette();
        RebuildBoardItems();
        RefreshAll();
    });
    #endregion

    #region - Category · palette -
    public AssemblyMode Mode { get; }
    public DevicePreset? EditingPreset { get; private set; }
    public IBaseDeviceModel? EditingDevice { get; private set; }

    public IReadOnlyList<EnumDeviceCategory> Categories { get; }

    /// <summary>프리셋 · 기존 장비를 고칠 때는 카테고리가 고정이다(경로가 정본).</summary>
    public bool CanChangeCategory => Mode == AssemblyMode.Compose;

    public EnumDeviceCategory Category
    {
        get => _category;
        set
        {
            if (_category == value || !CanChangeCategory) return;
            _category = value;

            // 보드의 부품은 그대로 옮긴다 — 새 카테고리에 못 다는 유형은 보드가 문제로 짚는다(말없이 빼지 않는다).
            var definitions = _board.ToDefinitions();
            var overrides = _board.ToOverrides();
            _board.Changed -= OnBoardChanged;
            _board.Slots.CollectionChanged -= OnSlotsChanged;
            foreach (var item in _itemsBySlot.Values) item.Detach();
            _itemsBySlot.Clear();
            _board = NewBoard(value);
            if (definitions.Count > 0) _board.ReplaceAll(definitions, overrides);

            NotifyOfPropertyChange();
            RebuildPalette();
            RebuildBoardItems();
            RefreshPresets();
            RefreshAll();
        }
    }

    public ObservableCollection<PaletteItemViewModel> Palette { get; }

    public string PaletteSearch
    {
        get => _paletteSearch;
        set { if (_paletteSearch == (value ?? string.Empty)) return; _paletteSearch = value ?? string.Empty; NotifyOfPropertyChange(); RebuildPalette(); }
    }

    public bool IsPaletteEmpty => Palette.Count == 0;

    public string PaletteEmptyText => !_catalog.IsLoaded
        ? "부품 카탈로그를 읽지 못했다 — 서버 판본이 7.0 미만이거나 연결이 끊겼다"
        : "이 카테고리에 달 수 있는 부품이 없다";

    private void RebuildPalette()
    {
        Palette.Clear();
        foreach (var info in _catalog.ComponentTypes(_category).Where(i => AssemblyPalette.Matches(i, _paletteSearch)))
            Palette.Add(new PaletteItemViewModel(info));
        NotifyOfPropertyChange(nameof(IsPaletteEmpty));
        NotifyOfPropertyChange(nameof(PaletteEmptyText));
    }
    #endregion

    #region - Board -
    public ObservableCollection<BoardSlotViewModel> BoardItems { get; }

    public bool IsBoardEmpty => BoardItems.Count == 0;

    private AssemblyBoard NewBoard(EnumDeviceCategory category)
    {
        var board = new AssemblyBoard(category, code => _catalog.Find(code));
        board.Changed += OnBoardChanged;
        board.Slots.CollectionChanged += OnSlotsChanged;
        return board;
    }

    // 슬롯 목록의 낱낱 변경(CollectionChanged)을 따라가지 않는다 — 되돌리기는 목록을 비웠다가 다시 채우는데, 그 중간 상태를
    // 따라가면 화면 항목도 다 빠졌다 들어와 선택이 풀린다. 보드가 한 번의 조작을 끝내고 울리는 Changed 에서 한 번에 맞춘다.
    private void OnSlotsChanged(object? sender, NotifyCollectionChangedEventArgs e) { }

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        RebuildBoardItems();
        RefreshAll();
    }

    // 슬롯마다 화면 항목 하나 — 창이 살아 있는 동안 보관한다. 뺐다가 [되돌리기] 로 돌아온 슬롯이 같은 항목으로 돌아와야
    // 화면의 선택 · 포커스가 이어진다(항목을 새로 만들면 목록이 그것을 처음 보는 행으로 다룬다).
    private readonly Dictionary<AssemblySlot, BoardSlotViewModel> _itemsBySlot = new();

    /// <summary>보드의 슬롯 목록을 화면용 항목으로 다시 맞춘다.</summary>
    private void RebuildBoardItems()
    {
        var wanted = new List<BoardSlotViewModel>();
        foreach (var slot in _board.Slots)
        {
            var info = _catalog.Find(slot.TypeCode);
            if (!_itemsBySlot.TryGetValue(slot, out var item) || !ReferenceEquals(item.Info, info))
            {
                item?.Detach();      // 카탈로그가 다시 읽혀 유형 정보가 바뀌었다 — 항목을 새로 만든다
                item = new BoardSlotViewModel(slot, info);
                _itemsBySlot[slot] = item;
            }
            wanted.Add(item);
        }

        // 비우고 다시 채우면 목록의 선택이 풀리고 속성 칸이 닫힌다(순서를 바꿀 때마다 · 되돌릴 때마다).
        // 빠진 것만 빼고, 새 것만 끼우고, 자리가 바뀐 것은 옮긴다 — 목록은 Move 에서 선택을 지킨다.
        foreach (var gone in BoardItems.Where(i => !wanted.Contains(i)).ToList()) BoardItems.Remove(gone);
        for (var index = 0; index < wanted.Count; index++)
        {
            if (index < BoardItems.Count && ReferenceEquals(BoardItems[index], wanted[index])) continue;
            var from = BoardItems.IndexOf(wanted[index]);
            if (from >= 0) BoardItems.Move(from, index);
            else BoardItems.Insert(index, wanted[index]);
        }

        if (_inspected is not null && !BoardItems.Contains(_inspected)) Inspect(null);
        NotifyOfPropertyChange(nameof(IsBoardEmpty));
    }

    /// <summary>화면의 선택이 바뀌었다 — 하나면 속성 칸에 올린다.</summary>
    public void OnBoardSelectionChanged(IReadOnlyList<BoardSlotViewModel> selected)
    {
        foreach (var item in BoardItems) item.IsSelected = selected.Contains(item);
        Inspect(selected.Count == 1 ? selected[0] : null);
        NotifyOfPropertyChange(nameof(SelectedCount));
        NotifyOfPropertyChange(nameof(CanRemoveSelected));
    }

    public int SelectedCount => BoardItems.Count(i => i.IsSelected);

    /// <summary>팔레트의 키보드 비헤이비어가 부른다.</summary>
    public System.Windows.Input.ICommand AddFromPaletteCommand { get; }

    /// <summary>키보드 폴백 — 팔레트에서 Enter: 보드 끝에 단다(끌어 놓기와 같은 경로).</summary>
    public void AddFromPalette(PaletteItemViewModel? item)
    {
        if (item is null || IsBusy) return;
        var slot = _board.Add(item.Code);
        StatusText = slot is null ? $"'{item.Label}' 은(는) 이 카테고리에 달 수 없다" : $"'{item.Label}' 을(를) 달았다 — key {slot.Key}";
        if (slot is not null) SelectOnly(slot);
    }

    public bool CanRemoveSelected => SelectedCount > 0 && !IsBusy;

    public void RemoveSelected()
    {
        var slots = BoardItems.Where(i => i.IsSelected).Select(i => i.Slot).ToList();
        if (slots.Count == 0 || IsBusy) return;
        _board.Remove(slots);
        StatusText = $"{slots.Count}개를 뺐다 — [되돌리기] 로 도로 넣을 수 있다";
    }

    public bool CanUndo => _board.CanUndo && !IsBusy;

    public void Undo()
    {
        if (!CanUndo) return;
        _board.Undo();
        StatusText = "되돌렸다";
    }

    public bool CanRenumberChannels => BoardItems.Any(i => i.Slot.Channel.HasValue) && !IsBusy;

    public void RenumberChannels()
    {
        if (!CanRenumberChannels) return;
        _board.RenumberChannels(1);
        StatusText = "채널을 보드 순서대로 1번부터 다시 매겼다";
    }

    public async Task RepeatExpandAsync()
    {
        if (IsBusy) return;
        var all = _catalog.ComponentTypes(_category).Select(i => new PaletteItemViewModel(i)).ToList();
        var preselected = _inspected is null ? null : all.FirstOrDefault(p => p.Code == _inspected.Slot.TypeCode);
        var spec = await _dialogs.AskRepeatExpandAsync(all, preselected, _board.Slots.Select(s => s.Key).ToList());
        if (spec is null) return;

        var added = _board.Expand(spec);
        StatusText = added.Count == 0 ? "펼치지 못했다 — key 가 겹치거나 규칙이 틀렸다" : $"{added.Count}개를 펼쳤다";
    }

    private void SelectOnly(AssemblySlot slot)
    {
        var item = BoardItems.FirstOrDefault(i => ReferenceEquals(i.Slot, slot));
        if (item is null) return;
        OnBoardSelectionChanged(new[] { item });
        SelectionRequested?.Invoke(this, new[] { item });
    }

    /// <summary>화면의 목록 선택을 이 항목들로 맞춰 달라(추가 직후).</summary>
    public event EventHandler<IReadOnlyList<BoardSlotViewModel>>? SelectionRequested;
    #endregion

    #region - Drag (IDragDropHandler) -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy) return false;

        return target.ZoneKey switch
        {
            // 보드: 팔레트 블록(추가) 또는 보드 슬롯(이동)
            BoardZoneKey => payload.Items.All(i => i is PaletteItemViewModel) || payload.Items.All(i => i is BoardSlotViewModel),
            // 빼는 곳: 보드 슬롯만
            BinZoneKey => payload.Items.Count > 0 && payload.Items.All(i => i is BoardSlotViewModel),
            _ => false,
        };
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (!CanDrop(payload, target)) return;

        if (target.ZoneKey == BinZoneKey)
        {
            var removed = payload.Items.OfType<BoardSlotViewModel>().Select(i => i.Slot).ToList();
            _board.Remove(removed);
            StatusText = $"{removed.Count}개를 뺐다 — [되돌리기] 로 도로 넣을 수 있다";
            return;
        }

        var index = target.IsReorder ? target.InsertionIndex : -1;

        if (payload.Items[0] is PaletteItemViewModel)
        {
            AssemblySlot? last = null;
            foreach (var item in payload.Items.OfType<PaletteItemViewModel>())
            {
                var slot = _board.Add(item.Code, index);
                if (slot is null) continue;
                last = slot;
                if (index >= 0) index++;     // 여러 개를 끌면 놓은 자리부터 차례로
            }
            if (last is not null) SelectOnly(last);
            return;
        }

        var slots = payload.Items.OfType<BoardSlotViewModel>().Select(i => i.Slot).ToList();
        if (index < 0) index = _board.Slots.Count;
        _board.Move(slots, index);     // 순서만 바뀐 것은 미저장 변경으로 세지 않는다(서버에 순서 계약이 없다)
    }
    #endregion

    #region - Inspector -
    public BoardSlotViewModel? Inspected => _inspected;
    public bool HasInspected => _inspected is not null;
    public ObservableCollection<OverrideRowViewModel> OverrideRows { get; }

    /// <summary>카탈로그 사실 — 회색 글자 상자. 입력 칸으로 만들면 보내게 되고 그건 422 다(AS L293 · L369).</summary>
    public string CatalogStates => AssemblyPalette.Join(_inspected?.Info?.States ?? Array.Empty<string>());
    public string CatalogCommands => AssemblyPalette.Join(_inspected?.Info?.Commands ?? Array.Empty<string>());
    public string CatalogProduces => AssemblyPalette.Join(_inspected?.Info?.Produces ?? Array.Empty<string>());
    public string CatalogNote => _inspected?.Info is null
        ? "카탈로그에 없는 유형이다 — 등록 · 적용할 수 없다"
        : _inspected.Info.ReportsState ? "이 유형은 동작 상태를 보고한다" : "이 유형은 상태를 보고하지 않는다 — 건강(health)만 온다";

    private void Inspect(BoardSlotViewModel? item)
    {
        _inspected = item;
        OverrideRows.Clear();
        if (item?.Info is { } info)
            foreach (var name in info.OverrideParams) OverrideRows.Add(new OverrideRowViewModel(item.Slot, name));

        NotifyOfPropertyChange(nameof(Inspected));
        NotifyOfPropertyChange(nameof(HasInspected));
        NotifyOfPropertyChange(nameof(CatalogStates));
        NotifyOfPropertyChange(nameof(CatalogCommands));
        NotifyOfPropertyChange(nameof(CatalogProduces));
        NotifyOfPropertyChange(nameof(CatalogNote));
        NotifyOfPropertyChange(nameof(HasOverrideRows));
    }

    public bool HasOverrideRows => OverrideRows.Count > 0;
    #endregion

    #region - Presets -
    public ObservableCollection<DevicePreset> CategoryPresets { get; }
    public bool HasCategoryPresets => CategoryPresets.Count > 0;
    public bool CanWritePresets => !_presets.IsReadOnly;
    public string? PresetStoreMessage => _presets.StateMessage;

    private void RefreshPresets()
    {
        CategoryPresets.Clear();
        foreach (var preset in _presets.ForCategory(_category)) CategoryPresets.Add(preset);
        NotifyOfPropertyChange(nameof(HasCategoryPresets));
    }

    /// <summary>프리셋을 보드에 올린다(교체). 보드에 뭔가 있으면 먼저 묻는다 — 되돌리기 한 번으로 돌아올 수 있다.</summary>
    public async Task LoadPresetAsync(DevicePreset? preset)
    {
        if (preset is null || IsBusy || preset.Category != _category) return;
        if (_board.Slots.Count > 0 && !await _dialogs.ConfirmAsync("프리셋 올리기", $"보드의 부품 {_board.Slots.Count}개를 '{preset.Name}' 의 부품으로 바꾼다. [되돌리기] 로 돌아올 수 있다."))
            return;

        _board.ReplaceAll(preset.Components, preset.ComponentOverrides);
        StatusText = $"'{preset.Name}' 을(를) 보드에 올렸다";
    }

    public bool CanSaveAsPreset => !_board.HasErrors && _board.Slots.Count > 0 && CanWritePresets && !IsBusy;

    public async Task SaveAsPresetAsync()
    {
        if (!CanSaveAsPreset) return;

        var editing = Mode == AssemblyMode.EditPreset ? EditingPreset : null;
        var name = editing?.Name ?? await _dialogs.AskTextAsync("프리셋으로 저장", "프리셋 이름", $"{DeviceCategoryText.Of(_category)} 프리셋");
        if (string.IsNullOrWhiteSpace(name)) return;

        var preset = BuildPreset(editing?.Id ?? DevicePresetStore.NewId(), name!.Trim(), editing);
        var result = _presets.Save(preset);
        StatusText = result.IsSuccess ? $"프리셋 '{preset.Name}' 을(를) 저장했다" : result.Message ?? "프리셋을 저장하지 못했다";
        if (!result.IsSuccess) return;

        if (Mode == AssemblyMode.EditPreset) EditingPreset = _presets.Find(preset.Id) ?? preset;
        _board.MarkBaseline();     // 저장한 것이 새 기준이다 — 슬롯을 새로 만들지 않으니 선택과 속성 칸이 그대로 남는다
        RefreshPresets();
        RefreshAll();
    }

    private DevicePreset BuildPreset(string id, string name, DevicePreset? basis) => (basis ?? BasisFromDevice(id, name)) with
    {
        Id = id,
        Name = name,
        Category = _category,
        Components = _board.ToDefinitions(),
        ComponentOverrides = StripNulls(_board.ToOverrides()),
        IsSeed = false,
    };

    /// <summary>
    /// 기존 장비에서 프리셋을 굳힐 때는 그 장비의 구조 값(종류 · 제원 기본값 · 임계치 · 동작 모드)도 같이 가져간다 —
    /// 안 가져가면 그 프리셋으로 만든 카메라에 동작 모드가 없다. 일련번호 · MAC 같은 그 장비만의 것은 가져가지 않는다.
    /// </summary>
    private DevicePreset BasisFromDevice(string id, string name)
    {
        var device = EditingDevice;
        var spec = device?.Axes?.HardwareSpec;
        var config = device?.Axes?.DeviceConfig;
        return new DevicePreset
        {
            Id = id,
            Name = name,
            Category = _category,
            TypeAxisCode = device?.TypeAxisCode,
            Manufacturer = spec?.Manufacturer,
            Model = spec?.Model,
            Firmware = spec?.Firmware,
            HardwareRev = spec?.HardwareRev,
            MaxDetectionRange = spec?.MaxDetectionRange,
            OnvifVersion = spec?.OnvifVersion,
            Thresholds = (JObject?)config?.Thresholds?.DeepClone(),
            Modes = (JObject?)config?.Modes?.DeepClone(),
        };
    }

    /// <summary>프리셋에는 "뺀 key 의 null" 을 담지 않는다 — 그것은 기존 장비에 보낼 때만 뜻이 있다.</summary>
    private static JObject? StripNulls(JObject? overrides)
    {
        if (overrides is null) return null;
        var kept = new JObject(overrides.Properties().Where(p => p.Value.Type != JTokenType.Null).Select(p => new JProperty(p.Name, p.Value.DeepClone())));
        return kept.HasValues ? kept : null;
    }
    #endregion

    #region - Commit -
    public string PrimaryText => Mode switch
    {
        AssemblyMode.EditDevice => "적용",
        AssemblyMode.EditPreset => "프리셋 저장",
        _ => "등록…",
    };

    public string PrimaryHint => Mode switch
    {
        AssemblyMode.EditDevice => "다시 받아 비교한 뒤 PATCH 1건",
        AssemblyMode.EditPreset => "이 컴퓨터의 프리셋 파일에 저장",
        _ => "개체 정보만 넣으면 POST 1건",
    };

    public bool CanCommit => !IsBusy && !_board.HasErrors && Mode switch
    {
        AssemblyMode.EditDevice => _board.IsDirty,
        AssemblyMode.EditPreset => _board.IsDirty && CanWritePresets,
        _ => _board.Slots.Count > 0,
    };

    public string? CommitBlockedReason => IsBusy ? "처리 중입니다."
        : _board.HasErrors ? _board.Problems.FirstOrDefault()
        : Mode == AssemblyMode.Compose ? (_board.Slots.Count == 0 ? "보드에 부품을 먼저 단다" : null)
        : !_board.IsDirty ? "바뀐 것이 없다"
        : Mode == AssemblyMode.EditPreset && !CanWritePresets ? _presets.StateMessage
        : null;

    public async Task CommitAsync()
    {
        if (!CanCommit) return;

        switch (Mode)
        {
            case AssemblyMode.EditPreset:
                await SaveAsPresetAsync();
                return;

            case AssemblyMode.Compose:
                // 보드를 이름 없는 프리셋으로 굳혀 등록 창에 넘긴다 — 등록 경로는 하나다(프리셋 → POST 1건).
                var transient = BuildPreset(DevicePresetStore.NewId(), $"{DeviceCategoryText.Of(_category)} (조립기)", null);
                var newId = await _dialogs.OpenRegisterAsync(transient);
                if (newId is null) return;
                RegisteredDeviceId = newId;
                _closeWithoutAsking = true;
                await TryCloseAsync(true);
                return;

            case AssemblyMode.EditDevice:
                await ApplyToDeviceAsync();
                return;
        }
    }

    /// <summary>등록이 끝났으면 그 장비의 Id — 콘솔이 새 장비를 고른다.</summary>
    public int? RegisteredDeviceId { get; private set; }

    /// <summary>적용 전 미리보기 — 더한 것 · 뺀 것 · 고친 것(FR-16).</summary>
    public string DiffSummary
    {
        get
        {
            var diff = _board.Diff();
            if (diff.IsEmpty) return "바뀐 것이 없다";
            var lines = new List<string>();
            if (diff.Added.Count > 0) lines.Add($"더함 {diff.Added.Count}: {string.Join(", ", diff.Added.Select(e => e.Key))}");
            if (diff.Removed.Count > 0) lines.Add($"뺌 {diff.Removed.Count}: {string.Join(", ", diff.Removed.Select(e => e.Key))} — 이 부품의 관측 기록이 함께 삭제된다");
            if (diff.Changed.Count > 0) lines.Add($"고침 {diff.Changed.Count}: {string.Join(", ", diff.Changed.Select(e => $"{e.Key}({string.Join("/", e.ChangedFields)})"))}");
            return string.Join(Environment.NewLine, lines);
        }
    }

    private async Task ApplyToDeviceAsync()
    {
        if (_applyService is null || EditingDevice is null) return;
        if (!await _dialogs.ConfirmAsync("부품 구성 적용", DiffSummary + Environment.NewLine + Environment.NewLine + "서버는 부품 배열을 통째로 바꾼다. 보내기 직전에 장비를 다시 받아 그 사이 바뀌지 않았는지 확인한다."))
            return;

        IsBusy = true;
        try
        {
            var result = await _applyService.ApplyAsync(EditingDevice, _deviceBaseline, _board.ToDefinitions(), _board.ToOverrides());
            StatusText = result.Message;

            if (result.IsSuccess)
            {
                _deviceBaseline = _board.ToDefinitions();
                _board.MarkBaseline();
                _closeWithoutAsking = true;
                await TryCloseAsync(true);
            }
            else if (result.IsConflict)
            {
                StatusText = result.Message + " — 조립기를 닫고 다시 연다(그 사이의 변경을 덮어쓰지 않는다)";
            }
        }
        finally { IsBusy = false; }
    }
    #endregion

    #region - Status -
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; RefreshAll(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>머리글 — 무엇을 조립하고 있나.</summary>
    public string Subject => Mode switch
    {
        AssemblyMode.EditDevice => $"{DeviceCategoryText.Of(_category)} · #{EditingDevice?.DeviceNumber} {EditingDevice?.DeviceName}",
        AssemblyMode.EditPreset => $"프리셋 · {EditingPreset?.Name}",
        _ => $"{DeviceCategoryText.Of(_category)} · 새로 조립",
    };

    public string FooterText => $"부품 {_board.Slots.Count} · 미저장 변경 {_board.UnsavedChangeCount}";
    public string FooterNote => "저장 전에는 서버에 아무것도 보내지 않습니다";
    public IReadOnlyList<string> Problems => _board.Problems;
    public bool HasProblems => _board.Problems.Count > 0;

    private bool _closeWithoutAsking;

    private void RefreshAll()
    {
        NotifyOfPropertyChange(nameof(FooterText));
        NotifyOfPropertyChange(nameof(Problems));
        NotifyOfPropertyChange(nameof(HasProblems));
        NotifyOfPropertyChange(nameof(Subject));
        NotifyOfPropertyChange(nameof(CanCommit));
        NotifyOfPropertyChange(nameof(CommitBlockedReason));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(CanRemoveSelected));
        NotifyOfPropertyChange(nameof(CanRenumberChannels));
        NotifyOfPropertyChange(nameof(CanSaveAsPreset));
        NotifyOfPropertyChange(nameof(IsBusy));
        NotifyOfPropertyChange(nameof(DiffSummary));
    }
    #endregion
}

/// <summary>카테고리의 우리말 이름 — 콘솔 레일과 같은 말을 쓴다.</summary>
public static class DeviceCategoryText
{
    public static string Of(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "제어기",
        EnumDeviceCategory.Sensor => "센서",
        EnumDeviceCategory.Camera => "카메라",
        EnumDeviceCategory.Speaker => "스피커",
        EnumDeviceCategory.Enclosure => "함체",
        EnumDeviceCategory.Lamp => "경광등",
        EnumDeviceCategory.Gate => "통문",
        _ => category.ToString(),
    };
}
