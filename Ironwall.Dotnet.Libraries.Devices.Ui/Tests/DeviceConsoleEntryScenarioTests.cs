using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 창에서 조립기 · 셋업 결선으로 들어가는 입구(카탈로그 §6.1 WIR-002 · 003 · §6.8 ASM-001 · 002) — 진짜 패널 뷰모델 8개를 가짜 API 위에 세운 장비 콘솔.
/// 창 없음 · 서버 없음. 입구 단추의 배선(XAML)은 파일을 XML 로 읽어 확인한다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceConsoleEntryScenarioTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string ControllerRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Controller);

    private sealed class RecordingAssembly : IAssemblyLauncher
    {
        public bool IsAvailable { get; set; } = true;
        public List<string> Calls { get; } = new();
        public Task<int?> ComposeAsync(EnumDeviceCategory category) { Calls.Add($"compose:{category}"); return Task.FromResult<int?>(null); }
        public Task<bool> EditDeviceAsync(IBaseDeviceModel device, EnumDeviceCategory category) { Calls.Add($"edit:{category}:{device.Id}"); return Task.FromResult(false); }
        public Task<int?> RegisterFromPresetAsync(EnumDeviceCategory category) { Calls.Add($"register:{category}"); return Task.FromResult<int?>(null); }
        public Task ManagePresetsAsync(EnumDeviceCategory category) { Calls.Add($"presets:{category}"); return Task.CompletedTask; }
    }

    private sealed class RecordingWiring : IWiringLauncher
    {
        public bool IsAvailable { get; set; } = true;
        public List<IBaseDeviceModel> Opened { get; } = new();
        public Task<bool> OpenAsync(IBaseDeviceModel controller) { Opened.Add(controller); return Task.FromResult(false); }
    }

    private sealed class StubCatalog : ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<CatalogExtraAxis>();
        public IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }

    private static async Task<DeviceDashboardViewModel> OpenAsync(RecordingAssembly assembly, RecordingWiring wiring)
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService),
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog(), null,
            new Lazy<IAssemblyLauncher>(() => assembly), new Lazy<IWiringLauncher>(() => wiring));

        groups.Add(new DeviceGroupModel { Id = 1, Name = "정문" });
        devices.Add(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "북측 제어기", IpAddress = "10.99.7.1", Port = 9001 });
        devices.Add(new ControllerDeviceModel { Id = 22, DeviceNumber = 2, DeviceName = "남측 제어기", IpAddress = "10.99.7.2", Port = 9002 });
        devices.Add(new EnclosureDeviceModel { Id = 31, DeviceNumber = 1, DeviceName = "함체 1" });

        await ((IActivate)console).ActivateAsync();
        return console;
    }

    private static List<object> RowsOf(DeviceDashboardViewModel console) => console.Rows!.Cast<object>().ToList();

    private static string NameOf(object row) => row switch
    {
        ControllerDeviceViewModel c => c.DeviceName,
        _ => row.ToString() ?? string.Empty,
    };

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static XDocument DashboardXaml()
        => XDocument.Load(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Views", "Dashboards", "DeviceDashboardView.xaml"));

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static string? Attr(XElement e, string localName) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

    private static XElement ById(XDocument doc, string id)
        => doc.Descendants().Single(e => Attr(e, "AutomationProperties.AutomationId") == id);

    #region - 조립기 입구 (ASM-001 · 002) -
    [Fact]
    [Trait("Scenario", "ASM-001")]
    public async Task should_offer_the_assembly_menu_only_on_category_rails_and_never_on_the_groups_rail_ASM001()
    {
        var assembly = new RecordingAssembly();
        var console = await OpenAsync(assembly, new RecordingWiring());

        var byRail = new Dictionary<string, bool>();
        foreach (var rail in console.RailEntries.Select(r => r.Key).ToList())
        {
            await console.SelectRailAsync(rail);
            byRail[rail] = console.CanAssemble;
        }
        await console.SelectRailAsync(DeviceDashboardViewModel.GroupsRailKey);
        var groups = console.CanAssemble;
        await console.OpenAssemblyAsync();                                                 // 가려진 채 불러도 열리는 길이 없다

        Assert.False(byRail[DeviceDashboardViewModel.GroupsRailKey]);
        Assert.False(groups);
        Assert.Empty(assembly.Calls);
        foreach (var category in new[] { EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera, EnumDeviceCategory.Enclosure })
            Assert.True(byRail[DeviceDashboardViewModel.RailKeyOf(category)], $"{category} 레일에 조립기 메뉴가 없다");

        // `Console.Devices.Toolbar.AssemblyMenu` 는 CanAssemble 에 묶인 묶음 안에 있다 — 그룹 레일에서는 단추가 아예 접힌다
        var menu = ById(DashboardXaml(), "Console.Devices.Toolbar.AssemblyMenu");
        var host = menu.Ancestors().First(a => Attr(a, "Visibility") is { } v && v.Contains("Binding"));
        Assert.Contains("CanAssemble", Attr(host, "Visibility"));
    }

    [Fact]
    [Trait("Scenario", "ASM-002")]
    public async Task should_list_exactly_assembler_register_from_preset_and_preset_manager_under_the_menu_and_open_each_for_the_rail_ASM002()
    {
        var doc = DashboardXaml();
        var button = ById(doc, "Console.Devices.Toolbar.AssemblyMenu");
        var items = button.Descendants(Presentation + "MenuItem").Select(m => (Id: Attr(m, "AutomationProperties.AutomationId"), Header: Attr(m, "Header"), Click: Attr(m, "Click"))).ToList();
        var codeBehind = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Views", "Dashboards", "DeviceDashboardView.xaml.cs"));

        Assert.Equal(new[]
        {
            ("Console.Devices.Toolbar.Assembly", "조립기", "OnOpenAssembly"),
            ("Console.Devices.Toolbar.RegisterFromPreset", "프리셋으로 등록", "OnRegisterFromPreset"),
            ("Console.Devices.Toolbar.Presets", "프리셋 관리", "OnManagePresets"),
        }, items.Select(i => (i.Id!, i.Header!, i.Click!)));                                // ▾ = 3항목
        Assert.Equal("OnOpenAssemblyMenu", Attr(button, "Click"));
        Assert.Contains("vm.OpenAssemblyAsync()", codeBehind);
        Assert.Contains("vm.RegisterFromPresetAsync()", codeBehind);
        Assert.Contains("vm.ManagePresetsAsync()", codeBehind);

        // 세 항목이 부르는 뷰모델 길 — 고른 레일의 카테고리로 연다
        var assembly = new RecordingAssembly();
        var console = await OpenAsync(assembly, new RecordingWiring());
        await console.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Enclosure));
        await console.OpenAssemblyAsync();
        await console.RegisterFromPresetAsync();
        await console.ManagePresetsAsync();

        Assert.Equal(new[] { "compose:Enclosure", "register:Enclosure", "presets:Enclosure" }, assembly.Calls);
    }
    #endregion

    #region - 셋업 · 결선 입구 (WIR-002 · 003) -
    [Fact]
    [Trait("Scenario", "WIR-002")]
    public async Task should_enable_setup_wiring_for_one_controller_and_disable_it_with_a_reason_for_two_WIR002()
    {
        var wiring = new RecordingWiring();
        var console = await OpenAsync(new RecordingAssembly(), wiring);
        Assert.True(await console.SelectRailAsync(ControllerRail));
        var rows = RowsOf(console);
        var none = (console.IsWiringVisible, console.CanOpenWiring, console.WiringBlockedReason);

        console.OnRowsSelected(rows.Take(1).ToList());                                      // 제어기 1행
        var one = (console.IsWiringVisible, console.CanOpenWiring, console.WiringBlockedReason);
        console.OnRowsSelected(rows.Take(2).ToList());                                      // 2행
        var two = (console.IsWiringVisible, console.CanOpenWiring, console.WiringBlockedReason);
        await console.OpenWiringAsync();                                                    // 꺼진 채 불러도 열리지 않는다
        console.OnRowsSelected(rows.Take(1).ToList());
        await console.OpenWiringAsync();

        Assert.Equal((true, false, "제어기 한 대를 고르세요."), none);
        Assert.Equal((true, true, (string?)null), one);
        Assert.Equal((true, false, "제어기 한 대를 고르세요."), two);                       // 꺼짐 + 사유(ShowOnDisabled 툴팁)
        var opened = Assert.Single(wiring.Opened);
        Assert.Equal(NameOf(rows[0]), opened.DeviceName);

        // 단추 배선 — IsEnabled=CanOpenWiring · ToolTip=WiringBlockedReason(꺼져도 보임)
        var button = ById(DashboardXaml(), "Console.Devices.Toolbar.Wiring");
        Assert.Contains("CanOpenWiring", Attr(button, "IsEnabled"));
        Assert.Contains("WiringBlockedReason", Attr(button, "ToolTip"));
        Assert.Equal("True", Attr(button, "ToolTipService.ShowOnDisabled"));
    }

    [Fact]
    [Trait("Scenario", "WIR-003")]
    public async Task should_show_the_selected_controller_in_the_detail_before_wiring_is_opened_WIR003()
    {
        var wiring = new RecordingWiring();
        var console = await OpenAsync(new RecordingAssembly(), wiring);
        Assert.True(await console.SelectRailAsync(ControllerRail));
        var rows = RowsOf(console);

        var picked = rows.Single(r => NameOf(r) == "남측 제어기");
        console.OnRowsSelected(new List<object> { picked });
        var name = console.Form.Fields.Single(f => f.Key == "name_device").Text;           // `Devices.Detail.Field.name_device`
        await console.OpenWiringAsync();

        Assert.Equal("남측 제어기", name);
        Assert.Same(picked, Assert.Single(console.Form.Rows));
        Assert.Equal(22, Assert.Single(wiring.Opened).Id);                                  // 상세의 그 제어기로 결선 창을 연다
    }
    #endregion
}
