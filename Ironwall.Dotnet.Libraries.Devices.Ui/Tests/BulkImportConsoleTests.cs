using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔의 대량 등록 배선(device-bulk-import FR-01 · FR-07 · FR-18) — 진짜 패널 뷰모델 위에서 툴바 판정 · 내보내기 · 올리기 입구를 본다.
/// 파일 창은 가짜 고르개로 바꿔 끼운다(창을 띄우지 않는다).
/// </summary>
[Collection("CaliburnIoC")]
public class BulkImportConsoleTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"bulk-console-test-{Guid.NewGuid():N}");

    public BulkImportConsoleTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _ioc.Dispose();
        try { Directory.Delete(_folder, recursive: true); } catch { /* 시험이 만든 임시 폴더 — 못 지워도 시험 결과와 무관 */ }
    }

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    private async Task<DeviceDashboardViewModel> OpenAsync()
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
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog());

        devices.Add(new LampDeviceModel { Id = 11, DeviceNumber = 1, DeviceName = "경광등 1", IpAddress = "10.1.1.1", IpPort = 80, UserPassword = "lamp-secret" });
        devices.Add(new LampDeviceModel { Id = 12, DeviceNumber = 2, DeviceName = "경광등 2", IpAddress = "10.1.1.2", IpPort = 80 });

        await ((IActivate)console).ActivateAsync();
        return console;
    }

    private sealed class StubCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public System.Collections.Generic.IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public System.Collections.Generic.IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public System.Collections.Generic.IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }

    private sealed class FakePicker : IBulkFilePicker
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public string? PickOpen() => OpenPath;
        public string? PickSave(string suggestedName, BulkFileFormat format) => SavePath;
    }

    [Fact, Trait("Scenario", "BLK-021")]
    public async Task should_hide_bulk_entries_and_refuse_files_when_the_groups_rail_is_open()
    {
        var console = await OpenAsync();
        var path = Path.Combine(_folder, "groups.csv");
        await File.WriteAllTextAsync(path, "이름\r\n그룹 1\r\n", Encoding.UTF8);

        Assert.False(console.IsBulkVisible);
        await console.ImportFileAsync(path);

        Assert.False(console.BulkImport.IsOpen);
        Assert.Equal("장비 목록(레일)을 고른 뒤 올리세요.", console.StatusText);
    }

    [Fact, Trait("Scenario", "BLK-021")]
    public async Task should_export_only_the_searched_rows_or_all_rows_without_the_password_when_exporting()
    {
        var console = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var picker = new FakePicker { SavePath = Path.Combine(_folder, "lamps.csv") };
        console.BulkFilePicker = picker;
        Assert.True(console.IsBulkVisible);
        Assert.True(console.CanBulkExport);

        console.SearchText = "경광등 1";
        await console.ExportAsync(BulkFileFormat.Csv, all: false);
        var filtered = await File.ReadAllTextAsync(picker.SavePath);

        await console.ExportAsync(BulkFileFormat.Csv, all: true);
        var all = await File.ReadAllTextAsync(picker.SavePath);

        Assert.Contains("경광등 1", filtered);
        Assert.DoesNotContain("경광등 2", filtered);
        Assert.Contains("경광등 2", all);
        Assert.DoesNotContain("lamp-secret", all);
        Assert.Contains("2대를 파일로 내보냈습니다", console.StatusText);
    }

    [Fact, Trait("Scenario", "BLK-022")]
    public async Task should_open_the_preview_with_same_rows_and_close_it_on_rail_change_when_an_export_is_uploaded()
    {
        var console = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var picker = new FakePicker { SavePath = Path.Combine(_folder, "lamps.xlsx") };
        console.BulkFilePicker = picker;
        await console.ExportAsync(BulkFileFormat.Excel, all: true);

        picker.OpenPath = picker.SavePath;
        await console.PickAndImportAsync();

        Assert.True(console.BulkImport.IsOpen);
        Assert.Equal(2, console.BulkImport.Rows.Count);
        Assert.All(console.BulkImport.Rows, r => Assert.Equal(BulkRowVerdict.Same, r.Verdict));
        Assert.False(console.BulkImport.CanRegister);   // 보낼 것이 없다

        await console.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Gate));
        Assert.False(console.BulkImport.IsOpen);
    }

    [Fact, Trait("Scenario", "BLK-022")]
    public async Task should_say_why_when_the_dropped_file_is_not_csv_or_xlsx()
    {
        var console = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        await console.ImportFileAsync(Path.Combine(_folder, "lamps.txt"));

        Assert.False(console.BulkImport.IsOpen);
        Assert.Contains("CSV(.csv) 또는 Excel(.xlsx)", console.StatusText);
        Assert.False(DeviceDashboardViewModel.IsBulkFile("a.xls"));
        Assert.True(DeviceDashboardViewModel.IsBulkFile("목록.XLSX"));
    }
}
