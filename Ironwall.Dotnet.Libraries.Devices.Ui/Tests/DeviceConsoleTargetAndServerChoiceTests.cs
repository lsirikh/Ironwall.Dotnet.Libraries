using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// GIS 실창 WP-2(2026-09-27) 판정에서 나온 장비 콘솔 결함 네 가지를 고정한다.
/// <list type="number">
///   <item>SC-DEV-033 — 레일을 열면 아무도 고르지 않은 첫 장비가 상세 칸에 올라왔다(DataGrid 가 ListCollectionView 의 현재 항목을 따라감).</item>
///   <item>SC-DEV-033 — 목록 재조회(바쁨) 중에 고른 행을 말없이 거절하고 원래 선택으로 튕겼다.</item>
///   <item>SC-DEV-015 — 스피커 등록 폼이 유형을 보지 않고 첫 서버(VMS)를 관리 서버로 골라 서버가 422 로 거절했다.</item>
///   <item>SC-DEV-012/014 — 제어기 · 카메라 · 경광등은 포트 없이는 등록되지 않는데 폼이 필수로 말하지 않았다.</item>
/// </list>
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceConsoleTargetAndServerChoiceTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);
    private static readonly string SpeakerRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Speaker);

    #region - 1. 첫 행 자동 선택 -
    [Fact]
    public void should_not_bind_grid_selection_to_current_item_when_device_list_is_a_collection_view()
    {
        // Arrange — 콘솔 목록은 검색 필터를 건 ListCollectionView 다. 뷰의 DataGrid 가 현재 항목을 따라가면 첫 행이 저절로 골라진다.
        var xaml = File.ReadAllText(Path.Combine(ProjectDir(), "Views", "Dashboards", "DeviceDashboardView.xaml"));

        // Act
        var grid = Regex.Match(xaml, "<DataGrid\\b[^>]*AutomationProperties\\.AutomationId=\"Console\\.Devices\\.Grid\"[^>]*>", RegexOptions.Singleline).Value;

        // Assert
        Assert.NotEmpty(grid);
        Assert.Contains("IsSynchronizedWithCurrentItem=\"False\"", grid);
    }

    [Fact]
    public void should_leave_nothing_selected_when_items_arrive_and_current_item_sync_is_off()
    {
        // 원인 고정 — 같은 조건(ListCollectionView · 기본 동기화)이면 DataGrid 는 첫 항목을 고른다. 끄면 아무것도 안 고른다.
        RunSta(() =>
        {
            var items = new ObservableCollection<string> { "첫 장비", "둘째 장비" };

            var synced = new DataGrid();
            synced.BeginInit(); synced.EndInit();
            synced.ItemsSource = new ListCollectionView(items);

            var unsynced = new DataGrid { IsSynchronizedWithCurrentItem = false };
            unsynced.BeginInit(); unsynced.EndInit();
            unsynced.ItemsSource = new ListCollectionView(items);

            Assert.Equal("첫 장비", synced.SelectedItem);   // 결함의 기전 — 아무도 고르지 않았는데 골라졌다
            Assert.Null(unsynced.SelectedItem);
        });
    }
    #endregion

    #region - 2. 바쁨 중 선택 거절은 말한다 -
    [Fact]
    public async Task should_tell_why_when_a_row_is_picked_while_the_list_is_reloading()
    {
        // Arrange
        BusySource source = null!;
        var (console, _) = await OpenAsync(lampSource: panel => source = new BusySource(panel, Lamp(11, "경광등 1"), Lamp(12, "경광등 2")));
        await console.SelectRailAsync(LampRail);
        source.Busy = true;                                   // [갱신] 뒤 패널이 2초 더 바쁘다

        // Act
        var accepted = console.OnRowsSelected(new List<object> { source.Items[1] });

        // Assert
        Assert.False(accepted);
        Assert.Empty(console.Form.Rows);                      // 편집 대상이 바뀌지 않았다
        Assert.Contains("고른 행을 받지 않았습니다", console.StatusText);
    }

    [Fact]
    public async Task should_stay_silent_when_the_list_rebuild_clears_the_selection_while_busy()
    {
        BusySource source = null!;
        var (console, _) = await OpenAsync(lampSource: panel => source = new BusySource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        source.Busy = true;

        console.OnRowsSelected(new List<object>());           // 목록이 다시 만들어지며 선택이 풀렸다 — 운영자의 조작이 아니다

        Assert.DoesNotContain("고른 행을 받지 않았습니다", console.StatusText);
    }
    #endregion

    #region - 3. 관리 서버 선택지 · 기본값 -
    private static ServerModel Server(int id, string name, string? type) => new() { Id = id, Name = name, CategoryServer = type };

    [Fact]
    public void should_offer_only_speaker_servers_when_types_are_known()
    {
        var servers = new IServerModel[] { Server(2, "VMS-01", "VMS"), Server(5, "방송-01", "SPEAKER_API"), Server(7, "프록시", "PROXY") };

        var allowed = DeviceServerChoice.Allowed(servers, EnumDeviceCategory.Speaker);

        Assert.Equal(new[] { 5 }, allowed.Select(s => s.Id));
    }

    [Fact]
    public void should_default_to_the_only_valid_server_and_to_none_when_several_or_none_are_valid()
    {
        var one = new IServerModel[] { Server(2, "VMS-01", "VMS"), Server(5, "방송-01", "SPEAKER_API") };
        var two = new IServerModel[] { Server(5, "방송-01", "SPEAKER_API"), Server(6, "방송-02", "SPEAKER_API") };
        var none = new IServerModel[] { Server(2, "VMS-01", "VMS") };

        Assert.Equal(5, DeviceServerChoice.DefaultFor(one, EnumDeviceCategory.Speaker)?.Id);
        Assert.Null(DeviceServerChoice.DefaultFor(two, EnumDeviceCategory.Speaker));
        Assert.Null(DeviceServerChoice.DefaultFor(none, EnumDeviceCategory.Speaker));   // 예전엔 VMS 를 골라 422
    }

    [Fact]
    public void should_keep_legacy_first_server_default_when_server_types_are_unknown()
    {
        // 6.3 응답에는 category_server 가 없다 — 거를 근거가 없으면 예전 동작 그대로.
        var legacy = new IServerModel[] { Server(9, "B", null), Server(3, "A", null) };

        Assert.Equal(3, DeviceServerChoice.DefaultFor(legacy, EnumDeviceCategory.Speaker)?.Id);
        Assert.Equal(2, DeviceServerChoice.Allowed(legacy, EnumDeviceCategory.Speaker).Count);
    }

    [Fact]
    public async Task should_list_only_speaker_servers_in_the_speaker_form_server_field()
    {
        var servers = new ServerProvider(new MockLogService());
        var (console, _) = await OpenAsync(servers: servers);
        servers.Add(Server(2, "VMS-01", "VMS"));
        servers.Add(Server(5, "방송-01", "SPEAKER_API"));
        var spec = DevicePropertyCatalog.For(EnumDeviceCategory.Speaker, isAxisContract: false).Single(s => s.Key == "server_id");

        var options = console.OptionsFor(spec, EnumDeviceCategory.Speaker);

        Assert.Equal(new[] { "방송-01" }, options.Select(o => o.Display));
    }

    [Fact]
    public async Task should_preselect_the_speaker_server_not_the_first_server_when_a_speaker_is_added()
    {
        // Arrange — 스피커 패널의 [추가]는 컨테이너에서 서버 목록을 읽는다.
        var servers = new ServerProvider(new MockLogService());
        servers.Add(Server(2, "VMS-01", "VMS"));
        servers.Add(Server(5, "방송-01", "SPEAKER_API"));
        var previous = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(ServerProvider) ? servers : previous(type, key);
        var (console, _) = await OpenAsync(servers: servers);
        await console.SelectRailAsync(SpeakerRail);

        // Act
        console.Add();

        // Assert
        var draft = Assert.IsType<SpeakerDeviceViewModel>(Assert.Single(console.Form.Rows));
        Assert.Equal(5, ((ISpeakerDeviceModel)draft.Model).Server?.Id);   // 예전: 2(VMS) → 서버 422
    }
    #endregion

    #region - 4. 포트는 등록 필수 -
    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Lamp)]
    public void should_require_port_on_create_when_category_is_registered_only_with_a_port(EnumDeviceCategory category)
    {
        foreach (var axis in new[] { false, true })
        {
            var port = DevicePropertyCatalog.For(category, axis).Single(s => s.Key == "connection.ip_port");

            Assert.True(port.IsRequiredOnCreate, $"{category} axis={axis}");
            Assert.Contains("필수", DevicePropertyAccessor.Validate(port, "", isCreating: true) ?? string.Empty);
            Assert.NotNull(DevicePropertyAccessor.Validate(port, "0", isCreating: true));   // 패널은 1 이상만 보낸다
            Assert.Null(DevicePropertyAccessor.Validate(port, "", isCreating: false));       // 기존 장비 편집은 그대로
        }
    }
    #endregion

    #region - Helpers -
    private static async Task<(DeviceDashboardViewModel Console, LampDevicePanelViewModel Lamps)> OpenAsync(
        Func<LampDevicePanelViewModel, IDeviceConsoleSource>? lampSource = null, ServerProvider? servers = null)
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var lamps = new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            lamps,
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, servers ?? new ServerProvider(log), api, new StubCatalog());

        if (lampSource is not null) console.UseSource(LampRail, lampSource(lamps));

        await ((IActivate)console).ActivateAsync();
        return (console, lamps);
    }

    private static LampDeviceViewModel Lamp(int id, string name) => new(new LampDeviceModel { Id = id, DeviceNumber = id, DeviceName = name });

    private static string ProjectDir([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, ".."));

    private static void RunSta(System.Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    /// <summary>바쁨을 시험이 정하는 목록 원천.</summary>
    private sealed class BusySource : IDeviceConsoleSource
    {
        public BusySource(BasePanelViewModel panel, params LampDeviceViewModel[] rows)
        {
            Panel = panel;
            foreach (var row in rows) Items.Add(row);
        }

        public ObservableCollection<LampDeviceViewModel> Items { get; } = new();
        public bool Busy { get; set; }

        public BasePanelViewModel Panel { get; }
        public IEnumerable Rows => Items;
        public INotifyCollectionChanged RowsChanged => Items;
        public int RowCount => Items.Count;
        public bool IsBusy => Busy;
        public event EventHandler? BusyEnded { add { } remove { } }

        public void Select(IReadOnlyList<object> rows) { }
        public object? CreateDraft() => null;
        public void AdoptDraft(object draft) { }
        public void ReleaseDraft(object draft) { }
        public bool Save() => false;
        public void Delete() { }
        public bool Reload() => false;
    }

    private sealed class StubCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
    #endregion
}
