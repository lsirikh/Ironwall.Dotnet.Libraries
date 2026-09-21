using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>콘솔이 조립기 · 프리셋 · 등록 창을 여는 입구(FR-17). 콘솔 뷰모델은 이 인터페이스만 안다.</summary>
public interface IAssemblyLauncher
{
    /// <summary>부품 모델이 있는 서버(7.0+)에서만 입구를 낸다(FR-18).</summary>
    bool IsAvailable { get; }

    /// <summary>새로 조립. 등록까지 했으면 그 장비의 Id.</summary>
    Task<int?> ComposeAsync(EnumDeviceCategory category);

    /// <summary>기존 장비의 부품 구성 바꾸기. 적용했으면 true.</summary>
    Task<bool> EditDeviceAsync(IBaseDeviceModel device, EnumDeviceCategory category);

    /// <summary>프리셋으로 등록. 등록했으면 그 장비의 Id.</summary>
    Task<int?> RegisterFromPresetAsync(EnumDeviceCategory category);

    Task ManagePresetsAsync(EnumDeviceCategory category);
}

/// <summary>
/// 창 관리자(<see cref="IWindowManager"/>)로 라이브러리가 직접 창을 연다 — 호스트의 메시지 계약을 늘리지 않는다.
/// 조립기는 싱글턴이 아니다: 열 때마다 새 뷰모델을 만든다.
/// </summary>
public sealed class AssemblyLauncher : IAssemblyLauncher, IAssemblyDialogs
{
    private readonly IWindowManager _windows;
    private readonly IComponentCatalog _catalog;
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService _providerService;
    private readonly DeviceProvider _devices;
    private readonly ControllerDeviceProvider _controllers;
    private readonly DeviceQueryPolicy _policy;
    private readonly ILogService? _log;
    private readonly Lazy<DevicePresetStore> _store;

    public AssemblyLauncher(IWindowManager windows, IComponentCatalog catalog, IDeviceApiService api, IDeviceProviderService providerService,
        DeviceProvider devices, ControllerDeviceProvider controllers, DeviceQueryPolicy policy, ILogService? log = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _log = log;

        // 파일은 처음 쓸 때 읽는다 — 장비 창을 열기만 해도 디스크를 건드리지는 않는다.
        _store = new Lazy<DevicePresetStore>(() =>
        {
            var store = new DevicePresetStore(DevicePresetStore.DefaultPath);
            store.Load();
            if (!string.IsNullOrEmpty(store.StateMessage)) _log?.Warning($"[Assembly] {store.StateMessage}");
            return store;
        });
    }

    public bool IsAvailable => _policy.IsAxisContract;

    #region - IAssemblyLauncher -
    public async Task<int?> ComposeAsync(EnumDeviceCategory category)
    {
        if (!IsAvailable) return null;      // 6.3 에는 부품 모델이 없다 — 입구가 가려져 있어도 여기서 한 번 더 막는다
        var vm = AssemblyViewModel.Compose(category, _catalog, _store.Value, this);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(1280, 760, resizable: true));
        return vm.RegisteredDeviceId;
    }

    public async Task<bool> EditDeviceAsync(IBaseDeviceModel device, EnumDeviceCategory category)
    {
        if (!IsAvailable) return false;
        var vm = AssemblyViewModel.ForDevice(device, category, _catalog, _store.Value, new ComponentApplyService(_api, _providerService, _log), this);
        return await _windows.ShowDialogAsync(vm, null, WindowSettings(1280, 760, resizable: true)) == true;
    }

    public async Task<int?> RegisterFromPresetAsync(EnumDeviceCategory category)
    {
        if (!IsAvailable) return null;
        var vm = NewRegister(category, null);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Large), 660, resizable: true));
        return vm.RegisteredDeviceId;
    }

    public async Task ManagePresetsAsync(EnumDeviceCategory category)
    {
        var vm = new PresetManagerViewModel(_store.Value, _catalog, this, category);
        vm.OpenInAssemblyRequested += async (_, preset) =>
        {
            // async void 처리기다 — 여기서 새는 예외는 앱을 죽인다. 잡아서 기록한다.
            try
            {
                var editor = AssemblyViewModel.ForPreset(preset, _catalog, _store.Value, this);
                await _windows.ShowDialogAsync(editor, null, WindowSettings(1280, 760, resizable: true));
                vm.ReloadAndSelect(preset.Id);
            }
            catch (Exception ex) { _log?.Error($"[Assembly] 프리셋을 조립기로 열지 못했다 — {ex.Message}"); }
        };
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Large), 560, resizable: true));
    }
    #endregion

    #region - IAssemblyDialogs -
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new ConfirmPromptViewModel(title, message);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Small), 260, resizable: false));
        return vm.Result;
    }

    public async Task<string?> AskTextAsync(string title, string label, string initial)
    {
        var vm = new TextPromptViewModel(title, label, initial);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Small), 240, resizable: false));
        return vm.Result;
    }

    public async Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys)
    {
        var vm = new RepeatExpandViewModel(palette, preselected, existingKeys);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Small), 620, resizable: false));
        return vm.Result;
    }

    public async Task<int?> OpenRegisterAsync(DevicePreset preset)
    {
        var vm = NewRegister(preset.Category, preset);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Large), 660, resizable: true));
        return vm.RegisteredDeviceId;
    }
    #endregion

    private RegisterFromPresetViewModel NewRegister(EnumDeviceCategory category, DevicePreset? fixedPreset)
        => new(_store.Value, _catalog, new PresetRegistrar(_api, _providerService, _log),
            _controllers.CollectionEntity.ToList(),
            c => _devices.OfType<IBaseDeviceModel>().Where(d => DeviceRailCounter.CategoryOf(d) == c).Select(d => d.DeviceNumber).ToHashSet(),
            category, fixedPreset);

    private static IDictionary<string, object> WindowSettings(double width, double height, bool resizable)
    {
        var settings = new Dictionary<string, object>
        {
            ["Width"] = width,
            ["Height"] = height,
            ["MinWidth"] = Math.Min(width, 420),
            ["MinHeight"] = Math.Min(height, 200),
            ["SizeToContent"] = SizeToContent.Manual,
            ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
            ["ResizeMode"] = resizable ? ResizeMode.CanResize : ResizeMode.NoResize,
            ["ShowInTaskbar"] = false,
        };

        // 창의 바탕은 여기서 정하지 않는다 — 한 번 찾아 넣은 브러시는 테마를 바꿔도 옛 색으로 굳는다.
        // 뷰가 제 바탕을 DynamicResource 로 칠한다.
        return settings;
    }
}
