using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
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
/// 장비 콘솔 뷰모델(device-console-redesign FR-02 · FR-03 · FR-10 ~ FR-12) — 진짜 패널 뷰모델 8개를 가짜 API 위에 세워 본다.
/// 계약은 6.3(컨테이너 미구성의 기본값)이라 레일은 8개다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceDashboardConsoleTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    private static async Task<(DeviceDashboardViewModel Console, LampDevicePanelViewModel Lamps)> OpenAsync(Func<LampDevicePanelViewModel, IDeviceConsoleSource>? lampSource = null, Lazy<Consoles.Assembly.IAssemblyLauncher>? launcher = null, EventAggregator? eventAggregator = null)
    {
        var log = new MockLogService();
        var events = eventAggregator ?? new EventAggregator();
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
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog(), null, launcher);

        if (lampSource is not null) console.UseSource(LampRail, lampSource(lamps));

        // 카테고리별 프로바이더는 만들어진 뒤의 추가만 따라간다.
        groups.Add(new DeviceGroupModel { Id = 1, Name = "정문" });
        devices.Add(new LampDeviceModel { Id = 11, DeviceNumber = 1, DeviceName = "경광등 1", Status = EnumDeviceStatus.ACTIVATED });
        devices.Add(new LampDeviceModel { Id = 12, DeviceNumber = 2, DeviceName = "경광등 2", Status = EnumDeviceStatus.ERROR });
        devices.Add(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1" });

        await ((IActivate)console).ActivateAsync();
        return (console, lamps);
    }

    private static List<object> RowsOf(DeviceDashboardViewModel console) => console.Rows!.Cast<object>().ToList();

    #region - 닫기 확인(2026-09-27 전수 조사: ✕ · 좌측 메뉴 전환이 적용하지 않은 변경을 묻지 않고 버렸다) -
    private sealed class CloseSink : IHandle<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenConfirmPopupMessageModel>,
                                     IHandle<Ironwall.Dotnet.Libraries.ViewModel.Models.ClosePanelMessageModel>
    {
        public List<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenConfirmPopupMessageModel> Confirms { get; } = new();
        public int ClosePanels { get; private set; }

        public Task HandleAsync(Ironwall.Dotnet.Libraries.ViewModel.Models.OpenConfirmPopupMessageModel message, CancellationToken cancellationToken)
        { Confirms.Add(message); return Task.CompletedTask; }

        public Task HandleAsync(Ironwall.Dotnet.Libraries.ViewModel.Models.ClosePanelMessageModel message, CancellationToken cancellationToken)
        { ClosePanels++; return Task.CompletedTask; }
    }

    private static async Task<(DeviceDashboardViewModel Console, CloseSink Sink)> OpenWithSinkAsync()
    {
        var events = new EventAggregator();
        var sink = new CloseSink();
        events.SubscribeOnPublishedThread(sink);
        var (console, _) = await OpenAsync(eventAggregator: events);
        return (console, sink);
    }

    [Fact]
    public async Task should_close_without_asking_when_the_device_console_has_nothing_pending()
    {
        var (console, sink) = await OpenWithSinkAsync();

        Assert.True(await console.CanCloseAsync());
        Assert.Empty(sink.Confirms);
        Assert.Null(console.PendingWorkSummary());
    }

    [Fact]
    public async Task should_ask_before_closing_when_the_device_detail_has_unapplied_changes()
    {
        var (console, sink) = await OpenWithSinkAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { RowsOf(console)[0] });
        console.Detail.Tracker.Touch("name_device", "경광등 1", "고친 이름");

        Assert.False(await console.CanCloseAsync());

        var confirm = Assert.Single(sink.Confirms);
        Assert.Contains("적용하지 않은 장비 정보 변경", confirm.Explain);
        Assert.IsType<CallCloseDeviceConsoleMessageModel>(confirm.MessageModel);
        Assert.Equal(0, sink.ClosePanels);            // 묻기만 하고 닫지 않는다
    }

    [Fact]
    public async Task should_close_without_asking_again_when_the_device_console_close_was_confirmed()
    {
        var (console, sink) = await OpenWithSinkAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { RowsOf(console)[0] });
        console.Detail.Tracker.Touch("name_device", "경광등 1", "고친 이름");

        await console.HandleAsync(new CallCloseDeviceConsoleMessageModel(), CancellationToken.None);

        Assert.Equal(1, sink.ClosePanels);            // [확인] — 닫기를 다시 청한다
        Assert.True(await console.CanCloseAsync());   // 이번에는 묻지 않는다
        Assert.Empty(sink.Confirms);
    }
    #endregion

    [Fact]
    public async Task should_list_groups_and_seven_categories_with_live_counts_when_opened()
    {
        var (console, _) = await OpenAsync();

        Assert.Equal(8, console.RailEntries.Count);   // 6.3 계약 — 부품으로 찾기는 나오지 않는다
        Assert.Equal(DeviceDashboardViewModel.GroupsRailKey, console.RailEntries[0].Key);
        Assert.Equal(DeviceDashboardViewModel.GroupsRailKey, console.SelectedRail!.Key);

        var lamps = console.RailEntries.Single(e => e.Key == LampRail);
        Assert.Equal((2, 1), (lamps.Count, lamps.BadCount));
        Assert.Contains("전체 3대", console.RailFooterText);
        Assert.Contains("장애 1대", console.RailFooterText);
    }

    [Fact]
    public async Task should_update_rail_badge_immediately_when_a_device_is_added()
    {
        var (console, _) = await OpenAsync();

        console.DeviceProvider.Add(new LampDeviceModel { Id = 13, DeviceNumber = 3, DeviceName = "경광등 3" });

        Assert.Equal(3, console.RailEntries.Single(e => e.Key == LampRail).Count);
    }

    [Fact]
    public async Task should_show_rows_and_columns_of_the_category_when_rail_is_switched()
    {
        var (console, _) = await OpenAsync();

        Assert.True(await console.SelectRailAsync(LampRail));

        Assert.Equal(EnumDeviceCategory.Lamp, console.Category);
        Assert.Equal(2, RowsOf(console).Count);
        Assert.NotEmpty(console.Columns);
        Assert.True(console.CanDragToGroup);
        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
    }

    [Fact]
    public async Task should_filter_rows_by_number_or_name_when_search_text_is_set()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        console.SearchText = "등 2";

        Assert.Single(RowsOf(console));
        Assert.Contains("전체 2", console.ListStatusText);
    }

    [Fact]
    public async Task should_load_form_when_a_row_is_selected()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        Assert.True(console.OnRowsSelected(RowsOf(console).Take(1).ToList()));

        Assert.Equal(ConsoleDetailState.Single, console.Detail.State);
        Assert.Equal("경광등 1", console.Form.Fields.Single(f => f.Key == "name_device").Text);
        Assert.True(console.CanDelete);
    }

    [Fact]
    public async Task should_block_selection_and_rail_change_when_form_has_unapplied_changes()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var rows = RowsOf(console);
        console.OnRowsSelected(rows.Take(1).ToList());
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";

        IReadOnlyList<object>? restored = null;
        console.SelectionRestoreRequested += (_, r) => restored = r;

        Assert.False(console.OnRowsSelected(rows.Skip(1).Take(1).ToList()));
        Assert.False(await console.SelectRailAsync(DeviceDashboardViewModel.GroupsRailKey));

        Assert.Equal(LampRail, console.SelectedRail!.Key);
        Assert.Same(rows[0], console.Form.Rows.Single());
        Assert.Same(rows[0], restored!.Single());            // 화면은 이 행으로 선택을 되돌린다
        Assert.Equal("바꾼 이름", console.Form.Fields.Single(f => f.Key == "name_device").Text);
    }

    [Fact]
    public async Task should_keep_list_untouched_until_register_when_add_is_pressed()
    {
        var (console, lamps) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        console.Add();

        Assert.Equal(ConsoleDetailState.Create, console.Detail.State);
        Assert.Equal(2, lamps.ViewModelProvider.Count);      // Draft 는 폼에만 물려 있다
        Assert.Equal("3", console.Form.Fields.Single(f => f.Key == "number_device").Text);   // 번호는 패널의 자동 배정을 그대로 쓴다

        console.Revert();

        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
        Assert.Empty(console.Form.Sections);
        Assert.Equal(2, lamps.ViewModelProvider.Count);      // [취소] = 아무것도 안 남는다
    }

    [Fact]
    public async Task should_not_write_or_save_when_apply_fails_validation()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var row = (LampDeviceViewModel)RowsOf(console)[0];
        console.OnRowsSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "  ";

        console.Apply();

        Assert.Equal("경광등 1", row.DeviceName);
        Assert.Equal(ConsoleDetailState.Dirty, console.Detail.State);   // 손댄 칸은 남는다
        Assert.True(console.Form.Fields.Single(f => f.Key == "name_device").HasError);
    }

    [Fact]
    public async Task should_write_touched_field_into_row_and_settle_when_applied()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var row = (LampDeviceViewModel)RowsOf(console)[0];
        console.OnRowsSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "정문 경광등";

        console.Apply();

        Assert.Equal("정문 경광등", row.DeviceName);   // 패널의 저장은 이 행을 서버 목록과 비교해 보낸다
        Assert.False(console.Detail.Tracker.IsDirty);
    }

    [Fact]
    public async Task should_drop_selection_and_unapplied_changes_when_closed()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(RowsOf(console).Take(1).ToList());
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";

        await ((IDeactivate)console).DeactivateAsync(close: false);

        Assert.Empty(console.Form.Sections);
        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
        Assert.Null(console.Rows);
    }

    #region - 조립기 입구 (FR-17 · FR-18) -
    [Fact]
    public async Task should_hide_every_assembly_entry_when_the_server_has_no_component_model()
    {
        var launcher = new FakeLauncher { IsAvailable = false };        // 6.3 운영
        var (console, _) = await OpenAsync(launcher: new Lazy<Consoles.Assembly.IAssemblyLauncher>(() => launcher));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(RowsOf(console).Take(1).ToList());

        Assert.False(console.CanAssemble);
        Assert.False(console.CanEditComponents);

        await console.OpenAssemblyAsync();
        await console.RegisterFromPresetAsync();
        await console.ManagePresetsAsync();
        await console.EditComponentsAsync();
        Assert.Equal(0, launcher.Calls);                                 // 가려져 있어도 부르면 열리는 길이 없어야 한다
    }

    [Fact]
    public async Task should_offer_component_editing_only_for_one_saved_device_without_pending_edits()
    {
        var launcher = new FakeLauncher { IsAvailable = true };
        var (console, _) = await OpenAsync(launcher: new Lazy<Consoles.Assembly.IAssemblyLauncher>(() => launcher));
        await console.SelectRailAsync(LampRail);
        var rows = RowsOf(console);

        Assert.True(console.CanAssemble);
        Assert.False(console.CanEditComponents);                         // 고른 것이 없다

        console.OnRowsSelected(rows.Take(1).ToList());
        Assert.True(console.CanEditComponents);

        console.OnRowsSelected(rows.Take(2).ToList());
        Assert.False(console.CanEditComponents);                         // 부품 구성은 한 대씩

        console.OnRowsSelected(rows.Take(1).ToList());
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";
        Assert.False(console.CanEditComponents);                         // 미적용 변경을 두고 다른 창으로 가지 않는다
    }

    [Fact]
    public async Task should_still_open_the_device_console_when_the_assembly_entry_cannot_be_built()
    {
        // 입구의 의존 하나가 컨테이너에서 안 풀려도 장비 창은 열려야 한다 — 입구만 감춘다(6.3 운영에서는 보이지도 않는 기능이다).
        var broken = new Lazy<Consoles.Assembly.IAssemblyLauncher>(() => throw new InvalidOperationException("resolution failed"));

        var (console, _) = await OpenAsync(launcher: broken);
        await console.SelectRailAsync(LampRail);

        Assert.False(console.CanAssemble);
        Assert.Equal(2, RowsOf(console).Count);
    }

    private sealed class FakeLauncher : Consoles.Assembly.IAssemblyLauncher
    {
        public bool IsAvailable { get; set; }
        public int Calls { get; private set; }
        public Task<int?> ComposeAsync(EnumDeviceCategory category) { Calls++; return Task.FromResult<int?>(null); }
        public Task<bool> EditDeviceAsync(IBaseDeviceModel device, EnumDeviceCategory category) { Calls++; return Task.FromResult(false); }
        public Task<int?> RegisterFromPresetAsync(EnumDeviceCategory category) { Calls++; return Task.FromResult<int?>(null); }
        public Task ManagePresetsAsync(EnumDeviceCategory category) { Calls++; return Task.CompletedTask; }
    }
    #endregion

    #region - 저장 · 등록이 끝난 뒤의 판정 (패널의 async void 경로 대신 각본대로 끝나는 원천을 쓴다) -
    private static LampDeviceViewModel Lamp(int id, string name) => new(new LampDeviceModel { Id = id, DeviceNumber = Math.Max(id, 1), DeviceName = name });

    [Fact]
    public async Task should_reselect_refetched_row_and_report_applied_when_server_kept_the_value()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "정문 경광등";

        console.Apply();
        Assert.Equal(1, source.SaveCalls);

        var refetched = Lamp(11, "정문 경광등");             // 재조회는 행 인스턴스를 새로 만든다
        source.ReplaceWith(refetched);
        source.Finish();

        Assert.Same(refetched, console.Form.Rows.Single());
        Assert.Equal(ConsoleDetailStateMachine.AppliedMessage(1, 1), console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_name_the_field_when_refetched_value_differs_from_what_was_written()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "정문 경광등";
        console.Apply();

        source.ReplaceWith(Lamp(11, "경광등 1"));             // 서버가 거절했다 — 재조회한 값은 옛 이름이다
        source.Finish();

        Assert.Contains("이름", console.Detail.LastMessage);
        Assert.Contains("값이 저장되지 않았습니다", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_stay_in_create_state_when_draft_is_still_unsaved_after_save()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.Add();
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "새로 단 경광등";

        console.Apply();
        Assert.Equal(2, source.Items.Count);                  // [등록] 때 비로소 목록에 들어간다
        source.Finish();                                      // 패널은 실패한 Draft(Id≤0)를 목록에 남긴다

        Assert.True(console.Detail.IsCreating);
        Assert.Contains("등록하지 못했습니다", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_select_the_new_row_when_draft_was_created_on_the_server()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.Add();
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "새로 단 경광등";
        console.Apply();

        var created = Lamp(42, "새로 단 경광등");
        source.ReplaceWith(Lamp(11, "경광등 1"), created);
        source.Finish();

        Assert.False(console.Detail.IsCreating);
        Assert.Same(created, console.Form.Rows.Single());
        Assert.Equal("등록했습니다.", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_select_the_new_row_as_soon_as_it_appears_before_the_panel_finishes()
    {
        // Arrange — GIS 실창 #16: 새 그룹이 목록에 떴는데도 상세가 "새 그룹 등록 · 아직 등록 전" 이었다
        // (패널은 저장 뒤 2초를 더 기다렸다가 끝남을 알린다).
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.Add();
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "새로 단 경광등";
        console.Apply();
        var created = Lamp(42, "새로 단 경광등");

        // Act — 끝남(Finish) 전에 목록만 바뀐다
        source.ReplaceWith(Lamp(11, "경광등 1"), created);

        // Assert
        Assert.False(console.Detail.IsCreating);
        Assert.Same(created, console.Form.Rows.Single());
        Assert.Equal("등록했습니다.", console.Detail.LastMessage);

        source.Finish();                                      // 늦게 온 끝남도 같은 행을 쥔다
        Assert.Same(created, console.Form.Rows.Single());
        Assert.False(console.IsOperationRunning);
    }

    [Fact]
    public async Task should_not_pick_an_unrelated_new_row_early_when_it_does_not_match_the_draft()
    {
        // Arrange
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.Add();
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "새로 단 경광등";
        console.Apply();

        // Act — 다른 곳에서 동시에 만든 행이 먼저 들어왔다(번호도 이름도 다르다)
        source.ReplaceWith(Lamp(11, "경광등 1"), Lamp(77, "남이 만든 경광등"));

        // Assert — 확실하지 않으면 끝남을 기다린다
        Assert.True(console.Detail.IsCreating);
    }

    [Fact]
    public async Task should_use_the_right_subject_particle_when_a_create_form_opens()
    {
        // Arrange — GIS 실창 #15: "새 그룹이(가) 만들어집니다". 그룹 레일은 진짜 그룹 패널이 [추가]를 작업 스레드에서
        // 끝내 시험 호스트에서는 교차 스레드가 된다 — 같은 문장 틀을 경광등 레일로 본다(그룹 자체는 조사 시험이 본다).
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        // Act
        console.Add();

        // Assert
        Assert.Equal(ConsoleDetailState.Create, console.Detail.State);
        Assert.Contains("새 경광등이 만들어집니다", console.Detail.CreateBanner);
        Assert.DoesNotContain("이(가)", console.Detail.CreateBanner);
    }

    [Fact]
    public async Task should_keep_the_component_button_in_place_but_disabled_when_a_field_is_touched()
    {
        // Arrange — GIS 실창 #13: 단추가 사라지면 폼 전체가 38px 튄다
        var launcher = new FakeLauncher { IsAvailable = true };
        var (console, _) = await OpenAsync(launcher: new Lazy<Consoles.Assembly.IAssemblyLauncher>(() => launcher));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(RowsOf(console).Take(1).ToList());
        Assert.True(console.IsEditComponentsVisible);
        Assert.Null(console.EditComponentsBlockedReason);
        var announced = new List<string?>();
        console.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        // Act
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";

        // Assert — 바인딩이 알아야 단추가 실제로 꺼진다(예전에는 적용 · 이동 때만 알렸다)
        Assert.Contains(nameof(DeviceDashboardViewModel.CanEditComponents), announced);
        Assert.True(console.IsEditComponentsVisible);
        Assert.False(console.CanEditComponents);
        Assert.False(string.IsNullOrWhiteSpace(console.EditComponentsBlockedReason));
    }

    [Fact]
    public async Task should_ignore_completion_of_another_rail_when_it_arrives_late()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        await console.SelectRailAsync(DeviceDashboardViewModel.GroupsRailKey);

        source.Finish();                                      // 떠난 레일의 끝남 — 지금 화면을 건드리면 안 된다

        Assert.Equal(DeviceDashboardViewModel.GroupsRailKey, console.SelectedRail!.Key);
        Assert.Empty(console.Form.Sections);
    }

    [Fact]
    public async Task should_keep_changes_and_leave_no_orphan_draft_when_panel_refuses_to_save()
    {
        // 패널은 권한이 없거나 다른 일을 하는 중이면 말없이 돌아온다 — 끝남도 오지 않는다.
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")) { RefuseSave = true });
        await console.SelectRailAsync(LampRail);
        console.Add();
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "새로 단 경광등";

        console.Apply();

        Assert.Single(source.Items);                           // 넣었던 Draft 를 도로 뺐다
        Assert.True(console.Detail.IsCreating);
        Assert.True(console.Detail.Tracker.IsDirty);           // 손댄 칸은 그대로
        Assert.False(console.IsOperationRunning);              // 걸어 둔 일이 없다 — 다음 끝남이 "등록했습니다."로 읽히지 않는다

        source.Finish();
        Assert.True(console.Detail.IsCreating);
        Assert.DoesNotContain("등록했습니다.", console.Detail.LastMessage ?? string.Empty);
    }

    [Fact]
    public async Task should_not_wait_for_anything_when_delete_is_requested()
    {
        // 삭제는 확인 팝업부터 뜬다 — 취소하면 끝남이 영영 오지 않으므로 걸어 두면 안 된다.
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1"), Lamp(12, "경광등 2")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });

        console.Delete();

        Assert.Equal(1, source.DeleteCalls);
        Assert.False(console.IsOperationRunning);
        Assert.True(console.OnRowsSelected(new List<object> { source.Items[1] }));   // 취소한 뒤에도 콘솔은 평소대로 움직인다
    }

    [Fact]
    public async Task should_drop_vanished_rows_from_selection_when_list_changes_without_a_pending_operation()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1"), Lamp(12, "경광등 2")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0], source.Items[1] });

        var survivor = Lamp(12, "경광등 2");
        source.ReplaceWith(survivor);                          // 확인된 삭제 → 패널이 다시 읽었다
        source.Finish();

        Assert.Same(survivor, console.Form.Rows.Single());
        Assert.Contains("1대가 목록에서 사라졌습니다", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_rebind_rows_and_keep_typed_text_when_list_is_refetched_while_dirty()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "쓰던 이름";

        var refetched = Lamp(11, "경광등 1");
        source.ReplaceWith(refetched);
        source.Finish();

        Assert.Same(refetched, console.Form.Rows.Single());    // 옛 인스턴스에 쓰면 저장 경로가 그 값을 못 본다
        Assert.Equal("쓰던 이름", console.Form.Fields.Single(f => f.Key == "name_device").Text);
        Assert.True(console.Detail.Tracker.IsDirty);
    }

    [Fact]
    public async Task should_refuse_navigation_and_commands_when_an_operation_is_running()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1"), Lamp(12, "경광등 2")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "정문 경광등";
        console.Apply();                                       // 끝남이 오기 전

        Assert.True(console.IsOperationRunning);
        Assert.False(console.OnRowsSelected(new List<object> { source.Items[1] }));
        Assert.False(await console.SelectRailAsync(DeviceDashboardViewModel.GroupsRailKey));
        Assert.False(console.CanAdd);
        Assert.False(console.CanDelete);

        console.Apply();
        Assert.Equal(1, source.SaveCalls);                     // 두 번 누른다고 두 번 저장하지 않는다
    }

    [Fact]
    public async Task should_follow_actual_grid_selection_when_restored_rows_are_hidden_by_search()
    {
        ScriptedSource source = null!;
        var (console, _) = await OpenAsync(panel => source = new ScriptedSource(panel, Lamp(11, "경광등 1"), Lamp(12, "경광등 2")));
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { source.Items[0] });

        console.NarrowSelectionTo(new List<object>());         // 검색에 가려져 그리드에는 아무것도 안 골라졌다

        Assert.Empty(console.Form.Rows);
        Assert.False(console.CanDelete);                       // 눈에 안 보이는 장비를 지우지 않는다
    }

    /// <summary>끝남을 시험이 정한다 — 진짜 패널은 활성화 수명주기용으로만 물려 둔다.</summary>
    private sealed class ScriptedSource : IDeviceConsoleSource
    {
        public ScriptedSource(BasePanelViewModel panel, params LampDeviceViewModel[] rows)
        {
            Panel = panel;
            foreach (var row in rows) Items.Add(row);
        }

        public ObservableCollection<LampDeviceViewModel> Items { get; } = new();
        public int SaveCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public bool RefuseSave { get; set; }

        public BasePanelViewModel Panel { get; }
        public IEnumerable Rows => Items;
        public INotifyCollectionChanged RowsChanged => Items;
        public int RowCount => Items.Count;
        public bool IsBusy => false;
        public event EventHandler? BusyEnded;

        public void Select(IReadOnlyList<object> rows) { }
        public object? CreateDraft() => new LampDeviceViewModel(new LampDeviceModel { DeviceNumber = 9, DeviceName = "새 경광등 9" });
        public void AdoptDraft(object draft) => Items.Add((LampDeviceViewModel)draft);
        public void ReleaseDraft(object draft) => Items.Remove((LampDeviceViewModel)draft);
        public bool Save() { SaveCalls++; return !RefuseSave; }
        public void Delete() => DeleteCalls++;
        public bool Reload() => true;

        public void ReplaceWith(params LampDeviceViewModel[] rows)
        {
            Items.Clear();
            foreach (var row in rows) Items.Add(row);
        }

        public void Finish() => BusyEnded?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    private sealed class StubCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
}
