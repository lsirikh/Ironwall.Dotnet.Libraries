using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// GIS 실창 WP-2 2 ~ 6차(2026-09-28) 판정에서 나온 장비 콘솔 · 조립기 결함을 고정한다.
/// <list type="number">
///   <item>SC-DEV-013/015/014.lamp — [등록] 직후(패널이 2초 더 바쁨) 레일 전환을 <b>말없이</b> 거절했다. 콘솔은 앞 레일에 남아
///         '경광등'으로 친 것이 함체로 만들어졌다(api_logs: <c>POST devices/enclosures</c> name='…LAMP…'). 화면 레일의 되돌림은
///         커널(fbc7e895 · 레일 담당 시험 <c>tests/Consoles.ViewTests/DeviceConsoleRailViewTests</c>)이 맡고, 여기서는 까닭 한 줄을 본다.</item>
///   <item>SC-DEV-015(4 ~ 6차) — 방송서버가 없을 때 스피커 [추가]가 안내를 <b>기다린 뒤</b> 초안을 달아, 콘솔은 초안이 없다고 보고
///         등록 폼을 열지 않았다(제목 '선택한 항목 없음'). 초안을 먼저 달고 안내한다.</item>
///   <item>SC-ASM-024/025 — 프리셋 목록 · 배정 목록 항목의 UIA 이름이 레코드 ToString(<c>DevicePreset { Id = … }</c>) · 형식 이름이었다.
///         (카테고리 콤보 항목의 영문 열거값 이름은 보고만 했다 — 컨테이너 스타일을 바꾸면 테마 항목 모양을 잃을 위험.)</item>
/// </list>
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceConsoleRailRefusalAndNamesTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);
    private static readonly string EnclosureRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Enclosure);
    private static readonly string SpeakerRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Speaker);

    #region - 1. 바쁠 때 막힌 레일 전환은 까닭을 말한다 -
    [Fact]
    public async Task should_say_why_when_the_rail_switch_is_refused_while_the_list_is_busy()
    {
        // Arrange — [등록] 직후 패널이 2초 더 바쁘다.
        BusySource source = null!;
        var console = await OpenAsync(panel => source = new BusySource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        source.Busy = true;

        // Act
        var switched = await console.SelectRailAsync(EnclosureRail);

        // Assert — 콘솔은 경광등에 남고, 상태 띠가 까닭을 말한다(예전: 말없이 거절).
        Assert.False(switched);
        Assert.Equal(LampRail, console.SelectedRail?.Key);
        Assert.Equal(DeviceDashboardViewModel.RailRefusedWhileBusyText, console.StatusText);
    }

    [Fact]
    public async Task should_not_blame_busy_when_the_rail_switch_is_refused_by_unapplied_changes()
    {
        // 미적용 변경으로 막힌 것은 바닥 막대(상세)가 흔들리며 말한다 — '처리 중' 까닭을 붙이지 않는다.
        BusySource source = null!;
        var console = await OpenAsync(panel => source = new BusySource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        console.Detail.Tracker.Touch("name_device", "경광등 1", "고친 이름");

        Assert.False(await console.SelectRailAsync(EnclosureRail));
        Assert.NotEqual(DeviceDashboardViewModel.RailRefusedWhileBusyText, console.StatusText);
    }
    #endregion

    #region - 2. 방송서버가 없어도 스피커 등록 폼은 열린다 -
    [Fact]
    public async Task should_open_the_speaker_create_form_even_when_no_broadcast_server_exists()
    {
        // Arrange — 서버는 VMS 하나뿐(스피커를 받는 서버 없음). 호스트의 안내 팝업은 곧바로 끝나지 않는다(창을 띄운다).
        var servers = new ServerProvider(new MockLogService());
        servers.Add(new ServerModel { Id = 2, Name = "VMS-01", CategoryServer = "VMS" });
        var previous = IoC.GetInstance;
        IoC.GetInstance = (type, key) => type == typeof(ServerProvider) ? servers : previous(type, key);
        var events = new EventAggregator();
        var popup = new SlowPopupHost();
        events.SubscribeOnPublishedThread(popup);
        var console = await OpenAsync(events: events, servers: servers);
        await console.SelectRailAsync(SpeakerRail);

        // Act
        console.Add();

        // Assert — 안내는 뜨고, 등록 폼도 열린다(초안이 폼에 물림). 관리 서버는 비어 있다(VMS 를 고르지 않는다).
        Assert.Single(popup.Popups);
        Assert.True(console.Detail.IsCreating, $"등록 폼이 열려야 한다 · 상태 '{console.StatusText}'");
        var draft = Assert.IsType<SpeakerDeviceViewModel>(Assert.Single(console.Form.Rows));
        Assert.Null(((ISpeakerDeviceModel)draft.Model).Server);
        popup.Finish();
    }
    #endregion

    #region - 3. 목록 항목의 읽히는 이름 -
    [Fact]
    public void should_read_preset_by_its_name_when_listed()
    {
        var preset = new DevicePreset { Id = "seed-enclosure-standard", Name = "표준 옥외 함체", Category = EnumDeviceCategory.Enclosure };

        Assert.Equal("표준 옥외 함체", preset.ToString());
    }

    [Fact]
    public void should_read_assign_item_by_number_and_name_when_listed()
    {
        var item = new DeviceAssignItemViewModel { Id = 7, DeviceNumber = 12, DeviceName = "정문 센서" };

        Assert.Equal("#12 정문 센서", item.ToString());
    }

    #endregion

    #region - Helpers -
    private static async Task<DeviceDashboardViewModel> OpenAsync(Func<LampDevicePanelViewModel, IDeviceConsoleSource>? lampSource = null,
        EventAggregator? events = null, ServerProvider? servers = null)
    {
        var log = new MockLogService();
        events ??= new EventAggregator();
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
        return console;
    }

    private static LampDeviceViewModel Lamp(int id, string name) => new(new LampDeviceModel { Id = id, DeviceNumber = id, DeviceName = name });

    /// <summary>호스트의 안내 팝업처럼 곧바로 끝나지 않는 처리기 — 창을 띄우는 동안 게시(await)가 돌아오지 않는다.</summary>
    private sealed class SlowPopupHost : IHandle<OpenInfoPopupMessageModel>
    {
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<OpenInfoPopupMessageModel> Popups { get; } = new();

        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken)
        {
            Popups.Add(message);
            return _closed.Task;
        }

        public void Finish() => _closed.TrySetResult();
    }

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
