using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.IO;
using System.Windows;

namespace DeviceConsolePreview;

/// <summary>조립기 · 펼치기 · 프리셋 · 등록 창의 미리보기 — 진짜 뷰 + 진짜 뷰모델, 가짜 카탈로그, 임시 폴더의 프리셋 파일.</summary>
internal sealed class AssemblyPreview
{
    private readonly PreviewComponentCatalog _catalog = new();
    private readonly DevicePresetStore _store;
    private readonly PreviewDialogs _dialogs = new();

    public AssemblyPreview(string workDirectory)
    {
        _store = new DevicePresetStore(Path.Combine(workDirectory, "preview-presets.json"));
        _store.Load();      // 파일이 없으니 본보기 5종이 심긴다
    }

    /// <summary>보드에 부품을 몇 개 올린 조립기 — 하나는 key 가 겹치고, 하나는 비가동, 하나는 상태 없는 유형.</summary>
    public async Task<(FrameworkElement View, AssemblyViewModel ViewModel)> ComposeAsync()
    {
        var vm = AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, _catalog, _store, _dialogs);
        var view = new AssemblyView { DataContext = vm };
        await ((IActivate)vm).ActivateAsync();

        foreach (var code in new[] { "DOOR_SENSOR", "TEMPERATURE_SENSOR", "HEATER", "FAN", "POWER_SUPPLY", "NETWORK_INTERFACE" })
            vm.AddFromPalette(vm.Palette.FirstOrDefault(p => p.Code == code));

        if (vm.BoardItems.Count >= 4)
        {
            vm.BoardItems[1].Slot.Channel = 1;
            vm.BoardItems[2].Slot.InService = false;
            vm.OnBoardSelectionChanged(new[] { vm.BoardItems[2] });
            var enabled = vm.OverrideRows.FirstOrDefault(r => r.Name == "enabled");
            if (enabled is not null) enabled.Text = "false";
        }
        return (view, vm);
    }

    public void MakeKeyCollision(AssemblyViewModel vm)
    {
        if (vm.BoardItems.Count >= 4) vm.BoardItems[3].Slot.Key = vm.BoardItems[2].Slot.Key;
    }

    public async Task<FrameworkElement> EditDeviceAsync()
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "door", Type = "DOOR_SENSOR", Label = "전면 문" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "heater", Type = "HEATER" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "old_part", Type = "RETIRED_TYPE", Label = "옛 부품" });
        var device = new EnclosureDeviceModel { Id = 401, DeviceNumber = 1, DeviceName = "동측 함체 1", Axes = new DeviceAxesModel { HardwareSpec = spec } };

        var vm = AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, _catalog, _store,
            new ComponentApplyService(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()), _dialogs);
        var view = new AssemblyView { DataContext = vm };
        await ((IActivate)vm).ActivateAsync();
        vm.AddFromPalette(vm.Palette.FirstOrDefault(p => p.Code == "FAN"));
        return view;
    }

    public FrameworkElement RepeatExpand(bool withConflict)
    {
        var palette = _catalog.ComponentTypes(EnumDeviceCategory.Controller).Select(i => new PaletteItemViewModel(i)).ToList();
        var existing = withConflict ? new[] { "ci_03", "ci_07" } : Array.Empty<string>();
        var vm = new RepeatExpandViewModel(palette, palette.FirstOrDefault(p => p.Code == "CONTACT_INPUT"), existing) { KeyFormat = "ci_{02d}" };
        return new RepeatExpandView { DataContext = vm };
    }

    public FrameworkElement PresetManager()
        => new PresetManagerView { DataContext = new PresetManagerViewModel(_store, _catalog, _dialogs, EnumDeviceCategory.Enclosure) };

    public FrameworkElement Register(bool withProblem)
    {
        var vm = new RegisterFromPresetViewModel(_store, _catalog,
            new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()),
            Array.Empty<IControllerDeviceModel>(), _ => new HashSet<int> { 1, 2 }, EnumDeviceCategory.Enclosure);
        if (withProblem) { vm.DeviceNumber = "2"; vm.DeviceName = " "; }
        else vm.DeviceName = "서측 함체 3";
        return new RegisterFromPresetView { DataContext = vm };
    }

    private sealed class PreviewDialogs : IAssemblyDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult<string?>(initial);
        public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys) => Task.FromResult<RepeatExpandSpec?>(null);
        public Task<int?> OpenRegisterAsync(DevicePreset preset) => Task.FromResult<int?>(null);
    }
}

/// <summary>목업의 부품 유형 가운데 몇 개 — 가족(형태) 다섯 가지가 다 보이게 골랐다.</summary>
internal sealed class PreviewComponentCatalog : IComponentCatalog
{
    private static readonly EnumDeviceCategory[] Boxes = { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Controller };
    private static readonly EnumDeviceCategory[] All = Enum.GetValues<EnumDeviceCategory>();

    private readonly List<ComponentTypeInfo> _types = new()
    {
        Type("DOOR_SENSOR", "문 센서", ComponentFamily.Sensing, new[] { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Gate }, new[] { "OPEN", "CLOSED" }, produces: new[] { "DOOR_OPENED" }),
        Type("TEMPERATURE_SENSOR", "온도 센서", ComponentFamily.Sensing, Boxes, Array.Empty<string>(), produces: new[] { "OVER_TEMPERATURE" }),
        Type("HUMIDITY_SENSOR", "습도 센서", ComponentFamily.PowerEnvironment, Boxes, Array.Empty<string>()),
        Type("UPS", "무정전 전원", ComponentFamily.PowerEnvironment, Boxes, new[] { "ONLINE", "ON_BATTERY" }),
        Type("CONTACT_INPUT", "접점 입력", ComponentFamily.Sensing, Boxes, new[] { "ACTIVE", "IDLE" }),
        Type("HEATER", "히터", ComponentFamily.PowerEnvironment, Boxes, new[] { "ON", "OFF" }, new[] { "ON", "OFF" }, overrides: new[] { "enabled", "on_below_c", "off_above_c" }),
        Type("FAN", "팬", ComponentFamily.PowerEnvironment, Boxes, new[] { "ON", "OFF" }, new[] { "ON", "OFF" }, overrides: new[] { "enabled", "on_above_c" }),
        Type("POWER_SUPPLY", "전원 공급기", ComponentFamily.PowerEnvironment, All, new[] { "NORMAL", "BATTERY" }),
        Type("RELAY_OUTPUT", "릴레이 출력", ComponentFamily.Actuation, Boxes, new[] { "ON", "OFF" }, new[] { "ON", "OFF", "PULSE" }),
        Type("DOOR_ACTUATOR", "문 구동기", ComponentFamily.Actuation, new[] { EnumDeviceCategory.Gate }, new[] { "OPEN", "CLOSED", "MOVING" }, new[] { "OPEN", "CLOSE", "STOP" }),
        Type("PTZ_UNIT", "PTZ 구동부", ComponentFamily.Actuation, new[] { EnumDeviceCategory.Camera }, new[] { "IDLE", "MOVING" }, new[] { "MOVE", "STOP", "PRESET" }),
        Type("LENS", "렌즈", ComponentFamily.Optics, new[] { EnumDeviceCategory.Camera }, Array.Empty<string>()),
        Type("IR_ILLUMINATOR", "적외선 조명", ComponentFamily.Optics, new[] { EnumDeviceCategory.Camera }, new[] { "ON", "OFF" }, new[] { "ON", "OFF" }, overrides: new[] { "enabled" }),
        Type("NETWORK_INTERFACE", "네트워크 인터페이스", ComponentFamily.Network, All, new[] { "UP", "DOWN" }),
    };

    private static ComponentTypeInfo Type(string code, string label, ComponentFamily family, IReadOnlyCollection<EnumDeviceCategory> appliesTo,
        string[] states, string[]? commands = null, string[]? produces = null, string[]? overrides = null) => new()
        {
            Code = code, Label = label, Family = family, AppliesTo = appliesTo, States = states,
            Commands = commands ?? Array.Empty<string>(), Produces = produces ?? Array.Empty<string>(), OverrideParams = overrides ?? Array.Empty<string>(),
        };

    public bool IsLoaded => true;
    public event EventHandler? CatalogChanged { add { } remove { } }
    public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
    public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => _types.Where(t => t.AppliesToCategory(category)).ToList();
    public ComponentTypeInfo? Find(string? code) => _types.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
}
